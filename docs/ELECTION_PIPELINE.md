# Election pipeline — preprocess stage

**Status (2026-10-07):** load, check, profile and view only. No processing rules yet. The frames are shown as delivered, except that they are filtered to one municipality, and party columns that are empty for that municipality are dropped (both recorded). Processing, such as which form to use, how to treat the collection district, and moving votes to another unit, goes in `preprocessor.py` once decided.

## Data

| | Results | Boundaries |
|---|---|---|
| Original name (sv) | *Röster per distrikt, slutligt antal röster inklusive totalt valdeltagande* | *Valdistrikt* |
| English | Votes per voting district, final count, incl. turnout | Voting districts |
| File (Helsingborg) | `Raw_data/1-Helsingborig/election/roster-per-distrikt-slutligt-antal-roster-inklusive-totalt-valdeltagande-kommunvalen-2026.xlsx` | `.../election/valdistrikt-skane-lan-2026.geojson` |
| Coverage | whole country (290 municipalities, 6,626 districts) | Skåne county (836 districts), EPSG:3006 |
| Election | municipal council election (*val till kommunfullmäktige*) 2026, final count by the county administrative boards | same year |

- **Source:** Valmyndigheten (Swedish Election Authority), assumed from the file names and the workbook's Information sheet.
- **Licence:** not stated in the files. Election results are public, but check the terms before publishing anything.

### Workbook sheets

| Sheet | Content | Level |
|---|---|---|
| `Information` | description of the workbook (Swedish) | — |
| `roster_KF` | votes per district and party **or summary category**, one row per pair (long format) | district |
| `Antal_kommun` / `_valkrets` / `_distrikt` | votes, one column per party | municipality / constituency / district |
| `Andel_kommun` / `_valkrets` / `_distrikt` | share of valid votes (0–1), one column per party | same |
| `Valdeltagande_kommun` / `_valkrets` / `_distrikt` | votes cast, eligible voters, turnout | same |

## How to run

From the repo root. The municipality is given by its 4-digit code (*Kommunkod*); Helsingborg is 1283.

```bash
python Src/Scripts/run_election_pipeline.py --input "Raw_data/1-Helsingborig/election" --municipality 1283 --output "Processed_data/Helsingborg_Final"
```

The folder must hold exactly one `.xlsx` and one boundary file (`.geojson`, `.gpkg` or `.shp`). Reading the national workbook takes about 2 minutes. Open the printed `index.html`.

Tests (synthetic data only): `python tests/test_election_pipeline.py`

## Outputs

`<output>/election_<YYYY-MM-DD>/`:

```
index.html                          run, frames, issues/notes, key figures, consistency checks, parties,
                                    collection districts, results ↔ boundaries, Information sheet, field dictionary
tables/<frame>.html                 every sheet (municipality only) and the district boundaries
profiles/<frame>_profile.html/.md   DataFrameProfiler report per frame
election_preprocess_report.json     inputs, rows before filtering, dropped columns, every check
```

## Code (`Src/pipelines/election/`)

| File | Job |
|---|---|
| `config.py` | sheet catalog, code columns, summary categories, national parties (abbreviation + English), field dictionary |
| `loader.py` | finds the two files; election type/year from the file name; reads the workbook with **codes as text**; filters to the municipality; drops empty party columns |
| `validator.py` | consistency checks, boundary checks, results ↔ boundaries join |
| `preprocessor.py` | empty for now |
| `exporter.py` | HTML views, profiles, index page, JSON report |
| `pipeline.py` | `ElectionPipeline(BasePipeline)` |

## Checks

**Results:**
- district codes: repeats, format, collection districts;
- in the long sheet: party votes = valid votes; valid + invalid = votes cast; the summary rows equal the summary columns;
- wide counts equal the long sheet, district by district;
- shares = counts / valid votes;
- votes / eligible voters reproduces the turnout column;
- turnout votes equal the votes-cast rows;
- municipality totals = sum over the districts.

**Boundaries:**
- CRS;
- null, empty or invalid geometry;
- repeated or malformed codes;
- unknown columns;
- overlaps between districts;
- area and extent.

**Join:** result districts without a boundary, boundaries without results, and name differences.

## Assumptions and known traps

- **Codes are text with leading zeros** (`0114`, `01140101`). pandas' Excel reader turns them into numbers unless told otherwise (verified), so `CODE_COLUMNS` are read as text.
- **The long sheet mixes parties with summary rows** (`Summa giltiga röster`, `Ogiltiga röster - …`, `Valdeltagande`, `Röstberättigade`). Summing `Röster` over all rows double-counts. Summary categories are listed in `SUMMARY_CATEGORIES`; every other category is treated as a party.
- **Collection district** (*Uppsamlingsdistrikt*, code = 6-digit constituency code): votes counted centrally. It has votes but no area and 0 eligible voters, and it is missing from the boundary file.
- **`Valdeltagande (%)` is a fraction 0–1,** despite the name.
- **The wide sheets have a column for every party in the country.** Columns empty for the municipality are dropped (294 per sheet for Helsingborg).
- **Only the eight Riksdag parties get English names and abbreviations.** Local parties keep their Swedish name.

## Expected results (Helsingborg 1283, checked 2026-10-07)

- **0 issues; all 9 consistency checks pass.**
- **Districts:** 92 voting districts plus 1 collection district (`128300`: 2,067 votes cast, 2.2% of the municipality).
- **Totals:**
  - 95,468 votes cast, 94,476 valid, 992 invalid;
  - 119,937 eligible voters;
  - turnout per district 57.6%–93.4% (median 78.9%).
- **Boundaries:** 92 districts covering 425 km² (district median 0.63 km²), no overlaps; all 92 match the results by code and name.
- **Parties:** 29 got votes in Helsingborg. Largest: S 29.9%, M 25.5%, SD 18.7%, V 9.5%.

See also: `docs/data-analysis/2026-10-07_helsingborg_election_2026_profiling.md`. It covers how the districts relate to the SCB grid and DeSO areas.

## Failure cases

- **Not exactly one `.xlsx` and one boundary file in the folder:** `FileNotFoundError` listing what was found.
- **Municipality code not 4 digits, or not in the files:** `ValueError`.
- **A new sheet or column:** loaded and flagged (`config.SHEETS`, `config.FIELDS`).
- **A new invalid-vote category:** reported as an issue.
