"""
Grid Strategy: Partition buildings into regular grid cells.

Pros:
  - Predictable tile-based loading
  - Works for any city
  - Easy caching and streaming
  - Configurable cell size
  
Cons:
  - Grid may not align with natural boundaries
  - Buildings spanning cells need special handling
"""

from .base import MeshStrategy, MeshGroup, StrategyConfig
from typing import List
from shapely.geometry import box
import logging

logger = logging.getLogger(__name__)


class GridConfig(StrategyConfig):
    """Config for grid strategy."""
    cell_size_m: int = 1000  # 1km cells
    overlap_m: int = 0  # Optional overlap for seamless loading


class GridStrategy(MeshStrategy):
    """Partition buildings into regular grid cells."""
    
    def __init__(self, buildings_gdf, config: GridConfig = None):
        super().__init__(buildings_gdf, config or GridConfig())
    
    def partition(self) -> List[MeshGroup]:
        """Create regular grid and assign buildings to cells."""
        self.groups = []
        
        bounds = self.buildings_gdf.total_bounds  # minx, miny, maxx, maxy
        cell_size = self.config.cell_size_m
        overlap = self.config.overlap_m
        
        logger.info(
            f"Creating grid: cell_size={cell_size}m, overlap={overlap}m"
        )
        
        # Create grid
        x = bounds[0]
        grid_col = 0
        cell_id = 0
        
        while x < bounds[2]:
            y = bounds[1]
            grid_row = 0
            
            while y < bounds[3]:
                # Cell bounds with optional overlap
                cell_bounds = (
                    x - overlap, 
                    y - overlap,
                    x + cell_size + overlap, 
                    y + cell_size + overlap
                )
                cell_geom = box(*cell_bounds)
                
                # Find buildings in cell
                mask = self.buildings_gdf.geometry.intersects(cell_geom)
                building_indices = self.buildings_gdf[mask].index.tolist()
                
                if building_indices:
                    bounds_actual = self.buildings_gdf.iloc[building_indices].geometry.total_bounds
                    
                    group = MeshGroup(
                        group_id=f"grid_{grid_col:03d}_{grid_row:03d}",
                        group_name=f"Grid Cell ({grid_col}, {grid_row})",
                        building_indices=building_indices,
                        bounds=bounds_actual,
                        building_count=len(building_indices)
                    )
                    self.groups.append(group)
                
                y += cell_size
                grid_row += 1
            
            x += cell_size
            grid_col += 1
        
        logger.info(
            f"Created {len(self.groups)} grid cells "
            f"({self.config.cell_size_m}m cells, {grid_col} columns, {grid_row} rows)"
        )
        return self.groups
    
    def get_strategy_name(self) -> str:
        return "Regular Grid"
    
    def get_strategy_description(self) -> str:
        return f"Regular {self.config.cell_size_m}m grid ({len(self.groups)} cells)"
