# Data report (PDF) — every dataset, profiled and mapped

**Added:** 2026-10-13
**Code:** `Src/pipelines/data_report/` + `Src/Scripts/build_data_report.py`

## Purpose

A single, readable PDF that explains the city's data:

- what each dataset is (Swedish and English name, source, licence, year, unit);
- how far each delivery reaches, and when it describes the city;
- what each one contains, with maps and charts;
- how the datasets relate to each other;
- their known limitations.

It is meant for understanding the data and for discussing it with supervisors. It is not an input to the pipeline.

## How to run

```bash
python Src/Scripts/build_data_report.py --config configs/cities/helsingborg.json
```

`--output <folder>` overrides the output folder. The default is the config's `dataReport.output`.

Run time is about 4–5 minutes, mostly reading the buildings and drawing the building maps.

## Inputs

All read-only. The script is city-agnostic: every path comes from the city config.

| Config key | What is read |
|---|---|
| `buildings.source` | processed buildings with heights (`buildings_lidar_added.gpkg`), plus `lidar_heights_summary.json` next to it |
| Unity package (`unity.*`) | `spatial_layers/{ruta,deso,valdistrikt}/geometry.json` and `data_layers/*/values.json`, the same values the twin shows. **Run `build_unity_package.py` first.** |
| `statistics.population`, `statistics.income` | SCB deliveries (table years and extents) |
| `election.folder`, `election.municipalityCode` | municipal totals per party, collection district |
| `dataReport.employment` | SCB *Arbetsmarknad – Utbildning* (not in the package yet) |
| `dataReport.lidarTiles`, `dataReport.surfaceModel` | `*_tile.json`, `*_strip.json`, `y*.json` sidecars (point counts, dates); no LAZ is read |
| `dataReport.roadMap`, `dataReport.propertyMap` | Vägkartan and Fastighetskartan shapefiles (context and counts) |
| `dataReport.buildingsRaw` | the raw buildings folder, for its `_readMe` delivery note |

## Outputs

`<output>/<city>_data_report.pdf` and `<output>/figures/*.png` (19 figures).

The report shows FUK-licensed data, so `dataReport.output` must point to an ignored folder (`Processed_data/...`). The PDF footer repeats the licence warning on every page.

## Contents of the PDF

- **Cover:** key figures, licence note, contents.
- **Key findings and how to read the report.**
- **Sections 1–10:**
  1. dataset inventory, extents and data years;
  2. base map;
  3. buildings and heights;
  4. LiDAR and surface model;
  5. population;
  6. income;
  7. employment and education;
  8. election;
  9. relationships (Spearman ρ, correlation matrix);
  10. limitations.
- **Appendix A:** SCB field dictionary (Swedish → English, with the source of each meaning).
- **Appendix B:** how the report was made.

## Design rules

- **Numbers come from data.** Every number in the text is computed at run time from the data, through the `facts` each figure function returns. Re-running after a data update keeps text and figures consistent.
- **Interpretive sentences are hand-written and must be re-checked after a data change.** These are the geographic descriptions such as "richer areas along the northern coast", and they live in `report.py`.
- **Missing ≠ 0.** Units missing from a table are hatched "no data", never drawn as zero. On the employment maps a reported 0 has its own class.
- **Map conventions:**
  - all statistics maps share one frame (the union of the grid deliveries and the study area);
  - classes hold roughly equal numbers of areas unless fixed;
  - legends sit below the maps;
  - the dashed rectangle is the study area.
- **Colours:** one documented palette (`style.py`).
  - Blue ramp for magnitude; blue ↔ red with a grey midpoint for signed values.
  - Categorical slots in a fixed order.
  - Exception: the three parties that win districts use their Swedish conventional colours (M blue, S red, SD yellow).
- **Fonts:** matplotlib's DejaVu fonts are registered in ReportLab, so å/ä/ö, ρ, ≥ and ² render in text and figures alike.

## Dependencies

`reportlab` (new; added to `requirements.txt` and installed in `digitaltwin`), plus `matplotlib` and `Pillow`, which are already present.

## Verify

1. The script prints the PDF path and exits 0.
2. Open the PDF. It should have 24 pages, and the contents page numbers should match the section pages.
3. Compare with Helsingborg 2026-10-13:
   - 43,981 buildings, 2,505 without a height;
   - 115,895 grid residents;
   - 67,691 grid jobs;
   - 95,468 votes cast, municipal turnout 79.6 %;
   - DeSO income vs employment ρ = 0.92.

## Failure cases

- **Unity package missing:** `FileNotFoundError` on `geometry.json`. Run `Src/Scripts/Unity/build_unity_package.py --config ...` first.
- **`dataReport` section missing:** the script stops on the LiDAR folder, which is required. Every other `dataReport` key is optional: without it, the related figures or layer counts are missing or wrong.
- **A new SCB table year or column:** loads as usual. The appendix lists only fields in the dataset dictionaries.

## Known limitations

- **One election only.** Only the municipal-election sheet (`roster_KF`) is used.
- **Correlations describe areas, not people.** The report says so; keep that wording if the text is edited.

## Study materials: 2D vs 3D pattern test (2026-10-14)

```bash
python Src/Scripts/build_study_materials.py --config configs/cities/helsingborg.json
```

- **Scenarios:** `configs/study/pattern_scenarios.json` (P1 income × employment rate, P2 income × share 0–15, P3 turnout × share born abroad). Questions, data and expected answers live there; ρ, n and named areas are computed.
- **Output** (`Processed_data/Helsingborg_Final/study/`, git-ignored):
  - `study_2d_maps.pdf`: participant material, one landscape page per scenario (question, map A in blue, map B in orange, 5 equal-count classes). No answers.
  - `study_facilitator_handbook.pdf`: session plan, script, scoring, answers with Spearman ρ, example areas, exceptions, scatter plots, interview guide, survey outline, answer sheet. Facilitator only.
- **Checked 2026-10-14:** P1 ρ = +0.92 (68 DeSO), P2 ρ = −0.08 (68 DeSO; median split 17/17/17/17), P3 ρ = −0.94 (60 districts). **P3 replaced the same day:** share born abroad × share with ≥ 3 years post-secondary education on 250 m grid cells with at least 50 residents, ρ = −0.71 (n 357); the scenario's `unitFilter` hides the other cells in both the 2D maps and the 3D view.
- The DeSO employment rate comes from the SCB employment delivery (Tab9_DeSO). Since 2026-10-14 the same rate is also in the Unity package (`deso_employment.employment_rate_20_64`, config `statistics.employment`); the 3D views of P1–P3 read it from there and give the same ρ and n (`docs/data-analysis/2026-10-14_helsingborg_deso-employment_integration.md`). The 3D side of the study: `docs/EVALUATION_SESSION.md` → Pattern study.
