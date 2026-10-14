"""
Load everything the data report shows, for one city config.

Read-only. Nothing here changes data; derived numbers are taken from where the
project already computes them, so the report shows what the twin shows:

    buildings        processed buildings + heights (city config `buildings`)
    SCB layers       values and geometry from the city's Unity package
                     (population, income, DeSO, grid; built by build_unity_package.py)
    employment       raw SCB delivery via pipelines.scb (not in the package yet)
    election         municipal totals from pipelines.election; district values
                     and estimates from the Unity package
    LiDAR / DSM      tile sidecars (*_tile.json, *_strip.json, y*_18.json)
    base maps        Lantmäteriet Vägkartan and Fastighetskartan shapefiles

Paths that the package builder does not need are in the config section
`dataReport` (employment, lidarTiles, surfaceModel, roadMap, propertyMap,
output). Nothing here names a city.
"""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional

import geopandas as gpd
import numpy as np
import pandas as pd
from shapely.geometry import MultiPolygon, Polygon, box, shape

from pipelines.election.config import DISTRICT_CODE_COLUMN, INVALID_PREFIX, SUMMARY_CATEGORIES
from pipelines.election.loader import load_election
from pipelines.election.validator import is_collection, long_by_district
from pipelines.scb.loader import SourceTable, load_scb_tables
from pipelines.unity_package.config import CityConfig, load_city_config
from pipelines.unity_package.statistics import build_ruta_units

logger = logging.getLogger(__name__)

BUILDING_COLUMNS = [
    "object_id", "object_type", "object_type_en", "object_type_category", "primary_purpose_en",
    "footprint_area_m2", "collection_level_en", "height_m", "has_height", "height_source",
    "height_quality", "no_height_reason", "ground_z", "height_change_flag", "version_valid_from",
]


@dataclass
class ReportData:
    cfg: CityConfig
    report_cfg: Dict
    repo_root: Path
    buildings: gpd.GeoDataFrame
    height_summary: Dict
    lidar_tiles: gpd.GeoDataFrame
    lidar_strips: pd.DataFrame
    dsm_tiles: gpd.GeoDataFrame
    package: Dict[str, gpd.GeoDataFrame]          # spatial layer id -> units
    values: Dict[str, pd.DataFrame]               # data layer id -> values by unit id
    population_tables: Dict[str, SourceTable]
    income_tables: Dict[str, SourceTable]
    employment_tables: Dict[str, SourceTable]
    employment_cells: gpd.GeoDataFrame            # one row per employment grid key, with Tab7/8/9/10 values
    employment_deso: gpd.GeoDataFrame
    election: Dict
    roadmap: Dict[str, gpd.GeoDataFrame]
    propertymap: Dict[str, gpd.GeoDataFrame]
    deliveries: Dict[str, Dict]
    extents: Dict[str, tuple] = field(default_factory=dict)


# ---------------------------------------------------------------------------
# Unity package readers
# ---------------------------------------------------------------------------

def _ring(coords) -> List[tuple]:
    return [(c["easting"], c["northing"]) for c in coords]


def read_spatial_layer(package_dir: Path, layer_id: str) -> gpd.GeoDataFrame:
    """Package geometry.json -> GeoDataFrame (unit_id, display_name, geometry)."""
    data = json.loads((package_dir / "spatial_layers" / layer_id / "geometry.json").read_text(encoding="utf-8"))
    rows = []
    for unit in data["units"]:
        polys = []
        for p in (unit.get("geometry") or {}).get("multiPolygon", {}).get("polygons", []):
            holes = [_ring(h["coordinates"]) for h in p.get("holes", [])]
            polys.append(Polygon(_ring(p["exterior"]["coordinates"]), holes))
        geom = MultiPolygon(polys) if polys else None
        rows.append({"unit_id": unit["id"], "display_name": unit.get("displayName"), "geometry": geom})
    return gpd.GeoDataFrame(rows, geometry="geometry", crs="EPSG:3006").set_index("unit_id")


def read_data_layer(package_dir: Path, layer_id: str) -> pd.DataFrame:
    """Package values.json -> DataFrame by unit id; invalid entries are NaN/None."""
    data = json.loads((package_dir / "data_layers" / layer_id / "values.json").read_text(encoding="utf-8"))
    out = {}
    for col in data["columns"]:
        key = next(k for k in col if k.endswith("Values"))
        s = pd.Series(col[key], index=data["unitIds"], dtype="object" if key == "stringValues" else "float64")
        s[~pd.Series(col["valid"], index=data["unitIds"])] = np.nan
        out[col["variableId"]] = s
    return pd.DataFrame(out)


# ---------------------------------------------------------------------------
# Raw sources
# ---------------------------------------------------------------------------

