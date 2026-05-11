#!/usr/bin/env python3
"""
Example: Profile a DataFrame with the DataFrameProfiler

Shows how to use the profiler to understand data structure and quality.
"""

import logging
from pathlib import Path
import geopandas as gpd
import sys

# Add Src to path
sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.data_profiler import DataFrameProfiler

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def main():
    # Load dataset
    logger.info("Loading dataset...")
    gdf = gpd.read_file("Processed_data/byggnad_processed.gpkg")
    
    # Profile
    logger.info("Profiling dataset...")
    profiler = DataFrameProfiler(gdf, "Byggnad (Swedish Buildings)")
    
    # Generate Markdown report
    logger.info("Generating Markdown profile...")
    profiler.save_markdown(Path("reports/data-quality/byggnad_profile.md"))
    
    # Generate HTML report
    logger.info("Generating HTML profile...")
    profiler.save_html(Path("reports/data-quality/byggnad_profile.html"))
    
    # Print summary to console
    print("\n" + "=" * 80)
    print("DATASET PROFILE")
    print("=" * 80)
    print(profiler.to_markdown())
    
    return 0


if __name__ == "__main__":
    sys.exit(main())
