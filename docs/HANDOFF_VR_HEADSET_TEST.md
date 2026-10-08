# Handoff: first VR headset test (GTX 1080 PC)

Written 2026-10-08 on the laptop (BIBOP: i7-1255U, Intel Iris Xe only). The
laptop cannot run Quest Link, so the VR mode has only been tested with the XR
Device Simulator. The next session runs on the PC with the **GTX 1080** and a
**Meta Quest** connected through Meta Horizon Link.

Read first: `CLAUDE.md` → `Project_livingContext.md` →
[UNITY_VR_TABLETOP.md](UNITY_VR_TABLETOP.md) (the VR mode doc) →
[VR_TABLETOP_PLAN.md](VR_TABLETOP_PLAN.md) (the plan).

## Where things stand

- **VR mode:** the city is a **fixed hologram table** (about 1.17 × 1.80 m,
  scale 1:6517). One user at a time walks around it: no locomotion, no street
  view.
- **Running mode:** the app runs on the PC over **Quest Link** (Play in the
  Editor, or a Windows build). A standalone Quest build is not set up.
- **Done (plan items 1–5):**
  - OpenXR + XR Interaction Toolkit 3.6.1;
  - `TabletopRig`: the **XR Origin is scaled, not the city**;
  - controller-ray picking through `IInteractionPointer`;
  - the hand menu on the left controller (tabs Info / Views / Legend /
    Compare / Task) and a hover label.
- **Scene:** `Assets/Scenes/Helsingborg_VR.unity` is **generated** from
  `Helsingborg.unity` by **UrbanAnalytics → VR → Build VR Scene**
  (`VRSceneBuilder`). Do not hand-edit it.
- **Commits:** "first vr iteration development" and "Updated UI for VR".

## Step 1: check the new machine

| Check | How | Expected |
|---|---|---|
| GPU visible | `Get-CimInstance Win32_VideoController` | NVIDIA GeForce GTX 1080 |
| Link is the OpenXR runtime | `HKLM:\SOFTWARE\Khronos\OpenXR\1` → `ActiveRuntime` | path to `oculus_openxr_64.json` (Meta Horizon). If it points to SteamVR, set Link active in the Meta Horizon Link app: Settings → General → OpenXR Runtime |
| Unity editor | `unity editors` | **6000.3.14f1** installed (use exactly this version) |
| Project path | | Same `W:\…\Portotype` if the drive is shared; otherwise a clone of the repo |
| Packages | `Packages/manifest.json` | `com.unity.xr.management` 4.7.0, `com.unity.xr.openxr` 1.16.1, `com.unity.xr.interaction.toolkit` 3.6.1, `com.unity.pipeline` (Unity CLI bridge) |
| XRI samples | `Assets/Samples/XR Interaction Toolkit/3.6.1/` | `Starter Assets` and `XR Device Simulator` (committed). If missing: Package Manager → XR Interaction Toolkit → Samples → import both |
| Python env (only for data work) | `digitaltwin` conda env | Not needed for the VR test |

On first open, Unity re-imports the project (no `Library/` yet), which takes
a while. Then check the console for compile errors.

## Step 2: run it in the headset

1. Connect the Quest with Link (cable or Air Link) and enter the Link home in
   the headset.
2. Open `Helsingborg_VR.unity` and press Play.
3. The console should show:
   - **no** `XRSimulatorFallback: no headset active` line. If it appears, the
     headset was not detected: start Link before Play, then stop and press
     Play again;
   - `TabletopRig: city 6.57 × 10.69 km on a 1.17 × 1.80 m table, 1 m = 6.517 km`;
   - `Picking colliders baked: 59 meshes, …`.
4. In the headset, hold the Meta button to recentre so you face the table.

## Step 3: first-test checklist

This has never been seen in a headset. Check, note the results, and fix:

- [ ] **Stereo / scale.** The table looks like a solid table at about 0.9 m.
      Depth feels right with no double images, so the scaled rig gives the
      correct eye separation. Both eyes show the city (shaders are
      single-pass instanced; `VertexColorUnlit` was fixed for this).
