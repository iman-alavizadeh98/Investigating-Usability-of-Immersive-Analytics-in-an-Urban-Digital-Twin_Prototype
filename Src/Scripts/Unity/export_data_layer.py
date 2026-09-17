import argparse
import json
import math
from dataclasses import dataclass
from pathlib import Path

import geopandas as gpd
import pandas as pd


SCHEMA_VERSION = "1.0"

SUPPORTED_VALUE_TYPES = {
    "float": "Float",
    "integer": "Integer",
    "boolean": "Boolean",
    "string": "String",
}


@dataclass(frozen=True)
class VariableSpec:
    variable_id: str
    source_field: str
    value_type: str
    unit: str
    display_name: str
    description: str


def parse_args():
    parser = argparse.ArgumentParser(
        description=(
            "Export analytical attributes into the UrbanAnalytics "
            "runtime DataLayer format."
        )
    )

    parser.add_argument(
        "--input",
        required=True,
        help="Input GIS file, e.g. Shapefile or GeoPackage.",
    )

    parser.add_argument(
        "--output",
        required=True,
        help="Output directory for layer.json and values.json.",
    )

    parser.add_argument(
        "--layer-id",
        required=True,
        help="Stable analytical DataLayer ID, e.g. income_2023.",
    )

    parser.add_argument(
        "--display-name",
        required=True,
        help="Human-readable DataLayer name.",
    )

    parser.add_argument(
        "--description",
        default="",
        help="Optional DataLayer description.",
    )

    parser.add_argument(
        "--target-spatial-layer",
        required=True,
        help="SpatialLayer ID whose units are referenced by this dataset.",
    )

    parser.add_argument(
        "--id-field",
        required=True,
        help="Source field containing the spatial-unit identifier.",
    )

    parser.add_argument(
        "--expected-crs",
        default=None,
        help="Optional expected CRS, e.g. EPSG:3006.",
    )

    parser.add_argument(
        "--filter-field",
        default=None,
        help="Optional field used to filter input rows.",
    )

    parser.add_argument(
        "--filter-value",
        default=None,
        help="Required value when --filter-field is used.",
    )

    parser.add_argument(
        "--variable",
        action="append",
        required=True,
        help=(
            "Variable specification. Repeat for multiple variables. "
            "Format: id|source_field|type|unit|display_name|description"
        ),
    )

    return parser.parse_args()


def parse_variable_spec(raw):
    parts = raw.split("|")

    if len(parts) < 3:
        raise ValueError(
            "Variable specification must contain at least: "
            "id|source_field|type"
        )

    variable_id = parts[0].strip()
    source_field = parts[1].strip()
    raw_type = parts[2].strip().lower()

    if not variable_id:
        raise ValueError("Variable ID cannot be empty.")

    if not source_field:
        raise ValueError(
            f"Source field cannot be empty for variable '{variable_id}'."
        )

    if raw_type not in SUPPORTED_VALUE_TYPES:
        raise ValueError(
            f"Unsupported value type '{parts[2]}'. "
            f"Supported types: {', '.join(SUPPORTED_VALUE_TYPES.values())}"
        )

    unit = parts[3].strip() if len(parts) > 3 else ""

    display_name = (
        parts[4].strip()
        if len(parts) > 4 and parts[4].strip()
        else variable_id
    )

    description = parts[5].strip() if len(parts) > 5 else ""

    return VariableSpec(
        variable_id=variable_id,
        source_field=source_field,
        value_type=SUPPORTED_VALUE_TYPES[raw_type],
        unit=unit,
        display_name=display_name,
        description=description,
    )


def validate_crs(gdf, expected_crs):
    if expected_crs is None:
        return

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


def make_semantic_id(layer_id, raw_id):
    """
    Keep this identical to export_polygon_spatial_layer.py so
    geometry and analytical data generate exactly the same IDs.
    """
    raw_id = str(raw_id).strip()

    if not raw_id:
        raise ValueError("Source feature has an empty ID.")

    if ":" in raw_id:
        raise ValueError(
            f"Source ID '{raw_id}' contains ':'. "
            "':' is reserved for semantic IDs."
        )

    return f"{layer_id}:{raw_id}"


def apply_filter(gdf, filter_field, filter_value):
    if filter_field is None:
        return gdf.copy()

    if filter_field not in gdf.columns:
        raise KeyError(
            f"Filter field '{filter_field}' does not exist."
        )

    if filter_value is None:
        raise ValueError(
            "--filter-value is required when --filter-field is used."
        )

    filtered = gdf[
        gdf[filter_field].astype(str).str.strip()
        == str(filter_value).strip()
    ].copy()

    if filtered.empty:
        raise ValueError("Filtering removed all rows.")

    return filtered


def is_missing(value):
    if pd.isna(value):
        return True

    if isinstance(value, str) and not value.strip():
        return True

    return False


