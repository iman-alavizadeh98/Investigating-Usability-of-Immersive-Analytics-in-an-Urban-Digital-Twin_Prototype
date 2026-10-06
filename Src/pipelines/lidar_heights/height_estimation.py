"""
Per-building height: one number for extruding a flat-roofed building.

    ground(x, y) = linear interpolation (Delaunay) of 2010 laser ground points
                   around the footprint (points inside the footprint ignored)
    height_m     = p95( z - ground(x, y) ) over the roof points inside the footprint

Roof points come from the 2018 surface model; the 2010 laser roof points
(class 1) are used only where the 2018 surface has too few points. See
config.py for every threshold and docs/data-analysis/2026-10-06_helsingborg_laserdata_nh_profiling.md
for why.
"""

import logging
from typing import Dict, Optional, Tuple

import matplotlib.tri as mtri
import numpy as np
import shapely
from scipy.spatial import cKDTree

from .config import HeightConfig, HeightSource, NoHeightReason, QualityLevel
from .sources import TilePoints

logger = logging.getLogger(__name__)


class GroundSurface:
    """
    Linear interpolation on the Delaunay triangulation of the ground points
    (the same surface as PDAL's hag_delaunay); nearest ground point outside
    their convex hull.

    Uses matplotlib.tri (its own Delaunay and triangle lookup in C++). The
    scipy routes (LinearNDInterpolator, Delaunay.find_simplex) call LAPACK,
    which crashes the process (Windows 0xc06d007f) in the digitaltwin env,
    where numpy.linalg is broken as well (checked 2026-10-06).
    """

    def __init__(self, x: np.ndarray, y: np.ndarray, z: np.ndarray):
        x = np.asarray(x, dtype=np.float64)
        y = np.asarray(y, dtype=np.float64)
        self._z = np.asarray(z, dtype=np.float64)
        self._tree = cKDTree(np.column_stack((x, y)))
        self._interp = None
        if len(x) >= 3:
            try:
                tri = mtri.Triangulation(x, y)
                self._interp = mtri.LinearTriInterpolator(tri, self._z)
            except (RuntimeError, ValueError):  # collinear or degenerate points
                self._interp = None

    def __call__(self, x: np.ndarray, y: np.ndarray) -> np.ndarray:
        x = np.asarray(x, dtype=np.float64)
        y = np.asarray(y, dtype=np.float64)
        out = np.full(len(x), np.nan)
        if self._interp is not None and len(x):
            values = self._interp(x, y)
            out = np.ma.filled(values.astype(np.float64), np.nan)
        missing = np.isnan(out)
        if missing.any():
            _, nearest = self._tree.query(np.column_stack((x[missing], y[missing])))
            out[missing] = self._z[nearest]
        return out


def _inside(points: TilePoints, geom) -> np.ndarray:
    """Indices of points strictly inside geom (boundary excluded)."""
    if geom is None or geom.is_empty or len(points) == 0:
        return np.empty(0, dtype=np.int64)
    idx = points.bbox_indices(*geom.bounds)
    if len(idx) == 0:
        return idx
    return idx[shapely.contains_xy(geom, points.x[idx], points.y[idx])]


def _outline_samples(geom, spacing: float) -> np.ndarray:
    """Points along the exterior rings, at most `spacing` apart."""
    polys = geom.geoms if hasattr(geom, "geoms") else [geom]
    coords = [np.asarray(shapely.segmentize(p.exterior, spacing).coords)[:, :2] for p in polys]
    return np.vstack(coords)


