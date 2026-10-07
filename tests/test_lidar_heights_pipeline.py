#!/usr/bin/env python3
"""
Tests for the building height pipeline (2018 surface model + 2010 laser data).

Synthetic data only; no project data is read. Two 100 x 100 m tiles per source,
the surface tiles packed in a zip like the Lantmäteriet delivery. The ground is
a slope (z = 10 + 0.05 x), so a wrong ground model shows up as a wrong height.
Run: python tests/test_lidar_heights_pipeline.py   (pytest also collects the test_* functions)
"""

import json
import sys
import tempfile
import zipfile
from pathlib import Path

import geopandas as gpd
import laspy
import numpy as np
import pandas as pd
from shapely.geometry import MultiPolygon, box

sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from pipelines.lidar_heights.config import HeightConfig, LiDARHeightPipelineConfig  # noqa: E402
from pipelines.lidar_heights.height_estimation import GroundSurface  # noqa: E402
from pipelines.lidar_heights.pipeline import LiDARHeightPipeline  # noqa: E402


def ground_z(x):
    return 10.0 + 0.05 * x


# name: (footprint, roof height 2010 or None, roof height 2018 or None, surface hole in 2018)
BUILDINGS = {
    "A_same":       (box(10, 10, 30, 30), 6.0, 6.0, False),     # unchanged
    "B_new_2012":   (box(40, 40, 60, 60), None, 9.0, False),    # built after the 2010 scan
    "C_demolished": (box(70, 10, 85, 25), 12.0, None, False),   # gone by 2018 (new one later)
    "D_hole_2018":  (box(10, 70, 25, 85), 7.0, 7.0, True),      # 2018 surface has a hole
    "E_tile_edge":  (box(90, 40, 110, 60), 8.0, 8.0, False),    # crosses the tile edge x = 100
    "G_rebuilt":    (box(120, 10, 140, 30), 5.0, 15.0, False),  # taller since 2010
    "H_low":        (box(150, 70, 153, 73), None, 1.0, False),  # 1 m high: below min visible
    "I_multi":      (MultiPolygon([box(160, 10, 170, 20), box(175, 10, 185, 20)]), 4.0, 4.0, False),
}
OUTSIDE = ("F_outside", box(500, 500, 510, 510))


def _roof(x, y, year):
    """Roof height above ground at points (0 where no building that year)."""
    h = np.zeros(len(x))
    for geom, h2010, h2018, _ in BUILDINGS.values():
        height = h2010 if year == 2010 else h2018
        if height is None:
            continue
        for part in getattr(geom, "geoms", [geom]):
            minx, miny, maxx, maxy = part.bounds
            h[(x >= minx) & (x <= maxx) & (y >= miny) & (y <= maxy)] = height
    return h


def _in_hole(x, y):
    m = np.zeros(len(x), dtype=bool)
    for geom, _, _, hole in BUILDINGS.values():
        if hole:
            minx, miny, maxx, maxy = geom.bounds
            m |= (x >= minx) & (x <= maxx) & (y >= miny) & (y <= maxy)
    return m


def _write(path, x, y, z, cls, point_format, version):
    las = laspy.create(point_format=point_format, file_version=version)
    las.x, las.y, las.z = x, y, z
    las.classification = cls
    las.write(path)


