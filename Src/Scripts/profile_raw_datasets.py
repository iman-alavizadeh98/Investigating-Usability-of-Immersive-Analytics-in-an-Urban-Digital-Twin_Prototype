#!/usr/bin/env python3
"""
Comprehensive Data Profiler for Gothenburg Digital Twin Raw Datasets
======================================================================

Purpose:
  Profile all raw datasets (Shapefiles, GeoPackages, GeoTIFFs, LiDAR point clouds)
  to understand structure, quality, and content before preprocessing.

Outputs:
  1. Processed_data/profile_summary.json  — consolidated summary of all datasets
  2. Processed_data/profiles/ — individual JSON file per dataset
  3. reports/data-quality/2026-04-23_dataset_profiling.md — human-readable report

Usage:
  python Src/Scripts/profile_raw_datasets.py

Requirements:
  geopandas, pandas, shapely, fiona, rasterio, pyproj, numpy, json

Author: Automated Data Pipeline
Date: 2026-04-23
"""

import json
import logging
from pathlib import Path
from typing import Dict, List, Any, Optional, Tuple
from dataclasses import dataclass, asdict
from datetime import datetime
import warnings

import geopandas as gpd
import pandas as pd
import numpy as np
from shapely.geometry import shape
import fiona
import rasterio
from rasterio.crs import CRS as RasterioCRS

warnings.filterwarnings("ignore", category=UserWarning)
warnings.filterwarnings("ignore", category=FutureWarning)

# ============================================================================
# CONFIGURATION
# ============================================================================

PROJECT_ROOT = Path(__file__).parent.parent.parent
RAW_DATA_DIR = PROJECT_ROOT / "Raw_data"
PROCESSED_DATA_DIR = PROJECT_ROOT / "Processed_data"
REPORTS_DIR = PROJECT_ROOT / "reports" / "data-quality"
PROFILES_DIR = PROCESSED_DATA_DIR / "profiles"

PROFILES_DIR.mkdir(parents=True, exist_ok=True)
REPORTS_DIR.mkdir(parents=True, exist_ok=True)

SAMPLE_SIZE = 1000
MIN_CATEGORICAL_SAMPLE = 5
MAX_SAMPLE_CATEGORIES = 5

LOG_FORMAT = "%(asctime)s | %(levelname)-8s | %(message)s"
logging.basicConfig(level=logging.INFO, format=LOG_FORMAT)
logger = logging.getLogger(__name__)

SWEDISH_KEYWORDS = {
    "byggnad", "objekt", "identifier", "id", "namn", "typ", "typ_kod",
    "höjd", "höjddata", "område", "gräns", "väg", "vägnät",
    "befolkning", "inkomst", "arbete", "arbetsstäd",
    "mark", "kommunikation", "egendom", "fastighet",
    "ortnamn", "terräng", "topografi", "laserdatå", "nh"
}

# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class SpatialMetadata:
    """Spatial metadata for a dataset."""
    crs: Optional[str]
    bounds: Optional[Dict[str, float]]
    geometry_types: List[str]
    geometry_type_counts: Dict[str, int]

@dataclass
class SchemaInfo:
    """Schema information for a dataset."""
    field_names: List[str]
    field_types: Dict[str, str]
    sample_values: Dict[str, List[Any]]

@dataclass
class QualityMetrics:
    """Data quality metrics."""
    total_features: int
    null_counts: Dict[str, int]
    duplicate_ids: List[str]
    geometry_valid_count: Optional[int]
    geometry_invalid_count: Optional[int]
    geometry_empty_count: Optional[int]
    geometry_multipart_count: Optional[int]
    numeric_ranges: Dict[str, Tuple[float, float]]
    categorical_distributions: Dict[str, Dict[str, int]]

@dataclass
class DatasetProfile:
    """Complete profile for a single dataset."""
    name: str
    file_path: str
    format_type: str
    file_size_mb: float
    creation_date: Optional[str]
    row_feature_count: int
    spatial: Optional[SpatialMetadata]
    schema: Optional[SchemaInfo]
    quality: Optional[QualityMetrics]
    anomalies: List[str]
    integration_notes: Dict[str, str]

# ============================================================================
# UTILITY FUNCTIONS
# ============================================================================

def get_file_size_mb(file_path: Path) -> float:
    """Get file size in MB."""
    try:
        return file_path.stat().st_size / (1024 * 1024)
    except Exception:
        return 0.0

