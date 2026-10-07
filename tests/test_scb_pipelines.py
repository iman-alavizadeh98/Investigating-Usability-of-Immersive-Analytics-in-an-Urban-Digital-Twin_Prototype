#!/usr/bin/env python3
"""
Tests for the SCB delivery pipelines (population, income), their shared core and the DataFrame HTML viewer.

Synthetic SCB-style shapefiles for an invented city; no project data is read.
Run: python tests/test_scb_pipelines.py   (pytest also collects the test_* functions)
"""

import json
import sys
import tempfile
from pathlib import Path

import geopandas as gpd
import pandas as pd
from shapely.geometry import box

sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from pipelines.population import PopulationPipeline  # noqa: E402
from pipelines.income import IncomePipeline  # noqa: E402
from pipelines.income.config import INCOME  # noqa: E402
from pipelines.population.config import POPULATION  # noqa: E402
from pipelines.scb.loader import load_scb_tables, parse_file_name  # noqa: E402
from pipelines.scb.validator import validate_tables  # noqa: E402
from utils.data_profiler import DataFrameProfiler  # noqa: E402
from utils.dataframe_html import dataframe_page, prepare_for_display  # noqa: E402

E0, N0 = 500000, 6500000  # south-west corner of the invented city
CRS = "EPSG:3006"


def _rid(e, n):
    return f"{e:06d}{n:07d}"


def _cells(specs):
    """specs: (easting, northing, size[, cut]) → geometry list; cut halves the cell."""
    geoms = []
    for e, n, s, *cut in specs:
        geoms.append(box(e, n, e + (s / 2 if cut and cut[0] else s), n + s))
    return geoms


