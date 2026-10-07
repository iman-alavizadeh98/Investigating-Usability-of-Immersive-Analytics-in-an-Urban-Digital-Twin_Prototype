#!/usr/bin/env python3
"""
Tests for the Unity package builder (city config -> buildings export + manifest).

Synthetic data only, for an invented city; no project data is read.
Run: python tests/test_unity_package.py   (pytest also collects the test_* functions)
"""

import json
import sys
import tempfile
from pathlib import Path

import geopandas as gpd
import numpy as np
import pandas as pd
from shapely.geometry import MultiPolygon, Polygon, box

sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from pipelines.unity_package.buildings import BUILDING_FIELDS, export_buildings, read_geometry_bin  # noqa: E402
from pipelines.unity_package.config import load_city_config  # noqa: E402
from pipelines.unity_package.manifest import write_manifest  # noqa: E402

OE, ON = 500000.0, 6500000.0  # origin of the invented city


def _signed_area(ring):
    x, z = ring[:, 0].astype(float), ring[:, 1].astype(float)
    return 0.5 * np.sum(x * np.roll(z, -1) - np.roll(x, -1) * z)


def _buildings():
    courtyard = Polygon(
        [(OE + 100, ON + 100), (OE + 100, ON + 140), (OE + 140, ON + 140), (OE + 140, ON + 100)],  # clockwise on purpose
        [[(OE + 110, ON + 110), (OE + 130, ON + 110), (OE + 130, ON + 130), (OE + 110, ON + 130)]],  # CCW hole on purpose
    )
    return gpd.GeoDataFrame(
        {
            "object_id": ["b-uuid", "a-uuid", "c-uuid"],
            "building_name_primary": [None, "Rådhuset", None],
            "house_number": [3, 1, 2],
            "main_building_flag": [True, False, None],
            "object_type": ["Bostad", "Samhällsfunktion", "Komplementbyggnad"],
            "object_type_en": ["Residence", "Public facility", "Ancillary building"],
            "primary_purpose_en": ["Detached house", "Town hall", "Ancillary building (unspecified)"],
            "quaternary_purpose_en": [None, None, None],          # empty everywhere -> skipped
            "height_m": [7.5, 21.25, 0.0],
            "has_height": [True, True, False],
            # "<NA>" as written by the 2026-10-06 height run for buildings with a height
            "no_height_reason_code": ["<NA>", None, "surface_shows_ground"],
            "ground_z": [12.0, 30.5, None],
            "footprint_area_m2": [1200.0, 400.0, 9.0],
            "version_valid_from": pd.to_datetime(["2015-03-01 10:48:49", "2020-06-30 00:00:00", "2011-03-21 17:50:46"], utc=True),
        },
        geometry=[
            MultiPolygon([courtyard]),
            MultiPolygon([box(OE + 10, ON + 10, OE + 30, ON + 30), box(OE + 40, ON + 10, OE + 50, ON + 20)]),
            MultiPolygon([box(OE + 200, ON + 200, OE + 203, ON + 203)]),
        ],
        crs="EPSG:3006",
    )


def _city(tmp: Path, gdf=None, origin=(OE, ON)):
    src = tmp / "buildings_lidar_added.gpkg"
    (gdf if gdf is not None else _buildings()).to_file(src, layer="buildings_lidar_added")
    cfg = {
        "city": {"id": "testby", "displayName": "Testby"},
        "crs": "EPSG:3006",
        "unity": {
            "streamingAssetsDirectory": str(tmp / "StreamingAssets"),
            "packageDirectory": "testby",
            "originInSourceCRS": {"easting": origin[0], "northing": origin[1], "elevation": 0.0},
            "metersToUnity": 0.001,
        },
        "buildings": {"source": str(src), "layer": None},
    }
    path = tmp / "testby.json"
    path.write_text(json.dumps(cfg), encoding="utf-8")
    return load_city_config(path, repo_root=tmp)