def get_creation_date(file_path: Path) -> Optional[str]:
    """Get file creation date as ISO string."""
    try:
        mtime = file_path.stat().st_mtime
        return datetime.fromtimestamp(mtime).isoformat()
    except Exception:
        return None

def flag_swedish_field_anomalies(field_names: List[str]) -> List[str]:
    """Detect potential typos or encoding issues in Swedish field names."""
    anomalies = []
    for field in field_names:
        if any(ord(c) > 127 for c in field if c not in "åäöÅÄÖ"):
            anomalies.append(f"Possible encoding issue in field '{field}'")
        has_swedish = any(c in "åäöÅÄÖ" for c in field)
        has_underscore = "_" in field
        has_caps = any(c.isupper() for c in field)
        if has_swedish and has_caps and not has_underscore:
            anomalies.append(f"Inconsistent naming in field '{field}'")
    return anomalies

def get_sample_values(series: pd.Series, n: int = 3) -> List[Any]:
    """Extract first n non-null sample values from a Series."""
    samples = series.dropna().head(n).tolist()
    return [v.item() if isinstance(v, (np.integer, np.floating)) else v for v in samples]

def compute_null_counts(gdf: gpd.GeoDataFrame) -> Dict[str, int]:
    """Compute null counts per field."""
    return {col: int(gdf[col].isna().sum()) for col in gdf.columns}

def compute_categorical_distribution(series: pd.Series, top_n: int = 5) -> Dict[str, int]:
    """Get top N value counts for a categorical field."""
    vc = series.value_counts().head(top_n)
    return {str(k): int(v) for k, v in vc.items()}

def detect_duplicate_ids(gdf: gpd.GeoDataFrame) -> List[str]:
    """Detect potential ID fields with duplicates."""
    duplicates = []
    id_candidates = ["id", "objektidentitet", "identitet", "uid", "code", "kod"]
    for col in gdf.columns:
        if col.lower() in id_candidates or "id" in col.lower():
            if gdf[col].dtype in ["object", "int64"]:
                dup_count = gdf[col].duplicated().sum()
                if dup_count > 0:
                    duplicates.append(f"Field '{col}' has {dup_count} duplicate values")
    return duplicates

def detect_geometry_issues(gdf: gpd.GeoDataFrame) -> Tuple[int, int, int, int]:
    """Check geometry validity, empty, and multipart counts."""
    valid_count = 0
    invalid_count = 0
    empty_count = 0
    multipart_count = 0
    
    for geom in gdf.geometry:
        if geom is None or geom.is_empty:
            empty_count += 1
        elif not geom.is_valid:
            invalid_count += 1
        else:
            valid_count += 1
        if geom is not None and hasattr(geom, "geoms"):
            if len(geom.geoms) > 1:
                multipart_count += 1
    
    return valid_count, invalid_count, empty_count, multipart_count

# ============================================================================
# PROFILERS FOR EACH DATASET TYPE
# ============================================================================

