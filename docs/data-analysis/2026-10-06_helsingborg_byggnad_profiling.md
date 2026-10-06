# Helsingborg buildings (Byggnad) — dataset and profiling report

**Date:** 2026-10-06
**Pipeline:** `Src/pipelines/buildings/` (`run_buildings_pipeline.py`)
**Scope:** raw input only. The pipeline itself was not run for this report.

## Dataset

| Item | Value |
|---|---|
| Original name | Byggnad, vektor (Byggnad Nedladdning, vektor, product spec v1.6) |
| English | Buildings, vector |
| Source | Lantmäteriet, delivered through SLU GET (`_readMe_ByggnadVektor.json`) |
| Delivered | 2025-05-16 |
| License | FUK (Forskning, utbildning och kulturverksamhet — research, education, culture) |
| File | `Raw_data/1-Helsingborig/byggnad_gpkg_helsingborg/byggnad_sverige.gpkg`, layer `byggnad` |
| CRS | EPSG:3006 (SWEREF 99 TM), 2D (no Z) |
| Extent | E 355,285–361,855, N 6,207,503–6,218,191 (about 6.6 × 10.7 km) |
| Versions | `versiongiltigfran` 2011-03-21 to 2025-05-07 |
| Rows / unique IDs | 45,891 rows, 43,981 `objektidentitet` |
| Geometry | 45,819 MultiPolygon, 72 Polygon; 0 invalid, 0 empty |
| Owner organisation | Lantmäteriet 26,377, Helsingborg (municipality) 19,430, Kommunsamverkan 84 |

## Fields (Swedish → English alias)

| Swedish | English column | Coverage | Notes |
|---|---|---|---|
| objektidentitet | object_id | 100% | UUID, defined as globally unique — see *Repeated IDs* |
| objekttyp | object_type (+ `_en`, `object_type_category`) | 100% | 7 values |
| andamal1…5 | primary…quinary_purpose (+ `_en`) | andamal1 100%, andamal2 713 buildings, andamal3 1 | format `"<objekttyp>;<ändamål>"` |
| byggnadsnamn1/2/3 | building_name_primary/secondary/tertiary | 217 / 13 / 0 buildings | mostly schools (100), unspecified public (27), religious (17) |
| husnummer | house_number | 100% (1–643) | building number **within a property** (registerenhet), not a street number |
| huvudbyggnad | main_building_flag (bool) + `main_building_flag_sv` | Ja on 148 buildings | marks the main building of a complex, for presentation |
| insamlingslage | collection_level (+ `_en`) | 100% | Fasad / Takkant / Ospecificerad (data is capitalised, spec lowercase) |
| lagesosakerhetplan / hojd | position_uncertainty_plan_m / height_m | 100% | plan: median 0.08 m, p90 0.5 m, max 50 m |
| versiongiltigfran, objektversion, objekttypnr, ursprunglig_organisation | version_valid_from, object_version, object_type_number, original_organisation | 100% | |

## Building types (unique IDs)

| objekttyp → English | Purpose detail available |
|---|---|
| Komplementbyggnad → Ancillary building | 26,413, no detail |
| Bostad → Residence | detached 6,469; apartment block 2,392; terraced 1,837; linked 1,245; small house with several flats 583 |
| Övrig byggnad → Other building | 2,106, no detail |
| Industri → Industrial | unspecified 803; `Tillverkning` (manufacturing) 334 |
| Samhällsfunktion → Public facility | unspecified 745; school 162; religious 57; cultural 42; sports hall 15; public bath 11; animal hospital 10; **hospital 9**; prison 8; health centre 8; fire station 4; bus station 3; railway station, riding hall, ice rink, police station 1 each |
| Verksamhet → Business | 607, no detail |
| Ekonomibyggnad → Farm building | 114, no detail |

Mixed use: 652 buildings have `Verksamhet;` (business) as a second purpose, 56 have `Bostad;Flerfamiljshus`.

## Anomalies found

