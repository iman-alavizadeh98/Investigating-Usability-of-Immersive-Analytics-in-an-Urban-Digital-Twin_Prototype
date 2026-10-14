"""
Figures for the data report. Each function draws one figure, saves a PNG into
`out_dir` and returns (path, facts), where `facts` holds the numbers the text
quotes, so text and figure always agree.

Map conventions: EPSG:3006, north up, one shared frame for all statistics maps
(union of the delivery grids), recessive context (water, roads), classed
colours with a "no data" swatch, and a 2 km scale bar.
"""

from __future__ import annotations

from pathlib import Path
from typing import Dict, List, Optional, Tuple

import geopandas as gpd
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from matplotlib.patches import Patch, Rectangle
from shapely.geometry import box

from . import style as S
from .sources import ReportData

S.apply_matplotlib_style()

PAGE_W = 6.9   # inches of usable width on A4 with the report margins


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _save(fig, out_dir: Path, name: str) -> Path:
    path = out_dir / f"{name}.png"
    fig.savefig(path)
    plt.close(fig)
    return path


def stats_frame(d: ReportData) -> Tuple[float, float, float, float]:
    """One frame for every statistics map: the union of the grid deliveries + study box."""
    boxes = [v for k, v in d.extents.items() if "grid" in k or "Study" in k]
    a = np.array(boxes)
    return a[:, 0].min(), a[:, 1].min(), a[:, 2].max(), a[:, 3].max()


def _fmt(v: float) -> str:
    if abs(v) >= 100_000:
        return f"{v / 1000:,.0f}k".replace(",", " ")
    if abs(v) >= 1000:
        return f"{v:,.0f}".replace(",", " ")
    if abs(v) >= 10:
        return f"{v:.0f}"
    return f"{v:.1f}".rstrip("0").rstrip(".")


def class_bounds(values: pd.Series, k: int = 6, clip=(0.0, 1.0), fixed: Optional[List[float]] = None) -> List[float]:
    """Quantile class bounds (unique), or `fixed` ones; the ends cover min and max."""
    v = values.dropna().astype(float)
    if fixed is not None:
        b = list(fixed)
    else:
        lo, hi = v.quantile(clip[0]), v.quantile(clip[1])
        q = np.unique(np.round(v[(v >= lo) & (v <= hi)].quantile(np.linspace(0, 1, k + 1)).to_numpy(), 6))
        b = list(q)
    b[0] = min(b[0], float(v.min()))
    b[-1] = max(b[-1], float(v.max())) + 1e-9
    return b


def _legend_below(ax, handles, title: str = "", ncol: int = 2):
    """Legend under a map, so it never covers data."""
    leg = ax.legend(handles=handles, title=title or None, loc="upper center", bbox_to_anchor=(0.5, -0.01), ncol=ncol,
                    fontsize=6.3, title_fontsize=6.8, handlelength=1.1, handleheight=0.8, columnspacing=0.8,
                    borderaxespad=0.2, frameon=False)
    leg._legend_box.align = "left"


def _legend_classes(ax, bounds, unit: str = "", title: str = "", no_data=True, fmt=_fmt, labels=None, colors=None):
    cmap_colors = colors or S.class_colors(len(bounds) - 1)
    labels = labels or [f"{fmt(bounds[i])} – {fmt(bounds[i + 1])}{(' ' + unit) if unit else ''}" for i in range(len(cmap_colors))]
    handles = [Patch(facecolor=c, edgecolor="none", label=l) for c, l in zip(cmap_colors, labels)]
    if no_data:
        handles.append(Patch(facecolor=S.NO_DATA, edgecolor=S.INK_3, hatch="////", linewidth=0.3, label="no data"))
    _legend_below(ax, handles, title)


def _scale_bar(ax, frame, length=2000):
    minx, miny, maxx, maxy = frame
    x0 = minx + 0.05 * (maxx - minx)
    y0 = miny + 0.04 * (maxy - miny)
    ax.plot([x0, x0 + length], [y0, y0], color=S.INK, lw=1.4, solid_capstyle="butt", zorder=20)
    ax.text(x0 + length / 2, y0 + 0.012 * (maxy - miny), f"{length // 1000} km", ha="center", va="bottom",
            fontsize=6.5, color=S.INK, zorder=20)
    ax.annotate("N", xy=(x0, y0 + 0.11 * (maxy - miny)), xytext=(x0, y0 + 0.06 * (maxy - miny)),
                ha="center", fontsize=7, color=S.INK, arrowprops=dict(arrowstyle="-|>", color=S.INK, lw=0.8), zorder=20)


def _map_axes(ax, frame, title: str = ""):
    minx, miny, maxx, maxy = frame
    ax.set_xlim(minx, maxx)
    ax.set_ylim(miny, maxy)
    ax.set_aspect("equal")
    ax.set_xticks([])
    ax.set_yticks([])
    for s in ax.spines.values():
        s.set_visible(True)
        s.set_color(S.GRID)
    ax.set_facecolor("white")
    if title:
        ax.set_title(title, fontsize=8.5, pad=4)


def _context(ax, d: ReportData, water=True, roads=True, box_outline=True):
    rm = d.roadmap
    if "my_riks" in rm:
        rm["my_riks"].plot(ax=ax, color=S.LAND, linewidth=0, zorder=0)
    if water and "mv_riks" in rm:
        rm["mv_riks"].plot(ax=ax, color=S.WATER, linewidth=0, zorder=1)
    if roads and "vl_riks" in rm:
        rm["vl_riks"].plot(ax=ax, color=S.ROAD, linewidth=0.35, zorder=8)
    if box_outline:
        minx, miny, maxx, maxy = d.extents["Study area (LiDAR tiles)"]
        ax.add_patch(Rectangle((minx, miny), maxx - minx, maxy - miny, fill=False, ec=S.BOX, lw=0.6, ls=(0, (3, 2)), zorder=9))


