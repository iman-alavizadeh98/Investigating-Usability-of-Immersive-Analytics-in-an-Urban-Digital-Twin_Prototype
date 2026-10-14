"""
Study materials for the 2D vs 3D pattern-finding test.

Two PDFs from one scenario config (configs/study/pattern_scenarios.json):

    <out>/study_2d_maps.pdf             participant material: one landscape page per scenario,
                                        the question and two maps (A and B). No answers.
    <out>/study_facilitator_handbook.pdf  facilitator only: session plan, questions, the
                                        answers with Spearman rho, n, named example areas,
                                        exceptions, scoring rubric, interview guide, survey,
                                        answer sheet.

Values come from the city's Unity package (the same numbers the 3D table shows;
since 2026-10-14 also the DeSO employment rate, data layer deso_employment). The
source "employment_rate_deso" (raw SCB Tab9_DeSO) is still supported. A scenario's
optional "unitFilter" (id prefix, minimum of a package variable) turns the other
units into no data, exactly as the 3D view's unitFilter does.
"""

from __future__ import annotations

import json
import logging
import textwrap
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List

import geopandas as gpd
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from matplotlib.backends.backend_pdf import PdfPages
from matplotlib.colors import BoundaryNorm, ListedColormap
from matplotlib.patches import Patch

from pipelines.scb.loader import load_scb_tables
from pipelines.unity_package.config import CityConfig, load_city_config

from . import style as S
from .figures import _legend_classes, _map_axes, _scale_bar, class_bounds
from .pdf import ReportDoc
from .sources import load_shapefiles, read_data_layer, read_spatial_layer

logger = logging.getLogger(__name__)

ORANGE = ["#fde6d6", "#fbcaa9", "#f6a77a", "#ef854f", "#eb6834", "#c4521f", "#9a3e15"]
CLASSES = 5


def _ramp(ramp: List[str], n: int) -> List[str]:
    idx = [round(1 + i * (len(ramp) - 2) / (n - 1)) for i in range(n)]
    return [ramp[i] for i in idx]


def _fmt(unit: str):
    if unit == "SEK":
        return lambda v: f"{v / 1000:.0f}k"
    return lambda v: f"{v:.0f}"


@dataclass
class ScenarioData:
    spec: Dict
    units: gpd.GeoDataFrame          # unit geometry + a, b, name
    rho: float
    n: int


# ---------------------------------------------------------------------------
# Data
# ---------------------------------------------------------------------------

def _variable(spec: Dict, cfg: CityConfig, rc: Dict, repo_root: Path, index: pd.Index) -> pd.Series:
    if spec["source"] == "package":
        return read_data_layer(cfg.package_dir, spec["dataLayerId"])[spec["variableId"]].reindex(index).astype(float)
    if spec["source"] == "employment_rate_deso":
        t = next(iter(load_scb_tables(repo_root / rc["employment"], only=["Tab9_DeSO"]).values()))
        d = t.data.set_index(t.data[t.id_column].astype(str).radd("deso:"))
        return (100.0 * d["Sysselsatt"] / d["Totalt"]).reindex(index).astype(float)
    raise ValueError(f"Unknown variable source {spec['source']!r}")


def _names(cfg: CityConfig, layer: str, units: gpd.GeoDataFrame) -> pd.Series:
    if layer == "deso":
        names = read_data_layer(cfg.package_dir, "deso_places")["area_name"].reindex(units.index)
        return names.fillna("") + " (" + units.index.str.replace("deso:", "", regex=False) + ")"
    if layer == "ruta":
        # "250 m grid cell in Eneborg V (3572506212750)": the district name is for orientation only.
        places = read_data_layer(cfg.package_dir, "ruta_places")["area_name"].reindex(units.index)
        code = units.index.str.split("_", n=1).str[1]
        return units["display_name"] + " in " + places.fillna("?") + " (" + code + ")"
    return units["display_name"].fillna(pd.Series(units.index, index=units.index))


def unit_filter_mask(cfg: CityConfig, units: gpd.GeoDataFrame, unit_filter: Dict) -> pd.Series:
    """
    True for the units a scenario keeps (same rule as the 3D view's
    `unitFilter`): unit-id prefix (e.g. 250 m cells only) and a minimum of
    a package variable (e.g. at least 50 residents). Others become no data.
    """
    keep = pd.Series(True, index=units.index)
    if not unit_filter:
        return keep
    if unit_filter.get("unitPrefix"):
        keep &= units.index.str.startswith(unit_filter["unitPrefix"])
    if unit_filter.get("variableId"):
        v = read_data_layer(cfg.package_dir, unit_filter["dataLayerId"])[unit_filter["variableId"]].reindex(units.index)
        keep &= v.astype(float).ge(float(unit_filter.get("minimum", 0)))
    return keep


