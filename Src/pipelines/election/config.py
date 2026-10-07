"""
Configuration for the election preprocess pipeline.

Two inputs, both published for the whole country or county:
  - results workbook (.xlsx): *Röster per distrikt, slutligt antal röster* —
    final vote counts per voting district (valdistrikt) from the county
    administrative boards' final count (länsstyrelsernas slutliga sammanräkning);
  - voting-district boundaries (.geojson / .gpkg / .shp): *Valdistrikt*.

City-independent: the municipality is chosen with its 4-digit code (Kommunkod)
on the command line; the election type and year are read from the results file
name. Nothing here names a municipality.

Assumptions and traps (CLAUDE.md §5, §7)
-----------------------------------------
- Codes are text with leading zeros (`01140101`); they must never be read as
  numbers. pandas' Excel reader converts number-like text to numbers by default
  ("0114" → 114, verified 2026-10-07), so the loader reads CODE_COLUMNS as text
  and warns if one still arrives as a number.
- The long sheet (`roster_KF`) mixes party rows with summary rows (valid votes,
  invalid votes, votes cast, eligible voters), so summing `Röster` over all
  rows double-counts. SUMMARY_CATEGORIES lists the summary rows; every other
  category is treated as a party (the set of local parties is open).
- `Uppsamlingsdistrikt` (collection district): votes counted centrally (e.g.
  late postal votes). It has votes but no area and 0 eligible voters, and is
  absent from the boundary file.
- `Valdeltagande (%)` holds a fraction 0-1, not a percentage.
- The wide sheets (`Antal_*`, `Andel_*`) have one column per party in the
  whole country (300+); the loader drops the party columns that are empty or
  zero for the chosen municipality and records how many.
- The licence of the files is not stated in them; Swedish election results are
  public, but check Valmyndigheten's terms before publishing anything.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Dict, Tuple

DATASET_METADATA = {
    "name_sv": "Val - röster per valdistrikt och valdistriktsindelning",
    "name_en": "Election - votes per voting district, and voting-district boundaries",
    "authority": "Valmyndigheten (Swedish Election Authority); counts from the county administrative "
                 "boards' final count (assumed from the file names and the workbook's Information sheet)",
    "license": "Public election data; licence not stated in the files - check before publishing",
    "format": "XLSX (results) + GeoJSON/GPKG/SHP (district boundaries)",
}

EXPECTED_CRS = "EPSG:3006"

#: Results file → election type and year, e.g. "...-kommunvalen-2026.xlsx".
ELECTION_FILE_PATTERN = re.compile(r"(?P<election>riksdagsval|regionval|kommunval)\w*?-(?P<year>\d{4})", re.IGNORECASE)

ELECTION_TYPES_EN = {
    "riksdagsval": "Parliamentary election (Riksdag)",
    "regionval": "Regional council election (regionfullmäktige)",
    "kommunval": "Municipal council election (kommunfullmäktige)",
}

RESULTS_SUFFIXES: Tuple[str, ...] = (".xlsx",)
BOUNDARY_SUFFIXES: Tuple[str, ...] = (".geojson", ".gpkg", ".shp")

MUNICIPALITY_CODE_PATTERN = r"^\d{4}$"
#: Real voting districts: municipality (4) + district (4). Collection districts
#: carry the 6-digit constituency code instead.
DISTRICT_CODE_PATTERN = r"^\d{8}$"
COLLECTION_DISTRICT_PREFIX = "Uppsamlingsdistrikt"

#: Columns that hold codes and must stay text.
CODE_COLUMNS: Tuple[str, ...] = (
    "Kommunkod", "Valkretskod", "Valdistriktskod", "Länskod", "Kommunvalkretskod",
    "Riksdagsvalkretskod", "Regionkod", "Regionvalkretskod",
)

MUNICIPALITY_COLUMN = "Kommunkod"
DISTRICT_CODE_COLUMN = "Valdistriktskod"
DISTRICT_NAME_COLUMN = "Valdistrikt"

# ---------------------------------------------------------------------------
# Workbook sheets
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class SheetSpec:
    description_en: str
    level: str          # "info", "district", "constituency", "municipality"
    kind: str           # "info", "long", "counts_wide", "shares_wide", "turnout"


SHEETS: Dict[str, SheetSpec] = {
    "Information": SheetSpec("Description of the workbook and its sheets (Swedish)", "info", "info"),
    "roster_KF": SheetSpec(
        "Votes per voting district and party or summary category, one row per pair (long format)", "district", "long",
    ),
    "Antal_kommun": SheetSpec("Votes per municipality, one column per party", "municipality", "counts_wide"),
    "Antal_valkrets": SheetSpec("Votes per municipal constituency, one column per party", "constituency", "counts_wide"),
    "Antal_distrikt": SheetSpec("Votes per voting district, one column per party", "district", "counts_wide"),
    "Andel_kommun": SheetSpec("Share of valid votes per municipality (0-1), one column per party", "municipality", "shares_wide"),
    "Andel_valkrets": SheetSpec("Share of valid votes per constituency (0-1), one column per party", "constituency", "shares_wide"),
    "Andel_distrikt": SheetSpec("Share of valid votes per voting district (0-1), one column per party", "district", "shares_wide"),
    "Valdeltagande_kommun": SheetSpec("Turnout per municipality", "municipality", "turnout"),
    "Valdeltagande_valkrets": SheetSpec("Turnout per municipal constituency", "constituency", "turnout"),
    "Valdeltagande_distrikt": SheetSpec("Turnout per voting district", "district", "turnout"),
}

#: Long-format categories that are NOT parties → English alias.
SUMMARY_CATEGORIES: Dict[str, str] = {
    "Summa giltiga röster": "valid_votes",
    "Ogiltiga röster - ej anmälda partier": "invalid_votes_unregistered_party",
    "Ogiltiga röster - blanka": "invalid_votes_blank",
    "Ogiltiga röster - övriga": "invalid_votes_other",
    "Valdeltagande": "votes_cast",
    "Röstberättigade": "eligible_voters",
}
INVALID_PREFIX = "Ogiltiga röster"

#: The eight parties in the Riksdag (2022-2026): Swedish name → (abbreviation, English).
#: Local and small parties keep their Swedish name only.
NATIONAL_PARTIES: Dict[str, Tuple[str, str]] = {
    "Arbetarepartiet-Socialdemokraterna": ("S", "Social Democrats"),
    "Moderaterna": ("M", "Moderate Party"),
    "Sverigedemokraterna": ("SD", "Sweden Democrats"),
    "Vänsterpartiet": ("V", "Left Party"),
    "Centerpartiet": ("C", "Centre Party"),
    "Miljöpartiet de gröna": ("MP", "Green Party"),
    "Kristdemokraterna": ("KD", "Christian Democrats"),
    "Liberalerna (tidigare Folkpartiet)": ("L", "Liberals"),
}

# ---------------------------------------------------------------------------
# Fields (non-party columns): original Swedish → English
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class FieldInfo:
    alias_en: str
    description_en: str
    unit: str = ""


FIELDS: Dict[str, FieldInfo] = {
    "Valtyp": FieldInfo("election_type", "Election type (Kommun = municipal council)"),
    "Kommun": FieldInfo("municipality", "Municipality name"),
    "Kommunkod": FieldInfo("municipality_code", "Municipality code, 4 digits (county 2 + municipality 2)"),
    "Kommunvalkrets": FieldInfo("municipal_constituency", "Municipal constituency; empty when the municipality has none"),
    "Kommunvalkretskod": FieldInfo("municipal_constituency_code", "Municipal constituency code, 6 digits"),
    "Valkretskod": FieldInfo("constituency_code", "Constituency code, 6 digits (municipality code + 00 when there is one constituency)"),
    "Valdistrikt": FieldInfo("voting_district", "Voting district name; 'Uppsamlingsdistrikt' = collection district (no area)"),
    "Valdistriktsnamn": FieldInfo("voting_district", "Voting district name"),
    "Valdistriktskod": FieldInfo("voting_district_code", "Voting district code, 8 digits; a 6-digit code = collection district"),
    "Parti/kategori": FieldInfo("party_or_category", "Party name, or a summary category (see SUMMARY_CATEGORIES)"),
    "Röster": FieldInfo("votes", "Number of votes", "votes"),
    "Summa giltiga röster": FieldInfo("valid_votes", "Valid votes in the district", "votes"),
    "Röstberättigade": FieldInfo("eligible_voters", "Persons entitled to vote; 0 for collection districts", "persons"),
    "Valdeltagande (%)": FieldInfo("turnout", "Turnout = votes cast / eligible voters, as a FRACTION 0-1 despite the '%'", "fraction"),
    "Länskod": FieldInfo("county_code", "County code, 2 digits"),
    "Länsnamn": FieldInfo("county", "County name"),
    "Län": FieldInfo("county", "County name"),
    "Riksdagsvalkretskod": FieldInfo("parliament_constituency_code", "Riksdag constituency code"),
    "Riksdagsvalkrets": FieldInfo("parliament_constituency", "Riksdag constituency"),
    "Regionkod": FieldInfo("region_code", "Region code"),
    "Region": FieldInfo("region", "Region (regional council) name"),
    "Regionvalkretskod": FieldInfo("regional_constituency_code", "Regional-council constituency code"),
    "Regionvalkrets": FieldInfo("regional_constituency", "Regional-council constituency"),
}

#: Columns of the wide sheets that are not parties.
WIDE_FIXED_COLUMNS = set(FIELDS)
