# Dataset: SCB Ruta grid statistics (Statistik på rutor)

**Original name (sv):** Statistik på rutor
**English name:** Grid-square statistics
**Source authority:** SCB — Statistiska centralbyrån / Statistics Sweden
**Reference year:** 2023
**Accessed / profiled:** 2026-08-05
**Format:** ESRI Shapefile (`.shp` + `.dbf` + `.prj` + `.shx`)
**CRS:** EPSG:3006 (SWEREF99 TM) — same as the buildings layer, no reprojection needed
**Location:** `Raw_data/0- Gothenburg/{befolkningShp,arbutbShp,inkomsterShp}/`
**Licence / usage:** SCB open statistics. Cells are aggregated and size-varied for
disclosure control; individual-level inference is not possible and must not be attempted.

## Spatial extent

| | easting | northing |
|---|---|---|
| min | 298,000 | 6,383,590 |
| max | 328,485 | 6,412,999 |

≈ 30.5 × 29.4 km, matching the Gothenburg buildings extent.

## Layers

Ten `*_Ruta_*` layers are registered in
`Src/pipelines/cell_attributes/config.py :: LAYER_REGISTRY`. `*_DeSO_*` variants
exist for the same themes at DeSO (administrative) geography — **not used**, since
the analytical grid is the join unit.

| layer_id | file | theme | cells | total | Swedish description |
|---|---|---|---|---|---|
| `population_age` | Tab1 | population | 4,139 | 717,781 | Befolkning efter ålder |
| `population_sex` | Tab2 | population | 4,139 | 717,781 | Befolkning efter kön |
| `population_marital` | Tab3 | population | 4,139 | 717,781 | Befolkning efter civilstånd |
| `population_origin` | Tab4 | population | 4,139 | 717,781 | Befolkning efter födelseregion |
| `population_total_100m` | Tab6 | population | 16,065 | 717,284 | Befolkning totalt (100 m rutor) |
| `employment_sector_night` | Tab7 | employment | 4,004 | 375,675 | Förvärvsarbetande efter sektor (nattbefolkning) |
| `employment_sector_day` | Tab8 | employment | 3,881 | 425,783 | Sysselsatta efter sektor (dagbefolkning) |
| `employment_status` | Tab9 | employment | 4,060 | 437,103 | Befolkning efter sysselsättning |
| `education_level` | Tab10 | education | 4,040 | 395,721 | Befolkning efter utbildningsnivå |
| `income_quartiles` | Tab11 | income | 4,122 | 326,496 | Inkomst efter kvartil |

`Tab5_DeSO` (migration/vital statistics) has no Ruta variant and is not registered.

## Cell geometry

Cells are squares on a lattice, at **mixed sizes** for disclosure control:

| size | layers | note |
|---|---|---|
| 100 m | Tab6 only | 16,065 cells |
| 250 m | most layers | the dense-area default |
| 1000 m | most layers | sparse/rural areas |

**Cell identity comes from the `Ruta` code, not the geometry.** The 13-character
code is a 6-digit easting followed by a 7-digit northing naming the south-west
corner (`3170006394000` → 317000, 6394000). See the profiling report for why the
geometry must not be used for binning.

## Field names — original → English alias

Full mapping in `Src/pipelines/cell_attributes/config.py :: RUTA_FIELD_TRANSLATIONS`.
Raw data keeps the original names; processed outputs carry the aliases.

### Grid identity
| Swedish | English | note |
|---|---|---|
| `Rutstorl` | `cell_size_m` | |
| `AstRutstor` | `cell_size_m` | Tab8 spelling variant |
| `Ruta` | `cell_code` | 13-char SW-corner code |
| `Totalt` | `total` | |
| `Totbef` | `total_population_base` | Tab10; *not* the same field as `Totalt` |

