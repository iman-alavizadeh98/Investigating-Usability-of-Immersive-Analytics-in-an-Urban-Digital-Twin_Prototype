"""
Building links and building-based estimates for the Unity package.

Associations (building → unit, one target per building)
  buildings_to_ruta, buildings_to_deso, buildings_to_valdistrikt
  A building belongs to the unit that contains its representative point (a
  point guaranteed inside the footprint, unlike the centroid). Buildings in no
  unit (e.g. no populated grid cell) get no link and are counted.

Dasymetric estimate (shortcuts D1 + D2; see docs/decisions/2026-10-07_dasymetric-grid-to-district.md)
  Grid counts are spread over the residential buildings of each cell in
  proportion to residential floor area:
      floors_i = max(1, round(height_i / storey_height))
      weight_i = footprint_area_i x floors_i
      count_i  = count_cell x weight_i / sum(weight_j over residential buildings j in the cell)
  Fallbacks: a cell with no residential building spreads over all its buildings
  (same weight); a cell with no building at all is assigned whole to the district
  containing its representative point. Building estimates are then summed per
  voting district. Districts not fully inside the area covered by SCB data get no
  estimate (their population would be incomplete).

Validation: building estimates of population, summed into SCB's 100 m grid
cells, are compared with SCB's own 100 m totals (an independent, finer table).
"""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Dict, Optional, Tuple

import geopandas as gpd
import numpy as np
import pandas as pd
import pyogrio
from shapely.geometry import box

from pipelines.scb.loader import SourceTable

from .layer_files import Variable, write_association, write_data_layer
from .statistics import RUTA_LAYER, col, find_table, keyed, ruta_key

logger = logging.getLogger(__name__)

BUILDING_PREFIX = "building:"
RESIDENTIAL_CATEGORY = "Residential"
#: Counts carried from the grid to buildings (output name → how to compute it from the grid tables).
ALLOCATED = ("population", "age_sum", "age_65_plus", "birth_sum", "born_abroad", "households", "income_sum", "q1", "q_sum")


def load_building_points(path: Path, layer: Optional[str]) -> gpd.GeoDataFrame:
    gdf = pyogrio.read_dataframe(path, layer=layer, columns=["object_id", "object_type_category", "height_m", "has_height"])
    gdf["building_id"] = BUILDING_PREFIX + gdf["object_id"].astype(str)
    gdf["footprint_m2"] = gdf.geometry.area
    gdf["is_residential"] = gdf["object_type_category"].astype("string").eq(RESIDENTIAL_CATEGORY).fillna(False)
    gdf["height_m"] = pd.to_numeric(gdf["height_m"], errors="coerce").fillna(0.0)
    gdf = gdf.set_geometry(gdf.geometry.representative_point())
    return gdf[["building_id", "is_residential", "height_m", "footprint_m2", "geometry"]]


def assign(points: gpd.GeoDataFrame, units: gpd.GeoDataFrame) -> pd.Series:
    """building_id → unit_id of the unit containing the point (first match on shared borders)."""
    joined = gpd.sjoin(points[["building_id", "geometry"]], units[["unit_id", "geometry"]], predicate="within", how="inner")
    joined = joined.drop_duplicates("building_id")
    return pd.Series(joined["unit_id"].to_numpy(), index=joined["building_id"].to_numpy())


def export_associations(package_dir: Path, points: gpd.GeoDataFrame, layers: Dict[str, gpd.GeoDataFrame]) -> Dict:
    report = {}
    for layer_id, units in layers.items():
        links = assign(points, units).sort_index()
        aid = f"buildings_to_{layer_id}"
        write_association(package_dir, aid, "buildings", layer_id, list(links.index), list(links.to_numpy()),
                          f"Building → {layer_id} unit containing the building's representative point")
        report[aid] = {"linked": int(len(links)), "unlinked": int(len(points) - len(links))}
    return report


def grid_counts(population: Dict[str, SourceTable], income: Dict[str, SourceTable], index: pd.Index) -> pd.DataFrame:
    grid = {250, 1000}
    age = keyed(find_table(population, "1", "Ruta", grid), ruta_key)
    birth = keyed(find_table(population, "4", "Ruta", grid), ruta_key)
    inc = keyed(find_table(income, "11", "Ruta", grid), ruta_key)
    age_cols = ["Alder_0_6", "Alder_7_15", "Alder_16_1", "Alder_20_2", "Alder_25_4", "Alder_45_6", "Alder_65"]
    birth_sum = sum(col(birth, c, index) for c in ["Sverige", "Norden_uto", "EU_utom_No", "Ovriga_var"])
    counts = pd.DataFrame({
        "population": col(age, "Totalt", index),
        "age_sum": sum(col(age, c, index) for c in age_cols),
        "age_65_plus": col(age, "Alder_65", index),
        "birth_sum": birth_sum,
        "born_abroad": birth_sum - col(birth, "Sverige", index),
        "households": col(inc, "Totalt", index),
        "income_sum": col(inc, "Tot_CDISP0", index),
        "q1": col(inc, "Kvartil1", index),
        "q_sum": sum(col(inc, f"Kvartil{i}", index) for i in range(1, 5)),
    }, index=index)
    return counts.fillna(0.0)  # a cell missing from one table contributes nothing to that count


