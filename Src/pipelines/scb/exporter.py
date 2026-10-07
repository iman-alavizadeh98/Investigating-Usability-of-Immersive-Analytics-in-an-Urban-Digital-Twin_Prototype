"""
Write the preprocess outputs of an SCB dataset: HTML views, profiles, JSON report.

Output folder layout:
    index.html                              start here: overview, issues, coverage, field dictionary
    tables/<table>.html                     the table itself (sortable, filterable)
    profiles/<table>_profile.html/.md       DataFrameProfiler report per table
    <dataset_id>_preprocess_report.json     inputs, run time, load/validation/preprocess results

The views contain the licensed data (SCB, FUK): write them to an ignored folder
such as Processed_data/, never to a tracked one.
"""

from __future__ import annotations

import html
import json
import logging
from datetime import datetime
from pathlib import Path
from typing import Dict

import numpy as np
import pandas as pd

from utils.data_profiler import DataFrameProfiler
from utils.dataframe_html import page_html, save_dataframe_html, searchable_table_html, table_html

from .config import ScbDataset
from .loader import SourceTable

logger = logging.getLogger(__name__)


def report_file_name(dataset: ScbDataset) -> str:
    return f"{dataset.dataset_id}_preprocess_report.json"


def column_notes(table: SourceTable, dataset: ScbDataset) -> Dict[str, str]:
    """Column → "alias: meaning (unit) [source]" for every column in the field dictionary."""
    notes = {}
    for col in table.data.columns:
        info = dataset.describe_field(col)
        if info:
            unit = f" ({info.unit})" if info.unit else ""
            notes[col] = f"{info.alias_en}: {info.description_en}{unit} [{info.source}]"
    return notes


def json_default(value):
    """json.dumps fallback for numpy scalars, paths and datetimes."""
    if isinstance(value, np.integer):
        return int(value)
    if isinstance(value, np.floating):
        return float(value)
    if isinstance(value, np.bool_):
        return bool(value)
    if isinstance(value, (Path, datetime)):
        return str(value)
    raise TypeError(f"Not JSON serializable: {type(value).__name__}")


def _items(lines) -> str:
    return "".join(f"<li>{html.escape(str(x))}</li>" for x in lines)


