"""Per-building height extraction and quality assessment from LiDAR."""

import logging
from pathlib import Path
from typing import Dict, List, Optional, Tuple
import uuid

import geopandas as gpd
import numpy as np
import laspy
from shapely.geometry import Point
from scipy import stats

from .config import HeightExtractionConfig, HeightSource, QualityLevel

logger = logging.getLogger(__name__)


class HeightEstimator:
    """Extract building heights from classified LiDAR point clouds."""
    
    def __init__(self, config: HeightExtractionConfig):
        """
        Initialize height estimator.
        
        Args:
            config: HeightExtractionConfig with extraction parameters
        """
        self.config = config
    
    def extract_heights_for_buildings(
        self,
        buildings_gdf: gpd.GeoDataFrame,
        laz_path: Path,
        height_run_id: str
    ) -> List[Dict]:
        """
        Extract heights for a set of buildings from a single LAZ tile.
        
        Args:
            buildings_gdf: GeoDataFrame subset of buildings intersecting this tile
            laz_path: Path to preprocessed LAZ with classification and HAG
            height_run_id: UUID linking all heights from this run
            
        Returns:
            List of height estimation dicts (one per building)
        """
        heights = []
        
        logger.info(f"Extracting heights from {laz_path.name} for {len(buildings_gdf)} buildings...")
        
        # Load classified LAZ once
        try:
            with laspy.open(laz_path) as src:
                las = src.read()
            
            logger.debug(
                f"  Loaded {len(las.points)} points from {laz_path.name}"
            )
        except Exception as e:
            logger.error(f"Failed to load LAZ file {laz_path}: {e}")
            raise
        
        # Process each building
        for idx, (_, building_row) in enumerate(buildings_gdf.iterrows()):
            try:
                height_dict = self._extract_height_for_building(
                    building_row,
                    las,
                    height_run_id
                )
                heights.append(height_dict)
                
                if (idx + 1) % 100 == 0:
                    logger.debug(f"  Processed {idx + 1}/{len(buildings_gdf)} buildings")
            
            except Exception as e:
                logger.warning(
                    f"Failed to extract height for building {idx}: {e}; "
                    f"using fallback"
                )
                # Create fallback entry
                building_id = building_row.get("object_id", f"unknown_{idx}")
                height_dict = self._create_fallback_height(
                    building_id,
                    height_run_id,
                    reason=f"extraction_error: {str(e)}"
                )
                heights.append(height_dict)
        
        logger.info(f"  Extracted heights for {len(heights)} buildings")
        
        return heights
    
    def _extract_height_for_building(
        self,
        building_row,
        las,
        height_run_id: str
    ) -> Dict:
        """
        Extract height for a single building.
        
        Args:
            building_row: GeoSeries row from buildings GeoDataFrame
            las: Loaded LAZ point cloud (laspy LasData object)
            height_run_id: Pipeline run UUID
            
        Returns:
            Dict with height_m, ground_z, roof_z, and quality metrics
        """
        building_id = building_row.get("object_id", "unknown")
        geometry = building_row.geometry
        
        # Find points within building footprint (with buffer for edge cases)
        buffer_dist = self.config.min_height_m  # Use min_height as buffer
        building_bounds = geometry.buffer(buffer_dist).bounds
        
        # Filter points by bounding box (fast)
        x_mask = (las.x >= building_bounds[0]) & (las.x <= building_bounds[2])
        y_mask = (las.y >= building_bounds[1]) & (las.y <= building_bounds[3])
        bbox_mask = x_mask & y_mask
        
        # Further filter by polygon intersection
        points_in_bbox = np.where(bbox_mask)[0]
        
        if len(points_in_bbox) == 0:
            return self._create_fallback_height(
                building_id,
                height_run_id,
                reason="no_points_in_bounds"
            )
        
        # Extract points intersecting building
        candidate_points = las.xyz[points_in_bbox]
        candidate_classes = las.classification[points_in_bbox]
        
        # Create point geometries for intersection test
        point_geoms = np.array([Point(p[:2]) for p in candidate_points])
        intersects = np.array([geometry.contains(Point(p[:2])) for p in candidate_points])
        
        # Get HAG values if available
        hag_values = None
        if "HeightAboveGround" in las.point_format.dimension_names:
            hag_values = las["HeightAboveGround"][points_in_bbox]
        
        all_points = candidate_points[intersects]
        all_classes = candidate_classes[intersects]
        
        if len(all_points) < self.config.min_points:
            return self._create_fallback_height(
                building_id,
                height_run_id,
                reason=f"insufficient_points: {len(all_points)}"
            )
        
        # Separate ground and non-ground points
        ground_mask = (all_classes == 2)  # ASPRS class 2 = ground
        non_ground_mask = ~ground_mask
        
        ground_points = all_points[ground_mask]
        non_ground_points = all_points[non_ground_mask]
        
        # Estimate ground elevation
        if len(ground_points) >= 3:
            # Use median for robustness against outliers
            ground_z = float(np.median(ground_points[:, 2]))
        else:
            # If insufficient ground points, use nearby points
            nearby_z_values = all_points[:, 2]
            ground_z = float(np.percentile(nearby_z_values, 5))  # 5th percentile as ground estimate
        
        # Extract height from non-ground points
        if hag_values is not None:
            # Use HAG if available (more accurate)
            non_ground_hag = hag_values[non_ground_mask]
            if len(non_ground_hag) > 0:
                # Use 95th percentile of HAG
                roof_hag = float(np.percentile(non_ground_hag, self.config.percentile))
            else:
                return self._create_fallback_height(
                    building_id,
                    height_run_id,
                    reason="no_non_ground_points"
                )
        else:
            # Fall back to z-coordinate difference
            if len(non_ground_points) > 0:
                height_estimates = non_ground_points[:, 2] - ground_z
                roof_hag = float(np.percentile(height_estimates, self.config.percentile))
            else:
                return self._create_fallback_height(
                    building_id,
                    height_run_id,
                    reason="no_non_ground_points"
                )
        
        # Apply constraints
        height_m = max(roof_hag, self.config.min_height_m)
        roof_z = ground_z + height_m
        
        # Compute coverage and variance for quality assessment
        building_area = geometry.area
        # Approximate: count points as ~0.2m diameter circles
        point_coverage_area = len(all_points) * (np.pi * 0.1**2)
        coverage_ratio = min(1.0, point_coverage_area / building_area) if building_area > 0 else 0.0
        
        # Compute z-coordinate variance
        if len(non_ground_points) > 1:
            z_variance = float(np.var(non_ground_points[:, 2]))
        else:
            z_variance = 0.0
        
        # Classify quality
        quality = self._classify_quality(
            point_count=len(all_points),
            ground_count=len(ground_points),
            non_ground_count=len(non_ground_points),
            coverage_ratio=coverage_ratio,
            z_variance=z_variance
        )
        
        return {
            "building_id": building_id,
            "ground_z": round(ground_z, 3),
            "roof_z": round(roof_z, 3),
            "height_m": round(height_m, 3),
            "height_source": HeightSource.LIDAR_HAG_P95.value,
            "height_quality": quality.value,
            "height_point_count": len(all_points),
            "ground_point_count": len(ground_points),
            "non_ground_point_count": len(non_ground_points),
            "coverage_ratio": round(coverage_ratio, 3),
            "z_variance": round(z_variance, 3),
            "height_run_id": height_run_id
        }
    
    def _classify_quality(
        self,
        point_count: int,
        ground_count: int,
        non_ground_count: int,
        coverage_ratio: float,
        z_variance: float
    ) -> QualityLevel:
        """
        Classify height estimation quality.
        
        Args:
            point_count: Total points in building
            ground_count: Ground-classified points
            non_ground_count: Non-ground-classified points
            coverage_ratio: Fraction of building area with points
            z_variance: Variance of z-coordinates
            
        Returns:
            QualityLevel (high, medium, or low)
        """
        if (
            point_count >= self.config.high_quality_min_points
            and coverage_ratio >= self.config.high_quality_min_coverage
            and z_variance <= self.config.high_quality_max_variance
        ):
            return QualityLevel.HIGH
        
        elif (
            point_count >= self.config.medium_quality_min_points
            and coverage_ratio >= self.config.medium_quality_min_coverage
        ):
            return QualityLevel.MEDIUM
        
        else:
            return QualityLevel.LOW
    
    def _create_fallback_height(
        self,
        building_id: str,
        height_run_id: str,
        reason: str = "unknown"
    ) -> Dict:
        """
        Create a fallback height entry for a building with insufficient coverage.
        
        Args:
            building_id: Building identifier
            height_run_id: Pipeline run UUID
            reason: Reason for fallback (logged)
            
        Returns:
            Dict with fallback height and quality flags
        """
        logger.debug(f"Fallback for {building_id}: {reason}")
        
        return {
            "building_id": building_id,
            "ground_z": None,
            "roof_z": None,
            "height_m": round(self.config.fallback_height_m, 3),
            "height_source": HeightSource.FALLBACK_DEFAULT.value,
            "height_quality": QualityLevel.LOW.value,
            "height_point_count": 0,
            "ground_point_count": 0,
            "non_ground_point_count": 0,
            "coverage_ratio": 0.0,
            "z_variance": None,
            "height_run_id": height_run_id,
            "fallback_reason": reason
        }
