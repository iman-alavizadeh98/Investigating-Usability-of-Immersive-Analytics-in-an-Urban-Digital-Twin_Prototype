import argparse
import json
from pathlib import Path

import geopandas as gpd
from shapely.geometry import Polygon, MultiPolygon


SCHEMA_VERSION = "1.0"


def parse_args():
    parser = argparse.ArgumentParser(
        description=(
            "Export a polygon GIS dataset into the Urban Analytics "
            "runtime spatial-layer format."
        )
    )

    parser.add_argument(
        "--input",
        required=True,
        help="Input GIS file, e.g. Shapefile or GeoPackage."
    )

    parser.add_argument(
        "--output",
        required=True,
        help="Output directory for layer.json and geometry.json."
    )

    parser.add_argument(
        "--layer-id",
        required=True,
        help="Stable spatial-layer ID, e.g. ruta_250."
    )

    parser.add_argument(
        "--display-name",
        required=True,
        help="Human-readable layer name."
    )

    parser.add_argument(
        "--description",
        default="",
        help="Optional layer description."
    )

    parser.add_argument(
        "--id-field",
        required=True,
        help="Source attribute used to construct stable unit IDs."
    )

    parser.add_argument(
        "--expected-crs",
        required=True,
        help="Expected CRS, e.g. EPSG:3006."
    )

    parser.add_argument(
        "--filter-field",
        default=None,
        help="Optional field used to filter input features."
    )

    parser.add_argument(
        "--filter-value",
        default=None,
        help="Value required in --filter-field."
    )

    parser.add_argument(
        "--geometry-type",
        choices=["Polygon", "MultiPolygon"],
        default="Polygon",
        help=(
            "Runtime geometry representation. "
            "MultiPolygon can accept both Polygon and MultiPolygon input."
        )
    )

    parser.add_argument(
        "--geometry-mode",
        choices=["Procedural", "Precomputed", "Hybrid"],
        default="Procedural"
    )

    parser.add_argument(
        "--visible-by-default",
        action=argparse.BooleanOptionalAction,
        default=True
    )

    parser.add_argument(
        "--selectable",
        action=argparse.BooleanOptionalAction,
        default=True
    )

    parser.add_argument(
        "--extractable",
        action=argparse.BooleanOptionalAction,
        default=True
    )

    return parser.parse_args()


def validate_crs(gdf, expected_crs):
    if gdf.crs is None:
        raise ValueError(
            "Input dataset has no CRS. "
            "The runtime exporter will not guess or assign one."
        )

    actual = gdf.crs.to_string().upper()
    expected = expected_crs.upper()

    if actual != expected:
        raise ValueError(
            f"CRS mismatch. Expected {expected}, got {actual}. "
            "This exporter does not reproject data."
        )


def remove_closing_coordinate(coords):
    coords = list(coords)

    if len(coords) >= 2 and coords[0] == coords[-1]:
        coords = coords[:-1]

    return coords


def coordinate_to_dto(coord):
    # Shapely coordinates may be 2D or 3D.
    easting = float(coord[0])
    northing = float(coord[1])
    elevation = float(coord[2]) if len(coord) >= 3 else 0.0

    return {
        "easting": easting,
        "northing": northing,
        "elevation": elevation
    }


def ring_to_dto(ring):
    coordinates = remove_closing_coordinate(
        ring.coords
    )

    if len(coordinates) < 3:
        raise ValueError(
            "Polygon ring contains fewer than three coordinates."
        )

    return {
        "coordinates": [
            coordinate_to_dto(coord)
            for coord in coordinates
        ]
    }


def polygon_to_dto(polygon):
    if polygon.is_empty:
        raise ValueError("Polygon is empty.")

    if not polygon.is_valid:
        raise ValueError(
            "Polygon is invalid. "
            "Geometry repair must happen in preprocessing, "
            "not during runtime export."
        )

    return {
        "exterior": ring_to_dto(
            polygon.exterior
        ),
        "holes": [
            ring_to_dto(interior)
            for interior in polygon.interiors
        ]
    }


def geometry_to_runtime_dto(
    geometry,
    requested_geometry_type
):
    if geometry is None or geometry.is_empty:
        raise ValueError(
            "Feature contains null or empty geometry."
        )

    if requested_geometry_type == "Polygon":
        if not isinstance(geometry, Polygon):
            raise ValueError(
                f"Expected Polygon geometry, got "
                f"{geometry.geom_type}."
            )

        return {
            "polygon": polygon_to_dto(
                geometry
            )
        }

    if requested_geometry_type == "MultiPolygon":
        if isinstance(geometry, Polygon):
            polygons = [geometry]

        elif isinstance(geometry, MultiPolygon):
            polygons = list(geometry.geoms)

        else:
            raise ValueError(
                f"Expected Polygon or MultiPolygon geometry, "
                f"got {geometry.geom_type}."
            )

        return {
            "multiPolygon": {
                "polygons": [
                    polygon_to_dto(polygon)
                    for polygon in polygons
                ]
            }
        }

    raise ValueError(
        f"Unsupported geometry type: "
        f"{requested_geometry_type}"
    )


def make_semantic_id(layer_id, raw_id):
    raw_id = str(raw_id).strip()

    if not raw_id:
        raise ValueError(
            "Source feature has an empty ID."
        )

    if ":" in raw_id:
        raise ValueError(
            f"Source ID '{raw_id}' contains ':'. "
            "':' is reserved for semantic IDs."
        )

    return f"{layer_id}:{raw_id}"


