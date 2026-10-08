"""
Analytical layers of the Unity package: SCB grid + DeSO statistics, voting
districts with election results, building links and district estimates.

Run through build_unity_package.py (step "analytics"). Every part is optional
and follows the city config: no `statistics` section → no grid/DeSO layers, no
`election` section → no voting districts, no `buildings` section → no links.

Writes an integration report (counts, coverage, validation) to
<package>/analytics_report.json; the package folder is git-ignored because it
holds licensed data.
"""

from __future__ import annotations

import json
import logging
from datetime import datetime, timezone
from typing import Dict

import pandas as pd

from .config import CityConfig

logger = logging.getLogger(__name__)

REPORT_FILE = "analytics_report.json"


def export_analytics(city: CityConfig) -> Dict:
    from .election import export_election
    from .links import (
        dasymetric, export_area_names, export_associations, export_district_estimates, grid_counts,
        load_building_points, statistics_extent, validate_100m,
    )
    from .statistics import export_deso, export_ruta, find_table, load_statistics

    package_dir = city.package_dir
    report: Dict = {"city": city.city_id, "exportedAt": datetime.now(timezone.utc).isoformat()}
    layers = {}

    population = income = None
    if city.statistics:
        logger.info("Statistics (SCB grid + DeSO)")
        population, income = load_statistics(city.statistics.population, city.statistics.income)
        ruta_units, report["ruta"] = export_ruta(package_dir, population, income)
        deso_units, report["deso"] = export_deso(package_dir, population, income)
        layers["ruta"], layers["deso"] = ruta_units, deso_units

    districts = None
    if city.election:
        logger.info("Election (voting districts)")
        districts, report["election"] = export_election(
            package_dir, city.election.folder, city.election.municipality_code, city.election.min_party_share_pct)
        layers["valdistrikt"] = districts
        for layer_id in ("ruta", "deso"):
            if layer_id in layers:
                report[f"{layer_id}_places"] = export_area_names(package_dir, layer_id, layers[layer_id], districts)

    if city.buildings and layers:
        logger.info("Building links")
        points = load_building_points(city.buildings.source, city.buildings.layer)
        report["associations"] = export_associations(package_dir, points, layers)

        if population is not None and districts is not None:
            logger.info("District estimates (dasymetric)")
            counts = grid_counts(population, income or {}, pd.Index(layers["ruta"]["unit_id"]))
            alloc, leftover, report["dasymetric"] = dasymetric(points, layers["ruta"], counts, city.estimates.storey_height_m)
            report["dasymetric"]["validation_100m"] = validate_100m(alloc, points, population)
            eligible = _eligible_from_package(package_dir)
            years = {
                "population": getattr(find_table(population, "1", "Ruta", {250, 1000}), "year", None),
                "income": getattr(find_table(income or {}, "11", "Ruta", {250, 1000}), "year", None),
            }
            report["valdistrikt_estimates"] = export_district_estimates(
                package_dir, alloc, leftover, points, layers["ruta"], districts, eligible,
                statistics_extent(population), city.estimates.min_coverage_pct, years)

    (package_dir / REPORT_FILE).write_text(json.dumps(report, ensure_ascii=False, indent=2, default=str), encoding="utf-8")
    logger.info(f"Analytics report: {package_dir / REPORT_FILE}")
    return report


def _eligible_from_package(package_dir):
    """Eligible voters per district code, read back from the exported election layer."""
    values = json.loads((package_dir / "data_layers" / "valdistrikt_election" / "values.json").read_text(encoding="utf-8"))
    column = next(c for c in values["columns"] if c["variableId"] == "eligible_voters")
    codes = [u.split(":", 1)[1] for u in values["unitIds"]]
    return pd.Series([v if ok else None for v, ok in zip(column["integerValues"], column["valid"])], index=codes, dtype=float)
