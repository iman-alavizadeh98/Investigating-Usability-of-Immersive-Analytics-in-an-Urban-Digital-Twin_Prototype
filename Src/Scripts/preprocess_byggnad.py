#!/usr/bin/env python3
"""
preprocess_byggnad.py
Thesis-grade preprocessing pipeline for Byggnad.gpkg

This script implements 8 stages of preprocessing:
  A - Data ingestion (preserve source fidelity)
  B - Schema harmonization (rename to English)
  C - Data quality assessment (measure before transform)
  D - Geometry validation and repair
  E - Geometry normalization (CRS, multipart handling)
  F - Semantic normalization (standardize values)
  G - Feature derivation (compute useful attributes)
  H - Export and provenance (save outputs + reports)

Methodology principle: Assess first, transform second, derive third, export last.
"""

import geopandas as gpd
import pandas as pd
import json
import os
from datetime import datetime
from pathlib import Path
from shapely.geometry import Point
from shapely.ops import unary_union

# ============================================================================
# JSON ENCODER FOR NUMPY TYPES
# ============================================================================
class NumpyEncoder(json.JSONEncoder):
    """
    Handle numpy and pandas types in JSON serialization.
    Convert pandas timestamps and numpy scalar types into JSON-serializable values

    """
    def default(self, obj):
        if isinstance(obj, (pd.Timestamp, pd.Timedelta)):
            return obj.isoformat()
        if hasattr(obj, 'item'):  # numpy scalars
            return obj.item()
        return super().default(obj)

# ============================================================================
# CONFIGURATION
# ============================================================================
INPUT_GPKG = "Raw_data/byggnad_gpkg/byggnad_sverige.gpkg"
OUTPUT_DIR = "Processed_data"
TARGET_CRS = "EPSG:3006"
EXPLODE_MULTIPOLYGONS = True
ASSUMED_HEIGHT_M = 10.0

# Schema for translating Swedish column names to English
RENAME_DICT = {
    "objektidentitet": "object_id",
    "versiongiltigfran": "valid_from",
    "lagesosakerhetplan": "horizontal_uncertainty",
    "lagesosakerhethojd": "vertical_uncertainty",
    "ursprunglig_organisation": "source_organization",
    "objektversion": "object_version",
    "objekttypnr": "object_type_id",
    "objekttyp": "object_type",
    "insamlingslage": "survey_position",
    "byggnadsnamn1": "building_name",
    "byggnadsnamn2": "building_name_alt",
    "byggnadsnamn3": "building_name_third",
    "husnummer": "house_number",
    "huvudbyggnad": "is_main_building",
    "andamal1": "primary_usage",
    "andamal2": "secondary_usage",
    "andamal3": "tertiary_usage",
    "andamal4": "usage_4",
    "andamal5": "usage_5",
}

# ============================================================================
# DATA IMPORT
# ============================================================================
def import_raw_buildings(path: str) -> tuple:
    """
    Load source GeoPackage exactly as delivered.
    Preserve raw dataframe and record ingestion metadata.
    
    Returns:
      (buildings_raw: GeoDataFrame, ingestion_report: dict)
    """
    print("\n" + "="*70)
    print("DATA IMPORT")
    print("="*70)
    
    if not os.path.exists(path):
        raise FileNotFoundError(f"Input GeoPackage not found: {path}")
    
    print(f"Loading: {path}")
    buildings_raw = gpd.read_file(path)
    
    ingestion_report = {
        "timestamp": datetime.now().isoformat(),
        "source_path": path,
        "source_file": os.path.basename(path),
        "crs": str(buildings_raw.crs),
        "row_count": len(buildings_raw),
        "column_count": len(buildings_raw.columns),
        "column_names": buildings_raw.columns.tolist(),
        "geometry_types": buildings_raw.geometry.type.unique().tolist(),
        "geometry_type_counts": buildings_raw.geometry.type.value_counts().to_dict(),
        "dataset_info": str(buildings_raw.info()),
        "sample_rows": buildings_raw.head(5).to_dict(orient="records"),
    }
    
    print(f" Loaded {len(buildings_raw)} features")
    print(f" CRS: {buildings_raw.crs}")
    print(f" Columns: {len(buildings_raw.columns)}")
    print(f" Geometry types: {ingestion_report['geometry_types']}")
    
    return buildings_raw, ingestion_report


