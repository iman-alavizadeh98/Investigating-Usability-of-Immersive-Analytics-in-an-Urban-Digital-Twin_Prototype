"""
Individual Building Strategy: Each building = one mesh file.

Pros:
  - Per-building click detection
  - Maximum granularity
  
Cons:
  - 220k+ files
  - Large disk usage (~20GB+)
  - Slow loading
"""

from .base import MeshStrategy, MeshGroup, StrategyConfig
from typing import List
import logging

logger = logging.getLogger(__name__)


class IndividualBuildingConfig(StrategyConfig):
    """Config for individual building strategy."""
    pass


class IndividualBuildingStrategy(MeshStrategy):
    """Generate ONE mesh file per building."""
    
    def __init__(self, buildings_gdf, config: IndividualBuildingConfig = None):
        super().__init__(buildings_gdf, config or IndividualBuildingConfig())
    
    def partition(self) -> List[MeshGroup]:
        """Create one group per building."""
        self.groups = []
        
        for idx, row in self.buildings_gdf.iterrows():
            geom = row.geometry
            bounds = geom.bounds  # (minx, miny, maxx, maxy)
            building_id = str(row.get("object_id", f"building_{idx}"))
            
            group = MeshGroup(
                group_id=f"bldg_{idx:06d}",
                group_name=f"Building {building_id}",
                building_indices=[idx],
                bounds=bounds,
                building_count=1
            )
            self.groups.append(group)
        
        logger.info(f"Created {len(self.groups)} individual building groups")
        return self.groups
    
    def get_strategy_name(self) -> str:
        return "Individual Buildings"
    
    def get_strategy_description(self) -> str:
        return f"One mesh per building ({len(self.groups):,} meshes)"
