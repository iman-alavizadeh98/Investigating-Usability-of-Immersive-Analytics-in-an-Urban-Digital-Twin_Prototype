"""
DataFrame Profiler: Generate human-readable summaries of dataframes.

Provides insights into data structure, quality, and content.
Exports to Markdown, HTML, or JSON.
"""

from pathlib import Path
from typing import Optional, Dict, Any, List
import geopandas as gpd
import pandas as pd
import logging

logger = logging.getLogger(__name__)


class DataFrameProfiler:
    """Profile a GeoDataFrame or DataFrame and generate summaries."""

    def __init__(
        self,
        gdf: pd.DataFrame,
        name: str = "Dataset",
        column_descriptions: Optional[Dict[str, str]] = None,
        key_columns: Optional[List[str]] = None
    ):
        """
        Initialize profiler.

        Args:
            gdf: DataFrame or GeoDataFrame to profile
            name: Human-readable name for the dataset
            column_descriptions: Optional mapping of column name -> meaning
            key_columns: Optional list of columns to check for duplicate keys
        """
        self.gdf = gdf
        self.name = name
        self.column_descriptions = column_descriptions or {}
        self.key_columns = key_columns or []
        self.is_geodataframe = isinstance(gdf, gpd.GeoDataFrame)

    def profile(self) -> Dict[str, Any]:
        """
        Create comprehensive profile of the dataframe.

        Returns:
            dict with profiling information
        """
        duplicates = self._get_duplicate_summary()

        profile = {
            "name": self.name,
            "type": "GeoDataFrame" if self.is_geodataframe else "DataFrame",
            "shape": {
                "rows": self.gdf.shape[0],
                "columns": self.gdf.shape[1]
            },
            "memory_mb": round(self.gdf.memory_usage(deep=True).sum() / 1024**2, 2),
            "crs": str(self.gdf.crs) if self.is_geodataframe else None,
            "columns": self._profile_columns(),
            "sample_rows": self.gdf.head(3).to_dict(orient="records"),
            "missing_values": self._get_missing_summary(),
            "numeric_stats": self._get_numeric_stats(),
            "categorical_stats": self._get_categorical_stats(),
            "duplicate_rows": duplicates.get("duplicate_rows", 0),
            "duplicate_keys": duplicates.get("duplicate_keys", {}),
            "column_descriptions": self.column_descriptions
        }

        return profile

    def _profile_columns(self) -> List[Dict[str, Any]]:
        """Profile each column."""
        profiles = []
        total_rows = len(self.gdf)

        for col in self.gdf.columns:
            meaning = self.column_descriptions.get(col, "")
            if col == "geometry" and self.is_geodataframe:
                null_count = self.gdf[col].isna().sum()
                null_pct = round(null_count / total_rows * 100, 1) if total_rows else 0.0
                profiles.append({
                    "name": col,
                    "dtype": "geometry",
                    "non_null_count": self.gdf[col].notna().sum(),
                    "null_count": null_count,
                    "null_percentage": null_pct,
                    "unique_values": self.gdf[col].notna().sum(),
                    "geometry_types": self.gdf[col].geom_type.unique().tolist(),
                    "meaning": meaning
                })
                continue

            series = self.gdf[col]
            missing = series.isna().sum()
            null_pct = round(missing / total_rows * 100, 1) if total_rows else 0.0

            profile = {
                "name": col,
                "dtype": str(series.dtype),
                "non_null_count": len(series) - missing,
                "null_count": missing,
                "null_percentage": null_pct,
                "unique_values": series.nunique(),
                "meaning": meaning
            }

            if series.dtype in ["int64", "float64"]:
                profile.update({
                    "min": float(series.min()),
                    "max": float(series.max()),
                    "mean": float(series.mean())
                })
            elif series.dtype == "object":
                value_counts = series.value_counts()
                profile.update({
                    "sample_value": str(series.iloc[0]) if len(series) > 0 else None,
                    "most_common": str(value_counts.index[0]) if len(value_counts) > 0 else None
                })

            profiles.append(profile)

        return profiles

    def _get_missing_summary(self) -> Dict[str, Dict[str, float]]:
        """Get missing value counts and percentages per column."""
        missing = {}
        total_rows = len(self.gdf)
        if total_rows == 0:
            return missing

        for col in self.gdf.columns:
            missing_count = self.gdf[col].isna().sum()
            if missing_count > 0:
                missing[col] = {
                    "count": int(missing_count),
                    "pct": round(missing_count / total_rows * 100, 1)
                }
        return missing

    def _get_duplicate_summary(self) -> Dict[str, Any]:
        """Get duplicate row and key counts."""
        summary = {
            "duplicate_rows": int(self.gdf.duplicated().sum()),
            "duplicate_keys": {}
        }

        for key in self.key_columns:
            if key in self.gdf.columns:
                summary["duplicate_keys"][key] = int(
                    self.gdf.duplicated(subset=[key]).sum()
                )

        return summary

    def _get_numeric_stats(self) -> Dict[str, Dict[str, float]]:
        """Get statistics for numeric columns."""
        stats = {}
        for col in self.gdf.select_dtypes(include=["int64", "float64"]).columns:
            stats[col] = {
                "min": float(self.gdf[col].min()),
                "max": float(self.gdf[col].max()),
                "mean": float(self.gdf[col].mean()),
                "median": float(self.gdf[col].median()),
                "std": float(self.gdf[col].std())
            }
        return stats

    def _get_categorical_stats(self) -> Dict[str, Dict[str, int]]:
        """Get value counts for categorical columns."""
        stats = {}
        for col in self.gdf.select_dtypes(include=["object"]).columns:
            if col == "geometry":
                continue

            vc = self.gdf[col].value_counts().head(10)
            stats[col] = {str(k): int(v) for k, v in vc.items()}
        return stats

    def to_markdown(self) -> str:
        """Export profile as Markdown."""
        prof = self.profile()

        md = f"# {prof['name']}\n\n"
        md += f"**Type**: {prof['type']}\n\n"

        md += "## Summary\n\n"
        md += "| Metric | Value |\n"
        md += "|--------|-------|\n"
        md += f"| Rows | {prof['shape']['rows']:,} |\n"
        md += f"| Columns | {prof['shape']['columns']} |\n"
        md += f"| Memory | {prof['memory_mb']:.1f} MB |\n"
        if prof["crs"]:
            md += f"| CRS | {prof['crs']} |\n"
        md += f"| Duplicate rows | {prof.get('duplicate_rows', 0):,} |\n"
        for key, count in prof.get("duplicate_keys", {}).items():
            md += f"| Duplicate {key} | {count:,} |\n"
        md += "\n"

        if prof.get("missing_values"):
            md += "## Missing Values\n\n"
            md += "| Column | Missing | Missing % |\n"
            md += "|--------|---------|-----------|\n"
            for col, info in prof["missing_values"].items():
                md += f"| `{col}` | {info['count']:,} | {info['pct']:.1f}% |\n"
            md += "\n"

        md += "## Columns\n\n"
        md += "| Name | Type | Non-Null | Null % | Unique | Meaning |\n"
        md += "|------|------|----------|--------|--------|---------|\n"

        for col in prof["columns"]:
            md += (
                f"| `{col['name']}` | {col['dtype']} | "
                f"{col['non_null_count']:,} | "
                f"{col['null_percentage']:.1f}% | "
                f"{col['unique_values']} | {col.get('meaning', '')} |\n"
            )
        md += "\n"

        if prof["numeric_stats"]:
            md += "## Numeric Statistics\n\n"
            md += "| Column | Min | Max | Mean | Std |\n"
            md += "|--------|-----|-----|------|-----|\n"
            for col, stats in prof["numeric_stats"].items():
                md += (
                    f"| `{col}` | {stats['min']:.2f} | {stats['max']:.2f} | "
                    f"{stats['mean']:.2f} | {stats['std']:.2f} |\n"
                )
            md += "\n"

        if prof["categorical_stats"]:
            md += "## Categorical Distributions\n\n"
            for col, values in prof["categorical_stats"].items():
                md += f"### {col}\n\n"
                for val, count in list(values.items())[:5]:
                    md += f"- `{val}`: {count}\n"
                if len(values) > 5:
                    md += f"- ... and {len(values) - 5} more\n"
                md += "\n"

        md += "## Sample Rows\n\n"
        md += "```\n"
        md += str(self.gdf.head(3).to_string())
        md += "\n```\n"

        return md

    def to_html(self) -> str:
        """Export profile as a styled HTML report."""
        prof = self.profile()

        def _table_or_empty(df: pd.DataFrame, empty_msg: str) -> str:
            if df is None or df.empty:
                return f"<p>{empty_msg}</p>"
            return df.to_html(index=False, classes="data-table", border=0)

        summary_rows = [
            {"Metric": "Rows", "Value": f"{prof['shape']['rows']:,}"},
            {"Metric": "Columns", "Value": f"{prof['shape']['columns']}"},
            {"Metric": "Memory (MB)", "Value": f"{prof['memory_mb']:.1f}"},
            {"Metric": "CRS", "Value": prof["crs"] or "n/a"},
            {"Metric": "Duplicate rows", "Value": f"{prof.get('duplicate_rows', 0):,}"},
        ]
        for key, count in prof.get("duplicate_keys", {}).items():
            summary_rows.append({
                "Metric": f"Duplicate {key}",
                "Value": f"{count:,}"
            })

        summary_html = _table_or_empty(
            pd.DataFrame(summary_rows),
            "No summary available."
        )

        missing_rows = []
        for col, info in prof.get("missing_values", {}).items():
            missing_rows.append({
                "Column": col,
                "Missing": info["count"],
                "Missing %": info["pct"]
            })
        missing_html = _table_or_empty(
            pd.DataFrame(missing_rows),
            "No missing values found."
        )

        column_rows = []
        for col in prof["columns"]:
            notes = ""
            if col.get("geometry_types"):
                notes = "Geometry: " + ", ".join(col["geometry_types"])
            column_rows.append({
                "Column": col["name"],
                "Type": col["dtype"],
                "Non-null": col["non_null_count"],
                "Null %": col["null_percentage"],
                "Unique": col["unique_values"],
                "Meaning": col.get("meaning") or "",
                "Notes": notes
            })
        columns_html = _table_or_empty(
            pd.DataFrame(column_rows),
            "No columns available."
        )

        numeric_rows = []
        for col, stats in prof.get("numeric_stats", {}).items():
            numeric_rows.append({
                "Column": col,
                "Min": stats["min"],
                "Max": stats["max"],
                "Mean": stats["mean"],
                "Median": stats["median"],
                "Std": stats["std"]
            })
        numeric_html = _table_or_empty(
            pd.DataFrame(numeric_rows),
            "No numeric columns found."
        )

        categorical_sections = []
        max_categorical_columns = 8
        categorical_items = list(prof.get("categorical_stats", {}).items())
        for col, values in categorical_items[:max_categorical_columns]:
            rows = [{"Value": k, "Count": v} for k, v in values.items()]
            table_html = _table_or_empty(
                pd.DataFrame(rows),
                "No categorical values available."
            )
            categorical_sections.append(f"<h3>{col}</h3>{table_html}")
        categorical_html = "".join(categorical_sections) or "<p>No categorical columns found.</p>"

        if len(categorical_items) > max_categorical_columns:
            remaining = len(categorical_items) - max_categorical_columns
            categorical_html += f"<p class=\"note\">Additional categorical columns not shown: {remaining}</p>"

        sample_html = _table_or_empty(
            pd.DataFrame(prof.get("sample_rows", [])),
            "No sample rows available."
        )

        return f"""
<!doctype html>
<html lang=\"en\">
  <head>
    <meta charset=\"utf-8\" />
    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
    <title>{prof['name']} - Profile</title>
    <style>
      :root {{
        --bg: #f7f7f9;
        --card: #ffffff;
        --text: #1f2328;
        --muted: #5a6570;
        --border: #e2e8f0;
        --accent: #2c7a7b;
      }}
      body {{
        margin: 0;
        background: var(--bg);
        color: var(--text);
        font-family: \"Segoe UI\", Arial, sans-serif;
        line-height: 1.5;
      }}
      .wrap {{
        max-width: 1200px;
        margin: 24px auto;
        padding: 0 20px 40px;
      }}
      header {{
        padding: 16px 20px;
        border-radius: 12px;
        background: linear-gradient(90deg, #e6fffa, #f0fff4);
        border: 1px solid var(--border);
      }}
      header h1 {{
        margin: 0 0 6px 0;
        font-size: 28px;
      }}
      header p {{
        margin: 0;
        color: var(--muted);
      }}
      section {{
        margin-top: 20px;
        padding: 16px 20px;
        background: var(--card);
        border: 1px solid var(--border);
        border-radius: 12px;
        box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
      }}
      h2 {{
        margin: 0 0 12px 0;
        font-size: 20px;
        border-left: 4px solid var(--accent);
        padding-left: 10px;
      }}
      h3 {{
        margin: 14px 0 8px 0;
        font-size: 16px;
        color: var(--muted);
      }}
      .data-table {{
        width: 100%;
        border-collapse: collapse;
        margin-top: 8px;
      }}
      .data-table th,
      .data-table td {{
        border: 1px solid var(--border);
        padding: 8px 10px;
        text-align: left;
        font-size: 13px;
      }}
      .data-table th {{
        background: #f8fafc;
        color: #1f2937;
      }}
      .note {{
        color: var(--muted);
        font-size: 13px;
      }}
    </style>
  </head>
  <body>
    <div class=\"wrap\">
      <header>
        <h1>{prof['name']}</h1>
        <p>Type: {prof['type']}</p>
      </header>

      <section>
        <h2>Summary</h2>
        {summary_html}
      </section>

      <section>
        <h2>Missing Values</h2>
        {missing_html}
      </section>

      <section>
        <h2>Columns</h2>
        {columns_html}
      </section>

      <section>
        <h2>Numeric Statistics</h2>
        {numeric_html}
      </section>

      <section>
        <h2>Categorical Distributions</h2>
        {categorical_html}
        <p class=\"note\">Top values per column (up to 10).</p>
      </section>

      <section>
        <h2>Sample Rows</h2>
        {sample_html}
      </section>
    </div>
  </body>
</html>
"""

    def save_markdown(self, output_path: Path):
        """Save profile to Markdown file."""
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, "w", encoding="utf-8") as f:
            f.write(self.to_markdown())

        logger.info(f"Saved Markdown profile: {output_path}")

    def save_html(self, output_path: Path):
        """Save profile to HTML file."""
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, "w", encoding="utf-8") as f:
            f.write(self.to_html())

        logger.info(f"Saved HTML profile: {output_path}")