def dasymetric(points: gpd.GeoDataFrame, ruta_units: gpd.GeoDataFrame, counts: pd.DataFrame, storey_height_m: float) -> Tuple[pd.DataFrame, pd.DataFrame, Dict]:
    """Returns (building allocations indexed by building_id, unallocated cells indexed by unit_id, report)."""
    cell_of = assign(points, ruta_units)
    b = points.set_index("building_id").loc[cell_of.index].copy()
    b["cell"] = cell_of
    b["floors"] = np.maximum(1, np.round(b["height_m"] / storey_height_m))
    b["weight"] = b["footprint_m2"] * b["floors"]

    res_weight = b[b["is_residential"]].groupby("cell")["weight"].sum()
    all_weight = b.groupby("cell")["weight"].sum()
    use_all = ~b["cell"].isin(res_weight.index[res_weight > 0])
    b["share"] = np.where(
        b["is_residential"] & ~use_all, b["weight"] / b["cell"].map(res_weight),
        np.where(use_all, b["weight"] / b["cell"].map(all_weight), 0.0),
    )
    alloc = b[["cell", "share"]].join(counts, on="cell")
    for c in ALLOCATED:
        alloc[c] = alloc[c] * alloc["share"]
    alloc = alloc[alloc["share"] > 0]

    cells_with_buildings = set(b["cell"])
    leftover = counts.loc[[u for u in counts.index if u not in cells_with_buildings]]
    leftover = leftover[leftover["population"] + leftover["households"] > 0]

    fallback_cells = sorted(set(b.loc[use_all, "cell"]))
    report = {
        "storey_height_m": storey_height_m,
        "buildings_in_populated_cells": int(len(b)),
        "residential_buildings_receiving_population": int((b["is_residential"] & ~use_all).sum()),
        "cells_spread_over_non_residential_buildings": len(fallback_cells),
        "cells_without_any_building": int(len(leftover)),
        "population_in_cells_without_buildings": float(leftover["population"].sum()),
        "population_total_grid": float(counts["population"].sum()),
        "population_allocated_to_buildings": float(alloc["population"].sum()),
    }
    return alloc, leftover, report


def validate_100m(alloc: pd.DataFrame, points: gpd.GeoDataFrame, population: Dict[str, SourceTable]) -> Dict:
    t100 = find_table(population, "6", "Ruta", {100})
    if t100 is None:
        return {"skipped": "no 100 m population table"}
    pts = points.set_index("building_id").loc[alloc.index]
    e = (np.floor(pts.geometry.x / 100) * 100).astype(int)
    n = (np.floor(pts.geometry.y / 100) * 100).astype(int)
    est = alloc["population"].groupby([e.to_numpy(), n.to_numpy()]).sum()
    est.index = [f"{a:06d}{b:07d}" for a, b in est.index]
    ids = t100.data[t100.id_column].astype("string").str.strip()
    obs = pd.Series(pd.to_numeric(t100.data["Totalt"], errors="coerce").to_numpy(), index=ids.to_numpy())
    both = pd.concat([obs.rename("observed"), est.rename("estimated")], axis=1).fillna(0.0)
    # Compare only inside the 100 m table's extent.
    resid = both["estimated"] - both["observed"]
    ss_res = float((resid ** 2).sum())
    ss_tot = float(((both["observed"] - both["observed"].mean()) ** 2).sum())
    return {
        "cells_compared": int(len(both)),
        "r2": round(1 - ss_res / ss_tot, 3) if ss_tot else None,
        "mae_persons": round(float(resid.abs().mean()), 2),
        "observed_total": float(both["observed"].sum()),
        "estimated_total": float(both["estimated"].sum()),
        "note": "estimated cells with no 100 m observation count as observed 0 (SCB lists only populated cells)",
    }


