"""Building height pipeline (package name kept from the LiDAR-only version).

One height per building for extruding flat roofs:

    height_m = p95( z - ground ) of the points inside the footprint

1. Load the buildings postprocess output (one row per object_id).
2. Group buildings into work cells; read the points of each cell from every
   tile they lie in (surface model + laser data, folder or zip).
3. Per building: interpolate the laser ground around the footprint, take the
   surface points inside it (laser roof points where the surface has a hole),
   height = 95th percentile of height above ground.
4. Export GeoPackage/Parquet, QC CSV, buildings without height, summary JSON.

pdal_pipelines.py and tile_index.py belong to the earlier PDAL-based version
and are not used since 2026-10-06.
"""

__version__ = "0.2.0"