def profile_shapefile(shp_path: Path) -> Optional[DatasetProfile]:
    """Profile a single Shapefile."""
    try:
        logger.info(f"Profiling Shapefile: {shp_path.name}")
        
        gdf = gpd.read_file(shp_path)
        if len(gdf) > SAMPLE_SIZE:
            logger.info(f"  → Sampling {SAMPLE_SIZE} of {len(gdf)} features")
            gdf = gdf.sample(n=SAMPLE_SIZE, random_state=42)
        
        crs = str(gdf.crs) if gdf.crs else None
        bounds_dict = None
        if not gdf.empty:
            bounds = gdf.total_bounds
            bounds_dict = {
                "west": float(bounds[0]),
                "south": float(bounds[1]),
                "east": float(bounds[2]),
                "north": float(bounds[3])
            }
        
        geom_types = gdf.geometry.geom_type.unique().tolist()
        geom_counts = {str(gt): int(count) for gt, count in gdf.geometry.geom_type.value_counts().items()}
        
        field_names = [col for col in gdf.columns if col != "geometry"]
        field_types = {col: str(gdf[col].dtype) for col in field_names}
        samples = {col: get_sample_values(gdf[col], n=3) for col in field_names}
        
        null_counts = compute_null_counts(gdf)
        dups = detect_duplicate_ids(gdf)
        valid, invalid, empty, multipart = detect_geometry_issues(gdf)
        
        numeric_ranges = {}
        for col in field_names:
            if gdf[col].dtype in ["int64", "float64"]:
                numeric_ranges[col] = (float(gdf[col].min()), float(gdf[col].max()))
        
        categorical_dists = {}
        for col in field_names:
            if gdf[col].dtype == "object" and gdf[col].nunique() < 100:
                categorical_dists[col] = compute_categorical_distribution(gdf[col])
        
        anomalies = flag_swedish_field_anomalies(field_names)
        if any(gdf[col].isna().sum() > len(gdf) * 0.5 for col in field_names):
            anomalies.append("Some fields are >50% null")
        if dups:
            anomalies.extend(dups)
        
        integration_notes = {
            "format": "Shapefile (multipart archive)",
            "suggested_english_names": "To be determined in translation phase",
            "potential_join_keys": "Review ID/code fields identified above"
        }
        
        spatial = SpatialMetadata(
            crs=crs,
            bounds=bounds_dict,
            geometry_types=geom_types,
            geometry_type_counts=geom_counts
        )
        
        schema = SchemaInfo(
            field_names=field_names,
            field_types=field_types,
            sample_values=samples
        )
        
        quality = QualityMetrics(
            total_features=len(gdf),
            null_counts=null_counts,
            duplicate_ids=dups,
            geometry_valid_count=valid,
            geometry_invalid_count=invalid,
            geometry_empty_count=empty,
            geometry_multipart_count=multipart,
            numeric_ranges=numeric_ranges,
            categorical_distributions=categorical_dists
        )
        
        profile = DatasetProfile(
            name=shp_path.stem,
            file_path=str(shp_path.relative_to(PROJECT_ROOT)),
            format_type="Shapefile",
            file_size_mb=get_file_size_mb(shp_path),
            creation_date=get_creation_date(shp_path),
            row_feature_count=len(gdf),
            spatial=spatial,
            schema=schema,
            quality=quality,
            anomalies=anomalies,
            integration_notes=integration_notes
        )
        
        logger.info(f"  ✓ Shapefile '{shp_path.name}': {len(gdf)} features, {len(field_names)} fields")
        return profile
        
    except Exception as e:
        logger.error(f"  ✗ Failed to profile Shapefile {shp_path.name}: {e}")
        return None

