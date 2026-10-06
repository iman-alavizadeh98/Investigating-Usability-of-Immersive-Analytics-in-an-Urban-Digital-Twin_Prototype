#!/usr/bin/env python3
"""
Building heights: one number per building (height_m) for extruding flat roofs.

    height_m = p95( z - ground ) of the points inside the footprint

Roof points from the 2018 surface model (Ytmodell från flygbild), ground from the
2010 laser data (Laserdata NH, Lantmäteriet's ground class). The 2010 laser roof
is used only where the 2018 surface has too few points. Buildings without a
height get height_m = 0 and has_height = False; the count and reasons are in
lidar_heights_summary.json, the list in buildings_without_height.csv.

Usage (paths relative to the repo root; these are the defaults):
    python Src/Scripts/run_lidar_height_pipeline.py \\
        --input Processed_data/Helsingborg_Final/buildings_2026-10-06_postprocess/buildings_processed_postprocess.gpkg \\
        --output-dir Processed_data/Helsingborg_Final/lidar_heights \\
        --lidar-dir Raw_data/1-Helsingborig/laserdata_nh_helsingborg \\
        --surface Raw_data/1-Helsingborig/ytmodell_050_helsingborg

    # synthetic tests (no project data)
    python Src/Scripts/run_lidar_height_pipeline.py --test
"""

import argparse
import json
import logging
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from pipelines.lidar_heights.config import LiDARHeightPipelineConfig  # noqa: E402
from pipelines.lidar_heights.pipeline import LiDARHeightPipeline  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")
logger = logging.getLogger(__name__)

# Defaults: Helsingborg (target city since 2026-10-06). Override with the flags.
DEFAULT_INPUT = Path("Processed_data/Helsingborg_Final/buildings_2026-10-06_postprocess/buildings_processed_postprocess.gpkg")
DEFAULT_OUTPUT_DIR = Path("Processed_data/Helsingborg_Final/lidar_heights")
DEFAULT_LIDAR_DIR = Path("Raw_data/1-Helsingborig/laserdata_nh_helsingborg")
DEFAULT_SURFACE = Path("Raw_data/1-Helsingborig/ytmodell_050_helsingborg")


def run_tests() -> int:
    """Run the synthetic tests in tests/test_lidar_heights_pipeline.py."""
    tests_dir = Path(__file__).resolve().parents[2] / "tests"
    sys.path.insert(0, str(tests_dir))
    import test_lidar_heights_pipeline as t  # noqa: E402

    tests = [(n, f) for n, f in sorted(vars(t).items()) if n.startswith("test_") and callable(f)]
    failed = 0
    for name, fn in tests:
        try:
            fn()
            logger.info(f"PASS {name}")
        except Exception as e:  # noqa: BLE001
            failed += 1
            logger.error(f"FAIL {name}: {type(e).__name__}: {e}")
    logger.info(f"{len(tests) - failed}/{len(tests)} passed")
    return 1 if failed else 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Building heights from the 2018 surface model and the 2010 laser data",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--input", type=Path, default=DEFAULT_INPUT,
                        help=f"Buildings postprocess GeoPackage (default: {DEFAULT_INPUT})")
    parser.add_argument("--layer", type=str, default=None,
                        help="GeoPackage layer (default: the file's only layer)")
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR,
                        help=f"Output folder (default: {DEFAULT_OUTPUT_DIR})")
    parser.add_argument("--lidar-dir", type=Path, default=DEFAULT_LIDAR_DIR,
                        help=f"Laserdata NH LAZ folder, 2010 (default: {DEFAULT_LIDAR_DIR})")
    parser.add_argument("--surface", type=Path, default=DEFAULT_SURFACE,
                        help=f"Ytmodell LAZ tiles, 2018: extracted folder (or the zip) (default: {DEFAULT_SURFACE})")
    parser.add_argument("--no-surface", action="store_true",
                        help="Use the 2010 laser data only")
    parser.add_argument("--work-cell", type=float, default=1000.0,
                        help="Processing cell size in m; smaller uses less memory (default: 1000)")
    parser.add_argument("--config", type=Path, default=None,
                        help="JSON config (LiDARHeightPipelineConfig fields); replaces the path flags")
    parser.add_argument("--test", action="store_true", help="Run the synthetic tests and exit")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    if args.test:
        return run_tests()

    if args.config:
        with open(args.config, encoding="utf-8") as f:
            config = LiDARHeightPipelineConfig.from_dict(json.load(f))
    else:
        config = LiDARHeightPipelineConfig(
            input_buildings_path=args.input,
            output_directory=args.output_dir,
            lidar_directory=args.lidar_dir,
            surface_path=None if args.no_surface else args.surface,
            input_layer=args.layer,
            work_cell_m=args.work_cell,
        )
    config.verbose = args.verbose

    logger.info(f"Input:   {config.input_buildings_path}")
    logger.info(f"Laser:   {config.lidar_directory}")
    logger.info(f"Surface: {config.surface_path}")
    logger.info(f"Output:  {config.output_directory}")
    logger.info(f"Run ID:  {config.height_run_id}")

    report = LiDARHeightPipeline(config).run(config.output_directory)
    if report["status"] != "success":
        logger.error(f"Pipeline failed: {report.get('error', 'unknown error')}")
        return 1
    s = report["stages"]["export"]["summary"]
    logger.info(
        f"Done: {s['buildings_with_height']:,} of {s['total_buildings']:,} buildings have a height; "
        f"{s['buildings_without_height']:,} without ({s['percent_without_height']}%): "
        f"{s['without_height_by_reason']}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
