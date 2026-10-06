#!/usr/bin/env python3
"""
Tests for the buildings (Byggnad) translations and postprocess snapshot.

Synthetic data only; no project data is read or written.
Run: python tests/test_buildings_pipeline.py   (pytest also collects the test_* functions)
"""

import sys
import tempfile
from pathlib import Path

import geopandas as gpd
import pandas as pd
from shapely.geometry import MultiPolygon, box

sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from pipelines.buildings.config import PURPOSES  # noqa: E402
from pipelines.buildings.pipeline import BuildingsPipeline  # noqa: E402
from pipelines.buildings.postprocess import build_postprocess_snapshot  # noqa: E402
from pipelines.buildings.translation import (  # noqa: E402
    lookup_purpose,
    translate_collection_level_column,
    translate_purpose_column,
)

# Every andamal value seen in the Gothenburg, Helsingborg and Fortuna deliveries.
OBSERVED_PURPOSES = [
    "Bostad;Flerfamiljshus", "Bostad;Ospecificerad", "Bostad;Småhus friliggande",
    "Bostad;Småhus kedjehus", "Bostad;Småhus med flera lägenheter", "Bostad;Småhus radhus",
    "Ekonomibyggnad;", "Industri;Ospecificerad", "Industri;Tillverkning", "Komplementbyggnad;",
    "Samhällsfunktion;Badhus", "Samhällsfunktion;Brandstation", "Samhällsfunktion;Busstation",
    "Samhällsfunktion;Djursjukhus", "Samhällsfunktion;Högskola", "Samhällsfunktion;Ishall",
    "Samhällsfunktion;Järnvägsstation", "Samhällsfunktion;Kommunhus",
    "Samhällsfunktion;Kriminalvårdsanstalt", "Samhällsfunktion;Kulturbyggnad",
    "Samhällsfunktion;Multiarena", "Samhällsfunktion;Ospecificerad", "Samhällsfunktion;Polisstation",
    "Samhällsfunktion;Ridhus", "Samhällsfunktion;Samfund", "Samhällsfunktion;Sjukhus",
    "Samhällsfunktion;Skola", "Samhällsfunktion;Sporthall", "Samhällsfunktion;Universitet",
    "Samhällsfunktion;Vårdcentral", "Verksamhet;", "Övrig byggnad;",
]


def test_all_observed_purposes_translate():
    for value in OBSERVED_PURPOSES:
        label, category, matched = lookup_purpose(value)
        assert matched, value
        assert label and label != value, value
        assert category, value


def test_purpose_examples():
    assert lookup_purpose("Samhällsfunktion;Sjukhus") == ("Hospital", "Public", True)
    assert lookup_purpose("Samhällsfunktion;Skola") == ("School", "Public", True)
    assert lookup_purpose("Bostad;Ospecificerad")[0] == "Residence (unspecified)"
    assert lookup_purpose("Industri;Ospecificerad")[0] == "Industrial (unspecified)"
    assert lookup_purpose("Verksamhet;") == ("Business (unspecified)", "Business", True)
    assert lookup_purpose(" Samhällsfunktion ; Skola ")[0] == "School"
    assert lookup_purpose(None) == (None, None, True)


def test_unknown_purpose_is_kept_and_reported():
    series = pd.Series(["Samhällsfunktion;Sjukhus", "Samhällsfunktion;Okänd", None])
    english, category, unmatched = translate_purpose_column(series)
    assert english.tolist() == ["Hospital", "Samhällsfunktion;Okänd", None]
    assert category.tolist() == ["Public", "Public", None]
    assert unmatched == {"Samhällsfunktion;Okänd": 1}


def test_collection_level_case_insensitive():
    english, unmatched = translate_collection_level_column(
        pd.Series(["Fasad", "Takkant", "Illustrativt läge", "Ospecificerad", "konstig"])
    )
    assert english.tolist() == ["Facade", "Roof edge", "Schematic/illustrative", "Unspecified", "konstig"]
    assert unmatched == {"konstig": 1}


def test_every_spec_type_has_purposes():
    for object_type in ["Bostad", "Industri", "Samhällsfunktion", "Verksamhet",
                        "Ekonomibyggnad", "Komplementbyggnad", "Övrig byggnad"]:
        assert object_type in PURPOSES


