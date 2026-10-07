"""
Population (SCB *Befolkning*) dataset definition: table catalog and field dictionary.

*Befolkning — Statistik på ruta och DeSO från SCB* (Population — grid-square and
DeSO statistics, Statistics Sweden), delivered through SLU GET as shapefiles in
EPSG:3006. Licence FUK: the data and anything that shows it (including the HTML
views) must not be published or committed.

Shared SCB rules (file names, ID columns, encoding fallback, identity fields)
are in `pipelines.scb.config`. Field meanings record their source: "pdf" =
stated in the delivery's older variable description, "inferred" = read from the
abbreviation (see that module's docstring).

Population-specific naming notes:
- Tab1 names are cut at 10 characters: `Alder_16_1` = 16-19, `Alder_20_2` = 20-24,
  `Alder_25_4` = 25-44, `Alder_45_6` = 45-64.
- Tab3's field names are cp1252 behind a UTF-8 `.cpg` (`Änka_Änk`).
- `Totbef` (Tab10) is the 25-64 population, not the same as `Totalt`.
"""

from __future__ import annotations

from pipelines.scb.config import FieldInfo, ScbDataset, TableSpec

P = "persons"

TABLES = {
    "1": TableSpec(
        "Befolkning efter ålder", "Population by age group",
        "registered population; age reached at the end of the year", pdf_code="B1",
    ),
    "2": TableSpec("Befolkning efter kön", "Population by sex", "registered population", pdf_code="B2"),
    "3": TableSpec(
        "Befolkning efter civilstånd", "Population by marital status", "registered population",
        total_fields=("Totalt", "TotCiv"), pdf_code="B3",
    ),
    "4": TableSpec(
        "Befolkning efter födelseland", "Population by country of birth", "registered population",
        total_fields=("Totalt", "TotFland"), pdf_code="B5",
    ),
    "5": TableSpec(
        "Befolkningsförändringar", "Population change (moves, migration, births, deaths)",
        "events during the year; Tot_Bef is the population stock",
        total_fields=("Tot_Bef",), parts_sum_to_total=False, source="inferred",
    ),
    "6": TableSpec(
        "Totalbefolkning på 100 m ruta", "Total population on 100 m cells", "registered population",
        total_fields=("Totalt", "Totbef"), pdf_code="B13",
    ),
    "10": TableSpec(
        "Befolkning 25-64 år efter utbildningsnivå", "Population aged 25-64 by education level",
        "registered population aged 25-64; highest education (SUN)",
        total_fields=("Totbef",), pdf_code="A9",
    ),
}

FIELDS = {
    # Totals
    "Totbef": FieldInfo("total_population", "Total persons; in Tab10 the population aged 25-64", "pdf", "total", P),
    "Tot_Bef": FieldInfo("total_population", "Total population (stock), in the population-change table", "inferred", "total", P),
    "TotCiv": FieldInfo("total", "Total (older name, Tab3)", "pdf", "total", P),
    "TotFland": FieldInfo("total", "Total (older name, Tab4)", "pdf", "total", P),
    # Tab1 - age (names cut at 10 characters)
    "Alder_0_6": FieldInfo("age_0_6", "Age 0-6 years (Ålder 0-6 år)", unit=P),
    "Alder_7_15": FieldInfo("age_7_15", "Age 7-15 years", unit=P),
    "Alder_16_1": FieldInfo("age_16_19", "Age 16-19 years (name cut from Alder_16_19)", unit=P),
    "Alder_20_2": FieldInfo("age_20_24", "Age 20-24 years (name cut from Alder_20_24)", unit=P),
    "Alder_25_4": FieldInfo("age_25_44", "Age 25-44 years (name cut from Alder_25_44)", unit=P),
    "Alder_45_6": FieldInfo("age_45_64", "Age 45-64 years (name cut from Alder_45_64)", unit=P),
    "Alder_65": FieldInfo("age_65_plus", "Age 65 years and over", unit=P),
    # Tab2 - sex
    "Man": FieldInfo("men", "Men (Män)", unit=P),
    "Kvinnor": FieldInfo("women", "Women (Kvinnor)", unit=P),
    # Tab3 - marital status
    "Ogifta": FieldInfo("unmarried", "Unmarried (Ogifta)", unit=P),
    "Gifta": FieldInfo("married", "Married (Gifta)", unit=P),
    "Skilda": FieldInfo("divorced", "Divorced (Skilda)", unit=P),
    "Änka_Änk": FieldInfo("widowed", "Widows and widowers (Änkor/änklingar)", unit=P),
    "Änka_Änkli": FieldInfo("widowed", "Widows and widowers (older name)", unit=P),
    # Tab4 - country of birth
    "Sverige": FieldInfo("born_sweden", "Born in Sweden", unit=P),
    "Norden_uto": FieldInfo("born_nordic_excl_sweden", "Born in the Nordic countries except Sweden (Norden utom Sverige)", unit=P),
    "EU_utom_No": FieldInfo(
        "born_eu_excl_nordic", "Born in the EU except the Nordic countries (the older PDF says EU28)", "inferred", unit=P,
    ),
    "Ovriga_var": FieldInfo(
        "born_rest_of_world", "Born in the rest of the world, including unknown (Övriga världen inkl. uppgift saknas)", unit=P,
    ),
    # Tab5 - population change (not in the PDF)
    "F_Inom": FieldInfo("moves_within", "Moves within the area (flyttningar inom); the area meant is not confirmed", "inferred", unit=P),
    "F_Till": FieldInfo("moves_in", "Moves into the area (flyttningar till)", "inferred", unit=P),
    "F_Fran": FieldInfo("moves_out", "Moves out of the area (flyttningar från)", "inferred", unit=P),
    "Inv": FieldInfo("immigrated", "Immigrated (invandrade)", "inferred", unit=P),
    "Utv": FieldInfo("emigrated", "Emigrated (utvandrade)", "inferred", unit=P),
    "Fodda": FieldInfo("births", "Births (födda)", "inferred", unit=P),
    "Doda": FieldInfo("deaths", "Deaths (döda)", "inferred", unit=P),
    # Tab10 - education, ages 25-64
    "Forgymn": FieldInfo("edu_pre_upper_secondary", "Pre-upper-secondary education (Förgymnasial)", unit=P),
    "Gymnasial": FieldInfo("edu_upper_secondary", "Upper-secondary education (Gymnasial)", unit=P),
    "Eftergymn2": FieldInfo("edu_post_secondary_lt_3y", "Post-secondary education, less than 3 years", unit=P),
    "Eftergymn3": FieldInfo("edu_post_secondary_3y_plus", "Post-secondary education, 3 years or more, incl. doctoral studies", unit=P),
    "UppgSakn": FieldInfo("edu_unknown", "Education level unknown (Uppgift saknas)", unit=P),
}

POPULATION = ScbDataset(
    dataset_id="population",
    metadata={
        "name_sv": "Befolkning - Statistik på ruta och DeSO från SCB",
        "name_en": "Population - grid-square and DeSO statistics from Statistics Sweden",
        "authority": "SCB (Statistiska centralbyrån / Statistics Sweden), delivered through SLU GET",
        "license": "FUK (Forskning, utbildning och kulturverksamhet): not publishable, never commit outputs",
        "format": "ESRI Shapefile",
    },
    tables=TABLES,
    fields=FIELDS,
)
