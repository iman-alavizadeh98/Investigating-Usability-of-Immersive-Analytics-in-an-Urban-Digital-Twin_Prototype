# Helsingborg SCB statistics (population, income) — profiling report

**Date:** 2026-10-07
**Scope:** read-only profiling of the raw delivery. Nothing has been exported yet. This is input for the next step: Helsingborg data layers in the Unity city package.

## Dataset

| Item | Value |
|---|---|
| Original names | *Befolkning* (population) and *Inkomster* (income) — "Statistik på ruta från SCB" |
| Source | SCB, through SLU GET (`metadata/_readMe_befolkning.json`, `_readMe_inkomster.json`) |
| License | FUK (research, education, culture). Not publishable, so never commit exports |
| Folders | `Raw_data/1-Helsingborig/befolkningShp_helsingborg/`, `inkomsterShp_helsingborg/` (shapefiles) |
| CRS | EPSG:3006 |
| Extent | cut to the same rectangle as the building data: E 355,285–361,855, N 6,207,503–6,218,191 |
| Years | population tables `_2024`, income tables `_2023`, DeSO codes `DeSO_2025` |

## Tables

| File | Unit | Rows | Columns (as delivered) | Meaning |
|---|---|---|---|---|
| `Tab1_Ruta_2024` | Ruta 250/1000 m | 492 | `Alder_0_6, Alder_7_15, Alder_16_1, Alder_20_2, Alder_25_4, Alder_45_6, Alder_65, Totalt` | population by age group |
| `Tab2_Ruta_2024` | Ruta | 492 | `Man, Kvinnor, Totalt` | by sex |
| `Tab3_Ruta_2024` | Ruta | 492 | `Ogifta, Gifta, Skilda, Änka_Änk, Totalt` | by marital status. **The .cpg says UTF-8, but the field names are cp1252** |
| `Tab4_Ruta_2024` | Ruta | 492 | `Sverige, Norden_uto, EU_utom_No, Ovriga_var, Totalt` | by country of birth |
| `Tab10_Ruta_2024` | Ruta | 483 | `Forgymn, Gymnasial, Eftergymn2, Eftergymn3, UppgSakn, Totbef` | education level, ages 25–64 |
| `Tab11_Ruta_2023` | Ruta | 494 | `Kvartil1..4, Totalt, Tot_CDISP0` | households (20+) by income quartile; `Tot_CDISP0` = sum of disposable income per consumption unit |
| `Tab6_Ruta_2024_region` | Ruta 100 m | 2,019 | `Ruta, Totalt` | total population only |
| `Tab1_DeSO_2024` | DeSO | 70 | age groups as Tab1 | — |
| `Tab4_DeSO_2024` | DeSO | 70 | country of birth as Tab4 | — |
| `Tab5_DeSO_2024` | DeSO | 70 | `F_Inom, F_Till, F_Fran, Inv, Utv, Fodda, Doda, Tot_Bef` | population change: moves within / in / out, immigration, emigration, births, deaths |
| `Tab11_DeSO_2023` | DeSO | 70 | `Kvartil1..4, Totalt, MedianInk` | income, **with median** |

The Ruta ID column is `RutID_SW`: 13 digits, the easting (6) and northing (7) of the cell's south-west corner. `Rutstorl` is the cell size in metres.

**The variable description PDF** (`metadata/Beskrivning_av_variabler.pdf`, the same file in both folders) is **older than the data**. Its column names (`Ald0_6`, `Kv`, `TotCiv`) and source years (2017–2019) do not match the delivered files. Our own Swedish → English table is needed, and it must be documented as ours.

## Findings that affect the export

1. **Mixed grid: 250 m cells in built-up areas, 1,000 m cells elsewhere.**
   - A 1,000 m cell can **overlap** 250 m cells, but it **holds only the remainder**. Example: 1,000 m cell `3600006211000` has 12 people, while the seven 250 m cells inside it have 1,387.
   - Drawn as full squares, 1,000 m cells would cover the 250 m cells with a wrong value.
   - The two sizes share IDs (same south-west corner), so **the unit ID must include the size**, e.g. `250_3600006211000`.
