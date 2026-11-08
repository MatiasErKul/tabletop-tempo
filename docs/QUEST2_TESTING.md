# Testing on a Quest 2 (sideloading)

Same APK as Quest 3/3S. Quest 2 differences to keep in mind: greyscale passthrough, no Depth
API, no headset camera access, weaker GPU (target **72 fps**).

## One-time headset setup

1. **Developer mode**
   1. You need a Meta developer account (<https://developers.meta.com/horizon/>) and to be in an
      organisation there (create one if asked).
   2. On your phone, open the **Meta Horizon** app → **Devices** → your Quest 2 →
      **Headset settings** → **Developer mode** → on. Reboot the headset.
2. **Hand tracking**: in the headset, **Settings → Movement tracking → Hand and body tracking**
   → on. Also turn on **auto-switch between hands and controllers**.
3. **Space Setup with a table** (optional, enables the "Use this table?" shortcut):
   **Settings → Physical space → Space setup** → scan the room → when asked to add furniture,
   draw a **Table** over your real table.
4. On the PC, install **Meta Quest Developer Hub (MQDH)**:
   <https://developers.meta.com/horizon/documentation/unity/ts-odh/>. It includes `adb`.
   Connect the headset by USB-C, put it on, and accept **Allow USB debugging** (tick *Always allow*).

## Install a build

```bash
./tools/unity.ps1 build-dev
```

Then either drag `Builds/TabletopTempo.apk` onto the device in MQDH, or:

```bash
adb install -r Builds/TabletopTempo.apk
```

The app appears under **Library → Unknown Sources** (or the search filter "Unknown sources").

## Logs

```bash
adb logcat -s Unity
```

Our lines start with `[TT]`. Capability detection prints e.g.
`[TT] Capabilities resolved: [meta-ovrplugin] tier=Low hands=True passthrough=True color=False ...`
On a Quest 2, expect `color=False depthHw=False tier=Low`.

## Performance (OVR Metrics Tool)

1. Install **OVR Metrics Tool** from the Meta Horizon Store (free) on the headset.
2. Open it → enable **Persistent overlay** with the **Basic** stats (FPS, CPU/GPU level, GPU %).
3. Launch Tabletop Tempo; play every stage. Note min fps per stage in `docs/PERFORMANCE.md`.

Target: steady **72 fps**. If a stage drops, note where and what was on screen.

## 10-minute test script

| # | What to do | Pass if |
|---|---|---|
| 1 | Cold start from the library | First frame in < 5 s, passthrough visible, status card readable in greyscale |
| 2 | Put controllers down, use hands only | Everything still works |
| 3 | *(from M5)* Touch-calibrate the table | Highway sits behind the table edge |
| 4 | *(from M5)* With a Space Setup table: accept "Use this table?" | Same result without touching |
| 5 | *(from M4)* Stage 1 claps | Claps register, misses never stop the run |
| 6 | *(from M5)* Stages 2–4 | Pound / tap / backhand / raise register |
| 7 | *(from M6)* Stage 5–6 with a real cup | Cup ring follows the cup; re-tap fixes drift |
| 8 | *(from M6)* Stage 5–6 with "virtual cup" | Finale completes |
| 9 | Take the headset off mid-stage, put it back | Game paused, 3-2-1 resume on the beat |
| 10 | Turn hand tracking off in settings, launch | Clear message asking to enable hand tracking |
