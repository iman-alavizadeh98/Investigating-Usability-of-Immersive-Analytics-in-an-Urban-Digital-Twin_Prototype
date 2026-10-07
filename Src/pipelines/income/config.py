"""
Income (SCB *Inkomster*) dataset definition: table catalog and field dictionary.

*Inkomster — Statistik på ruta och DeSO från SCB* (Income — grid-square and DeSO
statistics, Statistics Sweden), delivered through SLU GET as shapefiles in
EPSG:3006. Licence FUK: not publishable, never commit outputs.

The delivery's variable PDF (`metadata/Beskrivning_av_variabler.pdf`, same file
as in the population delivery) describes an OLDER version of this table (IH1,
income year 2017): fixed SEK bands `Lag/MLag/Mhog/Hog`. The delivered table has
quartiles `Kvartil1..4` instead, and the DeSO table adds a median. So:
- `Totalt` and `Tot_CDISP0` are described in the PDF ("pdf");
- the quartile and median meanings are "inferred". In particular, where the
  quartile boundaries come from (national or local) is NOT stated in the
  delivery.

"Ekonomisk standard" (economic standard) = disposable income per consumption
unit (disponibel inkomst per konsumtionsenhet), which adjusts household income
for household size and composition (PDF).
"""

from __future__ import annotations

from pipelines.scb.config import FieldInfo, ScbDataset, TableSpec

H = "households"

TABLES = {
    "11": TableSpec(
        "Hushåll 20+ år efter ekonomisk standard", "Households (20+) by economic standard",
        "households with members aged 20+ (PDF: Totalt antal hushåll 20+ år); "
        "economic standard = disposable income per consumption unit",
        total_fields=("Totalt",), pdf_code="IH1",
    ),
}

FIELDS = {
    "Kvartil1": FieldInfo(
        "households_income_q1", "Households in the lowest quartile of economic standard; quartile boundaries not stated",
        "inferred", "count", H,
    ),
    "Kvartil2": FieldInfo("households_income_q2", "Households in the second quartile of economic standard", "inferred", "count", H),
    "Kvartil3": FieldInfo("households_income_q3", "Households in the third quartile of economic standard", "inferred", "count", H),
    "Kvartil4": FieldInfo("households_income_q4", "Households in the highest quartile of economic standard", "inferred", "count", H),
    "Tot_CDISP0": FieldInfo(
        "sum_economic_standard_sek",
        "Sum of disposable income per consumption unit over the households "
        "(Summa av disponibel inkomst per konsumtionsenhet för hushåll); divide by Totalt for a mean",
        "pdf", "amount", "SEK",
    ),
    "MedianInk": FieldInfo(
        "median_economic_standard_sek",
        "Median income; presumably the median economic standard of the area's households (not confirmed). "
        "A median: never sum or average it across areas",
        "inferred", "median", "SEK",
    ),
    "Lag": FieldInfo("households_low", "Older table edition: households with economic standard <= 167,400 SEK", "pdf", "count", H),
    "MLag": FieldInfo("households_mid_low", "Older table edition: 167,401-241,464 SEK", "pdf", "count", H),
    "Mhog": FieldInfo("households_mid_high", "Older table edition: 241,465-333,192 SEK", "pdf", "count", H),
    "Hog": FieldInfo("households_high", "Older table edition: 333,193 SEK and above", "pdf", "count", H),
}

INCOME = ScbDataset(
    dataset_id="income",
    metadata={
        "name_sv": "Inkomster - Statistik på ruta och DeSO från SCB",
        "name_en": "Income - grid-square and DeSO statistics from Statistics Sweden",
        "authority": "SCB (Statistiska centralbyrån / Statistics Sweden), delivered through SLU GET",
        "license": "FUK (Forskning, utbildning och kulturverksamhet): not publishable, never commit outputs",
        "format": "ESRI Shapefile",
    },
    tables=TABLES,
    fields=FIELDS,
)
