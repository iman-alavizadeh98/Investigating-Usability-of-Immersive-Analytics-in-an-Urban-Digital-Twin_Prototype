"""
Configuration for the cell-attribute pipeline.

Declarative by design: adding a new statistical layer is a `CellLayerSpec` entry
in LAYER_REGISTRY, not new code. Population is only the first of several layers
(employment, education, income are already registered).

SOURCE
------
SCB (Statistics Sweden) "Statistik på rutor" / grid-square statistics, 2023,
delivered as ESRI Shapefiles in EPSG:3006 (SWEREF99 TM).

Swedish -> English aliases follow the convention in
Src/pipelines/buildings/config.py (module-level translation dict), per CLAUDE.md
§6: raw data keeps original names, processed data adds English aliases, and the
mapping is documented.

FIELD-NAMING HAZARDS (CLAUDE.md §7 -- source labels are NOT clean)
------------------------------------------------------------------
1. DBF truncates field names to 10 characters. `Alder_16_1` is really
   "Ålder 16-19" and `Alder_20_2` is "Ålder 20-24". The deprecated
   preprocess_spatial_joins.py copied these truncated names straight into output
   JSON keys, which is unreadable downstream. Aliased here.
2. `Änka_Ä` (Tab3, "widowed") reads as mojibake because the DBF encoding is not
   declared. Both the mojibake and the correct spelling are registered as keys.
3. The SAME concept is spelled differently across tables:
     Rutstorl (most) vs AstRutstor (Tab8)
     Näringsl (Tab7) vs Naringsliv (Tab8)
   Resolved through the alias table, never by ad-hoc string replacement.
4. `Totbef` (Tab10) is the population base, not the same field as `Totalt`.
5. `MedianInk` (Tab11) is a MEDIAN. Summing medians is meaningless, so it is
   registered as non-additive and excluded from aggregation.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional, Tuple

# ---------------------------------------------------------------------------
# Swedish -> English field aliases
# ---------------------------------------------------------------------------

RUTA_FIELD_TRANSLATIONS = {
    # Grid identity
    "Rutstorl": "cell_size_m",
    "AstRutstor": "cell_size_m",        # Tab8 spelling variant
    "Ruta": "cell_code",
    "Totalt": "total",
    "Totbef": "total_population_base",  # Tab10: population base, NOT `Totalt`

    # Tab1 - age. Names are DBF-truncated at 10 chars; real ranges in comments.
    "Alder_0_6": "age_0_6",
    "Alder_7_15": "age_7_15",
    "Alder_16_1": "age_16_19",          # truncated from Alder_16_19
    "Alder_20_2": "age_20_24",          # truncated from Alder_20_24
    "Alder_25_4": "age_25_44",          # truncated from Alder_25_44
    "Alder_45_6": "age_45_64",          # truncated from Alder_45_64
    "Alder_65": "age_65_plus",

    # Tab2 - sex
    "Man": "men",
    "Kvinnor": "women",

    # Tab3 - marital status
    "Ogifta": "unmarried",
    "Gifta": "married",
    "Skilda": "divorced",
    "Änka_Ä": "widowed",                # correct spelling
    "�nka_�": "widowed",      # mojibake as read from the DBF
    "Anka_A": "widowed",                # ASCII-folded variant, defensive

    # Tab4 - country of birth
    "Sverige": "born_sweden",
    "Norden_uto": "born_nordic_excl_sweden",
    "EU_utom_No": "born_eu_excl_nordic",
    "Ovriga_var": "born_other",

    # Tab7 / Tab8 - employment sector
    "Näringsl": "sector_private",
    "N�ringsl": "sector_private",  # mojibake variant
    "Naringsliv": "sector_private",     # Tab8 spelling
    "Staten": "sector_state",
    "Kommun": "sector_municipal",
    "Region": "sector_region",
    "Ovrigt": "sector_other",

    # Tab9 - employment status
    "Sysselsatt": "employed",
    "EjSsyssels": "not_employed",

    # Tab10 - education
    "Forgymn": "edu_pre_upper_secondary",
    "Gymnasial": "edu_upper_secondary",
    "Eftergymn2": "edu_tertiary_lt_3y",
    "Eftergymn3": "edu_tertiary_ge_3y",
    "UppgSakn": "edu_unknown",

    # Tab11 - income
    "Kvartil1": "income_q1",
    "Kvartil2": "income_q2",
    "Kvartil3": "income_q3",
    "Kvartil4": "income_q4",
    "MedianInk": "median_income",       # NON-ADDITIVE
}

#: Dataset names, Swedish and English, for documentation (CLAUDE.md §6).
DATASET_NAMES = {
    "sv": "Statistik på rutor (SCB)",
    "en": "Grid-square statistics (Statistics Sweden)",
    "authority": "SCB (Statistiska centralbyrån / Statistics Sweden)",
    "year": 2023,
    "crs": "EPSG:3006",
}

DEFAULT_SOURCE_ROOT = Path("Raw_data/0- Gothenburg")


@dataclass(frozen=True)
class CellLayerSpec:
    """
    One statistical source layer on the SCB Ruta lattice.

    Attributes:
        layer_id: Stable identifier used in outputs, e.g. "population_age".
        theme: Grouping for the UI / reports, e.g. "population".
        source_path: Shapefile path, relative to the repo root.
        count_fields: Fields that are COUNTS OF PEOPLE and may be summed when
            aggregating source cells into a target cell.
        non_additive_fields: Fields that must NOT be summed (medians, rates).
            Carried through only where a single source cell maps to a target cell.
        total_field: The layer's own total, used for conservation checks.
        expected_total: Measured total at time of writing; a mismatch means the
            source data changed and the reports need regenerating.
        size_field / code_field: Source column names for cell size and cell code.
    """

    layer_id: str
    theme: str
    source_path: Path
    count_fields: Tuple[str, ...]
    non_additive_fields: Tuple[str, ...] = ()
    total_field: Optional[str] = "Totalt"
    expected_total: Optional[int] = None
    size_field: str = "Rutstorl"
    code_field: str = "Ruta"
    description_sv: str = ""
    description_en: str = ""


def _pop(name: str) -> Path:
    return DEFAULT_SOURCE_ROOT / "befolkningShp" / name


def _work(name: str) -> Path:
    return DEFAULT_SOURCE_ROOT / "arbutbShp" / name


def _income(name: str) -> Path:
    return DEFAULT_SOURCE_ROOT / "inkomsterShp" / name


#: All Ruta layers verified to sit on the SCB lattice and nest into the 500 m
#: analytical grid. `expected_total` values were measured 2026-08-05.
LAYER_REGISTRY = {
    "population_age": CellLayerSpec(
        layer_id="population_age",
        theme="population",
        source_path=_pop("Tab1_Ruta_2023_region.shp"),
        count_fields=(
            "Alder_0_6", "Alder_7_15", "Alder_16_1", "Alder_20_2",
            "Alder_25_4", "Alder_45_6", "Alder_65", "Totalt",
        ),
        expected_total=717_781,
        description_sv="Befolkning efter ålder",
        description_en="Population by age group",
    ),
    "population_sex": CellLayerSpec(
        layer_id="population_sex",
        theme="population",
        source_path=_pop("Tab2_Ruta_2023_region.shp"),
        count_fields=("Man", "Kvinnor", "Totalt"),
        expected_total=717_781,
        description_sv="Befolkning efter kön",
        description_en="Population by sex",
    ),
    "population_marital": CellLayerSpec(
        layer_id="population_marital",
        theme="population",
        source_path=_pop("Tab3_Ruta_2023_region.shp"),
        # The widowed column name is mojibake in the source; resolved at load time.
        count_fields=("Ogifta", "Gifta", "Skilda", "Totalt"),
        expected_total=717_781,
        description_sv="Befolkning efter civilstånd",
        description_en="Population by marital status",
    ),
    "population_origin": CellLayerSpec(
        layer_id="population_origin",
        theme="population",
        source_path=_pop("Tab4_Ruta_2023_region.shp"),
        count_fields=("Sverige", "Norden_uto", "EU_utom_No", "Ovriga_var", "Totalt"),
        expected_total=717_781,
        description_sv="Befolkning efter födelseregion",
        description_en="Population by region of birth",
    ),
    "population_total_100m": CellLayerSpec(
        layer_id="population_total_100m",
        theme="population",
        source_path=_pop("Tab6_Ruta_2023_region.shp"),
        count_fields=("Totalt",),
        expected_total=717_284,
        description_sv="Befolkning totalt (100 m rutor)",
        description_en="Total population (100 m cells)",
    ),
    "employment_sector_night": CellLayerSpec(
        layer_id="employment_sector_night",
        theme="employment",
        source_path=_work("Tab7_Ruta_2023_region.shp"),
        # Näringsl is mojibake in the source; resolved at load time.
        count_fields=("Staten", "Kommun", "Region", "Ovrigt", "Totalt"),
        expected_total=375_675,
        description_sv="Förvärvsarbetande efter sektor (nattbefolkning)",
        description_en="Employed persons by sector (residence-based)",
    ),
    "employment_sector_day": CellLayerSpec(
        layer_id="employment_sector_day",
        theme="employment",
        source_path=_work("Tab8_Ruta_2023_region.shp"),
        count_fields=("Naringsliv", "Staten", "Kommun", "Region", "Ovrigt", "Totalt"),
        expected_total=425_783,
        size_field="AstRutstor",   # spelling variant, CLAUDE.md §7
        description_sv="Sysselsatta efter sektor (dagbefolkning)",
        description_en="Employed persons by sector (workplace-based)",
    ),
    "employment_status": CellLayerSpec(
        layer_id="employment_status",
        theme="employment",
        source_path=_work("Tab9_Ruta_2023_region.shp"),
        count_fields=("Sysselsatt", "EjSsyssels", "Totalt"),
        expected_total=437_103,
        description_sv="Befolkning efter sysselsättning",
        description_en="Population by employment status",
    ),
    "education_level": CellLayerSpec(
        layer_id="education_level",
        theme="education",
        source_path=_work("Tab10_Ruta_2023_region.shp"),
        count_fields=(
            "Forgymn", "Gymnasial", "Eftergymn2", "Eftergymn3", "UppgSakn", "Totbef",
        ),
        total_field="Totbef",
        expected_total=395_721,
        description_sv="Befolkning efter utbildningsnivå",
        description_en="Population by education level",
    ),
    "income_quartiles": CellLayerSpec(
        layer_id="income_quartiles",
        theme="income",
        source_path=_income("Tab11_Ruta_2023_region.shp"),
        count_fields=("Kvartil1", "Kvartil2", "Kvartil3", "Kvartil4", "Totalt"),
        # A median cannot be summed across cells. Excluded from aggregation.
        non_additive_fields=("MedianInk",),
        expected_total=326_496,
        description_sv="Inkomst efter kvartil",
        description_en="Income by quartile",
    ),
}

#: Layers loaded when no explicit selection is given.
DEFAULT_LAYERS = ("population_age",)


@dataclass
class CellAttributeConfig:
    """Run configuration for the cell-attribute pipeline."""

    layers: Tuple[str, ...] = DEFAULT_LAYERS
    cell_size_m: int = 500
    output_dir: Path = Path("Processed_data/analytics/cell_attributes_500m")
    source_root: Path = DEFAULT_SOURCE_ROOT

    #: How overlapping source resolutions are reconciled. See aggregate.py.
    reconciliation_rule: str = "fine_priority_disjoint_coarse"

    #: Fail the run if a layer's source total does not match `expected_total`.
    enforce_expected_totals: bool = True

    export_formats: Tuple[str, ...] = ("gpkg", "parquet", "json")


def resolve_layers(names) -> list:
    """Return CellLayerSpecs for the given ids, or raise with the valid options."""
    specs = []
    for name in names:
        if name not in LAYER_REGISTRY:
            raise KeyError(
                f"Unknown layer '{name}'. Available: {', '.join(sorted(LAYER_REGISTRY))}"
            )
        specs.append(LAYER_REGISTRY[name])
    return specs


def translate_columns(columns) -> dict:
    """Map source column names to English aliases, leaving unknown names as-is."""
    return {c: RUTA_FIELD_TRANSLATIONS.get(c, c) for c in columns}
