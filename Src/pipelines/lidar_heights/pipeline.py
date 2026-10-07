"""Building height pipeline: load -> validate -> preprocess (heights) -> export."""

import logging
import math
import time
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Optional

import geopandas as gpd
import shapely

from pipelines.base import BasePipeline
from .config import LiDARHeightPipelineConfig
from .export import HeightExporter
from .height_estimation import HeightEstimator
from .sources import TileSource

logger = logging.getLogger(__name__)


class LiDARHeightPipeline(BasePipeline):
    """
    One height per building from a surface model and laser data.

    Buildings are grouped into square work cells by centroid. For each cell the
    points of its area (plus the ground search distance) are read from every
    tile they lie in, so each building is processed exactly once with all of
    its points, also on tile edges.
    """

    def __init__(self, config: LiDARHeightPipelineConfig):
        self.config = config
        self.buildings_gdf: Optional[gpd.GeoDataFrame] = None
        self.lidar: Optional[TileSource] = None
        self.surface: Optional[TileSource] = None
        self.heights: List[Dict] = []
        self.validation_report: Dict = {}
        logging.basicConfig(level=logging.DEBUG if config.verbose else logging.INFO)

    # -- stages ---------------------------------------------------------------

    def load(self) -> None:
        cfg = self.config
        logger.info(f"Loading buildings from {cfg.input_buildings_path}")
        self.buildings_gdf = gpd.read_file(cfg.input_buildings_path, layer=cfg.input_layer)
        logger.info(f"  {len(self.buildings_gdf):,} buildings")
        self.lidar = TileSource(cfg.lidar_directory, "Laser data (Laserdata NH)")
        if cfg.surface_path is not None:
            self.surface = TileSource(cfg.surface_path, "Surface model (Ytmodell)")
        else:
            logger.warning("No surface model given: heights come from the laser data only")

    def validate(self) -> Dict:
        report = {"status": "valid", "issues": [], "warnings": []}
        gdf = self.buildings_gdf
        if gdf is None or len(gdf) == 0:
            report["status"] = "invalid"
            report["issues"].append("No buildings loaded")
            self.validation_report = report
            return report

        if gdf.crs is None or str(gdf.crs) != self.config.crs:
            report["status"] = "invalid"
            report["issues"].append(f"CRS is {gdf.crs}, expected {self.config.crs}")
        if "object_id" not in gdf.columns:
            report["status"] = "invalid"
            report["issues"].append("Missing column object_id")
        else:
            # Heights are joined back on object_id, so it must be unique.
            repeated = int(gdf["object_id"].duplicated().sum())
            if repeated:
                report["status"] = "invalid"
                report["issues"].append(
                    f"{repeated} repeated object_id rows; run the buildings pipeline "
                    f"with --postprocess and use its output"
                )
        invalid = int((~gdf.geometry.is_valid).sum())
        if invalid:
            report["warnings"].append(f"{invalid} buildings with invalid geometry")

        for label, source in (("lidar", self.lidar), ("surface", self.surface)):
            if source is None:
                continue
            outside = int((~gdf.geometry.intersects(shapely.box(*source.extent))).sum())
            report[f"{label}_tiles"] = len(source.tiles)
            report[f"{label}_buildings_outside_extent"] = outside
            report[f"{label}_capture_dates"] = source.capture_dates()
            if outside:
                report["warnings"].append(f"{outside} buildings outside the {label} tile extent")

        # The rules treat the surface as the newer source (see config.py).
        s_dates = report.get("surface_capture_dates")
        l_dates = report.get("lidar_capture_dates")
        if s_dates and l_dates and s_dates["to"] < l_dates["from"]:
            report["warnings"].append(
                f"Surface model ({s_dates['from']}..{s_dates['to']}) is older than the laser data "
                f"({l_dates['from']}..{l_dates['to']}); the rules assume the opposite. "
                f"Consider --no-surface."
            )

        logger.info(f"Validation: {report['status']}")
        for issue in report["issues"]:
            logger.error(f"  ERROR: {issue}")
        for warning in report["warnings"]:
            logger.warning(f"  WARNING: {warning}")
        self.validation_report = report
        return report

    def preprocess(self) -> None:
        cfg = self.config
        hc = cfg.height_config
        estimator = HeightEstimator(hc)
        gdf = self.buildings_gdf
        margin = max(hc.ground_search_m) + 1.0

        centroids = gdf.geometry.centroid
        cells = defaultdict(list)
        for i, (cx, cy) in enumerate(zip(centroids.x, centroids.y)):
            cells[(math.floor(cx / cfg.work_cell_m), math.floor(cy / cfg.work_cell_m))].append(i)
        logger.info(f"Processing {len(gdf):,} buildings in {len(cells)} cells of {cfg.work_cell_m:.0f} m")

        started = time.time()
        for n_cell, (cell, rows) in enumerate(sorted(cells.items()), 1):
            subset = gdf.iloc[rows]
            minx, miny, maxx, maxy = subset.total_bounds
            region = (minx - margin, miny - margin, maxx + margin, maxy + margin)

            lidar_pts = self.lidar.read_region(region)
            ground = lidar_pts.where_class(hc.ground_classes)
            lidar_roof = lidar_pts.where_class(hc.lidar_roof_classes)
            surface = self.surface.read_region(region) if self.surface is not None else None
            del lidar_pts

            for oid, geom in zip(subset["object_id"], subset.geometry):
                touches_data = self.lidar.tiles_intersecting(geom.bounds) or (
                    self.surface is not None and self.surface.tiles_intersecting(geom.bounds)
                )
                if not touches_data:
                    self.heights.append(estimator.no_tile_result(oid, cfg.height_run_id))
                    continue
                try:
                    self.heights.append(estimator.estimate(
                        oid, geom, ground, lidar_roof, surface, cfg.height_run_id
                    ))
                except Exception as e:  # noqa: BLE001 - recorded per building
                    logger.warning(f"  {oid}: {e}")
                    self.heights.append(estimator.error_result(oid, cfg.height_run_id, e))

            done = len(self.heights)
            logger.info(
                f"[{n_cell}/{len(cells)}] cell {cell}: {len(rows):,} buildings "
                f"(surface {len(surface) if surface is not None else 0:,} pts, "
                f"ground {len(ground):,}, laser roof {len(lidar_roof):,}) "
                f"- {done:,}/{len(gdf):,} done, {time.time() - started:.0f} s"
            )

    def export(self, output_dir: Optional[Path] = None) -> Dict:
        output_dir = Path(output_dir or self.config.output_directory)
        sources = {"lidar": {
            "path": str(self.config.lidar_directory),
            "capture_dates": self.lidar.capture_dates() if self.lidar else None,
        }}
        if self.config.surface_path is not None:
            sources["surface"] = {
                "path": str(self.config.surface_path),
                "capture_dates": self.surface.capture_dates() if self.surface else None,
            }
        return HeightExporter.write_enriched_buildings(
            self.heights, self.buildings_gdf, output_dir, self.config,
            validation_report=self.validation_report, sources=sources,
        )

    def run(self, output_dir: Optional[Path] = None) -> Dict:
        report = {"run_id": self.config.height_run_id, "status": "success", "stages": {}}
        try:
            logger.info("[1/4] LOAD")
            self.load()
            logger.info("[2/4] VALIDATE")
            validation = self.validate()
            report["stages"]["validate"] = validation
            if validation["status"] == "invalid":
                report["status"] = "failed"
                report["error"] = "; ".join(validation["issues"])
                return report
            logger.info("[3/4] HEIGHTS")
            self.preprocess()
            report["stages"]["preprocess"] = {"buildings": len(self.heights)}
            logger.info("[4/4] EXPORT")
            report["stages"]["export"] = self.export(output_dir)
        except Exception as e:  # noqa: BLE001
            logger.error(f"Pipeline failed: {e}", exc_info=True)
            report["status"] = "failed"
            report["error"] = str(e)
        return report
