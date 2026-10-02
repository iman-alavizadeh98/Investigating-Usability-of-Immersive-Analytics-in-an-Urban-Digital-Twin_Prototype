# Unity desktop interaction: camera, picking, selection, runtime switching, block comparison

Desktop (mouse + keyboard) interaction for the `UrbanAnalytics` runtime. Added
2026-10-02 (roadmap item 5). Design decisions:
[decisions/2026-10-02_desktop-picking-and-block-comparison.md](decisions/2026-10-02_desktop-picking-and-block-comparison.md).

## What it does

| Feature | How |
|---|---|
| Camera | Map-style orbit camera: pan, orbit, zoom to cursor, focus, home view |
| Picking | Pointer ray → `RaycastHit.triangleIndex` → entity ID through the chunk's triangle map (cells, height columns, glyphs, buildings) |
| Hover | Light overlay on the entity under the cursor + tooltip with the values the current visualization encodes |
| Selection | Click selects (yellow overlay); panel lists **every** variable of every data layer on the cell, with unit and percentile rank |
| Runtime switching | List of visualizations (scene startup spec + catalog in `StreamingAssets/visualizations/`), keys 1–9, 0 = clear |
| Block copy + compare | Copy a selected cell ("block") with everything drawn for it (surface/column, glyphs, its buildings) into slot A or B; both copies are shown side by side at the same scale with linked views and a value table (A, B, B − A) |

"Block" = one spatial unit of the clicked layer. With the current data that is
a 250 m Ruta cell (`ruta_250`); the code works for any polygon spatial layer,
so a real city-block layer would work unchanged.

## Controls

| Input | Action |
|---|---|
| Left click | Select (empty space clears) |
| Alt + left click | Select the cell under a building |
| Left drag / middle drag | Pan (the ground under the cursor follows the cursor) |
| Right drag | Orbit |
| Wheel | Zoom toward the cursor |
| W A S D / arrows | Pan · Shift = faster |
| Q / E | Rotate |
| + / − , PageUp / PageDown | Zoom |
| F | Focus the selection |
| Home | Overview of the whole city |
| C | Copy the selection's block into the next comparison slot |
| Esc | Clear the selection |
| 1–9 / 0 | Apply visualization 1–9 / clear |
| H | Hide/show the help line |
| [ / ] | Smaller / larger UI (remembered) |
| Drag / wheel on a comparison view | Rotate / zoom **both** copies |

Input that starts over UI is ignored by the camera and by picking. A press
becomes a drag after 6 px (`DesktopCameraController.ClickDragThresholdPixels`);
only a press without a drag selects.

## Where it lives

All under `Unity/City_Digital_Twin/Assets/`.

