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

- [ ] **M1 Skeleton** — Unity project, packages, repo + LFS, Boot → Game scene, passthrough, capability service, APK build, XR Simulator run
- [ ] M2 Input abstraction
- [ ] M3 Webcam mode
- [ ] M4 Rhythm core + Stage 1 (first demo-able slice)
- [ ] M5 Table + Stages 2–4
- [ ] M6 Cup + Stages 5–6
- [ ] M7 Flow
- [ ] M8 Polish
- [ ] M9 Ship
- [ ] M10 Stretch (camera cup finder) — dropped unless ahead

## Log

### 8 Nov 2026
**Done**
- Repo scaffold: `.gitignore`, `.gitattributes` (LFS for binaries), MIT `LICENSE`, `THIRD_PARTY_NOTICES.md`, docs.
- `tools/unity.ps1` batch wrapper (compile / test / build / method) + `tools/unity-path.txt`.
- Core: `CapabilityResolver` + probe registry with EditMode tests (unknown → Quest 2 path, flags gate Quest 3 extras).
- Runtime: `TabletopTempoSettings` (Resources), `CapabilityService`, `QualityTierApplier`, `AppBootstrap`.
- Editor: `BuildQuest` (APK to `Builds/TabletopTempo.apk`).

**In progress**
- Installing Unity 6000.3.28f1 + Android Build Support (first attempt timed out; retrying editor and modules separately).

**Known issues**
- None yet.
