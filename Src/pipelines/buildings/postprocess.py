"""
Post-processing utilities for the buildings pipeline.

Creates the clean postprocess snapshot: exactly one row per object_id.

Why this is needed: Lantmäteriet defines objektidentitet as globally unique,
but the delivered GeoPackages contain rows that share an object_id. Checked on
the Gothenburg, Helsingborg and Fortuna deliveries (2026-10-06):
  - a few rows are exact duplicates (same attributes and geometry);
  - no ID has more than one version;
  - the rest are separate, non-overlapping pieces of one building (in
    Helsingborg 1,384 of 1,634 groups touch and union into one polygon). They
    share every attribute except lagesosakerhetplan, lagesosakerhethojd and
    insamlingslage, i.e. the pieces were surveyed differently.
Dropping all but one piece deleted 6% of Helsingborg's footprint area (19% in
Gothenburg), so pieces are merged by default.

Steps:
  1. Drop exact duplicate rows (all attributes and geometry equal).
  2. Drop older versions of an object_id (keep the newest version_valid_from,
     then object_version), as described in the product spec.
  3. Resolve remaining repeated IDs (pieces of one building):
     - "merge" (default): union the pieces into one (Multi)Polygon. Attributes
       come from the largest piece; position uncertainties take the maximum
       over the pieces (conservative); source_part_count and
       collection_level_mixed record what was merged.
     - "keep_largest": keep only the largest piece (loses footprint area; the
       loss is reported).
"""

from pathlib import Path
from typing import Dict, Any, Tuple
from datetime import datetime, timezone
import json
import logging

import geopandas as gpd
import numpy as np
import pandas as pd
import shapely
from shapely.geometry import MultiPolygon, Polygon

logger = logging.getLogger(__name__)

DUPLICATE_STRATEGIES = ("merge", "keep_largest")

# Columns that legitimately differ between pieces of one building, and how
# "merge" combines them. Other differing columns are reported as conflicts and
# take the largest piece's value.
MAX_OVER_PARTS_COLUMNS = ("position_uncertainty_plan_m", "position_uncertainty_height_m")
COLLECTION_LEVEL_COLUMN = "collection_level"
AREA_COLUMN = "footprint_area_m2"


def _merge_parts(geometries) -> MultiPolygon:
    """Union building pieces into one MultiPolygon (polygonal parts only)."""
    merged = shapely.union_all(shapely.make_valid(np.asarray(geometries)))
    polygons = [
        part for part in shapely.get_parts(merged)
        if isinstance(part, (Polygon, MultiPolygon)) and not part.is_empty
    ]
    flat = []
    for polygon in polygons:
        flat.extend(polygon.geoms if isinstance(polygon, MultiPolygon) else [polygon])
    return MultiPolygon(flat)


def _equal_or_both_missing(left: pd.Series, right: pd.Series) -> pd.Series:
    return (left == right) | (left.isna() & right.isna())