def choropleth(ax, gdf: gpd.GeoDataFrame, column: str, bounds, d: ReportData, frame, title="", unit="",
               legend_title="", edge=None, cmap=None, norm=None, legend=True, labels=None):
    _context(ax, d, roads=False)
    has = gdf[column].notna()
    if (~has).any():
        gdf[~has].plot(ax=ax, facecolor=S.NO_DATA, edgecolor=S.INK_3, hatch="////", linewidth=0.15, zorder=3)
    if cmap is None:
        cmap, norm = S.stepped(bounds)
    gdf[has].plot(ax=ax, column=column, cmap=cmap, norm=norm, edgecolor=edge or "white", linewidth=0.15, zorder=4)
    if "vl_riks" in d.roadmap:
        d.roadmap["vl_riks"].plot(ax=ax, color="white", linewidth=0.25, alpha=0.6, zorder=8)
    _map_axes(ax, frame, title)
    _scale_bar(ax, frame)
    if legend:
        _legend_classes(ax, bounds, unit, legend_title, no_data=bool((~has).any()), labels=labels)


def join_values(units: gpd.GeoDataFrame, values: pd.DataFrame) -> gpd.GeoDataFrame:
    return units.join(values, how="left")


def _hbar(ax, labels, vals, color=S.CAT[0], fmt=lambda v: f"{v:,.0f}".replace(",", " "), xlabel=""):
    y = np.arange(len(labels))[::-1]
    ax.barh(y, vals, color=color, height=0.62, edgecolor="none")
    ax.set_yticks(y)
    ax.set_yticklabels(labels)
    ax.tick_params(axis="y", length=0)
    vmax = max(vals) if len(vals) else 1
    for yi, v in zip(y, vals):
        ax.text(v + vmax * 0.01, yi, fmt(v), va="center", fontsize=7, color=S.INK_2)
    ax.set_xlim(0, vmax * 1.18)
    ax.spines["left"].set_visible(False)
    ax.xaxis.grid(True, color=S.GRID, lw=0.5)
    ax.set_axisbelow(True)
    if xlabel:
        ax.set_xlabel(xlabel)


# ---------------------------------------------------------------------------
# 1. Overview
# ---------------------------------------------------------------------------

EXTENT_STYLE = {
    "Study area (LiDAR tiles)": (S.INK, "-", 1.4),
    "Buildings": (S.CAT[0], (0, (4, 2)), 1.1),
    "Population / income grid": (S.CAT[1], (0, (1, 1.5)), 1.3),
    "Employment grid": (S.CAT[2], (0, (6, 2, 1, 2)), 1.1),
    "Voting districts (municipality)": (S.CAT[6], "-", 0.8),
}


def fig_extents(d: ReportData, out_dir: Path):
    fig, axes = plt.subplots(1, 2, figsize=(PAGE_W, 4.6), gridspec_kw={"width_ratios": [1.0, 1.0]})
    dist = d.election["districts"]
    # Left: municipality
    ax = axes[0]
    dist.plot(ax=ax, facecolor=S.LAND, edgecolor="white", linewidth=0.4)
    for k, (minx, miny, maxx, maxy) in d.extents.items():
        c, ls, lw = EXTENT_STYLE.get(k, (S.INK_2, "-", 1))
        ax.add_patch(Rectangle((minx, miny), maxx - minx, maxy - miny, fill=False, ec=c, ls=ls, lw=lw))
    b = dist.total_bounds
    pad = 1500
    _map_axes(ax, (b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad), "Whole municipality")
    _scale_bar(ax, (b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad), 5000)
    # Right: zoom
    ax = axes[1]
    frame = stats_frame(d)
    _context(ax, d, box_outline=False)
    d.buildings.plot(ax=ax, color="#b9b7b0", linewidth=0, zorder=5)
    for k, (minx, miny, maxx, maxy) in d.extents.items():
        if k.startswith("Voting"):
            continue
        c, ls, lw = EXTENT_STYLE.get(k, (S.INK_2, "-", 1))
        ax.add_patch(Rectangle((minx, miny), maxx - minx, maxy - miny, fill=False, ec=c, ls=ls, lw=lw, zorder=10))
    pad = 400
    f2 = (frame[0] - pad, frame[1] - pad, frame[2] + pad, frame[3] + pad)
    _map_axes(ax, f2, "Study area (zoom)")
    _scale_bar(ax, f2)
    handles = [plt.Line2D([], [], color=c, ls=ls, lw=lw, label=k) for k, (c, ls, lw) in EXTENT_STYLE.items() if k in d.extents]
    fig.legend(handles=handles, loc="lower center", ncol=3, fontsize=6.8, bbox_to_anchor=(0.5, -0.06))
    return _save(fig, out_dir, "01_extents"), {}


