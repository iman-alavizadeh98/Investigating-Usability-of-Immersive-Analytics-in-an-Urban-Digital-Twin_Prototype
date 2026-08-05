# Decision: freeze the analytical grid anchor at (298000, 6383500)

**Date:** 2026-08-05
**Status:** Adopted
**Affects:** `Src/mesh_generation/grid_reference.py` (new), grid/district strategies,
mesh manifests, `Unity/.../CityMeshLoader.cs`

## Context

`GridStrategy.partition()` started the grid at `buildings_gdf.total_bounds` — the
extent of whatever data was passed in. Two consequences:

1. **Cell ids were not reproducible.** Process a subset, or add data, and the grid
   origin moves; `grid_003_007` then names a different piece of ground. Any
   analytic keyed on a cell id silently compares different places between runs.
2. **The grid did not align with the statistical grid.** The shipped run's origin
   was `(297697.899, 6383590.000)` — `mod 500` = **(197.9, 90.0)**. SCB `Ruta`
   cells (100/250/500/1000 m) would have been clipped by every cell boundary,
   forcing area-weighted interpolation instead of an exact join.

`CityMeshLoader.cs` had the same defect on the Unity side: it derived the scene
world origin from the run's own south-west corner, so regenerating the data
shifted every world coordinate — making interaction coordinates logged in one
evaluation session incomparable with another.

## Decision

One frozen lattice, defined once in `Src/mesh_generation/grid_reference.py` and
imported by both the mesh strategies and the cell-attribute pipeline:

```python
GRID_ANCHOR_X = 298000.0
GRID_ANCHOR_Y = 6383500.0     # EPSG:3006 / SWEREF99 TM
DEFAULT_CELL_SIZE_M = 500
CELL_ID_FORMAT = "grid_{col:+04d}_{row:+04d}"
```

Cell ids are **signed and anchor-relative** — a global lattice address, not a
per-run sequence number. Negative indices are therefore possible and must not
collide with positive ones, hence the explicit sign.

The anchor is written into every manifest (`grid_reference` block), and each group
carries its nominal `cell` corner alongside the existing content-bbox `origin`.
Unity reads the anchor and uses it as a stable world origin.

### Why this anchor

- **Multiple of both 500 and 1000**, so every SCB `Ruta` cell size (100, 250, 500,
  1000 m) nests into the 500 m lattice with no partial cells and no area-weighting.
  Verified across all 9 Ruta layers: every source cell ≤500 m nests into exactly
  one target cell.
- **298000** matches the project scope west bound already used in the (now
  deprecated) `preprocess_spatial_joins.py`.
- **6383500** is the multiple of 500 immediately below the southern data edge
  (6,383,590 — the municipal boundary).

## Alternatives considered

**Snap to the data extent, rounded down to 500 m** — still moves whenever the
extent changes. Solves alignment but not reproducibility.

**Use the SCB Ruta extent as the anchor** — would work today, but ties the mesh
lattice to one statistical delivery; a new SCB extract could move it.

**Keep per-run origins and store a transform** — pushes the problem downstream and
makes every consumer responsible for a correction they can silently forget.

## Consequences

- Cell ids change format and value; the run must be regenerated.
- `--grid-anchor-x/y` exist for deliberate experiments, but the default keeps mesh
  cells and analytical cells on the *same* lattice — the property the whole
  analytics join depends on.
- Unity world coordinates are now stable across regenerations, so interaction logs
  are comparable across evaluation sessions.
- `CityMeshLoader` warns when loading a manifest that predates the anchor.
- `_create_grid_districts()` is snapped to the same lattice (5000 m is a multiple
  of 500).

## Verification

- All 2,160 grid cells satisfy `cell_origin_x % 500 == 0` and
  `cell_origin_y % 500 == 0` (the shipped run: **none** did).
- Anchor stability: partitioning the full 201,594 buildings and a random 30%
  subset put **100%** of the 60,478 shared buildings in the same cell id.
  (`test_grid_anchor_stability`)
- All 9 SCB Ruta layers nest into the lattice at this anchor.