def build_postprocess_snapshot(
    gdf: gpd.GeoDataFrame,
    output_dir: Path,
    id_col: str = "object_id",
    version_col: str = "version_valid_from",
    version_num_col: str = "object_version",
    strategy: str = "merge",
) -> Tuple[gpd.GeoDataFrame, Dict[str, Any]]:
    """
    Create the postprocess snapshot (one row per object_id).

    Returns:
        (clean_gdf, report)
    """
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    if strategy not in DUPLICATE_STRATEGIES:
        raise ValueError(f"Unknown duplicate strategy {strategy!r}; expected one of {DUPLICATE_STRATEGIES}")
    if id_col not in gdf.columns:
        raise ValueError(f"Missing id column: {id_col}")
    if version_col not in gdf.columns:
        raise ValueError(f"Missing version column: {version_col}")
    if gdf[id_col].isna().any():
        raise ValueError(f"{int(gdf[id_col].isna().sum())} rows have no {id_col}")

    geometry_col = gdf.geometry.name
    input_rows = int(len(gdf))
    input_area = float(gdf.geometry.area.sum())

    # 1. Exact duplicates (attributes + geometry). Geometry compared as WKB.
    exact_key = pd.DataFrame(gdf.drop(columns=geometry_col))
    exact_key["__wkb"] = shapely.to_wkb(gdf.geometry.values)
    exact_mask = exact_key.duplicated(keep="first")
    data = gdf[~exact_mask.values]
    exact_duplicates_removed = int(exact_mask.sum())

    # 2. Older versions of the same object_id.
    version_cols = [version_col] + ([version_num_col] if version_num_col in data.columns else [])
    latest = (
        data[[id_col] + version_cols]
        .sort_values([id_col] + version_cols, kind="mergesort")
        .groupby(id_col, sort=False)
        .tail(1)
        .set_index(id_col)
    )
    latest_aligned = latest.reindex(data[id_col].values)
    is_latest = np.ones(len(data), dtype=bool)
    for col in version_cols:
        is_latest &= _equal_or_both_missing(
            data[col].reset_index(drop=True), latest_aligned[col].reset_index(drop=True)
        ).values
    older_versions_removed = int((~is_latest).sum())
    data = data[is_latest]

    # 3. Pieces of one building (same object_id and version, different geometry).
    data = data.assign(__order=np.arange(len(data)), __area=data.geometry.area.values)
    counts = data[id_col].map(data[id_col].value_counts())
    single = data[counts.values == 1]
    multi = data[counts.values > 1].sort_values(
        [id_col, "__area", "__order"], ascending=[True, False, True], kind="mergesort"
    )
    groups = multi.groupby(id_col, sort=False)
    representative = groups.head(1).copy()  # largest piece per object_id

    attribute_cols = [
        c for c in gdf.columns
        if c not in (geometry_col, id_col, AREA_COLUMN)
    ]
    conflicts = {}
    if len(multi):
        n_unique = groups[attribute_cols].nunique(dropna=False)
        conflicts = {c: int(n) for c, n in (n_unique > 1).sum().items() if n > 0}
    unexpected_conflicts = {
        c: n for c, n in conflicts.items()
        if c not in MAX_OVER_PARTS_COLUMNS
        and c not in (COLLECTION_LEVEL_COLUMN, f"{COLLECTION_LEVEL_COLUMN}_en")
    }
    if unexpected_conflicts:
        logger.warning(
            "Pieces of the same building differ in unexpected columns (largest piece's value kept): %s",
            unexpected_conflicts,
        )

    part_rows = int(len(multi))
    ids_with_parts = int(multi[id_col].nunique())
    part_count = groups.size()

    single = single.assign(source_part_count=1)
    if COLLECTION_LEVEL_COLUMN in data.columns:
        single = single.assign(collection_level_mixed=False)

    representative["source_part_count"] = representative[id_col].map(part_count).astype(int).values
    if COLLECTION_LEVEL_COLUMN in data.columns:
        mixed = groups[COLLECTION_LEVEL_COLUMN].nunique(dropna=False) > 1
        representative["collection_level_mixed"] = representative[id_col].map(mixed).astype(bool).values

    if strategy == "merge" and len(multi):
        merged_geometry = groups[geometry_col].apply(lambda s: _merge_parts(s.values))
        representative[geometry_col] = representative[id_col].map(merged_geometry).values
        for col in MAX_OVER_PARTS_COLUMNS:
            if col in multi.columns:
                representative[col] = representative[id_col].map(groups[col].max()).values

    # Per-building trace of what was merged or dropped.
    parts_trace = pd.DataFrame()
    if len(multi):
        trace = {
            id_col: part_count.index,
            "part_count": part_count.values,
            "parts_total_area_m2": groups["__area"].sum().round(2).values,
            "largest_part_area_m2": groups["__area"].max().round(2).values,
        }
        if COLLECTION_LEVEL_COLUMN in multi.columns:
            trace["collection_levels"] = groups[COLLECTION_LEVEL_COLUMN].apply(
                lambda s: "|".join(sorted(s.dropna().astype(str).unique()))
            ).values
        for col in MAX_OVER_PARTS_COLUMNS:
            if col in multi.columns:
                trace[f"{col}_min"] = groups[col].min().values
                trace[f"{col}_max"] = groups[col].max().values
        parts_trace = pd.DataFrame(trace)

    clean = pd.concat([single, representative]).sort_values("__order", kind="mergesort")
    clean = gpd.GeoDataFrame(
        clean.drop(columns=["__order", "__area"]), geometry=geometry_col, crs=gdf.crs
    )
    if AREA_COLUMN in clean.columns:
        clean[AREA_COLUMN] = clean.geometry.area.round(2)

    if clean[id_col].duplicated().any():
        raise RuntimeError("Postprocess snapshot still has repeated object IDs")
    invalid = int((~clean.geometry.is_valid).sum())
    if invalid:
        logger.warning("%s geometries are invalid after postprocess", invalid)

    output_area = float(clean.geometry.area.sum())

    output_gpkg = output_dir / "buildings_processed_postprocess.gpkg"
    clean.to_file(output_gpkg, layer="buildings_postprocess")
    parts_csv = output_dir / "buildings_postprocess_parts.csv"
    parts_trace.to_csv(parts_csv, index=False, encoding="utf-8")

    rules = [
        "Removed exact duplicate rows (all attributes and geometry equal).",
        "Removed older versions of an object_id (newest version_valid_from, then object_version, kept).",
    ]
    if strategy == "merge":
        rules.append(
            "Merged pieces sharing an object_id into one MultiPolygon. Attributes from the largest "
            "piece; position uncertainties = maximum over pieces; source_part_count and "
            "collection_level_mixed record the merge."
        )
    else:
        rules.append("Kept only the largest piece per object_id; other pieces dropped.")

    report = {
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "strategy": strategy,
        "rules": rules,
        "input_rows": input_rows,
        "exact_duplicate_rows_removed": exact_duplicates_removed,
        "older_version_rows_removed": older_versions_removed,
        "object_ids_with_multiple_pieces": ids_with_parts,
        "piece_rows": part_rows,
        "piece_rows_merged_or_dropped": part_rows - ids_with_parts,
        "output_rows": int(len(clean)),
        "input_footprint_area_m2": round(input_area, 1),
        "output_footprint_area_m2": round(output_area, 1),
        "footprint_area_change_pct": round(100.0 * (output_area - input_area) / input_area, 3) if input_area else 0.0,
        "columns_differing_between_pieces": conflicts,
        "unexpected_differing_columns": unexpected_conflicts,
        "invalid_geometries_after": invalid,
        "id_column": id_col,
        "version_columns": version_cols,
        "output_files": {
            "gpkg": output_gpkg.name,
            "parts_csv": parts_csv.name,
            "report_json": "buildings_postprocess_report.json",
            "report_md": "buildings_postprocess_report.md",
        },
    }

    with open(output_dir / "buildings_postprocess_report.json", "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, ensure_ascii=False)

    report_lines = [
        "# Buildings Postprocess Report",
        "",
        "One row per object_id. See `Src/pipelines/buildings/postprocess.py` for why IDs repeat.",
        "",
        f"Strategy: `{strategy}`",
        "",
        "## Rules",
        *[f"- {rule}" for rule in rules],
        "",
        "## Counts",
        f"- Input rows: {report['input_rows']}",
        f"- Exact duplicate rows removed: {report['exact_duplicate_rows_removed']}",
        f"- Older-version rows removed: {report['older_version_rows_removed']}",
        f"- Object IDs made of several pieces: {report['object_ids_with_multiple_pieces']} "
        f"({report['piece_rows']} rows, {report['piece_rows_merged_or_dropped']} rows merged away)",
        f"- Output rows: {report['output_rows']}",
        f"- Footprint area: {report['input_footprint_area_m2']:.0f} m² → {report['output_footprint_area_m2']:.0f} m² "
        f"({report['footprint_area_change_pct']}%)",
        "",
        "## Columns that differ between pieces of one building",
        *([f"- {c}: {n} buildings" for c, n in conflicts.items()] or ["- none"]),
        "",
        f"Unexpected (largest piece's value kept): {unexpected_conflicts or 'none'}",
        "",
        f"Per-building detail: `{parts_csv.name}`",
        "",
    ]
    with open(output_dir / "buildings_postprocess_report.md", "w", encoding="utf-8") as f:
        f.write("\n".join(report_lines))

    return clean, report
