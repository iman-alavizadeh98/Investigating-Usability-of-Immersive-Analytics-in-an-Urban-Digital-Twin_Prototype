# Decision: reconcile mixed SCB Ruta resolutions by fine-priority with disjoint coarse

**Date:** 2026-08-05
**Status:** Adopted
**Affects:** `Src/pipelines/cell_attributes/aggregate.py :: reconcile_resolutions`

## Context

SCB delivers `Ruta` grid statistics at **mixed cell sizes** for disclosure control:
dense areas at 250 m (or 100 m), sparse areas at 1000 m so small counts cannot
identify individuals. Measured on `Tab1_Ruta_2023_region.shp` (4,139 cells):

| `Rutstorl` | cells | population |
|---|---|---|
| 250 | 3,936 | 712,209 |
| 1000 | 203 | 5,572 |
| **total** | **4,139** | **717,781** |

The two sub-layers **overlap**: 3,517 ha, or 18.3% of the coarse union. 98 coarse
cells geometrically contain at least one fine cell; 566 fine cells (37,337
residents) fall inside a coarse footprint. Naively stacking and summing therefore
double-counts.

The obvious fix — treat coarse cells as parents and subtract the nested fine
population — **fails against the data**:

> **83 of the 98 overlapping coarse cells produce a negative remainder.**
> The fine cells inside them hold 37,337 residents, against those coarse cells'
> claimed 2,091.

A discrepancy that large is not rounding. The layers are **not** a coarse/fine
decomposition of the same population, so subtraction is unjustified.

Two further hazards, both measured:

- **`Ruta` is not a unique key.** 48 codes appear twice — always once as a 250 m
  cell and once as 1000 m, because the code encodes only the south-west corner,
  not the size. The old script's `drop_duplicates(subset=["Ruta"])` therefore
  discarded **2,186 residents** (717,781 → 715,595).
- **Geometry cannot be trusted for binning.** Vertices carry ~4 mm of float noise
  and 40 cells are clipped at the municipal boundary. Nesting 250 m cells into the
  500 m lattice succeeds **3,936/3,936 via the `Ruta` code** but only
  **981/3,936 via the geometry**.

## Decision

**Rule `fine_priority_disjoint_coarse`:** take every cell from the finest
resolution present; add a coarser cell **only where no finer cell covers it**.

Containment is tested by integer arithmetic on `Ruta`-derived corners, never by a
spatial predicate. The primary key is `(cell size, cell code)`.

Source cells larger than the target (1000 m into 500 m) are split across the cells
they cover using **integer largest-remainder apportionment**, so the parts sum
*exactly* to the original and conservation stays exact rather than approximate.
The split rule is recorded in the output metadata so it can be replaced later
(e.g. with floor-area weighting) without re-deriving the pipeline.

### Result (population, Tab1)

| | |
|---|---|
| fine cells kept | 3,936 → 712,209 |
| coarse cells kept (disjoint) | 105 → 3,481 |
| coarse cells dropped (already covered at 250 m) | 98 → 2,091 |
| **target total** | **715,690 (99.71% of 717,781)** |
| occupied 500 m cells | **1,766** |

## Alternatives considered

**Fine-only (discard the 1000 m layer)** — simplest, but loses all 5,572 rural
residents and populates only 1,346 cells. Rural areas would render as *zero*
population rather than sparse, which reads as missing data in the visualisation.

**Coarse-parent subtraction** — rejected on evidence: 83 negative remainders (see
above).

**Area-weighted interpolation** — reintroduces float tolerance and the
clipped-cell problem for 0.78% of the population, when `Ruta`-code nesting is
already exact. Complexity without accuracy.

**Keep both layers and let the visualisation decide** — no single conservation
number exists in the overlap zone, so the pipeline could not verify itself.

## Consequences

- 2,091 residents (0.29%) are deliberately not represented, in areas already
  described at 250 m. Recorded in the QC table (`dropped_total`) per layer.
- The rule applies **per layer**: each has its own coverage, so each has its own
  reconciled total.
- Coarse-cell splitting places residents in quadrants that may be empty. These are
  sparse rural cells (median 14 residents), so the spatial error is small; the
  rule is one line to explain and swap.
- Conservation is asserted, not assumed: `check_conservation()` raises.

## Verification

All 10 registered layers aggregate with conservation holding:

| layer | source total | target total | cells |
|---|---|---|---|
| population_age / sex / marital / origin | 717,781 | 715,690 | 1,766 |
| population_total_100m | 717,284 | 717,284 | 1,635 |
| employment_sector_night | 375,675 | 374,797 | 1,710 |
| employment_sector_day | 425,783 | 421,099 | 1,637 |
| employment_status | 437,103 | 436,075 | 1,722 |
| education_level | 395,721 | 394,539 | 1,713 |
| income_quartiles | 326,496 | 325,941 | 1,774 |

The 100 m layer needs no reconciliation (single resolution) and conserves exactly.
