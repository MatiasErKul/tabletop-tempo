#!/usr/bin/env python3
"""
Installs the developer-only webcam hand tracking (MediaPipe) into the Unity project.

  python tools/webcam/fetch_mediapipe.py

What it does (idempotent):
  1. Downloads the MediaPipe Unity Plugin package (homuler, MIT) from its GitHub release and
     verifies the SHA-256 GitHub publishes for it.
  2. Extracts it (safe tar filter) as an *embedded package* at
     TabletopTempo/Packages/com.github.homuler.mediapipe  (gitignored; Unity picks it up automatically).
  3. Removes the Android / iOS native libraries and restricts every assembly in the package to
     Editor + desktop, so nothing of it can ever end up in the Quest APK.
  4. Downloads Google's HandLandmarker model (Apache 2.0) to TabletopTempo/WebcamModels/
     (gitignored, outside Assets so it is never built into a player).

Nothing here is needed to build or run the game on a headset. A clone without it still compiles:
the webcam assembly only compiles when the package is present.
"""
import hashlib
import json
import os
import shutil
import sys
import tarfile
import tempfile
import urllib.request

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PROJECT = os.path.join(REPO, "TabletopTempo")
DL_DIR = os.path.join(REPO, "tools", "webcam", "_downloads")

PLUGIN_VERSION = "0.16.3"
PLUGIN_NAME = "com.github.homuler.mediapipe"
PLUGIN_URL = (f"https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v{PLUGIN_VERSION}/"
              f"{PLUGIN_NAME}-{PLUGIN_VERSION}.tgz")
PLUGIN_SHA256 = "cc3e77a219e0b99618ae3be64c31a566197deedc69c1e136acf52d65d7cf2e79"  # from the GitHub release

MODEL_URL = ("https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/"
             "float16/latest/hand_landmarker.task")
MODEL_SIZE = 7819105
# Pinned on first download (8 Nov 2026); Google publishes no hash for this file.
MODEL_SHA256 = "fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1"

PACKAGE_DIR = os.path.join(PROJECT, "Packages", PLUGIN_NAME)
MODEL_DIR = os.path.join(PROJECT, "WebcamModels")

