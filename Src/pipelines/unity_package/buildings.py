"""
Building export for the Unity runtime package.

Input: the buildings GeoPackage after the height pipeline
(buildings_lidar_added.gpkg): one row per object_id, all Byggnad attributes
(Swedish + English) and the height columns.

Output folder (urban_context/buildings/):

layer.json       definition: files, counts, origin, field list (label, group,
                 type, unit, description, source column), provenance.
geometry.bin     GBLD version 3, little-endian:
                   char[4] "GBLD", uint32 version = 3, uint32 buildingCount,
                   double originEasting, double originNorthing
                   per building:
                     uint16 idByteLength, byte[] id (UTF-8, "building:<object_id>")
                     float32 heightMeters  (0 when there is no height)
                     float32 groundZ       (metres above sea level; NaN = unknown)
                     uint8   flags         (bit 0: has height)
                     uint16  polygonCount
                     per polygon:
                       uint16 ringCount    (exterior first, then holes)
                       per ring:
                         uint32 vertexCount
                         vertexCount x (float32 x, float32 z)
                           x = easting - originEasting, z = northing - originNorthing
                   Rings are not closed (no repeated first vertex). Exterior
                   rings are counter-clockwise, holes clockwise (seen from
                   above, easting right / northing up).
attributes.json  same layout as a data-layer values file (DataLayerFileDto):
                   unitIds = building ids in geometry.bin order, one column per
                   field with typed values and a `valid` mask (false = no value).

Buildings are written sorted by id, so repeated exports are identical.
"""

import hashlib
import json
import logging
import math
import struct
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import geopandas as gpd
import numpy as np
import pandas as pd
from shapely.geometry import MultiPolygon, Polygon
from shapely.geometry.polygon import orient

from .config import MAX_DISTANCE_FROM_ORIGIN_M, CityConfig

logger = logging.getLogger(__name__)

MAGIC = b"GBLD"
VERSION = 3
ID_PREFIX = "building:"
CONTEXT_ID = "buildings"
OUTPUT_SUBDIR = Path("urban_context") / "buildings"
FLAG_HAS_HEIGHT = 1


# =============================================================================
# Fields shown in Unity
# =============================================================================

@dataclass(frozen=True)
class BuildingField:
    """One attribute in attributes.json and in the Unity info panel."""
    id: str
    column: str
    displayName: str
    group: str
    valueType: str            # Float | Integer | Boolean | String
    unit: str = ""
    description: str = ""
    required: bool = False    # missing column -> error; otherwise the field is skipped
    only_when: str = ""       # boolean column; the value counts as missing where it is false
                              # ("not <column>": missing where it is true)


