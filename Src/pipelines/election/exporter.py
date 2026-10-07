"""
Write the election preprocess outputs: HTML views, profiles, index, JSON report.

Output folder layout:
    index.html                          start here: parties, checks, collection districts, join, field dictionary
    tables/<frame>.html                 each workbook sheet (municipality only) and the district boundaries
    profiles/<frame>_profile.html/.md   DataFrameProfiler report per frame
    election_preprocess_report.json     inputs, filters, every check, preprocessing steps
"""

from __future__ import annotations

import html
import json
import logging
from pathlib import Path
from typing import Dict

import pandas as pd

from pipelines.scb.exporter import json_default
from utils.data_profiler import DataFrameProfiler
from utils.dataframe_html import page_html, save_dataframe_html, searchable_table_html, table_html

from .config import DATASET_METADATA, ELECTION_TYPES_EN, FIELDS, NATIONAL_PARTIES, SHEETS, SUMMARY_CATEGORIES
from .loader import ElectionSource
from .validator import party_columns

logger = logging.getLogger(__name__)

REPORT_FILE = "election_preprocess_report.json"
DISTRICTS_FRAME = "districts"


def column_notes(df: pd.DataFrame) -> Dict[str, str]:
    """Column → meaning for fields and national parties (local parties keep their name only)."""
    notes = {}
    for col in df.columns:
        if col in FIELDS:
            info = FIELDS[col]
            notes[col] = f"{info.alias_en}: {info.description_en}" + (f" ({info.unit})" if info.unit else "")
        elif col in NATIONAL_PARTIES:
            abbr, en = NATIONAL_PARTIES[col]
            notes[col] = f"Party {abbr}: {en}"
    return notes


def _frames(src: ElectionSource) -> Dict[str, pd.DataFrame]:
    frames = dict(src.sheets)
    frames[DISTRICTS_FRAME] = src.districts
    return frames


def _items(lines) -> str:
    return "".join(f"<li>{html.escape(str(x))}</li>" for x in lines)


def _index_page(src: ElectionSource, validation: dict, run_info: dict) -> str:
    frames = _frames(src)
    election = ELECTION_TYPES_EN.get(src.election_type or "", "unknown election type")

    overview = []
    for name, df in frames.items():
        spec = SHEETS.get(name)
        overview.append({
            "Frame": f'<a href="tables/{html.escape(name)}.html">{html.escape(name)}</a>',
            "Profile": f'<a href="profiles/{html.escape(name)}_profile.html">profile</a>',
            "Content": spec.description_en if spec else ("Voting-district boundaries" if name == DISTRICTS_FRAME else "(unknown sheet)"),
            "Level": spec.level if spec else ("district" if name == DISTRICTS_FRAME else None),
            "Rows (municipality)": len(df),
            "Rows (whole file)": src.national_rows.get(name if name != DISTRICTS_FRAME else "(boundaries)"),
            "Columns": len(df.columns),
            "Empty party columns dropped": src.dropped_party_columns.get(name),
        })

    findings = ""
    if validation["issues"]:
        findings += f'<p class="note">Issues</p><ul>{_items(validation["issues"])}</ul>'
    if validation["notes"]:
        findings += f'<p class="note">Notes</p><ul>{_items(validation["notes"])}</ul>'
    findings = findings or "<p>No issues or notes.</p>"

    checks_html = table_html(pd.DataFrame(validation["checks"]), "checks") if validation["checks"] else "<p>No checks ran.</p>"
    totals = validation.get("municipality_totals")
    turnout = validation.get("turnout")
    facts = []
    if totals:
        facts += [{"Item": k.replace("_", " ").capitalize(), "Value": f"{v:,}"} for k, v in totals.items()]
    if turnout:
        facts.append({"Item": "Turnout per district (min / median / max)",
                      "Value": f"{turnout['min']:.1%} / {turnout['median']:.1%} / {turnout['max']:.1%} "
                               f"({turnout['districts']} districts; stored as {turnout['stored_as']})"})
    d = validation.get("districts", {})
    if d:
        facts.append({"Item": "Boundary area", "Value": f"{d['total_area_km2']:,} km² in {d['rows']} districts; "
                      f"district median {d['district_area_km2']['50%']} km²"})
        facts.append({"Item": "Boundary extent (EPSG:3006)", "Value": ", ".join(f"{v:,.0f}" for v in d["bounds"])})
    facts_html = table_html(pd.DataFrame(facts), "facts") if facts else ""

    parties = validation.get("parties", [])
    parties_html = searchable_table_html(pd.DataFrame(parties), "parties") if parties else "<p>No party data.</p>"

    coll = validation.get("collection_districts", [])
    coll_html = table_html(pd.DataFrame(coll), "collection") if coll else "<p>None.</p>"

    join = validation.get("join")
    if join:
        join_rows = pd.DataFrame([
            {"Item": "Result districts (with an area)", "Value": join["result_districts"]},
            {"Item": "Boundary districts", "Value": join["boundary_districts"]},
            {"Item": "Matched by code", "Value": join["matched"]},
            {"Item": "Collection districts (no area)", "Value": ", ".join(join["collection_districts_without_area"]) or "-"},
            {"Item": "Results without boundary", "Value": ", ".join(join["results_without_boundary"]) or "-"},
            {"Item": "Boundaries without results", "Value": ", ".join(join["boundaries_without_results"]) or "-"},
            {"Item": "Matched but named differently", "Value": len(join.get("name_differences", []))},
        ])
        join_html = table_html(join_rows, "join")
    else:
        join_html = "<p>Join check skipped.</p>"

    seen: Dict[str, list] = {}
    for name, df in frames.items():
        geom_name = getattr(df, "geometry", None).name if name == DISTRICTS_FRAME else None
        spec = SHEETS.get(name)
        wide = spec is not None and spec.kind in ("counts_wide", "shares_wide")
        for col in df.columns:
            if col == geom_name or (wide and col in party_columns(df)):
                continue
            seen.setdefault(col, []).append(name)
    dictionary = pd.DataFrame([
        {"Swedish (source)": col,
         "English alias": FIELDS[col].alias_en if col in FIELDS else None,
         "Meaning": FIELDS[col].description_en if col in FIELDS else "UNKNOWN - not in config.FIELDS",
         "Unit": FIELDS[col].unit if col in FIELDS else None,
         "Frames": ", ".join(names)}
        for col, names in seen.items()
    ])
    categories = pd.DataFrame([{"Category (sv)": k, "English alias": v} for k, v in SUMMARY_CATEGORIES.items()])

    run_rows = pd.DataFrame([
        {"Item": "Dataset", "Value": f"{DATASET_METADATA['name_sv']} / {DATASET_METADATA['name_en']}"},
        {"Item": "Election", "Value": f"{election} {src.election_year or ''}".strip()},
        {"Item": "Municipality code", "Value": src.municipality_code},
        {"Item": "Source", "Value": DATASET_METADATA["authority"]},
        {"Item": "Licence", "Value": DATASET_METADATA["license"]},
        {"Item": "Results file", "Value": str(src.results_path)},
        {"Item": "Boundary file", "Value": str(src.boundaries_path)},
        {"Item": "Run", "Value": run_info["run_timestamp"]},
        {"Item": "Issues", "Value": str(validation["issue_count"])},
    ])
    body = (
        f"<section><h2>Run</h2>{table_html(run_rows, 'run')}</section>"
        f"<section><h2>Frames</h2>{searchable_table_html(pd.DataFrame(overview), 'overview', html_columns=('Frame', 'Profile'))}"
        '<p class="note">Rows are filtered to the municipality; wide sheets lose the party columns that are '
        "empty for it. Otherwise data is shown as delivered.</p></section>"
        f"<section><h2>Issues and notes</h2>{findings}</section>"
        f"<section><h2>Key figures</h2>{facts_html}</section>"
        f"<section><h2>Consistency checks</h2>{checks_html}</section>"
        f"<section><h2>Parties</h2>{parties_html}"
        '<p class="note">Votes over all districts including the collection district. English names only for '
        "the eight Riksdag parties; local parties keep their Swedish name.</p></section>"
        f"<section><h2>Collection districts</h2>{coll_html}</section>"
        f"<section><h2>Results ↔ boundaries</h2>{join_html}</section>"
        f"<section><h2>Workbook description (Information sheet, Swedish)</h2><ul>{_items(src.information)}</ul></section>"
        f"<section><h2>Field dictionary</h2>{searchable_table_html(dictionary, 'fields')}"
        f"<h3>Summary categories in the long sheet</h3>{table_html(categories, 'categories')}</section>"
    )
    return page_html("Election preprocess", body, subtitle=f"{election} {src.election_year or ''} · municipality {src.municipality_code}")


