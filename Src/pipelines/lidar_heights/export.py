"""Export enriched building heights to output formats."""

import logging
from pathlib import Path
from typing import Dict, List, Optional
import json
from datetime import datetime

import geopandas as gpd
import pandas as pd
import numpy as np

logger = logging.getLogger(__name__)


class HeightExporter:
    """Export enriched buildings with heights to GeoPackage, Parquet, and QC CSV."""
    
    @staticmethod
    def write_enriched_buildings(
        enriched_heights: List[Dict],
        original_buildings_gdf: gpd.GeoDataFrame,
        output_directory: Path,
        config
    ) -> Dict:
        """
        Write enriched buildings to output files.
        
        Produces three outputs:
        1. GeoPackage with all buildings, all original columns + height fields
        2. Parquet for downstream analytics
        3. CSV for QC review
        
        Args:
            enriched_heights: List of height dicts from height_estimation
            original_buildings_gdf: Original buildings GeoDataFrame
            output_directory: Path for output files
            config: LiDARHeightPipelineConfig with CRS and metadata
            
        Returns:
            Dict with export summary and statistics
        """
        output_directory = Path(output_directory)
        output_directory.mkdir(parents=True, exist_ok=True)
        
        logger.info("Preparing enriched buildings for export...")
        
        # Convert enriched heights to DataFrame
        heights_df = pd.DataFrame(enriched_heights)
        
        # Validate completeness
        if len(heights_df) != len(original_buildings_gdf):
            logger.warning(
                f"Height count mismatch: {len(heights_df)} heights vs "
                f"{len(original_buildings_gdf)} original buildings"
            )
        
        # Ensure building_id is in original for joining
        if "object_id" not in original_buildings_gdf.columns:
            logger.error("Original buildings must have 'object_id' column")
            raise ValueError("Original buildings must have 'object_id' column")
        
        # Join heights back to original buildings
        # Use left join to preserve all original buildings
        enriched_gdf = original_buildings_gdf.copy()
        enriched_gdf = enriched_gdf.merge(
            heights_df,
            left_on="object_id",
            right_on="building_id",
            how="left"
        )
        
        # Verify row count
        if len(enriched_gdf) != len(original_buildings_gdf):
            logger.error(
                f"Row count mismatch after join: "
                f"{len(enriched_gdf)} vs {len(original_buildings_gdf)}"
            )
            raise ValueError("Row count mismatch after joining heights")
        
        # Ensure height_m is never NaN; fill with fallback if needed
        if "height_m" in enriched_gdf.columns:
            nan_heights = enriched_gdf["height_m"].isna().sum()
            if nan_heights > 0:
                logger.warning(
                    f"Filling {nan_heights} NaN height values with fallback height"
                )
                enriched_gdf.loc[enriched_gdf["height_m"].isna(), "height_m"] = config.height_extraction_config.fallback_height_m
        
        # 1. Export GeoPackage
        gpkg_path = output_directory / "buildings_with_heights.gpkg"
        logger.info(f"Writing GeoPackage to {gpkg_path.name}...")
        
        enriched_gdf.to_file(
            gpkg_path,
            layer="buildings_with_heights",
            driver="GPKG",
            crs=config.crs
        )
        
        logger.info(f"  GeoPackage: {len(enriched_gdf)} buildings, CRS={config.crs}")
        
        # 2. Export Parquet (convert geometry to WKT for storage)
        parquet_path = output_directory / "buildings_with_heights.parquet"
        logger.info(f"Writing Parquet to {parquet_path.name}...")
        
        parquet_df = enriched_gdf.copy()
        parquet_df["geometry_wkt"] = parquet_df.geometry.to_wkt()
        parquet_df_export = parquet_df.drop(columns=["geometry"])
        
        parquet_df_export.to_parquet(
            parquet_path,
            index=False,
            engine="pyarrow"
        )
        
        logger.info(f"  Parquet: {len(parquet_df_export)} rows")
        
        # 3. Export QC CSV
        qc_path = output_directory / "building_height_qc.csv"
        logger.info(f"Writing QC CSV to {qc_path.name}...")
        
        qc_cols = [
            "object_id",
            "height_m",
            "height_source",
            "height_quality",
            "height_point_count",
            "ground_point_count",
            "non_ground_point_count",
            "coverage_ratio",
            "z_variance",
            "ground_z",
            "roof_z",
            "height_run_id"
        ]
        
        # Only include columns that exist
        qc_cols = [c for c in qc_cols if c in enriched_gdf.columns]
        
        qc_df = enriched_gdf[qc_cols].copy()
        qc_df.to_csv(qc_path, index=False)
        
        logger.info(f"  QC CSV: {len(qc_df)} rows")
        
        # Generate summary statistics
        summary = HeightExporter._compute_summary_stats(enriched_gdf, config)
        
        logger.info("Export complete.")
        
        return {
            "gpkg_path": str(gpkg_path),
            "parquet_path": str(parquet_path),
            "qc_csv_path": str(qc_path),
            "summary": summary
        }
    
    @staticmethod
    def _compute_summary_stats(enriched_gdf: gpd.GeoDataFrame, config) -> Dict:
        """
        Compute summary statistics for enriched heights.
        
        Args:
            enriched_gdf: Enriched GeoDataFrame
            config: Pipeline configuration
            
        Returns:
            Dict with statistics
        """
        if "height_m" not in enriched_gdf.columns:
            return {}
        
        height_m = enriched_gdf["height_m"]
        
        # Quality distribution
        quality_dist = {}
        if "height_quality" in enriched_gdf.columns:
            quality_dist = enriched_gdf["height_quality"].value_counts().to_dict()
        
        # Height statistics
        stats = {
            "total_buildings": len(enriched_gdf),
            "height_statistics": {
                "min_m": round(float(height_m.min()), 3),
                "max_m": round(float(height_m.max()), 3),
                "mean_m": round(float(height_m.mean()), 3),
                "median_m": round(float(height_m.median()), 3),
                "std_m": round(float(height_m.std()), 3),
                "percentile_25_m": round(float(height_m.quantile(0.25)), 3),
                "percentile_75_m": round(float(height_m.quantile(0.75)), 3)
            },
            "quality_distribution": quality_dist,
            "export_timestamp": datetime.utcnow().isoformat()
        }
        
        # Source distribution
        if "height_source" in enriched_gdf.columns:
            stats["source_distribution"] = enriched_gdf["height_source"].value_counts().to_dict()
        
        # Point coverage statistics
        if "height_point_count" in enriched_gdf.columns:
            point_counts = enriched_gdf["height_point_count"]
            stats["point_coverage"] = {
                "median_points_per_building": float(point_counts.median()),
                "mean_points_per_building": round(float(point_counts.mean()), 1),
                "max_points_per_building": int(point_counts.max()),
                "zero_point_buildings": int((point_counts == 0).sum())
            }
        
        logger.info(f"\nExport Summary:")
        logger.info(f"  Total buildings: {stats['total_buildings']}")
        logger.info(f"  Height range: {stats['height_statistics']['min_m']}-{stats['height_statistics']['max_m']} m")
        logger.info(f"  Mean height: {stats['height_statistics']['mean_m']} m")
        if quality_dist:
            logger.info(f"  Quality distribution: {quality_dist}")
        if "source_distribution" in stats:
            logger.info(f"  Source distribution: {stats['source_distribution']}")
        
        return stats