def _index_page(tables: Dict[str, SourceTable], validation: dict, run_info: dict, dataset: ScbDataset) -> str:
    reports = validation["tables"]
    meta = dataset.metadata

    # Overview
    rows = []
    for key in tables:
        r = reports[key]
        sizes = r.get("cell_sizes_m")
        rows.append({
            "Table": f'<a href="tables/{html.escape(key)}.html">{html.escape(key)}</a>',
            "Profile": f'<a href="profiles/{html.escape(key)}_profile.html">profile</a>',
            "Content": r["title_en"] or "(not in catalog)",
            "Unit": r["unit"],
            "Year": r["year"],
            "Rows": r["rows"],
            "Cell sizes (m: cells)": ", ".join(f"{k}: {v}" for k, v in sizes.items()) if sizes else None,
            "Total column": r["total_column"],
            "Total sum": r["total_sum"],
            "Value columns": ", ".join(r.get("value_columns", [])) or None,
            "Issues": len(r["issues"]),
            "Notes": len(r["notes"]),
        })
    overview = searchable_table_html(pd.DataFrame(rows), "overview", html_columns=("Table", "Profile"))

    # Issues and notes
    findings = []
    for key in tables:
        r = reports[key]
        if not (r["issues"] or r["notes"]):
            continue
        block = f"<h3>{html.escape(key)}</h3>"
        if r["issues"]:
            block += f'<p class="note">Issues</p><ul>{_items(r["issues"])}</ul>'
        if r["notes"]:
            block += f'<p class="note">Notes</p><ul>{_items(r["notes"])}</ul>'
        findings.append(block)
    findings_html = "".join(findings) or "<p>No issues or notes.</p>"

    # Value columns (amounts, medians): range per table
    value_rows = [
        {"Table": key, "Column": col, **{k.capitalize(): v for k, v in stats.items()}}
        for key in tables for col, stats in reports[key].get("value_stats", {}).items()
    ]
    values_html = (
        table_html(pd.DataFrame(value_rows), "values") if value_rows
        else "<p>No amount or median columns in these tables.</p>"
    )

    # Coverage
    coverage_blocks = []
    for group in validation["coverage"]:
        df = pd.DataFrame(group["per_table"]).rename(columns={
            "table": "Table", "units": "Units", "missing_vs_union": "Missing vs. union",
            "total_column": "Total column", "total_sum": "Total sum",
        })
        coverage_blocks.append(
            f"<h3>{html.escape(group['group'])}</h3>"
            f'<p class="note">{group["units_in_any_table"]:,} units in any table, '
            f'{group["units_in_every_table"]:,} in every table.</p>'
            + table_html(df, f"coverage-{len(coverage_blocks)}")
        )

    # Field dictionary: every column seen in the data
    seen: Dict[str, list] = {}
    for key, table in tables.items():
        for col in table.data.columns:
            if col != table.data.geometry.name:
                seen.setdefault(col, []).append(key)
    dictionary = []
    for col, keys in seen.items():
        info = dataset.describe_field(col)
        dictionary.append({
            "Swedish (source)": col,
            "English alias": info.alias_en if info else None,
            "Meaning": info.description_en if info else "UNKNOWN - not in the field dictionary",
            "Unit": info.unit if info else None,
            "Role": info.role if info else None,
            "Meaning source": info.source if info else None,
            "Tables": ", ".join(keys),
        })
    dictionary_html = searchable_table_html(pd.DataFrame(dictionary), "fields")

    run_rows = pd.DataFrame([
        {"Item": "Dataset", "Value": f"{meta['name_sv']} / {meta['name_en']}"},
        {"Item": "Source", "Value": meta["authority"]},
        {"Item": "Licence", "Value": meta["license"]},
        {"Item": "Input folder", "Value": run_info["input_dir"]},
        {"Item": "Run", "Value": run_info["run_timestamp"]},
        {"Item": "Tables", "Value": str(len(tables))},
        {"Item": "Issues", "Value": str(validation["issue_count"])},
    ])
    body = (
        f"<section><h2>Run</h2>{table_html(run_rows, 'run')}</section>"
        f"<section><h2>Tables</h2>{overview}"
        '<p class="note">Click a table name for its data, "profile" for its column statistics. '
        "Data is shown exactly as delivered (no preprocessing rules yet).</p></section>"
        f"<section><h2>Issues and notes</h2>{findings_html}</section>"
        f"<section><h2>Value columns</h2>"
        '<p class="note">Amounts (sums, e.g. SEK) and medians. They are not counts: never summed against '
        "the total, and a median must never be summed across units.</p>"
        f"{values_html}</section>"
        f"<section><h2>Coverage across tables</h2>"
        '<p class="note">Ruta units are keyed "&lt;size&gt;_&lt;id&gt;". A unit missing from a table means no data, not 0.</p>'
        f"{''.join(coverage_blocks) or '<p>No tables with IDs.</p>'}</section>"
        f"<section><h2>Field dictionary</h2>"
        '<p class="note">Meaning source: "pdf" = stated in the delivery\'s (older) variable description; '
        '"inferred" = read from the abbreviation, not confirmed by SCB.</p>'
        f"{dictionary_html}</section>"
    )
    return page_html(f"{dataset.dataset_id.capitalize()} preprocess", body, subtitle=meta["name_en"])


def export_outputs(
    tables: Dict[str, SourceTable],
    validation: dict,
    preprocessing: dict,
    output_dir: Path,
    run_info: dict,
    dataset: ScbDataset,
) -> dict:
    """Write every output; returns the paths written."""
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    written = {"tables": {}, "profiles": {}}

    for key, table in tables.items():
        spec = dataset.table(table.table_number)
        title = f"{key} - {spec.title_en}" if spec else key
        notes = column_notes(table, dataset)
        written["tables"][key] = str(save_dataframe_html(
            table.data, output_dir / "tables" / f"{key}.html", title=title,
            subtitle=f"Source: {table.path.name}", column_notes=notes, max_rows=None,
        ))
        profiler = DataFrameProfiler(
            table.data, name=title, column_descriptions=notes,
            key_columns=[table.id_column] if table.id_column else [],
        )
        html_path = output_dir / "profiles" / f"{key}_profile.html"
        profiler.save_markdown(output_dir / "profiles" / f"{key}_profile.md")
        profiler.save_html(html_path)
        written["profiles"][key] = str(html_path)

    index_path = output_dir / "index.html"
    index_path.write_text(_index_page(tables, validation, run_info, dataset), encoding="utf-8")
    written["index"] = str(index_path)

    report = {
        "dataset": dataset.metadata,
        "run": run_info,
        "inputs": {
            key: {
                "path": str(t.path),
                "table_number": t.table_number,
                "unit": t.unit,
                "year": t.year,
                "declared_encoding": t.declared_encoding,
                "encoding_fallback_used": t.encoding_used,
                "id_column": t.id_column,
                "size_column": t.size_column,
            }
            for key, t in tables.items()
        },
        "validation": validation,
        "preprocessing": preprocessing,
        "outputs": written,
    }
    report_path = output_dir / report_file_name(dataset)
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False, default=json_default), encoding="utf-8")
    written["report"] = str(report_path)
    logger.info(f"  - Wrote {len(tables)} data views, {len(tables)} profiles, index and report to {output_dir}")
    return written
