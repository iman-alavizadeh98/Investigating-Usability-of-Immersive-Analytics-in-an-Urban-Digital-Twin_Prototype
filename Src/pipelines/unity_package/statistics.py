"""
SCB statistics (population, income) for the Unity package.

Spatial layers
  ruta   SCB grid squares. 250 m cells as exact squares rebuilt from the cell ID
         (south-west corner + size; undoes the cut at the delivery edge). 1,000 m
         cells as their square MINUS the 250 m cells inside it, because SCB gives
         a 1,000 m cell only the remainder population outside the finer cells
         (shortcut D5). Unit id "ruta:<size>_<RutID>" (250 m and 1,000 m cells can
         share a corner, hence the size in the id).
  deso   DeSO areas as delivered (cut to the delivery rectangle). DeSO areas
         without geometry in the delivery are left out of both the spatial and
         the data layers (reported).

Data layers (values keyed by the unit ids above; a unit missing from a source
table has no value, never 0 - shortcut D4)
  ruta_population   counts and shares from population tables 1, 2, 4 and 10
  ruta_income       households by income quartile, income sum, mean economic standard
  deso_population   age and country-of-birth shares, population change (table 5)
  deso_income       households, median income, quartile shares

Shares are computed over the sum of the table's own sub-groups, not over the
published total, so they always add up to 100 % (SCB perturbs small counts and
sub-groups rarely add up to the total - shortcut D3).

Tables are found by table number and unit in the SCB file names, never by a
city-specific file name; the folders come from the city config.
"""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import geopandas as gpd
import numpy as np
import pandas as pd
from shapely.geometry import box
from shapely.ops import unary_union

from pipelines.scb.loader import SourceTable, load_scb_tables

from .layer_files import SpatialLayerSpec, Variable, write_data_layer, write_spatial_layer

logger = logging.getLogger(__name__)

RUTA_LAYER = "ruta"
DESO_LAYER = "deso"
RUTA_EASTING_DIGITS = 6


# =============================================================================
# Helpers
# =============================================================================

def find_table(tables: Dict[str, SourceTable], number: str, unit: str, size_filter: Optional[set] = None) -> Optional[SourceTable]:
    """The table with this number and unit. For Ruta, `size_filter` picks the mixed 250/1,000 m grid over the 100 m one."""
    for t in tables.values():
        if t.table_number != number or t.unit != unit:
            continue
        if size_filter and t.size_column:
            sizes = set(pd.to_numeric(t.data[t.size_column], errors="coerce").dropna().astype(int))
            if not sizes <= size_filter:
                continue
        return t
    return None


def ruta_key(table: SourceTable) -> pd.Series:
    """Unit id "ruta:<size>_<id>" per row."""
    ids = table.data[table.id_column].astype("string").str.strip()
    sizes = pd.to_numeric(table.data[table.size_column], errors="coerce").astype("Int64").astype("string")
    return f"{RUTA_LAYER}:" + sizes + "_" + ids


def ruta_square(unit_id: str):
    size, rid = unit_id.split(":", 1)[1].split("_", 1)
    e, n, s = int(rid[:RUTA_EASTING_DIGITS]), int(rid[RUTA_EASTING_DIGITS:]), int(size)
    return box(e, n, e + s, n + s)


def keyed(table: Optional[SourceTable], key_fn) -> Optional[pd.DataFrame]:
    """The table's attributes indexed by unit id (geometry dropped)."""
    if table is None:
        return None
    df = pd.DataFrame(table.data.drop(columns=table.data.geometry.name))
    df.index = key_fn(table)
    if df.index.duplicated().any():
        raise ValueError(f"{table.key}: repeated unit ids")
    return df


def share(part: pd.Series, whole: pd.Series) -> pd.Series:
    """Percent; no value where the whole is 0 or missing."""
    return (100.0 * part / whole.where(whole > 0)).astype(float)


def col(df: Optional[pd.DataFrame], name: str, index) -> pd.Series:
    """Column reindexed to `index`; all-missing if the table or column is absent."""
    if df is None or name not in df.columns:
        return pd.Series(np.nan, index=index, dtype=float)
    return pd.to_numeric(df[name], errors="coerce").reindex(index)


