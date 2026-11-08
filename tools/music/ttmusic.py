#!/usr/bin/env python3
"""
Tabletop Tempo music + chart generator (standard library only).

One pattern file (tools/music/patterns/*.json) describes a stage: tempo map, chords, drum and bass
patterns, and the moves the player performs. This tool renders BOTH from it:

  * an original backing track  -> TabletopTempo/Assets/_Project/Audio/Music/<id>.wav (44.1 kHz, 16-bit stereo)
  * the matching starter chart -> TabletopTempo/Assets/StreamingAssets/Charts/<id>.json

so music and chart can never drift apart. All output is original work (MIT, see THIRD_PARTY_NOTICES.md).

Usage:
  python tools/music/ttmusic.py all            # every pattern file
  python tools/music/ttmusic.py stage1_first_claps
  python tools/music/ttmusic.py --chart-only all

Pattern step strings: one character per 16th note of a bar (stepsPerBar = beatsPerBar * 4).
  '.' rest   'x' hit   'X' accented hit   (bass: '-' sustains the previous note)
Replacing a track later: drop your own WAV (or a verified CC0 recording) at the same path and set
"offsetSeconds"/tempo map in the chart so beat 0 matches the first downbeat. See docs/MUSIC.md.
"""
import argparse
import json
import math
import os
import random
import struct
import sys
import wave

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
PATTERNS = os.path.join(HERE, "patterns")
OUT_AUDIO = os.path.join(REPO, "TabletopTempo", "Assets", "_Project", "Audio", "Music")
OUT_CHARTS = os.path.join(REPO, "TabletopTempo", "Assets", "StreamingAssets", "Charts")

LEAD_IN = 0.10  # seconds of silence before beat 0 (avoids a clipped first transient)

NOTE_NAMES = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6,
              "Gb": 6, "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}
CHORD_SHAPES = {"": (0, 4, 7), "m": (0, 3, 7), "7": (0, 4, 7, 10), "m7": (0, 3, 7, 10),
                "maj7": (0, 4, 7, 11), "sus2": (0, 2, 7), "sus4": (0, 5, 7)}


def midi_hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def parse_chord(name, octave_root=48):
    """'Am7' -> (root midi, [midi notes])."""
    root = name[:2] if len(name) > 1 and name[1] in "#b" else name[:1]
    quality = name[len(root):]
    if root not in NOTE_NAMES or quality not in CHORD_SHAPES:
        raise ValueError(f"Unknown chord '{name}'")
    r = octave_root + NOTE_NAMES[root]
    return r, [r + i for i in CHORD_SHAPES[quality]]


# --------------------------------------------------------------------------- tempo map

class TempoMap:
    """Piecewise-constant tempo. segments: list of (start_beat, bpm)."""

    def __init__(self, segments):
        self.segments = sorted(segments)
        self._starts = []
        t = 0.0
        for i, (b, bpm) in enumerate(self.segments):
            if i > 0:
                pb, pbpm = self.segments[i - 1]
                t += (b - pb) * 60.0 / pbpm
            self._starts.append(t)

    def seconds(self, beat):
        idx = 0
        for i, (b, _) in enumerate(self.segments):
            if beat >= b:
                idx = i
        b0, bpm = self.segments[idx]
        return self._starts[idx] + (beat - b0) * 60.0 / bpm

    def bpm_at(self, beat):
        cur = self.segments[0][1]
        for b, bpm in self.segments:
            if beat >= b:
                cur = bpm
        return cur


# --------------------------------------------------------------------------- synth voices

class Rng:
    def __init__(self, seed):
        self.r = random.Random(seed)

    def noise(self):
        return self.r.random() * 2.0 - 1.0


def env_exp(i, decay_s):
    return math.exp(-i / (decay_s * SR))


def one_pole_lp(buf, cutoff):
    a = 1.0 - math.exp(-2.0 * math.pi * cutoff / SR)
    y = 0.0
    out = [0.0] * len(buf)
    for i, x in enumerate(buf):
        y += a * (x - y)
        out[i] = y
    return out


def one_pole_hp(buf, cutoff):
    lp = one_pole_lp(buf, cutoff)
    return [x - l for x, l in zip(buf, lp)]


