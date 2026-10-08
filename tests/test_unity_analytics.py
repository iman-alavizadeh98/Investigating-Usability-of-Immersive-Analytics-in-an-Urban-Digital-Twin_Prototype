#!/usr/bin/env python3
"""
Tests for the analytical layers of the Unity package (SCB grid + DeSO, voting
districts, building links, dasymetric district estimates).

Synthetic data for an invented city; no project data is read.
Run: python tests/test_unity_analytics.py   (pytest also collects the test_* functions)
"""

import json
import sys
import tempfile
from pathlib import Path

import geopandas as gpd
import pandas as pd
from shapely.geometry import box

sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from pipelines.scb.loader import load_scb_tables  # noqa: E402
from pipelines.unity_package.layer_files import write_spatial_layer, SpatialLayerSpec  # noqa: E402
from pipelines.unity_package.links import (  # noqa: E402
    assign, dasymetric, export_associations, export_district_estimates, grid_counts, statistics_extent,
)
from pipelines.unity_package.statistics import build_ruta_units, export_deso, export_ruta  # noqa: E402

E0, N0 = 500000, 6500000
CRS = "EPSG:3006"


def _rid(e, n):
    return f"{e:06d}{n:07d}"


def _scb(folder: Path):
    """Two 250 m cells inside one 1 km cell, plus one free 1 km cell; DeSO with one null geometry."""
    folder.mkdir(parents=True, exist_ok=True)
    cells = [(E0, N0, 250), (E0 + 250, N0, 250), (E0, N0, 1000), (E0 + 2000, N0, 1000)]
    geom = [box(e, n, e + s, n + s) for e, n, s, *_ in cells]
    base = {"Rutstorl": [c[2] for c in cells], "RutID_SW": [_rid(c[0], c[1]) for c in cells]}
    gpd.GeoDataFrame({**base, "Alder_0_6": [10, 0, 1, 0], "Alder_7_15": [0, 0, 0, 0], "Alder_16_1": [0, 0, 0, 0],
                      "Alder_20_2": [0, 0, 0, 0], "Alder_25_4": [30, 20, 3, 0], "Alder_45_6": [0, 0, 0, 0],
                      "Alder_65": [10, 0, 0, 4], "Totalt": [50, 20, 4, 4]}, geometry=geom, crs=CRS).to_file(folder / "Tab1_Ruta_2030.shp")
    gpd.GeoDataFrame({**base, "Sverige": [40, 10, 4, 4], "Norden_uto": [0, 0, 0, 0], "EU_utom_No": [0, 0, 0, 0],
                      "Ovriga_var": [10, 10, 0, 0], "Totalt": [50, 20, 4, 4]}, geometry=geom, crs=CRS).to_file(folder / "Tab4_Ruta_2030.shp")
    gpd.GeoDataFrame({**base, "Kvartil1": [5, 5, 0, 0], "Kvartil2": [5, 0, 1, 0], "Kvartil3": [5, 0, 0, 0], "Kvartil4": [5, 5, 1, 0],
                      "Totalt": [20, 10, 2, 0], "Tot_CDISP0": [6e6, 2e6, 5e5, 3e5]},   # last: 0 households, income sum > 0
                     geometry=geom, crs=CRS).to_file(folder / "Tab11_Ruta_2030.shp")
    gpd.GeoDataFrame({"DeSO_2031": ["9999C0001", "9999C0002"], "Alder_0_6": [10, 1], "Alder_7_15": [0, 0], "Alder_16_1": [0, 0],
                      "Alder_20_2": [0, 0], "Alder_25_4": [50, 1], "Alder_45_6": [0, 0], "Alder_65": [14, 1], "Totalt": [74, 3]},
                     geometry=[box(E0, N0, E0 + 1000, N0 + 1000), None], crs=CRS).to_file(folder / "Tab1_DeSO_2030.shp")


def _buildings():
    rows = [  # (id, category, height, footprint box) - all inside the first 250 m cell except the last two
        ("a", "Residential", 9.0, box(E0 + 10, N0 + 10, E0 + 30, N0 + 30)),    # 400 m2 x 3 floors = 1200
        ("b", "Residential", 3.0, box(E0 + 50, N0 + 50, E0 + 70, N0 + 70)),    # 400 m2 x 1 floor = 400
        ("c", "Industrial", 20.0, box(E0 + 100, N0 + 100, E0 + 150, N0 + 150)),  # gets nothing: cell has residential
        ("d", "Industrial", 6.0, box(E0 + 300, N0 + 10, E0 + 320, N0 + 30)),   # only building of cell 2 -> fallback
        ("e", "Ancillary", 3.0, box(E0 + 3000, N0 + 3000, E0 + 3010, N0 + 3010)),  # no populated cell
    ]
    g = gpd.GeoDataFrame({"building_id": [f"building:{r[0]}" for r in rows], "is_residential": [r[1] == "Residential" for r in rows],
                          "height_m": [r[2] for r in rows]}, geometry=[r[3] for r in rows], crs=CRS)
    g["footprint_m2"] = g.geometry.area
    return g.set_geometry(g.geometry.representative_point())


def test_ruta_remainder_geometry_and_ids():
    units = build_ruta_units([f"ruta:250_{_rid(E0, N0)}", f"ruta:250_{_rid(E0 + 250, N0)}", f"ruta:1000_{_rid(E0, N0)}"])
    coarse = units.set_index("unit_id").loc[f"ruta:1000_{_rid(E0, N0)}"]
    assert abs(coarse.geometry.area - (1_000_000 - 2 * 62_500)) < 1e-6   # square minus the two 250 m cells
    assert coarse["display_name"] == "1 km grid cell"


