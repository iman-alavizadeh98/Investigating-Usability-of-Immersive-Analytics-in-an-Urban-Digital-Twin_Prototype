# SCB pipelines (population, income, employment) — preprocess stage

**Status (2026-10-07):** load, check, profile and view only. No processing rules yet: the tables appear in the views exactly as delivered. The processing (which tables, how to handle the mixed grid, aliases, derived values, target unit, export) is decided later and goes in each dataset's `preprocessor.py`.

Population, income and employment (added 2026-10-13) are all *Statistik på ruta och DeSO från SCB* deliveries, in the same shapefile format. They share one core (`Src/pipelines/scb/`). Each dataset only adds its table catalog, its field dictionary and its preprocessor. The election data is a different format; see [ELECTION_PIPELINE.md](ELECTION_PIPELINE.md).

## Datasets

| Pipeline | Original name (sv) | English | Folder (Helsingborg) |
|---|---|---|---|
| `population` | *Befolkning* | Population | `Raw_data/1-Helsingborig/befolkningShp_helsingborg/` |
| `income` | *Inkomster* | Income | `Raw_data/1-Helsingborig/inkomsterShp_helsingborg/` |
| `employment` | *Arbetsmarknad – Utbildning* | Labour market – Education | `Raw_data/1-Helsingborig/employment/` |

- **Source:** SCB, delivered through SLU GET.
- **Licence:** FUK (research, education, culture). **Not publishable.** The HTML views contain the data, so write them under `Processed_data/` (ignored by git), never `reports/` or `docs/`.
- **Files:** `Tab<number>_<unit>_<year>[_<suffix>].shp`; units are `Ruta` (grid squares) and `DeSO` (demographic statistical areas).

### Tables in the catalogs

| Dataset | Table | Swedish | English | Total column | Value columns |
|---|---|---|---|---|---|
| population | 1 | Befolkning efter ålder | Population by age group | `Totalt` | |
| population | 2 | Befolkning efter kön | Population by sex | `Totalt` | |
| population | 3 | Befolkning efter civilstånd | Population by marital status | `Totalt` | |
| population | 4 | Befolkning efter födelseland | Population by country of birth | `Totalt` | |
| population | 5 | Befolkningsförändringar | Population change (DeSO only) | `Tot_Bef` (stock; the parts are flows) | |
| population | 6 | Totalbefolkning på 100 m ruta | Total population, 100 m cells | `Totalt` | |
| population | 10 | Befolkning 25–64 år efter utbildningsnivå | Population 25–64 by education | `Totbef` | |
| income | 11 | Hushåll 20+ år efter ekonomisk standard | Households (20+) by economic standard, in quartiles | `Totalt` (households) | `Tot_CDISP0` (SEK sum, grid), `MedianInk` (SEK median, DeSO) |
| employment | 7 | Förvärvsarbetande nattbefolkning (16+ år) efter sektor | Employed persons (16+) by sector, at **place of residence** | `Totalt` | |
| employment | 8 | Förvärvsarbetande dagbefolkning (16+ år) efter sektor | Employed persons (16+) by sector, at **place of work** | `Totalt` | |
| employment | 9 | Befolkning 20–64 år efter sysselsättning | Population 20–64 by employment status | `Totalt` | |
| employment | 10 | (same as population Tab10) | Population 25–64 by education | `Totbef` | |

A table number missing from the catalog still loads; it is reported as an issue.

## How to run

From the repo root:

```bash
python Src/Scripts/run_population_pipeline.py --input "Raw_data/1-Helsingborig/befolkningShp_helsingborg" --output "Processed_data/Helsingborg_Final"
```

```bash
python Src/Scripts/run_income_pipeline.py --input "Raw_data/1-Helsingborig/inkomsterShp_helsingborg" --output "Processed_data/Helsingborg_Final"
```

```bash
python Src/Scripts/run_employment_pipeline.py --input "Raw_data/1-Helsingborig/employment" --output "Processed_data/Helsingborg_Final"
```

`--tables Tab1_Ruta Tab2` loads only the files whose name starts with one of these. Open the printed `index.html`.

Tests (synthetic data only): `python tests/test_scb_pipelines.py`

## Outputs

`<output>/<dataset>_<YYYY-MM-DD>/`:

```
index.html                              start here
tables/<table>.html                     the data, as loaded (sortable, searchable)
profiles/<table>_profile.html/.md       DataFrameProfiler report
<dataset>_preprocess_report.json        inputs, encodings, every check, preprocessing steps
```

`index.html` contains:
- the run details;
- a table overview;
- issues and notes per table;
- value-column ranges;
- coverage across tables;
- a field dictionary: Swedish name → English alias, meaning, unit, role, and where the meaning comes from.

## Code

| Path | Job |
|---|---|
| `Src/pipelines/scb/config.py` | file-name pattern, ID/size column rules, encoding fallback, `TableSpec`, `FieldInfo` (with `role` and `unit`), `ScbDataset`, identity fields |
| `Src/pipelines/scb/loader.py` | finds and reads the shapefiles unchanged; encoding fallback; table/unit/year from the file name |
| `Src/pipelines/scb/validator.py` | checks per table and coverage across tables (reports, never stops) |
| `Src/pipelines/scb/exporter.py` | HTML views, profiles, index page, JSON report |
| `Src/pipelines/scb/pipeline.py` | `ScbTablesPipeline(BasePipeline)`: load → validate → preprocess → export |
| `Src/pipelines/scb/cli.py` | shared `--input/--output/--tables` command line |
| `Src/pipelines/population/` | `config.py` (`POPULATION`: catalog + fields), `preprocessor.py` (empty), `pipeline.py` |
| `Src/pipelines/income/` | `config.py` (`INCOME`), `preprocessor.py` (empty), `pipeline.py` |
| `Src/pipelines/employment/` | `config.py` (`EMPLOYMENT`; Tab10 is reused from `POPULATION`), `preprocessor.py` (empty), `pipeline.py` |

