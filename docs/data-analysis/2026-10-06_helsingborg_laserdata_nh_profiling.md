# Helsingborg laser data (Laserdata NH) — dataset and profiling report

**Date:** 2026-10-06
**Pipeline:** `Src/pipelines/lidar_heights/` (`run_lidar_height_pipeline.py`)
**Scope:** LAZ headers, the per-tile/strip JSON sidecars, and an in-memory timing probe on one tile. The pipeline itself was not run.

## Dataset

| Item | Value |
|---|---|
| Original name | Laserdata NH (Nationell höjdmodell) — SLU GET name "Laserdata NH 2019 - Lidardata" |
| English | Laser data, national elevation model |
| Source | Lantmäteriet, through SLU GET (`metadata/_readMe_Laser.json`, delivered to GET 2019-12-12) |
| License | FUK (research, education, culture) |
| Folder | `Raw_data/1-Helsingborig/laserdata_nh_helsingborg/` — 15 `.laz` tiles + `*_tile.json` + `*_strip.json`; product sheets in `metadata/` (`lidar_data_nh.pdf` is the English one) |
| Tiles | 2.5 × 2.5 km, E 355,000–362,500, N 6,207,500–6,220,000 (3 × 5 grid) |
| CRS | EPSG:3006 (SWEREF 99 TM) per the sidecars; **the LAZ headers carry no CRS** (the pipeline assumes 3006) |
| Format | LAS 1.2, point format 1, no extra dimensions |
| Points | 118.2 M in total (1.8–11.6 M per tile; the 1.8 M tile is mostly sea) |
| **Scan date** | **2010-04-12 for all 68 strips** (Leica ALS50-II, ~2,200 m altitude); tile classification last changed 2015-04-20 |
| Density | Spec 0.5–1 pt/m²; strips 0.56 pt/m² nominal; ~1.2–1.9 pt/m² per tile where strips overlap |
| Classes (level 3) | 1 Unclassified 46.4 M · 2 Ground 67.7 M · 9 Water 3.9 M · 11 Bridge 0.14 M. No building or vegetation class: roofs, trees and cars are all class 1 |

## Fit with the clean building footprints

Checked against `Processed_data/Helsingborg_Final/buildings_2026-10-06_postprocess/buildings_processed_postprocess.gpkg` (43,981 buildings):

- every building touches a tile (0 outside); 43,621 lie fully inside one tile, the rest cross a tile edge;
- small buildings get few points: in a sample of 50 buildings the median was ~55 points, the minimum 0.

Raw-tile probe (tile `621_35_0075`, 8,883 buildings, raw classes, no PDAL HAG, so the numbers are only indicative): 95% got a height. The rest were 403 `insufficient_coverage` (fewer than 5 points inside, mostly small sheds) and 51 `no_non_ground_points`.

## Findings that affect use

1. **The scan is from April 2010, but the footprints are from 2025.** A building built after April 2010 has no roof in the point cloud. It ends up in one of two ways:
   - **Flagged.** It is counted as `no_non_ground_points` / `insufficient_coverage` and gets no height.
   - **Wrong, and not flagged.** Cars, fences or bushes on the old site give a small height, clamped up to 2 m. Such a building gets a low height with `has_lidar_height = True`.

   Demolished or rebuilt buildings get the old building's height. The data holds no construction dates (`versiongiltigfran` is not one), so these buildings cannot be detected reliably.
2. **Quality labels do not fit this density.** `coverage_ratio` assumes points ~0.2 m apart. With ~1.5 pt/m² it stays around 0.05, below the 0.5 needed for "medium". The probe gave `low` for all 50 sampled buildings. `height_quality` therefore carries no information for this dataset until the thresholds are recalibrated.
3. **Trees over roofs.** No vegetation class exists, so canopy over a roof counts as roof. The 95th percentile can then be tree height.
4. **Performance (fixed 2026-10-06).** The old per-point Python loop took ~0.8 s per building, about 10 h single-core for Helsingborg. The vectorised selection gives the same results (0 differences on 40 buildings) and takes 0.5 ms per building, 4 s for the probe tile. PDAL (SMRF + HAG per tile) is now the slow step.

## Insufficiency note

- **What is missing:** surface data newer than 2010.
- **Why it matters:** it determines whether buildings built in 2010–2025 get a correct height. The count of such buildings is unknown, and in Helsingborg it is not small (e.g. the Oceanhamnen / H+ areas).
- **Already in the repo:** `Raw_data/1-Helsingborig/ytmodell_050_helsingborg.zip`, Lantmäteriet *Ytmodell från flygbild* (surface model from aerial images). It holds 15 LAZ tiles on the same 2.5 km grid, at 0.5 m resolution, from photos flown **2018-04-13 to 2018-04-21**. Building height could be taken as this 2018 surface minus the 2010 ground. The ground changes far less than the buildings. This would still miss buildings from 2018–2025.
- **Smallest additional data:** a laser scan of Helsingborg from after 2018, from Lantmäteriet or the municipality. Coverage has not been checked.
- **Fallback with current data:** use NH 2010 as now, and treat low heights and `no_non_ground_points` as suspect.

## Probe: 2018 surface model (Ytmodell från flygbild) vs NH 2010

**Product:** *Ytmodell från flygbild* (Digital Surface Model from Aerial Photos), product description v1.2, 2020-01-08, in the zip's `metadata/`.