def read_delivery_note(folder: Path) -> Dict:
    """SLU GET `_readMe*` note of a delivery folder (JSON or text), best effort."""
    notes = sorted((folder / "metadata").glob("_readMe*")) if (folder / "metadata").is_dir() else []
    if not notes:
        return {}
    raw = notes[0].read_bytes()
    text = next((raw.decode(enc) for enc in ("utf-8", "cp1252") if _decodes(raw, enc)), raw.decode("latin-1"))
    try:
        d = json.loads(text)
        return {"name": d.get("get_ui_name"), "provider": d.get("data_provider"),
                "delivered": d.get("delivered_to_get"), "license": d.get("license")}
    except json.JSONDecodeError:
        # Text note: "License:" followed by the value on the same or a later line.
        lines = [line.strip() for line in text.splitlines()]
        lic = None
        for i, line in enumerate(lines):
            if line.lower().startswith("license"):
                lic = line.split(":", 1)[1].strip() or next((n for n in lines[i + 1:] if n), None)
                break
        return {"name": None, "provider": None, "delivered": None, "license": lic}


def _decodes(raw: bytes, enc: str) -> bool:
    try:
        raw.decode(enc)
        return True
    except UnicodeDecodeError:
        return False


def load_lidar(folder: Path):
    tiles, strips = [], []
    for f in sorted(folder.glob("*_tile.json")):
        feat = json.loads(f.read_text(encoding="utf-8"))["features"][0]
        p = feat["properties"]
        row = {"tile": f.name.replace("_tile.json", ""), "changed": p.get("senastAndrad"),
               "geometry": shape(feat["geometry"])}
        for k in p.get("klasstatistik", []):
            row[f"class_{k['klass']}"] = k["antalPunkter"]
        tiles.append(row)
    for f in sorted(folder.glob("*_strip.json")):
        for feat in json.loads(f.read_text(encoding="utf-8"))["features"]:
            p = feat["properties"]
            strips.append({"strip": p.get("id"), "date": p.get("insamlingsdatum"), "altitude_m": p.get("flyghojd"),
                           "density": p.get("punkttathet"), "scanner": f"{p.get('skannerfabrikat')} {p.get('skannermodell')}"})
    t = gpd.GeoDataFrame(tiles, geometry="geometry", crs="EPSG:3006").fillna(0)
    s = pd.DataFrame(strips).drop_duplicates("strip")
    return t, s


def load_dsm(folder: Path) -> gpd.GeoDataFrame:
    rows = []
    for f in sorted(folder.glob("y*.json")):
        d = json.loads(f.read_text(encoding="utf-8"))
        p = d.get("properties", {})
        rows.append({"tile": p.get("Ruta", f.stem), "date_from": p.get("Datum_fran"), "date_to": p.get("Datum_till"),
                     "photo_resolution_cm": p.get("Upplosning_flygbild"), "geometry": shape(d["geometry"])})
    return gpd.GeoDataFrame(rows, geometry="geometry", crs="EPSG:3006")


def load_shapefiles(folder: Path, names: List[str]) -> Dict[str, gpd.GeoDataFrame]:
    out = {}
    for n in names:
        f = folder / f"{n}.shp"
        if f.exists():
            out[n] = gpd.read_file(f)
    return out


def employment_layers(tables: Dict[str, SourceTable]):
    """Grid cells and DeSO areas with the employment values (raw columns, merged spellings)."""
    def pick(prefix):
        return next((t for k, t in tables.items() if k.startswith(prefix)), None)

    frames = {}
    sector_alias = {"Näringsli": "private", "Naringsliv": "private", "Staten": "state", "Kommun": "municipal",
                    "Region": "region", "Ovrigt": "other", "Övrigt": "other"}
    for prefix, tag in (("Tab7_Ruta", "res"), ("Tab8_Ruta", "work"), ("Tab9_Ruta", "status"), ("Tab10_Ruta", "edu")):
        t = pick(prefix)
        if t is None:
            continue
        d = t.data.drop(columns=t.data.geometry.name).copy()
        key = "ruta:" + d[t.size_column].astype(int).astype(str) + "_" + d[t.id_column].astype(str).str.strip()
        d = d.drop(columns=[t.id_column, t.size_column]).rename(columns=sector_alias)
        d.columns = [f"{tag}_{c}" for c in d.columns]
        frames[tag] = d.set_index(key)
    cells = pd.concat(frames.values(), axis=1)
    units = build_ruta_units(list(cells.index))
    cells = units.set_index("unit_id").join(cells)

    deso_frames = []
    geom = None
    for prefix, tag in (("Tab8_DeSO", "work"), ("Tab9_DeSO", "status")):
        t = pick(prefix)
        if t is None:
            continue
        d = t.data.rename(columns=sector_alias).set_index(t.id_column)
        if geom is None:
            geom = d.geometry
        d = d.drop(columns=d.geometry.name)
        d.columns = [f"{tag}_{c}" for c in d.columns]
        deso_frames.append(d)
    deso = gpd.GeoDataFrame(pd.concat(deso_frames, axis=1), geometry=geom, crs="EPSG:3006")
    return cells, deso


