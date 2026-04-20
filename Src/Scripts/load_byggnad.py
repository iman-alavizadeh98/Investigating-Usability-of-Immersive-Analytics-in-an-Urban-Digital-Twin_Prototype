"""
load_byggnad.py
loading and validating the building data from Byggnad.gpkg

"""
import geopandas as gpd
import pandas as pd
from shapely.geometry import Point, Polygon, MultiPolygon
import json

#___ COONFIGURATION ______
INPUT_GPKG = "Raw_data/byggnad_gpkg/byggnad_sverige.gpkg"
OUTPUT_DIR = "Processed_data"
TARGET_CRS = "EPSG:3006"
PLACEHOLDER_HEIGHT = 10.0  # meters

#____LOAD______
print("____LOAD________________")
print("Loading building data...")
buildings = gpd.read_file(INPUT_GPKG)
print(f"Loaded {len(buildings)} building features.")
print(f"Original CRS: {buildings.crs}")
print(f"Geometry types: {buildings.geometry.type.unique()}")
print(buildings.info())
#print(buildings.head())

#______Preprocessing the GeoFrame______
print("______PREPROCESSING________________")

print("\nTranslating column names to English...")
rename_dict = {
    "objektidentitet": "object_id",
    "versiongiltigfran": "valid_from",
    "lagesosakerhetplan": "horizontal_accuracy",
    "lagesosakerhethojd": "vertical_accuracy",
    "ursprunglig_organisation": "source_organization",
    "objektversion": "object_version",
    "objekttypnr": "object_type_id",
    "objekttyp": "object_type",
    "insamlingslage": "capture_method",
    "byggnadsnamn1": "building_name",
    "byggnadsnamn2": "building_name_alt",
    "byggnadsnamn3": "building_name_third",
    "husnummer": "house_number",
    "huvudbyggnad": "is_main_building",
    "andamal1": "primary_usage",
    "andamal2": "secondary_usage",
    "andamal3": "tertiary_usage",
    "andamal4": "usage_4",
    "andamal5": "usage_5",
}
buildings = buildings.rename(columns=rename_dict)
print("Columns after renaming:")
#print(buildings.columns.to_list())
print(buildings.info())








'''
# ── VALIDATE & REPAIR ───────
print("── VALIDATE & REPAIR ────────────────────────────")
print("\nValidating geometries...")
buildings["geometry"] = buildings["geometry"].buffer(0)  # Fix invalid geometries (I didn't got the buffer thing, but it seems a regular data clean up for geospatial data.)
print(f"Invalid geometries: {(~buildings.geometry.is_valid).sum()}")


# ── EXPLODE MULTIPOLYGONS ───────
print("── EXPLODE MULTIPOLYGONS ─────────────────────")
print("\nHANDLING MULTIPOLYGONS...")
has_multipolygons = buildings.geometry.type == "MultiPolygon"
print(f"Features with MultiPolygon geometries: {has_multipolygons.sum()}")

if has_multipolygons.any():
    buildings = buildings.explode(index_drop=True)
    print(f"After explode: {len(buildings)} polygons")


#____ENSURE CRS______
print("── ENSURE CRS ─────────────────────────────")
if buildings.crs != TARGET_CRS:
    print(f"\nReprojecting to {TARGET_CRS}...")
    buildings = buildings.to_crs(TARGET_CRS)
    print(f"Reprojected to {TARGET_CRS}")
else:
    print(f"\nCRS is already {TARGET_CRS}")


#____ASSIGN IDs & COMPUTE METADATA______
print("── ASSIGN IDs & COMPUTE METADATA ─────────────────────")
print("\nAssigning IDs and computing metadata...")
buildings["building_id"] = [f"bld_{i:06d}" for i in range(len(buildings))]
buildings["centroid"] = buildings.geometry.centroid
buildings["height"] = PLACEHOLDER_HEIGHT  # Placeholder height for all buildings

# Compute bounding box per building
buildings["bbox"] = buildings.geometry.apply(
    lambda geom: [geom.bounds[0], geom.bounds[1], geom.bounds[2], geom.bounds[3]]
)

# ______ SUMMARY _______
print
print(f"\n{'='*50}")
print(f"LOAD SUMMARY")
print(f"{'='*50}")
print(f"Total buildings: {len(buildings)}")
print(f"CRS: {buildings.crs}")
print(f"Bounds: {buildings.total_bounds}")
print(f"Columns: {list(buildings.columns)}")
print(f"\nFirst 3 buildings:")
print(buildings[["building_id", "height", "centroid", "bbox"]].head(3))

# _____SAVE GEOJSON (for inspection) _____
import os
os.makedirs(OUTPUT_DIR, exist_ok=True)

geojson_path = os.path.join(OUTPUT_DIR, "buildings_loaded.geojson")
buildings.to_file(geojson_path, driver="GeoJSON")
print(f"\nSaved: {geojson_path}")

# _____SAVE METADATA (for inspection) _____
print("_____SAVE METADATA (for inspection) _____________")
metadata = {
    "source": INPUT_GPKG,
    "total_buildings": len(buildings),
    "crs": buildings.crs.to_string(),
    "bounds": {
        "xmin": buildings.total_bounds[0],
        "ymin": buildings.total_bounds[1],
        "xmax": buildings.total_bounds[2],
        "ymax": buildings.total_bounds[3],
    },
    "height_method": "placeholder_constant",
    "placeholder_height": PLACEHOLDER_HEIGHT,
}

metadata_path = os.path.join(OUTPUT_DIR, "load_metadata.json")
with open(metadata_path, "w") as f:
    json.dump(metadata, f, indent=2)
print(f"Saved: {metadata_path}")

print(f"\nData loaded and validated")

'''