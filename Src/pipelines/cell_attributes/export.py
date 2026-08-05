"""
Export aggregated cell attributes.

Outputs are keyed by the SAME cell id the mesh manifest uses
(mesh_generation.grid_reference.cell_id), so the Unity runtime joins analytics to
geometry with a dictionary lookup -- no spatial query, no tolerance.

Cell polygons are built from the LATTICE, not from source geometry. Source cells
carry ~4 mm of float noise and 40 are clipped at the municipal boundary; the
lattice gives exact squares that tile without gaps.

Mirrors the structure of Src/pipelines/lidar_heights/export.py.
"""

from __future__ import annotations

import json
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional

import geopandas as gpd
import pandas as pd
from shapely.geometry import box

logger = logging.getLogger(__name__)


class CellAttributeExporter:
    """Write aggregated cell attributes to GeoPackage / Parquet / JSON."""

    def __init__(self, output_dir: Path, anchor_x: float, anchor_y: float,
                 cell_size_m: int, crs: str = "EPSG:3006"):
        self.output_dir = Path(output_dir)
        self.anchor_x = anchor_x
        self.anchor_y = anchor_y
        self.cell_size_m = cell_size_m
        self.crs = crs
        self.output_dir.mkdir(parents=True, exist_ok=True)

    # -- geometry ----------------------------------------------------------

    def build_cell_geometries(self, df: pd.DataFrame) -> gpd.GeoDataFrame:
        """Attach exact lattice squares and canonical cell ids to a result frame."""
        from mesh_generation.grid_reference import cell_id as make_cell_id

        origins_x = self.anchor_x + df["grid_col"] * self.cell_size_m
        origins_y = self.anchor_y + df["grid_row"] * self.cell_size_m

        frame = df.copy()
        frame["cell_id"] = [
            make_cell_id(int(c), int(r))
            for c, r in zip(frame["grid_col"], frame["grid_row"])
        ]
        frame["cell_origin_x"] = origins_x
        frame["cell_origin_y"] = origins_y
        frame["cell_size_m"] = self.cell_size_m

        geometry = [
            box(x, y, x + self.cell_size_m, y + self.cell_size_m)
            for x, y in zip(origins_x, origins_y)
        ]

        ordered = ["cell_id", "grid_col", "grid_row",
                   "cell_origin_x", "cell_origin_y", "cell_size_m"]
        rest = [c for c in frame.columns if c not in ordered]
        return gpd.GeoDataFrame(frame[ordered + rest], geometry=geometry, crs=self.crs)

    # -- writers -----------------------------------------------------------

    def export_geopackage(self, gdf: gpd.GeoDataFrame, name: str) -> Optional[Path]:
        path = self.output_dir / f"{name}.gpkg"
        try:
            gdf.to_file(path, layer=name, driver="GPKG")
            logger.info(f"  GeoPackage: {path} ({len(gdf):,} cells)")
            return path
        except Exception as exc:
            logger.error(f"  GeoPackage export failed: {exc}")
            return None

    def export_parquet(self, gdf: gpd.GeoDataFrame, name: str) -> Optional[Path]:
        path = self.output_dir / f"{name}.parquet"
        try:
            gdf.to_parquet(path)
            logger.info(f"  Parquet: {path}")
            return path
        except Exception as exc:
            logger.error(f"  Parquet export failed: {exc}")
            return None

    def export_runtime_json(self, gdf: gpd.GeoDataFrame, name: str) -> Optional[Path]:
        """
        Flat {cell_id: {attribute: value}} for the Unity runtime.

        Geometry is omitted: the runtime already has the lattice from the mesh
        manifest's `grid_reference` block and can rebuild any cell's bounds.
        """
        path = self.output_dir / f"{name}.json"
        skip = {"geometry", "cell_id"}
        try:
            payload = {
                row["cell_id"]: {
                    k: (int(v) if isinstance(v, (int, float)) and float(v).is_integer() else v)
                    for k, v in row.items()
                    if k not in skip and pd.notna(v)
                }
                for row in gdf.drop(columns="geometry").to_dict("records")
            }
            with open(path, "w", encoding="utf-8") as handle:
                json.dump(payload, handle, ensure_ascii=False, separators=(",", ":"))
            logger.info(f"  Runtime JSON: {path} ({len(payload):,} cells)")
            return path
        except Exception as exc:
            logger.error(f"  JSON export failed: {exc}")
            return None

    def export_metadata(self, name: str, metadata: Dict) -> Path:
        path = self.output_dir / f"{name}_metadata.json"
        with open(path, "w", encoding="utf-8") as handle:
            json.dump(metadata, handle, indent=2, ensure_ascii=False, default=str)
        logger.info(f"  Metadata: {path}")
        return path

    def export_qc(self, rows: List[Dict], name: str) -> Optional[Path]:
        """Per-layer QC table: totals, reconciliation, conservation, data flags."""
        if not rows:
            return None
        path = self.output_dir / f"{name}_qc.csv"
        pd.DataFrame(rows).to_csv(path, index=False)
        logger.info(f"  QC table: {path}")
        return path

    # -- orchestration -----------------------------------------------------

    def export_all(self, gdf: gpd.GeoDataFrame, name: str, metadata: Dict,
                   qc_rows: List[Dict], formats=("gpkg", "parquet", "json")) -> Dict[str, Path]:
        logger.info(f"Exporting '{name}' to {self.output_dir}")
        written: Dict[str, Path] = {}

        if "gpkg" in formats:
            if (p := self.export_geopackage(gdf, name)):
                written["gpkg"] = p
        if "parquet" in formats:
            if (p := self.export_parquet(gdf, name)):
                written["parquet"] = p
        if "json" in formats:
            if (p := self.export_runtime_json(gdf, name)):
                written["json"] = p

        written["metadata"] = self.export_metadata(name, metadata)
        if (p := self.export_qc(qc_rows, name)):
            written["qc"] = p
        return written


def build_metadata(config, layer_reports: List[Dict], field_map: Dict[str, str],
                   anchor_x: float, anchor_y: float, cell_size_m: int) -> Dict:
    """
    Assemble the provenance record required by CLAUDE.md: sources, CRS, field
    translations, conservation results, and the assumptions applied.
    """
    from .config import DATASET_NAMES

    return {
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "dataset": DATASET_NAMES,
        "grid_reference": {
            "anchor_x": anchor_x,
            "anchor_y": anchor_y,
            "cell_size_m": cell_size_m,
            "crs": "EPSG:3006",
            "id_format": "grid_{col:+04d}_{row:+04d}",
            "note": (
                "Identical to the lattice in the mesh manifest's grid_reference "
                "block; cell ids join directly to mesh groups."
            ),
        },
        "reconciliation_rule": config.reconciliation_rule,
        "assumptions": [
            "Cell membership is derived from the `Ruta` CODE (exact), not the "
            "shapefile geometry (~4 mm noise; 40 cells clipped at the municipal "
            "boundary).",
            "Primary key is (cell size, cell code): 48 `Ruta` codes are shared "
            "between the 250 m and 1000 m sub-layers.",
            "Source cells larger than the target are split into equal shares using "
            "integer largest-remainder apportionment, so parts sum exactly.",
            "Attributes describe the 500 m CELL. They are NOT disaggregated to "
            "individual buildings: population is only known at cell resolution.",
            "Median fields (e.g. MedianInk) are non-additive and excluded from "
            "aggregation.",
        ],
        "field_translations_swedish_to_english": field_map,
        "layers": layer_reports,
    }