def fig_landcover(d: ReportData, out_dir: Path):
    my = d.roadmap.get("my_riks")
    classes = [("Vattenyta", "Water", S.CAT[0]), ("Bebyggelseområde", "Built-up area", S.CAT[1]),
               ("Skog", "Forest", S.CAT[2]), ("Öppen mark", "Open land", S.CAT[3])]
    fig = plt.figure(figsize=(PAGE_W, 4.5))
    ax = fig.add_axes([0.0, 0.0, 0.58, 1.0])
    ax2 = fig.add_axes([0.68, 0.55, 0.30, 0.33])
    areas = {}
    for sv, en, c in classes:
        part = my[my["KATEGORI"] == sv]
        part.plot(ax=ax, color=c, linewidth=0, alpha=0.75)
        areas[en] = part.area.sum() / 1e6
    if "jl_riks" in d.roadmap:
        d.roadmap["jl_riks"].plot(ax=ax, color=S.INK, linewidth=0.8, linestyle=(0, (3, 1.5)))
    d.roadmap["vl_riks"].plot(ax=ax, color="white", linewidth=0.7)
    b = my.total_bounds
    _map_axes(ax, (b[0], b[1], b[2], b[3]))
    _scale_bar(ax, tuple(b))
    from matplotlib import patheffects
    handles = [Patch(color=c, alpha=0.75, label=f"{en} ({sv})") for sv, en, c in classes]
    handles += [plt.Line2D([], [], color="white", lw=1.2, label="Roads (Vägkartan)",
                           path_effects=[patheffects.withStroke(linewidth=2.6, foreground=S.INK_3)]),
                plt.Line2D([], [], color=S.INK, lw=0.8, ls=(0, (3, 1.5)), label="Railway")]
    fig.legend(handles=handles, loc="lower left", bbox_to_anchor=(0.62, 0.08), fontsize=7)
    labels = list(areas)
    _hbar(ax2, labels, [areas[k] for k in labels], color=S.INK_3, fmt=lambda v: f"{v:.1f} km²")
    ax2.set_title("Area by land cover", fontsize=8)
    for i, (sv, en, c) in enumerate(classes):
        ax2.patches[i].set_facecolor(c)
    return _save(fig, out_dir, "02_landcover"), {"areas_km2": areas, "total_km2": sum(areas.values())}


def fig_data_years(d: ReportData, out_dir: Path):
    b = d.buildings
    v = pd.to_datetime(b["version_valid_from"], errors="coerce")
    rows = [
        ("LiDAR scan (Laserdata NH)", pd.Timestamp(d.lidar_strips["date"].min()), None),
        ("Surface model (Ytmodell, photos)", pd.Timestamp(d.dsm_tiles["date_from"].min()), pd.Timestamp(d.dsm_tiles["date_to"].max())),
        ("Building footprints (versions)", v.min(), v.max()),
        ("Population (SCB)", pd.Timestamp(f"{_year(d.population_tables)}-12-31"), None),
        ("Income (SCB)", pd.Timestamp(f"{_year(d.income_tables)}-12-31"), None),
        ("Employment (SCB)", pd.Timestamp(f"{_year(d.employment_tables)}-12-31"), None),
        (f"Election ({d.election['type'] or 'election'})", pd.Timestamp(f"{d.election['year']}-09-13"), None),
    ]
    adat = pd.to_datetime(d.propertymap["vl_riks"]["ADAT"], errors="coerce") if "vl_riks" in d.propertymap else None
    if adat is not None:
        rows.append(("Property map roads (last change)", adat.min(), adat.max()))
    fig, ax = plt.subplots(figsize=(PAGE_W, 2.6))
    for i, (label, a, z) in enumerate(rows[::-1]):
        if z is not None and z > a:
            ax.plot([a, z], [i, i], color=S.CAT[0], lw=3, solid_capstyle="round", alpha=0.5)
            ax.plot([z], [i], "o", color=S.CAT[0], ms=4)
        ax.plot([a], [i], "o", color=S.CAT[0], ms=5)
        if z is None or z.year == a.year:
            text = f"{a:%Y-%m}" if label.startswith(("LiDAR", "Surface")) else f"{a:%Y}"
        else:
            text = f"{a:%Y} – {z:%Y}"
        ax.text(a, i + 0.32, text, fontsize=6.5, color=S.INK_2, ha="left")
    ax.set_yticks(range(len(rows)))
    ax.set_yticklabels([r[0] for r in rows[::-1]])
    ax.tick_params(axis="y", length=0)
    ax.spines["left"].set_visible(False)
    ax.xaxis.grid(True, color=S.GRID, lw=0.5)
    ax.set_axisbelow(True)
    ax.set_ylim(-0.6, len(rows) - 0.2)
    return _save(fig, out_dir, "03_data_years"), {}


def _year(tables) -> Optional[int]:
    ys = [t.year for t in tables.values() if t.year]
    return max(set(ys), key=ys.count) if ys else None


# ---------------------------------------------------------------------------
# 2. Buildings
# ---------------------------------------------------------------------------

def fig_building_types(d: ReportData, out_dir: Path):
    b = d.buildings
    g = b.groupby("object_type_en").agg(n=("object_id", "size"), area=("footprint_area_m2", "sum")).sort_values("n", ascending=False)
    fig, axes = plt.subplots(1, 2, figsize=(PAGE_W, 2.5), sharey=True)
    _hbar(axes[0], list(g.index), list(g["n"]), color=S.CAT[0])
    axes[0].set_title("Number of buildings")
    _hbar(axes[1], list(g.index), list(g["area"] / 1e4), color=S.CAT[0], fmt=lambda v: f"{v:.0f} ha")
    axes[1].set_title("Total footprint area")
    axes[1].tick_params(axis="y", labelleft=False)
    fig.tight_layout()
    return _save(fig, out_dir, "10_building_types"), {"by_type": g}