def kick(rng):
    n = int(0.42 * SR)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 45.0 + 85.0 * math.exp(-t * 28.0)
        phase += 2 * math.pi * f / SR
        out[i] = math.sin(phase) * env_exp(i, 0.20)
    for i in range(int(0.004 * SR)):          # beater click
        out[i] += 0.35 * rng.noise() * (1 - i / (0.004 * SR))
    return out


def snare(rng):
    n = int(0.25 * SR)
    noise = one_pole_hp([rng.noise() for _ in range(n)], 900.0)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        phase += 2 * math.pi * 185.0 / SR
        out[i] = 0.55 * noise[i] * env_exp(i, 0.07) + 0.45 * math.sin(phase) * env_exp(i, 0.05)
    return out


def clap(rng):
    n = int(0.28 * SR)
    noise = one_pole_lp(one_pole_hp([rng.noise() for _ in range(n)], 700.0), 3500.0)
    out = [0.0] * n
    bursts = (0.0, 0.011, 0.022)
    for i in range(n):
        t = i / SR
        e = 0.0
        for b in bursts:
            if t >= b:
                e = max(e, math.exp(-(t - b) * 180.0))
        if t >= bursts[-1]:
            e = max(e, 0.6 * math.exp(-(t - bursts[-1]) * 18.0))
        out[i] = noise[i] * e * 1.6
    return out


def hat(rng, open_hat=False):
    n = int((0.30 if open_hat else 0.06) * SR)
    noise = one_pole_hp(one_pole_hp([rng.noise() for _ in range(n)], 6000.0), 6000.0)
    d = 0.09 if open_hat else 0.018
    return [noise[i] * env_exp(i, d) * 1.4 for i in range(n)]


def stomp(rng):
    """Big low 'stadium' thump: layered low sine + filtered noise body."""
    n = int(0.55 * SR)
    body = one_pole_lp([rng.noise() for _ in range(n)], 400.0)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 38.0 + 60.0 * math.exp(-t * 20.0)
        phase += 2 * math.pi * f / SR
        out[i] = 0.9 * math.sin(phase) * env_exp(i, 0.26) + 1.4 * body[i] * env_exp(i, 0.09)
    return out


def tick(rng, freq=1800.0, decay=0.02):
    n = int(0.08 * SR)
    return [math.sin(2 * math.pi * freq * i / SR) * env_exp(i, decay) for i in range(n)]


def bass_note(midi, length_s, cutoff=700.0):
    n = int(length_s * SR)
    f = midi_hz(midi)
    raw = [0.0] * n
    p1 = p2 = 0.0
    for i in range(n):
        p1 = (p1 + f / SR) % 1.0
        p2 = (p2 + f * 1.004 / SR) % 1.0
        saw = (2 * p1 - 1) * 0.5 + (2 * p2 - 1) * 0.5
        sub = math.sin(2 * math.pi * p1)
        a = min(1.0, i / (0.004 * SR))
        r = min(1.0, (n - i) / (0.02 * SR))
        raw[i] = (0.55 * saw + 0.6 * sub) * a * r * (0.65 + 0.35 * env_exp(i, 0.25))
    return one_pole_lp(raw, cutoff)


def pad_chord(notes, length_s):
    n = int(length_s * SR)
    out = [0.0] * n
    for m in notes:
        for det in (0.997, 1.003):
            f = midi_hz(m + 12) * det
            p = 0.0
            for i in range(n):
                p = (p + f / SR) % 1.0
                out[i] += (2 * p - 1)
    scale = 0.10 / max(1, len(notes))
    att, rel = 0.25 * SR, 0.35 * SR
    for i in range(n):
        out[i] *= scale * min(1.0, i / att) * min(1.0, (n - i) / rel)
    return one_pole_lp(one_pole_lp(out, 1400.0), 2200.0)


def pluck(midi, length_s):
    n = int(length_s * SR)
    f = midi_hz(midi)
    out = [0.0] * n
    for i in range(n):
        t = i / SR
        s = math.sin(2 * math.pi * f * t) + 0.35 * math.sin(4 * math.pi * f * t) + 0.12 * math.sin(6 * math.pi * f * t)
        out[i] = s * env_exp(i, 0.18) * min(1.0, i / (0.002 * SR))
    return out


