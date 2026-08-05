# Data profiling: SCB Ruta grid statistics

**Date:** 2026-08-05
**Source:** `Raw_data/0- Gothenburg/{befolkningShp,arbutbShp,inkomsterShp}/*_Ruta_*.shp`
**Environment:** conda env `digitaltwin`
**Companion:** `2026-08-05_scb-ruta_dataset.md` (field mapping, licence, extent)

## Summary

Ten Ruta layers, 4,000–16,000 cells each, EPSG:3006, geometry type Polygon
(axis-aligned squares). All measures are integer counts except `MedianInk`.

The headline finding is that **the `Ruta` code is trustworthy and the geometry is
not** — see §3. Everything else follows from that.

## 1. Cell-size distribution (`Rutstorl`)

Cell size varies for disclosure control. `Tab1_Ruta_2023_region.shp`:

| `Rutstorl` | cells | population | share |
|---|---|---|---|
| 250 | 3,936 | 712,209 | 99.22% |
| 1000 | 203 | 5,572 | 0.78% |
| **total** | **4,139** | **717,781** | |

Coarse cells are sparse areas: median 14 residents, max 281; 169 of 203 hold ≤50
people. `Tab6` is a single-resolution 100 m layer (16,065 cells). `Tab8` names the
same field `AstRutstor`.

## 2. Identifier uniqueness — `Ruta` is NOT a key

```
g.Ruta.is_unique  ->  False       48 duplicated codes
```

Every duplicate pair is one 250 m cell and one 1000 m cell sharing a south-west
corner. The code encodes only the corner, not the size, so the collision is
structural rather than a data error.

**Impact.** `drop_duplicates(subset=["Ruta"])` — as used by the now-deprecated
`preprocess_spatial_joins.py` — discards one member of each pair:

| | |
|---|---|
| true total | 717,781 |
| after dedup on `Ruta` alone | 715,595 |
| **residents silently lost** | **2,186** |

Duplicate-code counts per layer are in the QC table (26–54 depending on coverage).

**Required handling:** primary key `(Rutstorl, Ruta)`.

## 3. Geometry is unreliable for binning; the code is exact

Comparing the `Ruta`-derived corner against the shapefile's `minx`/`miny`:

| | deviation |
|---|---|
| easting | max **4.06 mm** |
| northing | max 4.06 mm for 4,113 cells; **26 cells differ by up to 590 m** |

Those 26 are **boundary-clipped**: their geometry is truncated at the municipal
edge (`y = 6,383,590`), giving heights of 160 m or 410 m instead of 250/1000 m.
The *nominal* corner from the code is correct; the geometry is the clipped
remnant. In total 40 cells have non-nominal extents (also 235 m and 485 m widths).

Nesting 250 m cells into the 500 m analytical lattice, anchor (298000, 6383500):

| method | cells nesting into exactly one target cell |
|---|---|
| via `Ruta` code | **3,936 / 3,936 (100%)** |
| via geometry | **981 / 3,936 (24.9%)** |

Float noise straddles bin edges. **All lattice math must key off the code.**

Lattice purity (code-derived): every fine cell satisfies `easting % 250 == 0` and
`northing % 250 == 0`; every coarse cell is a multiple of 1000. Ten exceptions in
Tab11 — see §6.

## 4. Sub-layer overlap — not a parent/child hierarchy

| | |
|---|---|
| 250 m union area | 24,545 ha |
| 1000 m union area | 19,223 ha |
| **intersection** | **3,517 ha (18.3% of coarse)** |
| coarse cells containing ≥1 fine cell | 98 of 203 |
| fine cells inside a coarse footprint | 566 (37,337 residents) |

Within each sub-layer there are no overlapping pairs — each is internally disjoint.

Treating coarse cells as parents and subtracting nested fine population **fails**:
**83 of the 98 overlapping coarse cells yield a negative remainder** (37,337
residents nested inside cells claiming 2,091 in total). The layers therefore do
not describe the same population at two scales.

Reconciliation rule adopted:
`docs/decisions/2026-08-05_ruta-resolution-reconciliation.md`.

## 5. Field naming hazards (CLAUDE.md §7)

1. **DBF 10-character truncation.** `Alder_16_1` is really *Ålder 16-19*;
   `Alder_20_2` is *20-24*; `Alder_25_4` is *25-44*; `Alder_45_6` is *45-64*. The
   deprecated script wrote these truncated names straight into output JSON keys.
2. **Encoding damage.** `Änka_Ä` (widowed, Tab3) reads as `'�nka_�'`
   because the DBF encoding is undeclared. Same for `Näringsl` (Tab7). Both the
   damaged and correct spellings are registered as alias keys, and column lookup
   falls back to matching by alias.
3. **Spelling variants for identical concepts:** `Rutstorl` vs `AstRutstor`,
   `Näringsl` (Tab7) vs `Naringsliv` (Tab8). Resolved via the alias table, never
   ad-hoc string replacement.
4. **`Totbef` ≠ `Totalt`.** Tab10's total is a population base with a different
   value (395,721 vs 717,781); it is declared as that layer's `total_field`.

## 6. Suspected anomalies

| finding | count | handling |
|---|---|---|
| `Ruta` codes shared across cell sizes | 48 | compound key |
| boundary-clipped cell geometry | 40 | nominal corner used |
| Tab11 cells labelled 1000 m but on a 250 m offset | **10** | flagged in QC; still nest into one 500 m cell, so aggregation is safe |
| coarse cells overlapping fine coverage | 98 | dropped by reconciliation |
| `MedianInk` present as a measure | 1 field | excluded — non-additive |

The Tab11 mismatch is logged as a warning at run time rather than silently
corrected: the size label is demonstrably not trustworthy for those rows, and
CLAUDE.md §7 requires flagging rather than assuming.

## 7. Value ranges (Tab1)

| field | min | median | max |
|---|---|---|---|
| `Totalt` (250 m cells) | 0 | 87 | 2,520 |
| `Totalt` (1000 m cells) | 0 | 14 | 281 |

Zero-population cells exist in both sub-layers and are retained — an occupied cell
with zero residents is meaningful (e.g. industrial), and distinct from an absent
cell.

## 8. Reproducing this profile

```bash
conda activate digitaltwin
python Src/Scripts/run_cell_attributes.py --all
# QC table: Processed_data/analytics/cell_attributes_500m/cell_attributes_500m_qc.csv
```

Per-layer counts, reconciliation outcomes, conservation results and anomaly counts
are written to that QC table on every run.
