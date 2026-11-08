# Android toolchain (for the Quest APK)

Unity 6000.3.28f1 needs: **OpenJDK 17**, an Android SDK with **platform-tools**,
**build-tools 36.0.0**, **platforms 34 (target) and 36**, **CMake 3.22.1**, and **NDK r27c
(27.2.12479018)**.

## Normal way

Unity Hub (current version) → Installs → 6000.3.28f1 → ⚙ → Add modules → Android Build Support
with **OpenJDK** and **Android SDK & NDK Tools**. Accept the Android SDK license.

## What this machine uses (and why)

The Unity Hub here was 3.1.2 (2022). Its headless installer used an outdated module list and put
OpenJDK 8, NDK r21d and SDK platforms 29/30 into the editor folder, which Unity 6.3 can't build with.
Instead of touching the editor install, a separate toolchain lives in `C:\Users\matvi\AndroidDev`
and Unity is pointed at it (Preferences → External Tools, stored per user).

Installed 8 Nov 2026 (Android SDK license accepted by the developer):

| Component | Source | Verified |
|---|---|---|
| OpenJDK 17.0.18+8 | Unity CDN (`download.unity3d.com/.../jdk17.0.18-8_<sha256>.zip`) | SHA-256 matches the hash in the file name |
| Android cmdline-tools 16.0 | `dl.google.com/android/repository/commandlinetools-win-12266719_latest.zip` | SHA-384 matches Unity's release metadata |
| platform-tools, build-tools 36.0.0, platforms 34 + 36, NDK 27.2.12479018, CMake 3.22.1 | `sdkmanager` (Google) | sdkmanager checks its own downloads |

Layout:

```
AndroidDev\OpenJDK\                       bin\java.exe
AndroidDev\sdk\cmdline-tools\16.0\        bin\sdkmanager.bat
AndroidDev\sdk\platform-tools\  build-tools\36.0.0\  platforms\android-34\ android-36\  cmake\3.22.1\
AndroidDev\sdk\ndk\27.2.12479018\
```

## Pointing Unity at it

1. Copy `tools/android-toolchain.example.txt` to `tools/android-toolchain.local.txt`
   (gitignored) and fill in the three paths.
2. Either build (`./tools/unity.ps1 build-dev` applies it automatically), or run
   **Tools → Tabletop Tempo → Apply Android Toolchain Paths** once in the editor.

To go back to the Hub-installed tools, delete `android-toolchain.local.txt` and tick
"installed with Unity" for JDK / SDK / NDK in **Preferences → External Tools**.

## Reproduce from scratch (PowerShell)

```powershell
$A = "$env:USERPROFILE\AndroidDev"
# 1) JDK + cmdline-tools: download the two zips above into $A\_downloads, unpack:
#    jdk17.zip -> $A\OpenJDK ;  cmdline-tools.zip -> $A\sdk\cmdline-tools\16.0
$env:JAVA_HOME = "$A\OpenJDK"
& "$A\sdk\cmdline-tools\16.0\bin\sdkmanager.bat" --sdk_root="$A\sdk" --licenses
& "$A\sdk\cmdline-tools\16.0\bin\sdkmanager.bat" --sdk_root="$A\sdk" --install `
    "platform-tools" "build-tools;36.0.0" "platforms;android-34" "platforms;android-36" `
    "ndk;27.2.12479018" "cmake;3.22.1"
```