# ============================================================================
# DATASET TRANSLATION TO ENGLISH
# ============================================================================
def translate_columns_to_english(gdf: gpd.GeoDataFrame) -> tuple:
    """
    Translate all Swedish columns to English using RENAME_DICT.
    
    Returns:
      (buildings_harmonized: GeoDataFrame, rename_metadata: dict)
    """
    print("\n" + "="*70)
    print("DATASET TRANSLATION TO ENGLISH")
    print("="*70)
    
    # Apply all translations from RENAME_DICT
    print(f"Translating {len(gdf.columns)} columns to English...")
    gdf = gdf.rename(columns=RENAME_DICT)
    
    # Identify which columns were translated
    translated = [col for col in RENAME_DICT.values() if col in gdf.columns]
    remaining_swedish = [col for col in gdf.columns if col in RENAME_DICT.keys()]
    unrenamed = [col for col in gdf.columns if col not in RENAME_DICT.values() and col not in RENAME_DICT.keys()]
    
    rename_metadata = {
        "timestamp": datetime.now().isoformat(),
        "total_columns": len(gdf.columns),
        "translated_columns": len(translated),
        "unrenamed_columns": unrenamed,
    }
    
    print(f"Translated {len(translated)} columns")
    if unrenamed:
        print(f"{len(unrenamed)} columns remain in Swedish: {unrenamed}")
    
    return gdf, rename_metadata


# ============================================================================
# DATA QUALITY ASSESSMENT
# ============================================================================
def assess_data_quality(gdf: gpd.GeoDataFrame) -> dict:
    """
    Assess data quality before any transformations.
    Check: missingness, uniqueness, domain consistency, geometry quality.
    
    Returns:
      quality_report: dict
    """
    print("\n" + "="*70)
    print("DATA QUALITY ASSESSMENT")
    print("="*70)
    
    quality_report = {
        "timestamp": datetime.now().isoformat(),
        "total_features": len(gdf),
        "missingness": {},
        "uniqueness": {},
        "geometry_quality": {},
        "domain_consistency": {},
    }
    
    # ── Missingness ──
    print("\nChecking missingness...")
    missing_geom = gdf.geometry.isna().sum()
    empty_geom = gdf.geometry.is_empty.sum()
    missing_object_id = gdf["object_id"].isna().sum() if "object_id" in gdf.columns else 0
    
    quality_report["missingness"] = {
        "missing_geometry": int(missing_geom),
        "empty_geometry": int(empty_geom),
        "missing_object_id": int(missing_object_id),
    }
    
    for col in ["building_name", "primary_usage", "is_main_building"]:
        if col in gdf.columns:
            quality_report["missingness"][f"missing_{col}"] = int(gdf[col].isna().sum())
    
    print(f"  Missing geometries: {missing_geom}")
    print(f"  Empty geometries: {empty_geom}")
    print(f"  Missing object_ids: {missing_object_id}")
    
    # ── Uniqueness ──
    print("\nChecking uniqueness...")
    if "object_id" in gdf.columns:
        duplicate_ids = (gdf["object_id"].duplicated()).sum()
        quality_report["uniqueness"]["duplicate_object_ids"] = int(duplicate_ids)
        print(f"  Duplicate object_ids: {duplicate_ids}")
    
    # ── Geometry Quality ──
    print("\nChecking geometry quality...")
    invalid_geom = (~gdf.geometry.is_valid).sum()
    quality_report["geometry_quality"] = {
        "invalid_geometries": int(invalid_geom),
        "geometry_types": gdf.geometry.type.value_counts().to_dict(),
    }
    print(f"  Invalid geometries: {invalid_geom}")
    print(f"  Geometry type distribution: {gdf.geometry.type.value_counts().to_dict()}")
    
    return quality_report


