#!/usr/bin/env python3
"""
Rasterises the move pictograms and lane frames (SVG sources) to PNG. Standard library only.

  python tools/art/pictograms.py            # all SVGs in TabletopTempo/Assets/_Project/Art/Pictograms/src
  python tools/art/pictograms.py --size 256

Supported SVG subset (enough for flat, high-contrast icons):
  <svg viewBox="0 0 W H">, <g transform=...>, <circle>, <ellipse>, <rect rx>, <line>, <polyline>,
  <polygon>; attributes fill, stroke, stroke-width, opacity (also inherited from <g>);
  transforms translate(), rotate(a [cx cy]), scale(s [sy]) — composed left to right.
Rendering uses signed distance fields with analytic anti-aliasing (one sample per pixel).
All artwork is original (MIT). Output: Art/Pictograms/<name>.png (RGBA, Git LFS).
"""
import argparse
import math
import os
import re
import struct
import sys
import xml.etree.ElementTree as ET
import zlib

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(REPO, "TabletopTempo", "Assets", "_Project", "Art", "Pictograms", "src")
OUT = os.path.join(REPO, "TabletopTempo", "Assets", "_Project", "Art", "Pictograms")

NAMED = {"white": (255, 255, 255), "black": (0, 0, 0), "none": None}


# ----------------------------------------------------------------------------- affine 2D

def mat_mul(a, b):
    # 3x3 affine as (a, b, c, d, e, f):  x' = a x + c y + e ; y' = b x + d y + f
    a0, a1, a2, a3, a4, a5 = a
    b0, b1, b2, b3, b4, b5 = b
    return (a0 * b0 + a2 * b1, a1 * b0 + a3 * b1, a0 * b2 + a2 * b3, a1 * b2 + a3 * b3,
            a0 * b4 + a2 * b5 + a4, a1 * b4 + a3 * b5 + a5)


def mat_inv(m):
    a, b, c, d, e, f = m
    det = a * d - b * c
    ia, ib, ic, id_ = d / det, -b / det, -c / det, a / det
    return (ia, ib, ic, id_, -(ia * e + ic * f), -(ib * e + id_ * f))


def apply(m, x, y):
    return m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5]


IDENT = (1.0, 0.0, 0.0, 1.0, 0.0, 0.0)


def parse_transform(s):
    m = IDENT
    for name, args in re.findall(r"(\w+)\s*\(([^)]*)\)", s or ""):
        v = [float(x) for x in re.split(r"[\s,]+", args.strip()) if x]
        if name == "translate":
            t = (1, 0, 0, 1, v[0], v[1] if len(v) > 1 else 0)
        elif name == "scale":
            sx = v[0]
            sy = v[1] if len(v) > 1 else sx
            t = (sx, 0, 0, sy, 0, 0)
        elif name == "rotate":
            a = math.radians(v[0])
            ca, sa = math.cos(a), math.sin(a)
            t = (ca, sa, -sa, ca, 0, 0)
            if len(v) == 3:
                t = mat_mul(mat_mul((1, 0, 0, 1, v[1], v[2]), t), (1, 0, 0, 1, -v[1], -v[2]))
        else:
            raise ValueError("unsupported transform " + name)
        m = mat_mul(m, t)
    return m


def parse_color(s):
    if s is None:
        return None
    s = s.strip()
    if s in NAMED:
        return NAMED[s]
    if s.startswith("#"):
        h = s[1:]
        if len(h) == 3:
            h = "".join(c * 2 for c in h)
        return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
    raise ValueError("unsupported colour " + s)


# ----------------------------------------------------------------------------- SDF primitives (local units)

def sd_segment(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / l2))
    qx, qy = ax + t * dx - px, ay + t * dy - py
    return math.hypot(qx, qy)


def sd_round_rect(px, py, x, y, w, h, r):
    cx, cy = x + w / 2, y + h / 2
    qx = abs(px - cx) - (w / 2 - r)
    qy = abs(py - cy) - (h / 2 - r)
    return math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - r


def sd_polygon(px, py, pts):
    d = float("inf")
    inside = False
    n = len(pts)
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        d = min(d, sd_segment(px, py, ax, ay, bx, by))
        if (ay > py) != (by > py) and px < (bx - ax) * (py - ay) / (by - ay + 1e-12) + ax:
            inside = not inside
    return -d if inside else d


class Shape:
    def __init__(self, kind, params, fill, stroke, width, opacity, m):
        self.kind, self.p, self.fill, self.stroke, self.width, self.opacity = kind, params, fill, stroke, width, opacity
        self.m = m
        self.inv = mat_inv(m)
        self.scale = math.sqrt(abs(m[0] * m[3] - m[1] * m[2]))  # local unit → output pixels

    def fill_sdf(self, x, y):
        k, p = self.kind, self.p
        if k == "circle":
            return math.hypot(x - p[0], y - p[1]) - p[2]
        if k == "ellipse":
            # approximate: scale space to unit circle (fine for icons)
            nx, ny = (x - p[0]) / p[2], (y - p[1]) / p[3]
            return (math.hypot(nx, ny) - 1) * min(p[2], p[3])
        if k == "rect":
            return sd_round_rect(x, y, *p)
        if k == "polygon":
            return sd_polygon(x, y, p)
        return float("inf")

    def stroke_dist(self, x, y):
        k, p = self.kind, self.p
        if k == "line":
            return sd_segment(x, y, *p)
        if k == "polyline":
            return min(sd_segment(x, y, p[i][0], p[i][1], p[i + 1][0], p[i + 1][1]) for i in range(len(p) - 1))
        return abs(self.fill_sdf(x, y))

    def bbox_local(self):
        k, p = self.kind, self.p
        pad = self.width
        if k == "circle":
            return p[0] - p[2] - pad, p[1] - p[2] - pad, p[0] + p[2] + pad, p[1] + p[2] + pad
        if k == "ellipse":
            return p[0] - p[2] - pad, p[1] - p[3] - pad, p[0] + p[2] + pad, p[1] + p[3] + pad
        if k == "rect":
            return p[0] - pad, p[1] - pad, p[0] + p[2] + pad, p[1] + p[3] + pad
        if k == "line":
            return min(p[0], p[2]) - pad, min(p[1], p[3]) - pad, max(p[0], p[2]) + pad, max(p[1], p[3]) + pad
        xs = [q[0] for q in p]
        ys = [q[1] for q in p]
        return min(xs) - pad, min(ys) - pad, max(xs) + pad, max(ys) + pad


