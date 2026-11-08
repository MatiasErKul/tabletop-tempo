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

## 5. Webcam hands (milestone 3, coming)

Setup for the external webcam, MediaPipe hand tracking, camera placement (overhead clamp about
50 cm above the desk works best) and calibration will be added here in M3.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Activate menu greyed out | Simulator not installed, or installed for another user. Re-run the installer, restart Unity. |
| Play starts but no simulator window | Check **Project Settings → XR Plug-in Management → Windows** has OpenXR ticked and **Meta XR** feature group enabled (`./tools/unity.ps1 setup` does this). |
| Batch-mode commands fail with "another Unity instance" | Close the Unity Editor before running `tools/unity.ps1`. |
