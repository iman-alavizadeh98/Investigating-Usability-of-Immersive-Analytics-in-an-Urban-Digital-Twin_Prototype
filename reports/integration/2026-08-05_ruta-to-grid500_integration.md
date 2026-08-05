# Integration report: SCB Ruta statistics → 500 m analytical grid

**Date:** 2026-08-05
**Pipeline:** `Src/pipelines/cell_attributes/` (`python Src/Scripts/run_cell_attributes.py --all`)
**Output:** `Processed_data/analytics/cell_attributes_500m/`

## What was joined

Ten SCB Ruta statistical layers (population ×5, employment ×3, education, income)
onto the frozen 500 m analytical lattice shared with the building meshes.

## Join method — the methodologically interesting part

**The join key is an integer lattice index derived from the `Ruta` code, not a
spatial predicate.** No `sjoin`, no intersection test, no distance tolerance.

```
easting  = int(Ruta[:6])            # exact, from the code
northing = int(Ruta[6:])
col = floor((easting  - 298000) / 500)
row = floor((northing - 6383500) / 500)
cell_id = f"grid_{col:+04d}_{row:+04d}"
```

This is possible because of two properties established beforehand:

1. **The anchor (298000, 6383500) is a multiple of both 500 and 1000**, so every
   SCB cell size (100/250/500/1000 m) nests into the target lattice with no
   partial cells.
2. **The `Ruta` code is exact where the geometry is not.** Nesting 250 m cells
   into 500 m: **3,936/3,936 via the code, 981/3,936 via geometry** (~4 mm vertex
   noise; 40 cells clipped at the municipal boundary).

Consequence: the integration has **no spatial tolerance parameter** and therefore
no tolerance-sensitivity to report. Cells either nest or they do not, and all of
them do.

## Matching rate

| layer | source cells | kept after reconciliation | dropped | target cells |
|---|---|---|---|---|
| population_age | 4,139 | 4,041 | 98 | 1,766 |
| population_sex | 4,139 | 4,041 | 98 | 1,766 |
| population_marital | 4,139 | 4,041 | 98 | 1,766 |
| population_origin | 4,139 | 4,041 | 98 | 1,766 |
| population_total_100m | 16,065 | 16,065 | 0 | 1,635 |
| employment_sector_night | 4,004 | 3,922 | 82 | 1,710 |
| employment_sector_day | 3,881 | 3,818 | 63 | 1,637 |
| employment_status | 4,060 | 3,973 | 87 | 1,722 |
| education_level | 4,040 | 3,951 | 89 | 1,713 |
| income_quartiles | 4,122 | 4,032 | 90 | 1,774 |

**Unmatched source cells: zero.** Every cell that survives reconciliation lands in
a target cell. "Dropped" rows are removed *deliberately* by the reconciliation rule
(coarse cells over ground already described at finer resolution), not failures to
match.

Merged output: **1,970 occupied 500 m cells** across all ten layers.

## Temporal alignment

All layers are SCB reference year **2023**, a single annual snapshot. No temporal
interpolation or alignment was required. The buildings layer has no comparable
timestamp, so building↔attribute joins are spatial only and carry no temporal
claim.

## Conservation

Every layer asserts that aggregation preserves its reconciled source total;
`check_conservation()` raises on mismatch.

| layer | reconciled source | aggregated target | conserved |
|---|---|---|---|
| population_age | 715,690 | 715,690 | ✓ |
| population_sex | 715,690 | 715,690 | ✓ |
| population_marital | 715,690 | 715,690 | ✓ |
| population_origin | 715,690 | 715,690 | ✓ |
| population_total_100m | 717,284 | 717,284 | ✓ |
| employment_sector_night | 374,797 | 374,797 | ✓ |
| employment_sector_day | 421,099 | 421,099 | ✓ |
| employment_status | 436,075 | 436,075 | ✓ |
| education_level | 394,539 | 394,539 | ✓ |
| income_quartiles | 325,941 | 325,941 | ✓ |

Source totals were additionally checked against recorded expectations (e.g.
population 717,781) so a changed delivery fails the run rather than silently
shifting the numbers.

## Quality risks introduced by the merge

| risk | magnitude | mitigation |
|---|---|---|
| Residents dropped where coarse cells overlap finer coverage | 2,091 of 717,781 (**0.29%**) | Recorded per layer as `dropped_total` in the QC table; ground is still described at 250 m |
| Coarse (1000 m) cells split across four target cells | 105 cells, 3,481 residents | Integer largest-remainder apportionment — parts sum *exactly*; rule recorded in metadata and swappable |
| Phantom placement within a split coarse cell | sparse rural, median 14 residents | Accepted; documented in the reconciliation decision note |
| Suppressed/rounded subtotals not summing to `Totalt` | small, SCB disclosure control | Not corrected; noted in the profiling report |
| `MedianInk` cannot be aggregated | 1 field | Excluded as non-additive rather than approximated |
| Tab11 cells with an unreliable size label | 10 | Flagged in QC; they still nest into one target cell so aggregation is unaffected |

## Cross-layer alignment with the mesh

Attribute cell ids use the same `grid_{col:+04d}_{row:+04d}` format, the same
anchor and the same cell size as the mesh manifest's `grid_reference` block, so
the runtime join is a dictionary lookup. `building_to_cell_500m.parquet` maps all
201,594 buildings to cells using the **same representative-point rule** as mesh
partitioning, guaranteeing a building's mesh group and its attribute cell are
identical.

Verification of that alignment is reported in
`reports/preprocessing/2026-08-05_grid-ownership_fix.md`.

## Reproducing

```bash
conda activate digitaltwin
python Src/Scripts/run_cell_attributes.py --all
python Src/Scripts/build_building_cell_index.py
```
