#!/usr/bin/env python3
"""
Build a PDF data report for one city: every dataset, profiled, mapped and compared.

    python Src/Scripts/build_data_report.py --config configs/cities/<city>.json [--output <folder>]

Read-only. Needs the city's processed buildings, its Unity package (run
build_unity_package.py first) and the raw deliveries named in the config
(sections buildings, statistics, election and dataReport).

Output: <output>/<city>_data_report.pdf plus figures/*.png. The default output
folder is config `dataReport.output`. The PDF shows FUK-licensed data: keep it
in an ignored folder (Processed_data/), never in reports/ or docs/.
"""

import argparse
import logging
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "Src"))

from pipelines.data_report.report import build_report  # noqa: E402
from pipelines.data_report.sources import load_report_data  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")
logger = logging.getLogger("build_data_report")


def main() -> int:
    parser = argparse.ArgumentParser(description="Build the PDF data report for a city")
    parser.add_argument("--config", required=True, help="City config, e.g. configs/cities/helsingborg.json")
    parser.add_argument("--output", default=None, help="Output folder (default: config dataReport.output)")
    args = parser.parse_args()

    data = load_report_data(Path(args.config), REPO_ROOT)
    out = Path(args.output) if args.output else REPO_ROOT / (data.report_cfg.get("output") or "Processed_data/reports")
    pdf = build_report(data, out)
    print("\n" + "=" * 80)
    print(f"Data report: {pdf}")
    print("=" * 80)
    return 0


if __name__ == "__main__":
    sys.exit(main())
