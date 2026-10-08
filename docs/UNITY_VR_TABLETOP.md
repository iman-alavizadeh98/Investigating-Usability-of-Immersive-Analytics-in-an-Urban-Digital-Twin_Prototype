# Unity VR tabletop mode

VR mode for the `UrbanAnalytics` runtime on Meta Quest: the city is a
"hologram table" and users walk around it. Plan:
[VR_TABLETOP_PLAN.md](VR_TABLETOP_PLAN.md). Decisions:
[decisions/2026-10-08_vr-scale-the-rig-not-the-city.md](decisions/2026-10-08_vr-scale-the-rig-not-the-city.md),
[decisions/2026-10-08_vr-resizable-table-wrist-menu-board.md](decisions/2026-10-08_vr-resizable-table-wrist-menu-board.md).

Status (2026-10-08):
- **Done:** XR setup, table (resizable), pointer abstraction, controller
  picking, the **VR UI** (wrist menu, hover label, table board), controller
  shortcuts (VR equivalents of every desktop key), VR study wording and
  VR log events.
- **Verified** in the Editor with the XR Device Simulator and scripted poses.
- **First headset run (2026-10-08)** found the simulator taking over the
  Quest (fixed, see Assumptions and limits). The fixed build has not yet been
  run in the headset.

The desktop screen-space panels still show on the PC monitor for the
facilitator. In the VR scene their help line lists only the facilitator's PC
keys (no mouse camera or mouse picking in VR).

## How to run

1. Open `Assets/Scenes/Helsingborg_VR.unity`.
2. **With a headset (Quest Link / Air Link):**
   - In the Meta Horizon Link app, make Link the active OpenXR runtime.
   - Start Link in the headset, then press Play.