# ============================================================================
# GEOMETRY VALIDATION AND REPAIR
# ============================================================================
def validate_and_repair_geometries(gdf: gpd.GeoDataFrame) -> tuple:
    """
    Validate geometries and normalize ALL geometries using buffer(0).
    
    This unconditionally rebuilds all geometries for:
    - Consistent normalization across dataset
    - Simplification of multipart geometries where valid
    - Resolution of topological issues that may not be detected by is_valid()
    
    Returns:
      (buildings_validated: GeoDataFrame, repair_report: dict)
    """
    print("\n" + "="*70)
    print("GEOMETRY VALIDATION AND REPAIR")
    print("="*70)
    
    invalid_before = (~gdf.geometry.is_valid).sum()
    multipolygon_before = (gdf.geometry.type == "MultiPolygon").sum()
    
    print(f"Before buffer(0) rebuild:")
    print(f"  Invalid geometries: {invalid_before}")
    print(f"  MultiPolygons: {multipolygon_before}")
    print(f"  Polygons: {(gdf.geometry.type == 'Polygon').sum()}")
    
    # Apply buffer(0) to ALL geometries for consistent normalization
    print("\nApplying buffer(0) to rebuild all geometries...")
    gdf["geometry"] = gdf["geometry"].buffer(0)
    
    invalid_after = (~gdf.geometry.is_valid).sum()
    multipolygon_after = (gdf.geometry.type == "MultiPolygon").sum()
    
    print(f"\nAfter buffer(0) rebuild:")
    print(f"  Invalid geometries: {invalid_after}")
    print(f"  MultiPolygons: {multipolygon_after}")
    print(f"  Polygons: {(gdf.geometry.type == 'Polygon').sum()}")
    
    repair_report = {
        "timestamp": datetime.now().isoformat(),
        "invalid_before": int(invalid_before),
        "invalid_after": int(invalid_after),
        "multipolygon_before": int(multipolygon_before),
        "multipolygon_after": int(multipolygon_after),
        "repair_applied": True,
        "repair_method": "buffer(0) - unconditional rebuild",
        "repair_rationale": "Normalize all geometries, simplify multiparts, ensure consistency",
    }
    
    return gdf, repair_report


# ============================================================================
# GEOMETRY AND CRS NORMALIZATION
# ============================================================================
def normalize_geometry_and_crs(gdf: gpd.GeoDataFrame) -> tuple:
    """
    Normalize CRS and geometry representation.
    Optionally explode multipolygons with tracking.
    
    Returns:
      (buildings_normalized: GeoDataFrame, normalization_report: dict)
    """
    print("\n" + "="*70)
    print("GEOMETRY and CRS NORMALIZATION")
    print("="*70)
    
    normalization_report = {
        "timestamp": datetime.now().isoformat(),
        "crs_before": str(gdf.crs),
        "crs_after": TARGET_CRS,
        "multipolygon_count_before": int((gdf.geometry.type == "MultiPolygon").sum()),
    }
    
    # ── CRS Normalization ──
    if gdf.crs != TARGET_CRS:
        print(f"Reprojecting to {TARGET_CRS}...")
        gdf = gdf.to_crs(TARGET_CRS)
    else:
        print(f"CRS already {TARGET_CRS}")
    
    # ── Multipolygon Handling ──
    multipolygon_count = (gdf.geometry.type == "MultiPolygon").sum()
    normalization_report["multipolygon_count_before"] = int(multipolygon_count)
    

    # Explode multipolygons if needed
    # In my data I only have 4 multipolygons, so I will not explode them.
    # but I will keep the code here for future reference.
    '''
    if EXPLODE_MULTIPOLYGONS and multipolygon_count > 0:
        print(f"Exploding {multipolygon_count} multipolygons...")
        
        # Preserve object_id, add part_index
        gdf["part_index"] = 0
        gdf = gdf.explode(index_drop=True)
        
        # Recalculate part indices by object_id
        if "object_id" in gdf.columns:
            gdf["part_index"] = gdf.groupby("object_id").cumcount()
        
        print(f"After explode: {len(gdf)} geometries")
    
    normalization_report["multipolygon_count_after"] = int((gdf.geometry.type == "MultiPolygon").sum())
    normalization_report["row_count_after_explode"] = len(gdf)
    '''
    return gdf, normalization_report


