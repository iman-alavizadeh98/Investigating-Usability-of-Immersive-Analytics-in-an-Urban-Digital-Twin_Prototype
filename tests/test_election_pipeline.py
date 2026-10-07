#!/usr/bin/env python3
"""
Tests for the election preprocess pipeline.

Synthetic workbook + boundaries for an invented municipality "0199" (a leading
zero on purpose: pandas reads number-like Excel text as numbers unless told
otherwise). No project data is read.
Run: python tests/test_election_pipeline.py   (pytest also collects the test_* functions)
"""

import json
import sys
import tempfile
from pathlib import Path

import geopandas as gpd
import pandas as pd
from shapely.geometry import box

sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from pipelines.election import ElectionPipeline  # noqa: E402
from pipelines.election.loader import load_election, parse_election  # noqa: E402
from pipelines.election.validator import validate_election  # noqa: E402

MUNI, OTHER = "0199", "1283"
E0, N0 = 500000, 6500000
# district code → (name, eligible, {party: votes}, invalid blank)
DISTRICTS = {
    "019900": ("Uppsamlingsdistrikt 00", 0, {"Moderaterna": 10, "Lokalpartiet": 2}, 1),
    "01990101": ("Norr", 100, {"Moderaterna": 30, "Arbetarepartiet-Socialdemokraterna": 40, "Lokalpartiet": 5}, 2),
    "01990102": ("Söder", 200, {"Moderaterna": 60, "Arbetarepartiet-Socialdemokraterna": 70, "Lokalpartiet": 0}, 3),
}


def _common(code, name, muni=MUNI):
    return {"Valtyp": "Kommun", "Kommun": "Testby", "Kommunvalkrets": None, "Valdistrikt": name,
            "Valdistriktskod": code, "Kommunkod": muni, "Valkretskod": muni + "00", "Länskod": muni[:2]}


def _workbook(path: Path, break_turnout=False):
    long_rows, wide_rows, share_rows, turnout_rows = [], [], [], []
    for code, (name, eligible, votes, blank) in DISTRICTS.items():
        valid = sum(votes.values())
        cast = valid + blank
        base = _common(code, name)
        for party, n in votes.items():
            long_rows.append({**base, "Parti/kategori": party, "Röster": n, "Summa giltiga röster": valid, "Röstberättigade": eligible})
        for cat, n in (("Summa giltiga röster", valid), ("Ogiltiga röster - blanka", blank), ("Valdeltagande", cast), ("Röstberättigade", eligible)):
            long_rows.append({**base, "Parti/kategori": cat, "Röster": n, "Summa giltiga röster": valid, "Röstberättigade": eligible})
        wide_rows.append({**base, **votes, "Nationalpartiet": None})            # a party that did not stand here
        share_rows.append({**base, **{p: n / valid for p, n in votes.items()}, "Nationalpartiet": 0.0})
        turnout = cast / eligible if eligible else None
        if break_turnout and code == "01990102":
            turnout = 0.5
        turnout_rows.append({**base, "Röster": cast, "Röstberättigade": eligible, "Valdeltagande (%)": turnout})
    # Another municipality, to be filtered out.
    other = _common(OTHER + "0101", "Annanstans", OTHER)
    long_rows.append({**other, "Parti/kategori": "Moderaterna", "Röster": 999, "Summa giltiga röster": 999, "Röstberättigade": 1500})
    wide_rows.append({**other, "Moderaterna": 999, "Annatparti": 5})
    muni_row = {
        "Valtyp": "Kommun", "Kommun": "Testby", "Kommunkod": MUNI, "Länskod": MUNI[:2],
        "Moderaterna": 100, "Arbetarepartiet-Socialdemokraterna": 110, "Lokalpartiet": 7,
    }
    with pd.ExcelWriter(path) as w:
        pd.DataFrame({"Röster i val till kommunfullmäktige 2030": ["Filen innehåller ...", "Flikar"]}).to_excel(w, sheet_name="Information", index=False)
        pd.DataFrame(long_rows).to_excel(w, sheet_name="roster_KF", index=False)
        pd.DataFrame([muni_row]).to_excel(w, sheet_name="Antal_kommun", index=False)
        pd.DataFrame(wide_rows).to_excel(w, sheet_name="Antal_distrikt", index=False)
        pd.DataFrame(share_rows).to_excel(w, sheet_name="Andel_distrikt", index=False)
        pd.DataFrame(turnout_rows).to_excel(w, sheet_name="Valdeltagande_distrikt", index=False)


