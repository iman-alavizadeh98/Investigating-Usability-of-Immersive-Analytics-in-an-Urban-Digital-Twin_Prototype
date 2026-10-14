"""
Shared look of the data report: palette, colour ramps, figure defaults, fonts.

Colours follow one documented palette (categorical slots in a fixed order, a
one-hue blue ramp for magnitude, blue <-> red with a grey midpoint for signed
values). Text never takes a series colour. Matplotlib's bundled DejaVu fonts are
registered with ReportLab too, so Swedish letters and symbols (ρ, ≥, ², –)
render the same in figures and text.
"""

from __future__ import annotations

from pathlib import Path

import matplotlib

matplotlib.use("Agg")

import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.colors import BoundaryNorm, LinearSegmentedColormap, ListedColormap  # noqa: E402

# Surfaces and ink
SURFACE = "#fcfcfb"
INK = "#0b0b0b"
INK_2 = "#52514e"
INK_3 = "#8a8984"
GRID = "#e4e3df"
NO_DATA = "#e9e8e4"

# Map context (recessive)
LAND = "#f4f3f0"
WATER = "#dde7ef"
ROAD = "#c9c7c1"
BOX = "#52514e"

# Categorical slots, fixed order
CAT = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"]

# Sequential blue (light -> dark) and a red arm for diverging values
BLUE = ["#cde2fb", "#b7d3f6", "#9ec5f4", "#86b6ef", "#6da7ec", "#5598e7", "#3987e5",
        "#2a78d6", "#256abf", "#1c5cab", "#184f95", "#104281", "#0d366b"]
RED = ["#fbd9d6", "#f6b8b2", "#f0968e", "#e8746b", "#e34948", "#c93b3a", "#a82f2e"]
MID = "#f0efec"

SEQ_CMAP = LinearSegmentedColormap.from_list("seq_blue", BLUE)
DIV_CMAP = LinearSegmentedColormap.from_list(
    "div_blue_red", [BLUE[-1], BLUE[6], BLUE[2], MID, RED[1], RED[4], RED[6]])

# Party colours: Swedish convention for the three parties that win districts.
PARTY_WIN = {"M": "#2a78d6", "S": "#e34948", "SD": "#eda100"}


def class_colors(n: int):
    """n ordered blue steps spread over the ramp (lightest still visible)."""
    if n == 1:
        return [BLUE[7]]
    idx = [round(2 + i * (len(BLUE) - 3) / (n - 1)) for i in range(n)]
    return [BLUE[i] for i in idx]


def stepped(bounds):
    """Discrete colormap + norm for class bounds (len = classes + 1)."""
    colors = class_colors(len(bounds) - 1)
    return ListedColormap(colors), BoundaryNorm(bounds, len(colors))


def apply_matplotlib_style() -> None:
    plt.rcParams.update({
        "font.family": "DejaVu Sans",
        "font.size": 8.5,
        "axes.edgecolor": GRID,
        "axes.labelcolor": INK_2,
        "axes.titlesize": 9.5,
        "axes.titleweight": "bold",
        "axes.titlecolor": INK,
        "axes.titlelocation": "left",
        "axes.spines.top": False,
        "axes.spines.right": False,
        "axes.grid": False,
        "xtick.color": INK_2,
        "ytick.color": INK_2,
        "xtick.labelsize": 7.5,
        "ytick.labelsize": 7.5,
        "legend.fontsize": 7.5,
        "legend.frameon": False,
        "figure.facecolor": "white",
        "axes.facecolor": "white",
        "savefig.dpi": 200,
        "savefig.bbox": "tight",
        "savefig.facecolor": "white",
    })


def dejavu_paths() -> dict:
    """Regular/bold DejaVu TTFs shipped with matplotlib."""
    root = Path(matplotlib.get_data_path()) / "fonts" / "ttf"
    return {"regular": root / "DejaVuSans.ttf", "bold": root / "DejaVuSans-Bold.ttf",
            "oblique": root / "DejaVuSans-Oblique.ttf"}