# ============================================================================
# SEMANTIC NORMALIZATION
# ============================================================================
def normalize_building_semantics(gdf: gpd.GeoDataFrame) -> tuple:
    """
    Standardize all categorical, boolean, and text values to English.
    Converts all Swedish values to their English equivalents.
    
    Returns:
      (buildings_semantic: GeoDataFrame, semantic_report: dict)
    """
    print("\n" + "="*70)
    print("SEMANTIC NORMALIZATION")
    print("="*70)
    
    semantic_report = {
        "timestamp": datetime.now().isoformat(),
        "transformations": [],
    }
    
    # ── Translate object_type (Building category) ──
    object_type_map = {
        "Bostad": "Housing",
        "Ekonomibyggnad": "Agricultural/Utility Building",
        "Industri": "Industrial",
        "Komplementbyggnad": "Complementary Building",
        "Samhällsfunktion": "Public Service",
        "Verksamhet": "Business/Commercial",
        "Övrig byggnad": "Other Building",
    }
    if "object_type" in gdf.columns:
        gdf["object_type"] = gdf["object_type"].map(object_type_map).fillna(gdf["object_type"])
        semantic_report["transformations"].append({
            "field": "object_type",
            "translation_count": gdf["object_type"].notna().sum(),
            "translation_map": object_type_map,
        })
        print(f"✓ Translated object_type to English ({len(object_type_map)} mappings)")
    
    # ── Translate survey_position (Capture method) ──
    survey_position_map = {
        "Fasad": "Facade",
        "Takkant": "Roof edge",
        "Illustrativt läge": "Illustrative position",
        "Ospecificerad": "Unspecified",
    }
    if "survey_position" in gdf.columns:
        gdf["survey_position"] = gdf["survey_position"].map(survey_position_map).fillna(gdf["survey_position"])
        semantic_report["transformations"].append({
            "field": "survey_position",
            "translation_count": gdf["survey_position"].notna().sum(),
            "translation_map": survey_position_map,
        })
        print(f"✓ Translated survey_position to English ({len(survey_position_map)} mappings)")
    
    # ── Translate is_main_building (Yes/No to True/False) ──
    if "is_main_building" in gdf.columns:
        original_unique = gdf["is_main_building"].dropna().unique()
        is_main_map = {
            "Ja": True,
            "Nej": False,
        }
        gdf["is_main_building"] = gdf["is_main_building"].map(is_main_map)
        semantic_report["transformations"].append({
            "field": "is_main_building",
            "original_values": list(original_unique),
            "translation_map": is_main_map,
        })
        print(f"✓ Translated is_main_building to boolean (Ja→True, Nej→False)")
    
    # ── Translate usage fields (complex hierarchical values with Swedish terms) ──
    # These contain format like "Category;Subcategory" with Swedish terms
    usage_term_map = {
        "Bostad": "Housing",
        "Flerfamiljshus": "Multi-family house",
        "Småhus friliggande": "Single-family house detached",
        "Småhus kedjehus": "Single-family house townhouse",
        "Småhus med flera lägenheter": "Single-family house multi-unit",
        "Småhus radhus": "Single-family house terraced",
        "Ekonomibyggnad": "Agricultural/Utility Building",
        "Industri": "Industrial",
        "Tillverkning": "Manufacturing",
        "Komplementbyggnad": "Complementary Building",
        "Samhällsfunktion": "Public Service",
        "Badhus": "Swimming pool",
        "Brandstation": "Fire station",
        "Busstation": "Bus station",
        "Djursjukhus": "Animal hospital",
        "Högskola": "University",
        "Ishall": "Ice hall",
        "Järnvägsstation": "Railway station",
        "Kommunhus": "Municipality office",
        "Kriminalvårdsanstalt": "Prison",
        "Kulturbyggnad": "Cultural building",
        "Multiarena": "Multi-arena",
        "Polisstation": "Police station",
        "Samfund": "Community center",
        "Skola": "School",
        "Sporthall": "Sports hall",
        "Universitet": "University",
        "Vårdcentral": "Health center",
        "Verksamhet": "Business/Commercial",
        "Ospecificerad": "Unspecified",
    }
    
    usage_fields = ["primary_usage", "secondary_usage", "tertiary_usage"]
    for field in usage_fields:
        if field in gdf.columns:
            def translate_usage(val):
                if pd.isna(val) or val == "":
                    return val
                # Split by semicolon and translate each part
                parts = str(val).split(";")
                translated_parts = [usage_term_map.get(part.strip(), part.strip()) for part in parts]
                return ";".join(translated_parts)
            
            gdf[field] = gdf[field].apply(translate_usage)
            semantic_report["transformations"].append({
                "field": field,
                "method": "hierarchical translation (Category;Subcategory)",
                "terms_mapped": len(usage_term_map),
            })
            print(f"✓ Translated {field} values to English")
    
    # ── Strip whitespace from all text fields ──
    text_fields = ["building_name", "building_name_alt", "building_name_third",
                   "primary_usage", "secondary_usage", "tertiary_usage"]
    for field in text_fields:
        if field in gdf.columns:
            gdf[field] = gdf[field].str.strip() if gdf[field].dtype == "object" else gdf[field]
    
    print(f"✓ Stripped whitespace from text fields")
    print(f"✓ All semantic values normalized to English")
    
    return gdf, semantic_report