def export_outputs(src: ElectionSource, validation: dict, preprocessing: dict, output_dir: Path, run_info: dict) -> dict:
    """Write every output; returns the paths written."""
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    written = {"tables": {}, "profiles": {}}

    for name, df in _frames(src).items():
        spec = SHEETS.get(name)
        title = f"{name} - {spec.description_en}" if spec else (f"{name} - voting-district boundaries" if name == DISTRICTS_FRAME else name)
        notes = column_notes(df)
        source = src.boundaries_path.name if name == DISTRICTS_FRAME else f"{src.results_path.name}, sheet {name}"
        written["tables"][name] = str(save_dataframe_html(
            df, output_dir / "tables" / f"{name}.html", title=title,
            subtitle=f"Source: {source} · municipality {src.municipality_code}", column_notes=notes, max_rows=None,
        ))
        profiler = DataFrameProfiler(df, name=title, column_descriptions=notes)
        html_path = output_dir / "profiles" / f"{name}_profile.html"
        profiler.save_markdown(output_dir / "profiles" / f"{name}_profile.md")
        profiler.save_html(html_path)
        written["profiles"][name] = str(html_path)

    index_path = output_dir / "index.html"
    index_path.write_text(_index_page(src, validation, run_info), encoding="utf-8")
    written["index"] = str(index_path)

    report = {
        "dataset": DATASET_METADATA,
        "run": run_info,
        "inputs": {
            "results_file": str(src.results_path),
            "boundaries_file": str(src.boundaries_path),
            "municipality_code": src.municipality_code,
            "election_type": src.election_type,
            "election_year": src.election_year,
            "rows_before_filter": src.national_rows,
            "empty_party_columns_dropped": src.dropped_party_columns,
            "information_sheet": src.information,
        },
        "validation": validation,
        "preprocessing": preprocessing,
        "outputs": written,
    }
    report_path = output_dir / REPORT_FILE
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False, default=json_default), encoding="utf-8")
    written["report"] = str(report_path)
    logger.info(f"  - Wrote {len(written['tables'])} data views and profiles, index and report to {output_dir}")
    return written
