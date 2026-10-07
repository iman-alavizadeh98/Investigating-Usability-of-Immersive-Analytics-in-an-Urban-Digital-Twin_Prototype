#!/usr/bin/env python3
"""
Run the income (SCB Inkomster) preprocess pipeline.

Loads every Tab*_<unit>_<year>.shp in the folder unchanged, checks it, and writes
HTML views of each table, a profile per table, an index page and a JSON report.
No processing rules are applied yet.

Usage (city-agnostic: pass the city's delivery folder and an output folder):
    python Src/Scripts/run_income_pipeline.py --input <inkomster folder> --output <city output folder>

Outputs go to <output>/income_<YYYY-MM-DD>/; open index.html there. The pages
contain the licensed SCB data, so use an ignored folder such as Processed_data/.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from pipelines.income import IncomePipeline  # noqa: E402
from pipelines.scb.cli import run_cli  # noqa: E402

if __name__ == "__main__":
    sys.exit(run_cli(IncomePipeline, "Load, check and view SCB income (Inkomster) tables"))
