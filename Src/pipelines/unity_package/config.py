"""
City config for the Unity package builder.

Example (configs/cities/<city>.json):

{
  "city": {"id": "helsingborg", "displayName": "Helsingborg"},
  "crs": "EPSG:3006",
  "unity": {
    "streamingAssetsDirectory": "Unity/City_Digital_Twin/Assets/StreamingAssets",
    "packageDirectory": "helsingborg",
    "originInSourceCRS": {"easting": 355000.0, "northing": 6207500.0, "elevation": 0.0},
    "metersToUnity": 0.001
  },
  "buildings": {
    "source": "Processed_data/<city>/lidar_heights/buildings_lidar_added.gpkg",
    "layer": null
  },
  "statistics": {                          optional: SCB grid + DeSO layers
    "population": "Raw_data/<city>/<SCB befolkning folder>",
    "income": "Raw_data/<city>/<SCB inkomster folder>"
  },
  "election": {                            optional: voting districts
    "folder": "Raw_data/<city>/<folder with results .xlsx + districts>",
    "municipalityCode": "<4-digit Kommunkod>",
    "minPartySharePct": 0.5
  },
  "estimates": {                           optional: building-based estimates per district
    "storeyHeightM": 3.0,
    "minCoveragePct": 99.0
  },
  "visualizations": "configs/visualizations/<set>"   optional: preset folder with catalog.json
}

Relative paths are resolved against the repository root.

The origin is FIXED per city and written by hand, never derived from the
data: positions and interaction logs must mean the same ground in every run
(see "Analytical Grid Anchor" in Project_livingContext.md). Choose a round
number on the 500 m lattice near the south-west corner of the city's data.
"""

import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Optional

# Coordinates are stored as float32 relative to the origin; beyond this
# distance float32 loses millimetre precision (2^24 mm ~ 16.7 km).
MAX_DISTANCE_FROM_ORIGIN_M = 16_000.0

ID_PATTERN = re.compile(r"^[a-z0-9_]+$")

# Every city package lives in StreamingAssets/cities/<packageDirectory>/.
# The whole cities/ folder is git-ignored: packages are generated from
# licensed data and are rebuilt, never committed.
PACKAGES_ROOT = "cities"


@dataclass
class BuildingsSource:
    source: Path
    layer: Optional[str] = None


@dataclass
class StatisticsSource:
    population: Path
    income: Optional[Path] = None


@dataclass
class ElectionSource:
    folder: Path
    municipality_code: str
    min_party_share_pct: float = 0.5


@dataclass
class EstimateSettings:
    storey_height_m: float = 3.0
    min_coverage_pct: float = 99.0


@dataclass
class CityConfig:
    city_id: str
    display_name: str
    crs: str
    streaming_assets_dir: Path
    package_directory: str
    origin_easting: float
    origin_northing: float
    origin_elevation: float
    meters_to_unity: float
    buildings: Optional[BuildingsSource] = None
    statistics: Optional[StatisticsSource] = None
    election: Optional[ElectionSource] = None
    estimates: EstimateSettings = field(default_factory=EstimateSettings)
    visualizations: Optional[Path] = None
    config_path: Optional[Path] = None
    raw: Dict = field(default_factory=dict)

    @property
    def package_dir(self) -> Path:
        """Folder of this city's package: StreamingAssets/cities/<packageDirectory>/."""
        return self.streaming_assets_dir / PACKAGES_ROOT / self.package_directory

    @property
    def manifest_path_in_streaming_assets(self) -> str:
        """What to enter as ProjectManager's manifest path in the Unity scene."""
        return f"{PACKAGES_ROOT}/{self.package_directory}/project_manifest.json"


def _require(d: Dict, key: str, where: str):
    if key not in d or d[key] in (None, ""):
        raise ValueError(f"City config: '{where}{key}' is required")
    return d[key]


def load_city_config(path: Path, repo_root: Path) -> CityConfig:
    path = Path(path)
    raw = json.loads(path.read_text(encoding="utf-8"))

    def resolve(p: str) -> Path:
        p = Path(p)
        return p if p.is_absolute() else repo_root / p

    city = _require(raw, "city", "")
    unity = _require(raw, "unity", "")
    origin = _require(unity, "originInSourceCRS", "unity.")

    city_id = _require(city, "id", "city.")
    if not ID_PATTERN.match(city_id):
        raise ValueError(f"City config: city.id '{city_id}' must be lowercase letters, digits or '_'")
    package_directory = unity.get("packageDirectory") or city_id
    if not ID_PATTERN.match(package_directory):
        raise ValueError(f"City config: unity.packageDirectory '{package_directory}' must be lowercase letters, digits or '_'")

    buildings = None
    if raw.get("buildings"):
        b = raw["buildings"]
        buildings = BuildingsSource(source=resolve(_require(b, "source", "buildings.")), layer=b.get("layer"))

    statistics = None
    if raw.get("statistics"):
        s = raw["statistics"]
        statistics = StatisticsSource(
            population=resolve(_require(s, "population", "statistics.")),
            income=resolve(s["income"]) if s.get("income") else None,
        )

    election = None
    if raw.get("election"):
        e = raw["election"]
        code = str(_require(e, "municipalityCode", "election."))
        if not re.fullmatch(r"\d{4}", code):
            raise ValueError(f"City config: election.municipalityCode must be 4 digits, got '{code}'")
        election = ElectionSource(
            folder=resolve(_require(e, "folder", "election.")),
            municipality_code=code,
            min_party_share_pct=float(e.get("minPartySharePct", 0.5)),
        )

    est = raw.get("estimates") or {}
    estimates = EstimateSettings(
        storey_height_m=float(est.get("storeyHeightM", 3.0)),
        min_coverage_pct=float(est.get("minCoveragePct", 99.0)),
    )

    return CityConfig(
        city_id=city_id,
        display_name=_require(city, "displayName", "city."),
        crs=_require(raw, "crs", ""),
        streaming_assets_dir=resolve(_require(unity, "streamingAssetsDirectory", "unity.")),
        package_directory=package_directory,
        origin_easting=float(_require(origin, "easting", "unity.originInSourceCRS.")),
        origin_northing=float(_require(origin, "northing", "unity.originInSourceCRS.")),
        origin_elevation=float(origin.get("elevation", 0.0)),
        meters_to_unity=float(_require(unity, "metersToUnity", "unity.")),
        buildings=buildings,
        statistics=statistics,
        election=election,
        estimates=estimates,
        visualizations=resolve(raw["visualizations"]) if raw.get("visualizations") else None,
        config_path=path,
        raw=raw,
    )
