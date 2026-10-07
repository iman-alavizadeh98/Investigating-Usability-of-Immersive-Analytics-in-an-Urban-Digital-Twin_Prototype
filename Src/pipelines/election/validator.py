"""
Checks and basic facts for one municipality's election data.

Reports, never stops the run. Results go into `issues` (needs a decision) or
`notes` (known property), plus named check results:

Results workbook
  - district codes: format, repeats, collection districts (Uppsamlingsdistrikt);
  - long sheet: party votes add up to valid votes; valid + invalid = votes cast;
    summary rows agree with the summary columns;
  - turnout sheet: votes cast / eligible voters reproduces the turnout column;
  - wide count sheet agrees with the long sheet, district by district;
  - share sheet = counts / valid votes;
  - municipality totals = sum over districts.
Boundaries
  - CRS, null / empty / invalid geometry, repeated codes, overlaps, area.
Join
  - result districts without a boundary and boundaries without results.

Every check runs only if the sheets it needs are present.
"""

from __future__ import annotations

import logging
from typing import Dict, List, Optional

import numpy as np
import pandas as pd

from .config import (
    COLLECTION_DISTRICT_PREFIX,
    DISTRICT_CODE_COLUMN,
    DISTRICT_CODE_PATTERN,
    DISTRICT_NAME_COLUMN,
    EXPECTED_CRS,
    FIELDS,
    INVALID_PREFIX,
    NATIONAL_PARTIES,
    SHEETS,
    SUMMARY_CATEGORIES,
    WIDE_FIXED_COLUMNS,
)
from .loader import ElectionSource

logger = logging.getLogger(__name__)

MAX_EXAMPLES = 5
#: Rounding tolerance when comparing shares / turnout fractions.
FRACTION_TOLERANCE = 1e-6


def _sheets_of_kind(src: ElectionSource, kind: str, level: Optional[str] = None) -> Dict[str, pd.DataFrame]:
    out = {}
    for name, df in src.sheets.items():
        spec = SHEETS.get(name)
        if spec and spec.kind == kind and (level is None or spec.level == level):
            out[name] = df
    return out


def party_columns(df: pd.DataFrame) -> List[str]:
    """Wide-sheet columns that are parties (everything not in config.FIELDS)."""
    return [c for c in df.columns if c not in WIDE_FIXED_COLUMNS]


def is_collection(df: pd.DataFrame) -> pd.Series:
    """Collection districts: name starts with 'Uppsamlingsdistrikt' or the code is not 8 digits."""
    named = df[DISTRICT_NAME_COLUMN].astype("string").str.startswith(COLLECTION_DISTRICT_PREFIX).fillna(False) \
        if DISTRICT_NAME_COLUMN in df.columns else pd.Series(False, index=df.index)
    bad_code = ~df[DISTRICT_CODE_COLUMN].astype("string").str.fullmatch(DISTRICT_CODE_PATTERN).fillna(False)
    return (named | bad_code).astype(bool)


def long_by_district(long: pd.DataFrame) -> pd.DataFrame:
    """Long sheet → one row per district: party sum, summary rows, summary columns."""
    category = long["Parti/kategori"].astype("string")
    is_summary = category.isin(list(SUMMARY_CATEGORIES)) | category.str.startswith(INVALID_PREFIX).fillna(False)
    code = long[DISTRICT_CODE_COLUMN]
    out = pd.DataFrame({
        "party_votes": long[~is_summary].groupby(code[~is_summary])["Röster"].sum(),
        "invalid_votes": long[category.str.startswith(INVALID_PREFIX).fillna(False)].groupby(code)["Röster"].sum(),
    })
    for cat, alias in (("Summa giltiga röster", "valid_row"), ("Valdeltagande", "cast_row"), ("Röstberättigade", "eligible_row")):
        rows = long[category == cat]
        out[alias] = rows.groupby(rows[DISTRICT_CODE_COLUMN])["Röster"].sum()
    out["valid_column"] = long.groupby(code)["Summa giltiga röster"].first()
    out["eligible_column"] = long.groupby(code)["Röstberättigade"].first()
    return out.fillna(0)


def _check(results: list, name: str, failed: int, checked: int, detail: str = "") -> None:
    results.append({"check": name, "checked": int(checked), "failed": int(failed),
                    "status": "ok" if failed == 0 else "MISMATCH", "detail": detail})