def profile_geopackage(gpkg_path: Path) -> List[Optional[DatasetProfile]]:
    """Profile a GeoPackage (may have multiple layers)."""
    profiles = []
    try:
        logger.info(f"Profiling GeoPackage: {gpkg_path.name}")
        
        layers = fiona.listlayers(str(gpkg_path))
        logger.info(f"  → Found {len(layers)} layer(s): {layers}")
        
        for layer_name in layers:
            try:
                gdf = gpd.read_file(gpkg_path, layer=layer_name)
                if len(gdf) > SAMPLE_SIZE:
                    logger.info(f"    Sampling {SAMPLE_SIZE} of {len(gdf)} features from layer '{layer_name}'")
                    gdf = gdf.sample(n=SAMPLE_SIZE, random_state=42)
                
                crs = str(gdf.crs) if gdf.crs else None
                bounds_dict = None
                if not gdf.empty:
                    bounds = gdf.total_bounds
                    bounds_dict = {
                        "west": float(bounds[0]),
                        "south": float(bounds[1]),
                        "east": float(bounds[2]),
                        "north": float(bounds[3])
                    }
                
                geom_types = gdf.geometry.geom_type.unique().tolist()
                geom_counts = {str(gt): int(count) for gt, count in gdf.geometry.geom_type.value_counts().items()}
                
                field_names = [col for col in gdf.columns if col != "geometry"]
                field_types = {col: str(gdf[col].dtype) for col in field_names}
                samples = {col: get_sample_values(gdf[col], n=3) for col in field_names}
                
                null_counts = compute_null_counts(gdf)
                dups = detect_duplicate_ids(gdf)
                valid, invalid, empty, multipart = detect_geometry_issues(gdf)
                
                numeric_ranges = {}
                for col in field_names:
                    if gdf[col].dtype in ["int64", "float64"]:
                        numeric_ranges[col] = (float(gdf[col].min()), float(gdf[col].max()))
                
                categorical_dists = {}
                for col in field_names:
                    if gdf[col].dtype == "object" and gdf[col].nunique() < 100:
                        categorical_dists[col] = compute_categorical_distribution(gdf[col])
                
                anomalies = flag_swedish_field_anomalies(field_names)
                if any(gdf[col].isna().sum() > len(gdf) * 0.5 for col in field_names):
                    anomalies.append("Some fields are >50% null")
                if dups:
                    anomalies.extend(dups)
                
                integration_notes = {
                    "format": f"GeoPackage layer: {layer_name}",
                    "suggested_english_names": "To be determined in translation phase",
                    "potential_join_keys": "Review ID/code fields identified above"
                }
                
                spatial = SpatialMetadata(
                    crs=crs,
                    bounds=bounds_dict,
                    geometry_types=geom_types,
                    geometry_type_counts=geom_counts
                )
                
                schema = SchemaInfo(
                    field_names=field_names,
                    field_types=field_types,
                    sample_values=samples
                )
                
                quality = QualityMetrics(
                    total_features=len(gdf),
                    null_counts=null_counts,
                    duplicate_ids=dups,
                    geometry_valid_count=valid,
                    geometry_invalid_count=invalid,
                    geometry_empty_count=empty,
                    geometry_multipart_count=multipart,
                    numeric_ranges=numeric_ranges,
                    categorical_distributions=categorical_dists
                )
                
                profile = DatasetProfile(
                    name=f"{gpkg_path.stem}_{layer_name}",
                    file_path=f"{str(gpkg_path.relative_to(PROJECT_ROOT))}::{layer_name}",
                    format_type="GeoPackage",
                    file_size_mb=get_file_size_mb(gpkg_path),
                    creation_date=get_creation_date(gpkg_path),
                    row_feature_count=len(gdf),
                    spatial=spatial,
                    schema=schema,
                    quality=quality,
                    anomalies=anomalies,
                    integration_notes=integration_notes
                )
                
                profiles.append(profile)
                logger.info(f"    ✓ Layer '{layer_name}': {len(gdf)} features, {len(field_names)} fields")
                
            except Exception as e:
                logger.error(f"    ✗ Failed to profile layer '{layer_name}': {e}")
        
        return profiles
        
    except Exception as e:
        logger.error(f"  ✗ Failed to profile GeoPackage {gpkg_path.name}: {e}")
        return []

def profile_geotiff(tif_path: Path) -> Optional[DatasetProfile]:
    """Profile a GeoTIFF raster (metadata only)."""
    try:
        logger.info(f"Profiling GeoTIFF: {tif_path.name}")
        
        with rasterio.open(tif_path) as src:
            crs = str(src.crs) if src.crs else None
            transform = src.transform
            bounds = src.bounds
            
            bounds_dict = {
                "west": float(bounds.left),
                "south": float(bounds.bottom),
                "east": float(bounds.right),
                "north": float(bounds.top)
            }
            
            width = src.width
            height = src.height
            cell_count = width * height
            
            dtype = str(src.dtypes[0]) if src.dtypes else "unknown"
            band_count = src.count
            pixel_size = (transform.a, transform.e)
            
            field_names = ["band", "data_type", "nodata", "resolution"]
            samples = {
                "band": [1],
                "data_type": [dtype],
                "nodata": [src.nodata],
                "resolution": [f"({pixel_size[0]:.2f}, {pixel_size[1]:.2f})"]
            }
            
            anomalies = []
            if src.nodata is None:
                anomalies.append("No nodata value defined")
            if src.count > 1:
                anomalies.append(f"Multi-band raster ({src.count} bands)")
            
            integration_notes = {
                "format": f"GeoTIFF raster ({band_count} band(s))",
                "suggested_usage": "Elevation grid for terrain mesh generation",
                "grid_cell_count": cell_count,
                "pixel_resolution": f"{pixel_size[0]:.2f} × {abs(pixel_size[1]):.2f} meters"
            }
            
            schema = SchemaInfo(
                field_names=field_names,
                field_types={"band": "int", "data_type": "str", "nodata": "float", "resolution": "str"},
                sample_values=samples
            )
            
            quality = QualityMetrics(
                total_features=cell_count,
                null_counts={},
                duplicate_ids=[],
                geometry_valid_count=None,
                geometry_invalid_count=None,
                geometry_empty_count=None,
                geometry_multipart_count=None,
                numeric_ranges={"pixel_value": (0.0, 255.0)},
                categorical_distributions={}
            )
            
            profile = DatasetProfile(
                name=tif_path.stem,
                file_path=str(tif_path.relative_to(PROJECT_ROOT)),
                format_type="GeoTIFF",
                file_size_mb=get_file_size_mb(tif_path),
                creation_date=get_creation_date(tif_path),
                row_feature_count=cell_count,
                spatial=SpatialMetadata(
                    crs=crs,
                    bounds=bounds_dict,
                    geometry_types=["Raster"],
                    geometry_type_counts={"Raster": 1}
                ),
                schema=schema,
                quality=quality,
                anomalies=anomalies,
                integration_notes=integration_notes
            )
            
            logger.info(f"  ✓ GeoTIFF '{tif_path.name}': {cell_count:,} cells, {width}×{height}, CRS={crs}")
            return profile
        
    except Exception as e:
        logger.error(f"  ✗ Failed to profile GeoTIFF {tif_path.name}: {e}")
        return None