def export_district_estimates(
    package_dir: Path,
    alloc: pd.DataFrame,
    leftover: pd.DataFrame,
    points: gpd.GeoDataFrame,
    ruta_units: gpd.GeoDataFrame,
    districts: gpd.GeoDataFrame,
    eligible: pd.Series,
    statistics_extent,
    min_coverage_pct: float,
    years: Dict[str, Optional[int]],
) -> Dict:
    district_of = assign(points, districts)
    sums = alloc.join(district_of.rename("district")).groupby("district")[list(ALLOCATED)].sum()
    if len(leftover):
        cell_pts = ruta_units.set_index("unit_id").loc[leftover.index].geometry.representative_point()
        cp = gpd.GeoDataFrame({"building_id": leftover.index}, geometry=cell_pts.to_numpy(), crs=districts.crs)
        cell_district = assign(cp, districts)
        extra = leftover.loc[cell_district.index, list(ALLOCATED)].groupby(cell_district.to_numpy()).sum()
        sums = sums.add(extra, fill_value=0.0)

    unit_ids = list(districts["unit_id"])
    sums = sums.reindex(unit_ids).fillna(0.0)
    coverage = pd.Series(
        (100.0 * districts.geometry.intersection(statistics_extent).area / districts.geometry.area).to_numpy(), index=unit_ids)
    ok = coverage >= min_coverage_pct

    def masked(s: pd.Series) -> pd.Series:
        return s.where(ok)

    def ratio(a, b, scale=100.0):
        return masked(scale * sums[a] / sums[b].where(sums[b] > 0))

    elig = pd.Series(eligible.reindex([u.split(":", 1)[1] for u in unit_ids]).to_numpy(), index=unit_ids)
    variables = [
        Variable("statistics_coverage", "Area covered by SCB data", coverage, "Float", "%",
                 "share of the district's area inside the SCB delivery extent", "derived"),
        Variable("residents_est", f"Residents (estimate, {years['population']})", masked(sums["population"]), "Float", "persons",
                 "grid population spread to residential buildings by floor area, summed per district", "dasymetric_estimate"),
        Variable("households_est", f"Households (estimate, {years['income']})", masked(sums["households"]), "Float", "households", "", "dasymetric_estimate"),
        Variable("mean_economic_standard_est", f"Mean economic standard (estimate, {years['income']})",
                 masked(sums["income_sum"] / sums["households"].where(sums["households"] > 0)), "Float", "SEK",
                 "sum of allocated income / sum of allocated households", "dasymetric_estimate"),
        Variable("share_low_income_est", "Households in lowest income quartile (estimate)", ratio("q1", "q_sum"), "Float", "%", "", "dasymetric_estimate"),
        Variable("share_born_abroad_est", "Born abroad (estimate)", ratio("born_abroad", "birth_sum"), "Float", "%", "", "dasymetric_estimate"),
        Variable("share_65_plus_est", "Age 65+ (estimate)", ratio("age_65_plus", "age_sum"), "Float", "%", "", "dasymetric_estimate"),
        Variable("eligible_per_resident", "Eligible voters per resident (check)", masked(elig / sums["population"].where(sums["population"] > 0)),
                 "Float", "", "eligible voters / estimated residents; ~0.8 expected", "derived"),
    ]
    rep = write_data_layer(
        package_dir, "valdistrikt_estimates", "Population and income per voting district (estimates)",
        "SCB grid statistics moved to voting districts through buildings (dasymetric, floor-area weighted). "
        f"Only districts at least {min_coverage_pct:.0f} % inside the SCB extent have values.",
        "valdistrikt", unit_ids, variables, {"method": "D1 + D2, see links.py and the decision note"},
    )
    rep["districts_with_estimates"] = int(ok.sum())
    ratio_vals = (elig / sums["population"].where(sums["population"] > 0))[ok]
    rep["eligible_per_resident_median"] = round(float(ratio_vals.median()), 3) if len(ratio_vals) else None
    return rep


def export_area_names(package_dir: Path, layer_id: str, units: gpd.GeoDataFrame, districts: gpd.GeoDataFrame) -> Dict:
    """
    Data layer "<layer>_places" with one String variable `area_name`: the name of
    the voting district containing each unit's representative point. Gives grid
    cells and DeSO areas a readable place name in the info panel. A unit outside
    every district has no value.
    """
    pts = gpd.GeoDataFrame({"building_id": units["unit_id"].to_numpy()},
                           geometry=units.geometry.representative_point().to_numpy(), crs=units.crs)
    district_of = assign(pts, districts)
    names = districts.set_index("unit_id")["display_name"]
    values = [names.get(district_of.get(u)) if u in district_of.index else None for u in units["unit_id"]]
    return write_data_layer(
        package_dir, f"{layer_id}_places", "Place names",
        "Name of the voting district containing the unit (for display only).",
        layer_id, list(units["unit_id"]),
        [Variable("area_name", "Area", values, "String", "", "voting district containing the unit's centre", "derived")],
    )


def statistics_extent(population: Dict[str, SourceTable]):
    """Rectangle covered by the SCB delivery (bounds of the delivered grid cells, which are cut to it)."""
    t = find_table(population, "1", "Ruta", {250, 1000})
    return box(*t.data.total_bounds)