def fig_building_heights(d: ReportData, out_dir: Path):
    b = d.buildings[d.buildings["has_height"].astype(bool)]
    fig, axes = plt.subplots(1, 2, figsize=(PAGE_W, 2.7), gridspec_kw={"width_ratios": [1.2, 1]})
    ax = axes[0]
    ax.hist(b["height_m"].clip(upper=40), bins=np.arange(0, 41, 1), color=S.CAT[0], edgecolor="white", linewidth=0.5)
    ax.set_xlabel("Height above ground (m); last bar = 40 m or more")
    ax.set_ylabel("Buildings")
    ax.set_title("Height distribution")
    ax.yaxis.grid(True, color=S.GRID, lw=0.5)
    ax.set_axisbelow(True)
    ax = axes[1]
    q = b.groupby("object_type_en")["height_m"].quantile([0.25, 0.5, 0.75]).unstack().sort_values(0.5)
    y = np.arange(len(q))
    ax.hlines(y, q[0.25], q[0.75], color=S.CAT[0], lw=3, alpha=0.45)
    ax.plot(q[0.5], y, "o", color=S.CAT[0], ms=5)
    for yi, m in zip(y, q[0.5]):
        ax.text(m, yi + 0.28, f"{m:.1f}", ha="center", fontsize=6.5, color=S.INK_2)
    ax.set_yticks(y)
    ax.set_yticklabels(q.index)
    ax.tick_params(axis="y", length=0)
    ax.spines["left"].set_visible(False)
    ax.set_xlabel("Height (m): dot = median, bar = middle 50 %")
    ax.set_title("Height by building type")
    ax.xaxis.grid(True, color=S.GRID, lw=0.5)
    ax.set_axisbelow(True)
    fig.tight_layout()
    hs = b["height_m"]
    return _save(fig, out_dir, "11_building_heights"), {
        "median": hs.median(), "p90": hs.quantile(0.9), "p99": hs.quantile(0.99), "max": hs.max(), "n": len(hs),
        "by_type_median": q[0.5]}


def fig_building_height_map(d: ReportData, out_dir: Path):
    b = d.buildings.copy()
    bounds = [0, 3, 6, 9, 12, 18, 30, 80]
    fig, ax = plt.subplots(figsize=(PAGE_W, 8.6))
    _context(ax, d, roads=True, box_outline=False)
    no_h = ~b["has_height"].astype(bool)
    b[no_h].plot(ax=ax, color="#d33a8a", linewidth=0, zorder=6)
    cmap, norm = S.stepped(bounds)
    b[~no_h].plot(ax=ax, column="height_m", cmap=cmap, norm=norm, linewidth=0, zorder=5)
    fr = tuple(b.total_bounds)
    _map_axes(ax, fr)
    _scale_bar(ax, fr)
    handles = [Patch(color=c, label=f"{bounds[i]}–{bounds[i + 1]} m" if i < len(bounds) - 2 else f"≥ {bounds[i]} m")
               for i, c in enumerate(S.class_colors(len(bounds) - 1))][::-1]
    handles.append(Patch(color="#d33a8a", label=f"no height ({int(no_h.sum()):,})".replace(",", " ")))
    ax.legend(handles=handles, title="Building height", loc="lower left", bbox_to_anchor=(0.01, 0.1), fontsize=7,
              title_fontsize=7.5, frameon=True, facecolor="white", edgecolor="none", framealpha=0.92)
    return _save(fig, out_dir, "12_building_height_map"), {"no_height": int(no_h.sum())}


def fig_height_quality(d: ReportData, out_dir: Path):
    b = d.buildings
    fig, axes = plt.subplots(1, 2, figsize=(PAGE_W, 2.3), gridspec_kw={"width_ratios": [1, 1]})
    src = b["height_source"].fillna("none").replace({"surface": "2018 surface model", "lidar": "2010 LiDAR", "none": "none"})
    qual = b["height_quality"].fillna("none")
    order_q = ["high", "medium", "low", "none"]
    tab = pd.crosstab(src, qual).reindex(columns=order_q, fill_value=0)
    tab = tab.loc[tab.sum(axis=1).sort_values().index]
    left = np.zeros(len(tab))
    colors = {"high": S.BLUE[10], "medium": S.BLUE[6], "low": S.BLUE[2], "none": S.NO_DATA}
    for qn in order_q:
        axes[0].barh(tab.index, tab[qn], left=left, color=colors[qn], label=qn, edgecolor="white", linewidth=0.8, height=0.6)
        left += tab[qn].to_numpy()
    for yi, n in enumerate(tab.sum(axis=1)):
        axes[0].text(n + tab.values.sum() * 0.01, yi, f"{n:,}".replace(",", " "), va="center", fontsize=7, color=S.INK_2)
    axes[0].set_xlim(0, tab.sum(axis=1).max() * 1.2)
    axes[0].legend(title="height quality", ncol=4, loc="upper center", bbox_to_anchor=(0.5, -0.25), fontsize=6.5, title_fontsize=7)
    axes[0].set_title("Where heights come from")
    axes[0].tick_params(axis="y", length=0)
    axes[0].spines["left"].set_visible(False)
    reasons = b["no_height_reason"].dropna().value_counts()
    nice = {"surface_shows_ground": "surface model shows ground", "no_points": "no points", "below_min_height": "below minimum height",
            "no_ground": "no ground found"}
    _hbar(axes[1], [nice.get(k, k) for k in reasons.index], list(reasons.values), color=S.INK_3)
    axes[1].set_title("Why buildings have no height")
    fig.tight_layout()
    return _save(fig, out_dir, "13_height_quality"), {"sources": src.value_counts(), "quality": qual.value_counts(), "reasons": reasons}