# Order = display order in Unity; groups appear in order of first use.
# Swedish originals sit next to the English values (CLAUDE.md principle 6).
BUILDING_FIELDS: List[BuildingField] = [
    # Identity
    BuildingField("name", "building_name_primary", "Name", "Identity", "String",
                  description="Building name (byggnadsnamn1). Most buildings have none."),
    BuildingField("name_secondary", "building_name_secondary", "Other name", "Identity", "String",
                  description="Second building name (byggnadsnamn2)."),
    BuildingField("house_number", "house_number", "Building no. in property", "Identity", "Integer",
                  description="Husnummer: number of the building within its property. Not a street address."),
    BuildingField("main_building", "main_building_flag", "Main building", "Identity", "Boolean",
                  description="Huvudbyggnad: marks the main building of a complex; 'No' does not mean secondary."),
    BuildingField("lm_object_id", "object_id", "Lantmäteriet ID", "Identity", "String", required=True,
                  description="objektidentitet (UUID)."),

    # Type and use
    BuildingField("type", "object_type_en", "Type", "Type and use", "String", required=True),
    BuildingField("type_sv", "object_type", "Type (Swedish)", "Type and use", "String", required=True,
                  description="objekttyp, original value."),
    BuildingField("category", "object_type_category", "Category", "Type and use", "String"),
    BuildingField("purpose", "primary_purpose_en", "Purpose", "Type and use", "String"),
    BuildingField("purpose_sv", "primary_purpose", "Purpose (Swedish)", "Type and use", "String",
                  description="andamal1, original value '<objekttyp>;<ändamål>'."),
    BuildingField("purpose_category", "primary_purpose_category", "Purpose category", "Type and use", "String"),
    BuildingField("purpose_2", "secondary_purpose_en", "Second purpose", "Type and use", "String"),
    BuildingField("purpose_2_sv", "secondary_purpose", "Second purpose (Swedish)", "Type and use", "String"),
    BuildingField("purpose_3", "tertiary_purpose_en", "Third purpose", "Type and use", "String"),
    BuildingField("purpose_3_sv", "tertiary_purpose", "Third purpose (Swedish)", "Type and use", "String"),
    BuildingField("purpose_4", "quaternary_purpose_en", "Fourth purpose", "Type and use", "String"),
    BuildingField("purpose_4_sv", "quaternary_purpose", "Fourth purpose (Swedish)", "Type and use", "String"),
    BuildingField("purpose_5", "quinary_purpose_en", "Fifth purpose", "Type and use", "String"),
    BuildingField("purpose_5_sv", "quinary_purpose", "Fifth purpose (Swedish)", "Type and use", "String"),

    # Size and height
    BuildingField("height", "height_m", "Height", "Size and height", "Float", "m", required=True,
                  description="95th percentile of roof height above ground.", only_when="has_height"),
    BuildingField("has_height", "has_height", "Height measured", "Size and height", "Boolean", required=True),
    BuildingField("no_height_reason", "no_height_reason_code", "No height because", "Size and height", "String",
                  description="surface_shows_ground = not in the surface model (built later or very low); "
                              "no_points / below_min_height / no_ground / no_tile = not enough data.",
                  only_when="not has_height"),
    BuildingField("footprint_area", "footprint_area_m2", "Footprint area", "Size and height", "Float", "m²"),
    BuildingField("ground_elevation", "ground_z", "Ground elevation", "Size and height", "Float", "m",
                  description="Median ground along the outline, metres above sea level (RH 2000)."),
    BuildingField("roof_elevation", "roof_z", "Roof elevation", "Size and height", "Float", "m",
                  description="ground_elevation + height, metres above sea level (RH 2000)."),

    # Height source (data quality)
    BuildingField("height_source", "height_source", "Height source", "Height source", "String",
                  description="surface = surface model; lidar = laser data (used where the surface has a hole); none."),
    BuildingField("height_quality", "height_quality", "Height quality", "Height source", "String",
                  description="high = both sources agree; medium = surface only; low = few points or laser only."),
    BuildingField("height_surface", "height_surface_m", "Height (surface model)", "Height source", "Float", "m"),
    BuildingField("height_lidar", "height_lidar_m", "Height (laser data)", "Height source", "Float", "m"),
    BuildingField("height_changed", "height_change_flag", "Sources disagree", "Height source", "Boolean",
                  description="Surface and laser heights differ by more than 3 m (changed building, trees, cranes)."),

    # Footprint survey (data quality)
    BuildingField("collection_level", "collection_level_en", "Surveyed at", "Footprint survey", "String",
                  description="Facade or roof edge; roof-edge footprints include the overhang."),
    BuildingField("collection_level_sv", "collection_level", "Surveyed at (Swedish)", "Footprint survey", "String"),
    BuildingField("collection_level_mixed", "collection_level_mixed", "Mixed survey levels", "Footprint survey", "Boolean"),
    BuildingField("position_uncertainty_plan", "position_uncertainty_plan_m", "Position uncertainty (plan)",
                  "Footprint survey", "Float", "m"),
    BuildingField("position_uncertainty_height", "position_uncertainty_height_m", "Position uncertainty (height)",
                  "Footprint survey", "Float", "m"),
    BuildingField("merged_pieces", "source_part_count", "Merged pieces", "Footprint survey", "Integer",
                  description="Number of source rows merged into this building (1 = not merged)."),
    BuildingField("version_valid_from", "version_valid_from", "Record version from", "Footprint survey", "String",
                  description="versiongiltigfran: date of this record version, not a construction date."),
    BuildingField("original_organisation", "original_organisation", "Recorded by", "Footprint survey", "String"),
]


# =============================================================================
# Geometry
# =============================================================================

def _polygons(geom) -> List[Polygon]:
    if isinstance(geom, Polygon):
        return [geom]
    if isinstance(geom, MultiPolygon):
        return list(geom.geoms)
    raise ValueError(f"Unsupported geometry type {geom.geom_type}")