def build_float_column(series, variable):
    values = []
    valid = []
    observed = []

    for row_index, raw_value in series.items():
        if is_missing(raw_value):
            values.append(0.0)
            valid.append(False)
            continue

        try:
            value = float(raw_value)
        except (TypeError, ValueError) as exception:
            raise ValueError(
                f"Variable '{variable.variable_id}' contains non-numeric "
                f"value '{raw_value}' at source row {row_index}."
            ) from exception

        if not math.isfinite(value):
            raise ValueError(
                f"Variable '{variable.variable_id}' contains non-finite "
                f"value '{raw_value}' at source row {row_index}."
            )

        values.append(value)
        valid.append(True)
        observed.append(value)

    value_range = None

    if observed:
        value_range = (
            min(observed),
            max(observed),
        )

    return values, valid, value_range


def build_integer_column(series, variable):
    values = []
    valid = []
    observed = []

    for row_index, raw_value in series.items():
        if is_missing(raw_value):
            values.append(0)
            valid.append(False)
            continue

        try:
            numeric = float(raw_value)
        except (TypeError, ValueError) as exception:
            raise ValueError(
                f"Variable '{variable.variable_id}' contains non-numeric "
                f"value '{raw_value}' at source row {row_index}."
            ) from exception

        if not math.isfinite(numeric):
            raise ValueError(
                f"Variable '{variable.variable_id}' contains non-finite "
                f"value '{raw_value}' at source row {row_index}."
            )

        rounded = round(numeric)

        if not math.isclose(
            numeric,
            rounded,
            rel_tol=0.0,
            abs_tol=1e-9,
        ):
            raise ValueError(
                f"Variable '{variable.variable_id}' is declared Integer "
                f"but contains '{raw_value}' at source row {row_index}."
            )

        value = int(rounded)

        values.append(value)
        valid.append(True)
        observed.append(value)

    value_range = None

    if observed:
        value_range = (
            min(observed),
            max(observed),
        )

    return values, valid, value_range


def build_boolean_column(series, variable):
    values = []
    valid = []

    true_values = {
        "true",
        "1",
        "yes",
        "y",
    }

    false_values = {
        "false",
        "0",
        "no",
        "n",
    }

    for row_index, raw_value in series.items():
        if is_missing(raw_value):
            values.append(False)
            valid.append(False)
            continue

        if isinstance(raw_value, bool):
            values.append(raw_value)
            valid.append(True)
            continue

        text = str(raw_value).strip().lower()

        if text in true_values:
            values.append(True)
            valid.append(True)

        elif text in false_values:
            values.append(False)
            valid.append(True)

        else:
            raise ValueError(
                f"Variable '{variable.variable_id}' contains invalid "
                f"Boolean value '{raw_value}' at source row {row_index}."
            )

    return values, valid


def build_string_column(series):
    values = []
    valid = []

    for raw_value in series:
        if is_missing(raw_value):
            values.append("")
            valid.append(False)
            continue

        values.append(
            str(raw_value)
        )

        valid.append(True)

    return values, valid


def build_runtime_column(gdf, variable):
    if variable.source_field not in gdf.columns:
        raise KeyError(
            f"Source field '{variable.source_field}' for variable "
            f"'{variable.variable_id}' does not exist."
        )

    series = gdf[
        variable.source_field
    ]

    column = {
        "variableId": variable.variable_id,
    }

    value_range = None

    if variable.value_type == "Float":
        values, valid, value_range = build_float_column(
            series,
            variable,
        )

        column["floatValues"] = values

    elif variable.value_type == "Integer":
        values, valid, value_range = build_integer_column(
            series,
            variable,
        )

        column["integerValues"] = values

    elif variable.value_type == "Boolean":
        values, valid = build_boolean_column(
            series,
            variable,
        )

        column["booleanValues"] = values

    elif variable.value_type == "String":
        values, valid = build_string_column(
            series
        )

        column["stringValues"] = values

    else:
        raise ValueError(
            f"Unsupported runtime value type: "
            f"{variable.value_type}"
        )

    valid_count = sum(valid)

    # An omitted validity mask means that all rows are valid.
    if valid_count != len(valid):
        column["valid"] = valid

    return (
        column,
        valid_count,
        value_range,
    )