def points(s):
    v = [float(x) for x in re.split(r"[\s,]+", s.strip()) if x]
    return [(v[i], v[i + 1]) for i in range(0, len(v) - 1, 2)]


def collect(el, m, style, shapes):
    tag = el.tag.split("}")[-1]
    st = dict(style)
    for a in ("fill", "stroke", "stroke-width", "opacity"):
        if a in el.attrib:
            st[a] = el.attrib[a]
    if "transform" in el.attrib:
        m = mat_mul(m, parse_transform(el.attrib["transform"]))
    f = lambda k, d=0.0: float(el.attrib.get(k, d))
    params = None
    if tag == "circle":
        params = (f("cx"), f("cy"), f("r"))
    elif tag == "ellipse":
        params = (f("cx"), f("cy"), f("rx"), f("ry"))
    elif tag == "rect":
        w, h = f("width"), f("height")
        params = (f("x"), f("y"), w, h, min(f("rx"), w / 2, h / 2))
    elif tag == "line":
        params = (f("x1"), f("y1"), f("x2"), f("y2"))
    elif tag in ("polyline", "polygon"):
        params = points(el.attrib["points"])
    if params is not None:
        fill = parse_color(st.get("fill", "black")) if tag not in ("line", "polyline") else None
        stroke = parse_color(st.get("stroke", "none"))
        shapes.append(Shape(tag, params, fill, stroke, float(st.get("stroke-width", 0)), float(st.get("opacity", 1)), m))
    for child in el:
        collect(child, m, st, shapes)


def render(svg_path, size):
    root = ET.parse(svg_path).getroot()
    vb = [float(v) for v in root.attrib.get("viewBox", "0 0 100 100").split()]
    s = size / max(vb[2], vb[3])
    base = (s, 0, 0, s, -vb[0] * s, -vb[1] * s)
    shapes = []
    collect(root, base, {}, shapes)

    img = [[0.0, 0.0, 0.0, 0.0] for _ in range(size * size)]  # premultiplied RGBA floats
    for sh in shapes:
        x0, y0, x1, y1 = sh.bbox_local()
        corners = [apply(sh.m, x, y) for x, y in ((x0, y0), (x1, y0), (x0, y1), (x1, y1))]
        px0 = max(0, int(min(c[0] for c in corners)) - 1)
        py0 = max(0, int(min(c[1] for c in corners)) - 1)
        px1 = min(size, int(max(c[0] for c in corners)) + 2)
        py1 = min(size, int(max(c[1] for c in corners)) + 2)
        for py in range(py0, py1):
            for px in range(px0, px1):
                lx, ly = apply(sh.inv, px + 0.5, py + 0.5)
                for colour, cov in layers(sh, lx, ly):
                    if cov <= 0:
                        continue
                    a = cov * sh.opacity
                    dst = img[py * size + px]
                    r, g, b = (c / 255.0 for c in colour)
                    inv = 1 - a
                    dst[0] = r * a + dst[0] * inv
                    dst[1] = g * a + dst[1] * inv
                    dst[2] = b * a + dst[2] * inv
                    dst[3] = a + dst[3] * inv
    return img


def layers(sh, lx, ly):
    out = []
    if sh.fill is not None:
        d = sh.fill_sdf(lx, ly) * sh.scale
        out.append((sh.fill, max(0.0, min(1.0, 0.5 - d))))
    if sh.stroke is not None and sh.width > 0:
        d = (sh.stroke_dist(lx, ly) - sh.width / 2) * sh.scale
        out.append((sh.stroke, max(0.0, min(1.0, 0.5 - d))))
    return out


def write_png(path, img, size):
    raw = bytearray()
    for y in range(size):
        raw.append(0)
        for x in range(size):
            r, g, b, a = img[y * size + x]
            if a > 1e-6:  # un-premultiply
                r, g, b = r / a, g / a, b / a
            raw += bytes((int(min(1, r) * 255 + 0.5), int(min(1, g) * 255 + 0.5),
                          int(min(1, b) * 255 + 0.5), int(min(1, a) * 255 + 0.5)))

    def chunk(t, data):
        c = struct.pack(">I", len(data)) + t + data
        return c + struct.pack(">I", zlib.crc32(t + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("names", nargs="*", help="svg base names (default: all)")
    ap.add_argument("--size", type=int, default=256)
    args = ap.parse_args(argv)
    files = sorted(f for f in os.listdir(SRC) if f.endswith(".svg"))
    if args.names:
        files = [f for f in files if f[:-4] in args.names]
    for f in files:
        img = render(os.path.join(SRC, f), args.size)
        out = os.path.join(OUT, f[:-4] + ".png")
        write_png(out, img, args.size)
        print(f"{f} -> {os.path.relpath(out, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
