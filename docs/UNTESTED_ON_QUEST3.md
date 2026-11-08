# Code paths nobody has run on a Quest 3 / 3S

Judges test on Quest 3 / 3S. We can only test on the XR Simulator, a webcam, and maybe a Quest 2.
Every path below only runs on Quest-3-class hardware. Before the final build, review each row:
anything still **unverified** must be switched off (feature flag) or proven harmless.

| # | Code path | Where | Runs when | What could go wrong | Fallback | Default | Verified on Q3/3S? |
|---|---|---|---|---|---|---|---|
| 1 | High quality tier | `CapabilityResolver` → `QualityTierApplier` ("Quest High" level) | Probe confirms colour passthrough **and** depth hardware | Heavier effects drop below 60 fps | Set `qualityOverride = ForceLow` in settings; "Quest High" is kept close to "Quest Low" | On (auto) | ☐ |
| 2 | Colour passthrough | Meta passthrough layer | Always on Q3/3S | UI tuned for greyscale looks different in colour (contrast, tints) | UI never relies on colour alone; check screenshots from a tester | On | ☐ |
| 3 | Depth API / MRUK environment raycast | not built yet | `optionalFeatures.DepthRaycast` **and** hardware supports it | Permission prompt, wrong hits, perf cost | Touch-calibrated table (main path) | **Off** | ☐ |
| 4 | Passthrough Camera API cup finder | not built (M10 stretch) | `optionalFeatures.CameraCupFinder` **and** hardware supports it | Permission denied, slow inference, wrong boxes, exceptions | Hand-anchored cup (main path) | **Off** | ☐ |

## Review before final build (M9)

- [ ] Every row is either ✔ verified by a tester (name + date + build) or its flag is off in the shipped settings asset.
- [ ] `TabletopTempoSettings.optionalFeatures` are all `false` in the committed asset unless verified.
- [ ] Row 1: a tester's OVR Metrics screenshot shows ≥ 60 fps through Stage 6 on Q3/3S.
