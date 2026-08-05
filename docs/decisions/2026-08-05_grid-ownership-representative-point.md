# Decision: single-owner building assignment by representative point

**Date:** 2026-08-05
**Status:** Adopted
**Affects:** `Src/mesh_generation/strategies/{grid,quadtree,district}.py`, `generator.py`

## Context

`GridStrategy.partition()` selected buildings with
`self.buildings_gdf.geometry.intersects(cell_geom)`, which assigns a building to
**every** cell it touches.

Measured on the shipped run `Processed_data/Gothenburg/building_meshes_grid_500m/`:

| | |
|---|---|
| building-id slots across all cells | 211,620 |
| unique building ids | 201,594 |
| buildings in more than one cell | **9,690** |
| duplication histogram | 9,475 in 2 cells · 94 in 3 · 121 in 4 (corners) |
| inflation | **5.0%** |

`MeshStrategy.validate()` already documented a "STRICT DETERMINISTIC OWNERSHIP
POLICY" requiring exactly one group per building, and **did** detect this. But
`generator.py` only called `logger.warning` and continued, so the run completed
and the inflated data shipped. No test instantiated a real strategy, so nothing
caught it either.

This matters beyond mesh duplication: the 500 m grid is the join key for every
cell-level analytic. A building counted in four cells corrupts any per-cell
statistic derived from building geometry.

## Decision

Assign each building to exactly one group by the cell containing its
**representative point**, and make ownership violations fail the run.

1. **`representative_point()`**, not `centroid`. Shapely guarantees the
   representative point lies inside the polygon. A centroid can fall *outside* a
   concave (L/U-shaped) or donut footprint — which would file a building under a
   cell it does not occupy.
2. **Half-open cell intervals** (`floor()` division): a point exactly on a
   boundary belongs to the cell above/right. `quadtree.py` uses explicit
   `px >= minx & px < maxx` tests so midline ties break identically.
3. **`generator.py` raises `StrategyValidationError`** by default
   (`GeneratorConfig.strict_ownership=True`), with `--allow-ownership-violations`
   as a deliberate escape hatch.
4. **`district.py`** collects buildings that fall in gaps between district
   polygons into a synthetic `district_unassigned` group instead of dropping them
   (4 real cases in Gothenburg). Districts, unlike a grid, need not tile the plane.

## Alternatives considered

**`within` (the existing `allow_partial_buildings=False` branch in `district.py`)**
— rejected. It silently **drops** every building straddling a boundary: data loss
instead of inflation, and harder to notice. Now a deprecated warn-only no-op.

**Largest-overlap-area** — correct, and arguably the most principled, but needs a
full overlay plus a tie-break rule. At 500 m cells it returns the same answer as
the representative point for essentially every real footprint, so it costs
complexity without changing results.

**Keep `intersects`, deduplicate afterwards** — rejected. Deduplication needs a
tie-break anyway, so the ownership rule has to exist regardless; better to apply
it once, up front.

## Consequences

- The 2,211-cell run must be regenerated; cell membership and ids both change.
- `partition()` is now a single vectorised binning pass rather than
  ~2,211 × 201,594 spatial-predicate evaluations.
- Existing manifests remain readable — the change is additive.
- Runs with a genuinely inconsistent partition now stop rather than producing
  quietly wrong data.

## Verification

Measured on 201,594 Gothenburg buildings:

| strategy | groups | slots | unique | duplicates | coverage |
|---|---|---|---|---|---|
| grid 500 m | 2,160 | 201,594 | 201,594 | **0** | 100.00% |
| district | 41 | 201,594 | 201,594 | **0** | 100.00% |
| quadtree | 1,035 | 201,594 | 201,594 | **0** | 100.00% |

Regression tests added to `tests/test_mesh_robustness.py` (12/12 pass):
`test_grid_ownership_is_exclusive` (straddling + concave-U + donut),
`test_grid_anchor_stability`, `test_generator_fails_on_ownership_violation`,
`test_quadtree_ownership_is_exclusive`.
