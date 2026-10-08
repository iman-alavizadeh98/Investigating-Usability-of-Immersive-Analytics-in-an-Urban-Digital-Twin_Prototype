# Handoff: first VR headset test (GTX 1080 PC)

Written 2026-10-08 on the laptop (BIBOP: i7-1255U, Intel Iris Xe only). The
laptop cannot run Quest Link, so the VR mode has only been tested with the XR
Device Simulator. The next session runs on the PC with the **GTX 1080** and a
**Meta Quest** connected through Meta Horizon Link.

Read first: `CLAUDE.md` → `Project_livingContext.md` →
[UNITY_VR_TABLETOP.md](UNITY_VR_TABLETOP.md) (the VR mode doc) →
[VR_TABLETOP_PLAN.md](VR_TABLETOP_PLAN.md) (the plan).

## Where things stand

- **VR mode:** the city is a **hologram table**, 1.66 × 2.60 m at start
  (1:4380), resizable 1.2–4.0 m with the right thumbstick (since the
  2026-10-08 usability pass; it was fixed at 1.17 × 1.80 m). One user at a
  time walks around it: no locomotion, no street view.
- **Running mode:** the app runs on the PC over **Quest Link** (Play in the
  Editor, or a Windows build). A standalone Quest build is not set up.
- **Done (plan items 1–5):**
  - OpenXR + XR Interaction Toolkit 3.6.1;
  - `TabletopRig`: the **XR Origin is scaled, not the city**;
  - controller-ray picking through `IInteractionPointer`;
  - the hand menu on the left controller (tabs Info / Views / Legend /
    Compare / Task) and a hover label;
  - 2026-10-08 usability pass (after the first headset run): wrist menu that
    opens when looked at (X pins it, new Help tab), table board (Y), A / B /
    thumbstick shortcuts, resizable table, MSAA 4× + eye-texture scale 1.2,
    VR study wording and VR log events. See
    [UNITY_VR_TABLETOP.md](UNITY_VR_TABLETOP.md).
- **Scene:** `Assets/Scenes/Helsingborg_VR.unity` is **generated** from
  `Helsingborg.unity` by **UrbanAnalytics → VR → Build VR Scene**
  (`VRSceneBuilder`). Do not hand-edit it.
- **Commits:** "first vr iteration development" and "Updated UI for VR".

## Step 1: check the new machine

**Done 2026-10-08 on the PC** (Alienware 17 R4, GTX 1080 Mobile, Quest 3).
All checks pass. Repo clone at
`W:\Investigating-Usability-of-Immersive-Analytics-in-an-Urban-Digital-Twin_Prototype`;
Unity CLI at `%LOCALAPPDATA%\Unity\bin\unity.exe`. Changes made: Oculus Touch
fallback profile on; Poke Point Affordances disabled by `VRSceneBuilder` (they
threw an NRE on every tween); scene rebuilt. See
[UNITY_VR_TABLETOP.md](UNITY_VR_TABLETOP.md), How to verify. Next: Step 2.

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
   - `XRSimulatorFallback: headset active …`, or `… simulator removed` if
     you put the headset on after Play. If it says `no XR loader active`,
     Link was not running: start Link, then stop and press Play again;
   - `TabletopRig: city 6.57 × 10.69 km on a 1.66 × 2.60 m table, 1 m = 4.380 km …`;
   - `Picking colliders baked: 59 meshes, …`;
   - `TabletopRig: XR eye texture W × H (resolution scale 1.20)`.
4. In the headset, hold the Meta button to recentre so you face the table.
5. If the agent drives checks while the headset is off, keep it awake with
   adb (`prox_close`), see "Testing with the headset off" in
   [UNITY_VR_TABLETOP.md](UNITY_VR_TABLETOP.md). Needs Developer Mode.

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
- [ ] **Sharpness.** Edges and text look clean (MSAA 4×, eye scale 1.2) and
      the frame rate holds; if not, lower `TabletopRig.eyeResolutionScale`.
- [ ] **Table size.** Right thumbstick up/down resizes smoothly; the start
      size (2.6 m) and the 1.2–4.0 m range suit the room.