class HeightEstimator:
    """Height for one building from prepared point sets of its surroundings."""

    def __init__(self, config: HeightConfig):
        self.config = config

    # -- pieces ---------------------------------------------------------------

    def ground_surface(self, geom, ground: TilePoints) -> Tuple[Optional[GroundSurface], int]:
        """Ground around the footprint, widening the search until enough points."""
        cfg = self.config
        exclude = geom.buffer(cfg.ground_exclude_buffer_m)
        for distance in cfg.ground_search_m:
            minx, miny, maxx, maxy = geom.bounds
            idx = ground.bbox_indices(minx - distance, miny - distance, maxx + distance, maxy + distance)
            if len(idx):
                idx = idx[~shapely.contains_xy(exclude, ground.x[idx], ground.y[idx])]
            if len(idx) >= cfg.ground_min_points:
                return GroundSurface(ground.x[idx], ground.y[idx], ground.z[idx]), int(len(idx))
        return None, 0

    def roof_heights(self, points: TilePoints, idx: np.ndarray, ground: GroundSurface) -> np.ndarray:
        """Height above ground of each selected point."""
        return points.z[idx] - ground(points.x[idx], points.y[idx])

    def surface_points(self, geom, surface: TilePoints) -> Tuple[np.ndarray, float]:
        """2018 surface points in the shrunk footprint (full footprint if too few)."""
        cfg = self.config
        shrunk = geom.buffer(-cfg.footprint_shrink_m)
        idx = _inside(surface, shrunk)
        used = shrunk
        if len(idx) < cfg.surface_min_points:
            idx = _inside(surface, geom)
            used = geom
        expected = used.area / cfg.surface_point_spacing_m ** 2 if not used.is_empty else 0.0
        fill = min(1.0, len(idx) / expected) if expected > 0 else 0.0
        return idx, fill

    # -- one building ---------------------------------------------------------

    def estimate(
        self,
        building_id: str,
        geom,
        ground_points: TilePoints,
        lidar_roof_points: TilePoints,
        surface_points: Optional[TilePoints],
        height_run_id: str,
    ) -> Dict:
        cfg = self.config
        result = self._empty(building_id, height_run_id)

        ground, n_ground = self.ground_surface(geom, ground_points)
        result["ground_point_count"] = n_ground
        if ground is None:
            return self._no_height(result, NoHeightReason.NO_GROUND)

        outline = _outline_samples(geom, cfg.outline_sample_spacing_m)
        outline_z = ground(outline[:, 0], outline[:, 1])
        result["ground_z"] = round(float(np.median(outline_z)), 3)
        result["ground_z_min"] = round(float(np.min(outline_z)), 3)

        # 2018 surface (primary)
        surface_hag = None
        if surface_points is not None:
            s_idx, fill = self.surface_points(geom, surface_points)
            result["surface_point_count"] = int(len(s_idx))
            result["surface_fill_ratio"] = round(fill, 3)
            if len(s_idx) >= cfg.surface_min_points:
                surface_hag = self.roof_heights(surface_points, s_idx, ground)
                result["height_surface_2018_m"] = round(float(np.percentile(surface_hag, cfg.percentile)), 3)

        # 2010 laser roof points (fallback, and for the change flag / quality)
        l_idx = _inside(lidar_roof_points, geom)
        result["lidar_point_count"] = int(len(l_idx))
        lidar_hag = None
        if len(l_idx) >= cfg.lidar_min_points:
            lidar_hag = self.roof_heights(lidar_roof_points, l_idx, ground)
            result["height_lidar_2010_m"] = round(float(np.percentile(lidar_hag, cfg.percentile)), 3)

        h_s = result["height_surface_2018_m"]
        h_l = result["height_lidar_2010_m"]
        if h_s is not None and h_l is not None:
            result["height_change_flag"] = bool(abs(h_s - h_l) > cfg.change_threshold_m)

        # Decide
        if h_s is not None and h_s >= cfg.min_visible_height_m:
            return self._with_height(result, HeightSource.SURFACE_2018, surface_hag)
        if h_s is not None and result["surface_fill_ratio"] >= cfg.surface_min_fill_ratio:
            # The 2018 surface sees the footprint and finds no building. A 2010
            # height would belong to whatever stood there in 2010.
            return self._no_height(result, NoHeightReason.SURFACE_SHOWS_GROUND)
        if h_l is not None and h_l >= cfg.min_visible_height_m:
            return self._with_height(result, HeightSource.LIDAR_2010, lidar_hag)
        if h_s is not None or h_l is not None:
            return self._no_height(result, NoHeightReason.BELOW_MIN_HEIGHT)
        return self._no_height(result, NoHeightReason.NO_POINTS)

    # -- result dicts ---------------------------------------------------------

    def _empty(self, building_id: str, height_run_id: str) -> Dict:
        return {
            "building_id": building_id,
            "height_m": self.config.fallback_height_m,
            "has_height": False,
            "height_source": HeightSource.NONE.value,
            "height_quality": QualityLevel.NONE.value,
            "no_height_reason": None,
            "ground_z": None,
            "ground_z_min": None,
            "roof_z": None,
            "height_p50_m": None,
            "height_max_m": None,
            "height_surface_2018_m": None,
            "height_lidar_2010_m": None,
            "height_change_flag": False,
            "surface_point_count": 0,
            "surface_fill_ratio": 0.0,
            "lidar_point_count": 0,
            "ground_point_count": 0,
            "height_run_id": height_run_id,
        }

    def _with_height(self, result: Dict, source: HeightSource, hag: np.ndarray) -> Dict:
        cfg = self.config
        height = float(np.percentile(hag, cfg.percentile))
        result.update({
            "height_m": round(height, 3),
            "has_height": True,
            "height_source": source.value,
            "roof_z": round(result["ground_z"] + height, 3),
            "height_p50_m": round(float(np.percentile(hag, 50)), 3),
            "height_max_m": round(float(np.max(hag)), 3),
        })
        n = result["surface_point_count"] if source == HeightSource.SURFACE_2018 else result["lidar_point_count"]
        if source == HeightSource.LIDAR_2010 or n < cfg.quality_min_points:
            quality = QualityLevel.LOW
        elif (result["height_lidar_2010_m"] is not None
              and abs(result["height_surface_2018_m"] - result["height_lidar_2010_m"]) <= cfg.agreement_threshold_m):
            quality = QualityLevel.HIGH
        else:
            quality = QualityLevel.MEDIUM
        result["height_quality"] = quality.value
        return result

    def _no_height(self, result: Dict, reason: NoHeightReason, detail: str = "") -> Dict:
        result["no_height_reason"] = reason.value + (f": {detail}" if detail else "")
        return result

    def error_result(self, building_id: str, height_run_id: str, error: Exception) -> Dict:
        return self._no_height(self._empty(building_id, height_run_id), NoHeightReason.ERROR, str(error))

    def no_tile_result(self, building_id: str, height_run_id: str) -> Dict:
        return self._no_height(self._empty(building_id, height_run_id), NoHeightReason.NO_TILE)
