"""
project_manifest.json for a city package, written from the city config and
the files that exist in the package.

All "definition" paths are relative to the manifest's own folder, so a package
can sit anywhere inside StreamingAssets (one folder per city).

Resources are found on disk, not remembered: running only one exporter later
keeps the entries of everything else already in the package, and nothing is
listed that was not exported.
"""

import json
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List

from .config import CityConfig

logger = logging.getLogger(__name__)

PACKAGE_VERSION = "1.0"
MANIFEST_NAME = "project_manifest.json"

# kind in the manifest -> folder pattern of its layer.json files in the package
RESOURCE_KINDS = {
    "urbanContext": "urban_context/*/layer.json",
    "spatialLayers": "spatial_layers/*/layer.json",
    "dataLayers": "data_layers/*/layer.json",
    "associations": "associations/*/layer.json",
}


def discover_resources(package_dir: Path) -> Dict[str, List[Dict]]:
    """Every layer.json in the package, by kind, sorted by id."""
    found = {}
    for kind, pattern in RESOURCE_KINDS.items():
        entries = []
        for layer_json in sorted(package_dir.glob(pattern)):
            definition = json.loads(layer_json.read_text(encoding="utf-8"))
            layer_id = definition.get("id") or layer_json.parent.name
            entries.append({"id": layer_id, "definition": layer_json.relative_to(package_dir).as_posix()})
        ids = [e["id"] for e in entries]
        if len(ids) != len(set(ids)):
            raise ValueError(f"Duplicate {kind} ids in {package_dir}: {ids}")
        found[kind] = entries
    return found


def write_manifest(city: CityConfig) -> Path:
    package_dir = city.package_dir
    package_dir.mkdir(parents=True, exist_ok=True)
    resources = discover_resources(package_dir)
    manifest = {
        "packageVersion": PACKAGE_VERSION,
        "projectId": city.city_id,
        "displayName": city.display_name,
        "spatialReference": {"sourceCRS": city.crs, "sourceUnit": "meter"},
        "unityTransform": {
            "originInSourceCRS": {
                "easting": city.origin_easting,
                "northing": city.origin_northing,
                "elevation": city.origin_elevation,
            },
            "metersToUnity": city.meters_to_unity,
            "axisMapping": {"easting": "X", "northing": "Z", "elevation": "Y"},
        },
        "urbanContext": resources["urbanContext"],
        "spatialLayers": resources["spatialLayers"],
        "dataLayers": resources["dataLayers"],
        "associations": resources["associations"],
        "generatedBy": "Src/Scripts/Unity/build_unity_package.py",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "cityConfig": str(city.config_path) if city.config_path else None,
    }
    path = package_dir / MANIFEST_NAME
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    counts = {k: len(v) for k, v in resources.items()}
    logger.info(f"Manifest: {path} {counts}")
    return path
