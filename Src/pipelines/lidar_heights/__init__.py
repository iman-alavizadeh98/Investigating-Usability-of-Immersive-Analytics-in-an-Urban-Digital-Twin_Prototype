"""LiDAR-based building height estimation pipeline.

This module provides a modular preprocessing pipeline for enriching building
footprints with accurate height estimates from LiDAR point clouds.

Workflow:
1. Load processed buildings from the buildings pipeline
2. Spatially index buildings to LiDAR tiles
3. Preprocess LiDAR using PDAL (outlier removal, ground classification, HAG)
4. Extract per-building heights with quality flags
5. Export enriched GeoPackage/Parquet for downstream mesh generation

Preserves all building IDs and maintains EPSG:3006 as the authoritative CRS.
"""

__version__ = "0.1.0"
