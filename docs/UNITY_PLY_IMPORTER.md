# Unity PLY Importer — loading the city

Loads the mesh pipeline's PLY output into Unity **at real-world coordinates**.

Since the 2026-08-03 export refactor, PLY is the primary mesh format written by
`Src/mesh_generation/builder.py`. This is the Unity-side counterpart, so the twin can
be rebuilt from pipeline output with no manual conversion step.

---

## How to use it

### 1. Check the run first (no scene changes)

**City Digital Twin → Inspect Mesh Run (no scene changes)...**

Pick the run's `meshes_manifest.json`. It parses every PLY and reports group count,
vertices, triangles, CRS, and the placed extent in km. Nothing is added to the scene.

### 2. Load the city

**City Digital Twin → Add City Mesh Loader to Scene**

Pick the same `meshes_manifest.json`. This adds a configured `CityMeshLoader` to the
scene. Then either:

- press **Load Now** on the component (works in edit mode), or
- press **Play** with *Load On Start* ticked.

Set **Max Groups** to ~50 for a first look — the 500 m grid run is 2211 cells.

### From code

```csharp
var go = new GameObject("City");
var city = go.AddComponent<CityMeshLoader>();
city.manifestPath = "../../Processed_data/Gothenburg/building_meshes_grid_500m/meshes_manifest.json";
city.combineMeshes = true;     // 2211 draw calls -> 11
city.maxGroups = 0;            // 0 = load everything
StartCoroutine(city.LoadCityAsync());
```

Reading a folder **without** rendering it — useful for validating a pipeline run:

```csharp
var result = PlyFolderImporter.ImportFolder(
    "../../Processed_data/Gothenburg/building_meshes_grid_500m/mesh_files");
PlyFolderImporter.LogReport(result);      // counts, failures, timing
var byId = result.ByGroupId();            // "grid_000_018" -> parsed mesh
```

Paths are relative to the **Unity project root** (`Unity/City_Digital_Twin/`, the
folder containing `Assets/`), so they stay portable. Absolute paths also work.

---

## Why the manifest is mandatory

Each PLY is written in a **local frame**: the exporter subtracts that group's own
origin from X and Y, so every file starts at (0,0). Load 2211 grid cells raw and they
all pile up on the world origin.

The real coordinates exist **only** in `meshes_manifest.json`, one `origin` per group:

```
scene.x = origin.x - sceneOrigin.x        // easting  -> Unity X
scene.z = origin.y - sceneOrigin.y        // northing -> Unity Z
scene.y = 0                               // heights are already inside the mesh
```

### Two rules that are easy to get wrong

**Do not add `origin.z`.** The exporter rebases X and Y only — `builder.py` subtracts
`origin[0]`/`origin[1]` and leaves Z as an **absolute** height. The manifest still
reports `origin.z` (0.5 m), but it was never subtracted, so adding it back lifts the
whole city. Confirmed on the 500 m grid run: mesh-local z-min == `origin.z` == 0.50.

**Do not place at raw SWEREF99 coordinates.** They are ~300 000 E / 6 400 000 N, and
float32 carries ~7 significant digits. Measured at northing 6 392 898, consecutive
float values are **0.5 m apart** — vertices visibly snap and surfaces z-fight.
Rebasing on the run's south-west corner brings that to **0.98 mm**.
`rebaseOnSouthWestCorner` is on by default; `SceneToCrs()` / `CrsToScene()` convert
back for analytics.

### Grid-specific note

The grid manifest's `origin` is the **content bounding box, not the 500 m cell
corner** — zero of 200 sampled origins land on a 500 m multiple, and content spans
reach 871 m because buildings straddling a border are assigned whole. So placement
must use `origin` directly, never a computed grid index. Cells legitimately overlap.

---

## Files

| File | Role |
|---|---|
| [PlyParser.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/PlyParser.cs) | Format reader. No Unity API calls, so it runs off the main thread. |
| [PlyMeshData.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/PlyMeshData.cs) | Parse result + `PlyImportSettings`. |
| [PlyMeshFactory.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/PlyMeshFactory.cs) | Builds a `Mesh`. Main thread only. |
| [PlyFolderImporter.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/PlyFolderImporter.cs) | Bulk-parses every PLY in a folder, in parallel. No scene involvement. |
| [MeshManifest.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/MeshManifest.cs) | Reads `meshes_manifest.json` — the only source of real-world coordinates. |
| [CityMeshLoader.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/CityMeshLoader.cs) | Loads a whole run into the scene, georeferenced, with mesh combining. |
| [ProjectPaths.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/ProjectPaths.cs) | Resolves paths that point outside the Unity project. |
| [Editor/CityLoaderMenu.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/Editor/CityLoaderMenu.cs) | The **City Digital Twin** menu. |
| [Editor/CityMeshLoaderInspector.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/Editor/CityMeshLoaderInspector.cs) | Load / Clear buttons, so loading works in edit mode. |
| [Editor/PlyScriptedImporter.cs](../Unity/City_Digital_Twin/Assets/Scripts/IO/Editor/PlyScriptedImporter.cs) | Imports `.ply` files placed inside `Assets/`. **Not georeferenced** — see below. |

Assembly definitions keep the editor code out of player builds.

### About `PlyScriptedImporter`

It makes any `.ply` **inside `Assets/`** a native mesh asset. It imports each file in
its own local frame and does **no** placement, so it is only for one-off meshes that
need to ship inside a build. It never sees `Processed_data/` — Unity does not scan
outside the project. For pipeline output, use `CityMeshLoader`.

---