def validate_results(src: ElectionSource, report: dict) -> None:
    checks = report["checks"]

    # District codes per district-level sheet
    for name, df in src.sheets.items():
        spec = SHEETS.get(name)
        if not spec or spec.level != "district" or DISTRICT_CODE_COLUMN not in df.columns:
            continue
        per_district = df if spec.kind != "long" else df.drop_duplicates(DISTRICT_CODE_COLUMN)
        repeats = int(per_district[DISTRICT_CODE_COLUMN].duplicated().sum())
        coll = is_collection(per_district)
        report["sheets"][name] = {
            "rows": int(len(df)), "districts": int(per_district[DISTRICT_CODE_COLUMN].nunique()),
            "collection_districts": int(coll.sum()), "repeated_codes": repeats,
        }
        if repeats:
            report["issues"].append(f"{name}: {repeats} repeated district codes")

    long_sheets = _sheets_of_kind(src, "long")
    long = next(iter(long_sheets.values()), None)
    if long is None or long.empty:
        report["notes"].append("No long-format sheet; long-sheet checks skipped")
    else:
        d = long_by_district(long)
        _check(checks, "party votes = valid votes (long sheet)", (d["party_votes"] != d["valid_row"]).sum(), len(d))
        _check(checks, "valid-votes row = valid-votes column", (d["valid_row"] != d["valid_column"]).sum(), len(d))
        _check(checks, "valid + invalid = votes cast", (d["valid_row"] + d["invalid_votes"] != d["cast_row"]).sum(), len(d))
        _check(checks, "eligible-voters row = column", (d["eligible_row"] != d["eligible_column"]).sum(), len(d))
        report["municipality_totals"] = {
            "votes_cast": int(d["cast_row"].sum()),
            "valid_votes": int(d["valid_row"].sum()),
            "invalid_votes": int(d["invalid_votes"].sum()),
            "eligible_voters": int(d["eligible_row"].sum()),
        }

        # Collection districts
        per_district = long.drop_duplicates(DISTRICT_CODE_COLUMN).set_index(DISTRICT_CODE_COLUMN)
        coll_codes = per_district.index[is_collection(per_district.reset_index()).to_numpy()]
        report["collection_districts"] = [
            {"code": c, "name": str(per_district.at[c, DISTRICT_NAME_COLUMN]),
             "votes_cast": int(d.at[c, "cast_row"]), "valid_votes": int(d.at[c, "valid_row"]),
             "eligible_voters": int(d.at[c, "eligible_row"]),
             "share_of_votes_cast": round(float(d.at[c, "cast_row"] / max(1, d["cast_row"].sum())), 4)}
            for c in coll_codes
        ]
        for row in report["collection_districts"]:
            report["notes"].append(
                f"Collection district {row['code']} ({row['name']}) holds {row['votes_cast']:,} votes cast "
                f"({row['share_of_votes_cast']:.1%} of the municipality) but has no area and 0 eligible voters"
            )

        # Parties
        category = long["Parti/kategori"].astype("string")
        party_rows = long[~(category.isin(list(SUMMARY_CATEGORIES)) | category.str.startswith(INVALID_PREFIX).fillna(False))]
        unknown_invalid = sorted(set(category[category.str.startswith(INVALID_PREFIX).fillna(False)]) - set(SUMMARY_CATEGORIES))
        if unknown_invalid:
            report["issues"].append(f"Invalid-vote categories not in config.SUMMARY_CATEGORIES: {unknown_invalid}")
        valid_total = max(1, report["municipality_totals"]["valid_votes"])
        parties = party_rows.groupby("Parti/kategori").agg(
            votes=("Röster", "sum"), districts_with_votes=("Röster", lambda s: int((s > 0).sum())),
        ).sort_values("votes", ascending=False)
        report["parties"] = [
            {"party_sv": p, "abbreviation": NATIONAL_PARTIES.get(p, ("", ""))[0],
             "party_en": NATIONAL_PARTIES.get(p, ("", "(local or small party; name not translated)"))[1],
             "votes": int(r.votes), "share_of_valid_votes": round(float(r.votes / valid_total), 4),
             "districts_with_votes": int(r.districts_with_votes)}
            for p, r in parties.iterrows()
        ]

        # Wide counts vs. long
        for name, wide in _sheets_of_kind(src, "counts_wide", "district").items():
            wide_long = wide.melt(id_vars=[DISTRICT_CODE_COLUMN], value_vars=party_columns(wide),
                                  var_name="Parti/kategori", value_name="wide_votes")
            merged = wide_long.merge(party_rows[[DISTRICT_CODE_COLUMN, "Parti/kategori", "Röster"]],
                                     on=[DISTRICT_CODE_COLUMN, "Parti/kategori"], how="outer")
            diff = merged["wide_votes"].fillna(0) != merged["Röster"].fillna(0)
            _check(checks, f"{name} = long sheet (district × party)", diff.sum(), len(merged))

    # Shares = counts / valid votes
    counts = _sheets_of_kind(src, "counts_wide", "district")
    for name, shares in _sheets_of_kind(src, "shares_wide", "district").items():
        if not counts or long is None:
            break
        count = next(iter(counts.values())).set_index(DISTRICT_CODE_COLUMN)
        share = shares.set_index(DISTRICT_CODE_COLUMN)
        valid = long_by_district(long)["valid_row"]
        cols = [c for c in party_columns(shares) if c in count.columns]
        expected = count[cols].fillna(0).div(valid.reindex(count.index).replace(0, np.nan), axis=0)
        delta = (share[cols].reindex(expected.index).fillna(0) - expected.fillna(0)).abs()
        bad = int((delta > FRACTION_TOLERANCE).to_numpy().sum())
        _check(checks, f"{name} = counts / valid votes", bad, delta.size, f"max difference {float(delta.max().max()):.2e}")

    # Turnout
    for name, t in _sheets_of_kind(src, "turnout", "district").items():
        col = "Valdeltagande (%)"
        if col not in t.columns:
            continue
        has_voters = t["Röstberättigade"] > 0
        recomputed = t.loc[has_voters, "Röster"] / t.loc[has_voters, "Röstberättigade"]
        delta = (recomputed - t.loc[has_voters, col]).abs()
        _check(checks, f"{name}: votes / eligible = turnout", (delta > FRACTION_TOLERANCE).sum(), len(delta),
               f"max difference {float(delta.max()) if len(delta) else 0:.2e}")
        values = t.loc[has_voters, col]
        report["turnout"] = {
            "min": float(values.min()), "median": float(values.median()), "max": float(values.max()),
            "districts": int(has_voters.sum()), "stored_as": "fraction 0-1" if values.max() <= 1 else "percent",
        }
        if long is not None:
            cast = long_by_district(long)["cast_row"]
            mismatch = (t.set_index(DISTRICT_CODE_COLUMN)["Röster"] != cast.reindex(t[DISTRICT_CODE_COLUMN]).to_numpy()).sum()
            _check(checks, f"{name}: votes = votes-cast row of the long sheet", mismatch, len(t))

    # Municipality totals = sum of districts
    for name, m in _sheets_of_kind(src, "counts_wide", "municipality").items():
        if not counts or m.empty:
            continue
        district = next(iter(counts.values()))
        cols = [c for c in party_columns(m) if c in district.columns]
        diff = (m[cols].fillna(0).sum() - district[cols].fillna(0).sum()).abs()
        _check(checks, f"{name} = sum over districts", int((diff > 0).sum()), len(cols))