def test_pipeline_preprocess_on_raw_schema():
    raw = gpd.GeoDataFrame(
        {
            "objektidentitet": ["a", "b", "b"],
            "objekttyp": ["Samhällsfunktion", "Bostad", "Bostad"],
            "andamal1": ["Samhällsfunktion;Sjukhus", "Bostad;Flerfamiljshus", "Bostad;Flerfamiljshus"],
            "andamal2": [None, "Verksamhet;", "Verksamhet;"],
            "insamlingslage": ["Fasad", "Takkant", "Fasad"],
            "huvudbyggnad": ["Ja", "Nej", "Nej"],
            "byggnadsnamn1": ["Testsjukhuset", None, None],
            "husnummer": [1, 2, 2],
        },
        geometry=[box(0, 0, 20, 10), box(50, 0, 60, 10), box(60, 0, 65, 10)],
        crs="EPSG:3006",
    )
    pipeline = BuildingsPipeline(config={}, verbose=False)
    pipeline.data = raw
    report = pipeline.validate()
    assert report["repeated_object_ids"] == 1
    assert report["andamal1_type_mismatch_rows"] == 0
    pipeline.preprocess()
    out = pipeline.data
    assert out["primary_purpose"].tolist()[0] == "Samhällsfunktion;Sjukhus"  # Swedish kept
    assert out["primary_purpose_en"].tolist() == ["Hospital", "Apartment building", "Apartment building"]
    assert out["primary_purpose_category"].tolist() == ["Public", "Residential", "Residential"]
    assert out["secondary_purpose_en"].tolist()[1] == "Business (unspecified)"
    assert out["object_type_category"].tolist()[0] == "Public"
    assert out["collection_level_en"].tolist() == ["Facade", "Roof edge", "Facade"]
    assert out["main_building_flag"].tolist() == [True, False, False]
    assert out["main_building_flag_sv"].tolist() == ["Ja", "Nej", "Nej"]
    assert out["footprint_area_m2"].tolist() == [200, 100, 50]
    assert pipeline.preprocessing_report["unmatched_values"] == {}


def _buildings():
    ts = pd.Timestamp("2020-01-01", tz="UTC")
    rows = [
        # A: two touching pieces, different survey accuracy -> merged into one polygon
        ("A", ts, 1, 0.1, "Fasad", box(0, 0, 10, 10)),
        ("A", ts, 1, 0.5, "Takkant", box(10, 0, 15, 10)),
        # B: exact duplicate rows -> one removed
        ("B", ts, 1, 0.2, "Fasad", box(100, 0, 110, 10)),
        ("B", ts, 1, 0.2, "Fasad", box(100, 0, 110, 10)),
        # C: older and newer version -> newest kept
        ("C", ts, 1, 0.2, "Fasad", box(200, 0, 205, 5)),
        ("C", ts + pd.Timedelta(days=1), 2, 0.2, "Fasad", box(200, 0, 206, 6)),
        # D: separate pieces (not touching) -> MultiPolygon with 2 parts
        ("D", ts, 1, 0.3, "Fasad", box(300, 0, 304, 4)),
        ("D", ts, 1, 0.3, "Fasad", box(310, 0, 312, 2)),
        # E: single building
        ("E", ts, 1, 0.3, "Fasad", box(400, 0, 401, 1)),
    ]
    gdf = gpd.GeoDataFrame(
        rows,
        columns=["object_id", "version_valid_from", "object_version",
                 "position_uncertainty_plan_m", "collection_level", "geometry"],
        geometry="geometry",
        crs="EPSG:3006",
    )
    gdf["footprint_area_m2"] = gdf.geometry.area
    return gdf


def test_postprocess_merge():
    with tempfile.TemporaryDirectory() as tmp:
        clean, report = build_postprocess_snapshot(_buildings(), Path(tmp), strategy="merge")
        assert (Path(tmp) / "buildings_processed_postprocess.gpkg").exists()
        assert (Path(tmp) / "buildings_postprocess_parts.csv").exists()

    by_id = clean.set_index("object_id")
    assert list(clean["object_id"]) == ["A", "B", "C", "D", "E"]
    assert report["exact_duplicate_rows_removed"] == 1
    assert report["older_version_rows_removed"] == 1
    assert report["object_ids_with_multiple_pieces"] == 2

    # A: union of touching pieces, worst-case uncertainty, attributes from the largest piece
    assert by_id.loc["A", "geometry"].area == 150
    assert len(by_id.loc["A", "geometry"].geoms) == 1
    assert by_id.loc["A", "position_uncertainty_plan_m"] == 0.5
    assert by_id.loc["A", "collection_level"] == "Fasad"
    assert bool(by_id.loc["A", "collection_level_mixed"]) is True
    assert by_id.loc["A", "source_part_count"] == 2
    assert by_id.loc["A", "footprint_area_m2"] == 150

    assert by_id.loc["C", "object_version"] == 2
    assert isinstance(by_id.loc["D", "geometry"], MultiPolygon)
    assert len(by_id.loc["D", "geometry"].geoms) == 2
    assert by_id.loc["E", "source_part_count"] == 1

    # No footprint area lost except the exact duplicate and the old version
    assert report["output_footprint_area_m2"] == 150 + 100 + 36 + 20 + 1


def test_postprocess_keep_largest():
    with tempfile.TemporaryDirectory() as tmp:
        clean, report = build_postprocess_snapshot(_buildings(), Path(tmp), strategy="keep_largest")
    by_id = clean.set_index("object_id")
    assert by_id.loc["A", "geometry"].area == 100
    assert by_id.loc["A", "position_uncertainty_plan_m"] == 0.1
    assert clean["object_id"].is_unique


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