def export_layer(args):
    input_path = Path(args.input)
    output_path = Path(args.output)

    if not input_path.exists():
        raise FileNotFoundError(
            f"Input dataset not found: {input_path}"
        )

    print(f"Reading: {input_path}")

    gdf = gpd.read_file(
        input_path
    )

    if gdf.empty:
        raise ValueError(
            "Input dataset contains no features."
        )

    validate_crs(
        gdf,
        args.expected_crs
    )

    if args.id_field not in gdf.columns:
        raise KeyError(
            f"ID field '{args.id_field}' "
            f"does not exist."
        )

    # ---------------------------------------------------------
    # Optional filtering
    # ---------------------------------------------------------

    if args.filter_field is not None:
        if args.filter_field not in gdf.columns:
            raise KeyError(
                f"Filter field '{args.filter_field}' "
                f"does not exist."
            )

        if args.filter_value is None:
            raise ValueError(
                "--filter-value is required when "
                "--filter-field is used."
            )

        gdf = gdf[
            gdf[args.filter_field]
            .astype(str)
            .str.strip()
            == str(args.filter_value).strip()
        ].copy()

        if gdf.empty:
            raise ValueError(
                "Filtering removed all features."
            )

    # ---------------------------------------------------------
    # Validate source IDs before export
    # ---------------------------------------------------------

    if gdf[args.id_field].isna().any():
        raise ValueError(
            f"ID field '{args.id_field}' "
            f"contains null values."
        )

    raw_ids = (
        gdf[args.id_field]
        .astype(str)
        .str.strip()
    )

    duplicates = raw_ids[
        raw_ids.duplicated(
            keep=False
        )
    ]

    if not duplicates.empty:
        duplicate_values = sorted(
            duplicates.unique().tolist()
        )

        preview = duplicate_values[:10]

        raise ValueError(
            f"ID field '{args.id_field}' is not unique "
            f"within this exported layer. "
            f"Example duplicates: {preview}"
        )

    # ---------------------------------------------------------
    # Build runtime units
    # ---------------------------------------------------------

    units = []
    semantic_ids = set()

    for row_index, row in gdf.iterrows():
        raw_id = row[args.id_field]

        semantic_id = make_semantic_id(
            args.layer_id,
            raw_id
        )

        if semantic_id in semantic_ids:
            raise ValueError(
                f"Duplicate semantic ID generated: "
                f"{semantic_id}"
            )

        semantic_ids.add(
            semantic_id
        )

        geometry = row.geometry

        runtime_geometry = (
            geometry_to_runtime_dto(
                geometry,
                args.geometry_type
            )
        )

        centroid = geometry.centroid

        unit = {
            "id": semantic_id,

            "displayName": str(
                raw_id
            ),

            "parentUnitId": None,

            "centroid": {
                "easting": float(
                    centroid.x
                ),
                "northing": float(
                    centroid.y
                ),
                "elevation": 0.0
            },

            "geometry": runtime_geometry
        }

        units.append(
            unit
        )

    unit_count = len(units)

    # ---------------------------------------------------------
    # layer.json
    # ---------------------------------------------------------

    layer_definition = {
        "id": args.layer_id,

        "displayName": (
            args.display_name
        ),

        "description": (
            args.description
        ),

        "geometryType": (
            args.geometry_type
        ),

        "geometryMode": (
            args.geometry_mode
        ),

        "geometrySource": (
            "geometry.json"
        ),

        "unitCount": (
            unit_count
        ),

        "visibleByDefault": (
            args.visible_by_default
        ),

        "selectable": (
            args.selectable
        ),

        "extractable": (
            args.extractable
        )
    }

    # ---------------------------------------------------------
    # geometry.json
    # ---------------------------------------------------------

    geometry_file = {
        "schemaVersion": (
            SCHEMA_VERSION
        ),

        "spatialLayerId": (
            args.layer_id
        ),

        "geometryType": (
            args.geometry_type
        ),

        "unitCount": (
            unit_count
        ),

        "units": units
    }

    # ---------------------------------------------------------
    # Write output
    # ---------------------------------------------------------

    output_path.mkdir(
        parents=True,
        exist_ok=True
    )

    layer_path = (
        output_path /
        "layer.json"
    )

    geometry_path = (
        output_path /
        "geometry.json"
    )

    with layer_path.open(
        "w",
        encoding="utf-8"
    ) as file:
        json.dump(
            layer_definition,
            file,
            ensure_ascii=False,
            indent=2
        )

    with geometry_path.open(
        "w",
        encoding="utf-8"
    ) as file:
        json.dump(
            geometry_file,
            file,
            ensure_ascii=False,
            separators=(",", ":")
        )

    # ---------------------------------------------------------
    # Summary
    # ---------------------------------------------------------

    print()
    print("=" * 70)
    print("SPATIAL LAYER EXPORT COMPLETE")
    print("=" * 70)

    print(
        f"Layer ID:       "
        f"{args.layer_id}"
    )

    print(
        f"CRS:            "
        f"{gdf.crs}"
    )

    print(
        f"Geometry type:  "
        f"{args.geometry_type}"
    )

    print(
        f"Unit count:     "
        f"{unit_count}"
    )

    print(
        f"Bounds:         "
        f"{gdf.total_bounds.tolist()}"
    )

    print(
        f"Layer file:     "
        f"{layer_path}"
    )

    print(
        f"Geometry file:  "
        f"{geometry_path}"
    )


def main():
    args = parse_args()

    try:
        export_layer(
            args
        )

    except Exception as exception:
        print()
        print("=" * 70)
        print("EXPORT FAILED")
        print("=" * 70)
        print(
            f"{type(exception).__name__}: "
            f"{exception}"
        )

        raise


if __name__ == "__main__":
    main()