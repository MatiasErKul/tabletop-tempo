# Tabletop Tempo *(working title)*

**Learn to drum with your hands, the table and a cup: falling move cards teach you each beat, building up to the classic cup game.**

A seated, hands-only mixed-reality rhythm game for Meta Quest 2 / 3 / 3S, built for the
Meta VR Start Developer Competition 2026 (Gaming track, New Experience division).

> Status: early development. See [`docs/PROGRESS.md`](docs/PROGRESS.md).

## What it is

Move pictograms fall down a highway behind your table toward a hit line. You clap, pound the
table, tap, and finally play the folk cup game with a real cup, all with your hands, seated,
in passthrough. Six short stages take about ten minutes in total.

- Hands only, no controllers needed
- Seated, everything within about 60 cm
- Works on Quest 2 (greyscale passthrough) as well as Quest 3 / 3S
- Real cup tracked through your hands, with a virtual cup if you don't have one
- All music, sound and art are original

## Repository layout

```
TabletopTempo/        Unity 6.3 LTS project (Assets/_Project/...)
tools/unity.ps1       batch-mode compile / test / build wrapper
tools/music/          original music + chart generator (coming)
docs/                 progress, architecture, testing and submission guides
```

## Build and test (batch mode)

Requires Unity **6000.3.28f1** with Android Build Support. Close the Unity Editor first.

```powershell
./tools/unity.ps1 compile     # compile check
./tools/unity.ps1 test        # EditMode + PlayMode tests
./tools/unity.ps1 build       # Builds/TabletopTempo.apk
python tools/build/check_apk.py Builds/TabletopTempo.apk   # permissions / devices / features check
```

Android toolchain notes (JDK 17, SDK, NDK r27c): [`docs/ANDROID_TOOLCHAIN.md`](docs/ANDROID_TOOLCHAIN.md).

## Licenses

Code and original assets: MIT (see `LICENSE`). Third-party SDKs are not included in this
repository; see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
