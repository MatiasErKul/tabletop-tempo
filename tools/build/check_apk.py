#!/usr/bin/env python3
"""
Checks a built APK against the competition / privacy rules. Exit code 1 on any violation.

  python tools/build/check_apk.py Builds/TabletopTempo.apk

Uses aapt2 from the Android SDK named in tools/android-toolchain.local.txt (sdk=...), or $ANDROID_SDK_ROOT.
Checks:
  * forbidden permissions are absent (camera, headset camera, microphone, screen capture, location…)
  * hand tracking + passthrough features are declared, supportedDevices = quest2|quest3|quest3s
  * prints size and the full permission list
"""
import os
import re
import subprocess
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

FORBIDDEN = [
    "android.permission.CAMERA",
    "horizonos.permission.HEADSET_CAMERA",
    "android.permission.RECORD_AUDIO",
    "android.permission.FOREGROUND_SERVICE_MEDIA_PROJECTION",
    "android.permission.ACCESS_FINE_LOCATION",
    "android.permission.ACCESS_COARSE_LOCATION",
    "android.permission.READ_CONTACTS",
    "com.oculus.permission.EYE_TRACKING",
    "com.oculus.permission.FACE_TRACKING",
    "com.oculus.permission.BODY_TRACKING",
]
REQUIRED_FEATURES = {"oculus.software.handtracking": "true", "com.oculus.feature.PASSTHROUGH": "true"}
SUPPORTED_DEVICES = "quest2|quest3|quest3s"


def find_aapt2():
    sdk = os.environ.get("ANDROID_SDK_ROOT")
    cfg = os.path.join(REPO, "tools", "android-toolchain.local.txt")
    if os.path.exists(cfg):
        for line in open(cfg, encoding="utf-8"):
            if line.strip().lower().startswith("sdk="):
                sdk = line.split("=", 1)[1].strip()
    if not sdk:
        sys.exit("No SDK: set sdk= in tools/android-toolchain.local.txt or ANDROID_SDK_ROOT")
    bt = os.path.join(sdk, "build-tools")
    versions = sorted(os.listdir(bt), key=lambda v: [int(x) if x.isdigit() else 0 for x in re.split(r"[.-]", v)])
    exe = "aapt2.exe" if os.name == "nt" else "aapt2"
    return os.path.join(bt, versions[-1], exe)


def parse_manifest(aapt2, apk):
    out = subprocess.run([aapt2, "dump", "xmltree", "--file", "AndroidManifest.xml", apk],
                         capture_output=True, text=True, check=True).stdout
    perms, features, meta = [], {}, {}
    element, attrs = None, {}

    def flush():
        if element == "uses-permission" and "name" in attrs:
            perms.append(attrs["name"])
        elif element == "uses-feature" and "name" in attrs:
            features[attrs["name"]] = attrs.get("required", "true")
        elif element == "meta-data" and "name" in attrs:
            meta[attrs["name"]] = attrs.get("value", "")

    for line in out.splitlines():
        s = line.strip()
        if s.startswith("E: "):
            flush()
            element, attrs = s[3:].split(" ")[0], {}
        m = re.search(r":(name|value|required)\([^)]*\)=(?:\"([^\"]*)\"|(\S+))", s)
        if m and element:
            attrs[m.group(1)] = m.group(2) if m.group(2) is not None else m.group(3)
    flush()
    return perms, features, meta


def main():
    apk = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "Builds", "TabletopTempo.apk")
    perms, features, meta = parse_manifest(find_aapt2(), apk)
    problems = []
    for p in FORBIDDEN:
        if p in perms:
            problems.append(f"forbidden permission: {p}")
    for f, req in REQUIRED_FEATURES.items():
        if features.get(f) != req:
            problems.append(f"feature {f} should be required={req}, is {features.get(f)}")
    if meta.get("com.oculus.supportedDevices") != SUPPORTED_DEVICES:
        problems.append(f"supportedDevices is {meta.get('com.oculus.supportedDevices')!r}, want {SUPPORTED_DEVICES!r}")

    print(f"APK: {apk} ({os.path.getsize(apk) / 1e6:.1f} MB)")
    print("permissions:")
    for p in perms:
        print("  -", p)
    print("supportedDevices:", meta.get("com.oculus.supportedDevices"))
    print("hand tracking:", features.get("oculus.software.handtracking"),
          "| frequency:", meta.get("com.oculus.handtracking.frequency"),
          "| passthrough:", features.get("com.oculus.feature.PASSTHROUGH"))
    if problems:
        print("\nFAIL:")
        for p in problems:
            print("  *", p)
        return 1
    print("\nOK: no forbidden permissions, features and devices as required.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
