"""
DataFrame → HTML viewer.

Writes a DataFrame or GeoDataFrame as a self-contained HTML page: sticky header,
click a column header to sort, a search box that filters rows, and missing values
shown as a grey dash. No external scripts or stylesheets, so the file opens
offline and can be archived next to the run that produced it.

Geometry is not written as WKT (a polygon can be thousands of characters).
It is replaced by `geometry_type`, `geometry_area_m2` and the bounds, which is
enough to check a table by eye. Area and bounds are in CRS units, so they are
metres only for a projected CRS such as EPSG:3006.

Usage:
    from utils.dataframe_html import save_dataframe_html
    save_dataframe_html(gdf, "out/table.html", title="Tab1_Ruta_2024",
                        column_notes={"Totalt": "Total population"})

The page holds the data itself. Data under a non-publishable licence (e.g. SCB
FUK) must be written to an ignored folder such as Processed_data/, never to a
tracked one.
"""

from __future__ import annotations

import html
import math
from pathlib import Path
from typing import Dict, Iterable, Optional

import pandas as pd

try:  # geopandas is optional for plain DataFrames
    import geopandas as gpd
except ImportError:  # pragma: no cover
    gpd = None

#: Rows written by default. Larger frames are cut with a visible note.
DEFAULT_MAX_ROWS = 10_000

_CSS = """
:root {
  --bg: #f6f7f9; --card: #ffffff; --text: #1f2328; --muted: #5a6570;
  --border: #e1e5ea; --head: #f1f4f7; --accent: #2c7a7b; --hover: #eef6f6;
  --warn-bg: #fff6e5; --warn-text: #8a5300;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg: #15181c; --card: #1d2126; --text: #e6e8eb; --muted: #9aa4ae;
    --border: #30363d; --head: #242a31; --accent: #4fb3b4; --hover: #22302f;
    --warn-bg: #3a2e14; --warn-text: #f0c36a;
  }
}
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--text);
       font: 14px/1.5 "Segoe UI", system-ui, Arial, sans-serif; }
.wrap { max-width: 1400px; margin: 0 auto; padding: 20px 16px 48px; }
header h1 { margin: 0 0 4px; font-size: 24px; }
header p { margin: 0; color: var(--muted); }
section { margin-top: 18px; padding: 14px 16px; background: var(--card);
          border: 1px solid var(--border); border-radius: 10px; overflow-x: auto; }
h2 { margin: 0 0 10px; font-size: 18px; border-left: 4px solid var(--accent); padding-left: 8px; }
h3 { margin: 14px 0 6px; font-size: 15px; color: var(--muted); }
a { color: var(--accent); }
.note { color: var(--muted); font-size: 13px; margin: 6px 0; }
.warn { background: var(--warn-bg); color: var(--warn-text); padding: 6px 10px; border-radius: 6px; }
.toolbar { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; margin-bottom: 8px; }
.toolbar input { padding: 6px 10px; min-width: 240px; border: 1px solid var(--border);
                 border-radius: 6px; background: var(--bg); color: var(--text); }
.scroll { overflow: auto; max-height: 75vh; border: 1px solid var(--border); border-radius: 8px; }
table.df { border-collapse: collapse; width: 100%; font-size: 13px; }
table.df th, table.df td { border-bottom: 1px solid var(--border); padding: 5px 10px;
                           text-align: left; white-space: nowrap; }
table.df th { position: sticky; top: 0; background: var(--head); cursor: pointer; user-select: none; }
table.df th[title] { text-decoration: underline dotted; }
table.df th.asc::after { content: " \\25B2"; font-size: 10px; }
table.df th.desc::after { content: " \\25BC"; font-size: 10px; }
table.df td.num { text-align: right; font-variant-numeric: tabular-nums; }
table.df td.null { color: var(--muted); }
table.df tbody tr:hover { background: var(--hover); }
dl.notes { display: grid; grid-template-columns: max-content 1fr; gap: 2px 14px; margin: 0; font-size: 13px; }
dl.notes dt { font-family: Consolas, monospace; }
dl.notes dd { margin: 0; color: var(--muted); }
"""