def profile_las_file(las_path: Path) -> Optional[DatasetProfile]:
    """Profile a LiDAR point cloud (.las/.laz) — metadata only."""
    try:
        logger.info(f"Profiling LiDAR file: {las_path.name}")
        
        try:
            import laspy
            with laspy.open(las_path) as f:
                las = f.read()
                point_count = len(las.points)
                crs_str = str(las.header.crs) if hasattr(las.header, "crs") else None
                
                bounds_dict = {
                    "west": float(las.header.x_min),
                    "south": float(las.header.y_min),
                    "east": float(las.header.x_max),
                    "north": float(las.header.y_max)
                }
                
                classifications = {}
                if hasattr(las, "classification"):
                    class_counts = pd.Series(las.classification).value_counts()
                    classifications = {str(k): int(v) for k, v in class_counts.items()}
                
                field_names = ["X", "Y", "Z", "intensity", "return_number", "classification"]
                schema = SchemaInfo(
                    field_names=field_names,
                    field_types={f: "float" if f in ["X", "Y", "Z"] else "int" for f in field_names},
                    sample_values={
                        "X": [float(las.header.x_min)],
                        "Y": [float(las.header.y_min)],
                        "Z": [float(las.header.z_min)],
                        "intensity": [0],
                        "return_number": [1],
                        "classification": list(classifications.keys())[:3]
                    }
                )
                
                quality = QualityMetrics(
                    total_features=point_count,
                    null_counts={},
                    duplicate_ids=[],
                    geometry_valid_count=None,
                    geometry_invalid_count=None,
                    geometry_empty_count=None,
                    geometry_multipart_count=None,
                    numeric_ranges={
                        "X": (float(las.header.x_min), float(las.header.x_max)),
                        "Y": (float(las.header.y_min), float(las.header.y_max)),
                        "Z": (float(las.header.z_min), float(las.header.z_max))
                    },
                    categorical_distributions={"classification": classifications}
                )
                
                integration_notes = {
                    "format": f"LAS v{las.header.version}",
                    "point_count": point_count,
                    "classification_available": bool(classifications),
                    "suggested_usage": "Height data extraction for buildings, terrain analysis"
                }
                
                anomalies = []
                if not crs_str:
                    anomalies.append("CRS not defined in LAS header")
                
        except ImportError:
            logger.warning(f"  → laspy not installed; using basic file-level profiling")
            point_count = None
            crs_str = None
            bounds_dict = None
            schema = SchemaInfo(
                field_names=["format", "status"],
                field_types={"format": "str", "status": "str"},
                sample_values={"format": ["LAS/LAZ"], "status": ["Requires laspy library"]}
            )
            quality = QualityMetrics(
                total_features=0,
                null_counts={},
                duplicate_ids=[],
                geometry_valid_count=None,
                geometry_invalid_count=None,
                geometry_empty_count=None,
                geometry_multipart_count=None,
                numeric_ranges={},
                categorical_distributions={}
            )
            integration_notes = {
                "format": "LAS/LAZ point cloud",
                "status": "Basic profiling only (install laspy)",
                "suggested_usage": "Height data, terrain analysis"
            }
            anomalies = ["laspy library not installed"]
        
        profile = DatasetProfile(
            name=las_path.stem,
            file_path=str(las_path.relative_to(PROJECT_ROOT)),
            format_type="LiDAR (LAS/LAZ)",
            file_size_mb=get_file_size_mb(las_path),
            creation_date=get_creation_date(las_path),
            row_feature_count=point_count or 0,
            spatial=SpatialMetadata(
                crs=crs_str,
                bounds=bounds_dict,
                geometry_types=["Point cloud"],
                geometry_type_counts={"Point cloud": 1}
            ),
            schema=schema,
            quality=quality,
            anomalies=anomalies,
            integration_notes=integration_notes
        )
        
        logger.info(f"  ✓ LiDAR '{las_path.name}': {point_count or 'unknown'} points")
        return profile
        
    except Exception as e:
        logger.error(f"  ✗ Failed to profile LiDAR file {las_path.name}: {e}")
        return None