# --------------------------------------------------------------------------- mixer

class Mix:
    def __init__(self, seconds):
        self.n = int(seconds * SR) + SR
        self.l = [0.0] * self.n
        self.r = [0.0] * self.n

    def add(self, buf, t, gain=1.0, pan=0.0):
        start = int(t * SR)
        if start >= self.n:
            return
        gl = gain * math.cos((pan + 1) * math.pi / 4) * math.sqrt(2)
        gr = gain * math.sin((pan + 1) * math.pi / 4) * math.sqrt(2)
        end = min(self.n, start + len(buf))
        L, R = self.l, self.r
        for i in range(start, end):
            v = buf[i - start]
            L[i] += v * gl
            R[i] += v * gr

    def write(self, path, length_s):
        n = min(self.n, int(length_s * SR))
        peak = max(1e-9, max(max(abs(x) for x in self.l[:n]), max(abs(x) for x in self.r[:n])))
        drive = 1.0 / peak * 1.2
        target = 10 ** (-1.0 / 20)  # -1 dBFS
        norm = target / math.tanh(1.2)
        fade = int(0.5 * SR)
        frames = bytearray()
        for i in range(n):
            f = min(1.0, (n - i) / fade)
            l = math.tanh(self.l[i] * drive) * norm * f
            r = math.tanh(self.r[i] * drive) * norm * f
            frames += struct.pack("<hh", int(l * 32767), int(r * 32767))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with wave.open(path, "wb") as w:
            w.setnchannels(2)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(bytes(frames))


# --------------------------------------------------------------------------- pattern → song

def steps(pattern):
    return [(i, c) for i, c in enumerate(pattern) if c in "xX"]


