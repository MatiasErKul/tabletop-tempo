# Architecture

One game, swappable inputs. Gameplay never talks to a tracker or an SDK directly.

```
Gameplay (Conductor · Chart player · Note highway · Judge · Scoring · Lessons)
        ▲ MoveEvents                       ▲ CupState
Gesture recognizers (plain C#, unit tested, thresholds in ScriptableObjects)
        ▲ HandFrame                        ▲ CupObservation
IHandFrameProvider                         ICupProvider
  MetaHandProvider (device)                  HandAnchoredCupProvider (main, all devices)
  WebcamMediaPipeProvider (editor only)      VirtualCupProvider (fallback)
  XRSimHandProvider (menus)                  ReplayCupProvider (tests)
  ReplayHandProvider (tests)                 PassthroughCupProvider (Q3 stretch, off)
```

## Assemblies

| Assembly | Platforms | References | Contents |
|---|---|---|---|
| `TabletopTempo.Core` | all | UnityEngine only | Pure logic: capabilities, (M2+) HandFrame, recognizers, conductor maths, charts |
| `TabletopTempo.Runtime` | all | Core, TextMeshPro | MonoBehaviours: boot, settings, highway, UI |
| `TabletopTempo.Platform.Meta` | Android, Editor, Win64 | Core, Runtime, Oculus.VR, Meta.XR.EnvironmentDepth | Meta SDK glue: capability probe, passthrough style, (M2) Meta hand provider |
| `TabletopTempo.Webcam` *(M3)* | Editor, Standalone only | Core, Runtime, MediaPipe | Webcam + MediaPipe provider. Never in the APK |
| `TabletopTempo.Editor` | Editor | everything | Project setup, scene builder, build script |
| `TabletopTempo.Tests.*` | Editor / all | Core, Runtime | EditMode + PlayMode tests |

Platform assemblies plug into the game through registries in Core (e.g.
`CapabilityProbeRegistry`), registered from `[RuntimeInitializeOnLoadMethod]`. Game code only
sees interfaces.

## Capabilities (section 2b)

`ICapabilityProbe` (platform) → `CapabilityProbeResult` (nullable fields: null = unknown)
→ `CapabilityResolver.Resolve` (pure, tested) → `DeviceCapabilities`.

- Unknown core feature (hands, passthrough, scene) → assume available (Quest 2 answer).
- Unknown extra (colour, depth, camera) → absent.
- Quest-3-only extras are used only if available **and** their flag in `TabletopTempoSettings` is on (all off by default).
- Quality tier High only with confirmed colour passthrough + depth hardware. Otherwise Low.
- In the editor the probe is replaced by a fixed Quest 2 profile (`simulateQuest2Capabilities`).

## Scenes and assets are generated

`Boot.unity` and `Game.unity` are built by `SceneBuilder` (Editor). Do not hand-edit them:
change the builder and run **Tools → Tabletop Tempo → Rebuild Scenes** or `./tools/unity.ps1 setup`.

Boot: `AppBootstrap` → resolve capabilities → apply quality tier → load Game.
Game: `OVRCameraRig` (passthrough underlay, floor-level origin, OVR hands) + `GameRoot/TableAnchor`.

## Hand input (M2)

```
IHandFrameProvider.Write(frame, dspTime)      Meta (device + XR Simulator) · webcam (M3) · replay
        │ world/tracking space (replays: already table space)
HandInputService.Process
        ├─ TableSpace.ToTableInPlace          table-local frame (from TableAnchor until calibration)
        ├─ HandShape.Compute                  palm centre/normal/forward, finger curls, fist/flat/pinch
        └─ HandFrameHistory.Push              ring buffer (120 frames), palm velocity
        → FrameReady event                    recognizers (M4+), recorder, debug overlay
```

- **21 joints, MediaPipe order** (`HandJoint`). Meta's OpenXR 26-joint skeleton maps onto it in
  `MetaHandProvider` (proximal = MCP knuckle, intermediate = PIP, distal = DIP). The rig uses the
  OpenXR skeleton types (`XRHandLeft/Right`), not the legacy OVR 24-bone skeleton.
- **Palm normal** always points out of the palm side (flat hand on the table → down). Unity is
  left-handed, so the cross product is flipped for the right hand; unit tests pin this down.
- **Times** are on the audio DSP clock (`AudioSettings.dspTime`), so the conductor can judge input directly.
- **No allocation per frame**: frames and history slots are pre-allocated and copied in place.
- `InputRig` picks the provider: replay asset → replay; editor + webcam mode → webcam (registered
  by the webcam assembly, M3); otherwise Meta hands (which also covers the XR Simulator's hands).
  Platform assemblies register factories in `HandProviderRegistry`.
- `SyntheticHand` builds plausible hands from (palm centre, orientation, curl): the basis of all
  recognizer tests and the ghost-hand demos.
- **Recording**: F10 (or Tools → Tabletop Tempo → Record Session) writes `.ttrec` text files to
  `Recordings/` (gitignored). Copy good ones into `Assets/_Project/Tests/Replays/` for tests.
- **Debug overlay**: F9 — provider, per-hand confidence, fist/flat/pinch, palm height, events, fps.

## Coordinate frames

All gameplay uses a **table-local frame**: origin = calibrated table point, +Y up, +Z away from
the player. Until calibration (M5) the `TableAnchor` sits at a seated default (0, 0.74, 0.40) m
in the floor-level tracking space.
