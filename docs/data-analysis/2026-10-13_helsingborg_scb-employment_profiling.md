# Helsingborg SCB employment (Arbetsmarknad) — profiling report

**Date:** 2026-10-13
**Scope:** read-only profiling of the raw delivery with the new `employment` pipeline (`Src/pipelines/employment/`, see `docs/SCB_PIPELINES.md`). Nothing has been exported to the Unity package.

## Dataset

| Item | Value |
|---|---|
| Original name | *Arbetsmarknad – Utbildning* (Labour market – Education), "Statistik på ruta och DeSO från SCB" |
| Source | SCB, through SLU GET (`metadata/_readMe_ArbetsmarknadUtbildning.json`) |
| License | FUK (research, education, culture). Not publishable, so never commit exports |
| Folder | `Raw_data/1-Helsingborig/employment/` (shapefiles) |
| CRS | EPSG:3006 (all 6 tables) |
| Extent | **cut to the study box** (`2026-10-13_helsingborg_study-area_extent.md`): DeSO bounds are exactly E 355,000–362,500, N 6,207,500–6,220,000. Grid cells reach E 354,000–363,000, N 6,207,000–6,221,000 because 1 km cells that cross the box edge are kept whole. **This is a larger area than the population/income delivery** (E 355,285–361,855, N 6,207,503–6,218,191) |
| Years | `_2023` (Tab7–9), `_2024` (Tab10); DeSO codes `DeSO_2025` |

## Tables

| File | Unit | Rows | Columns (as delivered) | Meaning (English alias) |
|---|---|---|---|---|
| `Tab7_Ruta_2023` | Ruta 250/1000 m | 569 | `Näringsli, Staten, Kommun, Region, Ovrigt, Totalt` | employed persons 16+ **where they live** (*nattbefolkning*, night-time population), by sector |
| `Tab8_Ruta_2023` | Ruta 250/1000 m | 674 | `AstRutstor, AstRutID_S, Näringsli, Staten, Kommun, Region, Övrigt, Totalt` | employed persons 16+ **where they work** (*dagbefolkning*, daytime population; `Ast` = *arbetsställe*, workplace), by sector |
| `Tab8_DeSO_2023` | DeSO | 73 | `astDeSO_20, Naringsliv, Staten, Kommun, Region, Ovrigt, Totalt` | as Tab8 Ruta |
| `Tab9_Ruta_2023` | Ruta 250/1000 m | 579 | `Sysselsatt, EjSsyssels, Totalt` | population 20–64 by employment status (employed / not employed) |
| `Tab9_DeSO_2023` | DeSO | 73 | `DeSO_2025, Sysselsatt, EjSsyssels, Totalt` | as Tab9 Ruta |
| `Tab10_Ruta_2024` | Ruta 250/1000 m | 572 | `Forgymn, Gymnasial, Eftergymn2, Eftergymn3, UppgSakn, Totbef` | education level, ages 25–64 (same table as in the population delivery) |

### Swedish → English field aliases

| Swedish (as delivered) | English alias | Source of the meaning |
|---|---|---|
| `Näringsli` / `Naringsliv` | `sector_private` (business sector, *Näringslivet*) | PDF (A2/A4) |
| `Staten` | `sector_state` (central government) | inferred |
| `Kommun` | `sector_municipal` | inferred |
| `Region` | `sector_region` (regions, former county councils) | inferred |
| `Ovrigt` / `Övrigt` | `sector_other` (contents not stated) | inferred |
| `Sysselsatt` | `employed` (*sysselsatta*) | inferred (PDF A7 calls it `Forvarb`, *förvärvsarbetande*) |
| `EjSsyssels` | `not_employed` (*ej sysselsatta*; doubled "s" as in the source) | inferred (PDF A7: `EjForvarb`) |
| `AstRutID_S` / `AstRutstor` | `cell_id` / `cell_size_m` of the workplace cell | inferred / PDF |
| `astDeSO_20` | `deso_code` of the workplace; the name is cut at 10 characters, so the DeSO edition is unknown | inferred |

The aliases match the older `cell_attributes` pipeline (`Src/pipelines/cell_attributes/config.py`).

