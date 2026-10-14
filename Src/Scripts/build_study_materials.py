#!/usr/bin/env python3
"""
Build the 2D vs 3D pattern-study materials: the participant 2D maps and the
facilitator handbook (with answers).

    python Src/Scripts/build_study_materials.py --config configs/cities/<city>.json
        [--scenarios configs/study/pattern_scenarios.json] [--output <folder>]

Read-only. Needs the city's Unity package (build_unity_package.py) and the
config section dataReport (employment, roadMap). Default output:
<dataReport.output>/../study (Processed_data, git-ignored: the maps show
FUK-licensed data).
"""

import argparse
import logging
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "Src"))

from pipelines.data_report.study import build_study_materials  # noqa: E402
from pipelines.unity_package.config import load_city_config  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")


def main() -> int:
    p = argparse.ArgumentParser(description="Build 2D maps and facilitator handbook for the pattern study")
    p.add_argument("--config", required=True)
    p.add_argument("--scenarios", default="configs/study/pattern_scenarios.json")
    p.add_argument("--output", default=None)
    a = p.parse_args()
    cfg = load_city_config(Path(a.config), REPO_ROOT)
    rc = cfg.raw.get("dataReport") or {}
    out = Path(a.output) if a.output else (REPO_ROOT / rc.get("output", "Processed_data/reports")).parent / "study"
    paths = build_study_materials(Path(a.config), REPO_ROOT / a.scenarios, REPO_ROOT, out)
    print("\n" + "=" * 80)
    for k, v in paths.items():
        print(f"{k:9s} {v}")
    print("=" * 80)
    return 0


if __name__ == "__main__":
    sys.exit(main())
