"""
Checks and basic facts for loaded SCB tables (any `ScbDataset`).

Nothing here changes the data or stops the run: results are reported. Each
table gets two lists:
  - `issues`: something is wrong or unknown and needs a decision
    (wrong CRS, bad geometry, malformed or repeated IDs, unknown columns,
    negative values, encoding fallback, file name outside the SCB pattern);
  - `notes`: known properties of SCB data worth seeing before processing
    (cells cut at the delivery edge, 1,000 m cells overlapping finer cells,
    sub-groups that do not add up to the total because SCB perturbs small counts,
    values such as income on units whose count is 0).

Field roles come from the dataset's field dictionary (`FieldInfo.role`): only
`count` fields are checked against the total; `amount` and `median` fields are
values the total does not contain.

Ruta cell positions are always taken from the ID (exact south-west corner),
never from the geometry, which is noisy and may be cut at the delivery edge.
"""

from __future__ import annotations

import logging
from typing import Dict, List, Optional

import numpy as np
import pandas as pd

from .config import EXPECTED_CRS, FULL_SQUARE_TOLERANCE, RUTA_EASTING_DIGITS, UNIT_RULES, ScbDataset
from .loader import SourceTable

logger = logging.getLogger(__name__)

#: Examples listed per finding in the report.
MAX_EXAMPLES = 5


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _role(dataset: ScbDataset, column: str) -> Optional[str]:
    info = dataset.describe_field(column)
    return info.role if info else None


def _numeric(table: SourceTable, column: str) -> bool:
    s = table.data[column]
    return pd.api.types.is_numeric_dtype(s) and not pd.api.types.is_bool_dtype(s)


def columns_with_role(table: SourceTable, dataset: ScbDataset, *roles: str) -> List[str]:
    """Numeric columns whose field role (FieldInfo.role) is one of `roles`."""
    return [
        c for c in table.data.columns
        if c != table.data.geometry.name and _numeric(table, c) and _role(dataset, c) in roles
    ]


def total_column(table: SourceTable, dataset: ScbDataset) -> Optional[str]:
    """The table's total column (first of TableSpec.total_fields present), or None."""
    spec = dataset.table(table.table_number)
    candidates = spec.total_fields if spec else columns_with_role(table, dataset, "total")
    return next((c for c in candidates if c in table.data.columns), None)


def ruta_corners(table: SourceTable) -> pd.DataFrame:
    """
    Per row: `easting`, `northing` (south-west corner from the ID), `size_m`, and
    `valid_id`. Rows with a malformed ID keep NaN corners.
    """
    ids = table.data[table.id_column].astype("string").str.strip()
    valid = ids.str.fullmatch(UNIT_RULES["Ruta"]["id_value_pattern"]).fillna(False).astype(bool)
    easting = pd.to_numeric(ids.str[:RUTA_EASTING_DIGITS].where(valid), errors="coerce")
    northing = pd.to_numeric(ids.str[RUTA_EASTING_DIGITS:].where(valid), errors="coerce")
    size = (
        pd.to_numeric(table.data[table.size_column], errors="coerce")
        if table.size_column else pd.Series(np.nan, index=table.data.index)
    )
    return pd.DataFrame({"easting": easting, "northing": northing, "size_m": size, "valid_id": valid})


def unit_keys(table: SourceTable) -> pd.Series:
    """
    Unique key per row. Ruta: "<size>_<id>", because a 250 m and a 1,000 m cell
    with the same south-west corner share the ID. DeSO: the code itself.
    """
    ids = table.data[table.id_column].astype("string").str.strip()
    if table.unit == "Ruta" and table.size_column:
        sizes = pd.to_numeric(table.data[table.size_column], errors="coerce").astype("Int64").astype("string")
        return sizes.fillna("?") + "_" + ids.fillna("?")
    return ids.fillna("?")


def _examples(values) -> list:
    return [v.item() if hasattr(v, "item") else v for v in list(values)[:MAX_EXAMPLES]]


# ---------------------------------------------------------------------------
# One table
# ---------------------------------------------------------------------------