def load_scenarios(config_path: Path, scenarios_path: Path, repo_root: Path):
    cfg = load_city_config(config_path, repo_root)
    rc = cfg.raw.get("dataReport") or {}
    spec = json.loads(Path(scenarios_path).read_text(encoding="utf-8"))
    out = []
    for s in spec["scenarios"]:
        units = read_spatial_layer(cfg.package_dir, s["unitLayer"])
        units["a"] = _variable(s["a"], cfg, rc, repo_root, units.index)
        units["b"] = _variable(s["b"], cfg, rc, repo_root, units.index)
        units["name"] = _names(cfg, s["unitLayer"], units)
        keep = unit_filter_mask(cfg, units, s.get("unitFilter"))
        units.loc[~keep, ["a", "b"]] = np.nan          # outside the filter: no data (hatched)
        both = units.dropna(subset=["a", "b"])
        rho = float(both["a"].rank().corr(both["b"].rank()))
        out.append(ScenarioData(s, units, rho, len(both)))
        logger.info(f"  {s['id']}: rho = {rho:+.2f}, n = {len(both)}")
    roadmap = load_shapefiles(repo_root / rc["roadMap"], ["my_riks", "mv_riks", "vl_riks"]) if rc.get("roadMap") else {}
    frame_units = read_spatial_layer(cfg.package_dir, "deso")
    b = frame_units.total_bounds
    pad = 300
    frame = (b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad)
    return cfg, spec, out, roadmap, frame


def describe(sd: ScenarioData) -> Dict:
    """Named example areas and exceptions for the handbook."""
    u = sd.units.dropna(subset=["a", "b"]).copy()
    ra, rb = u["a"].rank(pct=True), u["b"].rank(pct=True)
    if sd.rho < 0:
        rb = 1 - rb                                  # align so "agree" means following the pattern
    u["agree"] = (ra + rb) / 2
    u["gap"] = (ra - rb).abs()
    hi_a, hi_b = u["a"] >= u["a"].median(), u["b"] >= u["b"].median()
    quad = pd.crosstab(hi_a.map({True: "high", False: "low"}), hi_b.map({True: "high", False: "low"}))
    return {
        "top": u.sort_values("agree", ascending=False).head(3),
        "bottom": u.sort_values("agree").head(3),
        "exceptions": u.sort_values("gap", ascending=False).head(3),
        "quadrants": quad,
        "missing_b": int(sd.units["b"].isna().sum()),
    }


# ---------------------------------------------------------------------------
# Drawing
# ---------------------------------------------------------------------------

def _context(ax, roadmap):
    if "my_riks" in roadmap:
        roadmap["my_riks"].plot(ax=ax, color=S.LAND, linewidth=0, zorder=0)
    if "mv_riks" in roadmap:
        roadmap["mv_riks"].plot(ax=ax, color=S.WATER, linewidth=0, zorder=1)


def draw_map(ax, sd: ScenarioData, column: str, ramp: List[str], roadmap, frame, title: str):
    v = sd.units[column]
    bounds = class_bounds(v, k=CLASSES)
    colors = _ramp(ramp, len(bounds) - 1)
    _context(ax, roadmap)
    has = v.notna()
    if (~has).any():
        sd.units[~has].plot(ax=ax, facecolor=S.NO_DATA, edgecolor=S.INK_3, hatch="////", linewidth=0.2, zorder=3)
    sd.units[has].plot(ax=ax, column=column, cmap=ListedColormap(colors), norm=BoundaryNorm(bounds, len(colors)),
                       edgecolor="white", linewidth=0.35, zorder=4)
    if "vl_riks" in roadmap:
        roadmap["vl_riks"].plot(ax=ax, color="white", linewidth=0.25, alpha=0.5, zorder=5)
    _map_axes(ax, frame, title)
    _scale_bar(ax, frame)
    spec = sd.spec[column]
    f = _fmt(spec["unit"])
    unit = " SEK" if spec["unit"] == "SEK" else " %"
    labels = [f"{f(bounds[i])} – {f(bounds[i + 1])}{unit}" for i in range(len(colors))]
    _legend_classes(ax, bounds, colors=colors, labels=labels, no_data=bool((~has).any()))


