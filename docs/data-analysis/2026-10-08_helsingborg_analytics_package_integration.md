# Helsingborg analytics package — integration and visualization-readiness report

**Date:** 2026-10-08
**Run:** `python Src/Scripts/Unity/build_unity_package.py --config configs/cities/helsingborg.json`
**Output:** `Unity/City_Digital_Twin/Assets/StreamingAssets/cities/helsingborg/` (git-ignored; holds licensed data)
**Full machine-readable report:** `<package>/analytics_report.json`

## Inputs

| Dataset (sv → en) | Source | Unit | Year | Read through |
|---|---|---|---|---|
| *Befolkning* → population | SCB via SLU GET (FUK licence) | Ruta 250 m / 1 km, DeSO | 2024 | `pipelines.scb.loader` |
| *Inkomster* → income | SCB via SLU GET (FUK licence) | Ruta, DeSO | 2023 | `pipelines.scb.loader` |
| *Röster per distrikt* + *Valdistrikt* → votes per voting district + boundaries | Valmyndigheten (assumed) | voting district | 2026 (municipal) | `pipelines.election.loader` (municipality 1283) |
| *Byggnad* + heights → buildings | Lantmäteriet + height pipeline | building | footprints 2025, heights 2018 | `buildings_lidar_added.gpkg` |

## What was produced

**Spatial layers** (all MultiPolygon, EPSG:3006, hidden until a visualization uses them):

| Layer | Units | Notes |
|---|---|---|
| `ruta` | 501 | Exact squares from the cell id. 1 km cells drawn as the remainder outside their 250 m cells (e.g. 0.5625 km² = 1 km² − seven 250 m cells). Unit id `ruta:<size>_<RutID>`. |
| `deso` | 68 | As delivered (cut to the delivery rectangle). `1283A0010` and `1283C1010` have no geometry and are left out. |
| `valdistrikt` | 92 | The whole municipality. The collection district `128300` (2.2 % of votes) has no area and is not a unit. |

**Data layers:**

| Layer | Target | Variables | Notes |
|---|---|---|---|
| `ruta_population` | ruta | 23 | Counts, density, shares (age, sex, birth region, education 25–64). Shares are over the sum of sub-groups (D3). Missing cell = no value (D4). |
| `ruta_income` | ruta | 9 | Households, quartile counts and shares, income sum, mean economic standard. The mean has no value in **30 cells with 0 households but an income sum** (D4). |
| `deso_population` | deso | 8 | Shares, births, deaths, net migration per 1,000. Tab5 meanings are inferred. |
| `deso_income` | deso | 4 | **Median income** (only here), households, quartile shares. |
| `valdistrikt_election` | valdistrikt | 26 | Eligible voters, votes, turnout, shares for 9 parties (8 Riksdag parties + Helsingborgspartiet) + `share_other` (20 parties). Also deviations from the municipal result, and the largest party (string). Municipal turnout 79.6 % includes the collection district. |
| `valdistrikt_estimates` | valdistrikt | 8 | Dasymetric estimates (see the decision note), only for the 60 districts ≥ 99 % inside the SCB extent. |

**Associations** (building → unit, by representative point):

| Association | Linked | Unlinked |
|---|---|---|
| `buildings_to_ruta` | 40,043 | 3,938 (no populated cell: industry, harbour, parks) |
| `buildings_to_deso` | 43,981 | 0 |
| `buildings_to_valdistrikt` | 43,981 | 0 |

## Dasymetric estimate (D1 + D2)

| Item | Value |
|---|---|
| Residential buildings receiving population | 12,521 |
| Cells spread over non-residential buildings (no residential building) | 40 |
| Cells without any building | 3 (53 persons, assigned to a district by cell centre) |
| Population allocated to buildings | 115,842 of 115,895 |
| Check against SCB 100 m grid | 2,212 cells, R² 0.76, MAE 14.8 persons |
| Eligible voters per estimated resident (median of 60 districts) | 0.81 |
| Turnout vs estimated mean income, 60 districts (Spearman) | 0.91 |
| SD share vs estimated mean income (Spearman) | 0.21 |

## Visualization readiness

- **Target entities:** grid cells, DeSO areas, voting districts and buildings, through the associations.
- **Temporal granularity:** static, one year per source. Every legend shows the year in the data-layer name.
- **Units after conversion:**
  - persons, households, SEK (economic standard = disposable income per consumption unit);
  - % (0–100) for shares and turnout, percentage points for deviations;
  - persons/km² for density.
- **Scales in the presets:**
  - data min–max for district and DeSO layers;
  - 2–98 percentile for grid colours, so tiny cells do not dominate;
  - a fixed 0–50 % domain for the share aged 65+ in the four-variable view (many tiny cells are 100 %).
- **Performance:** package additions are 1.2 MB of geometry, 0.3 MB of data and 8.9 MB of associations. All six presets apply without errors in Play mode, within a few seconds each.
- **Not measured:** memory and frame rate on Quest (VR is the next step).

## Quality risks

- **Edge cells and DeSO areas are cut** at the delivery rectangle while their values describe the whole unit.
- **SCB perturbation** (Cell Key Method, ±3) makes small cells noisy; shares in tiny cells are extreme.
- **The estimate assumes all residential floor area is equally occupied.** Buildings without a measured height count as one floor.
- **The data years differ** (2023 / 2024 / 2026).
