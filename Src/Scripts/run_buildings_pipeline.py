#!/usr/bin/env python3
"""
Run the buildings dataset pipeline.

Loads raw Swedish buildings data, validates it, translates Swedish → English,
and exports processed dataset with metadata.

Usage:
    python run_buildings_pipeline.py
    python run_buildings_pipeline.py --output custom_output_dir
    python run_buildings_pipeline.py --input path/to/custom.gpkg
"""

import argparse
import logging
import sys
from pathlib import Path
from datetime import datetime

# Add Src to path
sys.path.insert(0, str(Path(__file__).parent.parent))

from pipelines.buildings.pipeline import BuildingsPipeline
from utils.data_profiler import DataFrameProfiler

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(name)s] %(levelname)s: %(message)s'
)
logger = logging.getLogger(__name__)


def main():
    parser = argparse.ArgumentParser(
        description="Process Swedish buildings (Byggnad) dataset"
    )
    parser.add_argument(
        "--input",
        default="Raw_data/byggnad_gpkg/byggnad_sverige.gpkg",
        help="Path to input GeoPackage"
    )
    parser.add_argument(
        "--output",
        default="Processed_data",
        help="Output directory for processed data (will create dated subfolder)"
    )
    parser.add_argument(
        "--profile",
        action="store_true",
        help="Generate data profile (Markdown + HTML)"
    )
    
    args = parser.parse_args()
    
    # Generate dated folder names
    timestamp = datetime.now().strftime("%Y-%m-%d")
    processed_dir = Path(args.output) / f"buildings_{timestamp}"
    
    # Run pipeline
    config = {
        "input_gpkg": args.input,
        "output_dir": str(processed_dir),
    }
    
    pipeline = BuildingsPipeline(config=config)
    report = pipeline.run(output_dir=processed_dir)
    
    # Print report
    print("\n" + "=" * 80)
    print("BUILDINGS PIPELINE COMPLETE")
    print("=" * 80)
    print(f"Status: {report.get('status')}")
    buildings_count = pipeline.validation_report.get('total_buildings', 0)
    print(f"Buildings processed: {buildings_count:,}")
    print(f"Output directory: {args.output}")
    
    if report.get("status") == "failed":
        print(f"Error: {report.get('error')}")
        return 1
    
    # Profile if requested
    if args.profile:
        logger.info("\nGenerating data profile...")
        profiler = DataFrameProfiler(
            pipeline.data,
            "Buildings (Byggnad) - Processed"
        )
        
        # Create reports folder with dated subfolder
        reports_dir = Path("reports") / f"buildings_{timestamp}"
        reports_dir.mkdir(parents=True, exist_ok=True)
        
        profile_md = reports_dir / "buildings_profile.md"
        profile_html = reports_dir / "buildings_profile.html"
        
        profiler.save_markdown(profile_md)
        profiler.save_html(profile_html)
        
        logger.info(f"Profile saved to {profile_md}")
        logger.info(f"Interactive HTML saved to {profile_html}")
    
    print("=" * 80)
    return 0


if __name__ == "__main__":
    sys.exit(main())
