"""
Create spatial join mappings between buildings and grid cells.

Phase 3: Spatial Joins

This script:
1. Loads building footprints
2. Loads all Ruta grid cells (population/employment/income data)
3. Performs spatial intersection: building ↔ grid cells
4. Creates bidirectional join tables
5. Aggregates demographic data by building
6. Exports JSON payload for Unity info panel

Building Click Behavior in Unity:
- User clicks building → query spatial join
- Find all grid cells overlapping building
- Aggregate statistics from all cells
- Display in info panel
"""

import json
import logging
from pathlib import Path
from dataclasses import dataclass, asdict, field
from typing import Dict, List, Tuple, Optional, Set
from datetime import datetime

import geopandas as gpd
import pandas as pd
import numpy as np
from shapely.geometry import box

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s'
)
logger = logging.getLogger(__name__)

# ============================================================================
# Configuration
# ============================================================================

CONFIG = {
    "buildings_gpkg": "Raw_data/byggnad_gpkg/byggnad_sverige.gpkg",
    "buildings_layer": "byggnad",
    "scope_bounds": {
        "west": 298000,
        "south": 6383590,
        "east": 328500,
        "north": 6413000
    },
    "population_data_dir": "Raw_data/befolkningShp/",
    "employment_data_dir": "Raw_data/arbutbShp/",
    "income_data_dir": "Raw_data/inkomsterShp/",
    "output_joins_dir": "Processed_data/spatial_joins/",
    "output_analytics_dir": "Processed_data/analytics/",
    "target_crs": "EPSG:3006",
    "spatial_predicate": "intersects"
}

# ============================================================================
# Data Classes
# ============================================================================

@dataclass
class GridCellAggregates:
    """Aggregated population/employment/income for a single grid cell."""
    grid_cell_id: str
    grid_cell_size_m: int
    total_population: int = 0
    age_0_6: int = 0
    age_7_15: int = 0
    age_16_19: int = 0
    age_20_24: int = 0
    age_25_44: int = 0
    age_45_64: int = 0
    age_65_plus: int = 0
    education_pregymn: int = 0
    education_gymnasium: int = 0
    education_postgymn_2: int = 0
    education_postgymn_3: int = 0
    education_missing: int = 0


@dataclass
class BuildingAnalytics:
    """Aggregated analytics for a single building."""
    building_id: str
    object_id: str
    centroid_x: float
    centroid_y: float
    overlapping_grid_cells: List[str] = field(default_factory=list)
    total_population: int = 0
    age_distribution: Dict[str, int] = field(default_factory=dict)
    education_distribution: Dict[str, int] = field(default_factory=dict)
    employment_distribution: Dict[str, int] = field(default_factory=dict)


@dataclass
class SpatialJoinReport:
    """Summary of spatial join operation."""
    timestamp: str
    total_buildings: int
    buildings_in_scope: int
    total_grid_cells: int
    grid_cells_in_scope: int
    joins_created: int
    buildings_with_data: int
    average_cells_per_building: float
    total_population_summed: int


# ============================================================================
# Data Loading
# ============================================================================

def load_buildings_subset(gpkg_path: str, bounds: Dict[str, float]) -> gpd.GeoDataFrame:
    """Load buildings within scope bounds."""
    logger.info(f"Loading buildings from {gpkg_path}")
    gdf = gpd.read_file(gpkg_path, layer="byggnad")
    
    # Filter to scope
    scope_box = box(bounds["west"], bounds["south"], bounds["east"], bounds["north"])
    gdf = gdf[gdf.geometry.intersects(scope_box)].copy()
    
    logger.info(f"Loaded {len(gdf)} buildings in scope")
    return gdf


