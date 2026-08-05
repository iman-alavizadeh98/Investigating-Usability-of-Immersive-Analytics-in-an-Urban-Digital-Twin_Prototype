"""
Grid Strategy: Partition buildings into regular grid cells.

OWNERSHIP MODEL
---------------
Each building is assigned to EXACTLY ONE cell, chosen by the cell containing the
building's representative point. The complete building lives in that cell; it is
never split at a cell boundary and never duplicated.

This replaces an earlier `geometry.intersects(cell_geom)` selection which assigned
a building to EVERY cell it touched. Measured on the 2026-08-05 500 m run, that
produced 211,620 building slots for 201,594 unique buildings -- 9,690 buildings
duplicated across 2-4 cells (5.0% inflation), silently violating the strict
ownership policy documented in `base.MeshStrategy.validate()`.

Why `representative_point()` and not `centroid`:
  - a centroid can fall OUTSIDE a concave (L/U-shaped) or donut footprint, which
    would assign the building to a cell it does not occupy;
  - `representative_point()` is guaranteed by Shapely to lie inside the polygon.

Why not `within` (the alternative in district.py):
  - `within` silently DROPS every building straddling a boundary, which is worse
    than duplication -- data loss instead of inflation.

Pros:
  - Predictable tile-based loading
  - Works for any city
  - Easy caching and streaming
  - Stable cell ids: the lattice is frozen (see grid_reference.py), so a cell id
    always names the same ground regardless of which subset was processed
Cons:
  - Grid may not align with natural boundaries
"""

from .base import MeshStrategy, MeshGroup, StrategyConfig
from ..grid_reference import (
    GRID_ANCHOR_X,
    GRID_ANCHOR_Y,
    DEFAULT_CELL_SIZE_M,
    ASSIGNMENT_RULE,
    cell_id as make_cell_id,
    cell_origin,
)
from typing import List
import numpy as np
import pandas as pd
import logging

logger = logging.getLogger(__name__)


class GridConfig(StrategyConfig):
    """Config for grid strategy."""

    # Square cells of cell_size_m x cell_size_m.
    cell_size_m: int = DEFAULT_CELL_SIZE_M

    # Frozen lattice anchor (EPSG:3006). Defaults to the project-wide anchor so
    # the mesh grid and the analytical attribute grid are the SAME lattice.
    # Overriding this breaks that join -- do it only for deliberate experiments.
    origin_x: float = GRID_ANCHOR_X
    origin_y: float = GRID_ANCHOR_Y

    # Retained only to fail loudly if someone sets it; see partition().
    overlap_m: int = 0


class GridStrategy(MeshStrategy):
    """Partition buildings into regular grid cells, one owner cell per building."""

    def __init__(self, buildings_gdf, config: GridConfig = None):
        super().__init__(buildings_gdf, config or GridConfig())

    def partition(self) -> List[MeshGroup]:
        """
        Assign every building to exactly one grid cell.

        Implemented as a single vectorised binning pass rather than testing each
        cell against every building: for the 500 m Gothenburg run that replaces
        ~2211 x 201k spatial predicate evaluations with one pass.
        """
        self.groups = []

        cell_size = self.config.cell_size_m
        origin_x = self.config.origin_x
        origin_y = self.config.origin_y

        if getattr(self.config, "overlap_m", 0):
            # Overlap exists to duplicate boundary buildings into neighbouring
            # tiles, which directly contradicts single-owner assignment. Fail
            # rather than silently ignoring a setting the caller expects to work.
            raise ValueError(
                f"GridConfig.overlap_m={self.config.overlap_m} is not supported: "
                "overlapping cells would assign a building to more than one group, "
                "violating the strict ownership policy in MeshStrategy.validate(). "
                "Use overlap_m=0."
            )

        logger.info(
            f"Creating grid: cell_size={cell_size}m, "
            f"anchor=({origin_x}, {origin_y}), rule={ASSIGNMENT_RULE}"
        )

        # One interior point per building. Guaranteed inside the polygon even for
        # concave footprints and footprints with holes.
        points = self.buildings_gdf.geometry.representative_point()

        # Half-open binning: a point exactly on a boundary goes to the higher cell.
        cols = np.floor((points.x.to_numpy() - origin_x) / cell_size).astype(int)
        rows = np.floor((points.y.to_numpy() - origin_y) / cell_size).astype(int)

        # Group positional row numbers (0..n-1) by cell. Positional indices are
        # used because downstream code indexes with .iloc; see base.MeshStrategy.
        positions = pd.DataFrame({"col": cols, "row": rows})
        positions["pos"] = np.arange(len(positions))

        for (col, row), chunk in positions.groupby(["col", "row"], sort=True):
            building_positions = chunk["pos"].tolist()

            # Content bbox of the buildings actually in this cell. This -- NOT the
            # cell corner -- stays as group.bounds because generator._build_group_mesh()
            # uses bounds[0]/bounds[1] as the mesh local origin, and the Unity
            # runtime places tiles from that same value.
            bounds_actual = self.buildings_gdf.iloc[building_positions].geometry.total_bounds

            ox, oy = cell_origin(int(col), int(row), cell_size, origin_x, origin_y)

            group = MeshGroup(
                group_id=make_cell_id(int(col), int(row)),
                group_name=f"Grid Cell ({int(col)}, {int(row)})",
                building_indices=building_positions,
                bounds=bounds_actual,
                building_count=len(building_positions),
                metadata={
                    "grid_col": int(col),
                    "grid_row": int(row),
                    "cell_size_m": cell_size,
                    "cell_origin_x": ox,
                    "cell_origin_y": oy,
                    "assignment_rule": ASSIGNMENT_RULE,
                },
            )
            self.groups.append(group)

        n_cols = len(np.unique(cols)) if len(cols) else 0
        n_rows = len(np.unique(rows)) if len(rows) else 0
        logger.info(
            f"Created {len(self.groups)} grid cells "
            f"({cell_size}m cells, {n_cols} occupied columns, {n_rows} occupied rows)"
        )
        return self.groups

    def get_strategy_name(self) -> str:
        return "Regular Grid"

    def get_strategy_description(self) -> str:
        return f"Regular {self.config.cell_size_m}m grid ({len(self.groups)} cells)"
