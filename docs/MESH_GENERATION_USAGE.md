# Mesh Generation — Usage Guide

How to turn processed building footprints (with heights) into 3D meshes.

Entry point: [Src/Scripts/run_mesh_generation.py](../Src/Scripts/run_mesh_generation.py)
Core code: [Src/mesh_generation/](../Src/mesh_generation/)

---

## What it does

Reads a GeoPackage of building footprints, extrudes each footprint into a 3D prism
(walls + flat roof) using its `height_m`, groups buildings into tiles using a chosen
**strategy**, and writes one mesh file per group.

- **Default output format: PLY only** (binary Stanford PLY, with per-vertex normals).
- **Default detail: full** — no vertex reduction. One full-detail mesh (LOD1) per group.
- Other formats (GLB, OBJ) and reduced-detail LODs are **opt-in** (see flags below).

Output lands in `Processed_data/building_meshes/` by default:

```
Processed_data/building_meshes/
├─ mesh_files/                 # the actual mesh files, one (or more) per group
│  ├─ grid_000_000_lod1.ply
│  ├─ grid_000_001_lod1.ply
│  └─ ...
└─ meshes_manifest.json        # index: every group, its files, bounds, CRS, origin, IDs
```

---

## Prerequisites

1. **Python env**: use the `digitaltwin` conda env.
   ```
   C:\Users\imana\miniforge3\envs\digitaltwin\python.exe
   ```
2. **Input data**: a processed buildings GeoPackage with `object_id`, `geometry`, and
   (ideally) `height_m`. The script auto-picks the best available input, in this order:
   1. `Processed_data/buildings_lidar_added.gpkg`  (LiDAR-enriched heights — preferred)
   2. `Processed_data/buildings_with_heights.gpkg`  (legacy name)
   3. `Processed_data/buildings_processed.gpkg`     (no heights → falls back to 10 m)

   Buildings with no `height_m` use the assumed default height (10 m).

Run all commands **from the project root**:
`w:\Investigating Usability of Immersive Analytics in an Urban Digital Twin\Portotype`

---

## Simple run

Default strategy is `district`, default format is PLY, full detail:

```
python Src/Scripts/run_mesh_generation.py
```

On Windows with the explicit env python (recommended, avoids env issues):

```
C:\Users\imana\miniforge3\envs\digitaltwin\python.exe Src\Scripts\run_mesh_generation.py
```

That's the whole "just run it" case. It loads the best available buildings file,
tiles them by district, and writes `.ply` files + a manifest to
`Processed_data/building_meshes/`.

---

## Common runs

Regular 1 km grid tiles (predictable, good for streaming):

```
python Src/Scripts/run_mesh_generation.py --strategy grid
```

Smaller 250 m grid tiles:

```
python Src/Scripts/run_mesh_generation.py --strategy grid --cell-size 250
```

Also write GLB alongside PLY (e.g. for a glTF viewer):

```
python Src/Scripts/run_mesh_generation.py --formats ply,glb
```

Full detail **plus** reduced-vertex LOD2/LOD3 (for LOD streaming):

```
python Src/Scripts/run_mesh_generation.py --reduce-vertices
```

Point at a specific input file and output folder:

```
python Src/Scripts/run_mesh_generation.py ^
  --input Processed_data/buildings_lidar_added.gpkg ^
  --output Processed_data/building_meshes_v2
```

(`^` is the Windows CMD line-continuation; drop it and put it all on one line if you prefer.)

---

## All options

| Flag | Default | Meaning |
|------|---------|---------|
| `--input PATH` | auto (see above) | Input buildings GeoPackage. |
| `--output DIR` | `Processed_data/building_meshes` | Output directory. |
| `--strategy {individual,district,grid,quadtree}` | `district` | How buildings are grouped into meshes. |
| `--cell-size N` | `1000` | Grid cell size in metres (square N×N cells). **Grid strategy only.** |
| `--max-buildings N` | `500` | Max buildings per cell before the quadtree subdivides. **Quadtree only.** |
| `--formats a,b,c` | `ply` | Comma-separated export formats, **primary first**. Supported: `ply`, `glb`, `obj`. The first one is authoritative — its export success is what "counts". |
| `--reduce-vertices` | off | Also emit decimated LOD2 (~50%) and LOD3 (~10%) meshes. Off = full detail only. |
| `--profile` | off | Also write a data profile (`byggnad_profile.md`) of the input. |

