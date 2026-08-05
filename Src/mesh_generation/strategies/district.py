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
from ..grid_reference import GRID_ANCHOR_X, GRID_ANCHOR_Y
from typing import List, Optional
from pathlib import Path
from shapely.geometry import box
import geopandas as gpd
import numpy as np
import logging

logger = logging.getLogger(__name__)


class DistrictConfig(StrategyConfig):
    """Config for district strategy."""
    district_layer_path: Optional[str] = None  # Path to district boundaries
    district_id_field: str = "id"
    district_name_field: str = "name"
    min_buildings_per_group: int = 50

    # DEPRECATED and ignored. Buildings are now always assigned to exactly one
    # district by their representative point (see partition()). Setting this to
    # False used to switch to `within`, which silently DROPPED every building
    # straddling a district boundary. Kept so existing call sites do not break;
    # a warning is emitted if it is set to False.
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
        """
        Create synthetic districts using a regular grid.

        Snapped to the frozen project lattice (grid_reference.GRID_ANCHOR) rather
        than started at the dataset's own total_bounds. An extent-derived start
        makes synthetic district ids meaningless across runs: the same id would
        name different ground whenever the input extent changed.
        """
        import math

        bounds = self.buildings_gdf.total_bounds  # minx, miny, maxx, maxy
        grid_size = 5000  # 5km grid cells

        # Snap the start DOWN to the lattice so cells align with the 500 m
        # analytical grid (5000 is a multiple of 500).
        start_x = GRID_ANCHOR_X + math.floor((bounds[0] - GRID_ANCHOR_X) / grid_size) * grid_size
        start_y = GRID_ANCHOR_Y + math.floor((bounds[1] - GRID_ANCHOR_Y) / grid_size) * grid_size

        districts = []
        district_id = 0
        x = start_x
        col = 0

        while x < bounds[2]:
            y = start_y
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
        """
        Group buildings by district, one owner district per building.

        Assignment is by REPRESENTATIVE POINT: each building goes to the single
        district containing an interior point of its footprint. The previous
        `intersects` selection put every boundary-straddling building into every
        district it touched, violating the strict ownership policy in
        base.MeshStrategy.validate() (same bug as grid.py). `within` is not an
        acceptable alternative -- it silently drops straddling buildings.

        District polygons (unlike a grid) need not tile the plane, so buildings
        falling in a gap, or in a district below min_buildings_per_group, are
        collected into a synthetic `district_unassigned` group rather than being
        dropped. That keeps coverage at 100% and makes the leftovers visible.
        """
        self.groups = []

        if not self.config.allow_partial_buildings:
            logger.warning(
                "DistrictConfig.allow_partial_buildings=False is deprecated and ignored: "
                "buildings are always assigned to exactly one district by representative "
                "point. The old False branch used `within`, which silently dropped "
                "buildings straddling a district boundary."
            )

        # One interior point per building; guaranteed inside concave/donut footprints.
        points = self.buildings_gdf.geometry.representative_point()
        n_buildings = len(self.buildings_gdf)
        owner = np.full(n_buildings, -1, dtype=int)

        # sjoin gives, for each point, the district polygon containing it. Points are
        # used (not polygons) so each building matches at most one district.
        points_gdf = gpd.GeoDataFrame(
            {"pos": np.arange(n_buildings)}, geometry=points, crs=self.buildings_gdf.crs
        )
        districts = self.districts_gdf.reset_index(drop=True)
        joined = gpd.sjoin(
            points_gdf, districts[["geometry"]], how="left", predicate="within"
        )
        # A point exactly on a shared district edge can match two polygons; keep the
        # first deterministically so ownership stays single-valued.
        joined = joined[~joined.index.duplicated(keep="first")]
        matched = joined["index_right"].notna()
        owner[joined.loc[matched, "pos"].to_numpy()] = (
            joined.loc[matched, "index_right"].to_numpy().astype(int)
        )

        assigned_positions = set()

        for district_idx, (_, district) in enumerate(districts.iterrows()):
            district_id = district.get(self.config.district_id_field, district_idx)
            district_name = district.get(self.config.district_name_field, f"district_{district_idx}")

            building_indices = np.flatnonzero(owner == district_idx).tolist()

            if len(building_indices) < self.config.min_buildings_per_group:
                # Too small to be its own mesh; fall through to the unassigned group
                # instead of vanishing.
                logger.debug(
                    f"District {district_name} has only {len(building_indices)} buildings; "
                    "deferring them to district_unassigned"
                )
                continue

            assigned_positions.update(building_indices)
            bounds = self.buildings_gdf.iloc[building_indices].geometry.total_bounds
            
            group = MeshGroup(
                group_id=f"district_{district_id:03d}",
                group_name=str(district_name),
                building_indices=building_indices,
                bounds=bounds,
                building_count=len(building_indices),
                metadata={"assignment_rule": "representative_point"},
            )
            self.groups.append(group)

        # Buildings in a gap between districts, outside every district, or in a
        # district too small to mesh on its own. Collected rather than dropped so
        # validate() still sees 100% coverage and the leftovers are inspectable.
        leftover = [p for p in range(n_buildings) if p not in assigned_positions]
        if leftover:
            logger.warning(
                f"{len(leftover)} building(s) not claimed by any district group "
                f"({100.0 * len(leftover) / n_buildings:.2f}%); collected into "
                "'district_unassigned'. Check district layer coverage."
            )
            self.groups.append(
                MeshGroup(
                    group_id="district_unassigned",
                    group_name="Unassigned (outside all districts)",
                    building_indices=leftover,
                    bounds=self.buildings_gdf.iloc[leftover].geometry.total_bounds,
                    building_count=len(leftover),
                    metadata={
                        "assignment_rule": "representative_point",
                        "unassigned": True,
                        "reason": "no containing district, or district below min_buildings_per_group",
                    },
                )
            )

        logger.info(f"Created {len(self.groups)} district groups")
        return self.groups
    
    def get_strategy_name(self) -> str:
        return "By District"
    
    def get_strategy_description(self) -> str:
        return f"One mesh per district ({len(self.groups)} districts)"