def fig_ground(d: ReportData, out_dir: Path):
    b = d.buildings.dropna(subset=["ground_z"])
    bounds = [-1, 5, 15, 25, 35, 45, 55, 80]
    fig, ax = plt.subplots(figsize=(PAGE_W * 0.62, 5.6))
    _context(ax, d, roads=False, box_outline=False)
    cmap, norm = S.stepped(bounds)
    b.plot(ax=ax, column="ground_z", cmap=cmap, norm=norm, linewidth=0, zorder=5)
    fr = tuple(d.buildings.total_bounds)
    _map_axes(ax, fr)
    _scale_bar(ax, fr)
    _legend_classes(ax, bounds, "m", "Ground level", no_data=False)
    return _save(fig, out_dir, "14_ground"), {"min": b["ground_z"].min(), "median": b["ground_z"].median(), "max": b["ground_z"].max()}


# ---------------------------------------------------------------------------
# 3. LiDAR
# ---------------------------------------------------------------------------

CLASS_NAMES = {"class_1": "1 Unclassified", "class_2": "2 Ground", "class_9": "9 Water", "class_11": "11 Bridge"}


def fig_lidar(d: ReportData, out_dir: Path):
    t = d.lidar_tiles.copy()
    cls = [c for c in t.columns if c.startswith("class_")]
    t["points"] = t[cls].sum(axis=1)
    t["density"] = t["points"] / t.geometry.area
    fig = plt.figure(figsize=(PAGE_W, 4.4))
    ax = fig.add_axes([0.0, 0.0, 0.45, 1.0])
    _context(ax, d, roads=False, box_outline=False)
    bounds = class_bounds(t["density"], k=4)
    cmap, norm = S.stepped(bounds)
    t.plot(ax=ax, column="density", cmap=cmap, norm=norm, edgecolor="white", linewidth=1.0, alpha=0.85, zorder=3)
    d.buildings.plot(ax=ax, color="white", linewidth=0, alpha=0.35, zorder=4)
    for _, r in t.iterrows():
        c = r.geometry.centroid
        ax.text(c.x, c.y, f"{r['points'] / 1e6:.1f} M\n{r['density']:.2f}/m²", ha="center", va="center", fontsize=5.8,
                color=S.INK, zorder=6, bbox=dict(boxstyle="round,pad=0.25", fc="white", ec="none", alpha=0.85))
    fr = tuple(t.total_bounds)
    _map_axes(ax, fr, "Points per 2.5 km tile (millions, per m²)")
    ax2 = fig.add_axes([0.62, 0.55, 0.36, 0.35])
    tot = t[cls].sum().rename(CLASS_NAMES)
    _hbar(ax2, list(tot.index), list(tot.values / 1e6), color=S.CAT[0], fmt=lambda v: f"{v:.1f} M")
    ax2.set_title("Points by class (all tiles)", fontsize=8)
    return _save(fig, out_dir, "20_lidar"), {"points": float(t["points"].sum()), "by_class": tot,
                                            "density_min": t["density"].min(), "density_max": t["density"].max(), "tiles": len(t)}


# ---------------------------------------------------------------------------
# 4. Population
# ---------------------------------------------------------------------------

def fig_population_maps(d: ReportData, out_dir: Path):
    g = join_values(d.package["ruta"], d.values["ruta_population"])
    frame = stats_frame(d)
    fig, axes = plt.subplots(1, 3, figsize=(PAGE_W, 4.1))
    choropleth(axes[0], g, "population_density", class_bounds(g["population_density"]), d, frame,
               "Population density", "/km²")
    choropleth(axes[1], g, "share_65_plus", class_bounds(g["share_65_plus"], fixed=[0, 10, 15, 20, 25, 35, 100]), d, frame,
               "Share aged 65+", "%")
    choropleth(axes[2], g, "share_born_abroad", class_bounds(g["share_born_abroad"], fixed=[0, 10, 20, 30, 45, 60, 100]),
               d, frame, "Share born abroad", "%")
    fig.tight_layout(w_pad=0.4)
    return _save(fig, out_dir, "30_population_maps"), {"density_max": g["population_density"].max()}


def fig_population_structure(d: ReportData, out_dir: Path):
    v = d.values["ruta_population"]
    ages = ["age_0_6", "age_7_15", "age_16_19", "age_20_24", "age_25_44", "age_45_64", "age_65_plus"]
    age_lbl = ["0–6", "7–15", "16–19", "20–24", "25–44", "45–64", "65+"]
    birth = ["born_sweden", "born_nordic", "born_eu", "born_rest_of_world"]
    birth_lbl = ["Sweden", "Nordic (excl. SE)", "EU (excl. Nordic)", "Rest of world"]
    a = v[ages].sum()
    bb = v[birth].sum()
    fig, axes = plt.subplots(1, 2, figsize=(PAGE_W, 2.4))
    _hbar(axes[0], age_lbl, list(a.values), color=S.CAT[0])
    axes[0].set_title("Residents by age group")
    _hbar(axes[1], birth_lbl, list(bb.values), color=S.CAT[0])
    axes[1].set_title("Residents by country of birth")
    fig.tight_layout()
    return _save(fig, out_dir, "31_population_structure"), {"ages": dict(zip(age_lbl, a.values)), "birth": dict(zip(birth_lbl, bb.values)),
                                                           "total": v["population"].sum(), "men": v["men"].sum(), "women": v["women"].sum()}


