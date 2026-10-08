# Unity VR tabletop mode

VR mode for the `UrbanAnalytics` runtime on Meta Quest: the city is a fixed
"hologram table" and users walk around it. Plan:
[VR_TABLETOP_PLAN.md](VR_TABLETOP_PLAN.md). Decision on the scale:
[decisions/2026-10-08_vr-scale-the-rig-not-the-city.md](decisions/2026-10-08_vr-scale-the-rig-not-the-city.md).

Status (2026-10-08):
- **Done:** XR setup, table, pointer abstraction, controller picking and the
  **VR UI** (hand menu + hover label).
- **Verified** in the Editor with the XR Device Simulator.
- **Not yet tested in a real headset.**

The desktop screen-space panels still show on the PC monitor, so the
facilitator keeps the study panel there.

## How to run

1. Open `Assets/Scenes/Helsingborg_VR.unity`.
2. **With a headset (Quest Link / Air Link):**
   - In the Meta Horizon Link app, make Link the active OpenXR runtime.
   - Start Link in the headset, then press Play.
3. **Without a headset:** press Play. `XRSimulatorFallback` starts the XR
   Device Simulator, and you drive the head and controllers with mouse and
   keyboard (the simulator's help panel shows the keys).
4. Stand at the table. The first time, hold the Quest's Meta button to recentre
   so you face the table.

Controls:

| Input | Action |
|---|---|
| Point a controller | Hover (white overlay + yellow reticle) |
| Trigger | Select; on empty table: clear the selection |
| Grip held + trigger | Select the cell (block) of a building |
| Trigger on the other controller | Makes that hand the active pointer |
| Ray on the hand menu + trigger | Press a button / tab |
| Thumbstick while pointing at a list | Scroll |
| X (left controller) | Show / hide the hand menu |
| PC keyboard (facilitator) | Desktop keys still work: 1–9 visualizations, Esc, C, … |

## VR UI

**Hand menu** (`XRHandMenu`): a panel 31 × 38 cm (0.6 mm per canvas unit)
held 10 cm above the left controller, tilted toward the eyes. It moves with
the hand, so it stays readable from every side of the table. You operate it
with the right controller's ray. Tabs:

| Tab | Content |
|---|---|
| Info | The selection: name, "in this view" values (large, with rank in words), foldable detail sections, footer IDs. Buttons: Select cell (for a building), Copy → A, Copy → B, Clear. **Opens automatically on a new selection** |
| Views | The visualization list (same catalog as the desktop list) + Clear view; the active view is highlighted |
| Legend | One legend per encoded variable (a second `LegendStackView` filling the panel) |
| Compare | Slots A/B: both block views (drag on a view rotates both) + table A, B, B − A (label on its own line, as the panel is narrow) |
| Task | Current study question and answer options, **read only**. The facilitator runs the session on the PC and the participant answers aloud. **Opens automatically when the facilitator presses "Show view"** |

The header line shows the active view.

**Hover label** (`XRHoverLabel`): a small label 3 cm above the hovered
point. It turns toward the user about the vertical axis and shows the name
plus the first 3 values of the current view (same text as the desktop
tooltip).

Rendering rules for both:
- **Backgrounds are fully opaque.** In linear colour space, the desktop panel
  colour's 6 % transparency lets the bright map show through clearly.
- **Canvas sorting order:** the menu is 110 and the label 100. The selection
  highlight shader is `Transparent+10` and writes no depth, so with a lower
  order it would draw over the panels.

Formatting is shared with the desktop: `DesktopInteractionUI`'s
`CreateHighlightRow`, `CreateValueRow`, `ValueWithUnit`, `CompareCell` and
`Difference` are now `internal static` and reused, so values read the same in
both modes.

## Rebuilding the VR scene

The VR scene is **generated** from the desktop scene. After changing
`Helsingborg.unity`, run **UrbanAnalytics → VR → Build VR Scene**. It
overwrites `Helsingborg_VR.unity`; do not hand-edit the VR scene.

`VRSceneBuilder` (`Assets/Scripts/UrbanAnalytics/XR/Editor/`) does the
following:
- copies the desktop scene;
- removes the `DesktopCameraController` camera;
- adds the XRI rig `XR Origin (XR Rig)` and disables `Locomotion` and every
  `*Teleport*` object;
- turns off XRI's `CurveVisualController` line visuals;
- adds `VRTable` (`TabletopRig`), `VRPointer` (`XRControllerPointer`, right
  hand first, then left) and `VRDevTools` (`XRSimulatorFallback`);
- points `InteractionManager` (camera, pointer) and `StudySession` (camera,
  `condition = vr`) at the rig;
- replaces `InputSystemUIInputModule` with `XRUIInputModule`;
- creates materials in `Assets/Materials/UrbanAnalytics/VR/`;
- adds the scene to Build Settings, **disabled**.

## Components

All runtime scripts are in `Assets/Scripts/UrbanAnalytics/XR/` (namespace
`UrbanAnalytics.XR`) and `Assets/Scripts/UrbanAnalytics/Interaction/`.

| File | Role |
|---|---|
| `Interaction/IInteractionPointer.cs` | `IInteractionPointer` + `PointerFrame` (ray, max distance, blocked, clicked, select-block). The only input `InteractionManager` reads for picking |
| `Interaction/DesktopMousePointer.cs` | The mouse behaviour that used to be built into `InteractionManager` (unchanged); used when `pointerSource` is empty |
| `XR/TabletopRig.cs` | Waits for the city to load, then scales and places the XR Origin, builds the table and floor, sets clip planes, and logs the scale |
| `XR/XRControllerPointer.cs` | Controller ray from the XRI Near-Far Interactor's stabilized origin; trigger/grip actions; UI blocking; draws its own ray + reticle in real metres |
| `XR/XRSimulatorFallback.cs` | Editor only: starts the XR Device Simulator when no headset is active |
| `XR/XRHandMenu.cs` | Hand menu (tabs Info / Views / Legend / Compare / Task), see VR UI |
| `XR/XRHoverLabel.cs` | Floating hover label, see VR UI |
| `XR/Editor/VRSceneBuilder.cs` | Builds the VR scene (above) |

Changes to existing code:
- `InteractionManager` has a new optional `pointerSource` field (a component
  implementing `IInteractionPointer`), a `Pointer` property, and
  `Pick(Ray, float)`. `Pick(Vector2)` still exists and now delegates to it.
- `LegendStackView.Container`: when set before Awake, the view fills that
  container instead of building its own screen canvas.
- `StudySession`: `ScenarioIndex`, `ScenarioCount`, `ViewShown` and the event
  `Changed` (raised on every panel refresh).
- `DesktopInteractionUI`: its formatting helpers are `internal` (see VR UI).

## How the table works

- The city is **not** moved or scaled: 1 Unity unit = 1 km stays true. The
  **XR Origin is scaled** by S = world units per real metre. Picking,
  highlights, comparison copies and logging need no change.
- S fits the city's longest side to `maxTableSizeMeters` (1.8 m) minus a
  0.08 m margin on each side. Helsingborg: city 6.57 × 10.69 km → table
  1.17 × 1.80 m, **S = 6.517 (1:6517)**, table top 0.90 m. Set
  `fixedWorldUnitsPerMeter` to keep one scale across datasets.
- Table top = the city base plane (y = 0 under `CityRoot`), 2 mm lower so the
  flat base layer does not z-fight with it. The user starts 0.45 m south of the
  table, facing north.
- Camera clip planes are set in metres × S (near 0.01 m, far 60 m), because
  Unity camera clip planes ignore the rig's scale.
- Placement runs **once**, after geometry, buildings and the startup
  visualization are ready. The log line `TabletopRig: city … 1 m = … km` is the
  record of the scale used.

## Assumptions and limits

- **Not yet tested on a headset.** In particular, the stereo separation on a
  scaled rig and the comfort of the 1:6517 scale need checking in the Quest.
- **UI size was chosen without a headset.** Tune `XRHandMenu.metersPerUnit`
  and its position/tilt, and `XRHoverLabel.metersPerUnit`, in the Inspector
  during the first headset test. Rebuilding the scene resets them to the code
  defaults, so put good values back into the code.
- **Study tasks are read only in VR.** The facilitator selects answers on the
  PC. The current scenario texts in `scenarios.json` mention desktop controls
  ("pan, orbit and zoom… press C"); VR sessions need VR wording.
- No VR-specific log events yet (hand used, menu tab opened, hand-menu
  visible). Selections, views and comparisons are logged as on the desktop.
- XRI line visuals are off because they are sized in world units. If XRI
  visuals are wanted later, scale their widths and distances by S.
- Columns of a signed/diverging height scale go **below** the base plane and
  therefore into the table.
- `XRSimulatorFallback` decides at Start. If the headset connects later, stop
  and press Play again.
- Interaction profiles: Meta Quest Touch Plus (Quest 3/3S) and Touch Pro are
  enabled for Windows. Quest 2 controllers would also need the Oculus Touch
  profile (Project Settings → XR Plug-in Management → OpenXR).
- Standalone Quest builds are not set up (see the plan).

## How to verify

Checks run 2026-10-08 in the Editor (Play mode with the simulator; scripts kept
outside the repo):
1. Console on Play shows `XRSimulatorFallback: … simulator started`, then
   `TabletopRig: city 6.57 × 10.69 km on a 1.17 × 1.80 m table, 1 m = 6.517 km`
   and `Picking colliders baked: 59 meshes, 826,677 triangles`. No errors; the
   only warnings are 2 XR audio-driver fallbacks.
2. Rig: XR Origin scale 6.517, floor 0.90 m × S below the table top, camera
   near 0.065 / far 391 world units.
3. Picking with world rays (`InteractionManager.Pick(Ray, float)`): 596/596
   down-rays onto sampled roofs resolve to their own building.
4. Pointer path: right controller frozen 40 cm above and 30 cm south of a roof,
   aimed at it. `InteractionManager.Hovered` = that building.
5. Screenshot from the user's standing position: the city on the table,
   floor around it.
6. Desktop scene unchanged: `Pointer` = `DesktopMousePointer`, and a simulated
   mouse click on a building selects it. EditMode tests: 62/62 pass.
7. VR UI (head and both controllers posed by script):
   - **real UI click:** the right ray aimed at the "S2 Income vs turnout"
     button hits it through `TrackedDeviceGraphicRaycaster` (pointer
     `Blocked` = true, so the city is not picked). A UI press through the
     interactor's `uiPressInput` applied view S2;
   - **Info:** a selection opens the tab with the title, values of the view
     and sections;
   - **Legend:** S2 shows column colour (turnout) and height (income);
   - **Compare:** copies A/B show both views and the A/B/B − A table;
   - **Task:** Start session + Show view opens the tab with "Task 1 / 7 ·
     training";
   - **hover label:** shows the name and values above the reticle;
   - captures confirmed the opaque panels and that the highlight no longer
     shows through. No console errors.
8. Desktop scene after the UI changes: the screen-space legend still builds
   (2 legends for S2); 62/62 tests pass.

## Failure cases

| Situation | Behaviour |
|---|---|
| `TabletopRig` without XR Origin or CityRoot | Error, component disabled, rig stays unscaled |
| City has no visible renderers | Error; the table is not placed |
| `pointerSource` set to a component that is not an `IInteractionPointer` | Error, falls back to the mouse |
| Controller not tracked | No ray, nothing hovered |
| Ray on UI | City not hovered, trigger does not select |
| XRI samples missing | `VRSceneBuilder` throws with the import instructions |