def load_population_data() -> gpd.GeoDataFrame:
    """Load and combine all population grid cells."""
    logger.info(f"Loading population data from {CONFIG['population_data_dir']}")
    
    population_dir = Path(CONFIG["population_data_dir"])
    shp_files = list(population_dir.glob("Tab*_Ruta_*.shp"))
    
    if not shp_files:
        logger.warning(f"No population Shapefiles found in {CONFIG['population_data_dir']}")
        return gpd.GeoDataFrame()
    
    gdfs = []
    for shp_file in shp_files:
        logger.info(f"  Loading {shp_file.name}")
        try:
            gdf = gpd.read_file(str(shp_file))
            gdfs.append(gdf)
        except Exception as e:
            logger.warning(f"  Failed to load {shp_file.name}: {e}")
    
    if gdfs:
        combined = pd.concat(gdfs, ignore_index=True)
        combined = combined.drop_duplicates(subset=["Ruta"])
        logger.info(f"Loaded {len(combined)} unique population grid cells")
        return combined
    
    return gpd.GeoDataFrame()


# ============================================================================
# Spatial Join
# ============================================================================

def create_spatial_joins(
    buildings: gpd.GeoDataFrame,
    grid_cells: gpd.GeoDataFrame,
    output_dir: Path
) -> Dict[str, List[str]]:
    """
    Create bidirectional spatial join: building ↔ grid cells.
    
    Returns: {building_id: [grid_cell_ids]}
    """
    logger.info("Performing spatial joins...")
    
    # Create spatial index for faster lookup
    grid_cells_index = grid_cells.sindex
    
    building_to_cells = {}
    join_count = 0
    
    for idx, building in buildings.iterrows():
        if idx % 1000 == 0:
            logger.info(f"  Processing building {idx + 1}/{len(buildings)}...")
        
        building_id = f"bldg_{idx:06d}"
        building_geom = building.geometry
        
        # Find intersecting grid cells
        possible_indices = list(grid_cells_index.intersection(building_geom.bounds))
        
        overlapping_cells = []
        for cell_idx in possible_indices:
            cell = grid_cells.iloc[cell_idx]
            if building_geom.intersects(cell.geometry):
                grid_cell_id = str(cell.get("Ruta", f"cell_{cell_idx}"))
                overlapping_cells.append(grid_cell_id)
                join_count += 1
        
        if overlapping_cells:
            building_to_cells[building_id] = overlapping_cells
    
    logger.info(f"Created {join_count} spatial joins")
    
    # Save join table
    output_dir.mkdir(parents=True, exist_ok=True)
    join_path = output_dir / "building_to_grid_cells.json"
    
    with open(join_path, 'w') as f:
        json.dump(building_to_cells, f, indent=2)
    
    logger.info(f"Saved building→cells join to {join_path}")
    
    return building_to_cells


# ============================================================================
# Analytics Aggregation
# ============================================================================

def aggregate_building_analytics(
    buildings: gpd.GeoDataFrame,
    population_grid: gpd.GeoDataFrame,
    building_to_cells: Dict[str, List[str]],
    output_dir: Path
) -> List[BuildingAnalytics]:
    """Aggregate demographics for each building."""
    logger.info("Aggregating building analytics...")
    
    # Create lookup for grid cells
    grid_lookup = {}
    for idx, row in population_grid.iterrows():
        grid_id = str(row.get("Ruta", f"cell_{idx}"))
        grid_lookup[grid_id] = row
    
    analytics_list = []
    
    for building_idx, (_, building) in enumerate(buildings.iterrows()):
        if building_idx % 1000 == 0:
            logger.info(f"  Aggregating building {building_idx + 1}/{len(buildings)}...")
        
        building_id = f"bldg_{building_idx:06d}"
        object_id = str(building.get("objektidentitet", ""))
        
        # Get overlapping cells for this building
        overlapping_cells = building_to_cells.get(building_id, [])
        
        if not overlapping_cells:
            continue
        
        # Aggregate data from all cells
        total_pop = 0
        age_agg = {}
        education_agg = {}
        
        for cell_id in overlapping_cells:
            if cell_id in grid_lookup:
                cell_row = grid_lookup[cell_id]
                
                # Population
                total = cell_row.get("Totalt", 0)
                total_pop += total if pd.notna(total) else 0
                
                # Age groups
                for age_field in ["Alder_0_6", "Alder_7_15", "Alder_16_1", "Alder_20_2", 
                                  "Alder_25_4", "Alder_45_6", "Alder_65"]:
                    val = cell_row.get(age_field, 0)
                    val = int(val) if pd.notna(val) else 0
                    age_agg[age_field] = age_agg.get(age_field, 0) + val
                
                # Education
                for edu_field in ["Forgymn", "Gymnasial", "Eftergymn2", "Eftergymn3", "UppgSakn"]:
                    val = cell_row.get(edu_field, 0)
                    val = int(val) if pd.notna(val) else 0
                    education_agg[edu_field] = education_agg.get(edu_field, 0) + val
        
        # Create analytics record
        analytics = BuildingAnalytics(
            building_id=building_id,
            object_id=object_id,
            centroid_x=float(building.geometry.centroid.x),
            centroid_y=float(building.geometry.centroid.y),
            overlapping_grid_cells=overlapping_cells,
            total_population=int(total_pop),
            age_distribution=age_agg,
            education_distribution=education_agg
        )
        
        analytics_list.append(analytics)
    
    # Save analytics
    output_dir.mkdir(parents=True, exist_ok=True)
    analytics_path = output_dir / "building_demographics.json"
    
    analytics_dicts = [asdict(a) for a in analytics_list]
    
    with open(analytics_path, 'w') as f:
        json.dump(analytics_dicts, f, indent=2)
    
    logger.info(f"Saved {len(analytics_list)} building analytics to {analytics_path}")
    
    return analytics_list