def test_buildings_export_and_manifest():
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        city = _city(tmp)
        result = export_buildings(city)
        manifest_path = write_manifest(city)

        pkg = tmp / "StreamingAssets" / "cities" / "testby"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        layer = json.loads((pkg / "urban_context/buildings/layer.json").read_text(encoding="utf-8"))
        attrs = json.loads((pkg / "urban_context/buildings/attributes.json").read_text(encoding="utf-8"))
        geo = read_geometry_bin(pkg / "urban_context/buildings/geometry.bin")

    # Manifest: from the config + what is on disk; paths relative to the package
    assert manifest["projectId"] == "testby"
    assert manifest["unityTransform"]["originInSourceCRS"]["easting"] == OE
    assert manifest["urbanContext"] == [{"id": "buildings", "definition": "urban_context/buildings/layer.json"}]
    assert manifest["spatialLayers"] == [] and manifest["dataLayers"] == []
    assert city.manifest_path_in_streaming_assets == "cities/testby/project_manifest.json"

    # Sorted by id, same order in both files
    ids = ["building:a-uuid", "building:b-uuid", "building:c-uuid"]
    assert [b["id"] for b in geo["buildings"]] == ids
    assert attrs["unitIds"] == ids
    assert geo["origin"] == (OE, ON)

    a, b, c = geo["buildings"]
    assert abs(a["height"] - 21.25) < 1e-6 and a["has_height"] and abs(a["ground_z"] - 30.5) < 1e-6
    assert c["height"] == 0.0 and not c["has_height"] and np.isnan(c["ground_z"])
    assert len(a["polygons"]) == 2                       # MultiPolygon kept as two polygons

    # Courtyard: exterior CCW, hole CW, relative coordinates, no closing vertex
    rings = b["polygons"][0]
    assert len(rings) == 2
    assert _signed_area(rings[0]) > 0 and _signed_area(rings[1]) < 0
    assert len(rings[0]) == 4 and len(rings[1]) == 4
    assert rings[0][:, 0].min() == 100.0 and rings[0][:, 1].max() == 140.0

    # Attributes: typed columns, validity masks, empty optional fields skipped
    cols = {c["variableId"]: c for c in attrs["columns"]}
    assert cols["name"]["stringValues"] == ["Rådhuset", "", ""]
    assert cols["name"]["valid"] == [True, False, False]
    assert cols["house_number"]["integerValues"] == [1, 3, 2]
    assert cols["main_building"]["valid"] == [True, True, False]
    assert cols["height"]["floatValues"] == [21.25, 7.5, 0.0]
    assert cols["height"]["valid"] == [True, True, False]   # no height measured -> not shown as 0 m
    assert cols["no_height_reason"]["valid"] == [False, False, True]   # only shown without a height
    assert cols["no_height_reason"]["stringValues"][2] == "surface_shows_ground"
    assert cols["ground_elevation"]["valid"] == [True, True, False]
    assert cols["version_valid_from"]["stringValues"] == ["2020-06-30", "2015-03-01", "2011-03-21"]
    assert "purpose_4" not in cols                      # all empty
    assert "collection_level" not in cols               # column missing (optional)
    assert layer["skippedFields"]["purpose_4"] == "no values"

    # Field list in layer.json = columns present, in display order, with groups
    assert [f["id"] for f in layer["fields"]] == [c["variableId"] for c in attrs["columns"]]
    assert layer["fields"][0]["group"] == "Identity"
    assert layer["buildingCount"] == 3 and layer["buildingsWithHeight"] == 2
    assert result["report"]["geometry"]["holes"] == 1


def test_manifest_keeps_other_exported_layers():
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        city = _city(tmp)
        export_buildings(city)
        # a data layer exported by some other step later
        dl = tmp / "StreamingAssets" / "cities" / "testby" / "data_layers" / "income_2023"
        dl.mkdir(parents=True)
        (dl / "layer.json").write_text(json.dumps({"id": "income_2023"}), encoding="utf-8")
        manifest = json.loads(write_manifest(city).read_text(encoding="utf-8"))
    assert manifest["dataLayers"] == [{"id": "income_2023", "definition": "data_layers/income_2023/layer.json"}]
    assert len(manifest["urbanContext"]) == 1


def test_rejects_far_origin_and_bad_input():
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        try:
            export_buildings(_city(tmp, origin=(OE - 50000, ON)))
            raise AssertionError("expected a far-origin error")
        except ValueError as e:
            assert "from the origin" in str(e)
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        g = _buildings()
        g = pd.concat([g, g.iloc[[0]]])
        try:
            export_buildings(_city(tmp, gdf=gpd.GeoDataFrame(g, crs="EPSG:3006")))
            raise AssertionError("expected a duplicate-id error")
        except ValueError as e:
            assert "not unique" in str(e)
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        g = _buildings().drop(columns=["object_type"])
        try:
            export_buildings(_city(tmp, gdf=g))
            raise AssertionError("expected a missing-column error")
        except ValueError as e:
            assert "object_type" in str(e)


def test_city_config_requires_fields():
    with tempfile.TemporaryDirectory() as tmp:
        p = Path(tmp) / "x.json"
        p.write_text(json.dumps({"city": {"id": "x", "displayName": "X"}, "crs": "EPSG:3006",
                                 "unity": {"streamingAssetsDirectory": "s", "metersToUnity": 0.001}}),
                     encoding="utf-8")
        try:
            load_city_config(p, Path(tmp))
            raise AssertionError("expected a missing-origin error")
        except ValueError as e:
            assert "originInSourceCRS" in str(e)


def test_field_ids_unique():
    ids = [f.id for f in BUILDING_FIELDS]
    assert len(ids) == len(set(ids))


if __name__ == "__main__":
    tests = [(name, fn) for name, fn in sorted(globals().items()) if name.startswith("test_") and callable(fn)]
    failed = 0
    for name, fn in tests:
        try:
            fn()
            print(f"PASS {name}")
        except Exception as exc:  # noqa: BLE001
            failed += 1
            print(f"FAIL {name}: {type(exc).__name__}: {exc}")
    print(f"{len(tests) - failed}/{len(tests)} passed")
    sys.exit(1 if failed else 0)
