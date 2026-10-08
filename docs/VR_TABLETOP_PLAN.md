# VR Tabletop Mode — Implementation Plan

Status: **planned, not started** (2026-10-07). No XR code or packages exist yet.

## What is being built

A VR mode for the existing Unity runtime (`Unity/City_Digital_Twin`, Unity 6000.3, URP, new Input System) on a **Meta Quest** headset.

- The city is shown as a **hologram table**: a fixed table with the city model on it, used for data analysis.
- The **table never moves**. Users change their viewpoint by **physically walking around it**. There is no street view, teleport, flying or artificial camera movement.
- **Single user** at a time. No networking.
- **Runs on the PC** (Windows build or Editor Play mode) over **Quest Link / Air Link**. A standalone Android build is a later option, not the target.
- The **desktop mode must keep working**, because the study compares desktop and VR. Share one codebase and abstract input, camera and UI; do not fork.

## What is reused unchanged

These components carry over without changes:
- data and spatial layers, associations, scales and palettes, glyphs;
- `GeometryManager`, `SpatialMeshChunk`;
- the entity model (`EntityReference`, `EntityInfo`, `EntityHighlighter`, `PickingColliders`, `InteractionContext`);
- the logic in `ComparisonManager` and `VisualizationSwitcher` (only their input bindings change);
- `RuntimeAssetReader`, which already has an Android/Quest path.

## Work items (in order)

1. **XR setup.** Add OpenXR with Meta Quest support and the XR Interaction Toolkit, with single-pass instanced rendering. Add an XR Origin rig and the XR Device Simulator for testing without the headset. Use a separate VR scene, or a mode switch, next to the desktop scene.
2. **Table.** Place a fixed table object (position and height set in the scene or a config file) and parent the city model to it at table scale. Nothing moves at runtime.
3. **Pointer abstraction.** `InteractionManager` builds its ray from `Mouse.current` + `ScreenPointToRay`. Replace that with an interface that supplies a ray plus select/modifier buttons, implemented once for desktop (mouse) and once for VR (controller ray, optionally hand pinch). Do the same for the keyboard shortcuts in `ComparisonManager`, `VisualizationSwitcher` and `InteractionManager` (Alt).
4. **Precise picking at table scale.** Buildings are only millimetres wide at this scale. Smooth the ray, highlight whatever is under the pointer before the user confirms, and prefer a near-range pointer.
5. **World-space UI.** `DesktopInteractionUI` (about 2,500 lines) builds a `ScreenSpaceOverlay` canvas, which does not render in VR.
   - Build a **wrist/hand menu** (layers, visualization switch, filters, comparison) and a **detail panel** opened on selection. The panels follow the user, because people walk around the table.
   - Also needed: XR UI input module, tracked-device raycasting, labels and legend that turn to face the user, and text sized for the headset.
   - Replace the tooltip anchor that uses `Mouse.current.position`.
6. **Study logging (VR fields).** Log head pose around the table, controller pointer hovers and selections, UI panels opened, filter and visualization changes, and the session ID. Write the logs to `persistentDataPath`.

## Open decision

- **Is the map on the table fixed, or can it pan and zoom inside the table?**
  - **Fixed:** the whole city is always shown, and no clipping is needed, but single buildings are hard to pick.
  - **Pan/zoom:** this needs a **clipping shader** on every material (buildings, terrain, overlays, highlights) so content past the table edge is not drawn, and colliders outside the table must be turned off.

## Later / optional (not in the first version)

- **Standalone Quest build (APK).** Needs:
  - `BuildingRuntimeLoader`, `RutaCityLoader`, `MeshManifest`, `PlyParser` and `ProjectPaths` to read through `RuntimeAssetReader` instead of `File.*`;
  - Quest performance work: HDR, post-processing and shadows off, MSAA 4x, foveated rendering, building LODs;
  - checking load time and memory for the 24.5 MB `attributes.json`.
- **Passthrough** (mixed reality on a real table).

## Out of scope

Street-level view, locomotion, multi-user or shared tables, real-time data.