**To add another SCB delivery:** create a package with a `config.py` defining an `ScbDataset`, plus a `pipeline.py` that subclasses `ScbTablesPipeline`, and a run script that calls `run_cli`.

Shared utilities: `Src/utils/dataframe_html.py` (HTML viewer for any DataFrame) and `Src/utils/data_profiler.py`.

## What the checks report

**Issues** (need a decision):
- CRS other than EPSG:3006;
- null, empty or invalid geometry;
- malformed or repeated IDs;
- Ruta cells off their own grid;
- columns missing from the field dictionary;
- negative values;
- a missing total column;
- an encoding fallback;
- a file name outside the SCB pattern.

**Notes** (known properties of SCB data):
- cells that are not a full square (cut at the delivery edge);
- coarse cells overlapping finer cells of the same table;
- rows where the sub-groups do not add up to the total;
- rows with a total of 0 but a non-zero value column (a mean is undefined there).

**Field roles** (`FieldInfo.role`) decide what is checked:
- only `count` fields are compared with the total;
- `amount` (a sum, e.g. SEK) and `median` fields are values, not parts of the total;
- a median must never be summed or averaged across units.

Ruta positions come from the ID (`RutID_SW` or `Ruta`: 6-digit easting + 7-digit northing of the south-west corner), not from the geometry. Ruta units are keyed `<size>_<id>`, because a 250 m cell and a 1,000 m cell can share a corner and so share an ID.

## Assumptions and known traps

- **The variable PDF in the deliveries is older than the data.** Each field records whether its meaning is stated there (`pdf`) or read from the abbreviation (`inferred`).
  - The population Tab5 fields are all inferred.
  - So are the income quartiles and `MedianInk`. Where the quartile boundaries come from (national or local) is not stated.
- **Encoding.** Population Tab3's `.cpg` says UTF-8, but its field names are cp1252. The same applies to employment Tab7 and Tab8 Ruta. The loader retries with the encodings in `ENCODING_FALLBACKS`.
- **Workplace ID columns** (employment Tab8) have an `Ast`/`ast` prefix: `AstRutID_S`, `AstRutstor`, and `astDeSO_20`. The last is cut at 10 characters, so the DeSO edition year is lost. `UNIT_RULES` matches them.
- **Sector spellings differ across tables:** `Näringsli`/`Naringsliv` and `Ovrigt`/`Övrigt`. Each pair has one English alias in the field dictionary.
- **Residence vs workplace:** employment Tab7 counts employed people where they live, Tab8 where they work. Never add or mix them.
- **Names cut to 10 characters:** `Alder_16_1` is age 16–19, `Alder_20_2` 20–24, `Alder_25_4` 25–44, `Alder_45_6` 45–64.
- **A unit missing from a table means no data, not 0.**
- **SCB perturbs small counts,** so sub-groups rarely add up exactly to the total, and an income sum can appear on a cell with 0 households.
- **Data years differ:** population 2024, income 2023 (from the file names).

## Expected results (Helsingborg delivery, checked 2026-10-07)

**Population:**
- 9 tables, 4 issues: the Tab3 encoding fallback, and 2 null DeSO geometries in each of Tab1/4/5_DeSO.
- Grid tables: 492 cells (480 × 250 m + 12 × 1,000 m), except Tab10 with 483. 21 cut cells; 4 coarse cells overlap 250 m cells.
- Totals: 115,895 on the grid; 115,576 on the 100 m grid; 128,150 on DeSO. DeSO areas cut by the delivery rectangle keep the values of the whole area.

**Income:**
- 2 tables, 1 issue: 2 null DeSO geometries.
- Grid: 494 cells (480 × 250 m + 14 × 1,000 m), 55,064 households; 21 cut cells; 7 coarse cells overlap 250 m cells.
- **30 cells have 0 households but a positive income sum.**
- DeSO: 59,791 households; median income 198,693–411,400 SEK.

**Employment (checked 2026-10-13):**
- 6 tables, 4 issues: the cp1252 fallback in Tab7/Tab8 Ruta, and 1 null DeSO geometry (`1283A0010`) in each of Tab8/Tab9_DeSO. 0 unknown columns.
- **Larger extent than population/income** (cut to the study box): 569–674 grid cells per table, 73 DeSO areas.
- Totals: 57,748 employed residents (Tab7), 67,691 jobs on the grid (Tab8; 70,266 per DeSO), 69,814 persons aged 20–64 (Tab9).
- Tab10 is identical to the population delivery's on the 483 cells they share, and has 89 more cells.
- Report: `docs/data-analysis/2026-10-13_helsingborg_scb-employment_profiling.md`.

## Failure cases

- **Folder missing, or no `.shp` in it:** `FileNotFoundError`. Same when `--tables` matches nothing.
- **No encoding can decode a file:** `ValueError` naming the file. Add the encoding to `ENCODING_FALLBACKS`.
- **A new table number or column:** loads and is flagged. Add it to the dataset's `TABLES` / `FIELDS`, with its source.
