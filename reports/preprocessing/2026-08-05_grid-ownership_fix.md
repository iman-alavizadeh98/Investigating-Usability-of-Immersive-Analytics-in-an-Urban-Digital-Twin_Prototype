# Preprocessing report: grid ownership fix and lattice freeze

**Date:** 2026-08-05
**Affects:** `Src/mesh_generation/` strategies, generator, manifests
**Decision notes:** `docs/decisions/2026-08-05_grid-ownership-representative-point.md`,
`docs/decisions/2026-08-05_frozen-grid-anchor.md`

## Problem

Two defects in the same code path, both silent:

1. **Building duplication.** `grid.py` selected buildings with
   `geometry.intersects(cell_geom)`, assigning a building to every cell it touched.
2. **Non-reproducible cell ids.** The grid started at `buildings_gdf.total_bounds`,
   so the origin moved whenever the input extent changed.

`MeshStrategy.validate()` detected (1) but `generator.py` only logged a warning, so
the run completed. No test instantiated a real strategy, so nothing caught it.

## Before / after — building ownership

Measured on 201,594 Gothenburg buildings
(`Processed_data/Gothenburg/lidar_heights_2026-08-03/buildings_lidar_added.gpkg`).

| metric | before (shipped run) | after |
|---|---|---|
| building-id slots | 211,620 | **201,594** |
| unique buildings | 201,594 | 201,594 |
| **duplicated buildings** | **9,690** | **0** |
| extra copies | 10,026 (**5.0% inflation**) | 0 |
| in 2 / 3 / 4 cells | 9,475 / 94 / 121 | 0 / 0 / 0 |
| coverage | 100% | 100% |
| validation errors | 2 (warned, ignored) | 0 |

All three grouping strategies now satisfy exclusive ownership:

| strategy | groups | slots | unique | duplicates | coverage |
|---|---|---|---|---|---|
| grid 500 m | 2,160 | 201,594 | 201,594 | 0 | 100.00% |
| district | 41 | 201,594 | 201,594 | 0 | 100.00% |
| quadtree | 1,035 | 201,594 | 201,594 | 0 | 100.00% |

The district strategy additionally surfaced **4 buildings** falling in gaps between
district polygons. Previously they were silently dropped; they are now collected
into `district_unassigned` and reported.

## Before / after — lattice alignment

| metric | before | after |
|---|---|---|
| grid origin | (297,697.899, 6,383,590.000) | (298,000, 6,383,500) |
| origin mod 500 | **(197.9, 90.0)** — off-lattice | **(0, 0)** |
| cells off the 500 m lattice | all 2,211 | **0** of 2,160 |
| cell id format | `grid_003_007` (per-run sequence) | `grid_+003_+007` (global lattice address) |
| subset stability | ids shift with input extent | **100%** of 60,478 shared buildings keep their id |

The anchor is a multiple of both 500 and 1000, so all SCB Ruta cell sizes
(100/250/500/1000 m) nest into it exactly — this is what makes the attribute join
integer arithmetic rather than a spatial interpolation.

## Cell-count change

2,211 → **2,160** cells (−51). The old run had *more* cells because a duplicated
building could occupy a cell that contained no building of its own.

## Manifest schema additions

Both additive; older manifests still load.

Run level:
```json
"grid_reference": {
  "anchor_x": 298000.0, "anchor_y": 6383500.0, "cell_size_m": 500,
  "crs": "EPSG:3006", "assignment_rule": "representative_point",
  "id_format": "grid_{col:+04d}_{row:+04d}", "half_open_intervals": true
}
```

Per group:
```json
"cell": {"col": 0, "row": 19, "origin_x": 298000.0, "origin_y": 6393000.0, "size_m": 500},
"assignment_rule": "representative_point"
```

`cell.origin_*` is the **exact lattice corner** (the analytics join key).
The existing `origin` remains the **content bbox corner** (the mesh local frame,
used by Unity for placement). Keeping both distinct is deliberate.

## Performance

`partition()` changed from ~2,211 × 201,594 spatial-predicate evaluations to a
single vectorised binning pass: **3.2 s** for the full city on the grid strategy.
Mesh building dominates total runtime and is unchanged.

## Regression tests

`tests/test_mesh_robustness.py` — 12/12 pass (8 pre-existing + 4 new):

- `test_grid_ownership_is_exclusive` — a building straddling a cell boundary, plus
  a **concave U** and a **donut** whose centroids fall outside the polygon (the
  case motivating `representative_point` over `centroid`).
- `test_grid_anchor_stability` — full set vs 30% subset must agree on every id.
- `test_generator_fails_on_ownership_violation` — raises by default, continues
  with `strict_ownership=False`.
- `test_quadtree_ownership_is_exclusive` — buildings on quadrant midlines.

## Reproducing

```bash
conda activate digitaltwin
python Src/Scripts/run_mesh_generation.py --strategy grid --cell-size 500 \
  --input Processed_data/Gothenburg/lidar_heights_2026-08-03/buildings_lidar_added.gpkg \
  --output Processed_data/Gothenburg/building_meshes_grid_500m_anchored
python tests/test_mesh_robustness.py
```

The previous run is retained at `building_meshes_grid_500m/` until the anchored run
is validated in Unity; rollback is a single path change.
