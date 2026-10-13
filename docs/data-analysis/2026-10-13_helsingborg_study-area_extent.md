# Helsingborg study area — extent (bounding box)

**Purpose:** the area to use when downloading more data for Helsingborg, so new layers line up with the existing twin.

## Bounding box

| | SWEREF 99 TM (EPSG:3006), m | WGS84 (EPSG:4326), ° |
|---|---|---|
| North | 6220000 | 56.105284 |
| South | 6207500 | 55.990849 |
| West | 355000 | 12.668632 |
| East | 362500 | 12.795529 |

As `minX, minY, maxX, maxY`:

- EPSG:3006: `355000, 6207500, 362500, 6220000`
- EPSG:4326 (lon/lat): `12.668632, 55.990849, 12.795529, 56.105284`

## Corners

| Corner | Easting (m) | Northing (m) | Latitude | Longitude |
|---|---|---|---|---|
| SW | 355000 | 6207500 | 55.990849 | 12.675394 |
| SE | 362500 | 6207500 | 55.993056 | 12.795529 |
| NE | 362500 | 6220000 | 56.105284 | 12.789116 |
| NW | 355000 | 6220000 | 56.103067 | 12.668632 |

- The SW corner is the Unity origin in `configs/cities/helsingborg.json` (`unity.originInSourceCRS`).
- The area is 7.5 km E–W × 12.5 km N–S.
- The rectangle is exact in EPSG:3006. In WGS84 it is slightly tilted, so the lat/lon box above (min/max of the corners) is a little larger than the area. Downloads by lat/lon get a few hundred metres of extra data at the edges; nothing is missed.

## Where it comes from

- **Source:** the footprints of the 15 *Laserdata NH* tiles (Lantmäteriet), read from the `*_tile.json` files in `Raw_data/1-Helsingborig/laserdata_nh_helsingborg/`.
- **Tile grid:** 3 × 5 tiles of 2.5 km. Together they form a full rectangle with no gaps.
- **Tile names:** names like `10A007_620_35_7550` encode the SW corner (N 6207500, E 355000). Portals that offer 2.5 km tiles will line up exactly with this box.
- **WGS84 conversion:** `pyproj` (`Transformer.from_crs(3006, 4326, always_xy=True)`), run in the `digitaltwin` env on 2026-10-13.

## Checks

- **Processed buildings:** all 43,981 buildings in `Processed_data/Helsingborg_Final/lidar_heights/buildings_lidar_added.gpkg` lie inside the box (355285–361855 E, 6207503–6218191 N).
- **SCB statistics:** the SCB grid rectangle is a different area. Of the 92 voting districts, 16 lie outside it and 17 only partly inside (see `2026-10-07_helsingborg_election_2026_profiling.md`). Check a statistics layer's own extent before assuming it matches this box.

## Reproduce

Run this from the repo root with the `digitaltwin` env:

```python
import json, glob
from pyproj import Transformer
fs = glob.glob("Raw_data/1-Helsingborig/laserdata_nh_helsingborg/*_tile.json")
pts = [p for f in fs for p in json.load(open(f))["features"][0]["geometry"]["coordinates"][0]]
xs, ys = [p[0] for p in pts], [p[1] for p in pts]
print(min(xs), min(ys), max(xs), max(ys))  # 355000 6207500 362500 6220000
t = Transformer.from_crs(3006, 4326, always_xy=True)
for x, y in [(min(xs), min(ys)), (max(xs), min(ys)), (max(xs), max(ys)), (min(xs), max(ys))]:
    print(x, y, t.transform(x, y))
```
