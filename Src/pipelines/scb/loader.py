"""
Load every SCB table (population, income, ...) in a delivery folder, unchanged.

Raw column names and values are kept as delivered. What the loader adds is
bookkeeping on the side (`SourceTable`): what the file name says (table number,
unit, year), which encoding decoded it, and which columns hold the ID and the
cell size.
"""

from __future__ import annotations

import logging
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Sequence

import geopandas as gpd

from .config import ENCODING_FALLBACKS, FILE_NAME_PATTERN, UNIT_RULES

logger = logging.getLogger(__name__)


@dataclass
class SourceTable:
    """One shapefile as loaded, plus what is known about it."""

    key: str                          # file stem, e.g. "Tab1_Ruta_2024"
    path: Path
    data: gpd.GeoDataFrame
    table_number: Optional[str]       # "1"; None if the name does not match the SCB pattern
    unit: Optional[str]               # "Ruta" / "DeSO" / other text from the name / None
    year: Optional[int]               # data year from the file name
    suffix: Optional[str]             # e.g. "region"
    declared_encoding: Optional[str]  # text of the .cpg file, None if there is none
    encoding_used: Optional[str]      # None = the declared/default encoding worked
    id_column: Optional[str] = None
    size_column: Optional[str] = None
    warnings: List[str] = field(default_factory=list)


def parse_file_name(stem: str) -> Dict[str, Optional[str]]:
    """Split `Tab1_Ruta_2024[_region]` into its parts; all None if it does not match."""
    match = FILE_NAME_PATTERN.match(stem)
    if not match:
        return {"number": None, "unit": None, "year": None, "suffix": None}
    return match.groupdict()


def discover_shapefiles(folder: Path) -> List[Path]:
    """All `.shp` files directly in `folder`, sorted by name."""
    folder = Path(folder)
    if not folder.is_dir():
        raise FileNotFoundError(f"Folder not found: {folder}")
    files = sorted(folder.glob("*.shp"), key=lambda p: p.name.lower())
    if not files:
        raise FileNotFoundError(f"No .shp files in {folder}")
    return files


def read_with_encoding_fallback(path: Path) -> tuple[gpd.GeoDataFrame, Optional[str]]:
    """
    Read a shapefile with its declared encoding; if that cannot decode it, try
    ENCODING_FALLBACKS in order. Returns (data, fallback used or None).
    """
    try:
        return gpd.read_file(path), None
    except UnicodeDecodeError as first_error:
        for encoding in ENCODING_FALLBACKS:
            try:
                data = gpd.read_file(path, encoding=encoding)
            except UnicodeDecodeError:
                continue
            return data, encoding
        raise ValueError(
            f"{path.name}: the declared encoding failed ({first_error}) and so did "
            f"every fallback in ENCODING_FALLBACKS {ENCODING_FALLBACKS}"
        ) from first_error


def _find_columns(columns: Sequence[str], unit: Optional[str]) -> tuple[Optional[str], Optional[str]]:
    """(id column, size column) for a unit, using the rules in config.UNIT_RULES."""
    rules = UNIT_RULES.get(unit or "")
    candidates = [rules] if rules else list(UNIT_RULES.values())  # unknown unit: try every rule
    for rule in candidates:
        id_col = next((c for c in rule.get("id_columns", ()) if c in columns), None)
        if id_col is None and "id_column_pattern" in rule:
            pattern = re.compile(rule["id_column_pattern"])
            id_col = next((c for c in columns if pattern.match(c)), None)
        size_col = next((c for c in rule.get("size_columns", ()) if c in columns), None)
        if id_col:
            return id_col, size_col
    return None, None


def load_table(path: Path) -> SourceTable:
    """Load one shapefile into a SourceTable."""
    path = Path(path)
    parts = parse_file_name(path.stem)
    cpg = path.with_suffix(".cpg")
    declared = cpg.read_text(encoding="ascii", errors="replace").strip() if cpg.exists() else None

    data, fallback = read_with_encoding_fallback(path)
    table = SourceTable(
        key=path.stem,
        path=path,
        data=data,
        table_number=parts["number"],
        unit=parts["unit"],
        year=int(parts["year"]) if parts["year"] else None,
        suffix=parts["suffix"],
        declared_encoding=declared,
        encoding_used=fallback,
    )
    if parts["number"] is None:
        table.warnings.append(
            "File name does not follow Tab<number>_<unit>_<year>; table, unit and year are unknown"
        )
    if fallback:
        table.warnings.append(
            f"Declared encoding {declared or '(none, GDAL default)'} could not decode the file; read as {fallback}"
        )
    table.id_column, table.size_column = _find_columns(list(data.columns), table.unit)
    if table.id_column is None:
        table.warnings.append("No ID column found (see config.UNIT_RULES)")
    if table.unit == "Ruta" and table.size_column is None:
        table.warnings.append("No cell-size column found (see config.UNIT_RULES)")

    for message in table.warnings:
        logger.warning(f"  - {table.key}: {message}")
    logger.info(
        f"  - {table.key}: {len(data):,} rows, {len(data.columns) - 1} attribute columns, "
        f"CRS {data.crs.to_string() if data.crs else None}"
    )
    return table


def load_scb_tables(folder: Path, only: Optional[Sequence[str]] = None) -> Dict[str, SourceTable]:
    """
    Load every table in `folder`.

    Args:
        folder: delivery folder with the .shp files.
        only: optional file-stem prefixes to keep, e.g. ["Tab1_Ruta", "Tab2"].
    """
    files = discover_shapefiles(folder)
    if only:
        files = [f for f in files if any(f.stem.startswith(prefix) for prefix in only)]
        if not files:
            raise FileNotFoundError(f"No .shp in {folder} starts with any of {list(only)}")
    logger.info(f"Loading {len(files)} table(s) from {folder}")
    return {table.key: table for table in (load_table(f) for f in files)}