2. **Edge cells are cut** by the delivery rectangle: 21 of 492. Their values describe the whole cell.
3. **Tables cover different cell sets.** The union is 501 cells; each table has 483–494. A missing cell means **no data**, not 0. The same key has identical geometry in different tables.
4. **Cells exist only where people live.** Of 43,981 buildings, 39,236 are in a 250 m cell, 661 only in a 1,000 m cell, and **4,084 in no cell** (industry, harbour, parks).
5. **The grid has no median income.**
   - Derivable: mean economic standard per household = `Tot_CDISP0 / Totalt`.
   - Quartile shares = `Kvartil_i / Totalt`.
   - The **median exists only per DeSO** (`MedianInk`).
6. **DeSO areas are cut** to the rectangle too. Two of the 70 have empty geometry after cutting. Their values describe the whole DeSO.
7. **Small counts** are protected by SCB. Sub-groups do not always add up to `Totalt` (the same was seen in Gothenburg).

## Proposed handling (not yet confirmed by the user)

- **Rebuild each Ruta cell as an exact square** from `RutID_SW` + `Rutstorl`. This undoes the edge cutting.
- **Draw each 1,000 m cell as its square minus the 250 m cells inside it** (remainder geometry), so nothing overlaps.
- **One spatial layer per unit:** Ruta (250/1000), DeSO, and optionally the 100 m grid. **One data layer per table.**
- **Derived variables** (mean income, shares) computed in the exporter and documented.
- **Building → cell association** by a point inside each footprint (`representative_point`), written to the package. This restores data-driven colouring of buildings in Unity.
- **SCB table definitions** (file pattern, ID column, variables sv → en, units, derivations) go in one **city-independent** config. The city config only points to its files.

## Population pipeline check (2026-10-07, later the same day)

The population tables are now loaded and checked by `Src/pipelines/population/` (see `docs/SCB_PIPELINES.md`). Its first run on this delivery confirms findings 1, 2, 3 and 7 above. It also adds:

- **DeSO geometry:** the 2 cut DeSO areas have **null** geometry, not empty geometry, in Tab1, Tab4 and Tab5_DeSO.
- **Totals differ by unit:** DeSO 128,150, Ruta 250/1,000 m 115,895, Ruta 100 m 115,576. DeSO areas cut by the delivery rectangle keep the values of the whole area, so DeSO totals cannot be compared with the grid.
- **Overlapping cells:** 4 of the 12 1,000 m cells overlap 250 m cells. These are the cells that hold only the remainder (finding 1).
- **Perturbation:** sub-groups ≠ total on 366-416 of 492 grid rows, by at most 12 people.
- **Tab10 coverage:** 9 grid cells that are in the other tables are missing from Tab10 (education, ages 25-64).

## Income pipeline check (2026-10-07)

`Src/pipelines/income/` runs the same checks on the income delivery (see `docs/SCB_PIPELINES.md`). For each finding, the first point is the grid table (`Tab11_Ruta_2023`), the second the DeSO table (`Tab11_DeSO_2023`).

- **Rows:**
  - grid: 494 cells (480 × 250 m + 14 × 1,000 m), 55,064 households; 21 cells cut at the delivery edge; 7 coarse cells overlap 250 m cells;
  - DeSO: 70 areas, 59,791 households; 2 null geometries.
- **Value columns:**
  - grid: `Tot_CDISP0`, the SEK sum of economic standard;
  - DeSO: `MedianInk`, the median, 198,693–411,400 SEK.
- **Empty cells with income:** 30 grid cells have `Totalt` = 0 households but a positive `Tot_CDISP0`. A mean (`Tot_CDISP0 / Totalt`) is undefined there; this is a perturbation artefact.
- **Quartiles vs total:**
  - grid: they differ from `Totalt` on 391 of 494 rows (max 12);
  - DeSO: on 63 of 70 rows (max 9).
- **Quartile boundaries:** not stated in the delivery, so the quartile meanings are marked "inferred".

## Open questions for the user

1. Which units: Ruta + DeSO (recommended), or also the 100 m grid?
2. Are the two geometry fixes acceptable (exact squares; 1,000 m cells as remainders)?
3. Which tables: all of them, or a first subset (e.g. age, sex, income)?