def year_of(table: Optional[SourceTable]) -> Optional[int]:
    return table.year if table is not None else None


# =============================================================================
# Ruta (grid)
# =============================================================================

def build_ruta_units(keys: List[str], fine_size: int = 250) -> gpd.GeoDataFrame:
    """Exact squares; coarse cells minus the finer cells inside them (D5)."""
    df = pd.DataFrame({"unit_id": sorted(set(keys))})
    df["size_m"] = df["unit_id"].str.split(":").str[1].str.split("_").str[0].astype(int)
    df["geometry"] = df["unit_id"].map(ruta_square)
    gdf = gpd.GeoDataFrame(df, geometry="geometry", crs="EPSG:3006")
    fine = gdf[gdf["size_m"] == fine_size]
    fine_union = unary_union(list(fine.geometry)) if len(fine) else None
    remainder_cut = 0
    if fine_union is not None:
        for i in gdf.index[gdf["size_m"] > fine_size]:
            square = gdf.at[i, "geometry"]
            if square.intersects(fine_union):
                rest = square.difference(fine_union)
                if not rest.is_empty and rest.area > 1.0:
                    gdf.at[i, "geometry"] = rest
                    remainder_cut += 1
    # Readable name; the cell code stays in the unit id.
    gdf["display_name"] = gdf["size_m"].map(lambda s: f"{s} m grid cell" if s < 1000 else f"{s // 1000} km grid cell")
    gdf["area_km2"] = gdf.geometry.area / 1e6
    logger.info(f"  ruta: {len(gdf)} cells ({(gdf.size_m == fine_size).sum()} x {fine_size} m), "
                f"{remainder_cut} coarse cells drawn as remainders")
    return gdf


