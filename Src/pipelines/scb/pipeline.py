"""
Base pipeline for an SCB shapefile delivery (population, income, ...).

    load()       every Tab*_<unit>_<year>.shp in the folder, unchanged      (loader.py)
    validate()   checks and basic facts per table and across tables         (validator.py)
    preprocess() the dataset's own preprocessor (pass-through until rules exist)
    export()     HTML views, profiles, index page, JSON report              (exporter.py)

A dataset subclasses this and sets `dataset` (its ScbDataset) and, when it has
processing rules, overrides `preprocess_tables`.

Config keys:
    input_dir  (required) delivery folder with the shapefiles
    tables     (optional) file-stem prefixes to load, e.g. ["Tab1_Ruta", "Tab2"]
"""

from __future__ import annotations

from datetime import datetime
from pathlib import Path
from typing import Dict, Optional

from pipelines.base import BasePipeline

from .config import ScbDataset
from .exporter import export_outputs
from .loader import SourceTable, load_scb_tables
from .validator import validate_tables


class ScbTablesPipeline(BasePipeline):
    """Load, check and view one SCB delivery; subclasses choose the dataset."""

    dataset: Optional[ScbDataset] = None

    def __init__(self, name: Optional[str] = None, config: dict = None, verbose: bool = True):
        if self.dataset is None:
            raise TypeError(f"{type(self).__name__} must set the class attribute `dataset`")
        super().__init__(name or self.dataset.dataset_id.capitalize(), config, verbose)
        self.tables: Dict[str, SourceTable] = {}
        self.outputs: dict = {}

    def preprocess_tables(self, tables: Dict[str, SourceTable]) -> dict:
        """Dataset-specific processing; the default leaves the tables unchanged."""
        return {
            "steps": [],
            "note": "No preprocessing rules defined yet; tables are unchanged from the source files.",
            "tables": {key: {"rows": int(len(t.data))} for key, t in tables.items()},
        }

    def load(self):
        if not self.config.get("input_dir"):
            raise ValueError(f"config['input_dir'] is required (folder with the SCB {self.dataset.dataset_id} shapefiles)")
        self.tables = load_scb_tables(Path(self.config["input_dir"]), self.config.get("tables"))
        self.data = {key: table.data for key, table in self.tables.items()}

    def validate(self) -> dict:
        return validate_tables(self.tables, self.dataset)

    def preprocess(self):
        self.preprocessing_report = self.preprocess_tables(self.tables)
        self.data = {key: table.data for key, table in self.tables.items()}

    def export(self, output_dir: Path):
        run_info = {
            "input_dir": str(Path(self.config["input_dir"]).resolve()),
            "tables_filter": self.config.get("tables"),
            "run_timestamp": datetime.now().isoformat(timespec="seconds"),
            "output_dir": str(Path(output_dir).resolve()),
        }
        self.outputs = export_outputs(
            self.tables, self.validation_report, self.preprocessing_report, output_dir, run_info, self.dataset
        )