def _ruta_checks(table: SourceTable, report: dict) -> None:
    corners = ruta_corners(table)
    ok = corners["valid_id"] & corners["size_m"].notna()
    c = corners[ok]
    report["cell_sizes_m"] = {int(k): int(v) for k, v in c["size_m"].value_counts().sort_index().items()}

    off = (c["easting"] % c["size_m"] != 0) | (c["northing"] % c["size_m"] != 0)
    report["off_lattice_cells"] = int(off.sum())
    if off.any():
        report["issues"].append(
            f"{int(off.sum())} cells have a corner that is not a multiple of their size "
            f"(e.g. {_examples(table.data.loc[c.index[off], table.id_column])})"
        )

    # Geometry vs. the nominal square size².
    area = table.data.geometry.area[c.index]
    ratio = area / (c["size_m"] ** 2)
    not_full = (ratio - 1).abs() > FULL_SQUARE_TOLERANCE
    report["cells_not_full_square"] = int(not_full.sum())
    if not_full.any():
        report["notes"].append(
            f"{int(not_full.sum())} cells are not a full square (area off by more than "
            f"{FULL_SQUARE_TOLERANCE:.0%}), e.g. cut at the delivery edge; their values describe the whole cell"
        )

    # Coarser cells that contain finer cells of the same table.
    contains = set()
    for coarse in sorted(c["size_m"].unique()):
        is_coarse = c["size_m"] == coarse
        coarse_cells = set(zip(c.loc[is_coarse, "easting"], c.loc[is_coarse, "northing"]))
        finer = c[c["size_m"] < coarse]
        if not coarse_cells or finer.empty:
            continue
        parents = zip((finer["easting"] // coarse) * coarse, (finer["northing"] // coarse) * coarse)
        contains |= {(coarse, e, n) for e, n in parents if (e, n) in coarse_cells}
    report["coarse_cells_containing_finer_cells"] = len(contains)
    if contains:
        report["notes"].append(
            f"{len(contains)} coarser cells overlap finer cells of the same table; SCB gives the "
            "coarse cell only the remainder outside the finer cells"
        )


def _value_checks(table: SourceTable, dataset: ScbDataset, spec, report: dict) -> None:
    """Totals, sub-groups vs. total, negative values, values on empty units."""
    data = table.data
    total_col = total_column(table, dataset)
    parts = [c for c in columns_with_role(table, dataset, "count") if c != total_col]
    values = columns_with_role(table, dataset, "amount", "median")
    report["total_column"] = total_col
    report["count_columns"] = parts
    report["value_columns"] = values

    numeric = parts + ([total_col] if total_col else []) + values
    report["column_sums"] = {c: float(data[c].sum()) for c in parts + ([total_col] if total_col else [])}
    report["value_stats"] = {
        c: {"min": float(data[c].min()), "median": float(data[c].median()), "max": float(data[c].max()),
            "missing": int(data[c].isna().sum())}
        for c in values
    }
    report["total_sum"] = float(data[total_col].sum()) if total_col else None

    negatives = {c: int((data[c] < 0).sum()) for c in numeric if (data[c] < 0).any()}
    report["negative_values"] = negatives
    if negatives:
        report["issues"].append(f"Negative values: {negatives}")

    if total_col is None:
        report["issues"].append("No total column found (see TableSpec.total_fields)")
        return

    if spec is not None and spec.parts_sum_to_total and parts:
        diff = data[parts].sum(axis=1) - data[total_col]
        mismatch = diff != 0
        report["parts_vs_total"] = {
            "parts": parts,
            "rows_checked": int(len(diff)),
            "rows_where_parts_differ_from_total": int(mismatch.sum()),
            "max_abs_difference": float(diff.abs().max()) if len(diff) else 0.0,
            "sum_of_parts": float(data[parts].to_numpy().sum()),
            "sum_of_total": float(data[total_col].sum()),
        }
        if mismatch.any():
            report["notes"].append(
                f"On {int(mismatch.sum())} of {len(diff)} rows the parts do not add up to {total_col} "
                f"(max difference {report['parts_vs_total']['max_abs_difference']:g}); SCB perturbs small counts"
            )

    # A value (e.g. an income sum) on a unit whose total is 0 cannot be turned
    # into a per-person / per-household value there.
    empty_with_value = {
        c: int(((data[total_col] == 0) & data[c].notna() & (data[c] != 0)).sum()) for c in values
    }
    empty_with_value = {c: n for c, n in empty_with_value.items() if n}
    report["values_where_total_is_zero"] = empty_with_value
    for c, n in empty_with_value.items():
        report["notes"].append(
            f"{n} rows have {total_col} = 0 but a non-zero {c}; a per-unit average (e.g. {c} / {total_col}) "
            "is undefined there"
        )


def validate_table(table: SourceTable, dataset: ScbDataset) -> dict:
    """Checks and basic facts for one table."""
    data = table.data
    spec = dataset.table(table.table_number)
    geom = data.geometry
    report: dict = {
        "file": table.path.name,
        "table_number": table.table_number,
        "title_en": spec.title_en if spec else None,
        "title_sv": spec.title_sv if spec else None,
        "unit": table.unit,
        "year": table.year,
        "rows": int(len(data)),
        "columns": [c for c in data.columns if c != geom.name],
        "declared_encoding": table.declared_encoding,
        "encoding_fallback_used": table.encoding_used,
        "crs": data.crs.to_string() if data.crs else None,
        "geometry_types": {str(k): int(v) for k, v in geom.geom_type.value_counts().items()},
        "null_geometries": int(geom.isna().sum()),
        "empty_geometries": int(geom.is_empty.sum()),
        "invalid_geometries": int((~geom.is_valid & geom.notna()).sum()),
        "id_column": table.id_column,
        "size_column": table.size_column,
        "issues": list(table.warnings),
        "notes": [],
    }

    if spec is None:
        report["issues"].append(
            f"Table number {table.table_number!r} is not in the {dataset.dataset_id} table catalog; its meaning is unknown"
        )
    if report["crs"] != EXPECTED_CRS:
        report["issues"].append(f"CRS is {report['crs']}, expected {EXPECTED_CRS}")
    for key, label in (("null_geometries", "null"), ("empty_geometries", "empty"), ("invalid_geometries", "invalid")):
        if report[key]:
            report["issues"].append(f"{report[key]} {label} geometries")

    unknown = [c for c in report["columns"] if dataset.describe_field(c) is None]
    report["unknown_columns"] = unknown
    if unknown:
        report["issues"].append(f"Columns not in the {dataset.dataset_id} field dictionary (meaning unknown): {unknown}")

    # IDs
    if table.id_column:
        ids = data[table.id_column].astype("string").str.strip()
        rule = UNIT_RULES.get(table.unit or "", {})
        if "id_value_pattern" in rule:
            bad = ~ids.str.fullmatch(rule["id_value_pattern"]).fillna(False).astype(bool)
            report["ids_bad_format"] = int(bad.sum())
            if bad.any():
                report["issues"].append(
                    f"{int(bad.sum())} IDs do not match {rule['id_value_pattern']} (e.g. {_examples(ids[bad])})"
                )
        keys = unit_keys(table)
        dup = keys[keys.duplicated(keep=False)]
        report["duplicate_keys"] = int(keys.duplicated().sum())
        if len(dup):
            report["issues"].append(f"{report['duplicate_keys']} repeated unit keys (e.g. {_examples(dup.unique())})")
        if table.unit == "Ruta" and table.size_column:
            shared = ids.duplicated(keep=False) & ~keys.duplicated(keep=False)
            report["ids_shared_by_different_sizes"] = int(ids[shared].nunique())

    if table.unit == "Ruta" and table.id_column and table.size_column:
        _ruta_checks(table, report)

    _value_checks(table, dataset, spec, report)
    return report


# ---------------------------------------------------------------------------
# Across tables
# ---------------------------------------------------------------------------

def validate_coverage(tables: Dict[str, SourceTable], dataset: ScbDataset) -> List[dict]:
    """
    Which units each table covers, per group of comparable tables: same unit and,
    for Ruta, the same set of cell sizes (a 100 m grid is its own group).
    A unit missing from a table means "no data", not 0.
    """
    groups: Dict[str, List[SourceTable]] = {}
    for table in tables.values():
        if not table.id_column:
            continue
        if table.unit == "Ruta" and table.size_column:
            sizes = sorted(pd.to_numeric(table.data[table.size_column], errors="coerce").dropna().astype(int).unique())
            name = f"Ruta {'/'.join(str(s) for s in sizes)} m"
        else:
            name = str(table.unit)
        groups.setdefault(name, []).append(table)

    result = []
    for name, members in groups.items():
        keys = {t.key: set(unit_keys(t)) for t in members}
        union = set().union(*keys.values())
        common = set.intersection(*keys.values())
        rows = []
        for t in members:
            total = total_column(t, dataset)
            rows.append({
                "table": t.key,
                "units": len(keys[t.key]),
                "missing_vs_union": len(union - keys[t.key]),
                "total_column": total,
                "total_sum": float(t.data[total].sum()) if total else None,
            })
        result.append({
            "group": name,
            "tables": [t.key for t in members],
            "units_in_any_table": len(union),
            "units_in_every_table": len(common),
            "per_table": rows,
        })
    return result


def validate_tables(tables: Dict[str, SourceTable], dataset: ScbDataset) -> dict:
    """Run every check; returns {"tables": {key: report}, "coverage": [...], "issue_count": n}."""
    per_table = {}
    for key, table in tables.items():
        per_table[key] = validate_table(table, dataset)
        for issue in per_table[key]["issues"][len(table.warnings):]:  # loader warnings were logged on load
            logger.warning(f"  - {key}: {issue}")
    report = {
        "tables": per_table,
        "coverage": validate_coverage(tables, dataset),
        "issue_count": sum(len(r["issues"]) for r in per_table.values()),
    }
    logger.info(f"  - {len(per_table)} tables checked, {report['issue_count']} issue(s)")
    return report
