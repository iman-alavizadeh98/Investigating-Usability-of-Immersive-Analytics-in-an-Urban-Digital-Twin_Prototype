"""
Buildings dataset pipeline.

Loads, validates, analyzes, and preprocesses Swedish building (Byggnad) data.
Implements BasePipeline pattern for reusability with similar datasets.
"""

from pathlib import Path
import geopandas as gpd
import pandas as pd
import logging
from typing import Dict, Any
import json
import numpy as np

from pipelines.base import BasePipeline
from .config import (
    FIELD_TRANSLATIONS,
    BUILDING_TYPES,
    PURPOSES,
    PURPOSE_FIELDS,
    MAIN_BUILDING_VALUES,
    COLLECTION_LEVELS,
    DATASET_METADATA,
)
from .translation import (
    translate_collection_level_column,
    translate_object_type_column,
    translate_purpose_column,
)

logger = logging.getLogger(__name__)


class NumpyEncoder(json.JSONEncoder):
    """Custom JSON encoder to handle numpy/pandas types."""
    def default(self, obj):
        if isinstance(obj, np.integer):
            return int(obj)
        elif isinstance(obj, np.floating):
            return float(obj)
        elif isinstance(obj, np.ndarray):
            return obj.tolist()
        return super().default(obj)


class BuildingsPipeline(BasePipeline):
    """
    Pipeline for Swedish buildings (Byggnad) dataset.
    
    Workflow:
      1. Load: Read GeoPackage, preserve raw Swedish field names
      2. Validate: Check geometry, CRS, completeness
      3. Preprocess: Translate fields to English, standardize values
      4. Export: Save processed GeoPackage + metadata
    """
    
    def __init__(self, name: str = "Buildings", config: dict = None, verbose: bool = True):
        """Initialize pipeline with Swedish buildings configuration."""
        super().__init__(name, config, verbose)
        self.field_translations = FIELD_TRANSLATIONS
        self.building_types = BUILDING_TYPES
        self.purposes = PURPOSES
    
    def load(self):
        """Load raw GeoPackage preserving Swedish field names."""
        if not self.config.get("input_gpkg"):
            raise ValueError("config['input_gpkg'] is required (path to the raw Byggnad GeoPackage)")
        input_path = Path(self.config["input_gpkg"])

        if not input_path.exists():
            raise FileNotFoundError(f"Input GeoPackage not found: {input_path}")
        
        # A Byggnad delivery can hold several layers (byggnad, byggnadsanlaggningslinje,
        # ...); footprints are always the "byggnad" layer (PDF Table 2).
        layer = self.config.get("layer", DATASET_METADATA["layer"])
        self.logger.info(f"Loading buildings from {input_path} (layer {layer})")
        self.data = gpd.read_file(input_path, layer=layer)
        
        self.logger.info(f"  - Loaded {len(self.data):,} buildings")
        self.logger.info(f"  - Columns: {len(self.data.columns)} fields")
        self.logger.info(f"  - CRS: {self.data.crs}")
        self.logger.info(f"  - Geometry types: {self.data.geometry.type.unique()}")
    
    def validate(self) -> dict:
        """Validate data quality and structure."""
        report = {
            "total_buildings": len(self.data),
            "fields": len(self.data.columns),
            "crs": str(self.data.crs),
            "geometry_types": self.data.geometry.type.unique().tolist(),
            "issues": [],
        }
        
        # Check CRS
        if self.data.crs.to_string() != "EPSG:3006":
            report["issues"].append(f"CRS mismatch: expected EPSG:3006, got {self.data.crs}")
        
        # Check null geometries
        null_geom = self.data.geometry.isnull().sum()
        if null_geom > 0:
            report["issues"].append(f"Null geometries: {null_geom}")
        
        # Check invalid geometries
        invalid_geom = (~self.data.geometry.is_valid).sum()
        if invalid_geom > 0:
            report["issues"].append(f"Invalid geometries: {invalid_geom}")
        
        # Check required fields (from PDF)
        required_fields = ["objektidentitet", "objekttyp", "andamal1"]
        missing_fields = [f for f in required_fields if f not in self.data.columns]
        if missing_fields:
            report["issues"].append(f"Missing required fields: {missing_fields}")
        
        # Check field coverage
        for field in ["andamal1", "husnummer", "byggnadsnamn1"]:
            if field in self.data.columns:
                null_count = self.data[field].isna().sum()
                null_pct = (null_count / len(self.data)) * 100
                report[f"{field}_null_pct"] = null_pct

        # objektidentitet should be unique (PDF Table 5). Repeated IDs are
        # resolved by the postprocess step; here they are only counted.
        if "objektidentitet" in self.data.columns:
            id_counts = self.data["objektidentitet"].value_counts()
            repeated = id_counts[id_counts > 1]
            report["repeated_object_ids"] = int(len(repeated))
            report["rows_with_repeated_object_id"] = int(repeated.sum())
            if len(repeated):
                report["issues"].append(
                    f"{len(repeated)} object IDs appear on more than one row "
                    f"({int(repeated.sum())} rows); see postprocess report"
                )

        # The object-type part of andamal1 should equal objekttyp.
        if {"objekttyp", "andamal1"}.issubset(self.data.columns):
            purpose_type = self.data["andamal1"].astype("string").str.split(";").str[0].str.strip()
            mismatch = int((purpose_type.notna() & (purpose_type != self.data["objekttyp"])).sum())
            report["andamal1_type_mismatch_rows"] = mismatch
            if mismatch:
                report["issues"].append(f"andamal1 object type differs from objekttyp on {mismatch} rows")

        self.validation_report = report
        return report
    
    def preprocess(self):
        """
        Translate Swedish → English and standardize.
        
        Steps:
        1. Rename fields to English using translations
        2. Translate building type values
        3. Standardize purpose categories
        4. Clean and validate
        """
        self.logger.info("Translating field names (Swedish → English)")
        
        # Rename fields that have translations
        rename_map = {}
        for sv_field, en_field in self.field_translations.items():
            if sv_field in self.data.columns:
                rename_map[sv_field] = en_field
        
        self.data = self.data.rename(columns=rename_map)
        self.logger.info(f"  - Renamed {len(rename_map)} fields")
        
        # Original Swedish values stay in their columns; English values are
        # added as new columns. Values missing from config.py are kept as-is in
        # the *_en column and listed in preprocessing_report["unmatched_values"].
        unmatched = {}

        # Translate object_type values (Bostad → Residence, etc.)
        if "object_type" in self.data.columns:
            self.logger.info("Translating building types")
            english, category, missing = translate_object_type_column(self.data["object_type"])
            self.data["object_type_en"] = english
            self.data["object_type_category"] = category
            unmatched["object_type"] = missing

        # Translate purpose values ("Samhällsfunktion;Sjukhus" → "Hospital")
        for field in PURPOSE_FIELDS:
            if field not in self.data.columns:
                continue
            self.logger.info(f"Translating {field}")
            english, category, missing = translate_purpose_column(self.data[field])
            self.data[f"{field}_en"] = english
            if field == "primary_purpose":
                self.data["primary_purpose_category"] = category
            unmatched[field] = missing

        # Translate collection level (case-insensitive: data is "Fasad", PDF is "fasad")
        if "collection_level" in self.data.columns:
            self.logger.info("Translating collection levels")
            english, missing = translate_collection_level_column(self.data["collection_level"])
            self.data["collection_level_en"] = english
            unmatched["collection_level"] = missing

        # Standardize boolean fields; the raw "Ja"/"Nej" stays in main_building_flag_sv
        if "main_building_flag" in self.data.columns:
            raw = self.data["main_building_flag"]
            self.data["main_building_flag_sv"] = raw
            self.data["main_building_flag"] = raw.map(MAIN_BUILDING_VALUES)
            unknown = raw[raw.notna() & ~raw.isin(list(MAIN_BUILDING_VALUES))]
            unmatched["main_building_flag"] = unknown.astype(str).value_counts().to_dict()

        # Footprint area in m² (CRS is EPSG:3006, metres; checked in validate()).
        self.data["footprint_area_m2"] = self.data.geometry.area.round(2)

        unmatched = {field: dict(values) for field, values in unmatched.items() if values}
        for field, values in unmatched.items():
            self.logger.warning(
                f"  - {field}: {sum(values.values())} rows with values not in config.py: {values}"
            )
        self.preprocessing_report["unmatched_values"] = unmatched

        self.logger.info("Preprocessing complete")
    
    def export(self, output_dir: Path):
        """
        Export processed dataset with metadata.
        
        Outputs:
        - GeoPackage: processed buildings with English field names
        - Metadata JSON: translation mappings and dataset info
        - Summary JSON: statistics about the processed dataset
        """
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Export GeoPackage
        output_gpkg = output_dir / "buildings_processed.gpkg"
        self.logger.info(f"Exporting to {output_gpkg}")
        self.data.to_file(output_gpkg, layer="buildings")
        
        # Export metadata
        metadata = {
            "dataset": DATASET_METADATA,
            "field_translations": self.field_translations,
            "building_types": {k: v for k, v in self.building_types.items()},
            "purposes": {k: v for k, v in self.purposes.items()},
            "collection_levels": {k: v for k, v in COLLECTION_LEVELS.items()},
            "validation_report": self.validation_report,
            "unmatched_values": self.preprocessing_report.get("unmatched_values", {}),
        }
        
        metadata_file = output_dir / "buildings_metadata.json"
        with open(metadata_file, "w", encoding="utf-8") as f:
            json.dump(metadata, f, indent=2, ensure_ascii=False, cls=NumpyEncoder)
        self.logger.info(f"Exported metadata to {metadata_file}")
        
        # Export summary statistics
        summary = {
            "total_buildings": len(self.data),
            "columns": len(self.data.columns),
            "crs": str(self.data.crs),
            "geometry_valid": (~self.data.geometry.isnull() & self.data.geometry.is_valid).sum(),
            "fields_translated": len(self.field_translations),
        }
        
        if "object_type_en" in self.data.columns:
            summary["building_types_distribution"] = (
                self.data["object_type_en"].value_counts().to_dict()
            )
        
        if "primary_purpose_category" in self.data.columns:
            summary["purpose_categories"] = (
                self.data["primary_purpose_category"].value_counts().to_dict()
            )

        if "primary_purpose_en" in self.data.columns:
            summary["primary_purposes"] = (
                self.data["primary_purpose_en"].value_counts().to_dict()
            )

        summary["unmatched_values"] = self.preprocessing_report.get("unmatched_values", {})
        
        summary_file = output_dir / "buildings_summary.json"
        with open(summary_file, "w", encoding="utf-8") as f:
            json.dump(summary, f, indent=2, ensure_ascii=False, cls=NumpyEncoder)
        self.logger.info(f"Exported summary to {summary_file}")
