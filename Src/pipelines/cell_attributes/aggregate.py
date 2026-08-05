"""
Aggregate SCB Ruta statistics onto the frozen 500 m analytical lattice.

THE DOUBLE-COUNTING THIS REPLACES
---------------------------------
The deprecated Src/Scripts/legacy/preprocess_spatial_joins.py added each source
cell's FULL total to EVERY building overlapping it: a cell with 200 residents and
50 buildings gave all 50 buildings 200 people each. It also deduplicated on `Ruta`
alone, silently losing 2,186 residents (717,781 -> 715,595), because the same
`Ruta` code is reused across cell sizes.

Here each source cell is counted EXACTLY ONCE, into exactly one target cell, and
the result is checked against a known total.

RESOLUTION RECONCILIATION (rule: fine_priority_disjoint_coarse)
---------------------------------------------------------------
SCB mixes cell sizes for disclosure control. The sub-layers are NOT a clean
partition and NOT a parent/child decomposition. Measured on Tab1:

  - 3,936 cells at 250 m (712,209 residents), 203 at 1000 m (5,572)
  - the two sub-layers overlap over 18.3% of the coarse area
  - 98 coarse cells contain at least one fine cell
  - but 83 of those have a NEGATIVE remainder: the fine cells inside them hold
    37,337 residents against the coarse cell's claimed 2,091

That last figure rules out treating coarse cells as parents to be decremented --
subtraction is not justified by the data. The safe reading is that the coarse
layer describes areas the fine layer does not cover.

Rule applied: take every fine cell; add a coarse cell ONLY where no finer cell
covers it. Yields 715,690 of 717,781 (99.71%) across 1,766 occupied target cells.
The 2,091 discarded residents sit in areas already described at finer resolution.

Alternatives rejected:
  - fine-only: loses all 5,572 rural residents; rural cells would render as zero
    population rather than sparse, i.e. look like missing data;
  - area-weighted: reintroduces float tolerance and the clipped-cell problem for
    0.78% of the population, when Ruta-code nesting is already exact.
"""

from __future__ import annotations

import logging
from typing import Dict, List, Optional

import numpy as np
import pandas as pd

from .ruta_grid import (
    covered_target_cells,
    parse_ruta_codes,
    target_cell_indices,
    validate_lattice,
)

logger = logging.getLogger(__name__)


class ConservationError(Exception):
    """Raised when aggregation does not preserve the expected source total."""


def prepare_source_frame(gdf, spec, anchor_x: float, anchor_y: float, cell_size_m: int):
    """
    Normalise one source layer: parse codes, derive lattice indices, alias fields.

    The primary key is (cell_size_m, cell_code), never cell_code alone -- 48 codes
    are shared between the 250 m and 1000 m sub-layers, and deduplicating on the
    code alone silently drops rows.
    """
    from .config import RUTA_FIELD_TRANSLATIONS

    df = pd.DataFrame(gdf.drop(columns="geometry", errors="ignore"))

    size_field = _resolve_column(df, spec.size_field, "cell size")
    code_field = _resolve_column(df, spec.code_field, "cell code")

    easting, northing = parse_ruta_codes(df[code_field].tolist())
    sizes = df[size_field].to_numpy().astype(np.int64)

    lattice_report = validate_lattice(easting, northing, sizes, spec.layer_id)

    cols, rows = target_cell_indices(easting, northing, anchor_x, anchor_y, cell_size_m)

    out = df.copy()
    out["cell_code"] = df[code_field].astype(str)
    out["source_size_m"] = sizes
    out["source_easting"] = easting
    out["source_northing"] = northing
    out["grid_col"] = cols
    out["grid_row"] = rows

    duplicate_codes = int(out.duplicated(subset=["cell_code"]).sum())
    duplicate_keys = int(out.duplicated(subset=["source_size_m", "cell_code"]).sum())
    if duplicate_keys:
        raise ValueError(
            f"[{spec.layer_id}] {duplicate_keys} duplicate (size, code) key(s); "
            "the source is not uniquely keyed as expected."
        )

    # Rename measure columns to English aliases; keep originals recorded elsewhere.
    rename = {c: RUTA_FIELD_TRANSLATIONS[c] for c in out.columns if c in RUTA_FIELD_TRANSLATIONS}
    out = out.rename(columns=rename)

    quality = {
        "lattice": lattice_report,
        "duplicate_cell_codes": duplicate_codes,
        "note_duplicate_codes": (
            f"{duplicate_codes} `Ruta` code(s) appear at more than one cell size; "
            "keyed on (size, code) so no rows are lost."
            if duplicate_codes else "cell codes unique"
        ),
    }
    return out, rename, quality


