import numpy as np
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))
from shapely.geometry import Polygon
from mesh_generation.builder import MeshBuilder, check_watertight

b = MeshBuilder()


def report(name, poly):
    v, f = b.polygon_to_triangles(poly, 5.0)
    r = check_watertight(v, f)
    print(f"{name}: verts={len(v)} faces={len(f)} -> {r}")
    return r


# Closed solids: must be watertight
assert report("SQUARE", Polygon([(0, 0), (10, 0), (10, 10), (0, 10)]))["is_watertight"]
assert report("L-SHAPE", Polygon([(0, 0), (10, 0), (10, 4), (4, 4), (4, 10), (0, 10)]))["is_watertight"]
assert report("U-SHAPE", Polygon([(0, 0), (10, 0), (10, 10), (7, 10), (7, 3), (3, 3), (3, 10), (0, 10)]))["is_watertight"]
assert report("DONUT", Polygon([(0, 0), (20, 0), (20, 20), (0, 20)],
                               [[(6, 6), (6, 14), (14, 14), (14, 6)]]))["is_watertight"]

# Degenerate footprint (2 unique points) must be skipped -> empty mesh, not an open shell
v, f = b.polygon_to_triangles(Polygon([(0, 0), (10, 0), (0, 0)]), 5.0)
print(f"DEGENERATE: verts={len(v)} faces={len(f)} (expect 0,0)")
assert len(f) == 0, "degenerate footprint should produce no faces"

# Multi-building group: two separate closed boxes, combined -> each shell watertight
v1, f1 = b.polygon_to_triangles(Polygon([(0, 0), (10, 0), (10, 10), (0, 10)]), 5.0)
v2, f2 = b.polygon_to_triangles(Polygon([(20, 0), (30, 0), (30, 10), (20, 10)]), 5.0)
vc = np.vstack([v1, v2])
fc = np.vstack([f1, f2 + len(v1)])
r = check_watertight(vc, fc)
print(f"TWO BOXES combined: {r}")
assert r["is_watertight"], "two disjoint closed boxes should still be watertight"

# Negative control: an open box (roof faces removed) must NOT be watertight
v, f = b.polygon_to_triangles(Polygon([(0, 0), (10, 0), (10, 10), (0, 10)]), 5.0)
# remove the 2 roof cap faces (they are the ones with all z == z_roof)
z_roof = b.terrain_offset_m + 5.0
keep = [i for i, face in enumerate(f) if not np.allclose(v[face][:, 2], z_roof)]
f_open = f[keep]
r = check_watertight(v, f_open)
print(f"OPEN BOX (roof removed): {r}")
assert not r["is_watertight"], "open box must be flagged non-watertight"
assert r["boundary_edges"] > 0

print("\nALL WATERTIGHT CHECKS PASSED")