def fig_deso_population(d: ReportData, out_dir: Path):
    g = join_values(d.package["deso"], d.values["deso_population"])
    frame = stats_frame(d)
    fig, axes = plt.subplots(1, 3, figsize=(PAGE_W, 4.1))
    choropleth(axes[0], g, "population", class_bounds(g["population"], k=5), d, frame, "Residents per DeSO", "")
    choropleth(axes[1], g, "share_children_0_15", class_bounds(g["share_children_0_15"], k=5), d, frame, "Share aged 0–15", "%")
    nm = g["net_migration_per_1000"]
    bounds = [float(nm.min()), -20, -5, 5, 20, float(nm.max()) + 1e-9]
    from matplotlib.colors import BoundaryNorm, ListedColormap
    div_colors = [S.BLUE[10], S.BLUE[4], S.MID, S.RED[2], S.RED[5]]
    choropleth(axes[2], g, "net_migration_per_1000", bounds, d, frame, "Net migration per 1,000",
               cmap=ListedColormap(div_colors), norm=BoundaryNorm(bounds, len(div_colors)), legend=False)
    _legend_classes(axes[2], bounds, colors=div_colors, no_data=False,
                    labels=[f"{_fmt(bounds[i])} – {_fmt(bounds[i + 1])}" for i in range(len(div_colors))])
    fig.tight_layout(w_pad=0.4)
    return _save(fig, out_dir, "32_deso_population"), {}


# ---------------------------------------------------------------------------
# 5. Income
# ---------------------------------------------------------------------------

def fig_income(d: ReportData, out_dir: Path):
    gr = join_values(d.package["ruta"], d.values["ruta_income"])
    gd = join_values(d.package["deso"], d.values["deso_income"])
    frame = stats_frame(d)
    fig = plt.figure(figsize=(PAGE_W, 4.4))
    ax1 = fig.add_axes([0.0, 0.0, 0.34, 1.0])
    ax2 = fig.add_axes([0.345, 0.0, 0.34, 1.0])
    ax3 = fig.add_axes([0.76, 0.55, 0.23, 0.35])
    k = lambda v: f"{v / 1000:.0f}k"
    ms = gr["mean_economic_standard"].where(gr["households"] >= 5)
    gr["ms5"] = ms
    b1 = class_bounds(gr["ms5"], clip=(0.02, 0.98))
    choropleth(ax1, gr, "ms5", b1, d, frame, "Mean economic standard (grid)", legend=False)
    _legend_classes(ax1, b1, "SEK", "", fmt=k, no_data=bool(gr["ms5"].isna().any()))
    b2 = class_bounds(gd["median_income"], k=5)
    choropleth(ax2, gd, "median_income", b2, d, frame, "Median economic standard (DeSO)", legend=False)
    _legend_classes(ax2, b2, "SEK", "", fmt=k, no_data=bool(gd["median_income"].isna().any()))
    ax3.hist(gd["median_income"].dropna() / 1000, bins=12, color=S.CAT[0], edgecolor="white")
    ax3.set_xlabel("DeSO median (thousand SEK)")
    ax3.set_ylabel("DeSO areas")
    ax3.set_title("Spread between areas", fontsize=8)
    return _save(fig, out_dir, "40_income"), {
        "deso_min": gd["median_income"].min(), "deso_max": gd["median_income"].max(), "deso_median": gd["median_income"].median(),
        "cells_masked": int((gr["households"] < 5).sum()), "households": d.values["ruta_income"]["households"].sum()}


# ---------------------------------------------------------------------------
# 6. Employment
# ---------------------------------------------------------------------------

def fig_employment_maps(d: ReportData, out_dir: Path):
    c = d.employment_cells.copy()
    frame = stats_frame(d)
    # A cell missing from a table stays NaN (hatched, "no data"); a reported 0 gets its own class.
    top = max(4000.0, float(np.nanmax(c[["res_Totalt", "work_Totalt"]].to_numpy())) + 1)
    bounds = [0, 0.5, 10, 50, 150, 300, 600, 1000, top]
    labels = ["0", "1–10", "10–50", "50–150", "150–300", "300–600", "600–1 000", f"1 000–{_fmt(top - 1)}"]
    fig, axes = plt.subplots(1, 3, figsize=(PAGE_W, 4.6))
    choropleth(axes[0], c, "res_Totalt", bounds, d, frame, "Employed residents", legend=True, labels=labels)
    choropleth(axes[1], c, "work_Totalt", bounds, d, frame, "Jobs (workplaces)", legend=True, labels=labels)
    ratio = (c["work_Totalt"] / c["res_Totalt"]).where((c["res_Totalt"] >= 20) | (c["work_Totalt"] >= 20))
    c["ratio"] = ratio.replace([np.inf], 50)
    rb = [0, 0.25, 0.5, 0.8, 1.25, 2, 4, 1e6]
    from matplotlib.colors import BoundaryNorm, ListedColormap
    cols = [S.BLUE[11], S.BLUE[7], S.BLUE[3], S.MID, S.RED[1], S.RED[3], S.RED[6]]
    choropleth(axes[2], c, "ratio", rb, d, frame, "Jobs per employed resident", cmap=ListedColormap(cols),
               norm=BoundaryNorm(rb, len(cols)), legend=False)
    _legend_classes(axes[2], rb, colors=cols, labels=["< 0.25", "0.25–0.5", "0.5–0.8", "0.8–1.25 balanced", "1.25–2", "2–4", "> 4"],
                    no_data=True)
    fig.tight_layout(w_pad=0.4)
    return _save(fig, out_dir, "50_employment_maps"), {
        "employed_residents": c["res_Totalt"].sum(), "jobs": c["work_Totalt"].sum(),
        "job_cells_no_residents": int(((c["work_Totalt"] > 0) & (c["res_Totalt"].fillna(0) == 0)).sum())}


