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
    
    def __init__(self, gdf: pd.DataFrame, name: str = "Dataset"):
        """
        Initialize profiler.
        
        Args:
            gdf: DataFrame or GeoDataFrame to profile
            name: Human-readable name for the dataset
        """
        self.gdf = gdf
        self.name = name
        self.is_geodataframe = isinstance(gdf, gpd.GeoDataFrame)
    
    def profile(self) -> Dict[str, Any]:
        """
        Create comprehensive profile of the dataframe.
        
        Returns:
            dict with profiling information
        """
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
            "categorical_stats": self._get_categorical_stats()
        }
        
        return profile
    
    def _profile_columns(self) -> List[Dict[str, Any]]:
        """Profile each column."""
        profiles = []
        
        for col in self.gdf.columns:
            if col == "geometry" and self.is_geodataframe:
                profiles.append({
                    "name": col,
                    "dtype": "geometry",
                    "non_null_count": self.gdf[col].notna().sum(),
                    "null_count": self.gdf[col].isna().sum(),
                    "geometry_types": self.gdf[col].geom_type.unique().tolist()
                })
                continue
            
            series = self.gdf[col]
            missing = series.isna().sum()
            
            profile = {
                "name": col,
                "dtype": str(series.dtype),
                "non_null_count": len(series) - missing,
                "null_count": missing,
                "null_percentage": round(missing / len(series) * 100, 1),
                "unique_values": series.nunique()
            }
            
            # Add type-specific info
            if series.dtype in ["int64", "float64"]:
                profile.update({
                    "min": float(series.min()),
                    "max": float(series.max()),
                    "mean": float(series.mean())
                })
            elif series.dtype == "object":
                profile.update({
                    "sample_value": str(series.iloc[0]) if len(series) > 0 else None,
                    "most_common": str(series.value_counts().index[0]) if len(series) > 0 else None
                })
            
            profiles.append(profile)
        
        return profiles
    
    def _get_missing_summary(self) -> Dict[str, int]:
        """Get missing value counts per column."""
        missing = {}
        for col in self.gdf.columns:
            missing_count = self.gdf[col].isna().sum()
            if missing_count > 0:
                missing[col] = int(missing_count)
        return missing
    
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
        md += f"| Metric | Value |\n"
        md += f"|--------|-------|\n"
        md += f"| Rows | {prof['shape']['rows']:,} |\n"
        md += f"| Columns | {prof['shape']['columns']} |\n"
        md += f"| Memory | {prof['memory_mb']:.1f} MB |\n"
        if prof['crs']:
            md += f"| CRS | {prof['crs']} |\n"
        md += "\n"
        
        md += "## Columns\n\n"
        md += "| Name | Type | Non-Null | Null % | Unique |\n"
        md += "|------|------|----------|--------|--------|\n"
        
        for col in prof['columns']:
            md += (f"| `{col['name']}` | {col['dtype']} | "
                   f"{col['non_null_count']:,} | "
                   f"{col['null_percentage']:.1f}% | "
                   f"{col['unique_values']} |\n")
        md += "\n"
        
        if prof['numeric_stats']:
            md += "## Numeric Statistics\n\n"
            md += "| Column | Min | Max | Mean | Std |\n"
            md += "|--------|-----|-----|------|-----|\n"
            for col, stats in prof['numeric_stats'].items():
                md += (f"| `{col}` | {stats['min']:.2f} | {stats['max']:.2f} | "
                       f"{stats['mean']:.2f} | {stats['std']:.2f} |\n")
            md += "\n"
        
        if prof['categorical_stats']:
            md += "## Categorical Distributions\n\n"
            for col, values in prof['categorical_stats'].items():
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
        """Export profile as HTML table."""
        return self.gdf.head(10).to_html()
    
    def save_markdown(self, output_path: Path):
        """Save profile to Markdown file."""
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(self.to_markdown())
        
        logger.info(f"Saved Markdown profile: {output_path}")
    
    def save_html(self, output_path: Path):
        """Save profile to HTML file."""
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(self.to_html())
        
        logger.info(f"Saved HTML profile: {output_path}")
