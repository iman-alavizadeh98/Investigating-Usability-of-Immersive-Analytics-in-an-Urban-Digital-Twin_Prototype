# Decision: buildings following an inset extrusion shrink with the column

**Date:** 2026-10-02
**Status:** Adopted
**Affects:** `Unity/.../Visualization/Renderers/HeightSurfaceRenderer.cs`,
`BuildingSurfaceRenderer.cs`, `HeightSurfaceMeshBuilder.cs`,
`Visualization/Model/VisualizationRuntimeState.cs`

## Context

`InsetExtrusion` draws each Ruta cell as a column shrunk about the cell
centroid (default inset factor 0.8), so neighbouring columns read as separate
bars. A building layer set to `FollowHeightSurface` was lifted to the column
top but kept its real position. Measured on the test data: 51,237 of 181,008
lifted buildings lay entirely outside their column and floated in mid-air, and
78,549 overhung it.

## Decision

When the followed surface is inset, every following building is scaled
horizontally (X and Z, footprint and position) about the **same anchor** and by
the **same factor** as its cell's column. Height is not scaled. The anchor and
factor come from the mesh builder's own `CalculateCentroid` /
`ResolveInsetFactor`, published per unit through `SurfaceElevationField`, so
the column and the buildings cannot drift apart.

Result: buildings entirely off their column 51,237 → 8; overhang 78,549 →
16,652, the same cell-border cases as `FullExtrusion`.

## Alternatives considered

- **Reject `FollowHeightSurface` + `InsetExtrusion`** with a configuration
  error. Simple, but inset mode could then never show buildings on the data.
- **Lift only buildings that lie on the column**, leave the rest on the
  ground. Many ground buildings would be hidden inside or beside the columns
  and would look like data for the wrong height.
- **Leave buildings floating** and document it. Misleading in a study setting.

## Consequences

- In inset mode the city layout is compressed by the inset factor inside each
  cell; gaps open along cell borders. This is intended: inset mode is an
  abstract "bars" view, not a geographically exact one.
- Building footprints in inset mode are 20% smaller (default factor) than in
  reality. Anything measuring buildings (e.g. future picking or distance tools)
  must read the original geometry, not the displaced mesh.
- Resetting the visualization restores the original vertices exactly
  (verified by checksum).
