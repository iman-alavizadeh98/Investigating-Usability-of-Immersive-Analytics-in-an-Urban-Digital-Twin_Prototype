"""
Load an election delivery for one municipality.

The results workbook and the boundary file cover the whole country / county;
both are filtered to the municipality code given by the caller. Apart from that
row filter and dropping the wide sheets' party columns that are empty for the
municipality (both recorded), values and column names are kept as delivered.
"""

from __future__ import annotations

import logging
import re
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import geopandas as gpd
import pandas as pd

from .config import (
    BOUNDARY_SUFFIXES,
    CODE_COLUMNS,
    ELECTION_FILE_PATTERN,
    MUNICIPALITY_CODE_PATTERN,
    MUNICIPALITY_COLUMN,
    RESULTS_SUFFIXES,
    SHEETS,
    WIDE_FIXED_COLUMNS,
)

logger = logging.getLogger(__name__)


@dataclass
class ElectionSource:
    """Everything loaded for one municipality, plus what was done on load."""

    results_path: Path
    boundaries_path: Path
    municipality_code: str
    election_type: Optional[str]          # "kommunval", "riksdagsval", "regionval" or None
    election_year: Optional[int]
    information: List[str]                # text of the "Information" sheet
    sheets: Dict[str, pd.DataFrame]       # filtered to the municipality
    districts: gpd.GeoDataFrame           # filtered to the municipality
    national_rows: Dict[str, int]         # rows per sheet / boundary file before filtering
    dropped_party_columns: Dict[str, int] = field(default_factory=dict)
    warnings: List[str] = field(default_factory=list)


def find_inputs(folder: Path) -> Tuple[Path, Path]:
    """The one results workbook and the one boundary file in `folder`."""
    folder = Path(folder)
    if not folder.is_dir():
        raise FileNotFoundError(f"Election folder not found: {folder}")
    results = sorted(p for p in folder.iterdir() if p.suffix.lower() in RESULTS_SUFFIXES and not p.name.startswith("~$"))
    bounds = sorted(p for p in folder.iterdir() if p.suffix.lower() in BOUNDARY_SUFFIXES)
    if len(results) != 1 or len(bounds) != 1:
        raise FileNotFoundError(
            f"Expected exactly one results file {RESULTS_SUFFIXES} and one boundary file {BOUNDARY_SUFFIXES} "
            f"in {folder}; found results={[p.name for p in results]}, boundaries={[p.name for p in bounds]}"
        )
    return results[0], bounds[0]


def parse_election(name: str) -> Tuple[Optional[str], Optional[int]]:
    """("kommunval", 2026) from '...kommunvalen-2026.xlsx'; (None, None) if not recognised."""
    match = ELECTION_FILE_PATTERN.search(name)
    if not match:
        return None, None
    return match.group("election").lower(), int(match.group("year"))


def _codes_as_text(df: pd.DataFrame, where: str, warnings: List[str]) -> pd.DataFrame:
    """Make code columns text; warn if one was stored as numbers (leading zeros may be lost)."""
    for col in CODE_COLUMNS:
        if col not in df.columns:
            continue
        if pd.api.types.is_numeric_dtype(df[col]):
            warnings.append(f"{where}: code column {col} is numeric in the source; leading zeros may be lost")
            df[col] = df[col].astype("Int64").astype("string")
        else:
            df[col] = df[col].astype("string").str.strip()
    return df


def _drop_empty_party_columns(df: pd.DataFrame) -> Tuple[pd.DataFrame, int]:
    """Drop wide-sheet party columns that are empty or 0 in every remaining row."""
    party_cols = [c for c in df.columns if c not in WIDE_FIXED_COLUMNS]
    empty = [c for c in party_cols if pd.to_numeric(df[c], errors="coerce").fillna(0).eq(0).all()]
    return df.drop(columns=empty), len(empty)


def load_election(folder: Path, municipality_code: str) -> ElectionSource:
    """Load and filter the results workbook and the boundary file."""
    if not re.fullmatch(MUNICIPALITY_CODE_PATTERN, str(municipality_code)):
        raise ValueError(f"Municipality code must be 4 digits (Kommunkod), got {municipality_code!r}")
    results_path, boundaries_path = find_inputs(folder)
    election_type, year = parse_election(results_path.name)
    warnings: List[str] = []
    if election_type is None:
        warnings.append(f"Election type/year not recognised in the file name {results_path.name}")

    started = time.perf_counter()
    logger.info(f"Reading results workbook {results_path.name} (whole country; this takes a while)")
    # Codes must be read as text: pandas otherwise turns "0114" into 114.
    workbook = pd.read_excel(results_path, sheet_name=None, dtype={c: str for c in CODE_COLUMNS})
    logger.info(f"  - {len(workbook)} sheets read in {time.perf_counter() - started:.0f} s")

    information: List[str] = []
    sheets: Dict[str, pd.DataFrame] = {}
    national_rows: Dict[str, int] = {}
    dropped: Dict[str, int] = {}
    for name, df in workbook.items():
        spec = SHEETS.get(name)
        if spec is None:
            warnings.append(f"Sheet {name!r} is not in config.SHEETS; loaded as-is")
        if spec is not None and spec.kind == "info":
            information = [str(v).strip() for v in df.iloc[:, 0].dropna()] if df.shape[1] else []
            # The sheet's first line is read as the header; keep it as text too.
            information = [str(df.columns[0]).strip()] + information if df.shape[1] else information
            continue
        national_rows[name] = int(len(df))
        df = _codes_as_text(df, name, warnings)
        if MUNICIPALITY_COLUMN not in df.columns:
            warnings.append(f"Sheet {name!r} has no {MUNICIPALITY_COLUMN} column; kept unfiltered")
        else:
            df = df[df[MUNICIPALITY_COLUMN] == municipality_code].reset_index(drop=True)
        if spec is not None and spec.kind in ("counts_wide", "shares_wide"):
            df, n = _drop_empty_party_columns(df)
            dropped[name] = n
        sheets[name] = df
        logger.info(f"  - {name}: {national_rows[name]:,} rows → {len(df):,} for {municipality_code}")

    if not any(len(df) for df in sheets.values()):
        raise ValueError(f"No rows with {MUNICIPALITY_COLUMN} = {municipality_code} in {results_path.name}")

    logger.info(f"Reading boundaries {boundaries_path.name}")
    boundaries = gpd.read_file(boundaries_path)
    national_rows["(boundaries)"] = int(len(boundaries))
    boundaries = _codes_as_text(boundaries, boundaries_path.name, warnings)
    if MUNICIPALITY_COLUMN in boundaries.columns:
        boundaries = boundaries[boundaries[MUNICIPALITY_COLUMN] == municipality_code].reset_index(drop=True)
    else:
        warnings.append(f"Boundary file has no {MUNICIPALITY_COLUMN} column; kept unfiltered")
    if boundaries.empty:
        raise ValueError(f"No districts with {MUNICIPALITY_COLUMN} = {municipality_code} in {boundaries_path.name}")
    logger.info(f"  - {national_rows['(boundaries)']:,} districts → {len(boundaries):,} for {municipality_code}")

    for message in warnings:
        logger.warning(f"  - {message}")
    return ElectionSource(
        results_path=results_path,
        boundaries_path=boundaries_path,
        municipality_code=str(municipality_code),
        election_type=election_type,
        election_year=year,
        information=information,
        sheets=sheets,
        districts=boundaries,
        national_rows=national_rows,
        dropped_party_columns=dropped,
        warnings=warnings,
    )