# Sorting and filtering for every table with class "df" on the page.
_JS = """
(function () {
  function key(td) {
    if (td.classList.contains('null')) return null;
    var t = td.textContent.trim();
    if (td.classList.contains('num')) { var n = Number(t.replace(/[\\s,]/g, '')); if (!isNaN(n)) return n; }
    return t.toLowerCase();
  }
  document.querySelectorAll('table.df').forEach(function (table) {
    var body = table.tBodies[0];
    table.querySelectorAll('th').forEach(function (th, col) {
      th.addEventListener('click', function () {
        var asc = !th.classList.contains('asc');
        table.querySelectorAll('th').forEach(function (h) { h.classList.remove('asc', 'desc'); });
        th.classList.add(asc ? 'asc' : 'desc');
        var rows = Array.prototype.slice.call(body.rows);
        rows.sort(function (a, b) {
          var x = key(a.cells[col]), y = key(b.cells[col]);
          if (x === null && y === null) return 0;
          if (x === null) return 1;            // missing values always last
          if (y === null) return -1;
          if (x < y) return asc ? -1 : 1;
          if (x > y) return asc ? 1 : -1;
          return 0;
        });
        rows.forEach(function (r) { body.appendChild(r); });
      });
    });
  });
  document.querySelectorAll('input[data-filter]').forEach(function (input) {
    var table = document.getElementById(input.getAttribute('data-filter'));
    var counter = document.getElementById(input.getAttribute('data-filter') + '-count');
    input.addEventListener('input', function () {
      var q = input.value.trim().toLowerCase(), shown = 0;
      Array.prototype.forEach.call(table.tBodies[0].rows, function (r) {
        // Join cells with a separator so a match cannot span two neighbouring cells.
        var text = Array.prototype.map.call(r.cells, function (c) { return c.textContent; }).join('\\u0001');
        var hit = !q || text.toLowerCase().indexOf(q) !== -1;
        r.style.display = hit ? '' : 'none';
        if (hit) shown++;
      });
      if (counter) counter.textContent = shown.toLocaleString();
    });
  });
})();
"""


def prepare_for_display(df: pd.DataFrame) -> pd.DataFrame:
    """Copy of `df` with the active geometry replaced by type, area and bounds."""
    out = pd.DataFrame(df).copy()
    if gpd is not None and isinstance(df, gpd.GeoDataFrame) and df.geometry.name in df.columns:
        geom = df.geometry
        bounds = geom.bounds
        out = out.drop(columns=[geom.name])
        out["geometry_type"] = geom.geom_type
        out["geometry_area_m2"] = geom.area.round(1)
        for side in ("minx", "miny", "maxx", "maxy"):
            out[f"bounds_{side}"] = bounds[side].round(2)
    # Any other geometry-like column (e.g. a second GeoSeries) becomes its type only.
    for col in out.columns:
        if gpd is not None and isinstance(out[col].dtype, gpd.array.GeometryDtype):
            out[col] = gpd.GeoSeries(out[col]).geom_type
    return out


def _is_missing(value) -> bool:
    if value is None:
        return True
    try:
        return bool(pd.isna(value))
    except (TypeError, ValueError):  # lists, arrays
        return False


def _cell(value, raw_html: bool) -> str:
    if _is_missing(value):
        return '<td class="null">&mdash;</td>'
    if raw_html:
        return f"<td>{value}</td>"
    if isinstance(value, bool):
        return f"<td>{value}</td>"
    if isinstance(value, int) or (hasattr(value, "dtype") and getattr(value.dtype, "kind", "") in "iu"):
        return f'<td class="num">{int(value)}</td>'
    if isinstance(value, float) or (hasattr(value, "dtype") and getattr(value.dtype, "kind", "") == "f"):
        value = float(value)
        # At most 4 decimals, trailing zeros dropped (12.5000 -> 12.5, 3.0 -> 3).
        text = str(value) if math.isinf(value) else f"{value:.4f}".rstrip("0").rstrip(".")
        return f'<td class="num">{text}</td>'
    return f"<td>{html.escape(str(value))}</td>"


