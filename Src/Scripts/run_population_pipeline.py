#!/usr/bin/env python3
"""
Run the population (SCB Befolkning) preprocess pipeline.

Loads every Tab*_<unit>_<year>.shp in the folder unchanged, checks it, and writes
HTML views of each table, a profile per table, an index page and a JSON report.
No processing rules are applied yet.

Usage (city-agnostic: pass the city's delivery folder and an output folder):
    python Src/Scripts/run_population_pipeline.py --input <befolkning folder> --output <city output folder>
    python Src/Scripts/run_population_pipeline.py --input <folder> --output <folder> --tables Tab1_Ruta Tab2

Outputs go to <output>/population_<YYYY-MM-DD>/; open index.html there. The pages
contain the licensed SCB data, so use an ignored folder such as Processed_data/.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from pipelines.population import PopulationPipeline  # noqa: E402
from pipelines.scb.cli import run_cli  # noqa: E402

if __name__ == "__main__":
    sys.exit(run_cli(PopulationPipeline, "Load, check and view SCB population (Befolkning) tables"))
