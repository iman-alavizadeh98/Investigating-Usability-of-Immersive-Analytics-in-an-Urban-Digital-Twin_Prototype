"""
Election preprocess pipeline.

    load()       results workbook + district boundaries, filtered to one municipality   (loader.py)
    validate()   consistency checks, boundary checks, results ↔ boundaries join          (validator.py)
    preprocess() no rules yet: frames pass through unchanged                             (preprocessor.py)
    export()     HTML views, profiles, index page, JSON report                           (exporter.py)

Config keys:
    input_dir     (required) folder with one results .xlsx and one boundary file
    municipality  (required) 4-digit municipality code (Kommunkod), e.g. from the city's config
"""

from __future__ import annotations

from datetime import datetime
from pathlib import Path
from typing import Optional

from pipelines.base import BasePipeline

from .exporter import export_outputs
from .loader import ElectionSource, load_election
from .preprocessor import preprocess_election
from .validator import validate_election


class ElectionPipeline(BasePipeline):
    """Load, check and view one municipality's election results and voting districts."""

    def __init__(self, name: str = "Election", config: dict = None, verbose: bool = True):
        super().__init__(name, config, verbose)
        self.source: Optional[ElectionSource] = None
        self.outputs: dict = {}

    def load(self):
        for key in ("input_dir", "municipality"):
            if not self.config.get(key):
                raise ValueError(f"config['{key}'] is required")
        self.source = load_election(Path(self.config["input_dir"]), str(self.config["municipality"]))
        self.data = {**self.source.sheets, "districts": self.source.districts}

    def validate(self) -> dict:
        return validate_election(self.source)

    def preprocess(self):
        self.preprocessing_report = preprocess_election(self.source)
        self.data = {**self.source.sheets, "districts": self.source.districts}

    def export(self, output_dir: Path):
        run_info = {
            "input_dir": str(Path(self.config["input_dir"]).resolve()),
            "municipality": str(self.config["municipality"]),
            "run_timestamp": datetime.now().isoformat(timespec="seconds"),
            "output_dir": str(Path(output_dir).resolve()),
        }
        self.outputs = export_outputs(self.source, self.validation_report, self.preprocessing_report, output_dir, run_info)
