"""
Employment (SCB *Arbetsmarknad*) dataset definition: table catalog and field dictionary.

*Arbetsmarknad — Statistik på ruta och DeSO från SCB* (Labour market — grid-square
and DeSO statistics, Statistics Sweden), delivered through SLU GET as shapefiles
in EPSG:3006 (`metadata/_readMe_ArbetsmarknadUtbildning.json`: "Arbetsmarknad -
Utbildning"). Licence FUK: not publishable, never commit outputs.

The delivery's variable PDF (`metadata/Beskrivning_av_variabler.pdf`, the same
file as in the population and income deliveries) describes OLDER editions of
these tables (RAMS 2017):
- Tab7 ~ A2 *Förvärvsarbetande nattbefolkning (16+ år) efter sektor*: employed
  persons counted where they LIVE;
- Tab8 ~ A4 *Förvärvsarbetande dagbefolkning (16+ år) efter sektor*: employed
  persons counted where they WORK (arbetsställe, hence the `Ast`/`ast` ID columns);
- Tab9 ~ A7 *Befolkning 20-64 år efter sysselsättning*: employed / not employed;
- Tab10 = A9 education 25-64, also part of the population delivery (same
  definition as `pipelines.population`).

The PDF splits sector into `Offentliga` (public) / `Naringsliv` (business) only.
The delivered tables split the public sector into `Staten` / `Kommun` / `Region`
and add `Ovrigt`; those meanings are "inferred" from the Swedish words. Tab9's
`Sysselsatt` / `EjSsyssels` replace the PDF's `Forvarb` / `EjForvarb`; that they
mean the same, and the 20-64 age range, are inferred from A7.

Spelling variants of the same field (CLAUDE.md §7): `Näringsli` (Ruta, name cut
at 10 characters, cp1252 behind a UTF-8 `.cpg`) vs `Naringsliv` (DeSO), and
`Ovrigt` (Tab7, Tab8 DeSO) vs `Övrigt` (Tab8 Ruta). They share one English alias.
"""

from __future__ import annotations

from pipelines.population.config import FIELDS as POPULATION_FIELDS
from pipelines.population.config import TABLES as POPULATION_TABLES
from pipelines.scb.config import FieldInfo, ScbDataset, TableSpec

P = "persons"

_SECTOR_NOTE = "the PDF's older edition only splits public sector (Offentliga) from business (Naringsliv)"

TABLES = {
    "7": TableSpec(
        "Förvärvsarbetande nattbefolkning (16+ år) efter sektor",
        "Employed persons (16+) by sector, at place of residence",
        "employed persons aged 16+ counted where they live (night-time population); age from the PDF's A2",
        pdf_code="A2", source="inferred",
    ),
    "8": TableSpec(
        "Förvärvsarbetande dagbefolkning (16+ år) efter sektor",
        "Employed persons (16+) by sector, at place of work",
        "employed persons aged 16+ counted at their workplace (daytime population); age from the PDF's A4",
        pdf_code="A4", source="inferred",
    ),
    "9": TableSpec(
        "Befolkning 20-64 år efter sysselsättning",
        "Population aged 20-64 by employment status",
        "registered population aged 20-64 (age range from the PDF's A7, not confirmed for this edition)",
        pdf_code="A7", source="inferred",
    ),
    "10": POPULATION_TABLES["10"],
}

_EDUCATION_FIELDS = ("Totbef", "Forgymn", "Gymnasial", "Eftergymn2", "Eftergymn3", "UppgSakn")

FIELDS = {
    # Tab7 / Tab8 - sector
    "Näringsli": FieldInfo(
        "sector_private", "Business sector (Näringslivet); name cut from Näringsliv", "pdf", "count", P,
    ),
    "Naringsliv": FieldInfo("sector_private", "Business sector (Näringslivet)", "pdf", "count", P),
    "Staten": FieldInfo("sector_state", f"Central government (Staten); {_SECTOR_NOTE}", "inferred", "count", P),
    "Kommun": FieldInfo("sector_municipal", f"Municipalities (Kommun); {_SECTOR_NOTE}", "inferred", "count", P),
    "Region": FieldInfo(
        "sector_region", f"Regions, formerly county councils (Region); {_SECTOR_NOTE}", "inferred", "count", P,
    ),
    "Ovrigt": FieldInfo(
        "sector_other", "Other sector (Övrigt); what it contains is not stated in the delivery", "inferred", "count", P,
    ),
    "Övrigt": FieldInfo("sector_other", "Other sector (spelling with Ö, Tab8 Ruta)", "inferred", "count", P),
    # Tab9 - employment status
    "Sysselsatt": FieldInfo(
        "employed", "Employed (Sysselsatta); the PDF's A7 calls it Förvärvsarbetande (Forvarb)", "inferred", "count", P,
    ),
    "EjSsyssels": FieldInfo(
        "not_employed", "Not employed (Ej sysselsatta; 'Ss' as in the source); the PDF's A7: EjForvarb",
        "inferred", "count", P,
    ),
    # Tab10 - education, ages 25-64 (same table as in the population delivery)
    **{name: POPULATION_FIELDS[name] for name in _EDUCATION_FIELDS},
}

EMPLOYMENT = ScbDataset(
    dataset_id="employment",
    metadata={
        "name_sv": "Arbetsmarknad - Statistik på ruta och DeSO från SCB",
        "name_en": "Labour market - grid-square and DeSO statistics from Statistics Sweden",
        "authority": "SCB (Statistiska centralbyrån / Statistics Sweden), delivered through SLU GET",
        "license": "FUK (Forskning, utbildning och kulturverksamhet): not publishable, never commit outputs",
        "format": "ESRI Shapefile",
    },
    tables=TABLES,
    fields=FIELDS,
)