def export_ruta(package_dir: Path, population: Dict[str, SourceTable], income: Dict[str, SourceTable]) -> Tuple[gpd.GeoDataFrame, Dict]:
    grid = {250, 1000}
    t_age = find_table(population, "1", "Ruta", grid)
    t_sex = find_table(population, "2", "Ruta", grid)
    t_birth = find_table(population, "4", "Ruta", grid)
    t_edu = find_table(population, "10", "Ruta", grid)
    t_inc = find_table(income, "11", "Ruta", grid)
    used = [t for t in (t_age, t_sex, t_birth, t_edu, t_inc) if t is not None]
    if t_age is None:
        raise ValueError("Population table 1 (age) on the Ruta grid is required")

    keys = sorted(set().union(*[set(ruta_key(t)) for t in used]))
    units = build_ruta_units(keys)
    idx = pd.Index(units["unit_id"])

    report = {"tables": {t.key: int(len(t.data)) for t in used}, "units": len(units)}
    write_spatial_layer(package_dir, SpatialLayerSpec(
        RUTA_LAYER, "SCB grid (250 m / 1 km)",
        "SCB statistical grid squares. 250 m cells in built-up areas; 1 km cells drawn as the remainder "
        "outside the 250 m cells they overlap. Squares rebuilt from the cell id.",
        visible_by_default=False,
        provenance={"sources": [str(t.path) for t in used], "method": "squares from RutID_SW + Rutstorl; D5 remainder geometry"},
    ), units)

    age, sex, birth, edu, inc = (keyed(t, ruta_key) for t in (t_age, t_sex, t_birth, t_edu, t_inc))
    age_cols = ["Alder_0_6", "Alder_7_15", "Alder_16_1", "Alder_20_2", "Alder_25_4", "Alder_45_6", "Alder_65"]
    age_sum = sum(col(age, c, idx) for c in age_cols)
    pop = col(age, "Totalt", idx)
    birth_sum = sum(col(birth, c, idx) for c in ["Sverige", "Norden_uto", "EU_utom_No", "Ovriga_var"])
    edu_sum = sum(col(edu, c, idx) for c in ["Forgymn", "Gymnasial", "Eftergymn2", "Eftergymn3", "UppgSakn"])
    sex_sum = col(sex, "Man", idx) + col(sex, "Kvinnor", idx)
    area = pd.Series(units["area_km2"].to_numpy(), index=idx)
    y_pop = year_of(t_age)

    pv = [
        Variable("population", f"Population {y_pop}", pop, "Integer", "persons", "SCB Tab1 Totalt"),
        Variable("population_density", f"Population density {y_pop}", pop / area, "Float", "persons/km²",
                 "Totalt / cell area (1 km cells: remainder area)", "derived"),
        Variable("age_0_6", "Age 0-6", col(age, "Alder_0_6", idx), "Integer", "persons", "Alder_0_6"),
        Variable("age_7_15", "Age 7-15", col(age, "Alder_7_15", idx), "Integer", "persons", "Alder_7_15"),
        Variable("age_16_19", "Age 16-19", col(age, "Alder_16_1", idx), "Integer", "persons", "Alder_16_1 (name cut at 10 characters)"),
        Variable("age_20_24", "Age 20-24", col(age, "Alder_20_2", idx), "Integer", "persons", "Alder_20_2 (name cut at 10 characters)"),
        Variable("age_25_44", "Age 25-44", col(age, "Alder_25_4", idx), "Integer", "persons", "Alder_25_4 (name cut at 10 characters)"),
        Variable("age_45_64", "Age 45-64", col(age, "Alder_45_6", idx), "Integer", "persons", "Alder_45_6 (name cut at 10 characters)"),
        Variable("age_65_plus", "Age 65+", col(age, "Alder_65", idx), "Integer", "persons", "Alder_65"),
        Variable("share_children_0_15", "Children 0-15", share(col(age, "Alder_0_6", idx) + col(age, "Alder_7_15", idx), age_sum),
                 "Float", "%", "(age 0-6 + 7-15) / sum of age groups", "derived"),
        Variable("share_young_adults_20_24", "Young adults 20-24", share(col(age, "Alder_20_2", idx), age_sum),
                 "Float", "%", "age 20-24 / sum of age groups", "derived"),
        Variable("share_65_plus", "Age 65+", share(col(age, "Alder_65", idx), age_sum), "Float", "%",
                 "age 65+ / sum of age groups", "derived"),
        Variable("men", "Men", col(sex, "Man", idx), "Integer", "persons", "SCB Tab2 Man"),
        Variable("women", "Women", col(sex, "Kvinnor", idx), "Integer", "persons", "SCB Tab2 Kvinnor"),
        Variable("share_women", "Women", share(col(sex, "Kvinnor", idx), sex_sum), "Float", "%", "women / (men + women)", "derived"),
        Variable("born_sweden", "Born in Sweden", col(birth, "Sverige", idx), "Integer", "persons", "SCB Tab4 Sverige"),
        Variable("born_nordic", "Born in Nordic countries (excl. Sweden)", col(birth, "Norden_uto", idx), "Integer", "persons", "Norden_uto"),
        Variable("born_eu", "Born in EU (excl. Nordic)", col(birth, "EU_utom_No", idx), "Integer", "persons", "EU_utom_No"),
        Variable("born_rest_of_world", "Born in rest of world", col(birth, "Ovriga_var", idx), "Integer", "persons", "Ovriga_var (incl. unknown)"),
        Variable("share_born_abroad", "Born abroad", share(birth_sum - col(birth, "Sverige", idx), birth_sum), "Float", "%",
                 "1 - born in Sweden / sum of birth regions", "derived"),
        Variable("share_born_outside_europe", "Born outside Europe", share(col(birth, "Ovriga_var", idx), birth_sum), "Float", "%",
                 "rest of world / sum of birth regions", "derived"),
        Variable("share_post_secondary_3y", "Post-secondary education ≥ 3 years (25-64)", share(col(edu, "Eftergymn3", idx), edu_sum),
                 "Float", "%", f"SCB Tab10 Eftergymn3 / sum of education levels (ages 25-64, {year_of(t_edu)})", "derived"),
        Variable("share_pre_upper_secondary", "Pre-upper-secondary education only (25-64)", share(col(edu, "Forgymn", idx), edu_sum),
                 "Float", "%", "Forgymn / sum of education levels", "derived"),
    ]
    report["ruta_population"] = write_data_layer(
        package_dir, "ruta_population", f"Population {y_pop} (SCB grid)",
        f"SCB population tables 1, 2, 4, 10 on the grid, {y_pop}. Shares over the sum of sub-groups (D3).",
        RUTA_LAYER, idx, pv, {"sources": [str(t.path) for t in (t_age, t_sex, t_birth, t_edu) if t]},
    )

    if t_inc is not None:
        y_inc = year_of(t_inc)
        q = [col(inc, f"Kvartil{i}", idx) for i in range(1, 5)]
        q_sum = sum(q)
        households = col(inc, "Totalt", idx)
        income_sum = col(inc, "Tot_CDISP0", idx)
        mean = (income_sum / households.where(households > 0)).astype(float)
        report["mean_income_undefined_cells"] = int((households.eq(0) & income_sum.gt(0)).sum())
        iv = [
            Variable("households", f"Households {y_inc}", households, "Integer", "households", "SCB Tab11 Totalt (households 20+)"),
            Variable("mean_economic_standard", f"Mean economic standard {y_inc}", mean, "Float", "SEK",
                     "Tot_CDISP0 / Totalt; no value where Totalt = 0 (D4)", "derived"),
            Variable("share_low_income_q1", "Households in lowest income quartile", share(q[0], q_sum), "Float", "%", "Kvartil1 / sum of quartiles", "derived"),
            Variable("share_high_income_q4", "Households in highest income quartile", share(q[3], q_sum), "Float", "%", "Kvartil4 / sum of quartiles", "derived"),
            Variable("income_q1", "Households, quartile 1 (lowest)", q[0], "Integer", "households", "Kvartil1"),
            Variable("income_q2", "Households, quartile 2", q[1], "Integer", "households", "Kvartil2"),
            Variable("income_q3", "Households, quartile 3", q[2], "Integer", "households", "Kvartil3"),
            Variable("income_q4", "Households, quartile 4 (highest)", q[3], "Integer", "households", "Kvartil4"),
            Variable("income_sum", "Sum of economic standard", income_sum, "Float", "SEK", "Tot_CDISP0"),
        ]
        report["ruta_income"] = write_data_layer(
            package_dir, "ruta_income", f"Income {y_inc} (SCB grid)",
            f"SCB income table 11 on the grid, {y_inc}. Economic standard = disposable income per consumption unit.",
            RUTA_LAYER, idx, iv, {"sources": [str(t_inc.path)]},
        )
    return units, report