- Delivered as LAZ 1.2, point format 0, on the same 2.5 km tiles (`y<tile>_18.laz`).
- A 2.5D surface: one point per 0.5 m cell (~3.85 pt/m²) at the top of whatever was there.
- **Class 0 only:** there is no ground and no noise class. Pre-2019 production has holes where image matching failed.
- **Accuracy:** about 1.7 × the 0.24 m photo resolution.
- **Flown:** 2018-04-13 to 2018-04-21.

`ytmodell_100_helsingborg.zip` and `ytmodell_irf_100_helsingborg.zip` contain only metadata, with no LAZ files.

**Method** (in memory; script and CSV kept in the session scratchpad only):
- **Ground:** median of NH 2010 ground points (class 2) in a ring 1–6 m around the footprint.
- **DSM height:** 95th percentile of the 2018 points inside the footprint shrunk by 0.5 m, minus ground.
- **NH height:** 95th percentile of NH class-1 points inside the footprint, minus ground.
- **Scope:** tiles `621_35_2575` and `621_35_0075` (central Helsingborg), 18,303 buildings, ~25 s per tile with no PDAL step.

| Result | Value |
|---|---|
| Buildings with a height, NH 2010 (≥ 5 pts) | 90.0% |
| Buildings with a height, DSM 2018 (≥ 3 pts) | **97.9%** (the 390 without are tiny, median 3 m², almost all ancillary) |
| DSM p95 − NH p95, both available (16,320) | median −0.04 m; 85% within 1 m, 92% within 2 m |
| Same, footprints ≥ 100 m² (4,165) | median −0.01 m; 92% within 1 m, 96% within 2 m |
| DSM median vs NH p95 | worse (66% within 1 m), so use the p95 |
| Likely built 2010–2018 (NH none or < 2.5 m, DSM median ≥ 4 m) | 364 buildings (193 ancillary, 162 residence) |
| NH tall but DSM near ground | 189 (demolished/rebuilt, or DSM errors; includes a 22,800 m² industrial building). Needs a flag |
| Low or missing in both | 1,208 (6.6%), almost all ancillary (genuinely low); only 15 are ≥ 50 m² (candidates built after 2018) |
| DSM p95 − median > 5 m (spikes, trees, mixed heights) | 130 (0.7%) |

**Reading:** where the two sources overlap, DSM 2018 agrees with NH 2010 within the DSM's stated accuracy, and it covers more buildings, including those built in 2010–2018. The 2010 ground is already classified by Lantmäteriet (level 3), so no PDAL ground classification is needed. Buildings from 2018–2025 are still missing from both.

## Full run (user, 2026-10-06)

Output: `Processed_data/Helsingborg_Final/lidar_heights/`. 43,981 buildings, unique IDs, EPSG:3006, no null heights.

| Result | Value |
|---|---|
| With height | 41,476 (94.3%): 41,403 from the 2018 surface, 73 from the 2010 laser |
| Without height | 2,505 (5.7%), but only **5.1% of the footprint area** (341,101 of 6,705,555 m²) |
| Reasons | `surface_shows_ground` 2,363 · `no_points` 124 · `below_min_height` 17 · `no_ground` 1 |
| Without height and ≥ 100 m² | 238 buildings, 304,657 m² (125 residences, 35 industrial, 24 public, 21 business) |
| With height, by size | < 15 m² 87% · 15–50 m² 96% · 50–200 m² 98% · ≥ 200 m² 97% |
| Quality | high 28,257 · medium 5,384 · low 7,835 (all with < 20 surface points; median area 6.8 m², mostly sheds) · none 2,505 |
| 2018 vs 2010 | 37,892 with both; median −0.09 m; 82.6% within 1 m, 89.1% within 2 m; 2,747 differ by > 3 m (median 27 m², mostly ancillary) |
| Height | min 1.5, median 4.1, p75 6.9, max 70.7 m (industrial, harbour; 2010 laser agrees at 68.9 m) |

**Checks:**
- **The large "no height" buildings are real gaps.** At the six largest (10,500–40,000 m², industrial and business, mostly at E 361 km), both the 2010 laser (mostly class 2 inside the footprint, at the level of the surrounding ground) and the 2018 surface (fill 1.0) show ground. These buildings were built after April 2018. Only a newer surface can give them a height.
- **Spiky heights.** For 152 buildings the 95th percentile is > 10 m above the median.
  - 24 of them did not exist in 2010, so they were probably under construction in April 2018 (crane or partial structure). Example: a 654 m² apartment building has p95 66.8 m but a median of 12.6 m.
  - The rest are towers, chimneys or silos on part of the footprint, or merged pieces of different heights.
  - The single p95 gives the tallest part to the whole footprint.

**Smallest additional data:** a newer *Ytmodell från flygbild* for Helsingborg. Lantmäteriet updates it with the national aerial-photo programme, about a third of Sweden per year. It is the same product and tiling, so it can replace `ytmodell_050_helsingborg/` without code changes. Which years are available has not been checked.

**Adopted 2026-10-06:** the height pipeline (`Src/pipelines/lidar_heights/`) now uses the 2018 surface for roofs. The ground is a Delaunay interpolation of the NH 2010 ground points evaluated under each roof point, which is more accurate than the ring median used in this probe. The method and decision rules are in `Project_livingContext.md` under *Building Height Pipeline*.
