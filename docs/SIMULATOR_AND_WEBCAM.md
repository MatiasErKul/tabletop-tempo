# Running in the Meta XR Simulator (and, from M3, with webcam hands)

No headset needed. The Meta XR Simulator provides the head pose, a synthetic room (with a table)
and passthrough rendering. From milestone 3 your real hands come from an external webcam.

> The **Passthrough Camera API does not work in the XR Simulator or over Link**. That is why the
> webcam path exists. The core game (including the hand-anchored cup) needs no headset camera.

## 1. Install the standalone simulator (once, Windows)

1. Open <https://developers.meta.com/horizon/downloads/package/meta-xr-simulator-windows/>
   (sign in with your Meta developer account if asked).
2. Download the latest Windows installer and run it. It registers itself with Windows
   (the `xrsim://` protocol), which is how Unity finds it. No Unity package is needed: the
   Meta XR Core SDK (207.x) in this project detects the standalone install.
3. macOS (Apple Silicon): same page with `meta-xr-simulator-mac-arm`.

## 2. Activate it in Unity

1. Open the project in Unity 6000.3.28f1 (`TabletopTempo/` folder) via Unity Hub.
2. Menu **Window → Meta → Meta XR Simulator → Activate** (or the simulator button in the
   toolbar). This points the OpenXR runtime of the editor at the simulator.
3. Open `Assets/_Project/Scenes/Boot.unity` and press **Play**. The simulator window opens.
4. In the simulator, pick a synthetic environment that has a **table** (e.g. living room or
   office) so passthrough and Space Setup flows can be exercised.

To go back to Link/Quest: **Window → Meta → Meta XR Simulator → Deactivate**.

## 3. What you should see (M1)

- Passthrough (the synthetic room) behind everything.
- A small dark status card above the table area: `Tabletop Tempo · M1 skeleton`, the capability
  source (`editor-simulated-quest2`), tier `Low`, and live fps.
- Hands are the simulator's built-in hands (only 4 poses — fine for menus, not for gameplay).

## 4. Check the Quest 2 (greyscale) look

Quest 2 passthrough is black-and-white. To preview it:

1. Select `Assets/_Project/Settings/Resources/TabletopTempoSettings.asset`.
2. Tick **Simulate Greyscale Passthrough**, press Play.

Rule: every piece of UI must still read clearly in greyscale; colour is never the only cue.

## 5. Webcam hands (your real hands drive the game)

The simulator's own hands have only 4 poses. With an external webcam, MediaPipe tracks your real
hands (21 joints each) and they drive the gameplay; the XR Simulator still provides head pose,
room and rendering. Developer tool only: none of this is in the Quest APK.

### One-time setup

```bash
python tools/webcam/fetch_mediapipe.py
```

Downloads the MediaPipe Unity Plugin (MIT, 277 MB, SHA-256 checked) and the hand model
(Apache 2.0, 8 MB) into gitignored folders, strips mobile binaries and limits it to Editor/desktop.
Re-run any time; it is idempotent. (macOS: same command.)

### Camera placement

- **Overhead (recommended):** clamp the webcam about **50 cm above the desk**, looking straight
  down at the area in front of you. Both hands and ~50 × 40 cm of desk in view.
- **Front:** camera on the far side of the desk at hand height, facing you. Works, but height
  is estimated less precisely than with overhead.
- Normal indoor lighting, plain desk if possible, sleeves pulled back.

### Use it

1. Unity: **Tools → Tabletop Tempo → Webcam (MediaPipe)** opens the webcam window.
2. Pick the **Camera**, tick **Use webcam on Play** (or press **F8** during Play to switch
   between webcam hands and simulator hands), choose the **mount** and rough camera height.
3. Press **Play** (XR Simulator active). The window shows the camera with the tracked skeleton.
4. Click **Calibrate** and follow the four prompts; each captures by itself once you hold still:
   1. hold up only your **left** hand (fixes MediaPipe's left/right labels),
   2. touch the desk ~20 cm **left** of centre near your edge with your index fingertip,
   3. ~20 cm **right** of centre near your edge,
   4. straight ahead ~35 cm from your edge.
5. Your hands now appear as joint spheres above the virtual table, and the debug overlay (F9)
   shows them. Calibration is saved per machine (`TabletopTempo/UserSettings/TabletopTempoWebcam.json`).
   Recalibrate whenever the camera moves.

### How it works (for tuning)

Position on the desk comes from a 2D mapping (image → table) solved from the three touches; it
also gives the camera's focal length. Each frame, the hand's distance from the camera is solved
from MediaPipe's metric 3D landmarks and their 2D image positions (linear least squares over all
21 joints, exact for a pinhole camera and independent of how the hand is tilted), then corrected by
a hand-size factor learned during calibration. Hand shape uses the 3D landmarks rotated into table
space. **Measure the camera height** and enter it: distances scale with it. Joints are One-Euro filtered. Frame times are the **capture**
time, so tracker delay does not shift judging; remaining latency is covered by the latency
calibration (webcam mode is stored separately from headset).

### Webcam troubleshooting

| Symptom | Fix |
|---|---|
| "model missing" in the window | Run `python tools/webcam/fetch_mediapipe.py` |
| No camera / wrong camera | Pick it in the window *before* Play; close other apps using the camera |
| Hands jump left/right | Redo calibration step 1 (left hand only) |
| Height wrong (hands float or sink) | Set the camera height closer to reality, recalibrate |
| Results/s below ~20 | Lower the camera resolution in the camera's own settings, close heavy apps |

## Troubleshooting

| Symptom | Fix |
|---|---|
| Activate menu greyed out | Simulator not installed, or installed for another user. Re-run the installer, restart Unity. |
| Play starts but no simulator window | Check **Project Settings → XR Plug-in Management → Windows** has OpenXR ticked and **Meta XR** feature group enabled (`./tools/unity.ps1 setup` does this). |
| Batch-mode commands fail with "another Unity instance" | Close the Unity Editor before running `tools/unity.ps1`. |