# ============================================================================
# STAGE G: FEATURE DERIVATION
# ============================================================================
def stage_g_derive_features(gdf: gpd.GeoDataFrame) -> tuple:
    """
    Create derived attributes useful for analysis and visualization.
    Includes: area, perimeter, centroid, representative_point, bbox, vertex_count.
    
    Returns:
      (buildings_enriched: GeoDataFrame, derivation_report: dict)
    """
    print("\n" + "="*70)
    print("STAGE G: FEATURE DERIVATION")
    print("="*70)
    
    derivation_report = {
        "timestamp": datetime.now().isoformat(),
        "derived_fields": [],
    }
    
    # ── Footprint Area ──
    print("Computing footprint_area_m2...")
    gdf["footprint_area_m2"] = gdf.geometry.area
    derivation_report["derived_fields"].append("footprint_area_m2")
    
    # ── Perimeter ──
    print("Computing perimeter_m...")
    gdf["perimeter_m"] = gdf.geometry.length
    derivation_report["derived_fields"].append("perimeter_m")
    
    # ── Centroid ──
    print("Computing centroid...")
    gdf["centroid"] = gdf.geometry.centroid
    derivation_report["derived_fields"].append("centroid")
    
    # ── Representative Point (better for labels) ──
    print("Computing representative_point...")
    gdf["representative_point"] = gdf.geometry.representative_point()
    derivation_report["derived_fields"].append("representative_point")
    
    # ── Bounding Box ──
    print("Computing bbox...")
    gdf["bbox"] = gdf.geometry.apply(
        lambda geom: [geom.bounds[0], geom.bounds[1], geom.bounds[2], geom.bounds[3]]
    )
    derivation_report["derived_fields"].append("bbox")
    
    # ── Vertex Count ──
    print("Computing vertex_count...")
    gdf["vertex_count"] = gdf.geometry.apply(
        lambda geom: len(geom.exterior.coords) if hasattr(geom, "exterior") else 0
    )
    derivation_report["derived_fields"].append("vertex_count")
    
    # ── Assumed Height (placeholder for later LiDAR) ──
    print(f"Adding assumed_height_m = {ASSUMED_HEIGHT_M}m...")
    gdf["assumed_height_m"] = ASSUMED_HEIGHT_M
    derivation_report["derived_fields"].append("assumed_height_m")
    
    # ── Internal Processing ID ──
    print("Assigning internal_id...")
    gdf["internal_id"] = [f"bld_{i:06d}" for i in range(len(gdf))]
    derivation_report["derived_fields"].append("internal_id")
    
    print(f"✓ Derived {len(derivation_report['derived_fields'])} fields")
    
    return gdf, derivation_report


