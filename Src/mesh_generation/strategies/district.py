"""
District Strategy: Group buildings by municipality/district.

Pros:
  - 50-100 meshes
  - Fast loading
  - Natural administrative boundaries
  - Scalable to any city
  
Cons:
  - Requires district layer or synthetic grid
  - Less granular per-building control
"""

from .base import MeshStrategy, MeshGroup, StrategyConfig
from typing import List, Optional
from pathlib import Path
from shapely.geometry import box
import geopandas as gpd
import logging

logger = logging.getLogger(__name__)


class DistrictConfig(StrategyConfig):
    """Config for district strategy."""
    district_layer_path: Optional[str] = None  # Path to district boundaries
    district_id_field: str = "id"
    district_name_field: str = "name"
    min_buildings_per_group: int = 50
    allow_partial_buildings: bool = True


class DistrictStrategy(MeshStrategy):
    """Generate one mesh per district/municipality."""
    
    def __init__(self, buildings_gdf, config: DistrictConfig = None):
        super().__init__(buildings_gdf, config or DistrictConfig())
        self.districts_gdf = None
        self._load_districts()
    
    def _load_districts(self):
        """Load district/municipality boundaries."""
        if self.config.district_layer_path:
            try:
                self.districts_gdf = gpd.read_file(self.config.district_layer_path)
                logger.info(f"Loaded {len(self.districts_gdf)} districts")
            except Exception as e:
                logger.warning(f"Failed to load district layer: {e}")
                self._create_grid_districts()
        else:
            logger.info("No district layer provided; creating synthetic grid districts")
            self._create_grid_districts()
    
    def _create_grid_districts(self):
        """Create synthetic districts using a regular grid."""
        bounds = self.buildings_gdf.total_bounds  # minx, miny, maxx, maxy
        grid_size = 5000  # 5km grid cells
        
        districts = []
        district_id = 0
        x = bounds[0]
        col = 0
        
        while x < bounds[2]:
            y = bounds[1]
            row = 0
            
            while y < bounds[3]:
                district_box = box(x, y, x + grid_size, y + grid_size)
                
                districts.append({
                    "id": district_id,
                    "name": f"District_{col:02d}_{row:02d}",
                    "geometry": district_box,
                    "grid_col": col,
                    "grid_row": row
                })
                
                district_id += 1
                y += grid_size
                row += 1
            
            x += grid_size
            col += 1
        
        self.districts_gdf = gpd.GeoDataFrame(
            districts,
            crs=self.buildings_gdf.crs
        )
        logger.info(f"Created {len(self.districts_gdf)} synthetic district grid cells")
    
    def partition(self) -> List[MeshGroup]:
        """Group buildings by district intersection."""
        self.groups = []
        
        for district_idx, (_, district) in enumerate(self.districts_gdf.iterrows()):
            district_id = district.get(self.config.district_id_field, district_idx)
            district_name = district.get(self.config.district_name_field, f"district_{district_idx}")
            
            # Find buildings in this district
            if self.config.allow_partial_buildings:
                mask = self.buildings_gdf.geometry.intersects(district.geometry)
            else:
                mask = self.buildings_gdf.geometry.within(district.geometry)
            
            building_indices = self.buildings_gdf[mask].index.tolist()
            
            if len(building_indices) < self.config.min_buildings_per_group:
                logger.debug(
                    f"District {district_name} has only {len(building_indices)} buildings; skipping"
                )
                continue
            
            bounds = self.buildings_gdf.iloc[building_indices].geometry.total_bounds
            
            group = MeshGroup(
                group_id=f"district_{district_id:03d}",
                group_name=str(district_name),
                building_indices=building_indices,
                bounds=bounds,
                building_count=len(building_indices)
            )
            self.groups.append(group)
        
        logger.info(f"Created {len(self.groups)} district groups")
        return self.groups
    
    def get_strategy_name(self) -> str:
        return "By District"
    
    def get_strategy_description(self) -> str:
        return f"One mesh per district ({len(self.groups)} districts)"
