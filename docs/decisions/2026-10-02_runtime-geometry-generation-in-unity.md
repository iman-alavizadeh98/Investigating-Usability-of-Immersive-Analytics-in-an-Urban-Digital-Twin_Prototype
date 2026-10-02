# Decision: generate city geometry at runtime in Unity, not as pre-built meshes

**Date recorded:** 2026-10-02 (the decision was taken earlier during the Unity
work; the exact date was not recorded)
**Status:** Adopted
**Affects:** `Src/mesh_generation/` + `Src/Scripts/run_mesh_generation.py`
(discarded, kept), `Unity/.../Scripts/IO/` (discarded, kept),
`Unity/.../Scripts/UrbanAnalytics/` (current runtime)

## Context

The first approach built 3D building meshes offline in Python
(`Src/mesh_generation/`, strategy-based grouping, PLY primary) and imported them
into Unity with `IO/CityMeshLoader` + `PlyParser`, placed from
`meshes_manifest.json`.

In practice the pre-built meshes were **too heavy to import and to work with**.
For scale, the last full grid run was 2,160 cells / 2.50M vertices / ~110 MB of
PLY, before any analytical layer was added. Pre-baked geometry is also frozen: an
extrusion height or colour driven by data cannot change without regenerating
files, which works against a visualization system whose encodings change at
runtime.

## Decision

Ship **compact source data** to Unity and **generate geometry at runtime**:

- analytical polygons (e.g. SCB `Ruta` 250 m cells) are read from
  `StreamingAssets/spatial_layers/<id>/geometry.json` and triangulated in Unity
  (`GeometryManager`, `ProceduralPolygonMeshBuilder`, `PolygonTriangulator`);
- buildings are read from a footprint + height binary
  (`buildings_runtime_ruta.bin`, `GBLD` v2, ~14.5 MB for 201,594 buildings) and
  extruded in Unity by `UrbanContextManager`;
- geometry is batched into chunks (200 Ruta units / 750 buildings per chunk) that
  keep per-entity semantic ranges, so data-driven colour/height can be applied
  per entity without one GameObject per entity.

## Alternatives considered

**Keep pre-built meshes, optimise them** (decimation via `--reduce-vertices`,
coarser grouping strategies). Reduces size, but geometry stays static and the
import step stays heavy.

**Pre-built meshes as Unity assets** (`PlyScriptedImporter`). Moves the cost
into the editor/build and would still embed large meshes in the project, which
the 2026-08-26 version-control policy forbids.

## Consequences

- `Src/mesh_generation/`, `run_mesh_generation.py`, `validate_meshes.py`,
  `Unity/.../Scripts/IO/` and `docs/UNITY_PLY_IMPORTER.md` are **kept as a
  documented failed attempt**, not deleted. Do not extend them for the runtime.
- Python's role toward Unity becomes **exporting runtime packages** (JSON layers,
  building binary), e.g. `Src/Scripts/Unity/export_polygon_spatial_layer.py` and
  `export_data_layer.py`.
- Triangulation correctness now lives in C# and is not covered by the Python
  test suite.
- The building binary writer is not in the repository yet; it belongs to the
  planned data-pipeline rebuild. Current runtime data is test data.