# ============================================================================
# STAGE H: EXPORT AND PROVENANCE
# ============================================================================
def stage_h_export_outputs(
    gdf: gpd.GeoDataFrame,
    ingestion_report: dict,
    quality_report: dict,
    repair_report: dict,
    normalization_report: dict,
    semantic_report: dict,
    derivation_report: dict,
    schema_mapping: dict,
    output_dir: str,
) -> None:
    """
    Export processed data and comprehensive metadata.
    Save: GeoPackage, GeoJSON, quality report, schema mapping, preprocessing metadata.
    """
    print("\n" + "="*70)
    print("STAGE H: EXPORT AND PROVENANCE")
    print("="*70)
    
    os.makedirs(output_dir, exist_ok=True)
    
    # ── Export Processed GeoPackage ──
    gpkg_path = os.path.join(output_dir, "byggnad_processed.gpkg")
    print(f"Exporting GeoPackage: {gpkg_path}")
    
    # Convert derived geometry columns to WKT to avoid multi-geometry conflict
    gdf_export_gpkg = gdf.copy()
    if "centroid" in gdf_export_gpkg.columns:
        gdf_export_gpkg["centroid_wkt"] = gdf_export_gpkg["centroid"].to_wkt()
        gdf_export_gpkg = gdf_export_gpkg.drop(columns=["centroid"])
    if "representative_point" in gdf_export_gpkg.columns:
        gdf_export_gpkg["representative_point_wkt"] = gdf_export_gpkg["representative_point"].to_wkt()
        gdf_export_gpkg = gdf_export_gpkg.drop(columns=["representative_point"])
    
    gdf_export_gpkg.to_file(gpkg_path, driver="GPKG")
    
    # ── Export GeoJSON (inspection) ──
    geojson_path = os.path.join(output_dir, "byggnad_processed.geojson")
    print(f"Exporting GeoJSON: {geojson_path}")
    # Remove geometry-derived columns for readability
    gdf_export = gdf.copy()
    cols_to_drop = ["centroid", "representative_point"]
    cols_to_drop = [c for c in cols_to_drop if c in gdf_export.columns]
    if cols_to_drop:
        gdf_export = gdf_export.drop(columns=cols_to_drop)
    gdf_export.to_file(geojson_path, driver="GeoJSON")
    
    # ── Combine All Reports ──
    all_reports = {
        "timestamp": datetime.now().isoformat(),
        "pipeline_version": "1.0",
        "stages": {
            "A_ingestion": ingestion_report,
            "B_schema_harmonization": schema_mapping,
            "C_quality_assessment": quality_report,
            "D_geometry_validation": repair_report,
            "E_geometry_normalization": normalization_report,
            "F_semantic_normalization": semantic_report,
            "G_feature_derivation": derivation_report,
        },
        "final_row_count": len(gdf),
        "final_columns": gdf.columns.tolist(),
    }
    
    # ── Export Quality Report ──
    quality_report_path = os.path.join(output_dir, "preprocessing_quality_report.json")
    print(f"Exporting quality report: {quality_report_path}")
    with open(quality_report_path, "w") as f:
        json.dump(all_reports, f, indent=2, cls=NumpyEncoder)
    
    # ── Export Schema Mapping ──
    schema_mapping_path = os.path.join(output_dir, "schema_mapping.json")
    print(f"Exporting schema mapping: {schema_mapping_path}")
    with open(schema_mapping_path, "w") as f:
        json.dump(schema_mapping, f, indent=2, cls=NumpyEncoder)
    
    # ── Summary Statistics ──
    summary = {
        "timestamp": datetime.now().isoformat(),
        "total_buildings": len(gdf),
        "footprint_area_m2_min": float(gdf["footprint_area_m2"].min()),
        "footprint_area_m2_max": float(gdf["footprint_area_m2"].max()),
        "footprint_area_m2_mean": float(gdf["footprint_area_m2"].mean()),
        "bbox": [
            float(gdf.total_bounds[0]),
            float(gdf.total_bounds[1]),
            float(gdf.total_bounds[2]),
            float(gdf.total_bounds[3]),
        ],
        "crs": gdf.crs.to_string(),
    }
    
    summary_path = os.path.join(output_dir, "preprocessing_summary.json")
    print(f"Exporting summary: {summary_path}")
    with open(summary_path, "w") as f:
        json.dump(summary, f, indent=2, cls=NumpyEncoder)
    
    print(f"\n✓ All outputs exported to: {output_dir}")
    print(f"  - byggnad_processed.gpkg")
    print(f"  - byggnad_processed.geojson")
    print(f"  - preprocessing_quality_report.json")
    print(f"  - schema_mapping.json")
    print(f"  - preprocessing_summary.json")