def _write_delivery(folder: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    specs = [
        (E0, N0, 250), (E0 + 250, N0, 250), (E0 + 500, N0 + 250, 250, True),  # last one cut at the edge
        (E0, N0, 1000),                     # same corner as the first 250 m cell, overlaps three of them
        (E0 + 2000, N0, 1000),
    ]
    tab1 = gpd.GeoDataFrame({
        "Rutstorl": [s[2] for s in specs],
        "RutID_SW": [_rid(s[0], s[1]) for s in specs],
        "Alder_0_6": [10, 4, 0, 1, 2],
        "Alder_65": [5, 6, 3, 2, 0],
        "Totalt": [15, 10, 3, 3, 3],         # last row: parts 2 ≠ total 3 (SCB perturbation)
    }, geometry=_cells(specs), crs=CRS)
    tab1.to_file(folder / "Tab1_Ruta_2030.shp")

    # Tab3: field name in cp1252 while the .cpg says UTF-8, as in the real delivery.
    tab3 = gpd.GeoDataFrame({
        "Rutstorl": [250, 250, 1000],
        "RutID_SW": [_rid(E0, N0), _rid(E0 + 250, N0), _rid(E0, N0)],
        "Ogifta": [8, 5, 1],
        "Änka_Änk": [1, 0, 0],
        "Totalt": [9, 5, 1],
    }, geometry=_cells([(E0, N0, 250), (E0 + 250, N0, 250), (E0, N0, 1000)]), crs=CRS)
    tab3.to_file(folder / "Tab3_Ruta_2030.shp", encoding="cp1252")
    (folder / "Tab3_Ruta_2030.cpg").write_text("UTF-8", encoding="ascii")

    tab6 = gpd.GeoDataFrame({
        "Rutstorl": [100, 100],
        "Ruta": [_rid(E0, N0), _rid(E0 + 100, N0)],
        "Totalt": [7, 0],
    }, geometry=_cells([(E0, N0, 100), (E0 + 100, N0, 100)]), crs=CRS)
    tab6.to_file(folder / "Tab6_Ruta_2030_region.shp")

    deso = gpd.GeoDataFrame({
        "DeSO_2031": ["9999C0001", "bad-code", "9999A0002"],
        "Alder_0_6": [100, 50, 9],
        "Alder_65": [40, 20, 1],
        "Totalt": [140, 70, 10],
        "Mystery": [1, 2, 0],                # not in config.FIELDS
    }, geometry=[
        box(E0, N0, E0 + 1000, N0 + 1000), box(E0 + 1000, N0, E0 + 3000, N0 + 1000),
        None,                                # area cut away by the delivery rectangle, as in real data
    ], crs=CRS)
    deso.to_file(folder / "Tab1_DeSO_2030.shp")


def test_parse_file_name():
    assert parse_file_name("Tab6_Ruta_2030_region") == {"number": "6", "unit": "Ruta", "year": "2030", "suffix": "region"}
    assert parse_file_name("Tab10_DeSO_2031")["number"] == "10"
    assert parse_file_name("something_else")["number"] is None


def test_load_keeps_raw_data_and_detects_columns():
    with tempfile.TemporaryDirectory() as tmp:
        folder = Path(tmp) / "delivery"
        _write_delivery(folder)
        tables = load_scb_tables(folder)
        assert sorted(tables) == ["Tab1_DeSO_2030", "Tab1_Ruta_2030", "Tab3_Ruta_2030", "Tab6_Ruta_2030_region"]

        t3 = tables["Tab3_Ruta_2030"]
        assert t3.declared_encoding == "UTF-8" and t3.encoding_used == "cp1252"
        assert "Änka_Änk" in t3.data.columns

        t1 = tables["Tab1_Ruta_2030"]
        assert (t1.table_number, t1.unit, t1.year) == ("1", "Ruta", 2030)
        assert (t1.id_column, t1.size_column) == ("RutID_SW", "Rutstorl")
        assert t1.encoding_used is None
        assert list(t1.data["Totalt"]) == [15, 10, 3, 3, 3]          # unchanged
        assert tables["Tab6_Ruta_2030_region"].id_column == "Ruta"
        assert tables["Tab1_DeSO_2030"].id_column == "DeSO_2031"   # year in the name is data

        only = load_scb_tables(folder, only=["Tab1_Ruta"])
        assert list(only) == ["Tab1_Ruta_2030"]
        try:
            load_scb_tables(Path(tmp) / "missing")
            raise AssertionError("missing folder must raise")
        except FileNotFoundError:
            pass


def test_validation_reports_known_scb_properties():
    with tempfile.TemporaryDirectory() as tmp:
        folder = Path(tmp) / "delivery"
        _write_delivery(folder)
        report = validate_tables(load_scb_tables(folder), POPULATION)
        r1 = report["tables"]["Tab1_Ruta_2030"]
        assert r1["cell_sizes_m"] == {250: 3, 1000: 2}
        assert r1["cells_not_full_square"] == 1
        assert r1["coarse_cells_containing_finer_cells"] == 1
        assert r1["ids_shared_by_different_sizes"] == 1
        assert r1["duplicate_keys"] == 0 and r1["off_lattice_cells"] == 0
        assert r1["parts_vs_total"]["rows_where_parts_differ_from_total"] == 1
        assert r1["total_sum"] == 34
        assert r1["issues"] == [], r1["issues"]
        assert len(r1["notes"]) == 3

        r3 = report["tables"]["Tab3_Ruta_2030"]
        assert any("cp1252" in i for i in r3["issues"])
        assert r3["unknown_columns"] == []

        rd = report["tables"]["Tab1_DeSO_2030"]
        assert rd["ids_bad_format"] == 1
        assert rd["unknown_columns"] == ["Mystery"]
        assert rd["null_geometries"] == 1 and rd["invalid_geometries"] == 0

        groups = {g["group"]: g for g in report["coverage"]}
        g = groups["Ruta 250/1000 m"]
        assert g["units_in_any_table"] == 5 and g["units_in_every_table"] == 3
        missing = {row["table"]: row["missing_vs_union"] for row in g["per_table"]}
        assert missing == {"Tab1_Ruta_2030": 0, "Tab3_Ruta_2030": 2}
        assert "Ruta 100 m" in groups and "DeSO" in groups


def test_pipeline_writes_views_profiles_and_report():
    with tempfile.TemporaryDirectory() as tmp:
        folder = Path(tmp) / "delivery"
        out = Path(tmp) / "out"
        _write_delivery(folder)
        pipeline = PopulationPipeline(config={"input_dir": str(folder)}, verbose=False)
        result = pipeline.run(output_dir=out)
        assert result["status"] == "success", result

        index = (out / "index.html").read_text(encoding="utf-8")
        assert 'href="tables/Tab1_Ruta_2030.html"' in index
        assert "UNKNOWN - not in the field dictionary" in index           # Mystery column in the dictionary
        for key in pipeline.tables:
            page = (out / "tables" / f"{key}.html").read_text(encoding="utf-8")
            assert "<table" in page and "geometry_type" in page
            assert (out / "profiles" / f"{key}_profile.html").exists()
            assert (out / "profiles" / f"{key}_profile.md").exists()
        assert "Änka_Änk" in (out / "tables" / "Tab3_Ruta_2030.html").read_text(encoding="utf-8")

        report = json.loads((out / "population_preprocess_report.json").read_text(encoding="utf-8"))
        assert report["inputs"]["Tab3_Ruta_2030"]["encoding_fallback_used"] == "cp1252"
        assert report["preprocessing"]["steps"] == []
        assert report["validation"]["issue_count"] >= 3


def test_dataframe_html_escapes_and_summarises_geometry():
    gdf = gpd.GeoDataFrame(
        {"name": ["<script>alert(1)</script>", None], "n": [1, 2], "x": [0.5, float("nan")]},
        geometry=[box(0, 0, 10, 10), box(0, 0, 1, 1)], crs=CRS,
    )
    shown = prepare_for_display(gdf)
    assert "geometry" not in shown.columns
    assert list(shown["geometry_area_m2"]) == [100.0, 1.0]
    page = dataframe_page(gdf, "Test", column_notes={"n": "a count"})
    assert "<script>alert(1)</script>" not in page and "&lt;script&gt;" in page
    assert page.count('class="null"') == 2                       # None and NaN
    assert 'title="a count"' in page


def _write_income_delivery(folder: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    specs = [(E0, N0, 250), (E0 + 250, N0, 250), (E0 + 2000, N0, 1000)]
    gpd.GeoDataFrame({
        "Rutstorl": [s[2] for s in specs],
        "RutID_SW": [_rid(s[0], s[1]) for s in specs],
        "Kvartil1": [5, 1, 0], "Kvartil2": [4, 2, 0], "Kvartil3": [3, 3, 0], "Kvartil4": [2, 4, 0],
        "Totalt": [14, 10, 0],
        "Tot_CDISP0": [3_500_000.0, 3_000_000.0, 408_447.0],   # last: 0 households but an income sum
    }, geometry=_cells(specs), crs=CRS).to_file(folder / "Tab11_Ruta_2030.shp")
    gpd.GeoDataFrame({
        "DeSO_2031": ["9999C0001", "9999C0002"],
        "Kvartil1": [100, 50], "Kvartil2": [90, 60], "Kvartil3": [80, 70], "Kvartil4": [70, 81],
        "Totalt": [340, 260],
        "MedianInk": [250_000.0, 300_000.0],
    }, geometry=[box(E0, N0, E0 + 1000, N0 + 1000), box(E0 + 1000, N0, E0 + 3000, N0 + 1000)],
        crs=CRS).to_file(folder / "Tab11_DeSO_2030.shp")


def test_income_values_are_not_counted_as_parts():
    with tempfile.TemporaryDirectory() as tmp:
        folder = Path(tmp) / "income"
        _write_income_delivery(folder)
        report = validate_tables(load_scb_tables(folder), INCOME)
        ruta = report["tables"]["Tab11_Ruta_2030"]
        assert ruta["issues"] == [], ruta["issues"]
        assert ruta["count_columns"] == ["Kvartil1", "Kvartil2", "Kvartil3", "Kvartil4"]
        assert ruta["value_columns"] == ["Tot_CDISP0"]
        assert ruta["parts_vs_total"]["rows_where_parts_differ_from_total"] == 0   # SEK not added to households
        assert ruta["values_where_total_is_zero"] == {"Tot_CDISP0": 1}
        deso = report["tables"]["Tab11_DeSO_2030"]
        assert deso["value_columns"] == ["MedianInk"]
        assert deso["value_stats"]["MedianInk"]["median"] == 275_000.0
        assert deso["parts_vs_total"]["rows_where_parts_differ_from_total"] == 1  # 261 vs 260

        out = Path(tmp) / "out"
        result = IncomePipeline(config={"input_dir": str(folder)}, verbose=False).run(output_dir=out)
        assert result["status"] == "success", result
        assert (out / "income_preprocess_report.json").exists()
        index = (out / "index.html").read_text(encoding="utf-8")
        assert "Income preprocess" in index and "MedianInk" in index
        assert "UNKNOWN" not in index


def test_same_files_unknown_to_another_dataset_are_flagged():
    with tempfile.TemporaryDirectory() as tmp:
        folder = Path(tmp) / "income"
        _write_income_delivery(folder)
        report = validate_tables(load_scb_tables(folder), POPULATION)   # wrong catalog on purpose
        ruta = report["tables"]["Tab11_Ruta_2030"]
        assert any("not in the population table catalog" in i for i in ruta["issues"])
        assert set(ruta["unknown_columns"]) == {"Kvartil1", "Kvartil2", "Kvartil3", "Kvartil4", "Tot_CDISP0"}


def test_profiler_sees_pandas_string_columns():
    df = pd.DataFrame({"code": pd.array(["a", "b", "a"], dtype="string"), "n": pd.array([1, 2, None], dtype="Int64")})
    prof = DataFrameProfiler(df, "t").profile()
    assert prof["categorical_stats"]["code"] == {"a": 2, "b": 1}
    assert "n" in prof["numeric_stats"]


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
