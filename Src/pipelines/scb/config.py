"""
Shared configuration for SCB grid-square / DeSO deliveries (*Statistik på ruta
och DeSO från SCB*).

SCB delivers population, income, employment, ... as the same kind of shapefile
tables. What is common to all of them lives here: the file-name pattern, how to
find the ID and cell-size columns, the encoding fallback, and the identity
fields. Each dataset (`pipelines.population`, `pipelines.income`) adds its own
table catalog and field dictionary as an `ScbDataset`.

City-independent: nothing here names a city, a folder or a data year.

NAMING HAZARDS SEEN IN THE DATA (CLAUDE.md §7)
----------------------------------------------
- DBF field names are cut at 10 characters (`Alder_16_1` = age 16-19).
- A `.cpg` may declare UTF-8 while the field names are cp1252 (`Änka_Änk` in
  population Tab3), so the declared encoding fails; see ENCODING_FALLBACKS.
- The Ruta ID column is `RutID_SW` in most tables but `Ruta` in some; the DeSO ID
  column carries the DeSO edition year (`DeSO_2025`). Both are matched by rule.
- The variable PDF in the deliveries is older than the data, so field meanings
  record where they come from: "pdf" (stated there, possibly under an older
  column name) or "inferred" (read from the abbreviation, not confirmed by SCB).
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Dict, Optional, Tuple

# ---------------------------------------------------------------------------
# File discovery
# ---------------------------------------------------------------------------

#: `Tab1_Ruta_2024`, `Tab6_Ruta_2024_region`, `Tab11_DeSO_2023`
FILE_NAME_PATTERN = re.compile(
    r"^Tab(?P<number>\d+)_(?P<unit>[A-Za-z]+)_(?P<year>\d{4})(?:_(?P<suffix>[A-Za-z0-9]+))?$"
)

#: Encodings tried, in order, when the declared one (the `.cpg`) cannot decode
#: the file. cp1252 is what SCB/ArcGIS writes for Swedish field names.
ENCODING_FALLBACKS: Tuple[str, ...] = ("cp1252",)

EXPECTED_CRS = "EPSG:3006"

# ---------------------------------------------------------------------------
# Spatial units
# ---------------------------------------------------------------------------

#: Unit name as it appears in the file name → rules to find its columns.
#: Ruta = SCB grid square; DeSO = demografiska statistikområden (demographic
#: statistical areas).
UNIT_RULES = {
    "Ruta": {
        "id_columns": ("RutID_SW", "Ruta"),
        "size_columns": ("Rutstorl", "AstRutstor", "astRutstor"),
        # 6-digit easting + 7-digit northing of the cell's south-west corner.
        "id_value_pattern": r"^\d{13}$",
    },
    "DeSO": {
        "id_column_pattern": r"^DeSO(_\d{4})?$",
        "size_columns": (),
        # County+municipality (4 digits), area type A/B/C, 4-digit number: 1283C1400
        "id_value_pattern": r"^\d{4}[ABC]\d{4}$",
    },
}

#: Length of the easting part of a Ruta ID.
RUTA_EASTING_DIGITS = 6

#: A Ruta geometry whose area differs from size² by more than this fraction is
#: reported as "not a full square" (e.g. cut at the delivery boundary).
FULL_SQUARE_TOLERANCE = 0.01

# ---------------------------------------------------------------------------
# Tables and fields
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class TableSpec:
    """What one SCB table number means. Units and years come from the file."""

    title_sv: str
    title_en: str
    universe_en: str
    #: Candidates for the table's total column; the first one present is used.
    total_fields: Tuple[str, ...] = ("Totalt",)
    #: True when the `count` fields are a breakdown of the total. SCB perturbs
    #: small counts, so a breakdown may still differ from the total.
    parts_sum_to_total: bool = True
    #: Code of the matching table in the older PDF, when there is one.
    pdf_code: Optional[str] = None
    source: str = "pdf"


#: Field roles. Only `count` fields are checked against the total; `amount`
#: (a sum, e.g. SEK) and `median` are values that the total does not contain.
#: A median must never be summed across units.
ROLES = ("id", "size", "count", "total", "amount", "median")


@dataclass(frozen=True)
class FieldInfo:
    alias_en: str
    description_en: str
    source: str = "pdf"   # "pdf" or "inferred", see the module docstring
    role: str = "count"   # one of ROLES
    unit: str = ""        # "persons", "households", "SEK", "m", ...


#: Identity and shared total fields, used by every SCB dataset.
COMMON_FIELDS: Dict[str, FieldInfo] = {
    "RutID_SW": FieldInfo(
        "cell_id", "Ruta cell ID: easting (6 digits) + northing (7 digits) of the south-west corner, EPSG:3006",
        "inferred", "id",
    ),
    "Ruta": FieldInfo("cell_id", "Ruta cell ID (Rutidentitet), same format as RutID_SW", "pdf", "id"),
    "Rutstorl": FieldInfo("cell_size_m", "Cell side length (Rutstorlek)", "pdf", "size", "m"),
    "AstRutstor": FieldInfo("cell_size_m", "Cell side length (spelling variant)", "pdf", "size", "m"),
    "astRutstor": FieldInfo("cell_size_m", "Cell side length (spelling variant)", "pdf", "size", "m"),
    "Totalt": FieldInfo("total", "Total of the table's universe (see the table)", "pdf", "total"),
}

_DESO_ID = re.compile(UNIT_RULES["DeSO"]["id_column_pattern"])


@dataclass(frozen=True)
class ScbDataset:
    """One SCB delivery type: its id, metadata, table catalog and field dictionary."""

    dataset_id: str                     # "population" → population_<date>/, population_preprocess_report.json
    metadata: Dict[str, str]            # name_sv, name_en, authority, license, format
    tables: Dict[str, TableSpec]
    fields: Dict[str, FieldInfo] = field(default_factory=dict)  # merged over COMMON_FIELDS

    def table(self, number: Optional[str]) -> Optional[TableSpec]:
        return self.tables.get(number or "")

    def describe_field(self, name: str) -> Optional[FieldInfo]:
        """FieldInfo for a source column name, or None if it is unknown."""
        if name in self.fields:
            return self.fields[name]
        if name in COMMON_FIELDS:
            return COMMON_FIELDS[name]
        if _DESO_ID.match(name):
            year = name.split("_", 1)[1] if "_" in name else "unknown"
            return FieldInfo(
                "deso_code", f"DeSO area code; the column name gives the DeSO edition ({year})", "inferred", "id",
            )
        return None