# ============================================================================
# MAIN PIPELINE
# ============================================================================
def main():
    """Run the complete thesis-grade preprocessing pipeline."""
    print("\n" + "="*70)
    print("BYGGNAD PREPROCESSING PIPELINE - THESIS-GRADE")
    print("="*70)
    print(f"Start time: {datetime.now().isoformat()}")
    
    try:
        # Stage A: Ingestion
        buildings_raw, ingestion_report = import_raw_buildings(INPUT_GPKG)
        
        # Stage B: Schema Harmonization
        buildings_schema, schema_mapping = translate_columns_to_english(buildings_raw)
        
        # Stage C: Quality Assessment
        quality_report = assess_data_quality(buildings_schema)
        
        # Stage D: Geometry Validation & Repair
        buildings_valid, repair_report = validate_and_repair_geometries(buildings_schema)
        
        # Stage E: Geometry Normalization
        buildings_geom, normalization_report = normalize_geometry_and_crs(buildings_valid)
        
        # Stage F: Semantic Normalization
        buildings_sem, semantic_report = normalize_building_semantics(buildings_geom)
        
        # Stage G: Feature Derivation
        buildings_final, derivation_report = stage_g_derive_features(buildings_sem)
        
        # Stage H: Export & Provenance
        stage_h_export_outputs(
            buildings_final,
            ingestion_report,
            quality_report,
            repair_report,
            normalization_report,
            semantic_report,
            derivation_report,
            schema_mapping,
            OUTPUT_DIR,
        )
        
        print("\n" + "="*70)
        print("PIPELINE COMPLETE ✓")
        print("="*70)
        print(f"End time: {datetime.now().isoformat()}")
        print(f"\nOutput location: {OUTPUT_DIR}")
        print("\nReady for next stage: Mesh generation")
        
    except Exception as e:
        print(f"\n❌ PIPELINE FAILED: {e}")
        raise


if __name__ == "__main__":
    main()
