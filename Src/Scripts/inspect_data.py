import geopandas as gpd
import laspy
import numpy as np

# ── 1. Inspect Building GeoPackage ──────────────────────────────────────────
print("=" * 50)
print("BUILDING DATA")
print("=" * 50)

buildings = gpd.read_file(r"Raw_data/byggnad_gpkg/byggnad_sverige.gpkg")

print(f"CRS: {buildings.crs}")
print(f"Total buildings: {len(buildings)}")
print(f"Columns: {list(buildings.columns)}")
print(f"Bounds: {buildings.total_bounds}")
print(f"\nFirst row:\n{buildings.head(1)}")

# ── 2. Inspect LiDAR ─────────────────────────────────────────────────────────
print("\n" + "=" * 50)
print("LIDAR DATA")
print("=" * 50)

laz_file = r"Raw_data/laserdata_nh/09B002_639_31_5075.laz"
with laspy.open(laz_file) as f:
    print(f"Point count: {f.header.point_count}")
    print(f"CRS: {f.header.parse_crs()}")
    print(f"Min bounds: {f.header.mins}")
    print(f"Max bounds: {f.header.maxs}")
    las = f.read()
    print(f"Available dimensions: {list(las.point_format.dimension_names)}")