def build(pattern, chart_only=False):
    pid = pattern["id"]
    bpb = pattern.get("beatsPerBar", 4)
    spb = bpb * 4                       # 16th-note steps per bar
    sections = pattern["sections"]
    rng = Rng(pattern.get("seed", 1234))

    # Tempo map from sections (each section may set "bpm").
    tempo_segments, bar = [], 0
    base_bpm = pattern["bpm"]
    for s in sections:
        bpm = s.get("bpm", base_bpm)
        if not tempo_segments or tempo_segments[-1][1] != bpm:
            tempo_segments.append((bar * bpb, bpm))
        bar += s["bars"]
    total_bars = bar
    tm = TempoMap(tempo_segments)
    total_beats = total_bars * bpb
    length_s = LEAD_IN + tm.seconds(total_beats) + 1.5

    def t_of(beat):
        return LEAD_IN + tm.seconds(beat)

    chart_notes, chart_sections = [], []
    mix = None if chart_only else Mix(length_s)
    voices = {}
    if mix is not None:
        voices = {"kick": kick(rng), "snare": snare(rng), "clap": clap(rng), "hat": hat(rng),
                  "ohat": hat(rng, True), "stomp": stomp(rng), "click": tick(rng, 1800.0),
                  "clickHi": tick(rng, 2600.0)}
    gains = {"kick": 0.95, "snare": 0.55, "clap": 0.50, "hat": 0.22, "ohat": 0.18, "stomp": 1.0,
             "click": 0.45, "clickHi": 0.55}
    pans = {"hat": 0.25, "ohat": 0.25, "clap": -0.1}
    chords = pattern.get("chords", ["C"])
    bass_cache, pad_cache = {}, {}

    bar = 0
    for s in sections:
        start_beat = bar * bpb
        chart_sections.append({"name": s["name"], "startBeat": start_beat, "bars": s["bars"],
                               "bpm": s.get("bpm", base_bpm), "phase": s.get("phase", "play"),
                               "coach": s.get("coach", "")})
        for b in range(s["bars"]):
            bar_beat = (bar + b) * bpb
            bar_bpm = tm.bpm_at(bar_beat)
            step_s = 60.0 / bar_bpm / 4.0

            # ---- chart
            for entry in pattern.get("chartPatterns", {}).get(s.get("chart") or "", []):
                for i, c in steps(entry["steps"]):
                    note = {"beat": round(bar_beat + i / 4.0, 4), "move": entry["move"],
                            "hand": entry.get("hand", "both")}
                    if entry.get("durationBeats"):
                        note["duration"] = entry["durationBeats"]
                    chart_notes.append(note)

            if mix is None:
                continue

            # ---- drums
            if s.get("drums") == "countin":
                for k in range(bpb):
                    mix.add(voices["clickHi" if k == 0 else "click"], t_of(bar_beat + k), gains["click"])
            else:
                for name, pat in pattern.get("drumPatterns", {}).get(s.get("drums") or "", {}).items():
                    for i, c in steps(pat):
                        g = gains[name] * (1.0 if c == "X" else 0.8)
                        mix.add(voices[name], t_of(bar_beat + i / 4.0), g, pans.get(name, 0.0))

            # ---- harmony (one chord per bar, cycling)
            chord = chords[(bar + b) % len(chords)]
            root, notes = parse_chord(chord, pattern.get("octaveRoot", 48))
            if s.get("pad", True):
                key = (chord, round(bar_bpm, 3))
                if key not in pad_cache:
                    pad_cache[key] = pad_chord(notes, bpb * 60.0 / bar_bpm + 0.4)
                mix.add(pad_cache[key], t_of(bar_beat), 1.0, 0.0)

            bass_pat = pattern.get("bassPatterns", {}).get(s.get("bass") or "")
            if bass_pat:
                i = 0
                while i < len(bass_pat):
                    c = bass_pat[i]
                    if c in "xX":
                        j = i + 1
                        while j < len(bass_pat) and bass_pat[j] == "-":
                            j += 1
                        dur = (j - i) * step_s * 0.92
                        key = (root - 12, round(dur, 4))
                        if key not in bass_cache:
                            bass_cache[key] = bass_note(root - 12, dur)
                        mix.add(bass_cache[key], t_of(bar_beat + i / 4.0), 0.55 if c == "x" else 0.7)
                        i = j
                    else:
                        i += 1

            mel = pattern.get("melodyPatterns", {}).get(s.get("melody") or "")
            if mel:
                degrees = mel["degrees"]
                k = 0
                for i, c in steps(mel["steps"]):
                    m = notes[degrees[k % len(degrees)] % len(notes)] + 12 * (1 + degrees[k % len(degrees)] // len(notes))
                    mix.add(pluck(m, 0.5), t_of(bar_beat + i / 4.0), 0.22, 0.15)
                    k += 1
        bar += s["bars"]

    chart = {
        "id": pid,
        "title": pattern["title"],
        "audio": f"Music/{pid}",
        "offsetSeconds": LEAD_IN,
        "beatsPerBar": bpb,
        "tempo": [{"beat": b, "bpm": bpm} for b, bpm in tm.segments],
        "lengthBeats": total_beats,
        "sections": chart_sections,
        "notes": sorted(chart_notes, key=lambda n: (n["beat"], n["move"])),
        "generator": "tools/music/ttmusic.py (original work, MIT)",
    }
    os.makedirs(OUT_CHARTS, exist_ok=True)
    with open(os.path.join(OUT_CHARTS, pid + ".json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(chart, f, indent=1)
        f.write("\n")

    if mix is not None:
        mix.write(os.path.join(OUT_AUDIO, pid + ".wav"), length_s)
    return chart, length_s


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("targets", nargs="+", help="pattern ids, or 'all'")
    ap.add_argument("--chart-only", action="store_true", help="write charts, skip audio rendering")
    args = ap.parse_args(argv)

    files = sorted(f for f in os.listdir(PATTERNS) if f.endswith(".json"))
    ids = [f[:-5] for f in files] if "all" in args.targets else args.targets
    for pid in ids:
        path = os.path.join(PATTERNS, pid + ".json")
        if not os.path.exists(path):
            print(f"no pattern file: {path}", file=sys.stderr)
            return 1
        with open(path, encoding="utf-8") as f:
            pattern = json.load(f)
        chart, length_s = build(pattern, chart_only=args.chart_only)
        print(f"{pid}: {len(chart['notes'])} notes, {chart['lengthBeats']} beats, {length_s:.1f}s"
              + ("" if args.chart_only else " (wav written)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
