"""Export buildings with heights, a QC table, the buildings without height, and a summary."""

import json
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional

import geopandas as gpd
import pandas as pd

logger = logging.getLogger(__name__)

HEIGHT_COLUMNS = [
    "height_m", "has_height", "height_source", "height_quality",
    "no_height_reason", "no_height_reason_code",
    "ground_z", "ground_z_min", "roof_z",
    "height_p50_m", "height_max_m",
    "height_surface_2018_m", "height_lidar_2010_m", "height_change_flag",
    "surface_point_count", "surface_fill_ratio", "lidar_point_count", "ground_point_count",
    "height_run_id",
]


class HeightExporter:

    @staticmethod
    def write_enriched_buildings(
        heights: List[Dict],
        buildings_gdf: gpd.GeoDataFrame,
        output_directory: Path,
        config,
        validation_report: Optional[Dict] = None,
        sources: Optional[Dict] = None,
    ) -> Dict:
        """
        Outputs in output_directory:
          buildings_lidar_added.gpkg / .parquet  all building columns + height columns
          building_lidar_qc.csv                  object_id + height columns
          buildings_without_height.csv           buildings with has_height = False
          lidar_heights_summary.json             counts, reasons, statistics, config
        """
        output_directory = Path(output_directory)
        output_directory.mkdir(parents=True, exist_ok=True)

        heights_df = pd.DataFrame(heights)
        if heights_df["building_id"].duplicated().any():
            raise ValueError("More than one height per building")
        missing = set(buildings_gdf["object_id"]) - set(heights_df["building_id"])
        if missing:
            raise ValueError(f"{len(missing)} buildings have no height result")
        heights_df["no_height_reason_code"] = (
            heights_df["no_height_reason"].astype("string").str.split(":").str[0].str.strip()
        )

        gdf = buildings_gdf.merge(
            heights_df.rename(columns={"building_id": "object_id"}), on="object_id", how="left"
        )
        if len(gdf) != len(buildings_gdf):
            raise ValueError("Row count changed when joining heights")
        gdf["has_height"] = gdf["has_height"].astype(bool)
        gdf["height_change_flag"] = gdf["height_change_flag"].astype(bool)
        for col in ["height_m", "ground_z", "ground_z_min", "roof_z", "height_p50_m", "height_max_m",
                    "height_surface_2018_m", "height_lidar_2010_m", "surface_fill_ratio"]:
            gdf[col] = pd.to_numeric(gdf[col], errors="coerce").astype("float64")

        gpkg_path = output_directory / "buildings_lidar_added.gpkg"
        gdf.to_file(gpkg_path, layer="buildings_lidar_added", driver="GPKG")
        logger.info(f"  {gpkg_path.name}: {len(gdf):,} buildings")

        parquet_path = output_directory / "buildings_lidar_added.parquet"
        try:
            pq = pd.DataFrame(gdf.drop(columns="geometry"))
            pq["geometry_wkt"] = gdf.geometry.to_wkt()
            pq.to_parquet(parquet_path, index=False, engine="pyarrow")
        except ImportError:
            logger.warning("  Parquet skipped: pyarrow not installed")
            parquet_path = None

        qc_path = output_directory / "building_lidar_qc.csv"
        pd.DataFrame(gdf[["object_id"] + HEIGHT_COLUMNS]).to_csv(qc_path, index=False)

        missing_path = output_directory / "buildings_without_height.csv"
        info_cols = [c for c in ["object_type", "object_type_en", "primary_purpose_en", "footprint_area_m2"]
                     if c in gdf.columns]
        no_height = gdf.loc[~gdf["has_height"]]
        missing_df = pd.DataFrame(no_height[[
            "object_id", "no_height_reason_code", "no_height_reason", *info_cols,
            "height_surface_2018_m", "height_lidar_2010_m", "surface_point_count",
            "surface_fill_ratio", "lidar_point_count",
        ]])
        centroids = no_height.geometry.centroid
        missing_df["centroid_x"] = centroids.x.round(2)
        missing_df["centroid_y"] = centroids.y.round(2)
        missing_df.to_csv(missing_path, index=False)

        summary = HeightExporter._summary(gdf, config, validation_report, sources)
        summary_path = output_directory / "lidar_heights_summary.json"
        with open(summary_path, "w", encoding="utf-8") as f:
            json.dump(summary, f, indent=2, ensure_ascii=False, default=str)
        HeightExporter._log_summary(summary)

        return {
            "gpkg_path": str(gpkg_path),
            "parquet_path": str(parquet_path) if parquet_path else None,
            "qc_csv_path": str(qc_path),
            "without_height_csv_path": str(missing_path),
            "summary_path": str(summary_path),
            "summary": summary,
        }

    @staticmethod
    def _stats(values: pd.Series) -> Dict:
        values = values.dropna()
        if values.empty:
            return {}
        return {
            "count": int(len(values)),
            "min_m": round(float(values.min()), 3),
            "p25_m": round(float(values.quantile(0.25)), 3),
            "median_m": round(float(values.median()), 3),
            "mean_m": round(float(values.mean()), 3),
            "p75_m": round(float(values.quantile(0.75)), 3),
            "max_m": round(float(values.max()), 3),
        }

    @staticmethod
    def _summary(gdf: gpd.GeoDataFrame, config, validation_report, sources) -> Dict:
        total = len(gdf)
        has = gdf["has_height"]
        n_with = int(has.sum())
        both = gdf.dropna(subset=["height_surface_2018_m", "height_lidar_2010_m"])
        diff = both["height_surface_2018_m"] - both["height_lidar_2010_m"]
        hc = config.height_config
        return {
            "run_id": config.height_run_id,
            "run_timestamp": config.run_timestamp,
            "export_timestamp": datetime.now(timezone.utc).isoformat(),
            "input": str(config.input_buildings_path),
            "sources": sources or {},
            "total_buildings": total,
            "buildings_with_height": n_with,
            "buildings_without_height": total - n_with,
            "percent_without_height": round(100.0 * (total - n_with) / total, 2) if total else 0.0,
            "fallback_height_m": hc.fallback_height_m,
            "without_height_by_reason": gdf.loc[~has, "no_height_reason_code"].value_counts().to_dict(),
            "by_source": gdf["height_source"].value_counts().to_dict(),
            "by_quality": gdf["height_quality"].value_counts().to_dict(),
            "height_change_flag_count": int(gdf["height_change_flag"].sum()),
            "height_statistics": HeightExporter._stats(gdf.loc[has, "height_m"]),
            "surface_2018_vs_lidar_2010": {
                "buildings_with_both": int(len(both)),
                "median_difference_m": round(float(diff.median()), 3) if len(both) else None,
                "within_1m_pct": round(100.0 * float((diff.abs() <= 1).mean()), 1) if len(both) else None,
                "within_2m_pct": round(100.0 * float((diff.abs() <= 2).mean()), 1) if len(both) else None,
            },
            "method": {
                "height": f"p{hc.percentile:g} of (z - ground) inside the footprint",
                "ground": "linear (Delaunay) interpolation of 2010 laser ground points "
                          f"(classes {list(hc.ground_classes)}) within {list(hc.ground_search_m)} m; "
                          f"points inside the footprint + {hc.ground_exclude_buffer_m} m ignored",
                "order": "2018 surface; 2010 laser only where the 2018 surface has too few points; "
                         "else no height",
                "config": {k: (list(v) if isinstance(v, tuple) else v) for k, v in vars(hc).items()},
            },
            "validation": validation_report or {},
        }

    @staticmethod
    def _log_summary(s: Dict) -> None:
        logger.info("Summary:")
        logger.info(f"  Buildings: {s['total_buildings']:,}")
        logger.info(
            f"  With height: {s['buildings_with_height']:,}; without: {s['buildings_without_height']:,} "
            f"({s['percent_without_height']}%, height_m = {s['fallback_height_m']})"
        )
        logger.info(f"  Without height, by reason: {s['without_height_by_reason']}")
        logger.info(f"  By source: {s['by_source']}")
        logger.info(f"  By quality: {s['by_quality']}")
        logger.info(f"  2018 vs 2010 differ by more than the threshold: {s['height_change_flag_count']:,}")
        if s["height_statistics"]:
            st = s["height_statistics"]
            logger.info(f"  Height: min {st['min_m']}, median {st['median_m']}, max {st['max_m']} m")