def validate_districts(src: ElectionSource, report: dict) -> None:
    g = src.districts
    geom = g.geometry
    info = {
        "rows": int(len(g)),
        "crs": g.crs.to_string() if g.crs else None,
        "geometry_types": {str(k): int(v) for k, v in geom.geom_type.value_counts().items()},
        "null_geometries": int(geom.isna().sum()),
        "empty_geometries": int(geom.is_empty.sum()),
        "invalid_geometries": int((~geom.is_valid & geom.notna()).sum()),
        "repeated_codes": int(g[DISTRICT_CODE_COLUMN].duplicated().sum()) if DISTRICT_CODE_COLUMN in g else None,
        "bad_codes": int((~g[DISTRICT_CODE_COLUMN].astype("string").str.fullmatch(DISTRICT_CODE_PATTERN).fillna(False)).sum())
        if DISTRICT_CODE_COLUMN in g else None,
        "total_area_km2": round(float(geom.area.sum()) / 1e6, 2),
        "district_area_km2": {k: round(float(v) / 1e6, 3) for k, v in geom.area.describe()[["min", "50%", "max"]].items()},
        "bounds": [round(float(v), 1) for v in g.total_bounds],
        "unknown_columns": [c for c in g.columns if c != geom.name and c not in FIELDS],
    }
    report["districts"] = info
    if info["crs"] != EXPECTED_CRS:
        report["issues"].append(f"Boundaries: CRS is {info['crs']}, expected {EXPECTED_CRS}")
    for key in ("null_geometries", "empty_geometries", "invalid_geometries", "repeated_codes", "bad_codes"):
        if info[key]:
            report["issues"].append(f"Boundaries: {info[key]} {key.replace('_', ' ')}")
    if info["unknown_columns"]:
        report["issues"].append(f"Boundaries: columns not in config.FIELDS: {info['unknown_columns']}")

    # Overlaps between districts (they should tile the municipality).
    valid = g[geom.notna() & ~geom.is_empty]
    pairs = valid.sjoin(valid, predicate="overlaps", how="inner")
    pairs = pairs[pairs.index < pairs["index_right"]]
    overlap_m2 = float(sum(
        valid.geometry.loc[i].intersection(valid.geometry.loc[j]).area
        for i, j in zip(pairs.index, pairs["index_right"])
    ))
    info["overlapping_pairs"] = int(len(pairs))
    info["overlap_area_m2"] = round(overlap_m2, 1)
    if overlap_m2 > 1.0:
        report["notes"].append(f"Boundaries: {len(pairs)} district pairs overlap, {overlap_m2:,.0f} m² in total")