# =============================================================================
# DeSO
# =============================================================================

def export_deso(package_dir: Path, population: Dict[str, SourceTable], income: Dict[str, SourceTable]) -> Tuple[gpd.GeoDataFrame, Dict]:
    t_age = find_table(population, "1", "DeSO")
    t_birth = find_table(population, "4", "DeSO")
    t_change = find_table(population, "5", "DeSO")
    t_inc = find_table(income, "11", "DeSO")
    used = [t for t in (t_age, t_birth, t_change, t_inc) if t is not None]
    if not used:
        raise ValueError("No DeSO tables found")

    def deso_key(t: SourceTable) -> pd.Series:
        return f"{DESO_LAYER}:" + t.data[t.id_column].astype("string").str.strip()

    # Geometry: first table that has it for the area.
    geoms: Dict[str, object] = {}
    for t in used:
        for k, g in zip(deso_key(t), t.data.geometry):
            if k not in geoms and g is not None and not g.is_empty:
                geoms[k] = g
    all_keys = sorted(set().union(*[set(deso_key(t)) for t in used]))
    without_geometry = [k for k in all_keys if k not in geoms]
    keys = [k for k in all_keys if k in geoms]
    # Readable name; the DeSO code stays in the unit id.
    units = gpd.GeoDataFrame({"unit_id": keys, "display_name": ["DeSO area"] * len(keys)},
                             geometry=[geoms[k] for k in keys], crs="EPSG:3006")
    idx = pd.Index(keys)
    report = {"tables": {t.key: int(len(t.data)) for t in used}, "units": len(keys), "without_geometry": without_geometry}
    write_spatial_layer(package_dir, SpatialLayerSpec(
        DESO_LAYER, "DeSO areas", "SCB demographic statistical areas (DeSO), as delivered (cut to the delivery rectangle).",
        visible_by_default=False, provenance={"sources": [str(t.path) for t in used]},
    ), units)

    age, birth, change, inc = (keyed(t, deso_key) for t in (t_age, t_birth, t_change, t_inc))
    age_cols = ["Alder_0_6", "Alder_7_15", "Alder_16_1", "Alder_20_2", "Alder_25_4", "Alder_45_6", "Alder_65"]
    age_sum = sum(col(age, c, idx) for c in age_cols)
    birth_sum = sum(col(birth, c, idx) for c in ["Sverige", "Norden_uto", "EU_utom_No", "Ovriga_var"])
    stock = col(change, "Tot_Bef", idx)
    net = col(change, "F_Till", idx) - col(change, "F_Fran", idx) + col(change, "Inv", idx) - col(change, "Utv", idx)
    y = year_of(t_age)
    pv = [
        Variable("population", f"Population {y}", col(age, "Totalt", idx), "Integer", "persons", "SCB Tab1 Totalt (whole DeSO)"),
        Variable("share_children_0_15", "Children 0-15", share(col(age, "Alder_0_6", idx) + col(age, "Alder_7_15", idx), age_sum), "Float", "%", "", "derived"),
        Variable("share_65_plus", "Age 65+", share(col(age, "Alder_65", idx), age_sum), "Float", "%", "", "derived"),
        Variable("share_born_abroad", "Born abroad", share(birth_sum - col(birth, "Sverige", idx), birth_sum), "Float", "%", "", "derived"),
        Variable("share_born_outside_europe", "Born outside Europe", share(col(birth, "Ovriga_var", idx), birth_sum), "Float", "%", "", "derived"),
        Variable("births", "Births", col(change, "Fodda", idx), "Integer", "persons", "Fodda (meaning inferred)"),
        Variable("deaths", "Deaths", col(change, "Doda", idx), "Integer", "persons", "Doda (meaning inferred)"),
        Variable("net_migration_per_1000", "Net migration per 1,000 residents", (1000.0 * net / stock.where(stock > 0)).astype(float),
                 "Float", "‰", "(moves in - moves out + immigrated - emigrated) / Tot_Bef x 1000 (Tab5 meanings inferred)", "derived"),
    ]
    report["deso_population"] = write_data_layer(
        package_dir, "deso_population", f"Population {y} (DeSO)",
        f"SCB population tables 1, 4, 5 per DeSO area, {y}. Values describe the whole DeSO even where its polygon is cut.",
        DESO_LAYER, idx, pv, {"sources": [str(t.path) for t in (t_age, t_birth, t_change) if t]},
    )
    if t_inc is not None:
        yi = year_of(t_inc)
        q = [col(inc, f"Kvartil{i}", idx) for i in range(1, 5)]
        q_sum = sum(q)
        iv = [
            Variable("median_income", f"Median income {yi}", col(inc, "MedianInk", idx), "Float", "SEK",
                     "MedianInk; presumably median economic standard (inferred). Never sum or average across areas."),
            Variable("households", f"Households {yi}", col(inc, "Totalt", idx), "Integer", "households", "Totalt"),
            Variable("share_low_income_q1", "Households in lowest income quartile", share(q[0], q_sum), "Float", "%", "", "derived"),
            Variable("share_high_income_q4", "Households in highest income quartile", share(q[3], q_sum), "Float", "%", "", "derived"),
        ]
        report["deso_income"] = write_data_layer(
            package_dir, "deso_income", f"Income {yi} (DeSO)", f"SCB income table 11 per DeSO area, {yi}.",
            DESO_LAYER, idx, iv, {"sources": [str(t_inc.path)]},
        )
    return units, report


def load_statistics(population_dir: Path, income_dir: Optional[Path]) -> Tuple[Dict[str, SourceTable], Dict[str, SourceTable]]:
    population = load_scb_tables(population_dir)
    income = load_scb_tables(income_dir) if income_dir else {}
    return population, income
