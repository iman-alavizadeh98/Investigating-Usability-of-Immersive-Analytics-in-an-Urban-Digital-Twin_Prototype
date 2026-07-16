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
from pipelines.buildings.config import FIELD_TRANSLATIONS
from pipelines.buildings.postprocess import build_postprocess_snapshot
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
    parser.add_argument(
        "--snapshot-latest",
        action="store_true",
        help="Deprecated: use --postprocess"
    )
    parser.add_argument(
        "--postprocess",
        action="store_true",
        help="Create postprocess snapshot in a separate output folder"
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
    
    column_descriptions = {
        en: f"Swedish source: {sv}"
        for sv, en in FIELD_TRANSLATIONS.items()
    }
    column_descriptions.update({
        "object_type_en": "English label for object_type",
        "object_type_category": "High-level category for object_type",
        "primary_purpose_en": "English label for primary_purpose",
        "primary_purpose_category": "High-level category for primary_purpose",
        "collection_level_en": "English label for collection_level",
        "main_building_flag": "Main building flag (converted to boolean)"
    })

    # Profile base dataset if requested
    if args.profile:
        logger.info("\nGenerating base data profile...")
        profiler = DataFrameProfiler(
            pipeline.data,
            "Buildings (Byggnad) - Processed",
            column_descriptions=column_descriptions,
            key_columns=["object_id"]
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

    # Optional latest-only snapshot
    if args.postprocess or args.snapshot_latest:
        snapshot_dir = Path(args.output) / f"buildings_{timestamp}_postprocess"
        logger.info("\nCreating postprocess snapshot...")
        latest_gdf, snapshot_report = build_postprocess_snapshot(
            pipeline.data,
            snapshot_dir,
            id_col="object_id",
            version_col="version_valid_from",
            version_num_col="object_version"
        )
        logger.info(
            "Postprocess snapshot saved to %s (rows: %s)",
            snapshot_dir,
            f"{len(latest_gdf):,}"
        )
        logger.info(
            "Postprocess report saved to %s",
            snapshot_dir / "buildings_postprocess_report.md"
        )

        if args.profile:
            logger.info("\nGenerating postprocess snapshot profile...")
            latest_profiler = DataFrameProfiler(
                latest_gdf,
                "Buildings (Byggnad) - Postprocess Snapshot",
                column_descriptions=column_descriptions,
                key_columns=["object_id"]
            )

            latest_reports_dir = Path("reports") / f"buildings_{timestamp}_postprocess"
            latest_reports_dir.mkdir(parents=True, exist_ok=True)

            latest_profile_md = latest_reports_dir / "buildings_profile.md"
            latest_profile_html = latest_reports_dir / "buildings_profile.html"

            latest_profiler.save_markdown(latest_profile_md)
            latest_profiler.save_html(latest_profile_html)

            logger.info(f"Latest profile saved to {latest_profile_md}")
            logger.info(f"Latest HTML profile saved to {latest_profile_html}")
    
    print("=" * 80)
    return 0


if __name__ == "__main__":
    sys.exit(main())