- [ ] **Comfort / performance.** A steady frame rate on the GTX 1080
      (Unity Profiler, or the Meta Link performance overlay). The city is
      about 0.83 M building triangles; the table placement and the colliders
      are built once at load.
- [ ] **Pointer.** The right controller ray, yellow reticle and hover
      highlight are steady on small buildings. Trigger selects; grip + trigger
      selects the cell; trigger on the left controller switches hands.
- [ ] **Hand menu** (left controller; X toggles it).
  - Text readable, panel not too big or small, comfortable tilt.
  - Tune these in the Inspector on the `VRUI` object (`XRHandMenu`:
    `metersPerUnit` 0.0006, `localPositionMeters` (0, 0.10, 0.06),
    `localEulerAngles` (35, 0, 0), `sizeUnits` 520 × 640). **Write the final
    values back into the code defaults:** rebuilding the scene resets them.
- [ ] **Hover label** (`XRHoverLabel.metersPerUnit` 0.0006, 3 cm above the
      point): readable, not in the way.
- [ ] **Tabs.**
  - Views applies a view.
  - Legend matches the view.
  - Info opens on selection.
  - Compare shows A/B after Copy → A / B.
  - Task appears when the facilitator presses Start session + Show view on
    the PC monitor.
- [ ] **Facilitator view.** The desktop panels still show on the PC monitor
      (Game view) and the mouse works there.

## Driving Unity from the command line (for the agent)

- **Commands:** `unity status`, then `unity command <name>`. List them with
  `unity command --detail compact`.
- **Clean JSON:** use `unity command --result-only …`. Plain output appends a
  parameter column that breaks JSON parsing.
- **`run_script --file X.cs --entry Class.Method`** runs C# in the Editor.
  - Each call compiles a fresh assembly, so **static fields don't persist**
    between calls. Keep state in the scene (e.g. a named GameObject).
  - It can call project editor code, e.g.
    `UrbanAnalytics.XR.Editor.VRSceneBuilder.Build()`.
- **Play mode pauses when the Editor is unfocused.** Set
  `Application.runInBackground = true` via `run_script` for that play session.
- **`capture_game_view --save_path Temp/x.png`** writes to `Assets/Temp/`.
  Copy the file out, then `delete_asset` the `Assets/Temp` folder.
- **`package_add`** times out after about 25 s while still working; poll
  `package_status`.
- **Tests:** `run_tests --mode EditMode`; 62/62 should pass.
- **Simulating input in scripted checks:**
  - Disable the `TrackedPoseDriver` on the camera/controller and set their
    poses.
  - Press a UI button through
    `NearFarInteractor.uiPressInput.inputSourceMode = ManualValue` +
    `QueueManualState(true/false, …)`, then restore the mode.

## Open questions and next work

1. **Tune the UI** after the headset test (above).
2. **VR wording for study tasks.** `scenarios.json` (city package,
   `cities/helsingborg/visualizations/`) mentions desktop controls ("pan,
   orbit and zoom… press C").
3. **Plan item 6: VR-specific log events** (hand used, menu tab opened, menu
   shown/hidden, rig scale at session start). Selections, views and
   comparisons are already logged by `StudySession`. Logged camera poses are
   in world units: divide by the rig scale (6.517) for metres.
4. **Decision still open:** a fixed map, or pan/zoom inside the table?
   Pan/zoom needs a table-edge clipping shader. See the Open Questions in the
   living context.
5. **Known limits:**
   - columns of signed/diverging heights go into the table;
   - XRI's own line visuals are off, because they are sized in world units;
   - `XRSimulatorFallback` decides once at Start.
6. **`Assets/_Recovery/`** (an untracked Unity crash-recovery scene on the
   laptop): check it, then delete it or ignore it.

## After the test

Update `Project_livingContext.md` (status + changelog row) and
[UNITY_VR_TABLETOP.md](UNITY_VR_TABLETOP.md) (status, verification) with the
headset results, as `CLAUDE.md` requires.
