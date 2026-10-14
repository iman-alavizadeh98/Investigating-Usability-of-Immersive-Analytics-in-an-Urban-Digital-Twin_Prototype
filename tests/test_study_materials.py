#!/usr/bin/env python3
"""
Tests for the 2D study materials (Src/pipelines/data_report/study.py): the
scenario unit filter that matches the 3D views' unitFilter.

Synthetic package; no project data is read.
Run: python tests/test_study_materials.py   (pytest also collects the test_* functions)
"""

import json
import sys
import tempfile
from pathlib import Path
from types import SimpleNamespace

import geopandas as gpd
from shapely.geometry import box

sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from pipelines.data_report.study import unit_filter_mask  # noqa: E402


def test_unit_filter_keeps_250m_cells_with_enough_residents():
    with tempfile.TemporaryDirectory() as tmp:
        pkg = Path(tmp)
        ids = ["ruta:250_a", "ruta:250_b", "ruta:1000_c", "ruta:250_d"]
        layer = pkg / "data_layers" / "ruta_population"
        layer.mkdir(parents=True)
        (layer / "values.json").write_text(json.dumps({
            "unitIds": ids,
            "columns": [{"variableId": "population", "integerValues": [120, 10, 500, 0], "valid": [True, True, True, False]}],
        }), encoding="utf-8")
        units = gpd.GeoDataFrame(index=ids, geometry=[box(0, 0, 1, 1)] * 4, crs="EPSG:3006")
        cfg = SimpleNamespace(package_dir=pkg)

        keep = unit_filter_mask(cfg, units, {"unitPrefix": "ruta:250_", "dataLayerId": "ruta_population",
                                             "variableId": "population", "minimum": 50})
        assert keep.to_dict() == {"ruta:250_a": True, "ruta:250_b": False, "ruta:1000_c": False, "ruta:250_d": False}
        assert unit_filter_mask(cfg, units, None).all()


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
