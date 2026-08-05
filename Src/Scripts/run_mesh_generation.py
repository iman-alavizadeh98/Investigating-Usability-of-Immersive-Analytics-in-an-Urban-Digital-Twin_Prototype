#!/usr/bin/env python3
"""
Example: Generate Building Meshes with Different Strategies

Shows how to use the modular mesh generation architecture.

Usage:
    python run_mesh_generation.py --strategy district
    python run_mesh_generation.py --strategy grid                  # 1000x1000 m cells (default)
    python run_mesh_generation.py --strategy grid --cell-size 250  # 250x250 m cells
    python run_mesh_generation.py --strategy quadtree

Export defaults to PLY only, full detail (no vertex reduction). Use:
    --formats ply,glb          # also write GLB alongside PLY
    --reduce-vertices          # additionally emit decimated LOD2/LOD3 meshes
"""

import logging
import argparse
from pathlib import Path
import geopandas as gpd
import sys

# Add Src to path
sys.path.insert(0, str(Path(__file__).parent.parent))

from mesh_generation.generator import MeshGenerator, GeneratorConfig
from mesh_generation.grid_reference import (
    GRID_ANCHOR_X,
    GRID_ANCHOR_Y,
    DEFAULT_CELL_SIZE_M,
)
from utils.data_profiler import DataFrameProfiler

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(name)s] %(levelname)s: %(message)s'
)
logger = logging.getLogger(__name__)


def main():
    parser = argparse.ArgumentParser(
        description="Generate building meshes using different strategies"
    )
    parser.add_argument(
        "--input",
        default=None,
        help="Path to input GeoPackage with buildings (default: enriched buildings from LiDAR pipeline if available, else processed buildings)"
    )
    parser.add_argument(
        "--strategy",
        choices=["individual", "district", "grid", "quadtree"],
        default="district",
        help="Mesh grouping strategy"
    )
    parser.add_argument(
        "--cell-size",
        type=int,
        default=DEFAULT_CELL_SIZE_M,
        help=f"Grid cell size in meters; produces square cell_size x cell_size cells "
             f"(for grid strategy, default: {DEFAULT_CELL_SIZE_M}). The default matches "
             "the analytical cell size used by the cell-attribute pipeline."
    )
    parser.add_argument(
        "--grid-anchor-x",
        type=float,
        default=GRID_ANCHOR_X,
        help=f"Frozen grid lattice anchor easting, EPSG:3006 (default: {GRID_ANCHOR_X}). "
             "Change only for deliberate experiments — the default keeps mesh cells "
             "and analytical attribute cells on the SAME lattice."
    )
    parser.add_argument(
        "--grid-anchor-y",
        type=float,
        default=GRID_ANCHOR_Y,
        help=f"Frozen grid lattice anchor northing, EPSG:3006 (default: {GRID_ANCHOR_Y})."
    )
    parser.add_argument(
        "--allow-ownership-violations",
        action="store_true",
        help="Continue even if buildings are assigned to more than one group. "
             "Off by default: duplicated buildings inflate every downstream count. "
             "Use only when you knowingly accept an inconsistent partition."
    )
    parser.add_argument(
        "--max-buildings",
        type=int,
        default=500,
        help="Max buildings per cell (for quadtree)"
    )
    parser.add_argument(
        "--output",
        default="Processed_data/building_meshes",
        help="Output directory"
    )
    parser.add_argument(
        "--profile",
        action="store_true",
        help="Generate and save data profile"
    )
    parser.add_argument(
        "--formats",
        default="ply",
        help="Comma-separated mesh export formats, primary first. The first "
             "format is authoritative (its export gates each mesh). Supported: "
             "ply, glb, obj (default: ply)"
    )
    parser.add_argument(
        "--reduce-vertices",
        action="store_true",
        help="Also generate decimated (reduced-vertex) LOD2/LOD3 meshes "
             "alongside full-detail LOD1. Off by default: full detail only."
    )

    args = parser.parse_args()
    
    # Determine input path: prefer enriched buildings with heights
    if args.input is None:
        lidar_added_path = Path("Processed_data/buildings_lidar_added.gpkg")
        enriched_path = Path("Processed_data/buildings_with_heights.gpkg")
        fallback_path = Path("Processed_data/buildings_processed.gpkg")
        
        if lidar_added_path.exists():
            args.input = str(lidar_added_path)
            logger.info(f"Using LiDAR-enriched buildings: {lidar_added_path}")
        elif enriched_path.exists():
            args.input = str(enriched_path)
            logger.info(f"Using enriched buildings (legacy naming): {enriched_path}")
        elif fallback_path.exists():
            args.input = str(fallback_path)
            logger.warning(f"Enriched buildings not found; using processed buildings: {fallback_path}")
        else:
            logger.error(f"No buildings file found (checked: {lidar_added_path}, {enriched_path}, {fallback_path})")
            return 1
    
    input_path = Path(args.input)
    output_dir = Path(args.output)
    
    # Load data
    logger.info(f"Loading buildings from {input_path}...")
    buildings_gdf = gpd.read_file(input_path)
    logger.info(f"Loaded {len(buildings_gdf):,} buildings")
    
    # Profile if requested
    if args.profile:
        logger.info("\nGenerating data profile...")
        profiler = DataFrameProfiler(buildings_gdf, "Byggnad (Buildings)")
        profile_path = output_dir / "byggnad_profile.md"
        profiler.save_markdown(profile_path)
        logger.info(f"Profile saved to {profile_path}")
    
    # Configure strategy
    strategy_config = {}
    
    if args.strategy == "grid":
        strategy_config = {
            "cell_size_m": args.cell_size,
            "origin_x": args.grid_anchor_x,
            "origin_y": args.grid_anchor_y,
        }
        logger.info(
            f"Grid lattice: {args.cell_size}m cells anchored at "
            f"({args.grid_anchor_x}, {args.grid_anchor_y}) EPSG:3006"
        )
    elif args.strategy == "quadtree":
        strategy_config = {"max_buildings_per_cell": args.max_buildings}
    
    # Parse export formats; the first listed format is the primary/authoritative
    # output. Default is PLY only.
    export_formats = tuple(
        f.strip().lower() for f in args.formats.split(",") if f.strip()
    )
    if not export_formats:
        export_formats = ("ply",)
    logger.info(f"Export formats (primary first): {', '.join(export_formats)}")
    logger.info(
        "Vertex reduction (LOD2/LOD3): "
        + ("ENABLED" if args.reduce_vertices else "disabled (full detail only)")
    )

    # Create generator
    config = GeneratorConfig(
        strategy=args.strategy,
        strategy_config=strategy_config,
        assumed_height_m=10.0,
        terrain_offset_m=0.5,
        material_color=(1.0, 1.0, 1.0),
        crs="EPSG:3006",
        strict_ownership=not args.allow_ownership_violations,
        export_formats=export_formats,
        generate_lods=args.reduce_vertices,
    )
    
    generator = MeshGenerator(buildings_gdf, config)
    
    # Generate
    report = generator.generate(output_dir)
    
    # Print summary
    print("\n" + "=" * 80)
    print("GENERATION COMPLETE")
    print("=" * 80)
    print(f"Strategy:         {report['strategy']}")
    print(f"Total buildings:  {report['total_buildings_input']:,}")
    print(f"Mesh groups:      {report['total_groups']}")
    print(f"Total vertices:   {report['total_vertices']:,}")
    print(f"Total triangles:  {report['total_triangles']:,}")
    print(f"Output:           {output_dir}")
    print("=" * 80)
    
    return 0


if __name__ == "__main__":
    sys.exit(main())