3. **Without a headset:** press Play. `XRSimulatorFallback` starts the XR
   Device Simulator, and you drive the head and controllers with mouse and
   keyboard (the simulator's help panel shows the keys). If OpenXR loaded
   (Link running) it first waits 15 s for the headset. If the headset
   becomes active later, the simulator is removed and the Quest takes over.
   With the simulator the wrist menu is always shown (the gesture is
   impractical there).
4. Stand at the table. The rig tracks from the **floor** (tracking origin
   Floor), so the table top is 0.90 m above your real floor and your eye
   height is your real one. The first time, hold the Quest's Meta button to
   recentre so you face the table. The Quest boundary must have a correct
   floor height, and room for the table (2.6 m long at start, up to 4 m).

Controls (also on the wrist menu's **Help** tab):

| Input | Action | Desktop key |
|---|---|---|
| Point the right controller | Hover (white overlay + yellow reticle + label) | mouse hover |
| Trigger | Select; on empty table: clear the selection | click |
| Grip held + trigger | Select the area (cell) of a building | Alt+click |
| A (right) | Copy the selection to compare (A, then B) | C |
| B (right) | Clear the selection | Esc |
| Right thumbstick left / right | Previous / next view | 1–9 |
| Right thumbstick up / down (held) | Bigger / smaller table | — |
| Look at the left wrist | Open the wrist menu | — |
| X (left) | Pin the menu in place / send it back to the wrist | — |
| Y (left) | Show / hide the table board | — |
| Left grip (hold) | Grab the table: it follows the hand and turns with the wrist | — |
| Left thumbstick left / right | Turn the table about its centre | — |
| Left thumbstick press | Bring the table in front of you (keeps its rotation) | — |
| Trigger on the left controller | Point with the left hand instead | — |
| Ray on a panel + trigger / thumbstick | Press a button / scroll | click / wheel |
| PC keyboard (facilitator) | 1–9 views, 0 clear view, Esc, C, F2 study panel, [ ] panel size, H | — |

While any controller ray is on a panel, the thumbstick scrolls the panel
and does not resize, turn or switch views. The left grip is also the left
hand's "select the area" modifier; it only does that while the left hand is
the active pointer and its trigger is pressed during the grab.

The Help tab shows these controls as a labelled diagram of the two Quest 3
controllers (`Assets/Textures/UrbanAnalytics/VR/vr_controls.png`, drawn by
`Unity/City_Digital_Twin/Tools/make_vr_controls_image.ps1`; re-run it and
update `XRHandMenu.ControlsText` whenever a control changes).

### Testing with the headset off (proximity sensor)

When the headset is taken off, its proximity sensor pauses the VR session
(OpenXR goes to IDLE), so scripted checks in the Editor stop seeing the
headset. To keep it awake while testing from the PC (needs **Developer Mode**
on the Quest, set in the Meta Horizon phone app, and **USB debugging**
allowed in the headset once):

```bash
ADB="/c/Program Files/Unity/Hub/Editor/6000.3.14f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
"$ADB" devices                                                          # the Quest must be listed as "device"
"$ADB" shell am broadcast -a com.oculus.vrpowermanager.prox_close        # stay awake as if worn
"$ADB" shell am broadcast -a com.oculus.vrpowermanager.automation_disable # back to normal
```

Run the last command after testing; the setting is also reset by a reboot.
Without adb: cover the sensor between the lenses.

## VR UI

**Wrist menu** (`XRHandMenu`): a 26 × 32 cm panel (0.5 mm per canvas unit)
standing just above the **left wrist** (3 cm gap), upright and facing the
eyes. It lives in the rig's Camera Offset space (real metres), smoothed as
it follows the wrist.
- **Opens** when the user raises the left wrist and looks at it like a
  watch: the wrist within 40° of the view centre, closer than 0.75 m, the
  back of the wrist (left-controller direction (−1, 0.5, 0)) facing the eyes
  (dot > 0.25), and the controller not pointing away (not aiming at the
  table). Held for 0.15 s.
- **Closes** 0.6 s after the gesture ends, but never while a controller ray
  is on it.
- **X pins** it where it is (open, fixed in the room, also when the table is
  resized); X again sends it back to the wrist.
- All thresholds are Inspector fields on `VRUI/XRHandMenu`.

Tabs (operated with the right controller's ray):

| Tab | Content |
|---|---|
| Info | The selection: name, "in this view" values (large, with rank in words), foldable detail sections (+ / −), footer IDs. Buttons: Select cell (for a building), Copy → A, Copy → B, Clear. **Opens automatically on a new selection** |
| Views | The visualization list (same catalog as the desktop list) + Clear view; the active view is highlighted |
| Legend | One legend per encoded variable (a second `LegendStackView` filling the panel) |
| Compare | Slots A/B: both block views (drag on a view rotates both) + table A, B, B − A |
| Task | Current study question (the scenario's `questionVr` when set) and answer options, **read only**. The facilitator runs the session on the PC and the participant answers aloud. **Opens automatically when the facilitator presses "Show view"** |
| Help | The VR controls (`XRHandMenu.ControlsText`); no keyboard or mouse wording |

The header line shows the active view.

**Table board** (`XRTableBoard`): a 1.28 × 0.80 m read-only board (2 mm per
canvas unit, body text about 3 cm) standing 15 cm beyond the table edge on
the far side from the user, its bottom 10 cm above the table top. It shows
the current view, its legend and the selected area's values, so the user can
read them without raising a hand. When the user walks more than 50° around
the table it slides to the new opposite side; it follows table resizes. Y
hides/shows it. Rays stop on it (it does not let picks through).

**Virtual hands** (`XRVirtualHands`): stylised glove hands built from
capsules around each controller (palm, thumb, four fingers of three
segments). The index finger curls with the trigger, the other fingers with
the grip; the thumb rests on top. Geometry follows the OpenXR grip pose
(origin in the handle, +Z along the handle from little finger to thumb, +X
right, fingers pointing −Y), built for the right hand and mirrored for the
left. No colliders, so rays pass through. They replace the XRI starter
controller models, which are hidden. Tune on `VRUI → XRVirtualHands`:
`handScale`, `offsetMeters`, `offsetEuler`, colour/material `VRHand`.
- **Why the controllers were invisible:** the repo's `.gitignore` excludes
  `*.fbx`, so the starter `Models/UniversalController.fbx` is not committed;
  on a fresh clone the controller models have no mesh. The hands do not need
  it.

**Compass** (`TabletopRig`): N (yellow), E, S, W lie flat on the table margin
at each edge midpoint, 6 cm tall, fixed to the city (north = +Z = CRS
northing, east = +X = easting) and turned every frame so they read upright
for the viewer. They move, turn and scale with the table.

**Hover label** (`XRHoverLabel`): a small label 3 cm above the hovered
point. It turns toward the user about the vertical axis and shows the name
plus the first 3 values of the current view (same text as the desktop
tooltip).

Rendering rules:
- **Backgrounds are fully opaque.** In linear colour space, the desktop panel
  colour's 6 % transparency lets the bright map show through clearly.
- **Canvas sorting order:** menu 110, board 105, label 100. The selection
  highlight shader is `Transparent+10` and writes no depth, so with a lower
  order it would draw over the panels.
- **Glyphs:** `LiberationSans SDF` has no ▾/▸; foldable sections use + / −
  (desktop too).

Formatting is shared with the desktop: `DesktopInteractionUI`'s
`CreateHighlightRow`, `CreateValueRow`, `ValueWithUnit`, `CompareCell` and
`Difference` are `internal static` and reused, so values read the same in
both modes.

## Display quality

- **MSAA 4×** on `Assets/Settings/PC_RPAsset.asset` (was off). Thin
  buildings, column edges and text shimmered without it. Applies to the
  desktop scene too.
- **Eye-texture resolution scale 1.2** (`TabletopRig.eyeResolutionScale`,
  `XRSettings.eyeTextureResolutionScale`), applied once the headset display
  is active. Link recommended 1824 × 1968 per eye for the Quest 3 on this PC
  (below the panel's 2064 × 2208); 1.2 renders about 2190 × 2360. The log
  line `TabletopRig: XR eye texture W × H` records what is used. Lower it if
  the frame rate drops (GTX 1080 Mobile).
- The Link app's own settings (Devices → Quest 3 → Graphics preferences:
  refresh rate and render resolution) multiply with this; leave them at
  their defaults unless testing them on purpose. Air Link compresses more
  than a USB cable.

## Rebuilding the VR scene

The VR scene is **generated** from the desktop scene. After changing
`Helsingborg.unity` or any VR default, run **UrbanAnalytics → VR → Build VR
Scene**. It overwrites `Helsingborg_VR.unity`; do not hand-edit the VR scene.

`VRSceneBuilder` (`Assets/Scripts/UrbanAnalytics/XR/Editor/`) does the
following:
- copies the desktop scene;
- removes the `DesktopCameraController` camera;
- adds the XRI rig `XR Origin (XR Rig)`, sets its tracking origin to
  **Floor**, and disables `Locomotion`, every `*Teleport*` object and the
  `Poke Point Affordances` (see limits);
- turns off XRI's `CurveVisualController` line visuals;
- adds `VRTable` (`TabletopRig`), `VRPointer` (`XRControllerPointer`, right
  hand first, then left), `VRUI` (`XRHandMenu` with the controls image,
  `XRHoverLabel`, `XRTableBoard`, `XRControllerShortcuts`, `XRTableMover`,
  `XRVirtualHands` with the `VRHand` material and the controller models to
  hide) and `VRDevTools` (`XRSimulatorFallback`);
- points `InteractionManager` (camera, pointer) and `StudySession` (camera,
  `condition = vr`) at the rig;
- sets `DesktopInteractionUI.helpTextOverride` to the facilitator's PC keys;
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
| `XR/TabletopRig.cs` | Waits for the city to load, then scales and places the XR Origin, builds the table and floor, sets clip planes, logs the scale; `SetTableLength` resizes around the fixed table centre; sets the eye-texture resolution scale |
| `XR/XRControllerPointer.cs` | Controller ray from the XRI Near-Far Interactor's stabilized origin; trigger/grip actions; UI blocking (`ActiveHandOnUi`, `AnyHandOnUi`, `IsAnyHandPointingAt`); draws its own ray + reticle in real metres |
| `XR/XRControllerShortcuts.cs` | A / B / Y buttons and the right thumbstick (view flick, held resize); logs finished resizes |
| `XR/XRTableMover.cs` | Left grip grab (move + turn), left thumbstick turn, thumbstick press recall; logs moves |
| `XR/XRVirtualHands.cs` | Stylised hands on the controllers (fingers follow trigger / grip); hides the controller models |
| `Tools/make_vr_controls_image.ps1` | Draws the Help tab's controller diagram (`vr_controls.png`) |
| `XR/XRSimulatorFallback.cs` | Editor only: starts the XR Device Simulator when no headset is active, removes it when the headset starts |
| `XR/XRHandMenu.cs` | Wrist menu (tabs Info / Views / Legend / Compare / Task / Help), see VR UI |
| `XR/XRTableBoard.cs` | Board at the far table edge, see VR UI |
| `XR/XRHoverLabel.cs` | Floating hover label, see VR UI |
| `XR/Editor/VRSceneBuilder.cs` | Builds the VR scene (above) |

Changes to existing code:
- `InteractionManager` has an optional `pointerSource` field (a component
  implementing `IInteractionPointer`), a `Pointer` property, and
  `Pick(Ray, float)`. `Pick(Vector2)` still exists and delegates to it.
- `LegendStackView.Container`: when set before Awake, the view fills that
  container instead of building its own screen canvas.
- `StudySession`: `ScenarioIndex`, `ScenarioCount`, `ViewShown`, `Condition`,
  the event `Changed`, and `LogEvent(name, fields)` for extra events.
  `StudyScenario.questionVr` + `QuestionFor(condition)`: the PC study panel
  and the VR Task tab show the VR wording in the VR condition.
- `DesktopInteractionUI`: formatting helpers are `internal`; new
  `helpTextOverride` (empty = the desktop help line).

## How the table works

- The city is **not** moved or scaled: 1 Unity unit = 1 km stays true. The
  **XR Origin is scaled** by S = world units per real metre. Picking,
  highlights, comparison copies and logging need no change.
- At start, S fits the city's longest side to `maxTableSizeMeters`
  (**2.6 m**) minus a 0.08 m margin on each side. Helsingborg: city
  6.57 × 10.69 km → table **1.66 × 2.60 m, S = 4.380 (1:4380)**, table top
  0.90 m. Set `fixedWorldUnitsPerMeter` to keep one start scale across
  datasets.
- **Resizing** (`SetTableLength`, right thumbstick up/down, exponential:
  length × e^(0.6 · deflection · seconds)) keeps the table-top centre fixed in
  the room (`TableCenterRig`, XR Origin space) and only changes S; the origin
  is placed at `tableCentreWorld − S · TableCenterRig`. Range 1.2–4.0 m
  (S 9.6 … 2.7). The edge nearest the user moves towards them when the table
  grows: step back.
- **Moving and turning** (`MoveTable(centreRig, yaw)`, `BringTableTo(head,
  forward)`, driven by `XRTableMover`): the table's centre and yaw are kept
  in XR Origin space and the origin is placed at
  `tableCentreWorld − R(−yaw) · S · TableCenterRig` with rotation `R(−yaw)`.
  The city never moves (picking, highlights and logs stay in world units),
  only the rig. The grab reads the left controller in XR Origin space, which
  is unaffected by re-placing the rig, so dragging is stable. Every finished
  move is logged (`vr_table_move`).
- Table top = the city base plane (y = 0 under `CityRoot`), 2 mm lower (at
  the start scale) so the flat base layer does not z-fight with it. The user
  starts 0.45 m south of the table, facing north.
- Camera clip planes are set in metres × S (near 0.01 m, far 60 m) after every
  scale change, because Unity camera clip planes ignore the rig's scale. The
  pointer, hover label, wrist menu and board read the scale from the rig every
  frame, so they keep their real-metre sizes.
- The log line `TabletopRig: city … 1 m = … km` records the start scale;
  `XRControllerShortcuts: table resized to …` and the study event
  `vr_table_resize` record every finished resize.

## Study logging in VR

Extra events (StudyLog JSON lines, only while a session runs):

| Event | Fields | When |
|---|---|---|
| `vr_table_resize` | `lengthMeters`, `worldUnitsPerMeter` | Thumbstick released after a resize |
| `vr_menu` | `visible`, `pinned`, `tab` | Wrist menu opened/closed/pinned |
| `vr_menu_tab` | `tab` | Tab shown (by the user or automatically) |
| `vr_board` | `visible` | Board shown/hidden (Y) |
| `vr_table_move` | `how` (grab / turn / recall), `centerX`, `centerZ`, `yawDegrees` | Grab released, turn finished, table recalled |

Selections, views and comparisons are logged as on the desktop. Logged camera
poses are in world units: divide by the current `worldUnitsPerMeter` for
metres. Not logged yet: which hand is active.

## Assumptions and limits

- **Fixed build not yet run in the headset.** In particular the stereo
  separation on a scaled rig, the scale comfort, the wrist gesture
  thresholds and the board's readability need checking in the Quest. Tune in
  the Inspector, then put good values back into the code defaults:
  rebuilding the scene resets them.
- **Study tasks are read only in VR.** The facilitator selects answers on the
  PC. Only the training task (T0) has a `questionVr`; the others contain no
  device wording.
- The thumbstick view flick can leave a scenario's view during a study task
  (the desktop keys 1–9 can too).
- XRI line visuals are off because they are sized in world units. If XRI
  visuals are wanted later, scale their widths and distances by S.
- Columns of a signed/diverging height scale go **below** the base plane and
  therefore into the table.
- `XRSimulatorFallback` (fixed 2026-10-08): it used to decide once at Start.
  Over Link the OpenXR session only becomes active seconds later (and only
  once the headset is worn), so the simulator was spawned on top of the real
  headset. Its simulated HMD took over the camera (keyboard movement, eyes at
  floor height). Now it waits `headsetWaitSeconds` (15 s) and destroys the
  simulator as soon as `XRSettings.isDeviceActive` turns true. If OpenXR did
  not load at Play (`XR_ERROR_FORM_FACTOR_UNAVAILABLE`: Link not started in
  the headset), stop, start Link, and press Play again.
- `VRSceneBuilder` sets the XR Origin to **Floor** tracking (the starter rig
  ships with Not Specified + a 1.36 m camera offset, which lets the runtime
  pick a head-relative origin).
- Interaction profiles for Windows: Meta Quest Touch Plus (Quest 3/3S),
  Touch Pro and **Oculus Touch** as a fallback. XRI's actions and the
  shortcuts bind to generic `XRController` usages, so any of them works.
- The starter rig's **Poke Point Affordances** are disabled by
  `VRSceneBuilder`. Their `MaterialPropertyBlockHelper` has no renderer, so
  they threw a `NullReferenceException` on every tween. Poke interaction itself
  is unchanged.
- Standalone Quest builds are not set up (see the plan).

## How to verify

Checks run 2026-10-08 on the laptop in the Editor (Play mode with the
simulator; scripts kept outside the repo):
1. Console on Play shows `XRSimulatorFallback: … simulator started`, then
   `TabletopRig: city …` and `Picking colliders baked: 59 meshes, 826,677
   triangles`. No errors; the only warnings are 2 XR audio-driver fallbacks.
2. Rig: floor 0.90 m × S below the table top, clip planes scaled by S.
3. Picking with world rays (`InteractionManager.Pick(Ray, float)`): 596/596
   down-rays onto sampled roofs resolve to their own building.
4. Pointer path: right controller frozen 40 cm above and 30 cm south of a roof,
   aimed at it. `InteractionManager.Hovered` = that building.
5. Desktop scene unchanged: `Pointer` = `DesktopMousePointer`, and a simulated
   mouse click on a building selects it.
6. VR UI (head and both controllers posed by script): real UI click through
   `TrackedDeviceGraphicRaycaster`; Info, Legend, Compare and Task tabs; hover
   label; opaque panels.

Headset PC, 2026-10-08 (Alienware 17 R4, GTX 1080 Mobile + Intel HD 630,
Quest 3, Meta Horizon Link runtime 208; OpenXR runtime `oculus_openxr_64.json`;
the Editor renders on the GTX 1080, D3D12, Single Pass Instanced):
- Before the UI work: simulator frame cost (mono) about 7.4 ms CPU, 3.8 ms
  GPU, 1.66 M triangles, 205 draw calls. Quest 3 over Link needs < 13.9 ms
  (72 Hz).
- After the UI work (simulator, scripted):
  - `TabletopRig: city 6.57 × 10.69 km on a 1.66 × 2.60 m table, 1 m = 4.380
    km (scale 1:4380) …; resizable 1.2–4.0 m`; 0 console errors.
  - `SetTableLength(3.6)` → 3.60 m, S 3.107, table 2.27 × 3.60 m; requests of
    9 m and 0.2 m clamp to 4.00 and 1.20; the table centre maps to the city
    centre after resizing.
  - Board (view S2 + a selected voting district): view title, colour and
    height legends and the district's values; after resizing it stands at
    the new far edge.
  - Wrist menu: hidden; left controller posed in a watch-reading pose → shown
    above the wrist facing the eyes; lowered and aimed at the table →
    hidden; pinned then lowered → stays open in place; unpinned → hidden.
    Help tab: 6 tabs fit; text readable in a capture from 42 cm.
  - Shortcuts: the A, B, Y and thumbstick bindings resolve to the simulated
    right/left controllers; step view 4 → 5, Y toggles the board, A fills
    compare slot A, B clears the selection.
  - EditMode tests 62/62.
- Hands, table moving, compass, Help image (simulator, scripted):
  - `MoveTable(+1 m in x, 90°)`: the table centre still maps to the city
    centre; the city's north points along the rig's +X; the letters sit at
    N (+1.26, 0), E (0, −0.79), S (−1.26, 0), W (0, +0.79) m from the centre.
  - `BringTableTo` (viewer at the rig origin looking along +X): near edge
    0.45 m in front, rotation kept.
  - With the table turned 90°, the right-controller ray hovers a building
    (picking unaffected); the "S" letter reads upright on the near edge.
  - Both glove hands are built, the controller models hidden; captures of the
    open and fully closed (trigger + grip) poses.
  - Help tab: the diagram fills the panel width, the text list follows.
  - 0 console errors / warnings; EditMode tests 62/62.
- Headset test of this build: to do (checklist in
  [HANDOFF_VR_HEADSET_TEST.md](HANDOFF_VR_HEADSET_TEST.md)).

## Failure cases

| Situation | Behaviour |
|---|---|
| `TabletopRig` without XR Origin or CityRoot | Error, component disabled, rig stays unscaled |
| City has no visible renderers | Error; the table is not placed |
| `XRTableBoard` without a `TabletopRig` | Error, component disabled |
| `pointerSource` set to a component that is not an `IInteractionPointer` | Error, falls back to the mouse |
| Controller not tracked | No ray, nothing hovered; the wrist gesture does not trigger |
| Ray on UI | City not hovered, trigger does not select, thumbstick scrolls only |
| Resize beyond 1.2–4.0 m | Clamped |
| XRI samples missing | `VRSceneBuilder` throws with the import instructions |
| Play pressed before Link is running | `XRSimulatorFallback: no headset active (no XR loader active)`; start Link, stop and press Play again |
| Link running, headset not worn yet | Simulator after 15 s; removed automatically when the headset becomes active (`… simulator removed`) |
| Headset taken off during Play | Session pauses (proximity sensor); resumes when worn. For testing see "Testing with the headset off" |