**The variable PDF is older than the data** (RAMS 2017; same file as in the population and income deliveries). It lists only two sectors, `Offentliga` (public) and `Naringsliv` (business). The delivered tables split the public sector into `Staten` / `Kommun` / `Region` and add `Ovrigt`. Those meanings come from the Swedish words, not from SCB. The age ranges (16+ for Tab7/8, 20–64 for Tab9) also come from the PDF's older tables A2, A4 and A7, and are not confirmed for this edition.

## Pipeline result

```bash
python Src/Scripts/run_employment_pipeline.py --input "Raw_data/1-Helsingborig/employment" --output "Processed_data/Helsingborg_Final"
```

6 tables, **4 issues, 0 unknown columns**. Output: `Processed_data/Helsingborg_Final/employment_2026-10-13/index.html`.

| Table | Cells / areas | Total | Parts ≠ total | Issues |
|---|---|---|---|---|
| Tab7 Ruta | 531 × 250 m + 38 × 1 km; 37 cut; 8 coarse cells overlap 250 m cells | 57,748 employed | 439 of 569 rows (max 12) | cp1252 fallback |
| Tab8 Ruta | 640 × 250 m + 34 × 1 km; 28 cut; 10 overlap | 67,691 jobs | 170 of 674 (max 6) | cp1252 fallback |
| Tab8 DeSO | 73 | 70,266 jobs | 61 of 73 (max 7) | 1 null geometry |
| Tab9 Ruta | 540 × 250 m + 39 × 1 km; 37 cut; 9 overlap | 69,814 persons 20–64 | 411 of 579 (max 7) | — |
| Tab9 DeSO | 73 | 76,275 persons 20–64 | 58 of 73 (max 6) | 1 null geometry |
| Tab10 Ruta | 537 × 250 m + 35 × 1 km; 35 cut; 7 overlap | 63,746 persons 25–64 | 462 of 572 (max 10) | — |

No malformed or repeated IDs and no off-lattice cells. All Ruta IDs (including `AstRutID_S`) are 13-digit SW corners.

## Findings that affect later processing

1. **Two different populations: residents and workers.** Tab7 counts employed people at their **home** cell; Tab8 counts them at their **workplace** cell. The two must never be added or mixed. Their ratio (jobs per employed resident) is a meaningful derived value, but only on the same unit.
2. **Workplace cells cover areas with no residents.** Using a point inside each of the 43,981 buildings:

   | Table | In a 250 m cell | Only in a 1 km cell | In no cell |
   |---|---|---|---|
   | Tab7 (residence) | 39,039 | 681 | 4,261 |
   | Tab8 (workplace) | 39,992 | 644 | **3,345** |

   Tab8 also has 37 cells with `Totalt` = 0, where workers are recorded as zero.
3. **Sector field names are spelled differently across tables:**
   - `Näringsli` (Ruta) vs `Naringsliv` (DeSO);
   - `Ovrigt` (Tab7, Tab8 DeSO) vs `Övrigt` (Tab8 Ruta).

   Each pair shares one alias. Merge them through the field dictionary, never by string replacement.
4. **Encoding:** Tab7 and Tab8 Ruta declare UTF-8 in the `.cpg`, but their field names are cp1252 (the same trap as population Tab3). The loader falls back to cp1252.
5. **Tab10 is a superset of the population delivery's Tab10.** All 483 cells in both are identical in every column. The employment copy adds 89 cells because its extent is larger. Use one copy, not both.
6. **DeSO coverage:** 73 areas, the same codes in Tab8 and Tab9. Three are not in the population delivery: `1283A0030`, `1283C1690`, `1283C1700`. `1283A0010` has null geometry, as in population, because the box cut it away. DeSO values describe the whole area.
7. **Employment rate per DeSO** (`Sysselsatt / Totalt`): 0.505–0.912, median 0.798.
8. **Coverage differs by table.** 743 grid keys appear in at least one table; 494 appear in every table. A missing cell means **no data**, not 0.

## Open questions for the user

1. Which side to show: residence (Tab7), workplace (Tab8), or both as separate layers?
2. Should the larger extent of this delivery be kept, or cut to the population/income rectangle so that layers line up?
3. Derived values: employment rate (`Sysselsatt / Totalt`), sector shares, jobs per employed resident?
