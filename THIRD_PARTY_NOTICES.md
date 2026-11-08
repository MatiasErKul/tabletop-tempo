# Third-party notices

This repository's own code, tools, music, sound effects, pictograms and docs are MIT licensed
(see `LICENSE`). The components below are **not stored in this repository**. They are downloaded
by the Unity Package Manager (or by the developer) and stay under their own licenses. Where a
component is compiled into the shipped APK, that is noted.

Rule for this project: no component is added until its license is checked and recorded here.
Anything unclear or incompatible with a free public app + public repo is not used.

## SDKs and packages

| Component | Version | Source | License | In APK? | Notes |
|---|---|---|---|---|---|
| Unity Editor / Engine | 6000.3.28f1 | Unity | Unity Terms of Service (Personal plan) | Runtime yes | Not redistributed in repo |
| Unity packages (URP, OpenXR, XR Management, XR Hands, Input System, uGUI/TextMeshPro, Test Framework, AI Navigation, Newtonsoft Json) | per `Packages/packages-lock.json` | Unity registry | Unity Companion License (Newtonsoft: MIT) | Yes (runtime parts) | Fetched by UPM |
| Meta XR Core SDK | 207.0.1 | npm.developer.oculus.com | Oculus SDK License Agreement (<https://developers.meta.com/horizon/licenses/oculussdk/>), per the package's `LICENSE.md` | Yes | Not redistributed in repo; the license permits shipping apps built with it |
| Meta XR Interaction SDK (+ OVR integration) | 207.0.1 | npm.developer.oculus.com | Oculus SDK License Agreement | Yes | |
| Meta MR Utility Kit (MRUK) | 207.0.1 | npm.developer.oculus.com | Oculus SDK License Agreement | Yes | |

## Fonts, art, audio, models

| Asset | Source | License | Notes |
|---|---|---|---|
| TextMesh Pro Essential Resources (TMP Settings, shaders, LiberationSans SDF) | Unity uGUI 2.0 package, imported by setup into `Assets/TextMesh Pro` | Unity Companion License; **LiberationSans font: SIL Open Font License 1.1** (`Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`) | In APK. OFL allows embedding in apps |
| ~~EmojiOne sample sprites~~ (bundled with TMP Essentials) | emojione.com | Not clearly stated in the bundle ("review their licensing terms") | **Removed** by setup, not used |
| Music (all stages) | `tools/music/ttmusic.py` + `tools/music/patterns/`, this repo | MIT (original work) | Synthesised from scratch, no samples. Stage 1 done |
| Sound effects (hits, miss, count-in, UI, stage clear) | `tools/music/ttmusic.py sfx`, this repo | MIT (original work) | Synthesised from scratch |
| Pictograms + lane frames + star | SVG sources in `TabletopTempo/Assets/_Project/Art/Pictograms/src`, rasterised by `tools/art/pictograms.py` (own SDF rasteriser, stdlib only) | MIT (original work) | Clap done; other moves at M5/M6 |
| Shaders `TabletopTempo/Card`, `TabletopTempo/UnlitColor` | this repo | MIT (original work) | |

## Developer-only tools (never in the APK)

| Component | License | Notes |
|---|---|---|
| Meta XR Simulator (standalone) | Meta SDK license — *to confirm* | Installed separately by the developer |
| MediaPipe Unity Plugin v0.16.3 (homuler, <https://github.com/homuler/MediaPipeUnityPlugin>) | **MIT** (package `LICENSE.md`); bundles MediaPipe (Apache 2.0) and dependencies under Apache 2.0 / MIT / BSD-3 / OFL / permissive custom notices (package `Third Party Notices.md`) | Fetched by `tools/webcam/fetch_mediapipe.py` (SHA-256 verified) into a gitignored embedded package. Android/iOS binaries stripped; assemblies and DLLs limited to Editor/desktop; `check_apk.py` fails if any of it appears in an APK. Not redistributed |
| MediaPipe HandLandmarker model `hand_landmarker.task` (float16) | **Apache 2.0** (Google MediaPipe models) | Fetched to gitignored `TabletopTempo/WebcamModels/` (outside Assets, never built into a player). SHA-256 pinned in the fetch script |
