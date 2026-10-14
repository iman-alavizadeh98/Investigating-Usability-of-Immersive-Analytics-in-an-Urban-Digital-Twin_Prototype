#!/usr/bin/env python3
"""
Build a city's Unity runtime package from its city config.

    python Src/Scripts/Unity/build_unity_package.py --config configs/cities/<city>.json

Writes StreamingAssets/cities/<packageDirectory>/ (git-ignored) with the exported layers and
project_manifest.json. The manifest is always rewritten from what is in the
package, so it never needs hand editing.

    --only buildings   export the buildings, then rewrite the manifest
    --only analytics   export SCB grid/DeSO, voting districts, building links and
                       district estimates (config sections statistics/election/estimates)
    --only employment  add/replace only the DeSO employment layer (config
                       statistics.employment) in an existing package; needs the
                       package's DeSO layer, not the other raw deliveries
    --only visualizations   check the presets of config "visualizations" (and each
                       "visualizationSets" folder) against the package and copy them
                       to <package>/visualizations/ (sets: visualizations/<name>/)
    --only manifest    only rewrite the manifest

In the scene, point VisualizationSwitcher's catalog path at
"cities/<packageDirectory>/visualizations/catalog.json".

In the Unity scene, set ProjectManager's manifest path to
"cities/<packageDirectory>/project_manifest.json" (printed at the end).
"""

import argparse
import json
import logging
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT / "Src"))

from pipelines.unity_package.analytics import export_analytics  # noqa: E402
from pipelines.unity_package.buildings import export_buildings  # noqa: E402
from pipelines.unity_package.config import load_city_config  # noqa: E402
from pipelines.unity_package.manifest import write_manifest  # noqa: E402
from pipelines.unity_package.visualizations import export_visualization_sets  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")
logger = logging.getLogger("build_unity_package")

STEPS = ("buildings", "analytics", "employment", "visualizations", "manifest")


def add_employment_only(city) -> dict:
    """The DeSO employment layer for an existing package (its DeSO unit ids are read from the package)."""
    import pandas as pd
    from pipelines.scb.loader import load_scb_tables
    from pipelines.unity_package.statistics import export_deso_employment

    geometry = city.package_dir / "spatial_layers" / "deso" / "geometry.json"
    if not geometry.exists():
        raise FileNotFoundError(f"{geometry} not found: build the package (step analytics) first")
    deso_ids = pd.Index([u["id"] for u in json.loads(geometry.read_text(encoding="utf-8"))["units"]])
    return export_deso_employment(city.package_dir, load_scb_tables(city.statistics.employment), deso_ids)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a city's Unity runtime package")
    parser.add_argument("--config", type=Path, required=True, help="City config JSON (configs/cities/<city>.json)")
    parser.add_argument("--only", choices=STEPS, default=None,
                        help="Run one step (the manifest is rewritten after every step)")
    args = parser.parse_args()

    city = load_city_config(args.config, REPO_ROOT)
    logger.info(f"City: {city.display_name} ({city.city_id}) -> {city.package_dir}")

    reports = {}
    if args.only in (None, "buildings"):
        if city.buildings is None:
            if args.only == "buildings":
                parser.error("the city config has no 'buildings' section")
            logger.info("No 'buildings' section in the config; skipped")
        else:
            reports["buildings"] = export_buildings(city)["report"]

    if args.only in (None, "analytics"):
        if not (city.statistics or city.election):
            if args.only == "analytics":
                parser.error("the city config has no 'statistics' or 'election' section")
            logger.info("No 'statistics'/'election' section in the config; analytics skipped")
        else:
            analytics = export_analytics(city)
            reports["analytics"] = {k: v for k, v in analytics.items() if k in ("dasymetric", "valdistrikt_estimates", "associations")}

    if args.only == "employment":
        if city.statistics is None or city.statistics.employment is None:
            parser.error("the city config has no 'statistics.employment' folder")
        reports["deso_employment"] = add_employment_only(city)

    if args.only in (None, "visualizations"):
        if city.visualizations is None and not city.visualization_sets:
            if args.only == "visualizations":
                parser.error("the city config has no 'visualizations' folder")
        else:
            reports["visualizations"] = export_visualization_sets(
                city.package_dir, city.visualizations, city.visualization_sets)

    manifest_path = write_manifest(city)
    logger.info(json.dumps(reports, indent=2, ensure_ascii=False))
    logger.info(f"Done. In Unity, set ProjectManager's manifest path to: {city.manifest_path_in_streaming_assets}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
