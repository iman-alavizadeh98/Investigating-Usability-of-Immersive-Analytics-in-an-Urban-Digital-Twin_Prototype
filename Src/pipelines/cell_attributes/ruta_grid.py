"""
SCB Ruta lattice mathematics.

WHY THIS KEYS OFF THE CODE, NOT THE GEOMETRY
--------------------------------------------
Every SCB Ruta cell carries a 13-character `Ruta` code: a 6-digit easting followed
by a 7-digit northing, naming the cell's south-west corner in EPSG:3006. For
example "3170006394000" -> (317000, 6394000).

The code is EXACT. The shapefile geometry is not:

  - vertex coordinates carry ~4 mm of float noise (minx values look like
    317000.0022752948, widths like 249.9975...);
  - 40 cells are CLIPPED at the municipal boundary, so their geometry is smaller
    than their nominal size (26 southern cells sit on y=6383590 and are only
    160 m or 410 m tall).

Measured on Tab1 (4139 cells), binning into the 500 m analytical lattice:

    via the Ruta code : 3936 / 3936 fine cells nest into exactly one target cell
    via the geometry  :  981 / 3936   <- float noise straddles bin edges

So all lattice math here uses the code. Clipping then becomes irrelevant: the
nominal corner is what defines cell membership, and area-weighting is never needed.

RESOLUTIONS
-----------
SCB mixes cell sizes for disclosure control: dense areas get 100 m or 250 m cells,
sparse areas 1000 m so small counts cannot identify individuals. Verified: every
layer's cells satisfy `easting % size == 0` and `northing % size == 0` (with 10
known exceptions in Tab11, flagged rather than assumed clean -- see
validate_lattice).
"""

from __future__ import annotations

import logging
from typing import Iterable, List, Tuple

import numpy as np
import pandas as pd

logger = logging.getLogger(__name__)

#: Length of a well-formed Ruta code: 6-digit easting + 7-digit northing.
RUTA_CODE_LENGTH = 13
_EASTING_DIGITS = 6


def parse_ruta_codes(codes: Iterable[str]) -> Tuple[np.ndarray, np.ndarray]:
    """
    Vectorised parse of Ruta codes into south-west corner coordinates.

    Args:
        codes: Iterable of 13-character Ruta codes.

    Returns:
        (easting, northing) int64 arrays, EPSG:3006 metres.

    Raises:
        ValueError: if any code is not 13 characters or is not numeric. Fail loudly
            rather than silently producing coordinates in the wrong place.
    """
    series = pd.Series(list(codes), dtype="string").str.strip()

    bad_length = series[series.str.len() != RUTA_CODE_LENGTH]
    if len(bad_length) > 0:
        raise ValueError(
            f"{len(bad_length)} Ruta code(s) are not {RUTA_CODE_LENGTH} characters, "
            f"e.g. {bad_length.head(3).tolist()}. Cannot derive cell corners."
        )

    if not series.str.isdigit().all():
        offenders = series[~series.str.isdigit()].head(3).tolist()
        raise ValueError(f"Non-numeric Ruta code(s), e.g. {offenders}")

    easting = series.str[:_EASTING_DIGITS].astype("int64").to_numpy()
    northing = series.str[_EASTING_DIGITS:].astype("int64").to_numpy()
    return easting, northing


def target_cell_indices(
    easting: np.ndarray,
    northing: np.ndarray,
    anchor_x: float,
    anchor_y: float,
    cell_size_m: int,
) -> Tuple[np.ndarray, np.ndarray]:
    """
    Lattice (col, row) of the target cell containing each source corner.

    Half-open, matching mesh_generation.grid_reference: a corner exactly on a
    boundary belongs to the cell above/right.
    """
    cols = np.floor((easting - anchor_x) / cell_size_m).astype(np.int64)
    rows = np.floor((northing - anchor_y) / cell_size_m).astype(np.int64)
    return cols, rows


def nests_cleanly(
    easting: np.ndarray,
    northing: np.ndarray,
    source_size: np.ndarray,
    anchor_x: float,
    anchor_y: float,
    cell_size_m: int,
) -> np.ndarray:
    """
    Boolean mask: does each source cell fall entirely inside ONE target cell?

    A source cell nests when its SW and NE corners land in the same target cell.
    Sources larger than the target (e.g. 1000 m into 500 m) never nest and must be
    split; see aggregate.reconcile_resolutions.
    """
    c0, r0 = target_cell_indices(easting, northing, anchor_x, anchor_y, cell_size_m)
    # -1 keeps the far edge inside the same cell under half-open bounds.
    c1, r1 = target_cell_indices(
        easting + source_size - 1, northing + source_size - 1,
        anchor_x, anchor_y, cell_size_m,
    )
    return (c0 == c1) & (r0 == r1)


def covered_target_cells(
    easting: int,
    northing: int,
    source_size: int,
    anchor_x: float,
    anchor_y: float,
    cell_size_m: int,
) -> List[Tuple[int, int]]:
    """
    Every target cell covered by one source cell.

    Length 1 when the source is no larger than the target (the common case);
    length 4 for a 1000 m source against a 500 m target.
    """
    step = min(source_size, cell_size_m)
    cells = set()
    for dx in range(0, source_size, step):
        for dy in range(0, source_size, step):
            col = int(np.floor((easting + dx - anchor_x) / cell_size_m))
            row = int(np.floor((northing + dy - anchor_y) / cell_size_m))
            cells.add((col, row))
    return sorted(cells)


def validate_lattice(
    easting: np.ndarray,
    northing: np.ndarray,
    source_size: np.ndarray,
    layer_id: str = "",
) -> dict:
    """
    Check that source corners sit on their own declared lattice.

    REPORTS rather than raises: Tab11 contains 10 rows labelled `Rutstorl=1000`
    whose corners sit on a 250 m offset. They still nest into a single 500 m target
    cell, so aggregation stays correct, but the size label is demonstrably not
    trustworthy and must be surfaced (CLAUDE.md §7: never assume labels are clean
    just because they look official).
    """
    off_lattice = (easting % source_size != 0) | (northing % source_size != 0)
    count = int(off_lattice.sum())

    report = {
        "layer_id": layer_id,
        "total_cells": int(len(easting)),
        "off_lattice_cells": count,
        "off_lattice_fraction": round(count / max(1, len(easting)), 6),
        "sizes_present": sorted({int(s) for s in np.unique(source_size)}),
    }

    if count:
        idx = np.flatnonzero(off_lattice)[:5]
        report["examples"] = [
            {
                "easting": int(easting[i]),
                "northing": int(northing[i]),
                "declared_size_m": int(source_size[i]),
            }
            for i in idx
        ]
        logger.warning(
            f"[{layer_id}] {count} cell(s) have a corner that is not a multiple of "
            f"their declared size. The size label is unreliable for these rows; "
            f"aggregation still uses the nominal corner."
        )

    return report