def participant_page(sd: ScenarioData, number: int, roadmap, frame) -> plt.Figure:
    fig = plt.figure(figsize=(11.69, 8.27))
    s = sd.spec
    fig.text(0.04, 0.95, f"Scenario {number}: {s['title']}", fontsize=15, fontweight="bold", color=S.INK, va="top")
    fig.text(0.04, 0.905, "\n".join(textwrap.wrap(s["question"], 150)), fontsize=10.5, color=S.INK, va="top", linespacing=1.4)
    ax_a = fig.add_axes([0.03, 0.12, 0.46, 0.68])
    ax_b = fig.add_axes([0.51, 0.12, 0.46, 0.68])
    draw_map(ax_a, sd, "a", S.BLUE, roadmap, frame, f"A  {s['a']['label']} ({s['a']['year']})")
    draw_map(ax_b, sd, "b", ORANGE, roadmap, frame, f"B  {s['b']['label']} ({s['b']['year']})")
    fig.text(0.04, 0.035, f"Areas: {s['unitLabel']}. Each colour class holds about the same number of areas; darker = higher. "
             f"Hatched = no data. A: {s['a']['sourceText']}. B: {s['b']['sourceText']}.",
             fontsize=7, color=S.INK_2, wrap=True)
    return fig


def scatter_png(sd: ScenarioData, info: Dict, path: Path) -> Path:
    u = sd.units.dropna(subset=["a", "b"])
    s = sd.spec
    fig, ax = plt.subplots(figsize=(4.4, 3.3))
    ax.scatter(u["a"], u["b"], s=16, color=S.CAT[0], alpha=0.7, edgecolor="white", linewidth=0.4)
    exceptions = info["exceptions"] if abs(sd.rho) >= 0.4 else info["exceptions"].iloc[0:0]   # no pattern, no exceptions
    for _, r in exceptions.iterrows():
        ax.scatter([r["a"]], [r["b"]], s=30, facecolor="none", edgecolor=S.CAT[7], linewidth=1.2)
        ax.annotate(r["name"].split(" (")[0], (r["a"], r["b"]), xytext=(4, 4), textcoords="offset points",
                    fontsize=6.5, color=S.INK)
    fa = _fmt(s["a"]["unit"])
    ax.xaxis.set_major_formatter(plt.FuncFormatter(lambda v, _: fa(v)))
    ax.set_xlabel(f"A: {s['a']['label']} ({'SEK' if s['a']['unit'] == 'SEK' else '%'})")
    ax.set_ylabel(f"B: {s['b']['label']} (%)", fontsize=7.5)
    ax.text(0.02, 0.97, f"Spearman ρ = {sd.rho:+.2f}\nn = {sd.n} {s['unitLabel']}", transform=ax.transAxes, va="top",
            fontsize=7.5, color=S.INK_2)
    ax.grid(True, color=S.GRID, lw=0.5)
    ax.set_axisbelow(True)
    fig.tight_layout()
    fig.savefig(path)
    plt.close(fig)
    return path


# ---------------------------------------------------------------------------
# PDFs
# ---------------------------------------------------------------------------

def strength_word(rho: float) -> str:
    a = abs(rho)
    word = "strong" if a >= 0.7 else "moderate" if a >= 0.4 else "weak" if a >= 0.2 else "no"
    if word == "no":
        return "no relationship"
    return f"{word}, {'same direction' if rho > 0 else 'opposite directions'}"


def build_participant_pdf(scen: List[ScenarioData], roadmap, frame, out_dir: Path) -> List[Path]:
    pdf_path = out_dir / "study_2d_maps.pdf"
    pngs = []
    with PdfPages(pdf_path) as pdf:
        for i, sd in enumerate(scen, 1):
            fig = participant_page(sd, i, roadmap, frame)
            pdf.savefig(fig)
            png = out_dir / "figures" / f"{sd.spec['id']}_2d_page.png"
            fig.savefig(png, dpi=130)
            pngs.append(png)
            plt.close(fig)
    return [pdf_path] + pngs