def _boundaries(path: Path, drop_one=False):
    rows = [("01990101", "Norr", box(E0, N0 + 1000, E0 + 1000, N0 + 2000)),
            ("01990102", "Söder", box(E0, N0, E0 + 1000, N0 + 1000)),
            (OTHER + "0101", "Annanstans", box(E0 + 5000, N0, E0 + 6000, N0 + 1000))]
    if drop_one:
        rows = rows[1:]
    gpd.GeoDataFrame({
        "Valdistriktskod": [r[0] for r in rows], "Valdistriktsnamn": [r[1] for r in rows],
        "Kommunkod": [r[0][:4] for r in rows], "Kommun": ["Testby" if r[0].startswith(MUNI) else "Annan" for r in rows],
    }, geometry=[r[2] for r in rows], crs="EPSG:3006").to_file(path, driver="GeoJSON")


def _folder(tmp, **kw) -> Path:
    folder = Path(tmp) / "election"
    folder.mkdir()
    _workbook(folder / "roster-per-distrikt-kommunvalen-2030.xlsx", break_turnout=kw.get("break_turnout", False))
    _boundaries(folder / "valdistrikt-2030.geojson", drop_one=kw.get("drop_one", False))
    return folder


def test_parse_election():
    assert parse_election("roster-per-distrikt-slutligt-antal-roster-kommunvalen-2026.xlsx") == ("kommunval", 2026)
    assert parse_election("riksdagsvalet-2022.xlsx") == ("riksdagsval", 2022)
    assert parse_election("results.xlsx") == (None, None)


def test_load_keeps_leading_zeros_and_filters_municipality():
    with tempfile.TemporaryDirectory() as tmp:
        src = load_election(_folder(tmp), MUNI)
        assert (src.election_type, src.election_year) == ("kommunval", 2030)
        long = src.sheets["roster_KF"]
        assert set(long["Kommunkod"]) == {MUNI}                       # "0199", not 199
        assert "01990101" in set(long["Valdistriktskod"])
        assert src.warnings == []
        assert len(src.districts) == 2 and set(src.districts["Kommunkod"]) == {MUNI}
        wide = src.sheets["Antal_distrikt"]
        assert "Nationalpartiet" not in wide.columns and "Annatparti" not in wide.columns
        assert src.dropped_party_columns["Antal_distrikt"] == 2
        assert src.information[0] == "Röster i val till kommunfullmäktige 2030"
        try:
            load_election(Path(tmp) / "election", "9999")
            raise AssertionError("unknown municipality must raise")
        except ValueError:
            pass


def test_checks_pass_on_consistent_data():
    with tempfile.TemporaryDirectory() as tmp:
        report = validate_election(load_election(_folder(tmp), MUNI))
        failed = [c for c in report["checks"] if c["failed"]]
        assert failed == [], failed
        assert report["issues"] == [], report["issues"]
        assert report["municipality_totals"] == {"votes_cast": 223, "valid_votes": 217, "invalid_votes": 6, "eligible_voters": 300}
        assert [c["code"] for c in report["collection_districts"]] == ["019900"]
        assert report["join"]["matched"] == 2 and report["join"]["collection_districts_without_area"] == ["019900"]
        parties = {p["party_sv"]: p for p in report["parties"]}
        assert parties["Moderaterna"]["votes"] == 100 and parties["Moderaterna"]["abbreviation"] == "M"
        assert parties["Lokalpartiet"]["districts_with_votes"] == 2
        assert report["turnout"]["stored_as"] == "fraction 0-1"
        assert report["districts"]["overlapping_pairs"] == 0


def test_checks_catch_broken_turnout_and_missing_boundary():
    with tempfile.TemporaryDirectory() as tmp:
        report = validate_election(load_election(_folder(tmp, break_turnout=True, drop_one=True), MUNI))
        turnout = next(c for c in report["checks"] if "votes / eligible = turnout" in c["check"])
        assert turnout["failed"] == 1
        assert report["join"]["results_without_boundary"] == ["01990101"]
        assert any("no boundary" in i for i in report["issues"])


def test_pipeline_writes_outputs():
    with tempfile.TemporaryDirectory() as tmp:
        out = Path(tmp) / "out"
        pipeline = ElectionPipeline(config={"input_dir": str(_folder(tmp)), "municipality": MUNI}, verbose=False)
        result = pipeline.run(output_dir=out)
        assert result["status"] == "success", result
        index = (out / "index.html").read_text(encoding="utf-8")
        assert "Social Democrats" in index and "Uppsamlingsdistrikt 00" in index
        for frame in ("roster_KF", "Antal_distrikt", "Andel_distrikt", "Valdeltagande_distrikt", "Antal_kommun", "districts"):
            assert (out / "tables" / f"{frame}.html").exists(), frame
            assert (out / "profiles" / f"{frame}_profile.html").exists(), frame
        report = json.loads((out / "election_preprocess_report.json").read_text(encoding="utf-8"))
        assert report["inputs"]["municipality_code"] == MUNI
        # (parties + 4 summary rows) per district: 6 + 7 + 7, plus 1 row of the other municipality
        assert report["inputs"]["rows_before_filter"]["roster_KF"] == 21


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
