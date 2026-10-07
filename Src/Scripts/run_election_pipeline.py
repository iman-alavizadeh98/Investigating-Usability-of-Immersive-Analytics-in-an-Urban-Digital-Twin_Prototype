#!/usr/bin/env python3
"""
Run the election preprocess pipeline.

Loads the results workbook (votes per voting district, whole country) and the
voting-district boundaries (whole county) from one folder, keeps the given
municipality, checks the data and writes HTML views, profiles, an index page
and a JSON report. No processing rules are applied yet.

Usage (city-agnostic: pass the folder and the municipality code, Kommunkod):
    python Src/Scripts/run_election_pipeline.py --input <election folder> --municipality <4-digit code> --output <city output folder>

Outputs go to <output>/election_<YYYY-MM-DD>/; open index.html there.
"""

import argparse
import logging
import sys
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from pipelines.election import ElectionPipeline  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(name)s] %(levelname)s: %(message)s")


def main() -> int:
    parser = argparse.ArgumentParser(description="Load, check and view election results per voting district")
    parser.add_argument("--input", required=True, help="Folder with one results .xlsx and one boundary file")
    parser.add_argument("--municipality", required=True, help="4-digit municipality code (Kommunkod)")
    parser.add_argument("--output", required=True, help="Output folder (a dated election_<date> subfolder is created)")
    args = parser.parse_args()

    run_dir = Path(args.output) / f"election_{datetime.now():%Y-%m-%d}"
    pipeline = ElectionPipeline(config={"input_dir": args.input, "municipality": args.municipality})
    report = pipeline.run(output_dir=run_dir)

    print("\n" + "=" * 80)
    print(f"ELECTION PIPELINE: {report.get('status')}")
    if report.get("status") != "success":
        print(f"Error: {report.get('error')}")
        return 1
    print(f"Frames loaded: {len(pipeline.data)}")
    print(f"Issues found:  {pipeline.validation_report.get('issue_count', 0)} (listed in index.html)")
    print(f"Open:          {pipeline.outputs['index']}")
    print("=" * 80)
    return 0


if __name__ == "__main__":
    sys.exit(main())