def _ring_vertices(coords, origin: Tuple[float, float]) -> Optional[np.ndarray]:
    """Ring as float32 (x, z) relative to the origin, without closing or repeated vertices."""
    a = np.asarray(coords, dtype=np.float64)[:, :2]
    if len(a) > 1 and np.array_equal(a[0], a[-1]):
        a = a[:-1]
    rel = (a - np.asarray(origin)).astype(np.float32)
    # Vertices that coincide after float32 rounding would give zero-length walls.
    keep = np.ones(len(rel), dtype=bool)
    keep[1:] = np.any(rel[1:] != rel[:-1], axis=1)
    rel = rel[keep]
    if len(rel) > 1 and np.array_equal(rel[0], rel[-1]):
        rel = rel[:-1]
    return rel if len(rel) >= 3 else None


def encode_building(building_id: str, height: float, ground_z: float, has_height: bool,
                    geom, origin: Tuple[float, float]) -> Tuple[bytes, Dict]:
    """One GBLD v3 record and counters for the report."""
    stats = {"polygons": 0, "holes": 0, "dropped_rings": 0, "vertices": 0}
    polygons = []
    for poly in _polygons(geom):
        poly = orient(poly, sign=1.0)  # exterior counter-clockwise, holes clockwise
        exterior = _ring_vertices(poly.exterior.coords, origin)
        if exterior is None:
            stats["dropped_rings"] += 1 + len(poly.interiors)
            continue
        rings = [exterior]
        for interior in poly.interiors:
            hole = _ring_vertices(interior.coords, origin)
            if hole is None:
                stats["dropped_rings"] += 1
            else:
                rings.append(hole)
        polygons.append(rings)
    if not polygons:
        raise ValueError(f"{building_id}: no polygon with at least 3 distinct vertices")

    id_bytes = building_id.encode("utf-8")
    parts = [
        struct.pack("<H", len(id_bytes)), id_bytes,
        struct.pack("<ffBH", float(height), float(ground_z), FLAG_HAS_HEIGHT if has_height else 0, len(polygons)),
    ]
    for rings in polygons:
        parts.append(struct.pack("<H", len(rings)))
        for ring in rings:
            parts.append(struct.pack("<I", len(ring)))
            parts.append(np.ascontiguousarray(ring, dtype="<f4").tobytes())
            stats["vertices"] += len(ring)
        stats["polygons"] += 1
        stats["holes"] += len(rings) - 1
    return b"".join(parts), stats


def read_geometry_bin(path: Path) -> Dict:
    """Reader for GBLD v3 (used by tests and the export's own check)."""
    data = Path(path).read_bytes()
    if data[:4] != MAGIC:
        raise ValueError("Not a GBLD file")
    version, count = struct.unpack_from("<II", data, 4)
    if version != VERSION:
        raise ValueError(f"Expected GBLD version {VERSION}, got {version}")
    origin_e, origin_n = struct.unpack_from("<dd", data, 12)
    pos = 28
    buildings = []
    for _ in range(count):
        (n,) = struct.unpack_from("<H", data, pos)
        pos += 2
        bid = data[pos:pos + n].decode("utf-8")
        pos += n
        height, ground, flags, n_polys = struct.unpack_from("<ffBH", data, pos)
        pos += 11
        polys = []
        for _ in range(n_polys):
            (n_rings,) = struct.unpack_from("<H", data, pos)
            pos += 2
            rings = []
            for _ in range(n_rings):
                (n_vert,) = struct.unpack_from("<I", data, pos)
                pos += 4
                ring = np.frombuffer(data, dtype="<f4", count=2 * n_vert, offset=pos).reshape(n_vert, 2)
                pos += 8 * n_vert
                rings.append(ring)
            polys.append(rings)
        buildings.append({"id": bid, "height": height, "ground_z": ground,
                          "has_height": bool(flags & FLAG_HAS_HEIGHT), "polygons": polys})
    if pos != len(data):
        raise ValueError(f"{len(data) - pos} trailing bytes in {path}")
    return {"origin": (origin_e, origin_n), "buildings": buildings}


# =============================================================================
# Attributes
# =============================================================================

def _column(series: pd.Series, value_type: str) -> Dict:
    """One DataColumnDto-style column with a validity mask."""
    valid = series.notna().to_numpy()
    col: Dict = {}
    if value_type == "Float":
        values = pd.to_numeric(series, errors="raise").astype("float64")
        col["floatValues"] = [round(float(v), 4) if ok else 0.0 for v, ok in zip(values, valid)]
    elif value_type == "Integer":
        values = pd.to_numeric(series, errors="raise")
        bad = valid & (values.fillna(0) % 1 != 0).to_numpy()
        if bad.any():
            raise ValueError(f"Non-integer values in an Integer field: {series[bad].head().tolist()}")
        col["integerValues"] = [int(v) if ok else 0 for v, ok in zip(values, valid)]
    elif value_type == "Boolean":
        col["booleanValues"] = [bool(v) if ok else False for v, ok in zip(series, valid)]
    elif value_type == "String":
        def text(v):
            if isinstance(v, (pd.Timestamp, datetime)):
                return v.date().isoformat()  # dates are shown without time
            return str(v).strip()
        col["stringValues"] = [text(v) if ok else "" for v, ok in zip(series, valid)]
        valid = valid & np.array([s != "" for s in col["stringValues"]])
    else:
        raise ValueError(f"Unknown valueType {value_type}")
    col["valid"] = [bool(v) for v in valid]
    return col


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# =============================================================================
# Export
# =============================================================================

