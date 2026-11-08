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
| Meta XR Core SDK | 207.0.1 | npm.developer.oculus.com | Meta Platform Technologies SDK License Agreement — *to confirm from package LICENSE on first install* | Yes | Not redistributed in repo; permitted for apps built for Meta Quest |
| Meta XR Interaction SDK (+ OVR integration) | 207.0.1 | npm.developer.oculus.com | as above — *to confirm* | Yes | |
| Meta MR Utility Kit (MRUK) | 207.0.1 | npm.developer.oculus.com | as above — *to confirm* | Yes | |

## Fonts, art, audio, models

| Asset | Source | License | Notes |
|---|---|---|---|
| Music (all stages) | `tools/music/` generator, this repo | MIT (original work) | *Not generated yet* |
| Sound effects | `tools/music/` synth, this repo | MIT (original work) | *Not generated yet* |
| Pictograms | `TabletopTempo/Assets/_Project/Art/Pictograms/src`, this repo | MIT (original work) | *Not created yet* |

## Developer-only tools (never in the APK)

| Component | License | Notes |
|---|---|---|
| Meta XR Simulator (standalone) | Meta SDK license — *to confirm* | Installed separately by the developer |
| MediaPipe Unity Plugin (homuler) | MIT — *to confirm at M3* | Editor/Standalone only via asmdef constraints |
| MediaPipe HandLandmarker model | Apache 2.0 — *to confirm at M3* | Editor/Standalone only |
