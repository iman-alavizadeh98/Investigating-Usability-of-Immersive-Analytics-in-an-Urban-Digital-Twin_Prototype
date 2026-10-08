"""
Writers for the Unity package's spatial layers, data layers and associations.

Formats (read by the Unity runtime, `UrbanAnalytics`):

spatial_layers/<id>/layer.json      SpatialLayerDefinitionDto: id, displayName,
                                    description, geometryType, geometryMode,
                                    geometrySource, unitCount, visibleByDefault, ...
spatial_layers/<id>/geometry.json   SpatialGeometryFileDto: units with id
                                    ("<layer>:<sourceId>"), displayName, centroid
                                    and geometry (all units MultiPolygon here).
data_layers/<id>/layer.json         DataLayerDefinition: variables (id, displayName,
                                    description, valueType, unit), target layer.
data_layers/<id>/values.json        DataLayerFileDto: unitIds + one column per
                                    variable (typed values + `valid` mask).
associations/<id>/layer.json        source/target layers + provenance.
associations/<id>/pairs.json        sourceIds[] / targetIds[] (same length).

Coordinates stay in the source CRS (EPSG:3006, double precision); the runtime
converts them through SpatialReferenceManager.
"""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Sequence

import geopandas as gpd
import numpy as np
import pandas as pd
from shapely.geometry import MultiPolygon, Polygon
from shapely.geometry.polygon import orient

logger = logging.getLogger(__name__)

SCHEMA_VERSION = "1.0"


def _json(path: Path, obj, pretty: bool = True) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(obj, ensure_ascii=False, indent=2) if pretty else json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
    path.write_text(text, encoding="utf-8")


def _coord(x: float, y: float) -> Dict:
    return {"easting": round(float(x), 3), "northing": round(float(y), 3), "elevation": 0.0}


def _ring(coords) -> Dict:
    pts = list(coords)
    if len(pts) > 1 and pts[0] == pts[-1]:
        pts = pts[:-1]  # the runtime expects open rings
    return {"coordinates": [_coord(x, y) for x, y, *_ in pts]}


def _polygon(poly: Polygon) -> Dict:
    poly = orient(poly, sign=1.0)  # exterior CCW, holes CW
    return {"exterior": _ring(poly.exterior.coords), "holes": [_ring(h.coords) for h in poly.interiors]}


def as_multipolygon(geom) -> MultiPolygon:
    if isinstance(geom, Polygon):
        return MultiPolygon([geom])
    if isinstance(geom, MultiPolygon):
        return geom
    polys = [g for g in getattr(geom, "geoms", []) if isinstance(g, Polygon) and not g.is_empty]
    if not polys:
        raise ValueError(f"Not a polygonal geometry: {geom.geom_type}")
    return MultiPolygon(polys)


@dataclass
class SpatialLayerSpec:
    layer_id: str
    display_name: str
    description: str
    visible_by_default: bool = False
    provenance: Dict = field(default_factory=dict)


def write_spatial_layer(package_dir: Path, spec: SpatialLayerSpec, units: gpd.GeoDataFrame) -> Dict:
    """
    `units` needs columns `unit_id` (full id "<layer>:<source>"), `display_name`
    and a polygonal geometry. Rows without geometry are skipped (and reported).
    """
    out = package_dir / "spatial_layers" / spec.layer_id
    missing = units.geometry.isna() | units.geometry.is_empty
    if missing.any():
        logger.warning(f"  {spec.layer_id}: {int(missing.sum())} units without geometry are not written: "
                       f"{units.loc[missing, 'unit_id'].tolist()}")
    units = units[~missing]
    if units["unit_id"].duplicated().any():
        raise ValueError(f"{spec.layer_id}: repeated unit ids")
    records = []
    for uid, name, geom in zip(units["unit_id"], units["display_name"], units.geometry):
        mp = as_multipolygon(geom)
        c = geom.representative_point()  # inside concave / holed shapes, unlike the centroid
        records.append({
            "id": uid, "displayName": str(name), "parentUnitId": None,
            "centroid": _coord(c.x, c.y),
            "geometry": {"multiPolygon": {"polygons": [_polygon(p) for p in mp.geoms]}},
        })
    _json(out / "geometry.json", {
        "schemaVersion": SCHEMA_VERSION, "spatialLayerId": spec.layer_id,
        "geometryType": "MultiPolygon", "unitCount": len(records), "units": records,
    }, pretty=False)
    _json(out / "layer.json", {
        "id": spec.layer_id, "displayName": spec.display_name, "description": spec.description,
        "geometryType": "MultiPolygon", "geometryMode": "Procedural", "geometrySource": "geometry.json",
        "unitCount": len(records), "visibleByDefault": spec.visible_by_default,
        "selectable": True, "extractable": True,
        "provenance": {**spec.provenance, "exportedAt": datetime.now(timezone.utc).isoformat()},
    })
    logger.info(f"  spatial layer {spec.layer_id}: {len(records)} units")
    return {"units": len(records), "skipped_without_geometry": int(missing.sum())}


