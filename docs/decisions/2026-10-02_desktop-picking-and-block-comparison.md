# Decision: desktop picking, highlighting and block comparison

**Date:** 2026-10-02
**Status:** Adopted
**Affects:** `Unity/.../Scripts/UrbanAnalytics/Interaction/`, `VisualizationManager`,
`BuildingMeshChunk`, `BuildingSurfaceRenderer`. Usage:
[docs/UNITY_DESKTOP_INTERACTION.md](../UNITY_DESKTOP_INTERACTION.md)

## Context

The runtime had no interaction: no camera control, no picking, no way to
switch visualizations after startup. Chunks already stored per-entity triangle
ranges "for future RaycastHit.triangleIndex selection". The user also asked
to select a block, get a copy of it with more information, and compare it to
another block.

## Decisions

1. **Physics MeshColliders, cooked on worker threads.** Picking uses
   `Physics.Raycast` + `RaycastHit.triangleIndex` + the chunk triangle maps.
   Colliders exist only on shown chunks, are cooked with `Physics.BakeMesh` in
   a parallel job (whole city: 289 meshes, 3.3 M triangles, ~0.4 s), and are
   rebuilt after each visualization change. Building chunks are re-cooked only
   when their new `GeometryVersion` changed.
2. **Highlights are separate overlay meshes**, copied from the entity's shown
   triangles and drawn with a transparent shader (depth offset + faint pass
   where hidden). The visualization's vertex colours and materials are never
   touched, so highlights cannot corrupt an encoding or a reset.
3. **A block copy is a snapshot of what is drawn, rebuilt on every
   visualization change.** It holds the cell's surface/column, glyphs and
   associated buildings, at true scale. Both slots share one camera pose
   relative to their copy and one distance (fits the larger copy), and their
   views are linked.
4. **Runtime visualizations come from JSON spec files listed in
   `StreamingAssets/visualizations/catalog.json`**, read through
   `RuntimeAssetReader` (works on Android/Quest, which cannot list
   StreamingAssets). The scene's Inspector spec stays first in the list.
5. **The desktop UI is built in code** (uGUI), so the scene holds one component
   and no prefabs. Amended the same day after the first build was blurry and
   small: text is **TextMeshPro** (legacy `Text` blurs when scaled; the legend
   was converted too). The canvas uses an explicit pixel scale with a floor
   (0.75) instead of shrinking to a 1920×1080 reference, plus a user UI scale.
   Panels adapt to the free space, and the comparison render textures match
   their on-screen pixel size.

## Alternatives considered

- **CPU ray–triangle tests against cached mesh arrays (no physics).** No cooking
  and no PhysX memory, but it needs our own acceleration structure to be fast.
  The chunk ranges were designed for `triangleIndex`, and cooking turned out
  cheap. Revisit for Quest if collider memory proves too high (not measured
  yet).
- **Synchronous `MeshCollider` assignment.** Simplest, but it cooks 3.3 M
  triangles on the main thread at startup and after every switch.
- **Highlight by changing vertex colours.** No extra meshes, but it fights the
  renderers that own those colours and makes restore logic fragile.
- **Copies as data cards only (no 3D).** Less code, but "get a copy" of a block
  and comparing its built form (column height, glyph, buildings) needs
  geometry. The data table is included anyway.
- **Copies kept as frozen snapshots.** After a switch, slot A could show the
  old encoding and slot B the new one, which is a misleading comparison.
- **Placing copies in the city** (lifted above the original). It is hard to
  bring two distant blocks together and the city occludes them. The stage
  + render textures keep both side by side. A VR "table" can host the same
  copies later.
- **ScriptableObject presets.** Inspector-friendly, but not portable to
  external study scenarios (roadmap item 12); JSON uses the same schema as the
  Inspector spec.

## Consequences

- `VisualizationManager` exposes `IsBusy`, `VisualizationChanging` and
  `VisualizationApplied`; features that read meshes off the main thread must
  finish on `VisualizationChanging`.
- `BuildingSurfaceRenderer` restores vertices only for chunks it moved, so a
  colour-only layer no longer rewrites (and invalidates) every building mesh on
  clear. Verified: reset checksums unchanged.
- Coincident surfaces (shared walls of adjacent columns, glyph faces on
  roofs, duplicate building footprints in the data) give an arbitrary but
  visually consistent pick.
- The picking/info/copy services are ray- and entity-based, so a VR
  interactor can reuse them; only the camera controller and the screen UI are
  desktop-specific.