def table_html(
    df: pd.DataFrame,
    table_id: str,
    column_notes: Optional[Dict[str, str]] = None,
    html_columns: Iterable[str] = (),
) -> str:
    """
    One sortable `<table class="df">`.

    Args:
        df: data to show; index is not written (reset it first if it matters).
        table_id: unique id on the page (used by the search box).
        column_notes: column → description, shown as the header tooltip.
        html_columns: columns whose values are trusted HTML (e.g. links built by
            the caller). Every other value is escaped.
    """
    notes = column_notes or {}
    raw = set(html_columns)
    head = "".join(
        f'<th title="{html.escape(notes[c])}">{html.escape(str(c))}</th>' if c in notes
        else f"<th>{html.escape(str(c))}</th>"
        for c in df.columns
    )
    cols = list(df.columns)
    rows = []
    for values in df.itertuples(index=False, name=None):
        rows.append("<tr>" + "".join(_cell(v, cols[i] in raw) for i, v in enumerate(values)) + "</tr>")
    return (
        f'<table class="df" id="{html.escape(table_id)}"><thead><tr>{head}</tr></thead>'
        f"<tbody>{''.join(rows)}</tbody></table>"
    )


def searchable_table_html(
    df: pd.DataFrame,
    table_id: str,
    column_notes: Optional[Dict[str, str]] = None,
    html_columns: Iterable[str] = (),
) -> str:
    """`table_html` with a search box and a shown-rows counter above it."""
    tid = html.escape(table_id)
    return (
        f'<div class="toolbar"><input type="search" placeholder="Filter rows…" data-filter="{tid}">'
        f'<span class="note"><span id="{tid}-count">{len(df):,}</span> of {len(df):,} rows shown</span></div>'
        f'<div class="scroll">{table_html(df, table_id, column_notes, html_columns)}</div>'
    )


def page_html(title: str, body: str, subtitle: str = "") -> str:
    """Wrap section HTML in a complete page with the shared style and script."""
    sub = f"<p>{html.escape(subtitle)}</p>" if subtitle else ""
    return (
        "<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">"
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
        f"<title>{html.escape(title)}</title><style>{_CSS}</style></head>"
        f"<body><div class=\"wrap\"><header><h1>{html.escape(title)}</h1>{sub}</header>"
        f"{body}</div><script>{_JS}</script></body></html>\n"
    )


def dataframe_page(
    df: pd.DataFrame,
    title: str,
    subtitle: str = "",
    column_notes: Optional[Dict[str, str]] = None,
    max_rows: Optional[int] = DEFAULT_MAX_ROWS,
) -> str:
    """Full HTML page for one DataFrame: column notes, then the searchable table."""
    shown = prepare_for_display(df)
    notes = dict(column_notes or {})
    info = f"{len(df):,} rows × {len(df.columns)} columns"
    if gpd is not None and isinstance(df, gpd.GeoDataFrame):
        info += f" · CRS {df.crs}" if df.crs is not None else " · no CRS"
        notes.setdefault("geometry_type", "Geometry type of the row (the geometry itself is not written)")
        notes.setdefault("geometry_area_m2", "Geometry area in CRS units² (m² for EPSG:3006)")
    body = []
    if max_rows is not None and len(shown) > max_rows:
        body.append(
            f'<section><p class="warn">Showing the first {max_rows:,} of {len(shown):,} rows. '
            "Pass a larger max_rows to write more.</p></section>"
        )
        shown = shown.head(max_rows)
    described = {c: n for c, n in notes.items() if c in shown.columns}
    if described:
        items = "".join(
            f"<dt>{html.escape(str(c))}</dt><dd>{html.escape(n)}</dd>" for c, n in described.items()
        )
        body.append(f'<section><h2>Columns</h2><dl class="notes">{items}</dl></section>')
    body.append(f"<section><h2>Data</h2>{searchable_table_html(shown, 'data', notes)}</section>")
    return page_html(title, "".join(body), subtitle=" · ".join(s for s in (subtitle, info) if s))


def save_dataframe_html(
    df: pd.DataFrame,
    output_path: Path,
    title: str,
    subtitle: str = "",
    column_notes: Optional[Dict[str, str]] = None,
    max_rows: Optional[int] = DEFAULT_MAX_ROWS,
) -> Path:
    """Write `dataframe_page(...)` to `output_path` (folders are created)."""
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        dataframe_page(df, title, subtitle, column_notes, max_rows), encoding="utf-8"
    )
    return output_path
