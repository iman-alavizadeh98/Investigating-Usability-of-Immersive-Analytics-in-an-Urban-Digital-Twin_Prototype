"""Spatial indexing of buildings to LiDAR tiles."""

import logging
from pathlib import Path
from typing import Dict, List, Optional, Tuple
import json

import geopandas as gpd
import numpy as np
from shapely.geometry import box
import laspy

from .config import TileIndexConfig

logger = logging.getLogger(__name__)


class TileIndex:
    """Maps building footprints to intersecting LiDAR tiles."""
    
    def __init__(
        self,
        buildings_gdf: gpd.GeoDataFrame,
        config: TileIndexConfig
    ):
        """
        Initialize tile index.
        
        Args:
            buildings_gdf: GeoDataFrame with building footprints (must have geometry column)
            config: TileIndexConfig with tile directory and CRS
        """
        self.buildings_gdf = buildings_gdf
        self.config = config
        self.tile_bounds_map: Dict[str, Tuple[float, float, float, float]] = {}
        self._scan_tiles()
    
    def _scan_tiles(self) -> None:
        """Scan tile directory and extract bounding boxes from LAZ headers."""
        tile_dir = Path(self.config.tile_directory)
        
        if not tile_dir.exists():
            raise FileNotFoundError(f"Tile directory not found: {tile_dir}")
        
        laz_files = list(tile_dir.glob(f"*.{self.config.tile_extension}"))
        
        if not laz_files:
            raise FileNotFoundError(
                f"No .{self.config.tile_extension} files found in {tile_dir}"
            )
        
        logger.info(f"Scanning {len(laz_files)} LiDAR tiles for bounds...")
        
        for laz_path in laz_files:
            try:
                # Read LAZ header to get bounds without loading full point cloud
                with laspy.open(laz_path) as src:
                    header = src.header
                    bounds = (
                        header.x_min,
                        header.y_min,
                        header.x_max,
                        header.y_max
                    )
                    self.tile_bounds_map[laz_path.name] = bounds
                    
                    logger.debug(
                        f"  {laz_path.name}: "
                        f"x=[{bounds[0]:.1f}, {bounds[2]:.1f}], "
                        f"y=[{bounds[1]:.1f}, {bounds[3]:.1f}]"
                    )
            except Exception as e:
                logger.warning(f"Failed to read header from {laz_path.name}: {e}")
                continue
        
        logger.info(
            f"Indexed {len(self.tile_bounds_map)} tiles with valid bounds"
        )
    
    def get_tiles_for_buildings(self) -> Dict[str, List[int]]:
        """
        Determine which buildings intersect each tile.
        
        Returns:
            Dict mapping tile name → list of building indices in that tile
        """
        result = {}
        
        logger.info("Computing building-to-tile mappings...")
        
        for tile_name, (x_min, y_min, x_max, y_max) in self.tile_bounds_map.items():
            # Create a box geometry for the tile with buffer
            tile_box = box(
                x_min - self.config.buffer_meters,
                y_min - self.config.buffer_meters,
                x_max + self.config.buffer_meters,
                y_max + self.config.buffer_meters
            )
            
            # Find buildings that intersect this tile
            intersecting = self.buildings_gdf.geometry.intersects(tile_box)
            building_indices = list(self.buildings_gdf[intersecting].index)
            
            if building_indices:
                result[tile_name] = building_indices
                logger.debug(
                    f"  {tile_name}: {len(building_indices)} buildings"
                )
        
        logger.info(
            f"Mapped {sum(len(v) for v in result.values())} building-tile intersections"
        )
        
        return result
    
    def validate(self) -> Dict:
        """
        Validate tile coverage against buildings.
        
        Returns:
            Dict with coverage statistics and warnings
        """
        buildings_to_tiles = self.get_tiles_for_buildings()
        
        total_buildings = len(self.buildings_gdf)
        tiled_buildings = len(
            set(idx for indices in buildings_to_tiles.values() for idx in indices)
        )
        untiled_buildings = total_buildings - tiled_buildings
        
        stats = {
            "total_buildings": total_buildings,
            "tiled_count": tiled_buildings,
            "untiled_count": untiled_buildings,
            "coverage_percent": 100.0 * tiled_buildings / total_buildings if total_buildings > 0 else 0.0,
            "total_tiles": len(self.tile_bounds_map),
            "tiles_with_buildings": len(buildings_to_tiles),
            "warnings": []
        }
        
        if untiled_buildings > 0:
            warning = (
                f"WARNING: {untiled_buildings} buildings ({stats['coverage_percent']:.1f}%) "
                f"do not intersect any LiDAR tiles"
            )
            stats["warnings"].append(warning)
            logger.warning(warning)
        
        logger.info(
            f"Tile coverage: {tiled_buildings}/{total_buildings} buildings "
            f"({stats['coverage_percent']:.1f}%)"
        )
        
        return stats
