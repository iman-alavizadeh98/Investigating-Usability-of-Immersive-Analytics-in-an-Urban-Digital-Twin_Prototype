"""
Cell-attribute pipeline: SCB Ruta statistics -> the frozen 500 m analytical grid.

Follows the BasePipeline contract (load -> validate -> preprocess -> export) used
by the buildings and LiDAR-height pipelines.

Attributes attach to the CELL, not to individual buildings. Population and the
other SCB measures are only known at cell resolution; any per-building figure
would be modelled, unverifiable, and -- in a usability study -- a validity threat,
since participants would be reasoning about invented numbers. The deprecated
Src/Scripts/legacy/preprocess_spatial_joins.py attempted per-building attribution
and gave every building in a cell that cell's full population.
"""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Dict, List

import geopandas as gpd
import pandas as pd

from ..base import BasePipeline
from mesh_generation.grid_reference import GRID_ANCHOR_X, GRID_ANCHOR_Y

from .config import CellAttributeConfig, resolve_layers, RUTA_FIELD_TRANSLATIONS
from .aggregate import (
    aggregate_to_target_cells,
    check_conservation,
    prepare_source_frame,
    reconcile_resolutions,
)
from .export import CellAttributeExporter, build_metadata

logger = logging.getLogger(__name__)


class CellAttributePipeline(BasePipeline):
    """Aggregate one or more SCB Ruta layers onto the analytical grid."""

    def __init__(self, config: CellAttributeConfig = None, verbose: bool = True):
        self.cfg = config or CellAttributeConfig()
        super().__init__(name="CellAttributes", config=vars(self.cfg), verbose=verbose)

        self.anchor_x = GRID_ANCHOR_X
        self.anchor_y = GRID_ANCHOR_Y
        self.cell_size_m = self.cfg.cell_size_m

        self.specs = resolve_layers(self.cfg.layers)
        self.sources: Dict[str, gpd.GeoDataFrame] = {}
        self.prepared: Dict[str, pd.DataFrame] = {}
        self.renames: Dict[str, Dict[str, str]] = {}
        self.quality: Dict[str, dict] = {}
        self.layer_reports: List[dict] = []
        self.result: gpd.GeoDataFrame = None

    # -- load --------------------------------------------------------------

    def load(self):
        """Read each registered source layer. Raw values are left untouched."""
        for spec in self.specs:
            path = Path(spec.source_path)
            if not path.exists():
                raise FileNotFoundError(
                    f"Source for layer '{spec.layer_id}' not found: {path}. "
                    "SCB Ruta shapefiles live under Raw_data/0- Gothenburg/."
                )
            gdf = gpd.read_file(path)
            self.sources[spec.layer_id] = gdf
            logger.info(
                f"Loaded {spec.layer_id}: {len(gdf):,} cells from {path.name} "
                f"(CRS {gdf.crs})"
            )
        self.data = self.sources
        return self.data

    # -- validate ----------------------------------------------------------

    def validate(self):
        """
        Check CRS, lattice conformance, and each layer's declared total.

        A total that no longer matches `expected_total` means the source data
        changed; the profiling and integration reports would then be stale.
        """
        report = {"layers": {}, "errors": [], "warnings": []}

        for spec in self.specs:
            gdf = self.sources[spec.layer_id]
            entry = {"rows": int(len(gdf)), "crs": str(gdf.crs)}

            if gdf.crs is None or "3006" not in str(gdf.crs):
                report["errors"].append(
                    f"{spec.layer_id}: expected EPSG:3006, got {gdf.crs}"
                )

            prepared, rename, quality = prepare_source_frame(
                gdf, spec, self.anchor_x, self.anchor_y, self.cell_size_m
            )
            self.prepared[spec.layer_id] = prepared
            self.renames[spec.layer_id] = rename
            self.quality[spec.layer_id] = quality

            total_alias = rename.get(spec.total_field, spec.total_field)
            entry["total_alias"] = total_alias
            if total_alias in prepared.columns:
                observed = int(prepared[total_alias].sum())
                entry["source_total"] = observed
                entry["expected_total"] = spec.expected_total

                if spec.expected_total is not None and observed != spec.expected_total:
                    message = (
                        f"{spec.layer_id}: source total {observed:,} != expected "
                        f"{spec.expected_total:,}. The source data has changed; "
                        "regenerate the profiling and integration reports."
                    )
                    if self.cfg.enforce_expected_totals:
                        report["errors"].append(message)
                    else:
                        report["warnings"].append(message)

            if quality["lattice"]["off_lattice_cells"]:
                report["warnings"].append(
                    f"{spec.layer_id}: {quality['lattice']['off_lattice_cells']} cell(s) "
                    "have a corner inconsistent with their declared size label"
                )

            entry["quality"] = quality
            report["layers"][spec.layer_id] = entry

        if report["errors"]:
            for error in report["errors"]:
                logger.error(f"  ✗ {error}")
            raise ValueError(
                f"{len(report['errors'])} validation error(s); refusing to aggregate "
                "possibly-changed source data."
            )

        for warning in report["warnings"]:
            logger.warning(f"  ⚠ {warning}")

        self.validation_report = report
        return report

    # -- preprocess --------------------------------------------------------

    def preprocess(self):
        """Reconcile resolutions, aggregate to target cells, verify conservation."""
        frames = []

        for spec in self.specs:
            prepared = self.prepared[spec.layer_id]
            rename = self.renames[spec.layer_id]
            total_alias = rename.get(spec.total_field, spec.total_field)

            kept, reconciliation = reconcile_resolutions(prepared, total_alias)

            count_aliases = [rename.get(c, c) for c in spec.count_fields]
            aggregated = aggregate_to_target_cells(
                kept, count_aliases, self.anchor_x, self.anchor_y, self.cell_size_m
            )

            conservation = check_conservation(
                kept, aggregated, total_alias, spec.layer_id,
                reconciliation["dropped_total"], strict=True,
            )

            # Namespace columns per layer so themes never collide (several layers
            # each have a field aliased to "total").
            measures = [c for c in aggregated.columns if c not in ("grid_col", "grid_row")]
            aggregated = aggregated.rename(
                columns={c: f"{spec.layer_id}__{c}" for c in measures}
            )
            frames.append(aggregated)

            self.layer_reports.append({
                "layer_id": spec.layer_id,
                "theme": spec.theme,
                "source_file": str(spec.source_path),
                "description_sv": spec.description_sv,
                "description_en": spec.description_en,
                "source_cells": int(len(prepared)),
                "reconciliation": reconciliation,
                "conservation": conservation,
                "data_quality": self.quality[spec.layer_id],
                "non_additive_fields_excluded": list(spec.non_additive_fields),
                "field_translations": {
                    k: v for k, v in rename.items() if k in RUTA_FIELD_TRANSLATIONS
                },
            })

        merged = frames[0]
        for frame in frames[1:]:
            merged = merged.merge(frame, on=["grid_col", "grid_row"], how="outer")
        merged = merged.fillna(0)

        for column in merged.columns:
            if column not in ("grid_col", "grid_row"):
                merged[column] = merged[column].astype("int64")

        exporter = CellAttributeExporter(
            self.cfg.output_dir, self.anchor_x, self.anchor_y, self.cell_size_m
        )
        self.result = exporter.build_cell_geometries(merged)

        self.preprocessing_report = {
            "target_cells": int(len(self.result)),
            "layers": len(self.specs),
        }
        logger.info(
            f"Aggregated {len(self.specs)} layer(s) onto {len(self.result):,} "
            f"{self.cell_size_m}m cells"
        )
        return self.result

    # -- export ------------------------------------------------------------

    def export(self, output_dir: Path = None):
        """Write GeoPackage, Parquet, runtime JSON, metadata and QC table."""
        output_dir = Path(output_dir) if output_dir else self.cfg.output_dir
        exporter = CellAttributeExporter(
            output_dir, self.anchor_x, self.anchor_y, self.cell_size_m
        )

        name = f"cell_attributes_{self.cell_size_m}m"
        field_map = {
            k: v for k, v in RUTA_FIELD_TRANSLATIONS.items()
            if any(k in self.renames[s.layer_id] for s in self.specs)
        }
        metadata = build_metadata(
            self.cfg, self.layer_reports, field_map,
            self.anchor_x, self.anchor_y, self.cell_size_m,
        )

        qc_rows = [
            {
                "layer_id": r["layer_id"],
                "theme": r["theme"],
                "source_cells": r["source_cells"],
                "reconciliation_rule": r["reconciliation"]["rule"],
                "cells_kept": r["reconciliation"]["kept_cells"],
                "cells_dropped": r["reconciliation"]["dropped_cells"],
                "dropped_total": r["reconciliation"]["dropped_total"],
                "reconciled_source_total": r["conservation"]["reconciled_source_total"],
                "aggregated_target_total": r["conservation"]["aggregated_target_total"],
                "conserved": r["conservation"]["conserved"],
                "occupied_target_cells": r["conservation"]["occupied_target_cells"],
                "off_lattice_cells": r["data_quality"]["lattice"]["off_lattice_cells"],
                "duplicate_cell_codes": r["data_quality"]["duplicate_cell_codes"],
            }
            for r in self.layer_reports
        ]

        return exporter.export_all(
            self.result, name, metadata, qc_rows, self.cfg.export_formats
        )

    def run(self, output_dir: Path = None):
        """Execute the full pipeline."""
        self.load()
        self.validate()
        self.preprocess()
        return self.export(output_dir)