def _resolve_column(df: pd.DataFrame, preferred: str, what: str) -> str:
    """
    Find a column allowing for SCB's spelling variants and DBF mojibake.

    Handles e.g. Rutstorl vs AstRutstor, and columns whose names were mangled by
    an undeclared DBF encoding.
    """
    if preferred in df.columns:
        return preferred

    from .config import RUTA_FIELD_TRANSLATIONS

    target_alias = RUTA_FIELD_TRANSLATIONS.get(preferred)
    for column in df.columns:
        if RUTA_FIELD_TRANSLATIONS.get(column) == target_alias:
            logger.warning(
                f"Column '{preferred}' not found; using spelling variant '{column}' "
                f"for {what}."
            )
            return column

    raise KeyError(
        f"No column for {what} (wanted '{preferred}'). Available: {list(df.columns)}"
    )


def reconcile_resolutions(df: pd.DataFrame, total_alias: str = "total") -> tuple:
    """
    Apply fine_priority_disjoint_coarse to a prepared source frame.

    Returns:
        (kept_frame, report). `kept_frame` holds every source cell that should be
        counted exactly once.
    """
    sizes = sorted(df["source_size_m"].unique())
    if len(sizes) == 1:
        return df.copy(), {
            "rule": "single_resolution",
            "sizes_present": [int(s) for s in sizes],
            "kept_cells": int(len(df)),
            "dropped_cells": 0,
            "dropped_total": 0,
        }

    finest = int(min(sizes))
    fine = df[df["source_size_m"] == finest]
    coarse = df[df["source_size_m"] != finest]

    # Footprint of the fine layer, expressed on each coarse cell's own lattice, so
    # the containment test is integer arithmetic rather than a spatial predicate.
    dropped_frames: List[pd.DataFrame] = []
    kept_frames: List[pd.DataFrame] = [fine]

    for size in sorted(coarse["source_size_m"].unique()):
        block = coarse[coarse["source_size_m"] == size]
        fine_boxes = set(
            zip(
                (fine["source_easting"] // size * size).tolist(),
                (fine["source_northing"] // size * size).tolist(),
            )
        )
        covered = [
            (e, n) in fine_boxes
            for e, n in zip(block["source_easting"], block["source_northing"])
        ]
        covered = np.asarray(covered, dtype=bool)
        kept_frames.append(block[~covered])
        dropped_frames.append(block[covered])

    kept = pd.concat(kept_frames, ignore_index=True)
    dropped = (
        pd.concat(dropped_frames, ignore_index=True)
        if dropped_frames else pd.DataFrame(columns=df.columns)
    )

    dropped_total = int(dropped[total_alias].sum()) if total_alias in dropped else 0

    report = {
        "rule": "fine_priority_disjoint_coarse",
        "sizes_present": [int(s) for s in sizes],
        "finest_size_m": finest,
        "kept_cells": int(len(kept)),
        "dropped_cells": int(len(dropped)),
        "dropped_total": dropped_total,
        "rationale": (
            "Coarse cells overlapping the finer layer are dropped because the finer "
            "layer already describes that ground. Subtraction was rejected: 83 of 98 "
            "overlapping coarse cells yield a negative remainder, so the layers are "
            "not a parent/child decomposition."
        ),
    }
    return kept, report


def aggregate_to_target_cells(
    df: pd.DataFrame,
    count_aliases: List[str],
    anchor_x: float,
    anchor_y: float,
    cell_size_m: int,
) -> pd.DataFrame:
    """
    Sum count fields into target lattice cells.

    Source cells larger than the target are split across the cells they cover
    using integer largest-remainder apportionment, so the parts sum EXACTLY to the
    original value and conservation stays exact rather than approximate.
    """
    present = [c for c in count_aliases if c in df.columns]
    if not present:
        raise ValueError(f"None of the count fields {count_aliases} are present")

    oversized = df["source_size_m"] > cell_size_m
    simple = df[~oversized]
    split = df[oversized]

    frames = []
    if len(simple):
        frames.append(
            simple.groupby(["grid_col", "grid_row"], as_index=False)[present].sum()
        )

    if len(split):
        frames.append(
            _split_oversized(split, present, anchor_x, anchor_y, cell_size_m)
        )

    combined = pd.concat(frames, ignore_index=True)
    result = combined.groupby(["grid_col", "grid_row"], as_index=False)[present].sum()

    for column in present:
        result[column] = result[column].astype("int64")
    return result


def _split_oversized(
    df: pd.DataFrame,
    count_aliases: List[str],
    anchor_x: float,
    anchor_y: float,
    cell_size_m: int,
) -> pd.DataFrame:
    """
    Distribute a source cell larger than the target across the cells it covers.

    Equal shares with integer largest-remainder apportionment: simple to explain,
    conserves the total exactly, and the affected cells are sparse rural areas
    (median 14 residents) where the spatial error is small. The rule is recorded
    in the output metadata so it can be swapped for a floor-area weighting later.
    """
    rows = []
    for record in df.itertuples(index=False):
        data = record._asdict()
        cells = covered_target_cells(
            int(data["source_easting"]), int(data["source_northing"]),
            int(data["source_size_m"]), anchor_x, anchor_y, cell_size_m,
        )
        n = len(cells)
        shares = {c: _apportion(int(data.get(c, 0) or 0), n) for c in count_aliases}
        for i, (col, row) in enumerate(cells):
            entry = {"grid_col": col, "grid_row": row}
            entry.update({c: shares[c][i] for c in count_aliases})
            rows.append(entry)
    return pd.DataFrame(rows)


def _apportion(total: int, parts: int) -> List[int]:
    """Split an integer into `parts` near-equal integers summing exactly to it."""
    if parts <= 0:
        return []
    base, remainder = divmod(int(total), parts)
    return [base + (1 if i < remainder else 0) for i in range(parts)]


def check_conservation(
    source_df: pd.DataFrame,
    target_df: pd.DataFrame,
    total_alias: str,
    layer_id: str,
    dropped_total: int = 0,
    strict: bool = True,
) -> Dict:
    """
    Verify that aggregation preserved the reconciled source total.

    Raises ConservationError when the totals disagree: a mismatch means people
    were duplicated or lost, which is the exact class of bug this pipeline exists
    to prevent.
    """
    source_total = int(source_df[total_alias].sum()) if total_alias in source_df else 0
    target_total = int(target_df[total_alias].sum()) if total_alias in target_df else 0
    difference = target_total - source_total

    report = {
        "layer_id": layer_id,
        "reconciled_source_total": source_total,
        "aggregated_target_total": target_total,
        "difference": difference,
        "dropped_by_reconciliation": dropped_total,
        "conserved": difference == 0,
        "occupied_target_cells": int(len(target_df)),
    }

    if difference != 0:
        message = (
            f"[{layer_id}] conservation FAILED: aggregated {target_total:,} vs "
            f"reconciled source {source_total:,} (difference {difference:+,})"
        )
        if strict:
            raise ConservationError(message)
        logger.error(message)
    else:
        logger.info(
            f"[{layer_id}] conservation OK: {target_total:,} preserved across "
            f"{len(target_df):,} target cells"
        )

    return report