- [ ] **Wrist menu** (left wrist; opens when you look at it; X pins).
  - Opens reliably when reading the wrist like a watch, and does **not** pop
    up while pointing with the left hand or walking around.
  - Text readable, size right (26 × 32 cm), stays open while you use it.
  - Tune on `VRUI` → `XRHandMenu`: `wristOffsetMeters` (0, −0.01, −0.09),
    `panelGapMeters` 0.03, `metersPerUnit` 0.0005, `viewConeDegrees` 40,
    `maxWristDistanceMeters` 0.75, `wristFaceNormal` (−1, 0.5, 0),
    `facingThreshold` 0.25, `hideDelaySeconds` 0.6. **Write the final values
    back into the code defaults:** rebuilding the scene resets them.
- [ ] **Table board** (far table edge; Y hides it): readable from the user's
      side, moves sensibly when walking around, does not block the view.
      Tune `XRTableBoard.metersPerUnit` (0.002), `repositionDegrees` (50).
- [ ] **Shortcuts.** A copies to compare, B clears, thumbstick left/right
      switches views; with the ray on a panel the thumbstick only scrolls.
- [ ] **Hands.** The glove hands sit on your real hands (tune
      `XRVirtualHands.offsetMeters` / `offsetEuler` / `handScale`), fingers
      follow trigger and grip.
- [ ] **Moving the table.** Left grip drags and turns it smoothly; left
      thumbstick turns it; thumbstick press brings it in front of you.
- [ ] **Compass.** N / E / S / W readable on the table edges; N matches the
      map (Öresund to the west of Helsingborg).
- [ ] **Help tab.** The controller diagram is readable without zooming.
- [ ] **Selecting (bug fixed).** Trigger on a building / area selects it; the
      label stays above the selection; the Editor log shows
      `InteractionManager: click selected …` for every click.
- [ ] **Hands.** Put the controllers down: tracked hands appear; pinch
      selects; pinch on a toolbar button presses it; poking works; left fist
      grabs the table; the wrist menu opens when looking at the back of the
      left wrist (tune `handFaceNormal` if not). Pick a controller up: it
      takes over again.
- [ ] **Toolbar.** Reachable and readable at the table edge; follows you
      around the table; Buildings on/off makes buildings unselectable.
- [ ] **Copies.** A / Copy makes a copy above the selection with a line and a
      label; grip / pinch on it moves it; its label's Remove removes it;
      Remove copies clears them all.
- [ ] **Moving panels.** Grip / pinch on the Move bar under the menu, board
      and toolbar carries them; they stay; Reset panels brings them back.
- [ ] **Information.** Hover shows the top values; the board and Info show
      them first, then all sections.
- [ ] **Study in VR.** Task tab: Start → training → Show view → Done → task →
      answer + confidence → Submit; the PC panel follows along.
- [ ] **Help at start.** The Help tab opens in front of you; Close dismisses it.
- [ ] **Hands diagnosis (optional, hands are off by default).** With hand tracking on in the Quest (and Developer
      runtime features in the Link app if needed), put the controllers down
      and read the `XRInputModeSwitch:` lines in the Editor log (hand
      subsystem running? left/right tracked? input mode TrackedHand?).
      Hands on/off on the toolbar switches to controllers only.
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
2. ~~VR wording for study tasks~~ done 2026-10-08: `questionVr` on T0.
3. **Plan item 6: VR-specific log events.** Done 2026-10-08: `vr_menu`,
   `vr_menu_tab`, `vr_board`, `vr_table_resize`. Still missing: the active
   hand, and the rig scale at session start (only resizes are logged; the
   start scale is in the Unity log). Logged camera poses are in world units:
   divide by the current rig scale for metres.
4. **Decided 2026-10-08:** a resizable table instead of a fixed one. Panning
   inside the table (needs a table-edge clipping shader) stays open.
5. **Known limits:**
   - columns of signed/diverging heights go into the table;
   - XRI's own line visuals are off, because they are sized in world units;
   - ~~`XRSimulatorFallback` decides once at Start~~ fixed 2026-10-08: it
     waits for the Link session and removes the simulator when the headset
     starts (first headset run had the simulator on top of the Quest).
6. **`Assets/_Recovery/`** (an untracked Unity crash-recovery scene on the
   laptop): check it, then delete it or ignore it.

## After the test

Update `Project_livingContext.md` (status + changelog row) and
[UNITY_VR_TABLETOP.md](UNITY_VR_TABLETOP.md) (status, verification) with the
headset results, as `CLAUDE.md` requires.