def _scene(tmp: Path):
    lidar = tmp / "laser"
    lidar.mkdir()
    surface_dir = tmp / "surface_tiles"
    surface_dir.mkdir()
    for i, x0 in enumerate((0, 100)):
        # 2010 laser: 1 m grid; ground (2) where no building stood, roof (1) on buildings
        gx, gy = np.meshgrid(np.arange(x0 + 0.25, x0 + 100, 1.0), np.arange(0.25, 100, 1.0))
        x, y = gx.ravel(), gy.ravel()
        h = _roof(x, y, 2010)
        cls = np.where(h > 0, 1, 2).astype(np.uint8)
        _write(lidar / f"10A007_tile{i}.laz", x, y, ground_z(x) + h, cls, 1, "1.2")
        # 2018 surface: 0.5 m grid, class 0, holes where matching failed
        sx, sy = np.meshgrid(np.arange(x0, x0 + 100, 0.5), np.arange(0.0, 100, 0.5))
        x, y = sx.ravel(), sy.ravel()
        keep = ~_in_hole(x, y)
        x, y = x[keep], y[keep]
        _write(surface_dir / f"y_tile{i}_18.laz", x, y, ground_z(x) + _roof(x, y, 2018),
               np.zeros(len(x), dtype=np.uint8), 0, "1.2")
        # Sidecars like Lantmäteriet's: strip dates for the laser, photo dates for the surface
        (lidar / f"10A007_tile{i}_strip.json").write_text(json.dumps(
            {"features": [{"properties": {"insamlingsdatum": f"2010-04-1{i + 1}"}}]}), encoding="utf-8")
        (surface_dir / f"y_tile{i}_18.json").write_text(json.dumps(
            {"properties": {"Datum_fran": "2018-04-13", "Datum_till": f"2018-04-2{i}"}}), encoding="utf-8")
    surface_zip = tmp / "ytmodell.zip"
    with zipfile.ZipFile(surface_zip, "w") as z:
        for p in list(surface_dir.glob("*.laz")) + list(surface_dir.glob("*.json")):
            z.write(p, p.name)

    names = list(BUILDINGS) + [OUTSIDE[0]]
    geoms = [MultiPolygon([g]) if g.geom_type == "Polygon" else g for g, *_ in BUILDINGS.values()]
    geoms.append(MultiPolygon([OUTSIDE[1]]))
    gpkg = tmp / "buildings_processed_postprocess.gpkg"
    gpd.GeoDataFrame({"object_id": names}, geometry=geoms, crs="EPSG:3006").to_file(
        gpkg, layer="buildings_postprocess")
    return gpkg, lidar, surface_zip


def _run(tmp: Path):
    gpkg, lidar, surface_zip = _scene(tmp)
    out = tmp / "out"
    config = LiDARHeightPipelineConfig(
        input_buildings_path=gpkg, output_directory=out, lidar_directory=lidar,
        surface_path=surface_zip, input_layer="buildings_postprocess", work_cell_m=60.0,
    )
    report = LiDARHeightPipeline(config).run(out)
    return report, out


def test_ground_surface_follows_slope():
    gx, gy = np.meshgrid(np.arange(0, 50, 2.0), np.arange(0, 50, 2.0))
    g = GroundSurface(gx.ravel(), gy.ravel(), ground_z(gx.ravel()))
    q = np.array([3.3, 27.1, 47.0])  # grid covers 0..48
    assert np.allclose(g(q, np.array([5.0, 40.2, 12.0])), ground_z(q))
    # outside the hull: value of the nearest ground point (48, 48)
    assert np.allclose(g(np.array([80.0]), np.array([80.0])), ground_z(48.0))