def validate_join(src: ElectionSource, report: dict) -> None:
    district_sheets = [df for name, df in src.sheets.items()
                       if SHEETS.get(name) and SHEETS[name].level == "district" and DISTRICT_CODE_COLUMN in df.columns]
    if not district_sheets or DISTRICT_CODE_COLUMN not in src.districts.columns:
        report["notes"].append("Join check skipped: no district codes on one side")
        return
    results = pd.concat([df[[DISTRICT_CODE_COLUMN, DISTRICT_NAME_COLUMN]] for df in district_sheets]).drop_duplicates(DISTRICT_CODE_COLUMN)
    coll = is_collection(results)
    result_codes = set(results.loc[~coll, DISTRICT_CODE_COLUMN])
    geo_codes = set(src.districts[DISTRICT_CODE_COLUMN])
    report["join"] = {
        "result_districts": len(result_codes),
        "collection_districts_without_area": sorted(results.loc[coll, DISTRICT_CODE_COLUMN]),
        "boundary_districts": len(geo_codes),
        "matched": len(result_codes & geo_codes),
        "results_without_boundary": sorted(result_codes - geo_codes),
        "boundaries_without_results": sorted(geo_codes - result_codes),
    }
    if report["join"]["results_without_boundary"]:
        report["issues"].append(f"{len(report['join']['results_without_boundary'])} result districts have no boundary: "
                                f"{report['join']['results_without_boundary'][:MAX_EXAMPLES]}")
    if report["join"]["boundaries_without_results"]:
        report["issues"].append(f"{len(report['join']['boundaries_without_results'])} boundaries have no results: "
                                f"{report['join']['boundaries_without_results'][:MAX_EXAMPLES]}")
    # Names should agree for matched codes.
    if "Valdistriktsnamn" in src.districts.columns:
        names = results.set_index(DISTRICT_CODE_COLUMN)[DISTRICT_NAME_COLUMN]
        geo_names = src.districts.set_index(DISTRICT_CODE_COLUMN)["Valdistriktsnamn"]
        common = sorted(result_codes & geo_codes)
        differ = [c for c in common if str(names.get(c)).strip() != str(geo_names.get(c)).strip()]
        report["join"]["name_differences"] = [{"code": c, "results": str(names[c]), "boundaries": str(geo_names[c])}
                                              for c in differ]
        if differ:
            report["notes"].append(f"{len(differ)} matched districts are named differently in results and boundaries")


def validate_election(src: ElectionSource) -> dict:
    """Run every check; returns the report with issues, notes and check results."""
    report: dict = {"issues": list(src.warnings), "notes": [], "checks": [], "sheets": {}}
    validate_results(src, report)
    validate_districts(src, report)
    validate_join(src, report)
    for c in report["checks"]:
        if c["failed"]:
            report["issues"].append(f"Check failed: {c['check']} ({c['failed']} of {c['checked']}) {c['detail']}".strip())
    report["issue_count"] = len(report["issues"])
    for issue in report["issues"][len(src.warnings):]:
        logger.warning(f"  - {issue}")
    logger.info(f"  - {len(report['checks'])} checks run, {report['issue_count']} issue(s)")
    return report