1. **Repeated IDs.** 1,634 IDs appear on 3,544 rows (1,910 extra rows). All have the same version; the geometries do not overlap; 1,384 groups touch and union into one polygon. Only `lagesosakerhetplan`, `lagesosakerhethojd` and `insamlingslage` differ between the rows. Reading: one building delivered as pieces that were surveyed differently. 0 exact duplicate rows in Helsingborg. Handling: merged in the postprocess — [decision](../decisions/2026-10-06_building-duplicate-ids-merge.md).
2. **`Industri;Tillverkning`** (334 buildings) is not in spec v1.6 Table 6 (which lists e.g. *Annan tillverkningsindustri*). Translated literally as "Manufacturing" and flagged `not_in_spec` in `config.py`.
3. **Large "unspecified" share.** Public facilities: 745 of 1,077 have no specific use. Industrial, business, ancillary, other and farm buildings carry no specific use at all.
4. Collection-level values are capitalised in the data but lowercase in the spec; lookups are case-insensitive.
5. **Small buildings.** 12,657 buildings (after merging pieces) are under 15 m², mostly Komplementbyggnad. This is allowed: the spec requires buildings over 15 m² and permits smaller ones. Do not treat 15 m² as a minimum.

## Notes from the product description (v1.6) that affect use

Checked 2026-10-06 against `metadata/byggnad_nedladdning_vektor_1.6.pdf`.

- **One object = map geometry + register data** (§2.2.1): since 2011, each building's geometry and its record in the property register are stored together as one object. This fits the repeated-ID finding: one register building delivered as several surveyed geometry pieces. Merging them restores the one-object-per-building model.
- **Footprint area depends on the collection level** (Table 7). `Fasad` traces the outer wall inside the roof overhang; `Takkant` traces the roof edge, which is larger. Areas are therefore not strictly comparable between buildings. In Helsingborg, 183 merged buildings mix both levels (flagged by `collection_level_mixed`).
- **Type reliability** (§2.4.3): misclassifications occur mainly in Övrig byggnad, Ekonomibyggnad and Komplementbyggnad. Lantmäteriet does no field checks. Inside a municipality's responsibility area, purposes are classified by the municipality (§2.2.1).
- **`lagesosakerhetplan` / `hojd`** are the average deviation from the true position, in metres (Table 5). The required range for buildings is 0.02–50 m (Table 3). Values of 0.025–0.5 m usually come from municipal surveys (§2.4.4).
- **`husnummer`**: the building designation is the property designation plus `husnummer`, and numbers may be reused (Table 5). It is not an address and is not unique on its own.
- **`huvudbyggnad`**: set only where a main building in a complex needs marking, mainly for presentation. `Nej` therefore does not mean "secondary".
- **`versiongiltigfran`** tracks versions only. It is not a construction date or a date when the information became valid.
- **Codelist history** (v1.4, 2021): "Ospecificerad" was removed for Ekonomibyggnad, Verksamhet, Komplementbyggnad and Övrig byggnad. They now carry an empty purpose ("-" in the PDF, `Verksamhet;` in the data), which matches the data.
- **CRS** (§2.3.1): county and national extracts come in SWEREF 99 TM, but municipal extracts may come in a local SWEREF zone. This delivery is EPSG:3006, and `validate()` flags any other CRS.

## For the Unity building info panel

Available per building after the pipeline: name(s), object type (sv/en), all purposes (sv/en), broad category, house number within the property, main-building flag, footprint area (m²), collection level, position uncertainty, number of merged pieces, plus the LiDAR height once that pipeline runs.

**Missing: street address.** Byggnad has no address fields, and the Helsingborg map layers checked (`fastighetKommunikation` text layer `tx_riks`, 341 points) only hold place and area names. Why it matters: "address" was requested for the info panel. Smallest additional data: Lantmäteriet **Belägenhetsadress** (address points: street name, number, postcode, locality) for Helsingborg, which can be ordered from the same source (Geotorget / SLU GET); join address points to footprints by point-in-polygon. The property designation (fastighetsbeteckning), which together with `husnummer` forms the official building designation, is also not in this delivery. Fallback with current data: show name (where present), type, purpose and house number, and label the house number as "building no. within property".