def export_buildings(city: CityConfig, fields: List[BuildingField] = BUILDING_FIELDS) -> Dict:
    """Write urban_context/buildings/ for the city; returns the manifest entry and a report."""
    src = city.buildings
    if src is None:
        raise ValueError("City config has no 'buildings' section")
    if not src.source.exists():
        raise FileNotFoundError(f"Buildings file not found: {src.source}")

    logger.info(f"Reading buildings: {src.source}")
    gdf = gpd.read_file(src.source, layer=src.layer)
    if gdf.crs is None or gdf.crs.to_string().upper() != city.crs.upper():
        raise ValueError(f"CRS is {gdf.crs}, the city config says {city.crs}; this exporter does not reproject")
    if "object_id" not in gdf.columns or gdf["object_id"].isna().any():
        raise ValueError("Every building needs an object_id")
    if gdf["object_id"].duplicated().any():
        raise ValueError("object_id is not unique; use the buildings postprocess output")
    if gdf.geometry.isna().any() or gdf.geometry.is_empty.any():
        raise ValueError("Null or empty geometries")
    invalid = int((~gdf.geometry.is_valid).sum())
    if invalid:
        raise ValueError(f"{invalid} invalid geometries; repair them in preprocessing, not in the export")

    origin = (city.origin_easting, city.origin_northing)
    minx, miny, maxx, maxy = gdf.total_bounds
    far = max(abs(minx - origin[0]), abs(maxx - origin[0]), abs(miny - origin[1]), abs(maxy - origin[1]))
    if far > MAX_DISTANCE_FROM_ORIGIN_M:
        raise ValueError(
            f"Buildings reach {far:,.0f} m from the origin {origin}; float32 coordinates need "
            f"< {MAX_DISTANCE_FROM_ORIGIN_M:,.0f} m. Choose an origin inside the city's extent."
        )

    gdf = gdf.sort_values("object_id").reset_index(drop=True)
    unit_ids = [f"{ID_PREFIX}{oid}" for oid in gdf["object_id"].astype(str)]

    # Fields: required ones must exist; optional ones that are missing or empty are skipped.
    used_fields, columns, skipped = [], [], {}
    for f in fields:
        if f.column not in gdf.columns:
            if f.required:
                raise ValueError(f"Required column '{f.column}' (field '{f.id}') is missing")
            skipped[f.id] = "column missing"
            continue
        if gdf[f.column].isna().all():
            skipped[f.id] = "no values"
            continue
        col = _column(gdf[f.column], f.valueType)
        if f.only_when:
            negate = f.only_when.startswith("not ")
            cond_col = f.only_when[4:].strip() if negate else f.only_when
            if cond_col not in gdf.columns:
                raise ValueError(f"Field '{f.id}': only_when column '{cond_col}' is missing")
            condition = gdf[cond_col].fillna(False).astype(bool).to_numpy()
            if negate:
                condition = ~condition
            col["valid"] = [bool(v and c) for v, c in zip(col["valid"], condition)]
        if not any(col["valid"]):
            skipped[f.id] = "no values"
            continue
        columns.append({"variableId": f.id, **col})
        used_fields.append(f)
    for fid, why in skipped.items():
        logger.info(f"  field '{fid}' skipped: {why}")

    out_dir = city.package_dir / OUTPUT_SUBDIR
    out_dir.mkdir(parents=True, exist_ok=True)

    # geometry.bin
    totals = {"polygons": 0, "holes": 0, "dropped_rings": 0, "vertices": 0}
    records = []
    heights = gdf["height_m"].fillna(0.0).astype(float).to_numpy()
    has_height = gdf["has_height"].fillna(False).astype(bool).to_numpy()
    ground = (pd.to_numeric(gdf["ground_z"], errors="coerce").to_numpy()
              if "ground_z" in gdf.columns else np.full(len(gdf), np.nan))
    for i, (bid, geom) in enumerate(zip(unit_ids, gdf.geometry)):
        rec, stats = encode_building(bid, heights[i], ground[i], has_height[i], geom, origin)
        records.append(rec)
        for k in totals:
            totals[k] += stats[k]
    geometry_path = out_dir / "geometry.bin"
    with open(geometry_path, "wb") as f:
        f.write(MAGIC + struct.pack("<IIdd", VERSION, len(records), origin[0], origin[1]))
        for rec in records:
            f.write(rec)

    # attributes.json (DataLayerFileDto layout)
    attributes_path = out_dir / "attributes.json"
    attributes = {
        "schemaVersion": "1.0",
        "dataLayerId": CONTEXT_ID,
        "targetSpatialLayerId": CONTEXT_ID,
        "unitIds": unit_ids,
        "columns": columns,
    }
    attributes_path.write_text(json.dumps(attributes, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")

    # Check: read the binary back and compare with the attributes.
    check = read_geometry_bin(geometry_path)
    if [b["id"] for b in check["buildings"]] != unit_ids:
        raise RuntimeError("geometry.bin ids do not match attributes.json")

    # Provenance: the height run's summary sits next to its GeoPackage.
    height_summary = None
    summary_path = src.source.parent / "lidar_heights_summary.json"
    if summary_path.exists():
        s = json.loads(summary_path.read_text(encoding="utf-8"))
        height_summary = {k: s.get(k) for k in (
            "run_id", "run_timestamp", "sources", "buildings_with_height",
            "buildings_without_height", "without_height_by_reason", "method")}

    n_with = int(has_height.sum())
    layer = {
        "schemaVersion": "1.0",
        "id": CONTEXT_ID,
        "kind": "Buildings",
        "displayName": "Buildings",
        "description": f"Building footprints of {city.display_name}, extruded to one height each (flat roofs).",
        "geometryFile": "geometry.bin",
        "geometryFormat": f"GBLD v{VERSION}",
        "attributesFile": "attributes.json",
        "buildingCount": len(unit_ids),
        "buildingsWithHeight": n_with,
        "buildingsWithoutHeight": len(unit_ids) - n_with,
        "idPrefix": ID_PREFIX,
        "sourceCRS": city.crs,
        "coordinateOrigin": {"easting": origin[0], "northing": origin[1]},
        "extent": {"minEasting": float(minx), "minNorthing": float(miny),
                   "maxEasting": float(maxx), "maxNorthing": float(maxy)},
        "groundElevationDatum": "RH 2000 (metres above sea level)",
        "fields": [{k: v for k, v in asdict(f).items() if k not in ("required", "only_when")} for f in used_fields],
        "skippedFields": skipped,
        "geometryStats": totals,
        "provenance": {
            "source": str(src.source),
            "sourceLayer": src.layer,
            "sourceModified": datetime.fromtimestamp(src.source.stat().st_mtime, timezone.utc).isoformat(),
            "heights": height_summary,
            "exportedAt": datetime.now(timezone.utc).isoformat(),
            "cityConfig": str(city.config_path) if city.config_path else None,
        },
        "checksums": {"geometry.bin": _sha256(geometry_path), "attributes.json": _sha256(attributes_path)},
    }
    layer_path = out_dir / "layer.json"
    layer_path.write_text(json.dumps(layer, ensure_ascii=False, indent=2), encoding="utf-8")

    report = {
        "buildings": len(unit_ids),
        "with_height": n_with,
        "fields": len(used_fields),
        "skipped_fields": skipped,
        "geometry": totals,
        "geometry_bin_mb": round(geometry_path.stat().st_size / 1e6, 2),
        "attributes_json_mb": round(attributes_path.stat().st_size / 1e6, 2),
        "output": str(out_dir),
    }
    logger.info(
        f"Buildings: {report['buildings']:,} ({n_with:,} with height), {report['fields']} fields, "
        f"{totals['polygons']:,} polygons, {totals['holes']:,} holes, {totals['vertices']:,} vertices; "
        f"geometry.bin {report['geometry_bin_mb']} MB, attributes.json {report['attributes_json_mb']} MB"
    )
    if totals["dropped_rings"]:
        logger.warning(f"  {totals['dropped_rings']} rings with < 3 distinct vertices were dropped")

    entry = {"id": CONTEXT_ID, "definition": (OUTPUT_SUBDIR / "layer.json").as_posix()}
    return {"manifest_entry": entry, "report": report}
