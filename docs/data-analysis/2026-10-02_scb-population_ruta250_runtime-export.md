# SCB population (age, sex) → Unity runtime data layers, ruta_250

**Date:** 2026-10-02 · **Status:** test data (the data pipeline will be rebuilt)
**Why:** the glyph renderers (stacked bar, radial) and bidirectional height
need multi-variable data on the runtime spatial layer `ruta_250`; until now the
only runtime data layer was `income_2023` (one variable).

Source, licence and field definitions: see
[2026-08-05_scb-ruta_dataset.md](2026-08-05_scb-ruta_dataset.md) (same SCB Ruta
2023 tables, same English aliases).

## Exported layers

| Runtime layer | Source table (Swedish → English) | Filter | Variables (source field → id) |
|---|---|---|---|
| `population_age_2023` | `befolkningShp/Tab1_Ruta_2023_region.shp`, *Befolkning efter ålder* → Population by age | `Rutstorl = 250` | `Alder_0_6`→`age_0_6`, `Alder_7_15`→`age_7_15`, `Alder_16_1`→`age_16_19`, `Alder_20_2`→`age_20_24`, `Alder_25_4`→`age_25_44`, `Alder_45_6`→`age_45_64`, `Alder_65`→`age_65_plus`, `Totalt`→`total` (all Integer, persons) |
| `population_sex_2023` | `befolkningShp/Tab2_Ruta_2023_region.shp`, *Befolkning efter kön* → Population by sex | `Rutstorl = 250` | `Man`→`men`, `Kvinnor`→`women`, `Totalt`→`total` (Integer, persons) |

`Alder_16_1`, `Alder_20_2`, `Alder_25_4`, `Alder_45_6` are DBF-truncated names;
the age bands are 16–19, 20–24, 25–44, 45–64.

Files: `Unity/City_Digital_Twin/Assets/StreamingAssets/data_layers/<id>/{layer,values}.json`,
registered in `project_manifest.json`. CRS EPSG:3006 (checked by the exporter,
not reprojected).

## How to reproduce

```bash
python Src/Scripts/Unity/export_data_layer.py --input "Raw_data/0- Gothenburg/befolkningShp/Tab1_Ruta_2023_region.shp" --output Unity/City_Digital_Twin/Assets/StreamingAssets/data_layers/population_age_2023 --layer-id population_age_2023 --display-name "Population by age 2023" --target-spatial-layer ruta_250 --id-field Ruta --expected-crs EPSG:3006 --filter-field Rutstorl --filter-value 250 --restrict-to-spatial-layer Unity/City_Digital_Twin/Assets/StreamingAssets/spatial_layers/ruta_250/geometry.json --variable "age_0_6|Alder_0_6|integer|persons|Age 0–6" ... --variable "total|Totalt|integer|persons|Total population"
```

(One `--variable` per field as in the table; Tab2 the same with `men`, `women`,
`total`.) Run with the `digitaltwin` conda env.

`--restrict-to-spatial-layer` is new (2026-10-02): the Unity runtime rejects a
data layer containing unit IDs without geometry, so rows whose
`ruta_250:<Ruta>` ID is not in the spatial layer are dropped and **listed** in
the export log.

## Results and checks

| Check | population_age_2023 | population_sex_2023 |
|---|---|---|
| Rows (all sizes / 250 m) | 4,139 / 3,936 | 4,139 / 3,936 |
| Dropped: no geometry in `ruta_250` | 54 | 54 (same cells) |
| Exported units | 3,882 | 3,882 |
| `ruta_250` cells without population data | 37 (3,919 − 3,882) | 37 |
| Valid values | 3,882 / 3,882 for every variable | same |
| Sub-groups add up to `total` | 553 of 3,882 cells; others differ by −14…+16 | 711 of 3,882; others differ by −8…+8 |
| Zero-population cells | 98 | — |
| Persons (sum of `total`) | 711,681 | — |

**Why sub-groups do not add up:** SCB perturbs small counts for disclosure
control (noted in the 2026-08-05 profiling). Consequence for the runtime:
stacked bars use the **sum of the segments** as their height, not `total`.

**Why 54 cells drop:** `ruta_250` was built from the income table (Tab11), which
covers 3,919 250 m cells; the population tables cover 3,936. The 54 dropped
cells have population but no income-table geometry; 37 income cells have no
population row. A rebuilt pipeline should build the spatial layer from the union
of the tables used.

## Known gaps

- Test data; values are SCB 2023 as delivered, no cleaning beyond the size filter.
- Unit ID order differs between the two layers (harmless: lookups are by ID).
