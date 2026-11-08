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

## Coordinate frames

All gameplay uses a **table-local frame**: origin = calibrated table point, +Y up, +Z away from
the player. Until calibration (M5) the `TableAnchor` sits at a seated default (0, 0.74, 0.40) m
in the floor-level tracking space.
