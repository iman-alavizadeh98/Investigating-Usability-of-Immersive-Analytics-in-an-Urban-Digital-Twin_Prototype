# Cleaning report: SCB Ruta grid statistics

**Date:** 2026-08-05
**Pipeline:** `Src/pipelines/cell_attributes/`
**Scope:** ten Ruta layers (population, employment, education, income), 2023

Raw data is never modified. Everything below happens in-pipeline, and every
original value remains recoverable from `Raw_data/`.

## What was removed

| removed | count | reason |
|---|---|---|
| 1000 m cells overlapping finer coverage | 98 (population); 63–90 per other layer | The 250 m layer already describes that ground. Keeping both double-counts. See the reconciliation decision note. |
| `MedianInk` from aggregation | 1 field | A median cannot be summed across cells. Excluded rather than approximated into a misleading number. |

Population effect: 2,091 residents of 717,781 (**0.29%**) are not represented in
the target grid, all in areas covered at 250 m. Recorded per layer as
`dropped_total` in the QC table.

Nothing was removed as "invalid": no cell was discarded for bad geometry, out-of-range
values, or missing fields.

## What was repaired

| issue | repair |
|---|---|
| Cell corners unreliable from geometry (~4 mm float noise; 40 boundary-clipped cells) | Corners derived from the exact `Ruta` code instead. Geometry is never used for binning. |
| Column names damaged by undeclared DBF encoding (`Änka_Ä` → `'?nka_?'`, `Näringsl`) | Both damaged and correct spellings registered as alias keys; lookup falls back to matching by alias and **logs** the fallback. |
| Same concept spelled differently across tables (`Rutstorl`/`AstRutstor`, `Näringsl`/`Naringsliv`) | Resolved through the alias table, not ad-hoc string replacement. |

## What was imputed

**Nothing.** No missing value was filled, no population estimated, no attribute
interpolated.

The one derived operation is splitting a 1000 m source cell across the four 500 m
target cells it covers, using integer largest-remainder apportionment so the parts
sum *exactly* to the original. This is documented as a modelling choice
(`coarse_split_rule`) in the output metadata, not presented as measurement.

## What was standardised

| from | to |
|---|---|
| Mixed cell sizes (100 / 250 / 1000 m) | Uniform 500 m analytical cells |
| `Ruta` code as identifier | `cell_id` = `grid_{col:+04d}_{row:+04d}`, shared with the mesh manifest |
| Per-cell primary key | `(cell_size_m, cell_code)` — `Ruta` alone is **not** unique |
| Swedish field names | English aliases (originals preserved in raw data and recorded in metadata) |

### Swedish → English aliases applied

Full table in `docs/data-analysis/2026-08-05_scb-ruta_dataset.md`. Cases where the
alias also *corrects* the source name:

| original | alias | note |
|---|---|---|
| `Alder_16_1` | `age_16_19` | DBF truncated the real range |
| `Alder_20_2` | `age_20_24` | DBF truncated |
| `Alder_25_4` | `age_25_44` | DBF truncated |
| `Alder_45_6` | `age_45_64` | DBF truncated |
| `Änka_Ä` | `widowed` | encoding damage |
| `AstRutstor` | `cell_size_m` | Tab8 variant |
| `Naringsliv` / `Näringsl` | `sector_private` | two spellings |
| `Totbef` | `total_population_base` | distinct from `Totalt` |

## What remains unresolved

| issue | status |
|---|---|
| 10 Tab11 cells labelled `Rutstorl=1000` but sitting on a 250 m offset | **Flagged, not corrected.** They nest into one 500 m cell so aggregation is unaffected, but the size label is demonstrably unreliable. Logged as a warning each run and counted in the QC table. |
| Age/education subtotals not always summing to `Totalt` | **Not corrected.** SCB suppresses and rounds small counts for disclosure control; "fixing" it would fabricate data. |
| 40 boundary-clipped cell geometries | **Not corrected.** Irrelevant to aggregation, which uses nominal corners. Would matter only under area-weighting, which is deliberately avoided. |
| Coarse-cell splitting may place residents in empty quadrants | **Accepted and documented.** Sparse rural cells (median 14 residents); rule is swappable via metadata. |
| `MedianInk` unavailable at cell level | **Open.** A population-weighted approximation is possible but would need to be clearly labelled as derived. |

## Verification

- Source totals asserted against recorded expectations per layer (population
  717,781); a changed delivery fails the run.
- Conservation asserted after aggregation for all ten layers — all pass.
- Duplicate `(size, code)` keys would raise; duplicate `code`-only values are
  counted and reported (48 for population).

Machine-readable results: `Processed_data/analytics/cell_attributes_500m/cell_attributes_500m_qc.csv`.
