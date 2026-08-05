# Decision: statistical attributes attach to the 500 m cell, not to buildings

**Date:** 2026-08-05
**Status:** Adopted
**Affects:** `Src/pipelines/cell_attributes/`, `Src/Scripts/legacy/preprocess_spatial_joins.py`

## Context

`preprocess_spatial_joins.py :: aggregate_building_analytics()` produced a
per-building population figure by adding each overlapping grid cell's **full**
`Totalt` to every building:

```python
for cell_id in overlapping_cells:
    total = cell_row.get("Totalt", 0)
    total_pop += total if pd.notna(total) else 0
```

A cell with 200 residents and 50 buildings therefore gave **all 50 buildings 200
people each** (10,000 residents from 200). A building spanning two cells received
both cells' full totals. Summing the output would grossly overstate the city
population.

The script had never been run successfully — its configured source path
`Raw_data/befolkningShp/` does not exist (the data is under
`Raw_data/0- Gothenburg/befolkningShp/`) and `Processed_data/analytics/` was
empty — so nothing downstream was contaminated. The bug was latent.

The question this forces: **at what spatial unit should statistical attributes
live?**

## Decision

Attributes attach to the **500 m analytical cell**. The pipeline produces no
per-building population, employment, education or income figure.

Buildings still get a cell **membership** record
(`Processed_data/analytics/building_to_cell_500m.parquet`), built with the same
representative-point rule as mesh partitioning. Selecting a building resolves to
its cell, and the UI reports the *cell's* statistics as such.

## Rationale

1. **Correctness.** Population is only *known* at cell resolution. Any
   per-building number is a modelled disaggregation, not data. The old approach is
   one wrong way; area- or volume-weighting would be less obviously wrong but
   equally unverifiable — there is no ground truth to check it against.
   CLAUDE.md §9: *do not hallucinate missing attributes; do not claim a dataset
   supports something unless that support is visible in the data.*
2. **Conservation is checkable.** Cell totals must sum to a known figure
   (717,781 for the SCB 2023 population). Per-building values have no such
   invariant unless the weighting is built to guarantee one — machinery for a
   number that still cannot be validated.
3. **It matches the geometry.** Mesh groups *are* lattice cells (see
   `2026-08-05_frozen-grid-anchor.md`), so the join is a dictionary lookup on a
   shared cell id. No runtime spatial query.
4. **Research validity.** Colouring a 500 m tile by density is defensible in the
   thesis. Colouring individual buildings by a fabricated per-building population
   would have participants reasoning about noise — a validity threat in a
   usability study, not merely an accuracy quibble.

## Alternatives considered

**Fix the old script's arithmetic** — rejected. Its purpose *is* per-building
attribution; correcting the double-count would leave an invented attribute with
no way to validate it.

**Floor-area-weighted disaggregation** (`footprint_area × height`, normalised
within the cell) — conserves the cell total and is spatially plausible. Deferred,
not adopted: it is still modelled. If the interaction design later needs it, it
should ship as an explicitly-labelled derived field
(`population_estimate_floorarea_weighted`) carrying its derivation in metadata,
plus a check that per-building values re-sum to the cell total.

## Consequences

- `preprocess_spatial_joins.py` moved to `Src/Scripts/legacy/` with its four
  defects documented. Kept rather than deleted: it records a real methodological
  error caught before it propagated, which is useful lineage for the thesis.
- The runtime "click a building" panel reads *"500 m cell — 412 residents"*, not a
  per-building figure. This is a deliberate interface constraint, not a limitation
  to work around.
- Attribute outputs are keyed by the same `cell_id` as the mesh manifest.
