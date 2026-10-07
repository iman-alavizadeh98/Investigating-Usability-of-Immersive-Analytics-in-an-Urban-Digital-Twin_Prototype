# Helsingborg municipal election 2026 — profiling report

**Date:** 2026-10-07
**Scope:** read-only profiling of the election delivery, and how it lines up with the SCB population/income data. Nothing has been exported. Produced with `Src/pipelines/election/` (see `docs/ELECTION_PIPELINE.md`) plus one ad-hoc spatial cross-check (method below).

## Dataset

| Item | Value |
|---|---|
| Original names | *Röster per distrikt, slutligt antal röster inklusive totalt valdeltagande — kommunvalen 2026*; *Valdistrikt Skåne län 2026* |
| English | Votes per voting district, final count incl. turnout — municipal elections 2026; voting districts, Skåne county 2026 |
| Source | Valmyndigheten (assumed from file names and the workbook's Information sheet); counts are the county boards' final count |
| Access date | files added to `Raw_data/1-Helsingborig/election/` on 2026-10-07 |
| Format, CRS | XLSX (11 sheets, national); GeoJSON, EPSG:3006 (836 districts in Skåne) |
| Licence | not stated in the files; check before publishing |
| Municipality | Helsingborg, `Kommunkod` 1283: 92 voting districts + 1 collection district, one municipal constituency |

Original field names and English aliases: `Src/pipelines/election/config.py` (`FIELDS`, `SUMMARY_CATEGORIES`, `NATIONAL_PARTIES`). They are also shown in the pipeline's `index.html`.

## Findings

1. **The data is internally consistent.** All 9 checks pass for Helsingborg:
   - party votes add up to valid votes, and valid plus invalid votes equal votes cast;
   - the wide, share and long sheets agree in every district;
   - turnout recomputes exactly;
   - municipality totals equal the sum over the districts.
2. **The collection district** `128300` (*Uppsamlingsdistrikt 00*) holds 2,067 votes cast (2.2%). It has no area and no eligible voters, so it cannot be placed on a map. Its votes count toward the municipal result but not toward any district.
3. **Codes have leading zeros nationally** (`0114…`). pandas reads them as numbers by default, which breaks the codes for counties 01–09. The loader now reads them as text. Helsingborg (`12…`) would not have shown the bug.
4. **The long sheet mixes parties and summary rows.** A plain sum of `Röster` would triple-count.
5. **The turnout column is a fraction (0–1)** although it is labelled `(%)`.
6. **Boundaries:** 92 districts, all valid, no overlaps, 425 km² in total. Every district matches the results by code and name.
7. **Result:** 95,468 votes cast, turnout 57.6–93.4% per district (median 78.9%). S 29.9%, M 25.5%, SD 18.7%, V 9.5%. 29 parties received votes; Helsingborgspartiet, the largest local party, got 1.0%.

## How the election lines up with the SCB data (affects integration)

**Method:** ad-hoc script, read-only.
- **Extent:** the SCB extent is the bounding box of the population grid tables (E 355,285–361,855, N 6,207,503–6,218,191).
- **Population per district:** 100 m population cells (`Tab6_Ruta_2024_region`) were assigned to voting districts by cell centroid.
- **DeSO overlap:** DeSO areas (`Tab1_DeSO_2024`, 68 with geometry) were overlaid with the districts. Slivers under 100 m² were ignored.

| Question | Answer |
|---|---|
| Area covered by SCB data / buildings | the 70 km² delivery rectangle |
| Area covered by the election | the whole municipality, 425 km² |
| Voting districts fully / partly / not inside the rectangle | **59 / 17 / 16** |
| 100 m population inside some voting district | 115,576 of 115,576 (all) |
| Eligible voters per resident, in the 59 fully covered districts | median **0.80** (p10 0.72, p90 0.95) |
| Size: DeSO vs voting district (median) | 0.55 km² / 1,859 residents vs 0.63 km² / 1,328 eligible voters |
| DeSO lying ≥ 90% inside one voting district | 34 of 68 |
| Voting districts lying ≥ 90% inside one DeSO | 48 of 77 that touch a DeSO |

**What this means:**
- **Voting districts and DeSO areas are about the same size, but they don't nest.** About half match one-to-one. The rest straddle, so any shared unit needs a reallocation step: a share of one area's values goes to each area it overlaps.
- **Coverage is limited by the rectangle.** Only the 59 districts fully inside it can be compared with population and income at full coverage, plus the inner parts of 17 more. The 16 districts outside have election data only.
- **The ratio of 0.80 eligible voters per resident is plausible** (minors are not eligible). The grid-based population counts and the voter counts can therefore support a population-weighted reallocation. The ratio varies (0.72–0.95), which is a reminder that residents ≠ voters.

## Open decisions for the user

1. **Shared unit:**
   - DeSO (median income exists there; votes reallocated), or
   - voting district (votes exact; population and income reallocated from the 100 m / 250 m grid), or
   - keep each on its own unit and link them through buildings.
2. **The 17 partly covered districts:** use the covered part only, or drop them from comparisons.
3. **The collection district's 2.2%:** leave it unmapped (recommended; it is real but has no place), or spread it over the districts.
4. **Long or wide form for analysis:** the long sheet plus the turnout sheet carry everything; the wide sheets are redundant (verified identical).
