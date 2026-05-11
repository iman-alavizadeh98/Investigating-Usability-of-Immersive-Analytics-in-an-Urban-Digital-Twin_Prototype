"""
Quadtree Strategy: Hierarchical spatial partitioning.

Pros:
  - Adaptive density (dense areas = more cells, sparse = fewer)
  - LOD-friendly for hierarchical loading
  - Efficient for variable building density
  
Cons:
  - More complex
  - Variable mesh sizes
  - Harder to visualize grid structure
"""

from .base import MeshStrategy, MeshGroup, StrategyConfig
from typing import List
from shapely.geometry import box
import logging

logger = logging.getLogger(__name__)


class QuadtreeConfig(StrategyConfig):
    """Config for quadtree strategy."""
    max_buildings_per_cell: int = 500
    min_cell_size_m: int = 250
    initial_cell_size_m: int = 2000


class QuadtreeStrategy(MeshStrategy):
    """Recursively subdivide space using quadtree."""
    
    def __init__(self, buildings_gdf, config: QuadtreeConfig = None):
        super().__init__(buildings_gdf, config or QuadtreeConfig())
        self.cell_counter = 0
    
    def partition(self) -> List[MeshGroup]:
        """Build quadtree and generate groups."""
        self.groups = []
        self.cell_counter = 0
        
        bounds = self.buildings_gdf.total_bounds
        root_cell = box(bounds[0], bounds[1], bounds[2], bounds[3])
        
        logger.info(
            f"Building quadtree: max_buildings={self.config.max_buildings_per_cell}, "
            f"min_size={self.config.min_cell_size_m}m"
        )
        
        # Recursively subdivide
        self._subdivide(
            root_cell,
            self.buildings_gdf.index.tolist(),
            depth=0
        )
        
        logger.info(f"Created {len(self.groups)} quadtree cells")
        return self.groups
    
    def _subdivide(self, cell_geom, building_indices: List[int], depth: int = 0):
        """Recursively subdivide if needed."""
        
        if not building_indices:
            return
        
        # Check stopping criteria
        cell_width = cell_geom.bounds[2] - cell_geom.bounds[0]
        
        if (len(building_indices) <= self.config.max_buildings_per_cell or
            cell_width < self.config.min_cell_size_m):
            
            # Create group for this cell
            bounds_actual = (
                self.buildings_gdf.iloc[building_indices].geometry.total_bounds
            )
            group = MeshGroup(
                group_id=f"qtree_{self.cell_counter:05d}",
                group_name=f"Quadtree Cell (depth={depth})",
                building_indices=building_indices,
                bounds=bounds_actual,
                building_count=len(building_indices),
                metadata={"depth": depth, "cell_size_m": int(cell_width)}
            )
            self.groups.append(group)
            self.cell_counter += 1
            return
        
        # Subdivide into 4 quadrants
        minx, miny, maxx, maxy = cell_geom.bounds
        midx = (minx + maxx) / 2
        midy = (miny + maxy) / 2
        
        quadrants = [
            box(minx, miny, midx, midy),  # SW
            box(midx, miny, maxx, midy),  # SE
            box(minx, midy, midx, maxy),  # NW
            box(midx, midy, maxx, maxy),  # NE
        ]
        
        for quad_cell in quadrants:
            mask = self.buildings_gdf.iloc[building_indices].geometry.intersects(quad_cell)
            quad_indices = [building_indices[i] for i, m in enumerate(mask) if m]
            
            if quad_indices:
                self._subdivide(quad_cell, quad_indices, depth + 1)
    
    def get_strategy_name(self) -> str:
        return "Quadtree (Hierarchical)"
    
    def get_strategy_description(self) -> str:
        return (
            f"Adaptive quadtree: max {self.config.max_buildings_per_cell} "
            f"buildings/cell ({len(self.groups)} total cells)"
        )