@dataclass
class Variable:
    """One data-layer variable. `values` is aligned with the layer's unit ids; NaN/None = no value."""
    id: str
    display_name: str
    values: Sequence
    value_type: str = "Float"     # Float | Integer | Boolean | String
    unit: str = ""
    description: str = ""
    method: str = "observed"      # observed | derived | dasymetric_estimate | ... (see the integration report)


def _column(var: Variable) -> Dict:
    s = pd.Series(list(var.values), dtype="object")
    valid = s.notna() & s.map(lambda v: not (isinstance(v, float) and (np.isnan(v) or np.isinf(v))))
    col: Dict = {"variableId": var.id}
    if var.value_type == "Float":
        col["floatValues"] = [round(float(v), 6) if ok else 0.0 for v, ok in zip(s, valid)]
    elif var.value_type == "Integer":
        col["integerValues"] = [int(round(float(v))) if ok else 0 for v, ok in zip(s, valid)]
    elif var.value_type == "Boolean":
        col["booleanValues"] = [bool(v) if ok else False for v, ok in zip(s, valid)]
    elif var.value_type == "String":
        col["stringValues"] = [str(v) if ok else "" for v, ok in zip(s, valid)]
    else:
        raise ValueError(f"Unknown value type {var.value_type}")
    col["valid"] = [bool(v) for v in valid]
    return col


def write_data_layer(
    package_dir: Path,
    layer_id: str,
    display_name: str,
    description: str,
    target_layer_id: str,
    unit_ids: Sequence[str],
    variables: List[Variable],
    provenance: Optional[Dict] = None,
) -> Dict:
    out = package_dir / "data_layers" / layer_id
    unit_ids = list(unit_ids)
    if len(set(unit_ids)) != len(unit_ids):
        raise ValueError(f"{layer_id}: repeated unit ids")
    for v in variables:
        if len(v.values) != len(unit_ids):
            raise ValueError(f"{layer_id}.{v.id}: {len(v.values)} values for {len(unit_ids)} units")
    columns = [_column(v) for v in variables]
    _json(out / "values.json", {
        "schemaVersion": SCHEMA_VERSION, "dataLayerId": layer_id, "targetSpatialLayerId": target_layer_id,
        "unitIds": unit_ids, "columns": columns,
    }, pretty=False)
    _json(out / "layer.json", {
        "schemaVersion": SCHEMA_VERSION, "id": layer_id, "displayName": display_name, "description": description,
        "targetSpatialLayerId": target_layer_id, "dataFile": "values.json", "temporalMode": "Static",
        "variables": [
            {"id": v.id, "displayName": v.display_name,
             "description": f"{v.description} [method: {v.method}]".strip(),
             "valueType": v.value_type, "unit": v.unit}
            for v in variables
        ],
        "provenance": {**(provenance or {}), "exportedAt": datetime.now(timezone.utc).isoformat()},
    })
    valid_counts = {c["variableId"]: int(sum(c["valid"])) for c in columns}
    logger.info(f"  data layer {layer_id}: {len(unit_ids)} units x {len(variables)} variables")
    return {"units": len(unit_ids), "variables": len(variables), "valid_values": valid_counts}


def write_association(
    package_dir: Path,
    association_id: str,
    source_layer_id: str,
    target_layer_id: str,
    source_ids: Sequence[str],
    target_ids: Sequence[str],
    description: str,
    provenance: Optional[Dict] = None,
) -> Dict:
    if len(source_ids) != len(target_ids):
        raise ValueError(f"{association_id}: source and target lists differ in length")
    if len(set(source_ids)) != len(source_ids):
        raise ValueError(f"{association_id}: a source maps to more than one target")
    out = package_dir / "associations" / association_id
    _json(out / "pairs.json", {
        "schemaVersion": SCHEMA_VERSION, "associationId": association_id,
        "sourceIds": list(source_ids), "targetIds": list(target_ids),
    }, pretty=False)
    _json(out / "layer.json", {
        "schemaVersion": SCHEMA_VERSION, "id": association_id, "description": description,
        "sourceLayerId": source_layer_id, "targetLayerId": target_layer_id,
        "pairsFile": "pairs.json", "pairCount": len(source_ids),
        "provenance": {**(provenance or {}), "exportedAt": datetime.now(timezone.utc).isoformat()},
    })
    logger.info(f"  association {association_id}: {len(source_ids):,} pairs")
    return {"pairs": len(source_ids)}
