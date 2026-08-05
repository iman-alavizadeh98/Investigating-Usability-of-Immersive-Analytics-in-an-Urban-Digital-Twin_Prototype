#!/usr/bin/env python3
"""
Aggregate SCB Ruta statistics onto the frozen 500 m analytical grid.

Attributes attach to the CELL, keyed by the same cell id the mesh manifest uses,
so the Unity runtime joins analytics to geometry by dictionary lookup.

Usage:
    python run_cell_attributes.py                                   # population by age
    python run_cell_attributes.py --layers population_age,income_quartiles
    python run_cell_attributes.py --all                             # every registered layer
    python run_cell_attributes.py --list                            # show the registry

Replaces Src/Scripts/legacy/preprocess_spatial_joins.py, which gave every building
in a cell that cell's FULL population and deduplicated on `Ruta` alone (losing
2,186 residents).
"""

import argparse
import logging
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from pipelines.cell_attributes.config import (
    CellAttributeConfig,
    DEFAULT_LAYERS,
    LAYER_REGISTRY,
)
from pipelines.cell_attributes.pipeline import CellAttributePipeline

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
)
logger = logging.getLogger(__name__)


def main():
    parser = argparse.ArgumentParser(
        description="Aggregate SCB Ruta statistics onto the 500 m analytical grid"
    )
    parser.add_argument(
        "--layers",
        default=",".join(DEFAULT_LAYERS),
        help=f"Comma-separated layer ids (default: {','.join(DEFAULT_LAYERS)})",
    )
    parser.add_argument("--all", action="store_true", help="Process every registered layer")
    parser.add_argument("--list", action="store_true", help="List registered layers and exit")
    parser.add_argument(
        "--cell-size", type=int, default=500,
        help="Target cell size in metres (default: 500, matching the mesh grid)",
    )
    parser.add_argument(
        "--output", default=None,
        help="Output directory (default: Processed_data/analytics/cell_attributes_<size>m)",
    )
    parser.add_argument(
        "--allow-total-mismatch", action="store_true",
        help="Warn instead of failing when a layer's source total differs from the "
             "recorded expected total (i.e. the source data changed).",
    )
    args = parser.parse_args()

    if args.list:
        print(f"{'layer_id':26s} {'theme':12s} {'expected total':>15s}  description")
        for layer_id, spec in LAYER_REGISTRY.items():
            total = f"{spec.expected_total:,}" if spec.expected_total else "-"
            print(f"{layer_id:26s} {spec.theme:12s} {total:>15s}  {spec.description_en}")
        return 0

    layers = tuple(LAYER_REGISTRY) if args.all else tuple(
        s.strip() for s in args.layers.split(",") if s.strip()
    )

    output_dir = (
        Path(args.output) if args.output
        else Path(f"Processed_data/analytics/cell_attributes_{args.cell_size}m")
    )

    config = CellAttributeConfig(
        layers=layers,
        cell_size_m=args.cell_size,
        output_dir=output_dir,
        enforce_expected_totals=not args.allow_total_mismatch,
    )

    logger.info("=" * 78)
    logger.info(f"Cell attributes -> {args.cell_size}m analytical grid")
    logger.info(f"Layers: {', '.join(layers)}")
    logger.info("=" * 78)

    try:
        written = CellAttributePipeline(config).run()
    except Exception as exc:
        logger.error(f"Pipeline failed: {exc}")
        return 1

    logger.info("=" * 78)
    for kind, path in written.items():
        logger.info(f"  {kind:9s} {path}")
    logger.info("=" * 78)
    return 0


if __name__ == "__main__":
    sys.exit(main())