# ============================================================================
# Main Pipeline
# ============================================================================

def run_spatial_joins():
    """Main pipeline: load data → create spatial joins → aggregate analytics."""
    report = SpatialJoinReport(
        timestamp=datetime.now().isoformat(),
        total_buildings=0,
        buildings_in_scope=0,
        total_grid_cells=0,
        grid_cells_in_scope=0,
        joins_created=0,
        buildings_with_data=0,
        average_cells_per_building=0.0,
        total_population_summed=0
    )
    
    try:
        output_joins_dir = Path(CONFIG["output_joins_dir"])
        output_analytics_dir = Path(CONFIG["output_analytics_dir"])
        
        # Load data
        buildings = load_buildings_subset(CONFIG["buildings_gpkg"], CONFIG["scope_bounds"])
        population_grid = load_population_data()
        
        if buildings.empty or population_grid.empty:
            logger.error("Failed to load required data")
            return report
        
        report.total_buildings = len(buildings)
        report.buildings_in_scope = len(buildings)
        report.total_grid_cells = len(population_grid)
        
        # Create spatial joins
        building_to_cells = create_spatial_joins(
            buildings, population_grid, output_joins_dir
        )
        
        report.joins_created = sum(len(v) for v in building_to_cells.values())
        report.buildings_with_data = len(building_to_cells)
        
        if building_to_cells:
            report.average_cells_per_building = (
                report.joins_created / report.buildings_with_data
            )
        
        # Aggregate analytics
        analytics = aggregate_building_analytics(
            buildings, population_grid, building_to_cells, output_analytics_dir
        )
        
        report.total_population_summed = sum(a.total_population for a in analytics)
        
        # Save report
        report_path = output_joins_dir / "spatial_join_report.json"
        report_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(report_path, 'w') as f:
            json.dump(asdict(report), f, indent=2)
        
        # Print summary
        logger.info("=" * 70)
        logger.info("SPATIAL JOIN COMPLETE")
        logger.info("=" * 70)
        logger.info(f"Buildings in scope: {report.buildings_in_scope}")
        logger.info(f"Grid cells in scope: {report.grid_cells_in_scope}")
        logger.info(f"Spatial joins created: {report.joins_created}")
        logger.info(f"Buildings with overlapping data: {report.buildings_with_data}")
        logger.info(f"Avg cells per building: {report.average_cells_per_building:.2f}")
        logger.info(f"Total population (aggregated): {report.total_population_summed:,}")
        logger.info("=" * 70)
        
        return report
    
    except Exception as e:
        logger.error(f"Pipeline failed: {e}", exc_info=True)
        raise


if __name__ == "__main__":
    run_spatial_joins()
