"""
Assemble the data report: figures + numbers + explanatory text -> one PDF.

Every number in the text comes from the data at run time (figure `facts` or
direct computation), so re-running after a data update keeps text and charts
consistent. Interpretive sentences are worded around those numbers.
"""

from __future__ import annotations

import logging
from datetime import datetime
from pathlib import Path
from typing import Dict

import numpy as np
import pandas as pd

from pipelines.employment.config import EMPLOYMENT
from pipelines.income.config import INCOME
from pipelines.population.config import POPULATION

from . import figures as F
from .pdf import ReportDoc
from .sources import ReportData

logger = logging.getLogger(__name__)


def n(x, d: int = 0) -> str:
    """Thousands with commas; NaN -> '–'."""
    if x is None or (isinstance(x, float) and np.isnan(x)):
        return "–"
    return f"{x:,.{d}f}"


def pct(x, d: int = 1) -> str:
    return f"{x:.{d}f} %"


def _shp_count(folder) -> int:
    return len(list(Path(folder).glob("*.shp"))) if folder else 0


def build_report(d: ReportData, out_dir: Path) -> Path:
    out_dir = Path(out_dir)
    fig_dir = out_dir / "figures"
    fig_dir.mkdir(parents=True, exist_ok=True)
    city = d.cfg.display_name
    logger.info("Drawing figures")
    figs: Dict[str, tuple] = {}
    for name in ("extents", "data_years", "landcover", "building_types", "building_heights", "building_height_map",
                 "height_quality", "ground", "lidar", "population_structure", "population_maps", "deso_population",
                 "income", "employment_maps", "employment_sectors", "education", "election", "relationships", "correlation"):
        logger.info(f"  - {name}")
        figs[name] = getattr(F, f"fig_{name}")(d, fig_dir)
    f = {k: v[1] for k, v in figs.items()}
    path = {k: v[0] for k, v in figs.items()}

    b = d.buildings
    hs = d.height_summary
    pop = f["population_structure"]
    emp = f["employment_maps"]
    sec = f["employment_sectors"]
    el = f["election"]
    rel = f["relationships"]
    box_ = d.extents["Study area (LiDAR tiles)"]
    box_km2 = (box_[2] - box_[0]) * (box_[3] - box_[1]) / 1e6
    deso_pop_total = d.values["deso_population"]["population"].sum()

    doc = ReportDoc()

    # ------------------------------------------------------------------ cover
    doc.title(f"{city}: the data behind the digital twin",
              f"Every dataset in the prototype, what it contains, how it covers the city and how the datasets relate. "
              f"Generated {datetime.now():%Y-%m-%d} from the project data.")
    doc.tiles([
        (n(len(b)), f"buildings with footprint and type; {100 * b['has_height'].astype(bool).mean():.0f} % with a measured height"),
        (n(pop["total"]), f"residents on the SCB grid ({_year(d.population_tables)})"),
        (n(emp["jobs"]), f"jobs at workplaces on the grid ({_year(d.employment_tables)})"),
        (n(d.election["cast"]), f"votes cast, municipal election {d.election['year']}"),
    ])
    doc.note("<b>Licence.</b> Most of the data shown here is licensed under <b>FUK</b> (research, education and culture) "
             "from Lantmäteriet and SCB via SLU GET. Keep this PDF to yourself and your supervisors: do not publish it, "
             "upload it or commit it to git. Vägkartan (the road map) is open data (CC0); the election data's licence is "
             "not stated in the files.")
    doc.h2("Contents")
    doc.toc()
    doc.page_break()
    doc.h2("Key findings")
    lc_areas = f["landcover"]["areas_km2"]
    doc.bullets([
        f"<b>A compact coastal city.</b> {n(len(b))} buildings, {n(pop['total'])} residents and {n(emp['jobs'])} jobs in a "
        f"rectangle of about {f['landcover']['total_km2']:.0f} km², {lc_areas['Built-up area'] / f['landcover']['total_km2'] * 100:.0f} % "
        f"of it built-up. Most buildings are low (median {f['building_heights']['median']:.1f} m); the tall ones line the "
        f"centre and the harbour.",
        f"<b>One social pattern dominates.</b> Across DeSO areas, income, employment and birth country move almost "
        f"together (income vs employment ρ = {rel['income_employment'][0]:.2f}, vs born abroad ρ = {rel['income_abroad'][0]:.2f}). "
        f"Richer, mostly Swedish-born areas lie along the northern coast and in the south-east; the inner south and the "
        f"north-eastern estates are the opposite.",
        f"<b>Voting follows it.</b> Turnout rises with estimated district income (ρ = {rel['income_turnout'][0]:.2f}). "
        f"S is the largest party overall ({list(el['shares'].values())[0]:.1f} %) and leads in {el['winners'].get('S', 0)} "
        f"districts; M leads in {el['winners'].get('M', 0)}, mostly the wealthier coastal areas.",
        "<b>Age and jobs are separate dimensions.</b> The shares of children and of people aged 65+ and the location of "
        "workplaces barely follow income, so they are the most informative layers to contrast with it.",
        f"<b>Work and home are apart.</b> {emp['job_cells_no_residents']} grid cells have jobs but no residents (centre, "
        f"harbour, eastern industrial and retail areas).",
        f"<b>Heights are good but not complete.</b> {100 * b['has_height'].astype(bool).mean():.0f} % of buildings have a "
        f"measured height from the 2018 surface model; {n(f['building_height_map']['no_height'])} have none, mostly newer buildings.",
        "<b>Mind the gaps.</b> The datasets cover different areas and years (2010–2026), and SCB blurs small counts; "
        "Section 10 lists what that means for each comparison.",
    ])
    doc.h2("How to read this report")
    doc.bullets([
        "<b>Places are counted in four kinds of units.</b> Single <b>buildings</b>; SCB's statistical <b>grid</b> "
        "(<i>rutor</i>: 250 m squares in built-up areas, 1 km squares elsewhere); <b>DeSO</b> areas (<i>demografiska "
        "statistikområden</i>, 700–2,700 residents each); and <b>voting districts</b> (<i>valdistrikt</i>). Each map says which.",
        "<b>A missing area means no data, not zero.</b> SCB leaves out empty grid cells and suppresses some small ones. "
        "Hatched grey = no data.",
        "<b>Small counts are deliberately blurred by SCB</b> (perturbation, a few persons up or down), so the parts of a "
        "table rarely add up exactly to its total, and percentages in nearly empty cells are unreliable.",
        "<b>The datasets come from different years</b> (2010 to 2026, Figure 2). Read every comparison with that in mind.",
        "Swedish names are kept next to the English ones, so you can find them in the source files.",
        "<b>Colours:</b> light to dark blue = low to high. Blue ↔ red = below ↔ above a neutral middle (grey). "
        "Classes are chosen so each holds a similar number of areas, unless stated.",
    ])

    # ------------------------------------------------------------------ 1 inventory
    doc.h1("1  What data we have")
    dl = d.deliveries
    rows = [
        ["<i>Byggnad</i>", "Buildings (footprints, type, purpose)", f"Lantmäteriet · FUK · delivered {dl.get('buildings', {}).get('delivered', '?')}",
         f"versions {pd.to_datetime(b['version_valid_from'], errors='coerce').min():%Y}–{pd.to_datetime(b['version_valid_from'], errors='coerce').max():%Y}",
         "building", f"{n(len(b))} buildings"],
        ["<i>Laserdata NH</i>", "Laser point cloud (national elevation model)", "Lantmäteriet · FUK",
         f"scanned {d.lidar_strips['date'].min()}", "points, 2.5 km tiles", f"{f['lidar']['points'] / 1e6:.1f} M points, {f['lidar']['tiles']} tiles"],
        ["<i>Ytmodell från flygbild</i>", "Surface model from aerial photos (0.5 m)", "Lantmäteriet · FUK",
         f"photographed {d.dsm_tiles['date_from'].min()[:7]}", "surface points, 2.5 km tiles", f"{len(d.dsm_tiles)} tiles"],
        ["<i>Befolkning</i>", "Population (age, sex, birth country, marital status, education, change)",
         "SCB · FUK", str(_year(d.population_tables)), "grid 250 m / 1 km, 100 m, DeSO", f"{len(d.population_tables)} tables"],
        ["<i>Inkomster</i>", "Income (households by economic standard)", "SCB · FUK", str(_year(d.income_tables)),
         "grid, DeSO", f"{len(d.income_tables)} tables"],
        ["<i>Arbetsmarknad – Utbildning</i>", "Labour market and education", "SCB · FUK", f"{_year(d.employment_tables)}",
         "grid, DeSO", f"{len(d.employment_tables)} tables"],
        ["<i>Röster per distrikt; Valdistrikt</i>", "Votes per voting district; district boundaries",
         "Valmyndigheten (assumed) · licence not stated", str(d.election["year"]), "voting district",
         f"{len(d.election['districts'])} districts"],
        ["<i>Vägkartan</i>", "Road map 1:100 000 (roads, land cover, water, rail)", f"Lantmäteriet · CC0 · delivered {dl.get('roadmap', {}).get('delivered', '?')}",
         "–", "lines, polygons", f"{_shp_count(d.report_cfg and d.repo_root / d.report_cfg.get('roadMap', ''))} layers"],
        ["<i>Fastighetskartan Kommunikation</i>", "Property map 1:10 000, transport (streets, paths, rail, names)",
         f"Lantmäteriet · FUK · delivered {dl.get('propertymap', {}).get('delivered', '?')}", "edits to 2021", "lines, points, polygons",
         f"{_shp_count(d.repo_root / d.report_cfg.get('propertyMap', ''))} layers"],
    ]
    doc.table(["Swedish name", "What it is", "Source · licence", "Reference time", "Unit", "Size"], rows,
              widths=[0.17, 0.25, 0.2, 0.13, 0.13, 0.12],
              caption="Every dataset in the prototype. All are in SWEREF 99 TM (EPSG:3006). SCB years are the data years in the "
                      "file names; 'delivered' is when SLU GET received the product.")
    doc.p(f"<b>The deliveries do not cover the same area</b> (Figure 1). The <b>study area</b> is the footprint of the 15 LiDAR "
          f"tiles: {box_[2] - box_[0]:,.0f} × {box_[3] - box_[1]:,.0f} m ({box_km2:.1f} km², E {box_[0]:,.0f}–{box_[2]:,.0f}, "
          f"N {box_[1]:,.0f}–{box_[3]:,.0f}). The buildings, road map, property map, population and income were cut to a smaller "
          f"rectangle around the buildings. The employment delivery was cut to the study area, so it reaches further. "
          f"The election covers the <b>whole municipality</b>: only {el['districts_in_frame']} of its {len(d.package['valdistrikt'])} "
          f"voting districts lie fully inside the map frame used in this report.")
    doc.figure(path["extents"], "Extents of the deliveries. Left: the whole municipality with its voting districts. Right: the "
               "study area with the buildings; each rectangle is the bounding box of one delivery.", max_h_frac=0.5)
    doc.figure(path["data_years"], "When each dataset describes the city. Building footprints are updated object by object "
               "(the bar spans their version dates). The SCB tables describe 31 December of their year.")
    doc.p("The <b>2010 laser scan</b> is 15 years older than the building footprints, which is why heights come from the "
          "<b>2018 surface model</b> (Section 3). Population (2024), income (2023), employment (2023) and the 2026 election are "
          "close enough to compare, but they are not the same moment.")

    # ------------------------------------------------------------------ 2 base map
    doc.h1("2  Base map: land, water, roads")
    lc = f["landcover"]
    doc.p(f"Two Lantmäteriet map products give the city its context. <b>Vägkartan</b> (road map, 1:100 000) has land cover "
          f"(<i>my_riks</i>), water (<i>mv_riks</i>), {n(len(d.roadmap.get('vl_riks', [])))} road segments and railways. "
          f"<b>Fastighetskartan Kommunikation</b> (property map, 1:10 000) is far more detailed: "
          f"{n(len(d.propertymap.get('vl_riks', [])))} street segments, {n(len(d.propertymap.get('vo_riks', [])))} paths and "
          f"cycle ways, {n(len(d.propertymap.get('ba_riks', [])))} facility areas (sports grounds, plants), "
          f"{n(len(d.propertymap.get('tx_riks', [])))} place-name labels, quays and piers. Neither has street addresses.")
    doc.figure(path["landcover"], f"Land cover from Vägkartan within the delivery rectangle ({lc['total_km2']:.0f} km²). "
               "Land-cover polygons are generalised for 1:100 000, so small parks and blocks are merged.", max_h_frac=0.55)
    doc.p(f"Within the rectangle, <b>{lc['areas_km2']['Built-up area']:.0f} km²</b> is built-up, "
          f"{lc['areas_km2']['Open land']:.0f} km² open land (fields, parks, harbour areas), {lc['areas_km2']['Forest']:.0f} km² "
          f"forest and {lc['areas_km2']['Water']:.0f} km² water (Öresund on the west side). The city runs north–south along the "
          f"coast, with the railway and harbour along the shore.")

    # ------------------------------------------------------------------ 3 buildings
    doc.h1("3  Buildings")
    bt = f["building_types"]["by_type"]
    bh = f["building_heights"]
    doc.p(f"<b>{n(len(b))} buildings</b> from Lantmäteriet's <i>Byggnad</i>, after merging footprints that were delivered "
          f"in several pieces under one ID. Each building has a type (<i>objekttyp</i>), a purpose (<i>ändamål</i>), "
          f"a footprint area and, from the height pipeline, a height above ground. More than half of all buildings are "
          f"small ancillary buildings (<i>Komplementbyggnad</i>: sheds, garages), but most of the floor space is in "
          f"residences and industry (Figure {doc.fig_no + 1}).")
    doc.figure(path["building_types"], "Buildings by type (<i>objekttyp</i>). Left: count. Right: total footprint area.")
    rows = [[t, n(r["n"]), n(r["area"] / 1e4, 0), f"{bh['by_type_median'].get(t, np.nan):.1f}"] for t, r in bt.iterrows()]
    doc.table(["Type", "Buildings", "Footprint (ha)", "Median height (m)"], rows, widths=[0.4, 0.18, 0.2, 0.22],
              caption="Buildings by type. Heights only for buildings that have one.")
    doc.figure(path["building_heights"], f"Building heights ({n(bh['n'])} buildings with a height). Median {bh['median']:.1f} m, "
               f"90 % are below {bh['p90']:.1f} m, the tallest is {bh['max']:.0f} m.")
    doc.p("Height = the top of the roof (95th percentile of the surface points inside the footprint) minus the ground "
          "around the building. Most buildings are one- or two-storey houses and sheds; the tall ones are blocks of flats, "
          "offices and industrial halls in the centre and along the harbour (Figure "
          f"{doc.fig_no + 1}).")
    doc.figure(path["building_height_map"], "Building heights across the study area. Pink = no height could be measured "
               "(mostly buildings built after the 2018 photos).", max_h_frac=0.86)
    doc.h2("How reliable are the heights?")
    hq = f["height_quality"]
    doc.figure(path["height_quality"], "Left: which source each height comes from, and its quality label. Right: why "
               f"{n(f['building_height_map']['no_height'])} buildings have no height.")
    doc.bullets([
        f"<b>{n(hq['sources'].get('2018 surface model', 0))}</b> heights come from the 2018 surface model "
        f"(<i>Ytmodell från flygbild</i>), and only <b>{n(hq['sources'].get('2010 LiDAR', 0))}</b> from the 2010 laser scan, "
        f"where the surface model had too few points.",
        f"<b>{n(f['building_height_map']['no_height'])} buildings ({hs.get('percent_without_height', '?')} %) have no height.</b> "
        f"Most of them ({n(hq['reasons'].get('surface_shows_ground', 0))}) show bare ground in the 2018 surface, so they were "
        f"most likely built after April 2018 (e.g. the new Oceanhamnen district by the harbour).",
        f"{n(hs.get('height_change_flag_count', 0))} buildings are flagged because the 2010 and 2018 data disagree (rebuilt "
        f"or extended). Their 2018 height is used.",
        "Trees over a roof count as roof, so a few small buildings under trees are too tall.",
    ])
    gz = f["ground"]
    doc.figure(path["ground"], f"Ground level at each building (m above sea level, RH 2000), {gz['min']:.0f}–{gz['max']:.0f} m. "
               "The old town sits on the low shore; the land rises steeply to a plateau at about 40 m (the <i>landborg</i> ridge).",
               width_frac=0.7, max_h_frac=0.62)

    # ------------------------------------------------------------------ 4 LiDAR
    doc.h1("4  Elevation data: laser scan and surface model")
    lid = f["lidar"]
    st = d.lidar_strips
    doc.p(f"<b>Laserdata NH</b> (<i>Nationell höjdmodell</i>) is an airborne laser scan: {lid['points'] / 1e6:.1f} million "
          f"points over {lid['tiles']} tiles of 2.5 × 2.5 km, flown on <b>{st['date'].min()}</b> "
          f"({st['scanner'].iloc[0]}, about {st['altitude_m'].median():,.0f} m altitude, {len(st)} flight strips). "
          f"Density is {lid['density_min']:.1f}–{lid['density_max']:.1f} points per m² per tile; the lowest tile is mostly sea. "
          f"Points are classified only as ground, water, bridge and 'unclassified'. Roofs, trees and cars are all in "
          f"'unclassified', so the scan can't separate buildings from trees by itself.")
    doc.figure(path["lidar"], "Laser points per 2.5 km tile and by class. Ground points give the terrain under each building.",
               max_h_frac=0.5)
    doc.p(f"<b>Ytmodell från flygbild</b> (surface model from aerial photos) has one point every 0.5 m on top of whatever was "
          f"there (roofs, trees, ground), computed from photos taken {d.dsm_tiles['date_from'].min()} to "
          f"{d.dsm_tiles['date_to'].max()} at {d.dsm_tiles['photo_resolution_cm'].iloc[0]} cm resolution. It's 8 years newer "
          f"and denser than the laser scan, so it gives the roof heights. The laser scan's ground points still give the terrain, "
          f"because the ground changes far less than the buildings.")

    # ------------------------------------------------------------------ 5 population
    doc.h1("5  Population")
    a = pop["ages"]
    birth = pop["birth"]
    born_abroad = sum(v for k, v in birth.items() if k != "Sweden")
    doc.p(f"SCB's <i>Befolkning</i> tables count the registered residents on 31 December {_year(d.population_tables)}. On the "
          f"250 m / 1 km grid the delivery rectangle holds <b>{n(pop['total'])} residents</b> ({n(pop['women'])} women, "
          f"{n(pop['men'])} men). The {len(d.package['deso'])} DeSO areas with geometry hold {n(deso_pop_total)}"
          + (". That's more, because DeSO areas cut by the rectangle keep the population of the whole area."
             if deso_pop_total > pop["total"] else "."))
    doc.figure(path["population_structure"], "Residents by age group and by country of birth (grid totals). Age groups follow SCB's "
               "table, so they are not equally wide.")
    doc.p(f"<b>{pct(100 * born_abroad / sum(birth.values()))}</b> were born abroad, most of them outside Europe "
          f"({n(birth['Rest of world'])}). {pct(100 * a['65+'] / sum(a.values()))} are 65 or older and "
          f"{pct(100 * (a['0–6'] + a['7–15']) / sum(a.values()))} are children aged 0–15.")
    doc.figure(path["population_maps"], f"Population on the SCB grid. Density peaks at {n(f['population_maps']['density_max'])} "
               "residents per km² in the inner city. 1 km cells are drawn only where they don't overlap 250 m cells, because "
               "SCB gives them only the remaining population. Dashed line = study area.")
    doc.p("The maps show a clear pattern. The dense inner city and the large blocks of flats north-east and south-east of "
          "the centre have many residents born abroad. The coastal villa areas in the north and the far south are older, and "
          "most residents there were born in Sweden.")
    doc.figure(path["deso_population"], "The same population data per DeSO area. Net migration = (moved in − moved out + "
               "immigrated − emigrated) per 1,000 residents during the year; the meaning of SCB's change columns is inferred.")

    # ------------------------------------------------------------------ 6 income
    doc.h1("6  Income")
    inc = f["income"]
    doc.p(f"SCB's <i>Inkomster</i> describes <b>households</b> ({n(inc['households'])} on the grid) by their <b>economic "
          f"standard</b> (<i>ekonomisk standard</i>): disposable income per consumption unit, i.e. household income adjusted for "
          f"how many adults and children share it. The grid table gives the number of households in each income quartile "
          f"and the sum of their economic standard, so a mean per household can be derived. The <b>median</b> exists only per DeSO.")
    doc.figure(path["income"], f"Income. Left: mean economic standard per grid cell, hidden where a cell has fewer than 5 "
               f"households ({inc['cells_masked']} cells), because SCB's blurring makes those means meaningless. Middle and right: "
               f"median per DeSO, from {n(inc['deso_min'])} to {n(inc['deso_max'])} SEK (middle DeSO: {n(inc['deso_median'])} SEK).")
    doc.p("Income follows the same geography as birth country. It's highest in the villa areas along the northern coast and "
          "in the south-east, and lowest in the inner south and the north-eastern housing estates. The richest DeSO area has "
          f"about {inc['deso_max'] / inc['deso_min']:.1f} times the median standard of the poorest. Whether SCB's quartile "
          "boundaries are national or local is not stated in the delivery.")

    # ------------------------------------------------------------------ 7 employment
    doc.h1("7  Employment and education")
    doc.p(f"The <i>Arbetsmarknad – Utbildning</i> delivery counts employed people twice: <b>where they live</b> (Tab7, "
          f"<i>nattbefolkning</i>: {n(emp['employed_residents'])} employed residents aged 16+) and <b>where they work</b> "
          f"(Tab8, <i>dagbefolkning</i>: {n(emp['jobs'])} jobs). The two must never be added. Their ratio shows "
          f"which areas are workplaces and which are homes (Figure {doc.fig_no + 1}).")
    doc.figure(path["employment_maps"], f"Employment on the grid. Red cells have more jobs than employed residents (centre, harbour, "
               f"the industrial and retail areas in the east); blue cells are mainly residential. {emp['job_cells_no_residents']} "
               f"cells have jobs but no resident data at all. Ratio shown where a cell has at least 20 residents or jobs.")
    doc.figure(path["employment_sectors"], f"Top: sector of employment. The business sector (<i>Näringsliv</i>) dominates both views. "
               f"Bottom: share of residents aged 20–64 who are employed, per DeSO ({sec['rate_min']:.0f}–{sec['rate_max']:.0f} %, "
               f"middle {sec['rate_median']:.0f} %).", max_h_frac=0.62)
    doc.p(f"The employment rate varies widely, from about half to more than nine in ten. Its map is almost the same as the "
          f"income map (Section 9). The sector split is similar for residents and workplaces. The delivery's description is "
          f"an older edition that only splits public from private, so the meanings of <i>Staten</i> (state), <i>Kommun</i> "
          f"(municipality), <i>Region</i> and <i>Övrigt</i> (other) are inferred.")
    edu = f["education"]
    doc.figure(path["education"], f"Highest education of the {n(edu['edu_total'])} residents aged 25–64 (Tab10, education register). "
               "Left: share with at least three years of post-secondary education per grid cell.", max_h_frac=0.5)

    # ------------------------------------------------------------------ 8 election
    doc.h1(f"8  Municipal election {d.election['year']}")
    shares = el["shares"]
    top3 = list(shares.items())[:3]
    doc.p(f"Valmyndigheten's final count for the municipal council: <b>{n(d.election['cast'])} votes cast</b>, turnout "
          f"<b>{el['turnout_muni']:.1f} %</b> of {n(d.election['eligible'])} eligible voters. "
          + ", ".join(f"{k.split(' – ')[0]} {v:.1f} %" for k, v in top3) +
          f". {n(d.election['collection_cast'])} votes ({100 * d.election['collection_cast'] / d.election['cast']:.1f} %) were "
          f"counted in a collection district (<i>uppsamlingsdistrikt</i>) that has no area, so they can't be mapped.")
    doc.figure(path["election"], f"Election results. Turnout ranges from {el['turnout_min']:.0f} % to {el['turnout_max']:.0f} % "
               f"between voting districts. The maps are cropped to the study area; outer districts continue beyond the frame.",
               max_h_frac=0.86)
    w = el["winners"]
    doc.p(f"The geography repeats the income map. S is the largest party in {w.get('S', 0)} districts, mostly in the centre and "
          f"the east. M leads in {w.get('M', 0)} districts along the northern coast and in the south-east villa areas. "
          f"SD leads in {w.get('SD', 0)} districts on the eastern outskirts, none of them inside the study area. Turnout is lowest in the same districts "
          f"where income and employment are lowest.")

    # ------------------------------------------------------------------ 9 relationships
    doc.h1("9  How the datasets relate")
    doc.p("Areas, not people, are compared here. A strong correlation between two area averages doesn't mean the same holds "
          "for individuals (the <i>ecological fallacy</i>), and none of these numbers shows cause and effect. Spearman's ρ "
          "is used: it measures whether two values rise together in rank (+1), fall against each other (−1) or are "
          "unrelated (0), and is not thrown by a few extreme areas.")
    doc.figure(path["relationships"], "Three strong area-level relationships. Voting-district income is a <b>dasymetric "
               "estimate</b>: grid values spread over residential buildings and summed per district. It exists only for the "
               f"{rel['income_turnout'][1]} districts at least 99 % inside the statistics area.")
    corr_fig = (path["correlation"], f"Spearman correlations between DeSO-level variables (n = {f['correlation']['n']} DeSO areas). "
                "Red = rise together, blue = move in opposite directions.")
    doc.bullets([
        f"<b>Income, employment and birth country form one block.</b> Median income vs employment rate ρ = "
        f"{rel['income_employment'][0]:.2f}; vs share born abroad ρ = {rel['income_abroad'][0]:.2f}. On a map they "
        f"are nearly the same layer, so showing them together adds little; choose one or contrast them deliberately.",
        f"<b>Turnout follows income</b> (ρ = {rel['income_turnout'][0]:.2f} across voting districts).",
        "<b>Age is a separate dimension.</b> Shares of children and of people aged 65+ are only weakly related to income, so "
        "they make good contrasting layers.",
        "<b>Jobs per resident is independent of the social variables.</b> Workplaces sit in their own areas.",
    ])
    doc.figure(corr_fig[0], corr_fig[1], width_frac=0.85, max_h_frac=0.55)

    # ------------------------------------------------------------------ 10 limitations
    doc.h1("10  Data quality and limitations")
    rows = [
        ["Different extents", "Deliveries cover different rectangles; the election covers the whole municipality.",
         "Compare datasets only inside their overlap (Figure 1)."],
        ["Different years", "2010 laser scan … 2026 election.", "Say the year next to every value; heights use the 2018 surface."],
        ["Missing ≠ 0", "SCB leaves empty or suppressed cells out.", "Hatched as 'no data', never drawn as zero."],
        ["Blurred small counts", "SCB perturbs small counts; parts ≠ total in most rows.",
         "Shares are computed over the sum of the parts; small cells masked or read with care."],
        ["Mixed grid", "1 km cells overlap 250 m cells but hold only the remainder.", "1 km cells drawn as the remainder area."],
        ["Cut edge units", "Edge cells and DeSO areas are cut by the delivery rectangle but keep whole-area values.",
         "Read edge values as whole-area values; don't divide by the cut area."],
        ["Older variable description", "SCB's variable PDF is older than the data; some meanings are inferred.",
         "Each field is marked 'pdf' or 'inferred' (Appendix A)."],
        ["Heights after 2018", f"{n(f['building_height_map']['no_height'])} buildings have no height; trees can inflate small ones.",
         "Each building without a height carries the reason (<i>no_height_reason</i>); treat low heights with care."],
        ["Election collection district", f"{100 * d.election['collection_cast'] / d.election['cast']:.1f} % of votes have no place.",
         "Left out of the maps; included in municipal totals."],
        ["Area-level comparisons", "Strong area correlations don't describe individuals.", "Phrase findings about areas, not people."],
        ["No addresses", "Neither buildings nor maps carry street addresses.", "Order Lantmäteriet <i>Belägenhetsadress</i> if needed."],
    ]
    doc.table(["Issue", "What happens", "How it's handled"], rows, widths=[0.2, 0.45, 0.35],
              caption="Known limitations and how the prototype deals with them.")

    # ------------------------------------------------------------------ appendix
    doc.h1("Appendix A  SCB field dictionary (Swedish → English)")
    doc.p("Original SCB column names as delivered, the English alias used in the project, and where the meaning comes from: "
          "'pdf' = stated in the delivery's variable description (possibly under an older name), 'inferred' = read from the "
          "Swedish word, not confirmed by SCB.")
    for ds in (POPULATION, INCOME, EMPLOYMENT):
        rows = [[f"<i>{k}</i>", v.alias_en, v.description_en, v.role, v.source] for k, v in ds.fields.items()]
        doc.h2(f"{ds.metadata['name_sv'].split(' - ')[0]} ({ds.metadata['name_en'].split(' - ')[0]})")
        doc.table(["Column", "English alias", "Meaning", "Role", "Source"], rows, widths=[0.14, 0.2, 0.46, 0.09, 0.11],
                  font_size=6.8)

    doc.h1("Appendix B  How this report was made")
    doc.bullets([
        f"Command: <font face='DejaVu'>python Src/Scripts/build_data_report.py --config {d.cfg.config_path or 'configs/cities/&lt;city&gt;.json'}</font>",
        "Read-only. Building heights come from the processed buildings file; grid, DeSO and voting-district values "
        "from the city's Unity package (the same numbers the twin shows); employment, election totals and LiDAR "
        "facts from the raw deliveries.",
        f"Figures are saved next to the PDF in <i>figures/</i>. Run on {datetime.now():%Y-%m-%d %H:%M}.",
    ])

    pdf_path = out_dir / f"{d.cfg.city_id}_data_report.pdf"
    doc.build(pdf_path, title=f"{city} data report", footer=f"{city} data report · licensed data (FUK) — do not publish")
    return pdf_path


def _year(tables):
    return F._year(tables)