# ============================================================================
# MAIN WORKFLOW
# ============================================================================

def discover_and_profile_datasets() -> Dict[str, Any]:
    """Discover and profile all raw datasets."""
    logger.info("=" * 80)
    logger.info("STARTING DATA PROFILING FOR GOTHENBURG DIGITAL TWIN")
    logger.info(f"Raw data directory: {RAW_DATA_DIR}")
    logger.info("=" * 80)
    
    all_profiles: List[DatasetProfile] = []
    
    dataset_specs = [
        ("arbutbShp", "*.shp", profile_shapefile),
        ("befolkningShp", "*.shp", profile_shapefile),
        ("inkomsterShp", "*.shp", profile_shapefile),
        ("terrangVektorShp", "*.shp", profile_shapefile),
        ("vagkartaVektorShp", "*.shp", profile_shapefile),
        ("fastighetMarkShp", "*.shp", profile_shapefile),
        ("byggnad_gpkg", "*.gpkg", profile_geopackage),
        ("topografi10_gpkg", "*.gpkg", profile_geopackage),
        ("ortnamn", "*.gpkg", profile_geopackage),
        ("hojddata2m", "*.tif", profile_geotiff),
        ("laserdata_nh", "*.las", profile_las_file),
        ("laserdata_nh", "*.laz", profile_las_file),
    ]
    
    for folder, pattern, profiler in dataset_specs:
        search_path = RAW_DATA_DIR / folder
        if not search_path.exists():
            logger.warning(f"Directory not found: {search_path}")
            continue
        
        files = list(search_path.glob(pattern))
        
        for file_path in sorted(files):
            if profiler == profile_shapefile:
                profile = profile_shapefile(file_path)
                if profile:
                    all_profiles.append(profile)
            elif profiler == profile_geopackage:
                profiles = profile_geopackage(file_path)
                all_profiles.extend([p for p in profiles if p])
            elif profiler == profile_geotiff:
                profile = profile_geotiff(file_path)
                if profile:
                    all_profiles.append(profile)
            elif profiler == profile_las_file:
                profile = profile_las_file(file_path)
                if profile:
                    all_profiles.append(profile)
    
    logger.info("=" * 80)
    logger.info(f"PROFILING COMPLETE: {len(all_profiles)} dataset(s) profiled")
    logger.info("=" * 80)
    
    return {"profiles": all_profiles}

def serialize_profile_to_dict(profile: DatasetProfile) -> Dict[str, Any]:
    """Convert profile to JSON-serializable dict."""
    def convert_timestamps(obj):
        """Recursively convert pandas Timestamps to ISO strings."""
        if isinstance(obj, dict):
            return {k: convert_timestamps(v) for k, v in obj.items()}
        elif isinstance(obj, (list, tuple)):
            return [convert_timestamps(item) for item in obj]
        elif isinstance(obj, pd.Timestamp):
            return obj.isoformat()
        elif hasattr(obj, 'isoformat'):
            return obj.isoformat()
        else:
            return obj
    
    spatial_dict = asdict(profile.spatial) if profile.spatial else None
    schema_dict = asdict(profile.schema) if profile.schema else None
    quality_dict = asdict(profile.quality) if profile.quality else None
    
    return {
        "name": profile.name,
        "file_path": profile.file_path,
        "format_type": profile.format_type,
        "file_size_mb": round(profile.file_size_mb, 2),
        "creation_date": profile.creation_date,
        "row_feature_count": profile.row_feature_count,
        "spatial": convert_timestamps(spatial_dict),
        "schema": convert_timestamps(schema_dict),
        "quality": convert_timestamps(quality_dict),
        "anomalies": profile.anomalies,
        "integration_notes": profile.integration_notes
    }