def test_pipeline_heights_and_reasons():
    with tempfile.TemporaryDirectory() as tmp:
        report, out = _run(Path(tmp))
        assert report["status"] == "success", report
        gdf = gpd.read_file(out / "buildings_lidar_added.gpkg").set_index("object_id")
        summary = json.loads((out / "lidar_heights_summary.json").read_text(encoding="utf-8"))
        missing = pd.read_csv(out / "buildings_without_height.csv")

    assert len(gdf) == 9 and gdf.index.is_unique

    def row(oid):
        return gdf.loc[oid]

    # Heights are exact on a sloped ground: the ground model works per point
    for oid, height, source in [
        ("A_same", 6.0, "surface"),
        ("B_new_2012", 9.0, "surface"),
        ("D_hole_2018", 7.0, "lidar"),
        ("E_tile_edge", 8.0, "surface"),
        ("G_rebuilt", 15.0, "surface"),
        ("I_multi", 4.0, "surface"),
    ]:
        r = row(oid)
        assert r["has_height"], oid
        assert abs(r["height_m"] - height) < 0.01, (oid, r["height_m"])
        assert r["height_source"] == source, (oid, r["height_source"])
        assert abs(r["roof_z"] - r["ground_z"] - r["height_m"]) < 0.01

    assert row("A_same")["height_quality"] == "high"          # both sources agree
    assert row("B_new_2012")["height_quality"] == "medium"    # 2018 only
    assert row("D_hole_2018")["height_quality"] == "low"      # 2010 fallback
    assert bool(row("G_rebuilt")["height_change_flag"])
    assert not bool(row("A_same")["height_change_flag"])

    # Tile edge: points from both tiles (shrunk 19 x 19 m at 0.5 m spacing ~ 1,444)
    assert row("E_tile_edge")["surface_point_count"] > 1300

    # No height: 0 m, flagged, with the reason
    for oid, reason in [("C_demolished", "surface_shows_ground"),
                        ("H_low", "surface_shows_ground"),
                        ("F_outside", "no_tile")]:
        r = row(oid)
        assert not r["has_height"], oid
        assert r["height_m"] == 0.0
        assert r["no_height_reason_code"] == reason, (oid, r["no_height_reason_code"])
    # Buildings with a height have no reason (null, not the text "<NA>")
    assert gdf.loc[gdf["has_height"].astype(bool), "no_height_reason_code"].isna().all()
    # The demolished building's 2010 height is kept for inspection, not used
    assert abs(row("C_demolished")["height_lidar_m"] - 12.0) < 0.01

    assert summary["buildings_with_height"] == 6
    assert summary["buildings_without_height"] == 3
    assert summary["without_height_by_reason"] == {"surface_shows_ground": 2, "no_tile": 1}
    assert summary["by_source"] == {"surface": 5, "none": 3, "lidar": 1}
    assert sorted(missing["object_id"]) == ["C_demolished", "F_outside", "H_low"]

    # Capture dates come from the sidecar files, not from code
    assert summary["sources"]["lidar"]["capture_dates"]["from"] == "2010-04-11"
    assert summary["sources"]["lidar"]["capture_dates"]["to"] == "2010-04-12"
    assert summary["sources"]["surface"]["capture_dates"] == {"from": "2018-04-13", "to": "2018-04-21", "sidecar_files": 2}


def test_lidar_only_mode():
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        gpkg, lidar, _ = _scene(tmp)
        config = LiDARHeightPipelineConfig(
            input_buildings_path=gpkg, output_directory=tmp / "out", lidar_directory=lidar,
            surface_path=None, work_cell_m=60.0,
        )
        report = LiDARHeightPipeline(config).run(tmp / "out")
        gdf = gpd.read_file(tmp / "out" / "buildings_lidar_added.gpkg").set_index("object_id")
    assert report["status"] == "success"
    assert abs(gdf.loc["C_demolished", "height_m"] - 12.0) < 0.01  # 2010 is all it knows
    assert gdf.loc["B_new_2012", "no_height_reason"] == "no_points"


def test_repeated_object_ids_are_rejected():
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        gpkg, lidar, surface_zip = _scene(tmp)
        gdf = gpd.read_file(gpkg)
        pd.concat([gdf, gdf.iloc[[0]]]).to_file(gpkg, layer="buildings_postprocess")
        config = LiDARHeightPipelineConfig(
            input_buildings_path=gpkg, output_directory=tmp / "out",
            lidar_directory=lidar, surface_path=surface_zip,
        )
        report = LiDARHeightPipeline(config).run(tmp / "out")
    assert report["status"] == "failed"
    assert "repeated object_id" in report["error"]


def test_config_defaults():
    hc = HeightConfig()
    assert hc.percentile == 95.0
    assert hc.fallback_height_m == 0.0


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
