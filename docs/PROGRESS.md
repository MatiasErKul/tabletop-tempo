# Progress log

Competition deadline: **18 Nov 2026, 12:00 PM Pacific** (21:00 CET / 20:00 UTC).
Target: upload a playable build to the `Competition` release channel by **15 Nov** so remote
Quest 3/3S testers have a few days.

## Scope cut line (decided 8 Nov)

| Priority | Milestones |
|---|---|
| Must ship | M1 skeleton · M2 input abstraction · M4 rhythm core + Stage 1 · M5 table + Stages 2–4 · M6 cup + Stages 5–6 · M7 flow (onboarding, pause, save, results, accessibility basics) · M9 ship |
| Time-boxed | M3 webcam mode (only way to test with real hands until a Quest 2 is available) · M8 polish (light) |
| Dropped unless ahead | M10 camera cup finder, webcam cup detector |

## Key decisions

| Topic | Decision | Why |
|---|---|---|
| Unity | **6000.3.28f1** (Unity 6.3 LTS, released 3 Nov 2026) | Newest LTS; Meta SDK 207 needs ≥ 6000.0.66f2 |
| Meta SDK | **207.0.1** (Core, Interaction, Interaction OVR, MRUK) from `npm.developer.oculus.com` | Latest (12 Oct 2026). Meta renumbered after v8x, so 207 > v81 required by the rules |
| XR backend | Unity OpenXR + Meta XR feature group | Meta's current recommended path |
| Render pipeline | URP | Quest performance, Unity 6 default |
| Hand tracking | Hands only (manifest: hand tracking required) | Hands-first rule; whole game is hand poses |
| Devices | One APK, Quest 2 + 3 + 3S declared | Rule 2b.1 |
| Capability checks | Feature probes → `CapabilityResolver` (pure C#, unit tested). Unknown = Quest 2 path | Rule 2b.1, 2b.2 |
| Quality tier | High only if colour passthrough **and** depth hardware are both confirmed | Capability-based proxy for Quest 3-class GPU |
| Project path | `C:\Users\matvi\source\repos\Tabletop-Tempo` (short) | Windows long paths are disabled on this PC; Unity breaks past 260 chars |

If Meta SDK 207 misbehaves on Quest 2 (Meta no longer tests new SDKs on Quest 2), pin the last
version that works here and record it in this table.

## Milestones

- [x] **M1 Skeleton** — Unity project, packages, repo + LFS, Boot → Game scene, passthrough, capability service, release APK (59 MB, checks pass) · *open: XR Simulator run (needs install)*
- [x] M2 Input abstraction
- [x] M3 Webcam mode · *open: first real-webcam run by the developer*
- [ ] M4 Rhythm core + Stage 1 (first demo-able slice)
- [ ] M5 Table + Stages 2–4
- [ ] M6 Cup + Stages 5–6
- [ ] M7 Flow
- [ ] M8 Polish
- [ ] M9 Ship
- [ ] M10 Stretch (camera cup finder) — dropped unless ahead

## Log

### 8 Nov 2026 — M3 webcam mode
**Done**
- `tools/webcam/fetch_mediapipe.py`: MediaPipe Unity Plugin 0.16.3 (MIT, SHA-256 verified) as a
  gitignored embedded package + HandLandmarker model (Apache 2.0, hash pinned) outside Assets.
  Strips Android/iOS binaries, removes the package's tests, limits its assemblies **and** its
  "any platform" managed DLLs (Protobuf, Unsafe) to Editor/desktop. `check_apk.py` now also fails
  if any MediaPipe/Protobuf/model/webcam file is in an APK.
- `TabletopTempo.Webcam` assembly (Editor/desktop, compiles only when the package is present):
  `WebcamMediaPipeProvider` (WebCamTexture → HandLandmarker LIVE_STREAM, CPU; capture-time
  timestamps on the DSP clock; One-Euro filtered), `WebcamCalibrator` (4 guided steps, auto-capture
  when still), `WebcamWindow` (Tools → Tabletop Tempo → Webcam: camera picker, live skeleton,
  calibration). F8 switches webcam ↔ Meta/XR Simulator hands during Play.
- Core maths (`WebcamCalibration`, `WebcamHandMapper`, `OneEuroFilter`), tested against a simulated
  pinhole camera: affine from 3 touches, focal length, **per-frame depth by linear least squares**
  over 21 joints (first tried apparent-size depth; it was 3.3 cm off for a tilted hand, so it was
  replaced), learned hand-scale factor, palm normal/chirality preserved.

**Needs you (can't be tested here)**: a real webcam run (camera placement, calibration, latency feel).

### 8 Nov 2026 — Android toolchain
- Developer accepted the Android SDK license in chat. Installed a separate toolchain in
  `C:\Users\matvi\AndroidDev` (OpenJDK 17.0.18, cmdline-tools 16, platform-tools 37.0.1,
  build-tools 36, platforms 34 + 36, NDK r27c, CMake 3.22.1), hashes verified, and pointed Unity
  at it via `tools/android-toolchain.local.txt` + `AndroidToolchain` (see docs/ANDROID_TOOLCHAIN.md).
  The stale Hub-installed Android parts in the editor folder are unused.
- **First APKs build.** Release `Builds/TabletopTempo.apk` = 59 MB (first IL2CPP dev build 11 min,
  release 6 min). `tools/build/check_apk.py` passes on release: permissions are only INTERNET,
  HAND_TRACKING, USE_ANCHOR_API, USE_SCENE (+ an Android-internal receiver permission); hand
  tracking + passthrough required; `supportedDevices = quest2|quest3|quest3s`; hand tracking HIGH.
- Found: Meta Core ships the **Meta XR Operator** AI-tooling layer (`XrApiLayer_METAX_operator`),
  which adds `FOREGROUND_SERVICE_MEDIA_PROJECTION` (screen capture). Meta's build step includes it
  only in **development** builds, never release; `check_apk.py` fails if it ever appears in a
  release APK. Only ever upload release builds.
- `INTERNET` stays (Unity/Meta platform). The app itself sends nothing anywhere.

### 8 Nov 2026 — M2 input abstraction
**Done**
- Public repo created: <https://github.com/MatiasErKul/tabletop-tempo> (main + m1-skeleton pushed, LFS OK).
- Core: `HandFrame`/`HandData` (21 joints, table space, derived palm frame + fist/flat/pinch scores),
  `HandShape`, `HandFrameHistory` (ring buffer + velocity), `TableSpace`, `IHandFrameProvider`,
  `HandProviderRegistry`, `ReplaySession` (.ttrec text format) + `ReplayHandProvider`,
  `SyntheticHand` (scripted hands for tests and ghost demos).
- Meta: `MetaHandProvider` (OpenXR 26 → 21 joints, also drives XR Simulator hands), registered via
  `MetaRegistration`. Rig switched to OpenXR hand skeleton types.
- Runtime: `HandInputService`, `InputRig` (provider choice), `HandJointVisualizer`, `SessionRecorder`
  (F10), `DevOverlay` (F9; not `DebugOverlay`, which clashes with URP's class). Generated into
  Game.unity by `SceneBuilder`.
- EditMode tests for palm-normal convention, shape scores, history, table space, replay round trip.

**Not verifiable without hands yet**: real Meta hand data (XR Simulator only has 4 poses; webcam is M3).

### 8 Nov 2026 — M1 skeleton
**Done**
- Repo scaffold: `.gitignore`, `.gitattributes` (LFS for binaries), MIT `LICENSE`, `THIRD_PARTY_NOTICES.md`, docs.
- `tools/unity.ps1` batch wrapper (compile / test / build / method) + `tools/unity-path.txt`.
- Core: `CapabilityResolver` + probe registry with EditMode tests (unknown → Quest 2 path, flags gate Quest 3 extras).
- Runtime: `TabletopTempoSettings` (Resources), `CapabilityService`, `QualityTierApplier`, `AppBootstrap`.
- Editor: `BuildQuest` (APK to `Builds/TabletopTempo.apk`).

- Unity 6000.3.28f1 + Android Build Support installed via Unity Hub CLI (first combined attempt
  timed out; editor and modules installed separately). Personal license works in batch mode.
- `Packages/manifest.json`: Meta 207.0.1 (Core, Interaction, Interaction OVR, MRUK) + Unity
  6.3-bundled versions. Removed Unity's new-project defaults that conflict with the rules
  (Analytics, In-App Purchasing) or aren't needed (collab-proxy, multiplayer center, 2D, terrain…).
  `com.unity.modules.physics2d` must stay: Meta's `OVRRaycaster` uses it.
- Editor setup (`./tools/unity.ps1 setup`): Meta Project Setup auto-fixes, then our explicit
  player settings (IL2CPP, ARM64, API 32+, Vulkan, linear, ASTC), URP "Quest Low"/"Quest High"
  assets + quality levels, OpenXR loader (Android + Standalone) with Meta XR feature,
  OVRProjectConfig (Quest 2/3/3S, hands only, Fast Motion Mode, passthrough required, scene
  supported, no camera access), settings asset, TMP essentials, scene rebuild.
- Scenes generated by `SceneBuilder`: Boot (AppBootstrap) → Game (OVRCameraRig + passthrough
  underlay + OVR hands, GameRoot/TableAnchor, status card with capabilities + fps).
- PlayMode smoke test `BootFlowTests`.
- Music tool `tools/music/ttmusic.py` (stdlib only) + Stage 1 pattern → 48 s original track and
  chart (48 claps, 72 → 80 BPM). Docs: MUSIC, ARCHITECTURE, SIMULATOR_AND_WEBCAM, QUEST2_TESTING.

**Decisions**
- Hand tracking Fast Motion Mode (HIGH) on: claps and pounds are fast. Cost on Quest 2 to be
  measured; switch back to LOW in `MetaProjectSetup` if it hurts frame rate or latency.
- `OVRPassthroughLayer` in SDK 207 is always an underlay (no overlay-type field any more).

- Privacy: Meta's `Assets/Resources/DevAgentSettings.asset` holds a local access token and the
  dev PC's LAN IP and would ship in the APK. It is gitignored, and setup + every build blank it.
- Licenses: TMP's EmojiOne sample sprites have no clearly stated license → removed by setup.
  LiberationSans (SIL OFL 1.1) is kept and recorded.
- Tests: EditMode 12/12, PlayMode 1/1 (Boot → Game, Quest 2 profile, TMP present).

**Known issues / blocked**
- ~~APK build blocked by Unity Hub 3.1.2's stale Android modules~~ → solved with a separate
  toolchain (see "Android toolchain" above and docs/ANDROID_TOOLCHAIN.md).
- The old Hub's licensing client also conflicts with Unity 6.3's: every batch run waits ~75 s for
  licensing before the editor launches its own. Updating Hub should fix this.
- XR Simulator not installed yet (needs a download from Meta, see SIMULATOR_AND_WEBCAM.md).