def test_statistics_layers_shares_and_undefined_mean():
    with tempfile.TemporaryDirectory() as tmp:
        _scb(Path(tmp) / "scb")
        tables = load_scb_tables(Path(tmp) / "scb")
        pkg = Path(tmp) / "pkg"
        units, report = export_ruta(pkg, tables, tables)
        assert report["units"] == 4 and report["mean_income_undefined_cells"] == 1
        values = json.loads((pkg / "data_layers" / "ruta_population" / "values.json").read_text(encoding="utf-8"))
        cols = {c["variableId"]: c for c in values["columns"]}
        i = values["unitIds"].index(f"ruta:250_{_rid(E0, N0)}")
        assert cols["share_born_abroad"]["floatValues"][i] == 20.0      # 10 / (40 + 10)
        assert abs(cols["share_65_plus"]["floatValues"][i] - 20.0) < 1e-9
        inc = json.loads((pkg / "data_layers" / "ruta_income" / "values.json").read_text(encoding="utf-8"))
        mean = next(c for c in inc["columns"] if c["variableId"] == "mean_economic_standard")
        j = inc["unitIds"].index(f"ruta:1000_{_rid(E0 + 2000, N0)}")
        assert mean["valid"][j] is False                                 # 0 households: no mean (D4)
        deso_units, deso_report = export_deso(pkg, tables, tables)
        assert deso_report["without_geometry"] == ["deso:9999C0002"] and deso_report["units"] == 1
        layer = json.loads((pkg / "spatial_layers" / "ruta" / "layer.json").read_text(encoding="utf-8"))
        assert layer["geometryType"] == "MultiPolygon" and layer["visibleByDefault"] is False


def test_dasymetric_conserves_counts_and_uses_fallbacks():
    with tempfile.TemporaryDirectory() as tmp:
        _scb(Path(tmp) / "scb")
        tables = load_scb_tables(Path(tmp) / "scb")
        units, _ = export_ruta(Path(tmp) / "pkg", tables, tables)
        points = _buildings()
        counts = grid_counts(tables, tables, pd.Index(units["unit_id"]))
        alloc, leftover, report = dasymetric(points, units, counts, 3.0)
        assert abs(alloc.loc["building:a", "population"] - 50 * 1200 / 1600) < 1e-9
        assert abs(alloc.loc["building:b", "population"] - 50 * 400 / 1600) < 1e-9
        assert "building:c" not in alloc.index                            # non-residential in a residential cell
        assert alloc.loc["building:d", "population"] == 20                # fallback: only building in its cell
        assert report["cells_spread_over_non_residential_buildings"] == 1
        assert abs(alloc["population"].sum() + leftover["population"].sum() - counts["population"].sum()) < 1e-9
        assert set(leftover.index) == {f"ruta:1000_{_rid(E0, N0)}", f"ruta:1000_{_rid(E0 + 2000, N0)}"}


def test_associations_and_district_estimates_respect_coverage():
    with tempfile.TemporaryDirectory() as tmp:
        _scb(Path(tmp) / "scb")
        tables = load_scb_tables(Path(tmp) / "scb")
        pkg = Path(tmp) / "pkg"
        units, _ = export_ruta(pkg, tables, tables)
        points = _buildings()
        districts = gpd.GeoDataFrame({
            "unit_id": ["valdistrikt:00000001", "valdistrikt:00000002"], "display_name": ["Inne", "Ute"],
        }, geometry=[box(E0, N0, E0 + 1000, N0 + 1000), box(E0 + 1000, N0, E0 + 9000, N0 + 9000)], crs=CRS)
        write_spatial_layer(pkg, SpatialLayerSpec("valdistrikt", "Districts", ""), districts)
        rep = export_associations(pkg, points, {"ruta": units, "valdistrikt": districts})
        assert rep["buildings_to_ruta"] == {"linked": 4, "unlinked": 1}
        pairs = json.loads((pkg / "associations" / "buildings_to_valdistrikt" / "pairs.json").read_text(encoding="utf-8"))
        assert dict(zip(pairs["sourceIds"], pairs["targetIds"]))["building:a"] == "valdistrikt:00000001"

        counts = grid_counts(tables, tables, pd.Index(units["unit_id"]))
        alloc, leftover, _ = dasymetric(points, units, counts, 3.0)
        eligible = pd.Series([60.0, 5.0], index=["00000001", "00000002"])
        est = export_district_estimates(pkg, alloc, leftover, points, units, districts, eligible,
                                        statistics_extent(tables), 99.0, {"population": 2030, "income": 2030})
        values = json.loads((pkg / "data_layers" / "valdistrikt_estimates" / "values.json").read_text(encoding="utf-8"))
        cols = {c["variableId"]: c for c in values["columns"]}
        # District 1 lies inside the SCB extent: 50 + 20 (buildings) + 4 (remainder cell, no building) residents.
        assert cols["residents_est"]["valid"] == [True, False]           # district 2 is mostly outside -> no estimate
        assert abs(cols["residents_est"]["floatValues"][0] - 74.0) < 1e-9
        assert est["districts_with_estimates"] == 1


def test_assign_uses_point_inside_footprint():
    pts = _buildings()
    units = gpd.GeoDataFrame({"unit_id": ["u:1"]}, geometry=[box(E0, N0, E0 + 250, N0 + 250)], crs=CRS)
    linked = assign(pts, units)
    assert set(linked.index) == {"building:a", "building:b", "building:c"}


if __name__ == "__main__":
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_") and callable(v)]
    failed = 0
    for test in tests:
        try:
            test()
            print(f"PASS {test.__name__}")
        except Exception as exc:  # noqa: BLE001
            failed += 1
            print(f"FAIL {test.__name__}: {type(exc).__name__}: {exc}")
    print(f"{len(tests) - failed}/{len(tests)} passed")
    sys.exit(1 if failed else 0)