def fig_employment_sectors(d: ReportData, out_dir: Path):
    c = d.employment_cells
    sectors = ["private", "municipal", "region", "state", "other"]
    lbl = {"private": "Business (Näringsliv)", "municipal": "Municipality", "region": "Region", "state": "State", "other": "Other"}
    res = c[[f"res_{s}" for s in sectors]].sum().to_numpy()
    work = c[[f"work_{s}" for s in sectors]].sum().to_numpy()
    fig = plt.figure(figsize=(PAGE_W, 6.2))
    gs = fig.add_gridspec(2, 2, height_ratios=[1, 3.2], width_ratios=[1, 1.05], hspace=0.45, wspace=0.25)
    ax = fig.add_subplot(gs[0, :])
    for row, (name, v) in enumerate((("Where they work", work), ("Where they live", res))):
        left = 0
        share = v / v.sum() * 100
        for i, s in enumerate(sectors):
            ax.barh(row, share[i], left=left, color=S.CAT[i], edgecolor="white", linewidth=1, height=0.6)
            if share[i] >= 6:
                ax.text(left + share[i] / 2, row, f"{share[i]:.0f}%", ha="center", va="center", fontsize=6.8, color="white")
            left += share[i]
    ax.set_yticks([0, 1])
    ax.set_yticklabels(["Where they work", "Where they live"])
    ax.set_xlim(0, 100)
    ax.set_xlabel("Share of employed persons (%)")
    ax.tick_params(axis="y", length=0)
    ax.spines["left"].set_visible(False)
    ax.legend(handles=[Patch(color=S.CAT[i], label=lbl[s]) for i, s in enumerate(sectors)], ncol=5, loc="lower center",
              bbox_to_anchor=(0.5, 1.02), fontsize=6.6)
    ax.set_title("Employment by sector", pad=22)
    ax2 = fig.add_subplot(gs[1, 0])
    g = d.employment_deso.copy()
    g["rate"] = 100 * g["status_Sysselsatt"] / g["status_Totalt"]
    frame = stats_frame(d)
    choropleth(ax2, g, "rate", class_bounds(g["rate"], fixed=[50, 65, 72, 78, 84, 88, 95]), d, frame, "Employment rate, 20–64 (DeSO)", "%")
    inner = gs[1, 1].subgridspec(3, 1, height_ratios=[0.25, 1, 0.6])
    ax3 = fig.add_subplot(inner[1])
    ax3.hist(g["rate"].dropna(), bins=np.arange(48, 96, 3), color=S.CAT[0], edgecolor="white")
    ax3.set_xlabel("Employment rate (%)")
    ax3.set_ylabel("DeSO areas")
    ax3.set_title("Employment rate across DeSO", fontsize=8)
    return _save(fig, out_dir, "51_employment_sectors"), {
        "res_share": dict(zip(sectors, res / res.sum() * 100)), "work_share": dict(zip(sectors, work / work.sum() * 100)),
        "rate_min": g["rate"].min(), "rate_median": g["rate"].median(), "rate_max": g["rate"].max()}


def fig_education(d: ReportData, out_dir: Path):
    g = join_values(d.package["ruta"], d.values["ruta_population"])
    frame = stats_frame(d)
    v = d.values["ruta_population"]
    fig = plt.figure(figsize=(PAGE_W, 4.1))
    ax = fig.add_axes([0.0, 0.0, 0.42, 1.0])
    choropleth(ax, g, "share_post_secondary_3y", class_bounds(g["share_post_secondary_3y"], fixed=[0, 10, 20, 30, 40, 55, 100]), d, frame,
               "Share with ≥ 3 years post-secondary education", "%")
    ax2 = fig.add_axes([0.55, 0.45, 0.43, 0.38])
    e = d.employment_cells
    cols = ["edu_Forgymn", "edu_Gymnasial", "edu_Eftergymn2", "edu_Eftergymn3", "edu_UppgSakn"]
    lbl = ["Pre-upper-secondary", "Upper-secondary", "Post-sec. < 3 yrs", "Post-sec. ≥ 3 yrs", "Unknown"]
    tot = e[cols].sum()
    _hbar(ax2, lbl, list(tot.values), color=S.CAT[0])
    ax2.set_title("Residents 25–64 by highest education", fontsize=8)
    return _save(fig, out_dir, "52_education"), {"edu": dict(zip(lbl, tot.values)), "edu_total": tot.sum()}


# ---------------------------------------------------------------------------
# 7. Election
# ---------------------------------------------------------------------------

def fig_election(d: ReportData, out_dir: Path):
    e = d.election
    pv = e["party_votes"]
    share = 100 * pv / e["valid"]
    top = share.head(9)
    rest = share.iloc[9:].sum()
    short = {"Arbetarepartiet-Socialdemokraterna": "S – Social Democrats", "Moderaterna": "M – Moderates",
             "Sverigedemokraterna": "SD – Sweden Democrats", "Vänsterpartiet": "V – Left", "Miljöpartiet de gröna": "MP – Greens",
             "Centerpartiet": "C – Centre", "Kristdemokraterna": "KD – Christian Democrats",
             "Liberalerna (tidigare Folkpartiet)": "L – Liberals", "Helsingborgspartiet": "Helsingborgspartiet (local)"}
    labels = [short.get(p, p) for p in top.index] + [f"{len(share) - 9} other parties"]
    vals = list(top.values) + [rest]
    g = join_values(d.package["valdistrikt"], d.values["valdistrikt_election"])
    frame = stats_frame(d)
    fig = plt.figure(figsize=(PAGE_W, 8.2))
    ax = fig.add_axes([0.25, 0.7, 0.72, 0.27])
    _hbar(ax, labels, vals, color=S.CAT[0], fmt=lambda v: f"{v:.1f} %")
    ax.set_title(f"Municipal result {e['year']}: share of valid votes")
    ax1 = fig.add_axes([0.0, 0.1, 0.49, 0.54])
    choropleth(ax1, g, "turnout", class_bounds(g["turnout"], fixed=[55, 65, 72, 78, 84, 88, 95]), d, frame, "Turnout by voting district", "%")
    ax2 = fig.add_axes([0.51, 0.1, 0.49, 0.54])
    _context(ax2, d, roads=False)
    for p, c in S.PARTY_WIN.items():
        g[g["winner"] == p].plot(ax=ax2, color=c, edgecolor="white", linewidth=0.4, zorder=4, alpha=0.85)
    other = g[~g["winner"].isin(list(S.PARTY_WIN))]
    if len(other):
        other.plot(ax=ax2, color=S.INK_3, edgecolor="white", linewidth=0.4, zorder=4)
    _map_axes(ax2, frame, "Largest party by voting district")
    _scale_bar(ax2, frame)
    counts = g["winner"].value_counts()
    _legend_below(ax2, [Patch(color=c, label=f"{p} ({counts.get(p, 0)} districts)") for p, c in S.PARTY_WIN.items()], ncol=3)
    inside = g.geometry.within(box(*frame)).sum()
    return _save(fig, out_dir, "60_election"), {
        "turnout_muni": 100 * e["cast"] / e["eligible"], "shares": dict(zip(labels, vals)), "winners": counts,
        "turnout_min": g["turnout"].min(), "turnout_max": g["turnout"].max(), "districts_in_frame": int(inside)}