def election_totals(folder: Path, municipality: str) -> Dict:
    src = load_election(folder, municipality)
    long = src.sheets["roster_KF"]
    d = long_by_district(long)
    cat = long["Parti/kategori"].astype("string")
    is_party = ~(cat.isin(list(SUMMARY_CATEGORIES)) | cat.str.startswith(INVALID_PREFIX).fillna(False))
    votes = long[is_party].groupby("Parti/kategori")["Röster"].sum().sort_values(ascending=False)
    coll = long.loc[is_collection(long), DISTRICT_CODE_COLUMN].unique()
    return {
        "year": src.election_year, "type": src.election_type,
        "valid": float(d["valid_row"].sum()), "cast": float(d["cast_row"].sum()),
        "eligible": float(d["eligible_row"].sum()), "party_votes": votes,
        "collection_cast": float(d.loc[d.index.isin(coll), "cast_row"].sum()),
        "districts": src.districts, "municipality": municipality,
    }


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def load_report_data(config_path: Path, repo_root: Path) -> ReportData:
    cfg = load_city_config(config_path, repo_root)
    rc = cfg.raw.get("dataReport") or {}

    def path(key: str) -> Optional[Path]:
        return (repo_root / rc[key]) if rc.get(key) else None

    logger.info("Loading buildings")
    b = gpd.read_file(cfg.buildings.source, layer=cfg.buildings.layer, columns=BUILDING_COLUMNS) \
        if cfg.buildings.layer else gpd.read_file(cfg.buildings.source, columns=BUILDING_COLUMNS)
    summary_file = cfg.buildings.source.parent / "lidar_heights_summary.json"
    height_summary = json.loads(summary_file.read_text(encoding="utf-8")) if summary_file.exists() else {}

    logger.info("Loading LiDAR / DSM sidecars")
    lidar_tiles, lidar_strips = load_lidar(path("lidarTiles"))
    dsm = load_dsm(path("surfaceModel")) if path("surfaceModel") else gpd.GeoDataFrame()

    logger.info("Loading Unity package layers")
    pkg = cfg.package_dir
    package = {k: read_spatial_layer(pkg, k) for k in ("ruta", "deso", "valdistrikt")}
    values = {k: read_data_layer(pkg, k) for k in (
        "ruta_population", "ruta_income", "deso_population", "deso_income", "valdistrikt_election",
        "valdistrikt_estimates", "ruta_places", "deso_places")}

    logger.info("Loading SCB deliveries")
    population = load_scb_tables(cfg.statistics.population)
    income = load_scb_tables(cfg.statistics.income) if cfg.statistics.income else {}
    employment = load_scb_tables(path("employment")) if path("employment") else {}
    emp_cells, emp_deso = employment_layers(employment) if employment else (gpd.GeoDataFrame(), gpd.GeoDataFrame())

    logger.info("Loading election")
    election = election_totals(cfg.election.folder, cfg.election.municipality_code)

    logger.info("Loading base maps")
    roadmap = load_shapefiles(path("roadMap"), ["my_riks", "mv_riks", "vl_riks", "jl_riks", "mb_riks", "by_riks"]) \
        if path("roadMap") else {}
    propertymap = load_shapefiles(path("propertyMap"), ["vl_riks", "vo_riks", "jl_riks", "ba_riks", "tx_riks", "kl_riks", "bo_riks"]) \
        if path("propertyMap") else {}

    deliveries = {}
    for key, folder in (("buildings", path("buildingsRaw")), ("population", cfg.statistics.population),
                        ("income", cfg.statistics.income), ("employment", path("employment")),
                        ("lidar", path("lidarTiles")), ("dsm", path("surfaceModel")),
                        ("roadmap", path("roadMap")), ("propertymap", path("propertyMap"))):
        if folder is not None:
            deliveries[key] = read_delivery_note(Path(folder))

    data = ReportData(
        cfg=cfg, report_cfg=rc, repo_root=repo_root, buildings=b, height_summary=height_summary,
        lidar_tiles=lidar_tiles, lidar_strips=lidar_strips, dsm_tiles=dsm, package=package, values=values,
        population_tables=population, income_tables=income, employment_tables=employment,
        employment_cells=emp_cells, employment_deso=emp_deso, election=election,
        roadmap=roadmap, propertymap=propertymap, deliveries=deliveries,
    )
    data.extents = delivery_extents(data)
    return data


def delivery_extents(d: ReportData) -> Dict[str, tuple]:
    """Bounding boxes of the deliveries, to show that they do not all cover the same area."""
    def grid_bounds(tables: Dict[str, SourceTable]):
        t = next((t for t in tables.values() if t.unit == "Ruta" and t.size_column), None)
        if t is None:
            return None
        units = build_ruta_units(list("ruta:" + t.data[t.size_column].astype(int).astype(str) + "_" + t.data[t.id_column].astype(str)))
        return tuple(units.total_bounds)

    out = {
        "Study area (LiDAR tiles)": tuple(d.lidar_tiles.total_bounds),
        "Buildings": tuple(d.buildings.total_bounds),
        "Population / income grid": grid_bounds(d.population_tables),
        "Employment grid": grid_bounds(d.employment_tables) if d.employment_tables else None,
        "Voting districts (municipality)": tuple(d.election["districts"].total_bounds),
    }
    return {k: v for k, v in out.items() if v is not None}


def study_box(d: ReportData):
    minx, miny, maxx, maxy = d.extents["Study area (LiDAR tiles)"]
    return box(minx, miny, maxx, maxy)
