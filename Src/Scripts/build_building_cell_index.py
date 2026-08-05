#!/usr/bin/env python3
"""
Map every building to its owning analytical cell.

This is the legitimate half of the deprecated preprocess_spatial_joins.py: a
building genuinely does sit in one cell. What that script got wrong was giving
each building the cell's full population; here we only record WHICH cell owns it.

Assignment uses the SAME representative-point rule and the SAME frozen lattice as
mesh partitioning (Src/mesh_generation/strategies/grid.py), so a building's mesh
group id and its attribute cell id are guaranteed to be identical. That is what
makes "click a building -> show its cell's statistics" correct rather than
approximate: the panel reads "500 m cell -- 412 residents", never a fabricated
per-building number.

Usage:
    python build_building_cell_index.py
    python build_building_cell_index.py --input <buildings.gpkg> --cell-size 500
"""

import argparse
import logging
import sys
from pathlib import Path

import geopandas as gpd
import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).parent.parent))

from mesh_generation.grid_reference import (
    DEFAULT_CELL_SIZE_M,
    GRID_ANCHOR_X,
    GRID_ANCHOR_Y,
    cell_id as make_cell_id,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
)
logger = logging.getLogger(__name__)

DEFAULT_INPUT = Path(
    "Processed_data/Gothenburg/lidar_heights_2026-08-03/buildings_lidar_added.gpkg"
)


def main():
    parser = argparse.ArgumentParser(description="Map buildings to analytical cells")
    parser.add_argument("--input", default=str(DEFAULT_INPUT), help="Buildings GeoPackage")
    parser.add_argument("--cell-size", type=int, default=DEFAULT_CELL_SIZE_M)
    parser.add_argument(
        "--output", default=None,
        help="Output path (default: Processed_data/analytics/building_to_cell_<size>m.parquet)",
    )
    parser.add_argument("--id-field", default="object_id", help="Building identifier column")
    args = parser.parse_args()

    source = Path(args.input)
    if not source.exists():
        logger.error(f"Input not found: {source}")
        return 1

    buildings = gpd.read_file(source)
    logger.info(f"Loaded {len(buildings):,} buildings from {source.name} (CRS {buildings.crs})")

    if args.id_field not in buildings.columns:
        logger.error(
            f"Identifier column '{args.id_field}' not present. Available: "
            f"{[c for c in buildings.columns if c != 'geometry'][:12]}"
        )
        return 1

    # Same rule as GridStrategy: an interior point, guaranteed inside concave and
    # donut footprints where a centroid could fall outside.
    points = buildings.geometry.representative_point()
    cols = np.floor((points.x.to_numpy() - GRID_ANCHOR_X) / args.cell_size).astype(int)
    rows = np.floor((points.y.to_numpy() - GRID_ANCHOR_Y) / args.cell_size).astype(int)

    index = pd.DataFrame({
        "object_id": buildings[args.id_field].astype(str),
        "cell_id": [make_cell_id(int(c), int(r)) for c, r in zip(cols, rows)],
        "grid_col": cols,
        "grid_row": rows,
        "cell_origin_x": GRID_ANCHOR_X + cols * args.cell_size,
        "cell_origin_y": GRID_ANCHOR_Y + rows * args.cell_size,
        "cell_size_m": args.cell_size,
    })

    # Each building must own exactly one cell -- the same invariant the mesh
    # strategies enforce.
    duplicated = int(index.duplicated(subset=["object_id"]).sum())
    if duplicated:
        logger.error(f"{duplicated} building id(s) appear more than once; input ids are not unique")
        return 1

    output = Path(args.output) if args.output else Path(
        f"Processed_data/analytics/building_to_cell_{args.cell_size}m.parquet"
    )
    output.parent.mkdir(parents=True, exist_ok=True)

    try:
        index.to_parquet(output, index=False)
    except Exception as exc:
        logger.warning(f"Parquet export failed ({exc}); writing CSV instead")
        output = output.with_suffix(".csv")
        index.to_csv(output, index=False)

    logger.info(
        f"Wrote {len(index):,} building->cell mappings across "
        f"{index['cell_id'].nunique():,} cells: {output}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