def build_handbook(cfg: CityConfig, spec: Dict, scen: List[ScenarioData], page_pngs: List[Path], out_dir: Path) -> Path:
    from datetime import datetime
    doc = ReportDoc()
    doc.title("Facilitator handbook: 2D vs 3D pattern test",
              f"{cfg.display_name}. Three open scenarios, asked first on 2D maps and then on the 3D table. "
              f"Contains the answers: do not show to participants. Generated {datetime.now():%Y-%m-%d}.")
    doc.tiles([("3", "scenarios, each asked twice (2D, then 3D)"), ("≈ 2 min", "per scenario and round"),
               ("20 min", "with the system"), ("10 min", "interview")])
    doc.note("<b>Licence.</b> The maps show FUK-licensed SCB and Lantmäteriet data. Keep these PDFs within the project; "
             "don't publish them.")

    doc.h2("The session at a glance")
    doc.table(["Time", "What happens", "You do"], [
        ["Before", "Consent, participant ID, explain think-aloud", "“Please say what you look at and what you think, also when you're unsure.”"],
        ["0–1 min", "2D: how to read the maps", "Show page 1 of the 2D PDF: map A left, map B right, darker = higher."],
        ["1–7 min", "2D: scenarios 1, 2, 3", "Per scenario: read the question, start timer, take notes (below)."],
        ["7–8 min", "Round rating", "“How easy was it to see whether there was a pattern, 1 to 7?”"],
        ["8–11 min", "Headset on, VR tutorial", "Short: point, select, move/resize the table."],
        ["11–17 min", "3D: scenarios 1, 2, 3 (same data)", "Same question + “Does this view change or add anything?”"],
        ["17–18 min", "Round rating + comfort", "Same rating; “Any discomfort: none, slight, moderate, strong?”"],
        ["18–20 min", "Buffer", "Headset off."],
        ["then", "Interview (10 min)", "Section 5. Survey is sent the same day."],
    ], widths=[0.12, 0.33, 0.55])

    doc.h2("For every scenario, in both rounds")
    doc.bullets([
        "<b>Read the question aloud, word for word</b> (it's printed on the 2D page and below). Start the timer.",
        "<b>Let them explore and think aloud.</b> Help with controls only, never with the content.",
        "<b>Write down the time of their first claim</b> about a pattern, and what they said.",
        "<b>Stop at 2½ minutes.</b> If they haven't said it yet, ask: “Is there a pattern? How strong? Which direction?”",
        "<b>Ask: “How sure are you, 1 to 5?”</b> In the 3D round, also: “Does this view change or add anything?”",
        "Don't confirm or correct answers. Reveal the right answers only after they've done the survey.",
    ])
    doc.h2("Scoring each answer")
    doc.table(["What", "Score"], [
        ["Direction", "right / wrong / not stated (same direction, opposite directions, or none)"],
        ["Strength", "right / wrong (strong / some / none)"],
        ["Where", "names at least one correct area: yes / no"],
        ["False pattern", "scenario 2 only: claims a pattern that isn't there: yes / no"],
        ["Time", "seconds to the first claim; time-out at 150 s"],
    ], widths=[0.2, 0.8])

    for i, (sd, png) in enumerate(zip(scen, page_pngs), 1):
        s = sd.spec
        info = describe(sd)
        doc.h1(f"{i}  {s['title']}")
        doc.note(f"<b>Ask:</b> “{s['question']}”", tint=S_tint())
        doc.table(["", "Variable", "Year", "Source"], [
            ["A", s["a"]["label"], s["a"]["year"], s["a"]["sourceText"]],
            ["B", s["b"]["label"], s["b"]["year"], s["b"]["sourceText"]],
        ], widths=[0.04, 0.3, 0.08, 0.58])
        doc.p(f"<b>Shown in 2D:</b> page {i} of the 2D PDF, map A (blue) next to map B (orange). "
              f"<b>Shown in 3D:</b> one view of the {s['unitLabel']}: height = A, colour = B.")
        doc.h2("The answer")
        doc.note(f"<b>{strength_word(sd.rho).capitalize()}</b> (Spearman ρ = {sd.rho:+.2f}, n = {sd.n} {s['unitLabel']}). "
                 f"{s['answer']}", tint=S_answer())
        hi = ", ".join(info["top"]["name"])
        lo = ", ".join(info["bottom"]["name"])
        if abs(sd.rho) >= 0.4:
            pos = sd.rho > 0
            doc.bullets([
                f"<b>{'High on both' if pos else 'High A, low B'}:</b> {hi}.",
                f"<b>{'Low on both' if pos else 'Low A, high B'}:</b> {lo}.",
                f"<b>Exceptions</b> (furthest from the pattern, circled in the chart): {', '.join(info['exceptions']['name'])}.",
            ])
        q = info["quadrants"]
        doc.p("Areas above or below the median of each variable (a pattern shows as most areas on one diagonal):")
        doc.table(["", "B low", "B high"], [[f"A {k}", int(q.loc[k].get("low", 0)), int(q.loc[k].get("high", 0))]
                                            for k in ("low", "high") if k in q.index], widths=[0.2, 0.2, 0.2])
        doc.figure(scatter_png(sd, info, out_dir / "figures" / f"{s['id']}_scatter.png"),
                   "Facilitator only. Each dot is one area" + ("; red circles = the exceptions listed above."
                                                               if abs(sd.rho) >= 0.4 else ": no trend."), width_frac=0.62)
        doc.h2("Why this scenario")
        doc.bullets([f"<b>Planning link:</b> {s['planning']}", f"<b>What it tests:</b> {s['tests']}"] +
                    [f"<b>Note:</b> {n}" for n in s.get("notes", [])] +
                    ([f"<b>No data:</b> {info['missing_b']} {s['unitLabel']} have no value for B."] if info["missing_b"] else []))

    doc.h1("5  Interview (10 min)")
    doc.p("Ask the ★ questions first; the rest if time allows. Refer back to what they said while thinking aloud.")
    doc.bullets([
        "★ What's your overall impression? How did the flat maps compare with the 3D table?",
        "★ How did you decide whether there was a pattern? What did you look at first? <i>Different in 2D and 3D?</i>",
        "★ Did the 3D table change any of your answers, or how sure you were? Which scenario, and why?",
        "★ In the income and children question, what made you decide? <i>Did you expect a pattern before you looked?</i>",
        "Was there a moment when height and colour together helped you, or got in the way?",
        "★ Do you meet questions like these in your work? How do you answer them today?",
        "★ Where in a planning process would a view like this help, and where wouldn't it?",
        "Would you trust a pattern you found here enough to act on it? What would you check first?",
        "★ If you could change one thing, what would it be?",
        "Anything we haven't asked that you'd like to mention?",
    ])
    doc.h2("Survey (send the same day)")
    doc.bullets([
        "<b>About you:</b> role, years in planning, how often you use GIS, VR experience, how well you know the city.",
        "<b>The two views:</b> rate six statements for flat maps and for the 3D table (1–5): easy to see relations; easy to see "
        "where; easy to compare areas; confident; tiring; would use at work. Which view helped more per scenario; overall preference.",
        "<b>The 3D table:</b> System Usability Scale (10 standard items); useful features; hard features; discomfort.",
        "<b>Planning practice:</b> where it would help; who should use it; tools used today; data to add (up to 3).",
        "<b>Optional:</b> anything else.",
    ])

    doc.h1("Answer sheet")
    doc.p("Participant ID: ____________  Date: ____________  Order: 2D → 3D")
    rows = []
    for sd in scen:
        for rnd in ("2D", "3D"):
            rows.append([f"{sd.spec['id']} {sd.spec['title']}", rnd, "", "", "", "", "<br/><br/><br/>"])
    doc.table(["Scenario", "Round", "First claim (s)", "Claim: strength / direction", "Where", "Sure 1–5", "Notes"], rows,
              widths=[0.2, 0.07, 0.1, 0.2, 0.15, 0.08, 0.2], zebra=False)
    doc.p("Round ratings (1–7): 2D easy ____  3D easy ____    Discomfort after VR: none / slight / moderate / strong")

    path = out_dir / "study_facilitator_handbook.pdf"
    doc.build(path, title="Facilitator handbook", footer=f"{cfg.display_name} pattern study · FACILITATOR ONLY: contains answers")
    return path


def S_tint():
    from reportlab.lib import colors
    return colors.HexColor("#eef4fc")


def S_answer():
    from reportlab.lib import colors
    return colors.HexColor("#e8f6ef")


def build_study_materials(config_path: Path, scenarios_path: Path, repo_root: Path, out_dir: Path) -> Dict[str, Path]:
    out_dir = Path(out_dir)
    (out_dir / "figures").mkdir(parents=True, exist_ok=True)
    cfg, spec, scen, roadmap, frame = load_scenarios(config_path, scenarios_path, repo_root)
    paths = build_participant_pdf(scen, roadmap, frame, out_dir)
    handbook = build_handbook(cfg, spec, scen, paths[1:], out_dir)
    return {"maps": paths[0], "handbook": handbook}
