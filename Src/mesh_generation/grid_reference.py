"""
Frozen analytical grid reference for Gothenburg (EPSG:3006 / SWEREF99 TM).

WHY THIS MODULE EXISTS
----------------------
The 500 m analytical grid is the join key between two otherwise unrelated things:

  - mesh groups   (Src/mesh_generation/strategies/grid.py)
  - cell attributes (Src/pipelines/cell_attributes/, population/employment/income)

For that join to work, both must agree on *exactly* where the cell boundaries are.
This module is the single definition of that lattice. Nothing else may define it.

THE BUG THIS REPLACES
---------------------
`GridStrategy.partition()` previously started the grid at
`buildings_gdf.total_bounds` -- the input dataset's own extent. Cell ids therefore
changed whenever the input extent changed or a subset was processed: cell
`grid_003_007` meant a different piece of ground from run to run, which makes runs
non-reproducible and analytics joins meaningless.

Measured on the 2026-08-05 500 m run: the derived origin was
(297697.899, 6383590.000), i.e. (197.9, 90.0) off a 500 m lattice.

WHY THIS ANCHOR
---------------
GRID_ANCHOR = (298000.0, 6383500.0), chosen because:

  - both coordinates are multiples of 500 AND 1000, so every SCB `Ruta` statistical
    cell (100 m, 250 m, 500 m, 1000 m) nests into the 500 m lattice with no
    partial cells and no area-weighting;
  - 298000 matches the project scope west bound already used in
    Src/Scripts/preprocess_spatial_joins.py;
  - 6383500 is the multiple of 500 immediately below the southern data edge
    (6383590, the municipal boundary).

Verified against all 9 SCB Ruta layers: every source cell <= 500 m nests into
exactly one 500 m target cell at this anchor (3936/3936 for the 250 m population
layer).

CONVENTIONS
-----------
Cell indices are HALF-OPEN: a cell covers [origin, origin + size) on both axes, so
a point exactly on a boundary belongs to the cell above/right of it. `floor()`
division implements this directly. All strategies must use the same convention so
boundary ties resolve identically.

Indices are signed and anchor-relative. They are a global lattice address, not a
per-run sequence number, so `grid_+000_+018` always names the same ground.
"""

from __future__ import annotations

from typing import Tuple

# ---------------------------------------------------------------------------
# The frozen lattice. Do NOT derive these from any dataset's extent.
# ---------------------------------------------------------------------------

GRID_ANCHOR_X: float = 298000.0
GRID_ANCHOR_Y: float = 6383500.0
GRID_ANCHOR_CRS: str = "EPSG:3006"

DEFAULT_CELL_SIZE_M: int = 500

#: Format of a cell id. Signed and zero-padded so ids sort sensibly and negative
#: indices (west/south of the anchor) cannot collide with positive ones.
CELL_ID_FORMAT: str = "grid_{col:+04d}_{row:+04d}"

#: Recorded in manifests so a consumer can tell how buildings were assigned.
ASSIGNMENT_RULE: str = "representative_point"


def cell_index(
    x: float,
    y: float,
    cell_size_m: int = DEFAULT_CELL_SIZE_M,
    anchor_x: float = GRID_ANCHOR_X,
    anchor_y: float = GRID_ANCHOR_Y,
) -> Tuple[int, int]:
    """
    Return the (col, row) lattice index containing point (x, y).

    Half-open: a point exactly on a cell boundary belongs to the higher cell.

    Args:
        x, y: Coordinates in the anchor CRS (EPSG:3006 metres).
        cell_size_m: Target cell size.
        anchor_x, anchor_y: Lattice anchor; defaults to the frozen project anchor.
    """
    import math

    col = math.floor((x - anchor_x) / cell_size_m)
    row = math.floor((y - anchor_y) / cell_size_m)
    return int(col), int(row)


def cell_id(col: int, row: int) -> str:
    """Return the canonical cell id for a lattice index, e.g. 'grid_+000_+018'."""
    return CELL_ID_FORMAT.format(col=col, row=row)


def cell_origin(
    col: int,
    row: int,
    cell_size_m: int = DEFAULT_CELL_SIZE_M,
    anchor_x: float = GRID_ANCHOR_X,
    anchor_y: float = GRID_ANCHOR_Y,
) -> Tuple[float, float]:
    """Return the (easting, northing) south-west corner of a lattice cell."""
    return (anchor_x + col * cell_size_m, anchor_y + row * cell_size_m)


def cell_bounds(
    col: int,
    row: int,
    cell_size_m: int = DEFAULT_CELL_SIZE_M,
    anchor_x: float = GRID_ANCHOR_X,
    anchor_y: float = GRID_ANCHOR_Y,
) -> Tuple[float, float, float, float]:
    """Return (minx, miny, maxx, maxy) of a lattice cell, in the anchor CRS."""
    ox, oy = cell_origin(col, row, cell_size_m, anchor_x, anchor_y)
    return (ox, oy, ox + cell_size_m, oy + cell_size_m)


def grid_reference_metadata(
    cell_size_m: int = DEFAULT_CELL_SIZE_M,
    anchor_x: float = GRID_ANCHOR_X,
    anchor_y: float = GRID_ANCHOR_Y,
) -> dict:
    """
    The block written into mesh manifests and attribute metadata so downstream
    consumers (notably the Unity runtime) can reconstruct the lattice without
    hardcoding it.
    """
    return {
        "anchor_x": anchor_x,
        "anchor_y": anchor_y,
        "cell_size_m": cell_size_m,
        "crs": GRID_ANCHOR_CRS,
        "assignment_rule": ASSIGNMENT_RULE,
        "id_format": CELL_ID_FORMAT,
        "half_open_intervals": True,
    }