# ---------------------------------------------------------------------------
# 8. Relationships
# ---------------------------------------------------------------------------

def _spearman(a: pd.Series, b: pd.Series) -> Tuple[float, int]:
    m = a.notna() & b.notna()
    return float(a[m].rank().corr(b[m].rank())), int(m.sum())


def _scatter(ax, x, y, xl, yl, title, color=S.CAT[0]):
    m = x.notna() & y.notna()
    ax.scatter(x[m], y[m], s=14, color=color, alpha=0.75, edgecolor="white", linewidth=0.4)
    rho, n = _spearman(x, y)
    ax.set_xlabel(xl)
    ax.set_ylabel(yl)
    ax.set_title(title, fontsize=8.2)
    ax.text(0.03, 0.96, f"Spearman ρ = {rho:.2f}\nn = {n}", transform=ax.transAxes, va="top", fontsize=7, color=S.INK_2)
    ax.grid(True, color=S.GRID, lw=0.5)
    ax.set_axisbelow(True)
    return rho, n


def deso_table(d: ReportData) -> pd.DataFrame:
    t = d.values["deso_population"].join(d.values["deso_income"], rsuffix="_inc")
    emp = d.employment_deso.drop(columns="geometry").copy()
    emp.index = "deso:" + emp.index.astype(str)
    emp["employment_rate"] = 100 * emp["status_Sysselsatt"] / emp["status_Totalt"]
    t = t.join(emp[["employment_rate", "work_Totalt"]])
    t["jobs_per_resident"] = t["work_Totalt"] / t["population"]
    return t


def fig_relationships(d: ReportData, out_dir: Path):
    t = deso_table(d)
    v = d.values["valdistrikt_election"].join(d.values["valdistrikt_estimates"])
    v = v[v["mean_economic_standard_est"].notna()]
    fig, axes = plt.subplots(1, 3, figsize=(PAGE_W, 2.6))
    r1 = _scatter(axes[0], t["median_income"] / 1000, t["employment_rate"], "Median economic standard (kSEK)", "Employment rate (%)",
                  "DeSO: income vs employment")
    r2 = _scatter(axes[1], t["median_income"] / 1000, t["share_born_abroad"], "Median economic standard (kSEK)", "Born abroad (%)",
                  "DeSO: income vs born abroad")
    r3 = _scatter(axes[2], v["mean_economic_standard_est"] / 1000, v["turnout"], "Estimated mean econ. standard (kSEK)",
                  "Turnout (%)", "Voting district: income vs turnout")
    fig.tight_layout()
    return _save(fig, out_dir, "70_relationships"), {"income_employment": r1, "income_abroad": r2, "income_turnout": r3}


def fig_correlation(d: ReportData, out_dir: Path):
    t = deso_table(d)
    cols = {"median_income": "Median income", "share_low_income_q1": "Low-income share", "employment_rate": "Employment rate",
            "share_born_abroad": "Born abroad", "share_children_0_15": "Aged 0–15", "share_65_plus": "Aged 65+",
            "net_migration_per_1000": "Net migration", "jobs_per_resident": "Jobs per resident", "population": "Residents"}
    x = t[list(cols)].rename(columns=cols)
    r = x.rank().corr()
    fig, ax = plt.subplots(figsize=(PAGE_W * 0.8, 4.6))
    im = ax.imshow(r.values, cmap=S.DIV_CMAP, vmin=-1, vmax=1)
    ax.set_xticks(range(len(r)))
    ax.set_xticklabels(r.columns, rotation=40, ha="right")
    ax.set_yticks(range(len(r)))
    ax.set_yticklabels(r.index)
    for i in range(len(r)):
        for j in range(len(r)):
            val = r.values[i, j]
            ax.text(j, i, f"{val:.2f}", ha="center", va="center", fontsize=6.3, color="white" if abs(val) > 0.6 else S.INK)
    for s in ax.spines.values():
        s.set_visible(False)
    ax.tick_params(length=0)
    cb = fig.colorbar(im, ax=ax, fraction=0.035, pad=0.02)
    cb.outline.set_visible(False)
    cb.set_label("Spearman ρ (red = rise together, blue = opposite)", fontsize=7)
    fig.tight_layout()
    return _save(fig, out_dir, "71_correlation"), {"matrix": r, "n": int(x.dropna().shape[0])}
