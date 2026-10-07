"""
Reading LAZ tiles (folder or zip) and holding point arrays for fast lookup.

Both Lantmäteriet products use the same 2.5 km tiles, in EPSG:3006 / RH 2000.
The LAZ headers carry no CRS; the GeoJSON sidecars say EPSG:3006.
"""

import io
import json
import logging
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

import laspy
import numpy as np

logger = logging.getLogger(__name__)

Bounds = Tuple[float, float, float, float]


class TilePoints:
    """
    Point arrays sorted by x.

    A bounding-box lookup is then two binary searches plus a y-test on the
    x-slice. Percentiles and medians do not depend on point order.
    """

    def __init__(self, x, y, z, classification=None, _sorted: bool = False):
        x = np.asarray(x, dtype=np.float64)
        if _sorted:
            order = slice(None)
        else:
            order = np.argsort(x, kind="stable")
        self.x = x[order]
        self.y = np.asarray(y, dtype=np.float64)[order]
        self.z = np.asarray(z, dtype=np.float64)[order]
        self.classification = (
            np.asarray(classification)[order] if classification is not None
            else np.zeros(len(self.x), dtype=np.uint8)
        )

    @classmethod
    def empty(cls) -> "TilePoints":
        return cls(np.empty(0), np.empty(0), np.empty(0), np.empty(0, dtype=np.uint8))

    @classmethod
    def from_las(cls, las) -> "TilePoints":
        return cls(las.x, las.y, las.z, las.classification)

    def __len__(self) -> int:
        return len(self.x)

    def where_class(self, classes: Iterable[int]) -> "TilePoints":
        """Subset with the given classes (stays sorted)."""
        mask = np.isin(self.classification, list(classes))
        return TilePoints(self.x[mask], self.y[mask], self.z[mask], self.classification[mask], _sorted=True)

    def bbox_indices(self, minx: float, miny: float, maxx: float, maxy: float) -> np.ndarray:
        """Indices of points inside the closed box [minx, maxx] x [miny, maxy]."""
        lo = int(np.searchsorted(self.x, minx, side="left"))
        hi = int(np.searchsorted(self.x, maxx, side="right"))
        y = self.y[lo:hi]
        return lo + np.flatnonzero((y >= miny) & (y <= maxy))


@dataclass
class _Tile:
    name: str
    bounds: Bounds


class TileSource:
    """
    LAZ tiles from a folder or from a zip (read in place, nothing extracted).

    Tile extents come from the LAZ headers. Sidecar .json files are ignored.
    """

    def __init__(self, path: Path, label: str):
        self.path = Path(path)
        self.label = label
        if not self.path.exists():
            raise FileNotFoundError(f"{label} not found: {self.path}")
        self._zip = zipfile.ZipFile(self.path) if self.path.suffix.lower() == ".zip" else None
        self.tiles: List[_Tile] = []
        for name in self._names():
            with self._open(name) as src:
                h = src.header
                self.tiles.append(_Tile(name, (h.x_min, h.y_min, h.x_max, h.y_max)))
        if not self.tiles:
            raise FileNotFoundError(f"No .laz tiles in {self.path}")
        logger.info(f"{label}: {len(self.tiles)} tiles in {self.path}")

    def _names(self) -> List[str]:
        if self._zip is not None:
            return sorted(n for n in self._zip.namelist() if n.lower().endswith(".laz"))
        return sorted(p.name for p in self.path.glob("*.laz"))

    def _open(self, name: str):
        if self._zip is not None:
            return laspy.open(io.BytesIO(self._zip.read(name)))
        return laspy.open(self.path / name)

    # Date fields in Lantmäteriet's GeoJSON sidecars: Laserdata NH strips
    # (*_strip.json) have "insamlingsdatum"; Ytmodell tiles have "Datum_fran" /
    # "Datum_till" (first / last aerial photo used).
    DATE_KEYS = ("insamlingsdatum", "Datum_fran", "Datum_till")

    def capture_dates(self) -> Optional[Dict[str, str]]:
        """Earliest and latest capture date found in the sidecar JSON files, or None."""
        dates = []
        if self._zip is not None:
            names = [n for n in self._zip.namelist() if n.lower().endswith(".json") and "/" not in n.strip("/")]
            read = lambda n: self._zip.read(n)  # noqa: E731
        else:
            names = sorted(p.name for p in self.path.glob("*.json"))
            read = lambda n: (self.path / n).read_bytes()  # noqa: E731
        for name in names:
            try:
                doc = json.loads(read(name))
            except (ValueError, OSError):
                continue
            props = [f.get("properties", {}) for f in doc.get("features", [])] or [doc.get("properties", {})]
            for p in props:
                for key in self.DATE_KEYS:
                    value = p.get(key) if isinstance(p, dict) else None
                    if isinstance(value, str) and len(value) >= 10:
                        dates.append(value[:10])
        if not dates:
            return None
        return {"from": min(dates), "to": max(dates), "sidecar_files": len(names)}

    @property
    def extent(self) -> Bounds:
        b = np.array([t.bounds for t in self.tiles])
        return (b[:, 0].min(), b[:, 1].min(), b[:, 2].max(), b[:, 3].max())

    def tiles_intersecting(self, bounds: Bounds) -> List[_Tile]:
        minx, miny, maxx, maxy = bounds
        return [
            t for t in self.tiles
            if t.bounds[0] <= maxx and t.bounds[2] >= minx and t.bounds[1] <= maxy and t.bounds[3] >= miny
        ]

    def read_region(
        self,
        bounds: Bounds,
        classes: Optional[Iterable[int]] = None,
        chunk_size: int = 5_000_000,
    ) -> TilePoints:
        """All points inside bounds, from every tile they lie in."""
        minx, miny, maxx, maxy = bounds
        keep_classes = list(classes) if classes is not None else None
        xs, ys, zs, cs = [], [], [], []
        for tile in self.tiles_intersecting(bounds):
            with self._open(tile.name) as src:
                for chunk in src.chunk_iterator(chunk_size):
                    x = np.asarray(chunk.x)
                    y = np.asarray(chunk.y)
                    mask = (x >= minx) & (x <= maxx) & (y >= miny) & (y <= maxy)
                    cls = np.asarray(chunk.classification)
                    if keep_classes is not None:
                        mask &= np.isin(cls, keep_classes)
                    if mask.any():
                        xs.append(x[mask])
                        ys.append(y[mask])
                        zs.append(np.asarray(chunk.z)[mask])
                        cs.append(cls[mask])
        if not xs:
            return TilePoints.empty()
        return TilePoints(np.concatenate(xs), np.concatenate(ys), np.concatenate(zs), np.concatenate(cs))