| File | Role |
|---|---|
| `Scripts/UrbanAnalytics/Interaction/InteractionManager.cs` | Picking, hover, selection, highlights, focus; owns the services below |
| `.../Interaction/InteractionContext.cs` | Read-only access to the managers; unit → layer lookup, unit anchors, `BuildingIndex` (building ID → chunk/range, cell → buildings) |
| `.../Interaction/EntityReference.cs` | `EntityReference` (cell or building, with the building's cell), `PickResult` |
| `.../Interaction/PickingColliders.cs` | Keeps MeshColliders on shown chunks only; cooks them on worker threads |
| `.../Interaction/EntityGeometry.cs` | `EntityGeometryCollector`: copies an entity's shown triangles out of the batched chunks |
| `.../Interaction/EntityHighlighter.cs` | Named overlays (hover, selection, slot markers) |
| `.../Interaction/EntityInfo.cs` | `EntityInfoBuilder`: values, units, percentile ranks, "encoded" flags |
| `.../Interaction/ComparisonManager.cs` | Slots A/B, block copies, stage cameras, render textures |
| `.../Interaction/VisualizationSwitcher.cs` | Runtime switching + catalog loading |
| `.../Interaction/DesktopCameraController.cs` | Camera (on `Main Camera`) |
| `.../Interaction/UI/DesktopInteractionUI.cs` | Screen-space UI, built in code at startup |
| `.../Interaction/UI/RuntimeUi.cs`, `ComparisonViewInput.cs` | uGUI factory; drag/wheel on comparison views |
| `Shaders/UrbanAnalytics/HighlightOverlay.shader` + `Materials/UrbanAnalytics/HighlightOverlay.mat` | Overlay shader (visible pass + faint pass behind other geometry) |
| `StreamingAssets/visualizations/catalog.json` + `*.json` | Runtime-switchable visualizations |
| `Tests/Editor/UrbanAnalytics/InteractionTests.cs` | EditMode tests (references, percentile ranks, formatting) |

Scene (`SampleScene`): `/UrbanAnalytics/InteractionSystem`,
`/UrbanAnalytics/ComparisonSystem`, `/UrbanAnalytics/VisualizationSwitcherSystem`,
`DesktopCameraController` on `Main Camera`, `/InteractionUI`. Project setting:
user layer 8 = `ComparisonStage`.

Changes to existing code:
- `VisualizationManager`: `StartupVisualization`, `IsApplying`, `IsBusy`,
  events `VisualizationChanging` (before an apply/clear touches meshes) and
  `VisualizationApplied` (after every successful apply; `VisualizationChanged`
  fires only when there is a legend).
- `BuildingMeshChunk`: `GeometryVersion` / `NotifyGeometryChanged()`;
  `BuildingMeshUnitRange.HeightMeters`.
- `BuildingSurfaceRenderer`: bumps the version when it moves buildings and
  only writes vertices back on clear when it moved them.

## UI sharpness and size

- **Text is TextMeshPro** (SDF fonts, sharp at any scale). It needs the TMP
  Essential Resources in `Assets/TextMesh Pro/`, which were imported from the
  uGUI package on 2026-10-02. The colour legend (`VisualizationLegendView`)
  uses TMP too.
- **Scale.** The canvas uses constant pixel size with
  scale = UI scale × clamp(min(width / 1600, height / 900), 0.75, 4). The UI
  therefore grows on large screens and never shrinks below readable size.
  `[` / `]` or the A− / A+ buttons change the UI scale; it is remembered per
  machine in `PlayerPrefs` (`UrbanAnalytics.DesktopUiScale`). The legend
  canvas gets the same scale at runtime.
- **Layout adapts to the window.** The selection panel spans from below the
  legend to the bottom and narrows on narrow screens. The comparison views
  take the width beside it (200–440 units). The visualization list collapses
  automatically when the compare panel needs the room (unless you collapsed
  it yourself). The help line hides when there is no room between the side
  panels (the picking status always shows).
- **Comparison views render at exactly their on-screen pixel size**
  (`ComparisonManager.SetViewPixelSize`), so there is no upscaling blur.
- **Editor tip:** if everything (3D included) looks soft in the Game view,
  uncheck *Low Resolution Aspect Ratios* in the Game view's aspect dropdown.
  With Windows display scaling it renders at e.g. 733×412 and stretches the
  image. It was switched off on this machine on 2026-10-02. This is an Editor
  setting only; builds are unaffected.

## How picking works

1. `PickingColliders` adds a non-convex `MeshCollider` to every **shown**
   `SpatialMeshChunk` (base layer, height columns, glyphs) and
   `BuildingMeshChunk`, and disables colliders on hidden chunks (a height
   surface hides the flat source cells; they must not catch rays).
2. Colliders are cooked with `Physics.BakeMesh` in a parallel job, then assigned
   with the same cooking options, so assignment reuses the cooked data. The
   main thread waits for the rest after 3 frames (Unity's temporary job memory
   must not live longer than 4 frames).
3. They are rebuilt when the scene is stable after each apply/clear
   (`VisualizationManager.IsBusy == false`). `VisualizationChanging` waits for a
   running bake first, so no job reads a mesh while a renderer changes it.
   Building chunks are re-cooked only when `GeometryVersion` changed (buildings
   moved onto or off a height surface).
4. `Physics.Raycast` → `SpatialMeshChunk.TryGetUnitIdForTriangle` or
   `BuildingMeshChunk.TryGetBuildingForTriangle`. Buildings carry their cell
   (`AssociatedSpatialUnitId`), resolved to its layer by `InteractionContext`.

`InteractionManager.PickingStatus` / `IsPickingReady` report the state; the UI
shows "Preparing picking…" while colliders are rebuilt.

## Info panel

`EntityInfoBuilder.Build` returns sections:
- **Building** (for a building): ID, source height (m), its cell.
- **Cell**: ID, name, centre (SWEREF 99 TM), area (m²), number of associated
  buildings, mean and tallest building height (source heights).
- One section **per data layer that targets the cell's spatial layer**: every
  variable, its unit, and its **percentile rank** among all units with a valid
  value (mid-rank: share below + half the ties; `p52` = 52%). Missing values
  show "no data".

Values come straight from the data layers (no scale or palette applied).
Variables used by the active visualization are marked ●.

## Block copies and comparison

`ComparisonManager.CopyToSlot` takes the cell of the selection (a building →
its cell; a building outside every cell → the building alone) and copies every
triangle currently drawn for it: cell surface or height column, glyphs on it,
and its associated buildings (with their current colours and materials). A
neutral footprint of the cell is added 0.5 m below the base plane as a ground
reference.

Fairness rules (so the two copies can be compared by eye):
- same world scale (1 unit = 1 km) and same base plane (cell centroid on the
  ground = copy origin);
- both cameras use the **same pose relative to their copy**, with a distance
  that fits the larger copy, so equal pixels mean equal metres;
- rotating or zooming one view moves both;
- copies and the table are **rebuilt after every visualization change**, so
  both always show the active encoding (never one old and one new).

Copies live 100 units below the city on layer `ComparisonStage`; the main
camera's culling mask excludes that layer, and each slot camera renders only
its own stage into a 640×480 RenderTexture shown in the UI. Slot cells are
marked on the map in orange (A) and teal (B).

The table lists every comparable value (data variables + cell facts) as
A, B and B − A (plus the change relative to A).

## Runtime visualization catalog

`StreamingAssets/visualizations/catalog.json`:

```json
{ "visualizations": [
  { "id": "population_bars", "displayName": "Population (bars on income map)", "file": "population_bars.json" }
] }
```

Each file is a `VisualizationSpec` in JsonUtility JSON — the same schema as the
Inspector (enums as numbers; see `UNITY_GLYPHS_AND_BIDIRECTIONAL_HEIGHT.md`).
The option list is the scene's startup spec first, then the catalog in order.
Keys 1–9 address the first nine.

The seven presets shipped now are the specs verified in the glyph work (bars,
two-sided bars, signed bars, stacked and radial age glyphs, two-sided columns
with buildings, income columns with stacked bars). They use the current
**test** data layers.

A missing catalog logs a warning and only the startup spec is offered. A broken
entry (missing file, invalid JSON, unconfigured spec, duplicate ID) is an
error shown in the Visualizations panel.

## Assumptions and limits

- Desktop only. The picking, info, highlight and copy services take a ray or an
  entity and do not depend on the mouse, so a VR ray interactor can reuse them.
  The camera controller and the screen-space UI are desktop-only.
- **Collider memory was not measured.** Cooking all building colliders
  (3.3 M triangles) takes ~0.4 s, but Unity's allocator total did not change
  when they were removed (the cooked data seems to be held elsewhere), so there
  is no reliable figure. Measure with the Memory Profiler before enabling
  building picking on Quest; `InteractionManager.pickBuildings = false` picks
  cells only.
- Coincident surfaces are ambiguous by nature: the shared walls of two adjacent
  extruded columns, a glyph face lying exactly on a building roof, and
  duplicate building footprints in the source data (two pairs seen in a 20,160
  building sample, e.g. `building:111540` / `building:170919`). The ray then
  returns either one, matching what the renderer shows (z-fighting).
- Only one legend is shown (unchanged; roadmap item 6).
- No interaction logging yet: `HoverChanged`, `SelectionChanged`,
  `ComparisonManager.Changed` and `VisualizationSwitcher.Changed` are the hook
  points for study logging. Selections, copies and switches are written to the
  Unity console.
- At very small window sizes (below ~1000×560 px) the panels still cover much
  of the map. Collapse the visualization list or shrink the UI with `[`.

## How to verify

1. EditMode tests: `UrbanAnalytics.Tests` (Test Runner → EditMode), including
   `InteractionTests`.
2. Play `SampleScene`. The console logs
   `Picking colliders baked: 289 meshes, 3,345,969 triangles in ~0.4 s`.
   Hover, click, Copy → A / B, and press 1–8 / 0.
3. Play-mode checks used on 2026-10-02 (scripts kept outside the repo; method
   below):
   - **Exact picking.** Cast a short ray into the centroid of sampled
     triangles along their normal. The hit must be the same chunk, the same
     triangle index and the expected entity. Startup scene: 11,757/11,757 cell
     triangles (3 per cell) and 20,158/20,160 buildings. The 2 misses are
     duplicate footprints.
   - **Switching through all 8 visualizations and Clear.** After each one:
     colliders exist only on shown chunks, the selection overlay and both
     copies have exactly the expected triangle counts, and there are no
     console errors. After Clear, the building vertex/colour checksums equal
     the pristine ones, and there are no leaked meshes.
   - **Info panel.** All 12 data values equal the raw data layers.
     Percentiles equal an independent recomputation. The building count equals
     a scan of the building ranges.
   - **Simulated input (Input System events).** Hover and click select the
     cell under the pointer, a drag pans without changing the selection, the
     wheel zooms, C copies, Esc clears and 2 switches. Input over UI is ignored.

## Failure cases

| Situation | Behaviour |
|---|---|
| A manager is missing | Interaction stays off; status line says so |
| Highlight material unassigned | Falls back to `Shader.Find("UrbanAnalytics/HighlightOverlay")` (Editor only); error if not found |
| Layer `ComparisonStage` missing | Warning; copies on Default layer (main camera may see them below the city) |
| Nothing drawn for a block in the active visualization | Copy shows only the footprint; the view says so |
| Visualization apply fails | Error text in the Visualizations panel; scene restored by the renderers |