### Format notes
- `--formats ply` → only `*.ply` (default).
- `--formats ply,glb` → both; PLY is primary/gating, GLB is written alongside. A GLB
  failure is logged but does **not** fail the mesh.
- `--formats glb` → GLB becomes primary; no PLY written.
- The **first** format listed always drives success. Put the one you care about first.

---

## Strategies — which to pick

| Strategy | Groups | Best for | Gotcha |
|----------|--------|----------|--------|
| `individual` | one mesh per building | per-building picking / inspection | huge file count for a full city |
| `district` (default) | ~50–100 | fast loading, natural boundaries | uses **synthetic 5 km grid** if no district layer; **skips any cell with < 50 buildings** — a tiny test set can produce *zero* groups. Use `grid` for small samples. |
| `grid` | tile-based | streaming, caching, any city | fixed cells may cut across natural boundaries |
| `quadtree` | adaptive | variable density (dense downtown + sparse edges) | variable mesh sizes, harder to reason about |

> **Testing tip:** on a handful of buildings, use `--strategy grid`. The `district`
> strategy's 50-building minimum will otherwise silently skip everything.

---

## Reading the output

`meshes_manifest.json` is the index. Per group it records:

- `group_id`, `group_name`
- `primary_format` — e.g. `"ply"`
- `mesh_files` — authoritative map, `{lod: {format: filename}}`
  (e.g. `{"lod1": {"ply": "grid_000_000_lod1.ply"}}`)
- `glb_files` / `ply_files` — convenience views (each empty if that format wasn't written)
- `lod_levels` — e.g. `["lod1"]` (full detail only) or `["lod1","lod2","lod3"]` (with `--reduce-vertices`)
- `crs` (`EPSG:3006`) and `origin` (x, y, z) — meshes are **rebased to a local origin**;
  add the origin back to place them in EPSG:3006
- `bounds_epsg3006` — the group's real-world extent
- `building_ids` — the `object_id`s in that group (IDs are preserved)
- `height_sources` — count of where each building's height came from
- `triangle_count`, `vertex_count`, `file_size_mb`

The console also prints a summary (strategy, total buildings, groups, vertices, triangles).

---

## Verifying it worked

1. Check the run summary printed at the end (groups > 0, failures = 0).
2. Confirm files exist:
   ```
   dir Processed_data\building_meshes\mesh_files
   ```
3. Open a `.ply` in MeshLab / CloudCompare / Blender, or a `.glb` (if exported) in any
   glTF viewer. Buildings should sit near the origin (they're origin-rebased) at plausible
   heights.
4. Run the mesh test suite:
   ```
   C:\Users\imana\miniforge3\envs\digitaltwin\python.exe tests\test_mesh_robustness.py
   ```

> **Note:** [Src/Scripts/validate_meshes.py](../Src/Scripts/validate_meshes.py) validates
> **GLB files only**. It's useful only when you exported GLB (`--formats ...,glb`); it does
> not inspect the default PLY output.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `No buildings file found` | none of the expected inputs exist | run the buildings / LiDAR pipeline first, or pass `--input` |
| 0 groups created | `district` strategy on too-small a set (< 50 buildings/cell) | use `--strategy grid` |
| All heights look like 10 m | input has no `height_m` | run the LiDAR height pipeline to enrich buildings first |
| `mapbox_earcut not installed` warning | dependency missing | `pip install mapbox_earcut` — otherwise concave/holed roofs are approximated |
| Slow / large output for full city | `individual` strategy or tiny grid cells | use `district`/`grid` with a larger `--cell-size` |

---

## Programmatic use

If you'd rather call it from Python instead of the CLI:

```python
import geopandas as gpd
from mesh_generation.generator import MeshGenerator, GeneratorConfig

buildings = gpd.read_file("Processed_data/buildings_lidar_added.gpkg")

config = GeneratorConfig(
    strategy="grid",
    strategy_config={"cell_size_m": 500},
    export_formats=("ply",),   # first = primary/gating; add "glb" if needed
    generate_lods=False,       # True = also emit reduced LOD2/LOD3
    crs="EPSG:3006",
)

report = MeshGenerator(buildings, config).generate("Processed_data/building_meshes")
print(report["total_groups"], "groups,", report["total_triangles"], "triangles")
```

See [Src/mesh_generation/generator.py](../Src/mesh_generation/generator.py) for the full
`GeneratorConfig` field list.