DESKTOP_PLATFORMS = ["Editor", "WindowsStandalone64", "macOSStandalone", "LinuxStandalone64"]


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def download(url, dest):
    if os.path.exists(dest):
        return dest
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    tmp = dest + ".part"
    print(f"downloading {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "tabletop-tempo-fetch/1.0"})
    with urllib.request.urlopen(req, timeout=60) as r, open(tmp, "wb") as f:
        total = int(r.headers.get("Content-Length") or 0)
        done = 0
        while True:
            chunk = r.read(1 << 20)
            if not chunk:
                break
            f.write(chunk)
            done += len(chunk)
            if total and done % (25 << 20) < (1 << 20):
                print(f"  {done >> 20} / {total >> 20} MB")
    os.replace(tmp, dest)
    return dest


def install_plugin():
    tgz = download(PLUGIN_URL, os.path.join(DL_DIR, os.path.basename(PLUGIN_URL)))
    got = sha256(tgz)
    if got != PLUGIN_SHA256:
        os.remove(tgz)
        sys.exit(f"SHA-256 mismatch for plugin: got {got}, expected {PLUGIN_SHA256} (file deleted)")
    print("plugin SHA-256 OK")

    marker = os.path.join(PACKAGE_DIR, ".tt-installed")
    if os.path.exists(marker) and open(marker).read().strip() == PLUGIN_VERSION:
        print("plugin already extracted")
    else:
        with tempfile.TemporaryDirectory(dir=os.path.join(REPO, "tools", "webcam")) as tmp:
            with tarfile.open(tgz, "r:gz") as tf:
                tf.extractall(tmp, filter="data")  # blocks absolute paths, '..', links outside
            src = os.path.join(tmp, "package")
            if not os.path.isfile(os.path.join(src, "package.json")):
                sys.exit("unexpected archive layout (no package/package.json)")
            if os.path.exists(PACKAGE_DIR):
                shutil.rmtree(PACKAGE_DIR)
            shutil.move(src, PACKAGE_DIR)

    # Post-processing is idempotent and always runs (also repairs an older install).
    strip_mobile(PACKAGE_DIR)
    remove_package_tests(PACKAGE_DIR)
    restrict_assemblies(PACKAGE_DIR)
    restrict_managed_plugins(PACKAGE_DIR)
    with open(marker, "w") as f:
        f.write(PLUGIN_VERSION)
    print(f"plugin installed at {os.path.relpath(PACKAGE_DIR, REPO)}")


def remove_package_tests(root):
    """The package's own tests would join our test runs (embedded packages are testable)."""
    tests = os.path.join(root, "Tests")
    if os.path.isdir(tests):
        shutil.rmtree(tests)
        if os.path.exists(tests + ".meta"):
            os.remove(tests + ".meta")
        print("removed package Tests/")


# Editor + desktop players only; everything else (Any, Android, iOS, WebGL…) off.
_PLATFORM_DATA = """  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Android: 1
        Exclude Editor: 0
        Exclude Linux64: 0
        Exclude OSXUniversal: 0
        Exclude Win: 1
        Exclude Win64: 0
        Exclude iOS: 1
  - first:
      Android: Android
    second:
      enabled: 0
      settings: {}
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS
  - first:
      Standalone: Linux64
    second:
      enabled: 1
      settings:
        CPU: x86_64
  - first:
      Standalone: OSXUniversal
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
  - first:
      Standalone: Win64
    second:
      enabled: 1
      settings:
        CPU: x86_64
  - first:
      iPhone: iOS
    second:
      enabled: 0
      settings: {}
"""


def restrict_managed_plugins(root):
    """Managed DLLs shipped 'Any platform' (Protobuf, Unsafe) → Editor + desktop only."""
    n = 0
    for dirpath, _, filenames in os.walk(root):
        for fn in filenames:
            if not fn.lower().endswith(".dll.meta"):
                continue
            path = os.path.join(dirpath, fn)
            text = open(path, encoding="utf-8").read()
            start = text.find("  platformData:\n")
            end = text.find("  userData:", start)
            if start < 0 or end < 0:
                continue
            new = text[:start] + _PLATFORM_DATA + text[end:]
            if new != text:
                with open(path, "w", encoding="utf-8", newline="\n") as f:
                    f.write(new)
                n += 1
    print(f"restricted {n} DLL import settings to Editor/desktop")


def strip_mobile(root):
    """Delete Android / iOS native plugins (and their .meta files)."""
    removed = 0
    for dirpath, dirnames, filenames in os.walk(root, topdown=True):
        for d in list(dirnames):
            if d in ("Android", "iOS"):
                full = os.path.join(dirpath, d)
                shutil.rmtree(full)
                meta = full + ".meta"
                if os.path.exists(meta):
                    os.remove(meta)
                dirnames.remove(d)
                removed += 1
        for fn in filenames:
            low = fn.lower()
            # Android archives anywhere; desktop .so (Linux) / .dll / .dylib stay.
            if low.endswith((".aar", ".aar.meta")):
                os.remove(os.path.join(dirpath, fn))
                removed += 1
    print(f"stripped {removed} mobile plugin folders/files")


def restrict_assemblies(root):
    """Limit every asmdef in the package to Editor + desktop players."""
    n = 0
    for dirpath, _, filenames in os.walk(root):
        for fn in filenames:
            if not fn.endswith(".asmdef"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, encoding="utf-8-sig") as f:
                data = json.load(f)
            inc = data.get("includePlatforms") or []
            if inc == ["Editor"]:
                continue  # editor-only already
            data["includePlatforms"] = [p for p in DESKTOP_PLATFORMS if p == "Editor" or not inc or p in inc] or ["Editor"]
            data["excludePlatforms"] = []
            with open(path, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=4)
            n += 1
    print(f"restricted {n} assemblies to Editor/desktop")


def install_model():
    dest = os.path.join(MODEL_DIR, "hand_landmarker.task")
    download(MODEL_URL, dest)
    size = os.path.getsize(dest)
    digest = sha256(dest)
    if size != MODEL_SIZE:
        os.remove(dest)
        sys.exit(f"model size {size} != expected {MODEL_SIZE} (file deleted)")
    if MODEL_SHA256 and digest != MODEL_SHA256:
        os.remove(dest)
        sys.exit(f"model SHA-256 mismatch: {digest} (file deleted)")
    print(f"model OK: {os.path.relpath(dest, REPO)} sha256={digest}")


def main():
    install_plugin()
    install_model()
    print("done. Open/refresh Unity; webcam mode needs TabletopTempoSettings.webcamMode (or F8).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