def save_profiles_to_json(profiles: List[DatasetProfile]) -> None:
    """Save profiles to JSON."""
    
    for profile in profiles:
        output_file = PROFILES_DIR / f"{profile.name}_profile.json"
        with open(output_file, "w", encoding="utf-8") as f:
            json.dump(serialize_profile_to_dict(profile), f, indent=2, ensure_ascii=False)
        logger.info(f"Saved profile: {output_file}")
    
    consolidated = {
        "timestamp": datetime.now().isoformat(),
        "dataset_count": len(profiles),
        "total_profiles": [serialize_profile_to_dict(p) for p in profiles],
        "summary_statistics": {
            "total_features_across_all": sum(p.row_feature_count for p in profiles),
            "dataset_count_by_type": {
                fmt_type: len([p for p in profiles if p.format_type == fmt_type])
                for fmt_type in set(p.format_type for p in profiles)
            }
        }
    }
    
    summary_file = PROCESSED_DATA_DIR / "profile_summary.json"
    with open(summary_file, "w", encoding="utf-8") as f:
        json.dump(consolidated, f, indent=2, ensure_ascii=False)
    logger.info(f"Saved consolidated summary: {summary_file}")

def generate_markdown_report(profiles: List[DatasetProfile]) -> None:
    """Generate human-readable markdown report."""
    
    report_file = REPORTS_DIR / f"{datetime.now().strftime('%Y-%m-%d')}_dataset_profiling.md"
    
    with open(report_file, "w", encoding="utf-8") as f:
        f.write("# Gothenburg Digital Twin — Dataset Profiling Report\n\n")
        f.write(f"**Generated**: {datetime.now().isoformat()}\n\n")
        f.write(f"**Total datasets**: {len(profiles)}\n")
        f.write(f"**Total features/cells/points**: {sum(p.row_feature_count for p in profiles):,}\n\n")
        
        f.write("## Dataset Summary by Type\n\n")
        for fmt_type in set(p.format_type for p in profiles):
            matching = [p for p in profiles if p.format_type == fmt_type]
            f.write(f"- **{fmt_type}**: {len(matching)} dataset(s)\n")
        f.write("\n")
        
        f.write("## Per-Dataset Profiles\n\n")
        for profile in sorted(profiles, key=lambda p: p.name):
            f.write(f"### {profile.name}\n\n")
            f.write(f"- **File**: `{profile.file_path}`\n")
            f.write(f"- **Format**: {profile.format_type}\n")
            f.write(f"- **Size**: {profile.file_size_mb:.2f} MB\n")
            f.write(f"- **Features**: {profile.row_feature_count:,}\n")
            
            if profile.spatial:
                f.write(f"- **CRS**: {profile.spatial.crs or 'Not defined'}\n")
                if profile.spatial.geometry_types:
                    f.write(f"- **Geometry types**: {', '.join(profile.spatial.geometry_types)}\n")
            
            if profile.schema:
                f.write(f"- **Fields**: {len(profile.schema.field_names)}\n")
                for field in profile.schema.field_names[:5]:
                    field_type = profile.schema.field_types.get(field, "?")
                    f.write(f"  - `{field}` ({field_type})\n")
                if len(profile.schema.field_names) > 5:
                    f.write(f"  - ... and {len(profile.schema.field_names) - 5} more\n")
            
            if profile.anomalies:
                f.write(f"- **⚠ Issues**: {'; '.join(profile.anomalies[:3])}\n")
                if len(profile.anomalies) > 3:
                    f.write(f"   ... and {len(profile.anomalies) - 3} more\n")
            
            f.write("\n")
    
    logger.info(f"Saved markdown report: {report_file}")

# ============================================================================
# MAIN
# ============================================================================

if __name__ == "__main__":
    try:
        result = discover_and_profile_datasets()
        profiles = result["profiles"]
        
        logger.info("\nGenerating output files...")
        save_profiles_to_json(profiles)
        generate_markdown_report(profiles)
        
        logger.info("\n" + "=" * 80)
        logger.info("✓ DATA PROFILING COMPLETE")
        logger.info(f"  Profiles: {PROFILES_DIR}")
        logger.info(f"  Summary: {PROCESSED_DATA_DIR / 'profile_summary.json'}")
        logger.info(f"  Report: {REPORTS_DIR}")
        logger.info("=" * 80)
        
    except Exception as e:
        logger.exception(f"Fatal error: {e}")
        exit(1)
