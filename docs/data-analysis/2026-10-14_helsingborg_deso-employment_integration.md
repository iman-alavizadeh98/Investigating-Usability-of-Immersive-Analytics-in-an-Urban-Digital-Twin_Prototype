# DeSO employment rate in the Unity package — integration and visualization readiness

**Date:** 2026-10-14
**Purpose:** variable B of pattern scenario P1 (median income × employment rate,
DeSO) on the 3D table, the same numbers as the 2D study maps
(`Processed_data/Helsingborg_Final/study/study_2d_maps.pdf`).
**Licence:** SCB data under FUK (research and education only). The package is
git-ignored; this report contains no tables of values.

## Source

| | |
|---|---|
| Dataset | *Arbetsmarknad — Statistik på ruta och DeSO från SCB* → Labour market, grid and DeSO statistics (SCB, via SLU GET) |
| Table | Tab9 *Befolkning 20–64 år efter sysselsättning* → Population aged 20–64 by employment status; file `Tab9_DeSO_2023` |
| Fields used | `Sysselsatt` → employed; `EjSsyssels` → not employed; `Totalt` → residents 20–64; DeSO code column `DeSO_2025` |
| Year | 2023 (from the file name) |
| CRS | EPSG:3006 (geometry not used; values joined by DeSO code) |
| Meanings | Age range 20–64 and "Sysselsatt = employed" are inferred from the delivery PDF's older table A7 (see `docs/data-analysis/2026-10-13_helsingborg_scb-employment_profiling.md`) |

Found by **table number and unit** (`find_table(tables, "9", "DeSO")`), not by
file name, so the code works for any city's delivery. Folder from the city
config: `statistics.employment` (`configs/cities/helsingborg.json`:
`Raw_data/1-Helsingborig/employment`).

## Processing

```
employment_rate_20_64 = 100 × Sysselsatt / Totalt      [%]
```

- The study design specifies the **published total** `Totalt` as the
  denominator. The other package shares divide by the sum of their sub-groups
  (rule D3), because SCB perturbs small counts. Here `Sysselsatt + EjSsyssels`
  differs from `Totalt` in **54 of 68** areas (perturbation of a few persons);
  the effect on a rate over ~1,000 residents is well below one percentage point.
- `Totalt = 0` → no value; a DeSO missing from the table → no value, never 0 (D4).
- Also written: `employed_20_64`, `not_employed_20_64`, `population_20_64` (counts).

## Integration

| | |
|---|---|
| Target | data layer `deso_employment` on spatial layer `deso` (68 areas with geometry) |
| Join | DeSO code: `deso:<DeSO_2025>` = package unit id (same code system as the population/income DeSO tables) |
| Matched | **68 of 68** DeSO areas have a rate |
| Table rows not written | 5 (`1283A0010`, `1283A0030`, `1283C1010`, `1283C1690`, `1283C1700`): not in the package's DeSO layer (no geometry in the delivery box, or not in the population delivery) |
| Range | 50.5 – 89.2 % |
| Code | `Src/pipelines/unity_package/statistics.py: export_deso_employment` (called from `analytics.py` when the config has `statistics.employment`) |

## Visualization readiness

- **Spatial entity:** DeSO area (values describe the whole area, also where its
  polygon is cut by the delivery box).
- **Temporal:** static, 2023. P1 pairs it with median income 2023 (same year).
- **Units:** percent of residents aged 20–64.
- **Scale in the 3D view (P1):** colour, 5 quantile classes (equal counts) with
  the five oranges of the 2D maps (palette `study_oranges5`); see
  `docs/EVALUATION_SESSION.md` → Pattern study.
- **Check against the study numbers** (package values, Spearman ρ over units
  with both values):

| Scenario | A | B | ρ | n | Study design |
|---|---|---|---|---|---|
| P1 | DeSO median income | employment rate 20–64 | +0.922 | 68 | +0.92, 68 |
| P2 | DeSO median income | share aged 0–15 | −0.085 | 68 | −0.08, 68 |
| P3 (until 2026-10-14) | district turnout | share born abroad (estimate) | −0.945 | 60 (32 districts without B) | −0.94, 60 |
| P3 (new, 250 m grid, ≥ 50 residents) | share born abroad | share with ≥ 3 years post-secondary education | −0.707 | 357 | — |

## Reproduce

Full package (data machine, needs all raw deliveries):

```bash
python Src/Scripts/Unity/build_unity_package.py --config configs/cities/helsingborg.json
```

Only this layer, into an existing package (needs only the employment folder and
the package's DeSO layer):

```bash
python Src/Scripts/Unity/build_unity_package.py --config configs/cities/helsingborg.json --only employment
```

Test: `tests/test_unity_analytics.py::test_deso_employment_rate_and_missing_units`.

## Open points

- The 2D study builder (`Src/pipelines/data_report/study.py`) still computes the
  rate from the raw table (`source: employment_rate_deso`). It gives the same
  numbers; it could read `deso_employment` from the package instead, so both
  sides share one source.