## Performance

`combineMeshes` (default **on**) merges groups into ~262k-vertex chunks. At grid
scale that is the difference between 2211 draw calls and **11**.

Turn it off when each cell must be individually selectable. Individual mode attaches
a `CityMeshGroup` component to every group, carrying `groupId` and the group's
easting/northing for picking and interaction logging.

`maxGroups` and `radiusFilterMeters` limit what loads while iterating.

`addMeshColliders` is off by default — collider baking is expensive at city scale.
Turn it on only when picking is needed.

Unity's default 16-bit index buffer caps at 65 535 vertices. No individual grid cell
exceeds that (max 21 462), but merged chunks do, so `IndexFormat.UInt32` is selected
from the actual vertex count.

---

## Coordinate frame

The pipeline writes a local, origin-rebased frame: `X = easting - origin.x`,
`Y = northing - origin.y`, `Z = height in metres`. That frame is **Z-up,
right-handed** (EPSG:3006 metres). Unity is **Y-up, left-handed**. The conversion is
a single axis swap:

```
(x, y, z)  ->  (x, z, y)
```

**Triangle winding is deliberately left alone.** Swapping Y and Z is itself a
handedness flip: it turns the source's right-handed / counter-clockwise-front
convention directly into Unity's left-handed / clockwise-front one. Reversing the
index order *as well* would flip it back and render every building inside-out —
invisible under backface culling. Verified numerically: with the swap alone, 99.3% of
face normals agree with the exported per-vertex normals; with an added winding
reversal, 99.3% *disagree*.

`PlyImportSettings.FlipWinding` is an escape hatch for third-party PLYs using the
opposite convention. Leave it **off** for this project's meshes.

---

## Supported formats

- `ascii`, `binary_little_endian`, `binary_big_endian`
- Any property order; unneeded properties (colour, confidence, intensity) are skipped
  by declared type
- All PLY scalar types (`char`…`double`), so `int` and `uint` face indices both work
- N-gon faces, fan-triangulated (assumes convex faces — true for extruded footprints)
- Elements other than `vertex` / `face` are skipped correctly, including
  variable-length ones
- Files with no normals → normals recalculated
- Files with no faces → imported as a point cloud rather than an empty mesh

**Not supported:** list properties on the `vertex` element (fails loudly).

---

## Validation

Verified by compiling against Unity API stubs and running on real pipeline output.

**500 m grid run (`building_meshes_grid_500m`, 2211 cells):**

| | |
|---|---|
| Parse all 2211 files | **382 ms** (parallel), 2 668 448 verts, 0 failures |
| Manifest filename resolution | 2211/2211 |
| Placed extent | 30.8 × 29.4 km |
| Draw calls | 2211 individual → **11** combined |
| Largest combined mesh | 261 964 verts (UInt32, handled) |
| Scene ↔ CRS round-trip error | **0.097 cm** |
| Per-cell vertices | median 598, max 21 462 (none need UInt32) |

**Parser, single files:**

| File | Verts | Tris | Result |
|---|---|---|---|
| `district_002_lod1.ply` | 12 700 | 20 588 | 99.5% outward faces, 31 ms |
| `district_000_lod1.ply` | 237 594 | 408 500 | 97.6% outward, UInt32, 89 ms |
| pyvista `ant/airplane/nut/sphere.ply` | — | — | parse OK (no-normals + `int` index paths) |

Counts match the PLY headers exactly, and byte-walking consumes each file body with
**zero leftover bytes**, confirming the record stride and face-record walk are right.

Edge cases exercised: ASCII with a quad, interleaved colour properties, big-endian,
an unknown `meta` element with mixed int/double, a corrupt out-of-range face index
(warns once, drops the face), a truncated file, and a non-PLY file (both fail with
clear messages naming byte counts).

### Known limitation — decimated LODs

Decimated LOD3 meshes show ~90% outward-normal agreement versus ~99% for full-detail
LOD1, because vertex reduction moves vertices **without recomputing normals**.
Inward-facing faces stay at ~4%, so winding is correct; the rest is near-degenerate
triangles where the comparison is noise. Enable `RecalculateNormals` if decimated LODs
look wrongly shaded. This is a property of the exporter's LOD path, not an importer
bug.

---

## Failure cases

| Symptom | Cause |
|---|---|
| Everything stacked at the origin | Loaded PLYs without the manifest. Placement comes from `meshes_manifest.json`. |
| Whole city floats 0.5 m up | `origin.z` was added. It must not be — Z is already absolute. |
| Vertices snap / surfaces z-fight | Placed at raw SWEREF99 coordinates. Keep `rebaseOnSouthWestCorner` on. |
| Nothing visible, but the Console reports success | Camera. The city is ~30 km across — select the loader and press **F**; raise the camera far clip plane. |
| Buildings invisible / see-through from outside | Winding flipped. `FlipWinding` should be **off**. |
| Mesh truncated at ~65k vertices | `IndexFormat.UInt32` not applied — should be automatic. |
| Buildings lying flat | `ConvertAxes` disabled on a Z-up file. |
| `.ply` in `Processed_data/` never imports as an asset | Expected. Unity only imports inside `Assets/`. Use `CityMeshLoader`. |
| Raycast/picking returns nothing | No `MeshCollider`, or `combineMeshes` merged the cell you wanted to pick. |

---

## Assumptions

- Source units are metres; `Scale = 1.0` keeps 1 Unity unit = 1 metre.
- Faces are convex (fan triangulation).
- CRS and origin live in the manifest, not the PLY; the importer performs **no**
  re-projection.
