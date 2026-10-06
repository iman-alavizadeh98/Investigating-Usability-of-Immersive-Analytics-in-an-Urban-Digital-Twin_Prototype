"""
Configuration for the building height pipeline.

One height per building, for extruding flat-roofed (LOD1) buildings:

    height_m = p95( surface z - ground z ) over the points inside the footprint

Sources (Lantmäteriet, via SLU GET; see
docs/data-analysis/2026-10-06_helsingborg_laserdata_nh_profiling.md):
- surface: Ytmodell från flygbild (surface model from aerial photos), April 2018,
  0.5 m grid, LAZ, class 0 only. Gives the roof. Primary source.
- laser:   Laserdata NH, scanned 2010-04-12, 0.5-1 pt/m2, classified by
  Lantmäteriet (1 unclassified, 2 ground, 9 water, 11 bridge). Gives the ground
  for every building, and the roof only where the 2018 surface has no points.
"""

from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from pathlib import Path
from typing import Dict, Optional, Tuple
import uuid


class HeightSource(str, Enum):
    """Where height_m comes from."""
    SURFACE_2018 = "surface_2018"  # Ytmodell från flygbild
    LIDAR_2010 = "lidar_2010"      # Laserdata NH
    NONE = "none"                  # no height; height_m = fallback_height_m


class NoHeightReason(str, Enum):
    """Why a building has no height (column no_height_reason)."""
    NO_TILE = "no_tile"                    # footprint outside every tile of both sources
    NO_GROUND = "no_ground"                # no 2010 ground points within the search distance
    SURFACE_SHOWS_GROUND = "surface_shows_ground"  # 2018 surface covers the footprint but is
                                                   # below min_visible_height: built after
                                                   # April 2018, or a very low structure
    BELOW_MIN_HEIGHT = "below_min_height"  # only 2010 laser points, all below min_visible_height
    NO_POINTS = "no_points"                # too few points in the footprint in both sources
    ERROR = "error"


class QualityLevel(str, Enum):
    """
    high:   2018 surface, >= quality_min_points, and the 2010 laser agrees within
            agreement_threshold_m (two independent sources)
    medium: 2018 surface, >= quality_min_points
    low:    2018 surface with few points, or 2010 laser (may be outdated)
    none:   no height
    """
    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"
    NONE = "none"


@dataclass
class HeightConfig:
    """Per-building height rules. Defaults are documented in the living context."""

    percentile: float = 95.0
    """Height = this percentile of (surface z - ground z) inside the footprint."""

    footprint_shrink_m: float = 0.5
    """Surface points are taken inside the footprint shrunk by this much: the
    image-matched surface smears at walls, so edge points can be ground. Falls
    back to the full footprint when the shrunk one has too few points."""

    surface_min_points: int = 4
    """Minimum 2018 surface points for a height (0.5 m grid: 4 points per m2)."""

    surface_point_spacing_m: float = 0.5
    """Grid spacing of the surface model, used for the fill ratio."""

    surface_min_fill_ratio: float = 0.5
    """If the surface has at least this share of the expected points but shows
    no building, the building is treated as absent in 2018 (no fallback to 2010,
    which would give the height of whatever stood there in 2010)."""

    lidar_min_points: int = 5
    """Minimum 2010 laser roof points (class 1) for the fallback height."""

    lidar_roof_classes: Tuple[int, ...] = (1,)
    ground_classes: Tuple[int, ...] = (2,)

    ground_search_m: Tuple[float, ...] = (15.0, 50.0)
    """Ground points are searched within these distances of the footprint, the
    next one only if the previous gave too few points."""

    ground_min_points: int = 3

    ground_exclude_buffer_m: float = 0.5
    """Ground points inside the footprint grown by this much are ignored: on
    buildings that existed in 2010 they may be misclassified roof points."""

    outline_sample_spacing_m: float = 2.0
    """Spacing of the outline samples used for ground_z / ground_z_min."""

    min_visible_height_m: float = 1.5
    """Below this the sources show no building. Not a clamp: heights at or
    above it are kept as measured."""

    change_threshold_m: float = 3.0
    """height_change_flag when the 2018 and 2010 heights differ by more."""

    agreement_threshold_m: float = 1.5
    quality_min_points: int = 20

    fallback_height_m: float = 0.0
    """height_m for buildings without a height (has_height = False)."""


@dataclass
class LiDARHeightPipelineConfig:
    """Master configuration for the building height pipeline."""

    input_buildings_path: Path
    """Buildings postprocess GeoPackage (one row per object_id)."""

    output_directory: Path

    lidar_directory: Path
    """Folder with the Laserdata NH LAZ tiles (2010)."""

    surface_path: Optional[Path] = None
    """Ytmodell LAZ tiles (2018): the delivered zip or a folder. None = 2010 only."""

    input_layer: Optional[str] = None
    """GeoPackage layer (None = the file's only layer)."""

    height_config: HeightConfig = field(default_factory=HeightConfig)

    crs: str = "EPSG:3006"

    work_cell_m: float = 1000.0
    """Buildings are processed in square cells of this size (by centroid); the
    points of a cell are read from every tile they lie in, so buildings on tile
    edges get all their points. Smaller = less memory."""

    height_run_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    run_timestamp: str = field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    verbose: bool = False

    def __post_init__(self):
        self.input_buildings_path = Path(self.input_buildings_path)
        self.output_directory = Path(self.output_directory)
        self.lidar_directory = Path(self.lidar_directory)
        if self.surface_path is not None:
            self.surface_path = Path(self.surface_path)

    @staticmethod
    def from_dict(config_dict: Dict) -> "LiDARHeightPipelineConfig":
        config_dict = dict(config_dict)
        height = config_dict.pop("height_config", {}) or {}
        for key in ("lidar_roof_classes", "ground_classes", "ground_search_m"):
            if key in height:
                height[key] = tuple(height[key])
        return LiDARHeightPipelineConfig(height_config=HeightConfig(**height), **config_dict)