### Age (Tab1) — names truncated to 10 chars by the DBF format
| Swedish | English | real range |
|---|---|---|
| `Alder_0_6` | `age_0_6` | 0–6 |
| `Alder_7_15` | `age_7_15` | 7–15 |
| `Alder_16_1` | `age_16_19` | **16–19** (truncated) |
| `Alder_20_2` | `age_20_24` | **20–24** (truncated) |
| `Alder_25_4` | `age_25_44` | **25–44** (truncated) |
| `Alder_45_6` | `age_45_64` | **45–64** (truncated) |
| `Alder_65` | `age_65_plus` | 65+ |

### Sex, marital status, origin (Tab2–Tab4)
| Swedish | English |
|---|---|
| `Man` / `Kvinnor` | `men` / `women` |
| `Ogifta` / `Gifta` / `Skilda` | `unmarried` / `married` / `divorced` |
| `Änka_Ä` | `widowed` — reads as mojibake; see profiling report |
| `Sverige` | `born_sweden` |
| `Norden_uto` | `born_nordic_excl_sweden` |
| `EU_utom_No` | `born_eu_excl_nordic` |
| `Ovriga_var` | `born_other` |

### Employment (Tab7–Tab9)
| Swedish | English |
|---|---|
| `Näringsl` / `Naringsliv` | `sector_private` — two spellings across tables |
| `Staten` / `Kommun` / `Region` / `Ovrigt` | `sector_state` / `sector_municipal` / `sector_region` / `sector_other` |
| `Sysselsatt` / `EjSsyssels` | `employed` / `not_employed` |

### Education (Tab10) and income (Tab11)
| Swedish | English | note |
|---|---|---|
| `Forgymn` | `edu_pre_upper_secondary` | |
| `Gymnasial` | `edu_upper_secondary` | |
| `Eftergymn2` / `Eftergymn3` | `edu_tertiary_lt_3y` / `edu_tertiary_ge_3y` | <3y / ≥3y |
| `UppgSakn` | `edu_unknown` | uppgift saknas |
| `Kvartil1`–`Kvartil4` | `income_q1`–`income_q4` | counts of people |
| `MedianInk` | `median_income` | **NON-ADDITIVE** — never summed |

## Known gaps and caveats

- **`Ruta` is not unique**: 48 codes appear at two cell sizes. The primary key must
  be `(cell_size_m, cell_code)`.
- **Mixed resolutions overlap** (18.3% of coarse area) and are *not* parent/child.
  See `docs/decisions/2026-08-05_ruta-resolution-reconciliation.md`.
- **40 cells are clipped** at the municipal boundary; their geometry is smaller
  than nominal. Irrelevant here because binning uses the code.
- **10 cells in Tab11** declare `Rutstorl=1000` but sit on a 250 m offset — the
  size label is demonstrably unreliable for those rows.
- **Suppression**: age/education subtotals may not sum exactly to `Totalt` because
  small counts are suppressed or rounded.
- **`MedianInk`** cannot be aggregated; it is excluded rather than approximated.

## Preprocessing applied

Pipeline: `Src/pipelines/cell_attributes/` (`run_cell_attributes.py`).

1. Parse `Ruta` codes into exact SW corners (no geometry used).
2. Key on `(cell_size_m, cell_code)`; alias fields to English.
3. Reconcile mixed resolutions (`fine_priority_disjoint_coarse`).
4. Aggregate onto the frozen 500 m lattice, anchor (298000, 6383500).
5. Assert conservation against the reconciled source total.

## Target entity after integration

The **500 m analytical cell**, identified as `grid_{col:+04d}_{row:+04d}` — the
same id the mesh manifest uses, so analytics join to geometry by dictionary
lookup. Attributes are **not** disaggregated to individual buildings; see
`docs/decisions/2026-08-05_attributes-attach-to-cell.md`.

## Outputs

`Processed_data/analytics/cell_attributes_500m/`
— `.gpkg`, `.parquet`, `.json` (runtime), `_metadata.json`, `_qc.csv`.