def export_data_layer(args):
    input_path = Path(
        args.input
    )

    output_path = Path(
        args.output
    )

    if not input_path.exists():
        raise FileNotFoundError(
            f"Input dataset not found: {input_path}"
        )

    variables = [
        parse_variable_spec(raw)
        for raw in args.variable
    ]

    variable_ids = [
        variable.variable_id
        for variable in variables
    ]

    if len(set(variable_ids)) != len(variable_ids):
        raise ValueError(
            "Variable IDs must be unique."
        )

    print(
        f"Reading: {input_path}"
    )

    gdf = gpd.read_file(
        input_path
    )

    if gdf.empty:
        raise ValueError(
            "Input dataset contains no rows."
        )

    print(
        f"Source rows:       {len(gdf)}"
    )

    validate_crs(
        gdf,
        args.expected_crs,
    )

    if args.id_field not in gdf.columns:
        raise KeyError(
            f"ID field '{args.id_field}' does not exist."
        )

    gdf = apply_filter(
        gdf,
        args.filter_field,
        args.filter_value,
    )

    print(
        f"Rows after filter: {len(gdf)}"
    )

    if gdf[args.id_field].isna().any():
        raise ValueError(
            f"ID field '{args.id_field}' contains null values."
        )

    raw_ids = (
        gdf[args.id_field]
        .astype(str)
        .str.strip()
    )

    if (raw_ids == "").any():
        raise ValueError(
            f"ID field '{args.id_field}' contains empty values."
        )

    duplicates = raw_ids[
        raw_ids.duplicated(
            keep=False
        )
    ]

    if not duplicates.empty:
        duplicate_values = sorted(
            duplicates
            .unique()
            .tolist()
        )

        raise ValueError(
            f"ID field '{args.id_field}' is not unique within "
            f"this exported layer. Example duplicates: "
            f"{duplicate_values[:10]}"
        )

    unit_ids = [
        make_semantic_id(
            args.target_spatial_layer,
            raw_id,
        )
        for raw_id in gdf[
            args.id_field
        ]
    ]

    if len(set(unit_ids)) != len(unit_ids):
        raise ValueError(
            "Duplicate semantic unit IDs were generated."
        )

    definition_variables = [
        {
            "id": variable.variable_id,
            "displayName": variable.display_name,
            "description": variable.description,
            "valueType": variable.value_type,
            "unit": variable.unit,
        }
        for variable in variables
    ]

    layer_definition = {
        "schemaVersion": SCHEMA_VERSION,
        "id": args.layer_id,
        "displayName": args.display_name,
        "description": args.description,
        "targetSpatialLayerId": args.target_spatial_layer,
        "dataFile": "values.json",
        "temporalMode": "Static",
        "variables": definition_variables,
    }

    columns = []
    diagnostics = []

    for variable in variables:
        column, valid_count, value_range = build_runtime_column(
            gdf,
            variable,
        )

        columns.append(
            column
        )

        diagnostics.append(
            {
                "variable": variable,
                "valid_count": valid_count,
                "value_range": value_range,
            }
        )

    values_file = {
        "schemaVersion": SCHEMA_VERSION,
        "dataLayerId": args.layer_id,
        "targetSpatialLayerId": args.target_spatial_layer,
        "unitIds": unit_ids,
        "columns": columns,
    }

    output_path.mkdir(
        parents=True,
        exist_ok=True,
    )

    layer_path = (
        output_path /
        "layer.json"
    )

    values_path = (
        output_path /
        "values.json"
    )

    with layer_path.open(
        "w",
        encoding="utf-8",
    ) as file:
        json.dump(
            layer_definition,
            file,
            ensure_ascii=False,
            indent=2,
        )

    with values_path.open(
        "w",
        encoding="utf-8",
    ) as file:
        json.dump(
            values_file,
            file,
            ensure_ascii=False,
            separators=(",", ":"),
        )

    print()
    print("=" * 70)
    print("DATA LAYER EXPORT COMPLETE")
    print("=" * 70)

    print(
        f"Layer ID:          {args.layer_id}"
    )

    print(
        f"Target layer:      {args.target_spatial_layer}"
    )

    print(
        f"Source CRS:        {gdf.crs}"
    )

    print(
        f"Unit count:        {len(unit_ids)}"
    )

    print(
        f"Variable count:    {len(variables)}"
    )

    print()

    for diagnostic in diagnostics:
        variable = diagnostic[
            "variable"
        ]

        valid_count = diagnostic[
            "valid_count"
        ]

        value_range = diagnostic[
            "value_range"
        ]

        print(
            f"{variable.source_field} -> "
            f"{variable.variable_id}"
        )

        print(
            f"  type:            "
            f"{variable.value_type}"
        )

        print(
            f"  valid:           "
            f"{valid_count}/{len(unit_ids)}"
        )

        if value_range is not None:
            print(
                f"  range:           "
                f"{value_range[0]} -> "
                f"{value_range[1]}"
            )

    print()

    print(
        f"Definition:        "
        f"{layer_path}"
    )

    print(
        f"Values:            "
        f"{values_path}"
    )


def main():
    args = parse_args()

    try:
        export_data_layer(
            args
        )

    except Exception as exception:
        print()
        print("=" * 70)
        print("DATA LAYER EXPORT FAILED")
        print("=" * 70)

        print(
            f"{type(exception).__name__}: "
            f"{exception}"
        )

        raise


if __name__ == "__main__":
    main()