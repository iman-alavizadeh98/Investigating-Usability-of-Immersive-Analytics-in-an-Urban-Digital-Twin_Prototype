"""Main LiDAR height estimation pipeline orchestrator."""

import logging
import shutil
from pathlib import Path
from typing import Dict, Optional

import geopandas as gpd

from pipelines.base import BasePipeline
from .config import LiDARHeightPipelineConfig
from .tile_index import TileIndex
from .pdal_pipelines import PDALPipelineGenerator
from .height_estimation import HeightEstimator
from .export import HeightExporter

logger = logging.getLogger(__name__)


class LiDARHeightPipeline(BasePipeline):
    """
    Modular pipeline for enriching buildings with LiDAR-derived heights.
    
    Inherits from BasePipeline and follows the load → validate → preprocess → export pattern.
    """
    
    def __init__(self, config: LiDARHeightPipelineConfig):
        """
        Initialize LiDAR height estimation pipeline.
        
        Args:
            config: LiDARHeightPipelineConfig with all settings
        """
        self.config = config
        self.buildings_gdf: Optional[gpd.GeoDataFrame] = None
        self.tile_index: Optional[TileIndex] = None
        self.enriched_heights = []
        
        # Set up logging
        if config.verbose:
            logging.basicConfig(level=logging.DEBUG)
        else:
            logging.basicConfig(level=logging.INFO)
    
    def load(self) -> None:
        """Load processed buildings from GeoPackage."""
        logger.info(f"Loading buildings from {self.config.input_buildings_path}...")
        
        try:
            self.buildings_gdf = gpd.read_file(
                self.config.input_buildings_path
            )
            logger.info(f"  Loaded {len(self.buildings_gdf)} buildings")
        
        except Exception as e:
            logger.error(f"Failed to load buildings: {e}")
            raise
    
    def validate(self) -> Dict:
        """
        Validate inputs: CRS, geometry, tile coverage, LiDAR availability.
        
        Returns:
            Dict with validation status and statistics
        """
        logger.info("Validating pipeline inputs...")
        
        validation_report = {
            "status": "valid",
            "issues": [],
            "warnings": []
        }
        
        # Check buildings loaded
        if self.buildings_gdf is None or len(self.buildings_gdf) == 0:
            validation_report["status"] = "invalid"
            validation_report["issues"].append("No buildings loaded")
            return validation_report
        
        # Validate CRS
        if self.buildings_gdf.crs is None:
            validation_report["status"] = "invalid"
            validation_report["issues"].append("Buildings have undefined CRS")
        elif str(self.buildings_gdf.crs) != self.config.crs:
            validation_report["status"] = "invalid"
            validation_report["issues"].append(
                f"CRS mismatch: buildings are {self.buildings_gdf.crs}, "
                f"expected {self.config.crs}"
            )
        
        # Validate geometry
        invalid_geoms = (~self.buildings_gdf.geometry.is_valid).sum()
        if invalid_geoms > 0:
            validation_report["warnings"].append(
                f"{invalid_geoms} buildings with invalid geometry"
            )
        
        # Validate required columns
        required_cols = ["object_id", "geometry"]
        missing_cols = [c for c in required_cols if c not in self.buildings_gdf.columns]
        if missing_cols:
            validation_report["status"] = "invalid"
            validation_report["issues"].append(
                f"Missing required columns: {missing_cols}"
            )
        
        # Check LiDAR directory exists
        if not self.config.lidar_directory.exists():
            validation_report["status"] = "invalid"
            validation_report["issues"].append(
                f"LiDAR directory not found: {self.config.lidar_directory}"
            )
        
        # Check LiDAR tiles exist
        laz_files = list(self.config.lidar_directory.glob("*.laz"))
        if len(laz_files) == 0:
            validation_report["status"] = "invalid"
            validation_report["issues"].append(
                f"No LAZ files found in {self.config.lidar_directory}"
            )
        
        # Compute tile coverage
        if validation_report["status"] != "invalid":
            try:
                self.tile_index = TileIndex(
                    self.buildings_gdf,
                    self.config.tile_index_config
                )
                coverage_stats = self.tile_index.validate()
                validation_report["coverage"] = coverage_stats
                
                if coverage_stats["untiled_count"] > 0:
                    validation_report["warnings"].append(
                        f"{coverage_stats['untiled_count']} buildings not covered by tiles"
                    )
            except Exception as e:
                logger.warning(f"Failed to compute tile coverage: {e}")
        
        logger.info(f"Validation: {validation_report['status']}")
        for issue in validation_report["issues"]:
            logger.error(f"  ERROR: {issue}")
        for warning in validation_report["warnings"]:
            logger.warning(f"  WARNING: {warning}")
        
        return validation_report
    
    def preprocess(self) -> None:
        """
        Main preprocessing workflow: tile indexing, PDAL processing, height extraction.
        """
        logger.info("Starting preprocessing...")
        
        if self.buildings_gdf is None:
            raise RuntimeError("Buildings not loaded; call load() first")
        
        if self.tile_index is None:
            self.tile_index = TileIndex(
                self.buildings_gdf,
                self.config.tile_index_config
            )
        
        # Create output directories
        self.config.output_directory.mkdir(parents=True, exist_ok=True)
        self.config.temp_directory.mkdir(parents=True, exist_ok=True)
        
        logger.info(f"Temp directory: {self.config.temp_directory}")
        
        # Initialize processors
        pdal_gen = PDALPipelineGenerator(self.config.pdal_config)
        height_estimator = HeightEstimator(self.config.height_extraction_config)
        
        # Get building-to-tile mapping
        buildings_to_tiles = self.tile_index.get_tiles_for_buildings()
        
        logger.info(f"Processing {len(buildings_to_tiles)} tiles...")
        
        # Process each tile
        for tile_idx, (tile_name, building_indices) in enumerate(buildings_to_tiles.items(), 1):
            logger.info(
                f"[{tile_idx}/{len(buildings_to_tiles)}] Processing {tile_name} "
                f"({len(building_indices)} buildings)..."
            )
            
            try:
                # Input LAZ file
                input_laz = self.config.lidar_directory / tile_name
                
                if not input_laz.exists():
                    logger.warning(f"LAZ file not found: {input_laz}")
                    continue
                
                # Preprocess with PDAL
                classified_laz = pdal_gen.preprocess_tile(
                    input_laz,
                    self.config.temp_directory
                )
                
                # Get buildings for this tile
                tile_buildings = self.buildings_gdf.iloc[building_indices]
                
                # Extract heights
                tile_heights = height_estimator.extract_heights_for_buildings(
                    tile_buildings,
                    classified_laz,
                    self.config.height_run_id
                )
                
                self.enriched_heights.extend(tile_heights)
                
                # Clean up classified LAZ
                try:
                    classified_laz.unlink()
                    logger.debug(f"  Cleaned up {classified_laz.name}")
                except Exception as e:
                    logger.warning(f"Failed to delete {classified_laz}: {e}")
            
            except Exception as e:
                logger.error(f"Failed to process tile {tile_name}: {e}")
                # Continue with other tiles; use fallback heights for this tile
                tile_buildings = self.buildings_gdf.iloc[building_indices]
                for _, building in tile_buildings.iterrows():
                    fallback = height_estimator._create_fallback_height(
                        building.get("object_id", "unknown"),
                        self.config.height_run_id,
                        reason=f"tile_processing_error: {str(e)}"
                    )
                    self.enriched_heights.append(fallback)
        
        logger.info(f"Preprocessing complete: extracted {len(self.enriched_heights)} building heights")
    
    def export(self, output_dir: Optional[Path] = None) -> Dict:
        """
        Export enriched buildings to GeoPackage, Parquet, and QC CSV.
        
        Args:
            output_dir: Override output directory (default: config.output_directory)
            
        Returns:
            Dict with export summary and paths
        """
        if output_dir is None:
            output_dir = self.config.output_directory
        
        logger.info("Exporting enriched buildings...")
        
        if self.buildings_gdf is None:
            raise RuntimeError("Buildings not loaded")
        
        if len(self.enriched_heights) == 0:
            logger.warning("No heights extracted; creating empty outputs")
        
        export_result = HeightExporter.write_enriched_buildings(
            self.enriched_heights,
            self.buildings_gdf,
            output_dir,
            self.config
        )
        
        return export_result
    
    def run(self, output_dir: Optional[Path] = None) -> Dict:
        """
        Execute full pipeline: load → validate → preprocess → export.
        
        Args:
            output_dir: Override output directory
            
        Returns:
            Dict with full pipeline report
        """
        logger.info("=" * 80)
        logger.info("LiDAR Height Estimation Pipeline")
        logger.info(f"Run ID: {self.config.height_run_id}")
        logger.info(f"Start time: {self.config.run_timestamp}")
        logger.info("=" * 80)
        
        report = {
            "run_id": self.config.height_run_id,
            "status": "success",
            "stages": {}
        }
        
        try:
            # Stage 1: Load
            logger.info("\n[STAGE 1/4] LOAD")
            self.load()
            report["stages"]["load"] = "complete"
            
            # Stage 2: Validate
            logger.info("\n[STAGE 2/4] VALIDATE")
            validation_result = self.validate()
            report["stages"]["validate"] = validation_result
            
            if validation_result["status"] == "invalid":
                logger.error("Validation failed; aborting pipeline")
                report["status"] = "failed"
                return report
            
            # Stage 3: Preprocess
            logger.info("\n[STAGE 3/4] PREPROCESS")
            self.preprocess()
            report["stages"]["preprocess"] = {
                "heights_extracted": len(self.enriched_heights)
            }
            
            # Stage 4: Export
            logger.info("\n[STAGE 4/4] EXPORT")
            export_result = self.export(output_dir)
            report["stages"]["export"] = export_result
            
            logger.info("\n" + "=" * 80)
            logger.info("Pipeline Complete ✓")
            logger.info("=" * 80)
        
        except Exception as e:
            logger.error(f"Pipeline failed: {e}", exc_info=True)
            report["status"] = "failed"
            report["error"] = str(e)
        
        finally:
            # Clean up temp directory
            if self.config.temp_directory.exists():
                try:
                    shutil.rmtree(self.config.temp_directory)
                    logger.info(f"Cleaned up temp directory")
                except Exception as e:
                    logger.warning(f"Failed to clean up temp directory: {e}")
        
        return report
