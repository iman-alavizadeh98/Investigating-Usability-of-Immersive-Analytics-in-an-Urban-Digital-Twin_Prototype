"""
Shared command line for SCB delivery pipelines (run_population_pipeline.py,
run_income_pipeline.py): --input <folder> --output <folder> [--tables ...].
Outputs go to <output>/<dataset_id>_<YYYY-MM-DD>/.
"""

from __future__ import annotations

import argparse
import logging
from datetime import datetime
from pathlib import Path
from typing import Type

from .pipeline import ScbTablesPipeline


def run_cli(pipeline_cls: Type[ScbTablesPipeline], description: str) -> int:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(name)s] %(levelname)s: %(message)s")
    dataset_id = pipeline_cls.dataset.dataset_id
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument("--input", required=True, help=f"Folder with the SCB {dataset_id} shapefiles")
    parser.add_argument(
        "--output", required=True, help=f"Output folder (a dated {dataset_id}_<date> subfolder is created)"
    )
    parser.add_argument(
        "--tables", nargs="+", default=None,
        help="Only load files whose name starts with one of these, e.g. Tab1_Ruta Tab2",
    )
    args = parser.parse_args()

    run_dir = Path(args.output) / f"{dataset_id}_{datetime.now():%Y-%m-%d}"
    pipeline = pipeline_cls(config={"input_dir": args.input, "tables": args.tables})
    report = pipeline.run(output_dir=run_dir)

    print("\n" + "=" * 80)
    print(f"{dataset_id.upper()} PIPELINE: {report.get('status')}")
    if report.get("status") != "success":
        print(f"Error: {report.get('error')}")
        return 1
    print(f"Tables loaded: {len(pipeline.tables)}")
    print(f"Issues found:  {pipeline.validation_report.get('issue_count', 0)} (listed in index.html)")
    print(f"Open:          {pipeline.outputs['index']}")
    print("=" * 80)
    return 0
