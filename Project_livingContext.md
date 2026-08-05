# Project Living Context

This document is the working reference for future AI agents. Update it incrementally as the project changes. The goal is fast orientation: what is canonical now, what is legacy, and where a future change should be made without reading the whole repository.

## Project Snapshot

This repository is a Gothenburg urban digital twin prototype focused on Swedish geospatial data, buildings, preprocessing pipelines, mesh generation, and Unity-ready analytical assets.

The current project direction is pipeline-first. The modular architecture is now the main source of truth for how the repo is structured, while the older phase-based documents are still useful as historical snapshots of how the work evolved.

The main intent is to preserve Swedish source semantics while creating processed English-facing outputs for development, analytics, and runtime use. The key interaction pattern remains building-level spatial lookup: a building can later be linked to demographic, geographic, and metadata layers.

## Current Status

The current work is centered on the modular pipeline system and its downstream mesh and analytics workflows, with a new focus on LOD1 input enrichment via LiDAR height estimation and strategy-based mesh generation.

Current status:
- the repo now has a modular pipeline architecture;
- the buildings dataset pipeline is the main concrete dataset implementation;
- Swedish fields are translated into English aliases in processed data;
- validation and profiling are part of the workflow;
- an optional postprocess snapshot step can be generated for deduplicated outputs;
- a new LiDAR height estimation pipeline enriches buildings with accurate heights from laserdata_nh/ LAZ tiles (NEW);
- mesh generation exists as a separate layer from schema standardization;
- spatial joins and analytics payload preparation are part of the downstream chain;
- legacy phase docs still exist, but they should be treated as older snapshots unless a newer code/doc update says otherwise.

Important note:
- if phase notes, handoff docs, and code disagree, use the newest dated source or the current code path first;
- the older phase labels such as Phase 2, Phase 3, and Phase 4 are now historical framing, not the primary architecture language.

## Canonical Files

Check these first when updating context or reasoning about the project:

- [docs/MODULAR_ARCHITECTURE.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/MODULAR_ARCHITECTURE.md)
- [CLAUDE.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/CLAUDE.md)
- [Project_livingContext.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Project_livingContext.md)
- [docs/BUILDINGS_PIPELINE.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/BUILDINGS_PIPELINE.md)
- [docs/MESH_GENERATION_USAGE.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/MESH_GENERATION_USAGE.md) (NEW) — run-oriented usage guide for mesh generation
- [Src/pipelines/buildings/pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/buildings/pipeline.py)
- [Src/pipelines/buildings/config.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/buildings/config.py)
- [Src/pipelines/lidar_heights/pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/pipeline.py) (NEW)
- [Src/pipelines/lidar_heights/config.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/config.py) (NEW)
- [Src/pipelines/lidar_heights/height_estimation.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/height_estimation.py) (NEW)
- [Src/pipelines/lidar_heights/pdal_pipelines.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/pdal_pipelines.py) (NEW)
- [Src/pipelines/lidar_heights/tile_index.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/tile_index.py) (NEW)
- [Src/pipelines/lidar_heights/export.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/export.py) (NEW)
- [Src/mesh_generation/generator.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/generator.py)
- [Src/mesh_generation/builder.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/builder.py)
- [Src/mesh_generation/strategies/base.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/strategies/base.py) (NEW)
- [Src/mesh_generation/strategies/individual.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/strategies/individual.py) (NEW)
- [Src/mesh_generation/strategies/district.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/strategies/district.py) (NEW)
- [Src/mesh_generation/strategies/grid.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/strategies/grid.py) (NEW)
- [Src/mesh_generation/strategies/quadtree.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/strategies/quadtree.py) (NEW)
- [Src/Scripts/run_buildings_pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_buildings_pipeline.py)
- [Src/Scripts/run_lidar_height_pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_lidar_height_pipeline.py) (NEW)
- [Src/Scripts/run_mesh_generation.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_mesh_generation.py)
- [Src/Scripts/preprocess_spatial_joins.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/preprocess_spatial_joins.py)
- [Src/Scripts/validate_meshes.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/validate_meshes.py)
- [docs/UNITY_PLY_IMPORTER.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/UNITY_PLY_IMPORTER.md) (NEW) — Unity-side PLY import: coordinate frame, winding, 32-bit indices, failure cases
- [Src/mesh_generation/grid_reference.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/grid_reference.py) (NEW) — **the frozen 500 m lattice.** Single definition shared by mesh strategies and attribute layers; nothing else may define it
- [Src/pipelines/cell_attributes/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/cell_attributes) (NEW) — generic SCB Ruta → cell aggregation; add a layer via `config.LAYER_REGISTRY`, not code
- [docs/decisions/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/decisions) (NEW) — why ownership, anchor, cell-not-building, and reconciliation are as they are
- [Unity/City_Digital_Twin/Assets/Scripts/IO/CityMeshLoader.cs](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Unity/City_Digital_Twin/Assets/Scripts/IO/CityMeshLoader.cs) (NEW) — **the** way to load a mesh run into Unity, georeferenced from the manifest
- [Unity/City_Digital_Twin/Assets/Scripts/IO/PlyParser.cs](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Unity/City_Digital_Twin/Assets/Scripts/IO/PlyParser.cs) (NEW) — reads the pipeline's primary mesh format into Unity
- [docs/PHASE4_FINAL_HANDOFF.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/PHASE4_FINAL_HANDOFF.md)
- [PHASE2_3_4_COMPLETION_SUMMARY.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/PHASE2_3_4_COMPLETION_SUMMARY.md)
- [docs/data-analysis/2026-04-23_dataset_inventory.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/data-analysis/2026-04-23_dataset_inventory.md)
- [docs/data-analysis/2026-04-23_integration_strategy.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/data-analysis/2026-04-23_integration_strategy.md)

## Architecture

The repository is organized into layered workflows, but the modular architecture document is now the best high-level map of the current structure.

### Data Pipeline Layer

The data pipeline follows this sequence:
1. load raw geospatial data;
2. validate geometry, CRS, and required fields;
3. preprocess and translate Swedish fields and values;
4. export processed data and metadata;
5. optionally generate profile reports.

The shared pipeline contract is defined in [Src/pipelines/base.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/base.py). Dataset-specific logic lives in the buildings implementation.

### Buildings Dataset Layer

The active buildings workflow is implemented in [Src/pipelines/buildings/pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/buildings/pipeline.py).

Responsibilities:
- read the raw GeoPackage;
- preserve Swedish source fields on load;
- validate CRS, geometry validity, and required fields;
- translate selected fields to English aliases;
- derive standardized value columns where possible;
- export processed data, metadata, and summary statistics.

The canonical translation and classification tables are in [Src/pipelines/buildings/config.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/buildings/config.py).

### Profiling Layer

The profiling utility in [Src/utils/data_profiler.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/utils/data_profiler.py) generates readable summaries of DataFrames and GeoDataFrames.

Use it for:
- schema overview;
- null counts and percentages;
- sample rows;
- numeric statistics;
- categorical distributions;
- Markdown and HTML report exports.

### Mesh Generation Layer

Mesh generation is intentionally separated from the buildings data-prep pipeline.

The current mesh layer is implemented in:
- [Src/mesh_generation/generator.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/generator.py)
- [Src/mesh_generation/builder.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/builder.py)
- [Src/mesh_generation/strategies/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/strategies/)

It converts building footprints into 3D meshes and supports multiple grouping strategies. The strategy layer exists to trade off interaction fidelity against file count and load performance.

Mesh export supports multiple formats per LOD via `GeneratorConfig.export_formats` (default `("ply",)`, CLI `--formats`). **The FIRST format listed is the primary/authoritative output** — its export gates each LOD; any others are written alongside as extra artifacts whose failure never fails the LOD. Default is **PLY-only** (`export_ply()`, binary, with normals); add `glb`/`obj` to `--formats` if needed (e.g. `--formats ply,glb`). All formats for a group land together in the group's `mesh_files/` output dir. Manifests carry `primary_format`, a `mesh_files` map (`{lod: {fmt: filename}}`, authoritative), plus backward-compat `glb_files` / `ply_files` views (each empty when that format wasn't exported — so `glb_files` is empty in the default PLY-only run). `builder.export_mesh_suite()` is the current entry point (`export_glb_suite` is a kept alias). Vertex reduction (decimated LOD2/LOD3) is **off by default** (`generate_lods=False`, full-detail LOD1 only) and opted into with CLI `--reduce-vertices`.

Available strategies include:
- individual building meshes;
- district grouping;
- grid grouping;
- quadtree grouping.

### Spatial Join And Analytics Layer

The spatial join workflow in [Src/Scripts/preprocess_spatial_joins.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/preprocess_spatial_joins.py) maps buildings to demographic grid cells and prepares analytics payloads for interactive lookup.

Conceptual flow:
- load buildings within scope;
- load population or related grid layers;
- create building-to-cell mappings;
- aggregate demographic values by building;
- export JSON payloads for downstream use.

### LiDAR Height Estimation Pipeline (NEW)

The LiDAR height estimation pipeline in [Src/pipelines/lidar_heights/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/) enriches processed buildings with accurate height estimates derived from point cloud data.

**Purpose**: LOD1 input enrichment — add reliable `height_m` values to buildings so the mesh generator can produce accurate 3D models.

**Workflow**:
1. Load processed buildings (from buildings pipeline)
2. Validate CRS (EPSG:3006) and tile coverage
3. Batch-process by LiDAR tile:
   - Preprocess LAZ with PDAL: outlier removal → ground classification (SMRF) → height-above-ground
   - Extract per-building heights: compute ground_z (median), roof_hag (95th percentile), height_m
   - Classify quality (high/medium/low) based on point density and coverage
4. Export enriched buildings with height_m and quality flags

**Input**: 
- Processed buildings GeoPackage (must have `object_id` and geometry)
- LiDAR LAZ tiles from Raw_data/laserdata_nh/

**Output**:
- `Processed_data/buildings_lidar_added.gpkg` — enriched buildings with height_m, quality flags, point metadata
- `Processed_data/buildings_lidar_added.parquet` — same data in Parquet format
- `Processed_data/building_lidar_qc.csv` — QC subset for validation

**Key Design Constraints**:
- Preserves all building IDs (no filtering)
- Deduplicates buildings that appear on tile boundaries (keeps first occurrence)
- Uses 10m fallback height for buildings with insufficient LiDAR coverage
- Marks quality="low" when coverage is poor (enables downstream prioritization)
- Batch-processes by tile to manage memory (~500MB–1GB per tile)
- Uses PDAL for explicit preprocessing; every step is auditable
- All heights remain in EPSG:3006; coordinates unchanged
- Includes coverage metrics: `lidar_coverage_status` (good/partial/none) and `lidar_coverage_ratio`

**Usage**:
```bash
# Run with defaults
python Src/Scripts/run_lidar_height_pipeline.py

# Run validation tests
python Src/Scripts/run_lidar_height_pipeline.py --test

# Custom paths
python Src/Scripts/run_lidar_height_pipeline.py \
  --input Processed_data/buildings_processed.gpkg \
  --output-dir Processed_data \
  --lidar-dir Raw_data/laserdata_nh
```

**Configuration** is defined in [Src/pipelines/lidar_heights/config.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/config.py):
- `TileIndexConfig`: tile directory and CRS
- `PDALConfig`: outlier/ground classification/HAG parameters
- `HeightExtractionConfig`: min_points threshold, percentile, quality cutoffs
- `LiDARHeightPipelineConfig`: orchestration and reproducibility settings

Implementation notes:
- `height_estimation.py` contains the building height extraction logic
- `pdal_pipelines.py` contains the PDAL pipeline definitions
- `tile_index.py` handles LAZ tile indexing and lookup
- `export.py` handles the enriched dataset exports and QC outputs

**Validation**:
The pipeline includes embedded validation tests (--test mode) that check:
1. Synthetic ground truth (5m building → height ≈ 5.0m)
2. Fallback logic (insufficient points → low quality)
3. CRS preservation (output maintains EPSG:3006)
4. Building ID stability (no row count changes)
5. Determinism (re-runs produce identical heights)

### Legacy Code

The old phase-oriented scripts and handoff templates still exist and may still be useful for reference, but they are not the primary architecture language anymore.

Treat these as legacy or historical unless a newer doc or the current code explicitly says otherwise:
- [docs/phase2_progress.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/phase2_progress.md)
- [docs/phase4_handoff_template.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/phase4_handoff_template.md)
- [Src/Scripts/generate_building_meshes.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/generate_building_meshes.py)
- older references to `run_byggnad_pipeline.py`, `run_population_pipeline.py`, or `ByggnadsLPipeline`
- [Src/Scripts/legacy/preprocess_spatial_joins.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/legacy/preprocess_spatial_joins.py) — **deprecated 2026-08-05, do not run.** Gave every building in a cell that cell's FULL population, and lost 2,186 residents to `drop_duplicates(["Ruta"])`. Never ran successfully (its configured source path does not exist), so nothing downstream was contaminated. Kept for lineage. Replaced by `Src/pipelines/cell_attributes/` + `run_cell_attributes.py` (note: the new runner is `run_cell_attributes.py`, NOT the old deleted `run_population_pipeline.py` name listed above)

## Data Contracts And Assumptions

These assumptions should be changed only when code or newer handoff docs explicitly show a new convention.

### Spatial Reference

EPSG:3006 is the default spatial reference for the project. Any exception must be documented explicitly and converted deliberately.

### Analytical Grid Anchor (frozen — do not derive from data)

The 500 m analytical grid is the join key between mesh geometry and every statistical layer. Its anchor is **fixed** in [Src/mesh_generation/grid_reference.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/grid_reference.py) and must **never** be derived from a dataset's extent:

```
GRID_ANCHOR = (298000.0, 6383500.0)   EPSG:3006
DEFAULT_CELL_SIZE_M = 500
cell id = "grid_{col:+04d}_{row:+04d}"     signed, anchor-relative
```

- Multiple of both 500 and 1000, so every SCB `Ruta` cell size (100/250/500/1000 m) nests exactly — this is what makes attribute joins integer arithmetic rather than spatial interpolation.
- Cell intervals are **half-open**: a point on a boundary belongs to the cell above/right (`floor()` division). All strategies must break ties this way.
- Deriving the origin from `total_bounds` (the pre-2026-08-05 behaviour) makes cell ids mean different ground between runs. Same applies in Unity: `CityMeshLoader` must use the manifest's `grid_reference.anchor_*`, not the run's own SW corner, or interaction logs stop being comparable across sessions.

### Building ownership

Every building belongs to **exactly one** group, chosen by the cell containing its `representative_point()` (guaranteed inside concave/donut footprints, unlike a centroid). `MeshStrategy.validate()` enforces this and `generator.py` **raises** on violation. Never use `intersects` (duplicates straddlers) or `within` (drops them).

### Two different "origins" in a mesh manifest

Easy to confuse, and they mean different things:

- `origin` — the **content bbox corner** the mesh vertices were rebased on. This places the mesh; Unity uses it.
- `cell.origin_x/origin_y` — the **exact lattice corner**. This is the analytics join key.

Also note the exporter rebases X and Y only: **Z stays absolute**, so `origin.z` must never be added back during placement.

### Swedish Terminology

Raw and intermediate data should preserve Swedish names, spellings, and values whenever possible. English aliases are introduced for:
- developer readability;
- processed outputs;
- runtime labels;
- documentation clarity.

The project should not silently replace original source terminology in a way that loses traceability.

### Validation Order

Validation should happen before preprocessing or translation. If the source data is malformed, inconsistent, or missing critical fields, that should be visible before normalization.

### Building Heights

Height data may be incomplete. Downstream mesh logic may fall back to an assumed height when no reliable height field is present. If a better height source becomes available, that should be treated as a deliberate behavioral change.

### Analytical Grid Types

Ruta and DeSO are separate analytical concepts:
- Ruta is for regular spatial analysis and building-linked lookup;
- DeSO is for neighborhood-level statistical context;
- they should not be merged casually.

### Script Naming

If new scripts are added, prefer the pipeline naming pattern first. Old script names may remain for compatibility, but future additions should follow the modular architecture naming and folder structure.

## Outputs To Expect

Typical outputs from the buildings pipeline include:
- [Processed_data/buildings_processed.gpkg](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_processed.gpkg)
- [Processed_data/buildings_metadata.json](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_metadata.json)
- [Processed_data/buildings_summary.json](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_summary.json)
- [Processed_data/buildings_profile.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_profile.md)
- [Processed_data/buildings_profile.html](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_profile.html)

Optional outputs when `--postprocess` is enabled:
- postprocess snapshot data, reports, and profiles are written to a sibling dated folder with a `_postprocess` suffix.

Typical outputs from the LiDAR height pipeline include (NEW):
- [Processed_data/buildings_lidar_added.gpkg](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_lidar_added.gpkg)
- [Processed_data/buildings_lidar_added.parquet](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_lidar_added.parquet)
- [Processed_data/building_lidar_qc.csv](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/building_lidar_qc.csv)

Current mesh runs (Gothenburg, EPSG:3006, PLY primary — newest first):
- `Processed_data/Gothenburg/building_meshes_grid_500m_anchored/` (2026-08-05) — **CURRENT.** 500 m grid on the frozen lattice, **2,160 cells**, 2.50M verts / 4.20M tris / 110 MB. Zero duplicated buildings; every cell on the 500 m lattice; manifest carries `grid_reference` + per-group `cell`. This is what Unity's `CityMeshLoader` defaults to.
- `Processed_data/Gothenburg/building_meshes_grid_500m/` (2026-08-05) — **SUPERSEDED, kept for rollback.** Built on the `intersects` bug (9,690 duplicated buildings, 5.0% inflation) and an off-lattice origin. Do not use for analytics.
- `Processed_data/analytics/cell_attributes_500m/` (2026-08-05) — 10 SCB Ruta layers aggregated onto the same lattice; `.gpkg` / `.parquet` / `.json` (runtime, keyed by `cell_id`) / `_metadata.json` / `_qc.csv`.
- `Processed_data/analytics/building_to_cell_500m.parquet` — 201,594 buildings → owning cell, same rule as mesh partitioning.
- `Processed_data/Gothenburg/building_meshes_districts/` (2026-08-04) — 40 districts, 2.52M verts.
- `Processed_data/Gothenburg/building_meshes_whole_city/` (2026-08-04)

Each run holds `meshes_manifest.json` plus a `mesh_files/` folder. **The manifest is the only source of real-world coordinates** — PLY vertices are rebased to each group's own origin, so the meshes cannot be positioned without it.

Downstream outputs may include:
- [Processed_data/building_meshes/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/building_meshes) (pre-2026-08-03 layout: meshes sit in `glb_files/`)
- [Processed_data/spatial_joins/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/spatial_joins)
- [Processed_data/analytics/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/analytics)

## Working With The Buildings Pipeline

Use this pipeline when you need to:
- inspect the raw Byggnad dataset;
- verify field names and null coverage;
- translate Swedish schema into English developer-facing aliases;
- export a cleaned GeoPackage for downstream use;
- create a dataset profile for documentation or debugging.

Typical entrypoint:
- [Src/Scripts/run_buildings_pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_buildings_pipeline.py)

If the task is about mesh creation rather than schema analysis, the relevant entrypoint usually shifts to:
- [Src/Scripts/run_mesh_generation.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_mesh_generation.py)
- [Src/mesh_generation/generator.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/generator.py)

For a run-oriented usage walkthrough (simple `python ...` commands, flags, strategy choice, output/manifest layout, troubleshooting), see [docs/MESH_GENERATION_USAGE.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/MESH_GENERATION_USAGE.md).

## Dataset Coverage

The broader project includes more than buildings. The major raw data families referenced in the docs are:
- buildings;
- elevation and DEM;
- population;
- employment;
- income;
- place names;
- roads;
- property and land layers;
- terrain and topography;
- LiDAR;
- administrative and DeSO layers.

The dataset inventory in [docs/data-analysis/2026-04-23_dataset_inventory.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/data-analysis/2026-04-23_dataset_inventory.md) is the best quick map of what exists and what is intended for integration.

## Update Rules

When the project changes, update only the blocks that changed:
- Current Status;
- Canonical Files;
- Architecture;
- Data Contracts And Assumptions;
- Outputs To Expect;
- Working With The Buildings Pipeline;
- Changelog.

Do not rewrite the whole document unless the project structure changes significantly.

## Changelog

Keep this log short and dated. Record only changes that affect how future agents should reason about the project.

| Date | Change | Impact |
|------|--------|--------|
| 2026-05-12 | Reframed living context around modular architecture and legacy phase docs | Makes the current pipeline-first structure the primary source of truth |
| 2026-05-12 | Created living context section for the buildings pipeline | Gives future agents a stable, update-friendly reference |
| 2026-05-13 | Expanded DataFrameProfiler reporting and column meaning support | Produces richer HTML/Markdown profiles with duplicates and missing-value summaries |
| 2026-05-13 | Added optional postprocess snapshot outputs | Provides deduplicated outputs with a deletion report when requested |
| 2026-05-14 | Created LiDAR height estimation pipeline for LOD1 input enrichment | Buildings now enrich with height_m from laserdata_nh/ LAZ tiles; mesh generator can produce accurate 3D models |
| 2026-05-14 | Implemented mesh generation robustness improvements (Phase 3) | Mesh generator now tracks height sources, rebases coordinates locally, exports normals, preserves building IDs, and produces semantic-rich manifests with CRS/origin metadata |
| 2026-05-18 | Standardized living context for LiDAR and mesh submodules | Future agents can find the new pipeline and strategy module boundaries without inferring them from parent folders |
| 2026-05-18 | LiDAR pipeline robustness refactoring: 6 improvements | (1) Output naming: "buildings_lidar_added.*" (was "buildings_with_heights.*"); (2) Building validation: added coverage checks via validate_building_lidar_coverage(); (3) Per-building progress logging: improved visibility during extraction; (4) Border deduplication: _deduplicate_heights() removes tile-boundary duplicates; (5) File detection: run_mesh_generation.py checks for new filename first; (6) Coverage metrics: exported in QC CSV and enriched buildings with lidar_coverage_status/ratio |
| 2026-05-18 | LiDAR pipeline performance optimization: Phase 1 (PDAL caching) + Phase 2 (parallel processing) | (Phase 1) PDALCacheManager with MD5 hashing and config fingerprinting enables ~40-50% speedup on re-runs; (Phase 2) ProcessPoolExecutor-based tile parallelism enables ~2.5-3.5x speedup on first runs; both features configurable via CLI flags (--cache, --no-cache, --clear-cache, --parallel, --no-parallel, --max-workers) |
| 2026-06-24 | Mesh generation pipeline audit + 3 bug fixes | (1) strategies/base.py validate(): moved `buildings_per_group` append out of the inner per-building loop so it reports one count per group, not one per building; (2) lod_generation.py: fixed `Dict[str, any]` → `Dict[str, Any]` type hint and added `Any` import; (3) tests/test_mesh_robustness.py: updated test 5 to the current manifest schema (`lod_levels` list + `glb_files` dict, replacing stale `lod_level`/`glb_filename`). builder.py hole-roof index was reviewed and confirmed correct (commutative addition, not a bug). All 7 acceptance tests pass. |
| 2026-06-24 | Grid strategy configurable cell size (default 1000 m) + closed-cap mesh fix | (1) Grid `--strategy grid` produces square cell_size×cell_size m tiles; default cell size is **1000 m** in `strategies/grid.py` GridConfig.cell_size_m and `run_mesh_generation.py` `--cell-size`, override with `--cell-size N`. (Briefly set to 100 m earlier the same day, reverted to 1000 m as 100 m cells were too small.) (2) Roof/floor caps in `builder.py` rewrote fan triangulation → **mapbox_earcut** (`_triangulate_cap()`), which correctly closes concave footprints (L/U/T) AND respects interior holes/courtyards (fan triangulation sealed roofs over courtyards and could spill outside concave shapes); falls back to fan + one-time warning if mapbox_earcut is missing. Verified: square/L/U/donut caps match footprint area exactly, no roof-over-hole, no spill; robustness suite 7/7; full-city run (at 100 m) 43,981 buildings → 4,043 cells, 0 failures, 1.4M triangles. **New dependency: mapbox_earcut** (added to requirements). |
| 2026-06-24 | Rebuilt the Python environment | Both prior `conda_env` folders (`Portotype\conda_env`, `Prototype\conda_env`) had no scientific packages installed and were unusable. Created a fresh **conda env `digitaltwin`** (Python 3.11) from conda-forge with the full geospatial stack (geopandas 1.1.3, shapely 2.1.2, numpy 2.4.6, pyvista 0.48.4, rasterio, fiona, pyproj, laspy, pdal/python-pdal, scipy) plus pip-installed `pygltflib` and `mapbox_earcut`. Run pipelines with `C:\Users\imana\miniforge3\envs\digitaltwin\python.exe`. Code/git remain in `Portotype`. |
| 2026-07-17 | Created clean `beta` branch as a curated baseline off `main` | Rebuilt the branch file-by-file (not commit-by-commit) from the messy 29-commit `development`: brought over only the real code (`Src/`, `tests/`, `configs/`, `requirements.txt`), essential docs (`CLAUDE.md`, `docs/MODULAR_ARCHITECTURE.md`, `MESH_ROBUSTNESS_SUMMARY.md`, `LIDAR_PIPELINE_ROBUSTNESS_REFACTOR.md`), and this file; removed the `Unity/GISTesting` prototype; gitignored generated artifacts (`graphify-out/`, `validation_output.txt`). Dated `reports/` and other phase/summary docs were intentionally left on `development` (also preserved on `backup/development-pre-beta`). `beta` is 4 tidy commits on top of `main` and is intended to become main's basis via PR — do **not** merge `development` into `main` directly. |
| 2026-08-03 | Built a graphify knowledge graph of `Src/` | Ran `/graphify` scoped to `Src/` (38 Python files, ~27k words — the whole repo is too large due to `venv`/`conda_env`/Unity/GLB assets, so scoping to authored source is the right default). Code-only corpus → AST-only extraction, **0 token cost**. Result: 509 nodes, 896 edges, 28 communities in `graphify-out/` (`graph.html`, `GRAPH_REPORT.md`, `graph.json` — all already gitignored). God nodes / core abstractions: `LiDARHeightPipeline`, `MeshStrategy`, `MeshGroup`, `HeightEstimator`, `DataFrameProfiler`. Cross-community bridges: `BasePipeline` (betweenness 0.227) and `DataFrameProfiler` (0.202). Health check flagged 197 dangling-endpoint edges (references to external libs like `ndarray`, not real nodes) and ~95 collapsed duplicate `calls`/`references` edges — expected for AST extraction, graph is usable. Query the graph with `/graphify query "<question>"` instead of rebuilding. |
| 2026-08-03 | LiDAR height pipeline audit + 4 bug fixes | (1) **HAG masking bug (correctness)**: in `height_estimation._extract_height_for_building`, `hag_values` was sliced by the bounding-box mask (`points_in_bbox`) but then indexed with `non_ground_mask` (computed over the smaller polygon-filtered subset) — mismatched array lengths silently corrupted or crashed the HAG path on real tiles. HAG is now sliced identically to the other per-point arrays and filtered by the same `intersects` mask. (2) **Ground datum**: HAG path now reconstructs `ground_z` from `median(z - hag)` instead of relying on sparse ground points inside the footprint (which the building occludes); z-difference path keeps the old median/percentile ground estimate. (3) **height_source semantics**: was hardcoded to `lidar_hag_p95` even on the z-difference fallback; added `HeightSource.LIDAR_ZDIFF_P95` and the return dict now reports the method actually used. (4) **PDAL outlier removal**: `filters.outlier` only *flags* outliers (class 7); added a `filters.range` (`Classification![7:7]`) step to actually drop them before SMRF/HAG. Also fixed deprecated `datetime.utcnow()` → `datetime.now(timezone.utc)`. **Tests**: `--test` suite was silently broken (test 1 crashed on `laspy.ExtraDims` + unbound `temp_dir`, aborting the whole run); rewrote `test_synthetic_ground_truth` to use the current laspy API and a **non-degenerate footprint with points outside it** so it regression-guards the masking bug (400 tall exterior points must not leak in); fixed tests 1 & 2 to pass a GeoSeries row (not a dict) so `.geometry` access works. Tests 1, 2, 5 pass; 3 & 4 require `Processed_data/buildings_processed.gpkg` (absent in this checkout), unrelated to the fixes. |
| 2026-08-03 | Mesh export refactor: PLY-primary default + optional vertex reduction | (1) Default export is now **PLY-only, full detail**. `GeneratorConfig.export_formats` defaults to `("ply",)`; the **first** listed format is primary/gating (was hardcoded GLB). GLB/OBJ remain available via `--formats ply,glb`. (2) Vertex reduction (decimated LOD2/LOD3) is now **opt-in**: `GeneratorConfig.generate_lods` defaults to **False** (full-detail LOD1 only); CLI `--reduce-vertices` enables it. (3) `builder.export_glb_suite()` renamed to format-agnostic `export_mesh_suite()` (old name kept as alias); gating uses the primary format, secondary formats stay best-effort; helper `_export_one_format()` handles ply/glb/obj. (4) Output dir `glb_files/` → `mesh_files/`. (5) Manifests gained `primary_format` and an authoritative `mesh_files` (`{lod:{fmt:filename}}`) map; `glb_files`/`ply_files` kept as backward-compat views (empty when that format wasn't written). (6) `run_mesh_generation.py` `--formats` default `glb,ply`→`ply` (no longer force-includes glb) and added `--reduce-vertices`. Note: `validate_meshes.py` still validates GLB binaries only — it applies solely when GLB is explicitly requested, not to the default PLY output. test_mesh_robustness.py schema test updated; 8/8 pass; smoke-tested both default and `ply,glb + reduce` paths. |
| 2026-07-15 | Added PLY mesh export alongside GLB | `builder.py` gained `export_ply()` — writes Stanford PLY (default binary_little_endian, ascii optional) with per-vertex normals when available, implemented directly (no pygltflib dependency) so it works even where glTF libs are absent; coordinates use the same local origin-rebased frame as GLB (CRS/origin stay in the manifest). `export_glb_suite()` now takes `export_formats` and writes secondary formats (currently `ply`) alongside each LOD's GLB; **GLB stays the primary/authoritative output** — a PLY failure is logged but does not fail the LOD. `GeneratorConfig.export_formats` defaults to `("glb", "ply")`; `run_mesh_generation.py` exposes `--formats glb,ply` (glb is force-included). Manifests now carry a `ply_files` dict ({lod: filename}, empty when PLY disabled) mirroring `glb_files`. Validated: binary + ascii + no-normals PLY export on a synthetic tetrahedron; binary byte layout round-trips (verts, normals, tri indices) correctly. PLY files land next to the GLBs in `glb_files/`. |

| 2026-08-05 | Unity PLY importer (consumes the pipeline's primary mesh format) | New `Assets/Scripts/IO/` module in the Unity project — the first real code there (it was a bare template). Closes the loop opened by the 2026-08-03 PLY-primary refactor: Unity can now load pipeline mesh output directly. Files: `PlyParser.cs` (format reader, **no Unity API calls so it runs off the main thread**), `PlyMeshData.cs` (+`PlyImportSettings`), `PlyMeshFactory.cs` (Mesh build, main thread), `PlyRuntimeLoader.cs` (MonoBehaviour, loads by path — use for `Processed_data/` output that gets regenerated), `Editor/PlyScriptedImporter.cs` (makes `.ply` inside `Assets/` a native mesh asset — use for build-shipped meshes). Two asmdefs keep editor code out of player builds. **Coordinate frame (important):** pipeline PLYs are Z-up right-handed local metres; conversion to Unity is the single swap `(x,y,z)->(x,z,y)` and **winding must NOT be reversed** — the swap is itself a handedness flip, so reversing indices too renders every building inside-out. Verified numerically: axis-swap alone → 99.3% outward faces; with an added winding flip → 99.3% *inward*. `FlipWinding` exists as an off-by-default escape hatch for third-party PLYs. **Large meshes:** `IndexFormat.UInt32` is selected from the actual vertex count (district_000 is 237,594 verts, far over the 65,535 16-bit cap — the usual cause of silently truncated city meshes). Supports ascii/binary LE/BE, any property order, all PLY scalar types, n-gons, unknown elements, missing normals. Validated by compiling against Unity stubs and parsing real files (counts match headers, **0 leftover bytes**; largest district 89 ms) plus 7 synthetic edge cases (corrupt index warns+skips, truncated/non-PLY fail with clear messages). **Two import routes, easy to confuse:** `PlyRuntimeLoader` (runtime, any path, reads `Processed_data/` directly — this is the one for pipeline output) vs `PlyScriptedImporter` (editor-only, only files copied *into* `Assets/` — Unity never scans outside the project, so it will never see `Processed_data/`). `Editor/PlyImportMenu.cs` adds a **City Digital Twin → Load PLY into Scene… / Inspect PLY** menu so a district can be loaded or checked with no code and no play mode; it frames the object automatically because these meshes are km-scale and otherwise start off-camera, which reads as a silent failure. Docs: [docs/UNITY_PLY_IMPORTER.md](docs/UNITY_PLY_IMPORTER.md). Known limitation: decimated LOD3 shows ~90% (vs ~99%) normal agreement because vertex reduction moves vertices without recomputing normals — enable `RecalculateNormals` for those; exporter-side, not an importer bug. Note: each district PLY is rebased on **its own** origin, so multi-district scenes must offset transforms using the manifest `origin` — the importer does no re-projection. |

| 2026-08-05 | Unity georeferenced city loading (folder import + manifest placement) | Adds the two halves needed to get a full pipeline run into Unity at its real coordinates, split deliberately: **`PlyFolderImporter.cs`** bulk-parses every PLY in a folder (parallel, no Unity API, so it runs on worker threads — data-in only, no scene involvement) and **`CityMeshLoader.cs`** reads `meshes_manifest.json` and places each group. **`MeshManifest.cs`** parses the manifest (JsonUtility can't read its dictionary-shaped `mesh_files`, so a targeted raw-JSON scan recovers filenames; falls back `mesh_files`→`ply_files`→conventional name, and probes both `mesh_files/` and legacy `glb_files/`). **Two non-obvious placement rules, both verified against the 500 m grid run:** (1) **`origin.z` must NOT be applied** — `builder.py` rebases X/Y only and leaves Z absolute; the manifest still reports `origin.z` (0.5) but adding it back lifts the whole city (mesh-local z-min == origin.z == 0.50 confirms it was never subtracted). (2) **Raw SWEREF99 coordinates must not reach the renderer** — at northing 6,392,898 consecutive float32 values are **0.5 m** apart (measured), causing visible vertex snapping/z-fighting; rebasing on the run's SW corner gives 0.98 mm. `rebaseOnSouthWestCorner` defaults on; `SceneToCrs()`/`CrsToScene()` convert back for analytics (round-trip error 0.097 cm). Also note the grid manifest's `origin` is the **content bbox corner, not the 500 m cell corner** (0/200 sampled origins snap to a 500 m multiple), and content spans reach 871 m because border-straddling buildings are assigned whole — so placement must use `origin`, never a computed grid index. **Scale finding:** the 500 m grid run is 2211 cells / 2.67M verts / 117 MB, median only 598 verts per cell and **no cell over 65,535** — so per-cell meshes never need UInt32, but merging does. `combineMeshes` (default on) batches to ~262k verts: **2211 draw calls → 11**. Individual mode attaches a `CityMeshGroup` component (groupId + easting/northing) for picking and interaction logging. `maxGroups`/`radiusFilterMeters` limit scope while iterating. Validated end-to-end against the real run: 2211/2211 files parsed in 382 ms, 2211/2211 manifest filenames resolved, placed extent 30.8 × 29.4 km. Docs: [docs/UNITY_PLY_IMPORTER.md](docs/UNITY_PLY_IMPORTER.md). |

| 2026-08-05 | Removed the un-georeferenced Unity load path; manifest flow is now the only one | Cleanup of the same day's earlier work. **Deleted** `PlyRuntimeLoader.cs` (single-file, by-path MonoBehaviour) and `Editor/PlyImportMenu.cs` ("Load PLY into Scene") — both dropped a PLY into the scene **in its own local frame**, i.e. at (0,0), which is wrong for anything but inspecting one mesh in isolation and invited exactly the "everything stacks at the origin" mistake. Its `ResolvePath()` helper was used by `MeshManifest` and `PlyFolderImporter`, so it was extracted first into a neutral **`ProjectPaths.cs`** (`Resolve()`, `ProjectRoot`, `ProcessedDataDirectory`) rather than deleted with the file. **Added** `Editor/CityLoaderMenu.cs` (menu: *Add City Mesh Loader to Scene* / *Inspect Mesh Run* / *Inspect Single PLY* — all manifest-driven, inspection commands make no scene changes) and `Editor/CityMeshLoaderInspector.cs` (**Load Now / Clear** buttons that pump the coroutine in edit mode, so the city is visible in the Scene view without entering Play). `PlyScriptedImporter` is **kept but re-scoped**: it only handles `.ply` files copied *into* `Assets/`, imports them in their local frame with no placement, and its docs now point at `CityMeshLoader` for pipeline output. Verified nothing in `SampleScene` referenced the deleted components (guid scan) and the runtime assembly compiles clean with no dangling references. Docs [docs/UNITY_PLY_IMPORTER.md](docs/UNITY_PLY_IMPORTER.md) rewritten around the single flow, leading with usage. |

| 2026-08-05 | **Analytical grid foundation**: strict building ownership, frozen lattice, generic cell-attribute layers | Acts on a deep-research report; all its claims were verified against the data first, and two further bugs found. **(1) Building duplication (real, shipped).** `grid.py` used `geometry.intersects()`, assigning a building to EVERY cell it touched: 211,620 slots for 201,594 unique buildings — **9,690 duplicated across 2-4 cells, 5.0% inflation**. `base.MeshStrategy.validate()` already documented a STRICT OWNERSHIP POLICY and detected it, but `generator.py` only logged a warning; no test ever instantiated a real strategy. Fixed in grid/quadtree/district via `representative_point()` (guaranteed inside concave/donut footprints, unlike centroid); `within` was rejected as it silently DROPS straddlers. Generator now raises `StrategyValidationError` (`--allow-ownership-violations` to override). District gained a `district_unassigned` group — found 4 real buildings in polygon gaps. **(2) Unstable lattice (real).** The grid started at `buildings_gdf.total_bounds`, so cell ids changed with the input extent; the shipped origin sat **(197.9, 90.0) off** a 500 m lattice. New **`Src/mesh_generation/grid_reference.py`** freezes it at **(298000, 6383500) EPSG:3006** — a multiple of both 500 and 1000, so every SCB Ruta size (100/250/500/1000 m) nests exactly. Ids are now signed, anchor-relative (`grid_-001_+018`). **(3) Population double-counting (real, latent).** `preprocess_spatial_joins.py` gave every building in a cell that cell's FULL total; **plus a bug the report missed** — `drop_duplicates(subset=["Ruta"])` silently lost **2,186 residents** because `Ruta` collides between the 250 m and 1000 m sub-layers. Never run successfully (its configured path doesn't exist), so nothing was contaminated; moved to `Src/Scripts/legacy/` with its four defects documented. **(4) Rejected from the report:** the proposed Unity axis change (`Z = -PLY Y`) — the existing swap is verified (99.3% outward faces) and switching needs a coupled winding flip with a quiet failure mode, for a cosmetic gain. **Key data finding:** bin by the **`Ruta` CODE, never the geometry** — nesting 250 m cells into 500 m succeeds 3936/3936 via the code but only **981/3936 via geometry** (~4 mm vertex noise; 40 cells clipped at the municipal boundary). This makes the attribute join integer arithmetic — no spatial predicate, no tolerance. **New `Src/pipelines/cell_attributes/`** is generic per the user's steer (population is one layer of many): a new layer is a `CellLayerSpec` entry, not code. All **10 SCB Ruta layers** (population ×5, employment ×3, education, income) aggregate through one path with conservation asserted. Resolution reconciliation: the 250/1000 m sub-layers OVERLAP (18.3% of coarse area) and are NOT parent/child — **83 of 98 overlapping coarse cells give negative remainders** — so subtraction is unjustified; rule is fine-priority + disjoint coarse (717,781 → **715,690**, 99.71%, across 1,766 cells). **Unity:** `.gitignore` no longer excludes the whole `Unity/` tree (authored source was untracked); `CityMeshLoader` now prefers the frozen anchor over the run's own SW corner, so world coordinates stay stable across regenerations — interaction logs remain comparable between evaluation sessions. **Verified:** duplicates 10,026→**0**; 100% coverage on all three strategies; 100% of 60,478 shared buildings keep their cell id under a 30% subset; 0 cells off-lattice; 12/12 tests pass (4 new ownership guards); regenerated run 2,160 cells / 2.50M verts (−6.3%, the duplicates); cross-layer check — 1,823 shared cell ids with **0** origin mismatches and **201,594/201,594** buildings identically assigned by mesh and attribute index. Old run kept at `building_meshes_grid_500m/` pending Unity validation. Docs: 4 decision notes in `docs/decisions/`, dataset + profiling in `docs/data-analysis/`, 3 reports in `reports/`. |

## Open Questions

Keep this list small. Only include questions that affect the next implementation decision.

- Which dataset pipeline beyond buildings should become the next canonical example, if any?
- Should the old phase-based docs be archived, or kept as historical references in place?
- Is the default mesh strategy still district grouping, or should that be moved into a dedicated config reference?
- Should future dataset pipelines follow the same structure as the modular architecture exactly, or only loosely?

## Notes For Future Agents

Before editing code or docs, check the newest dated handoff or summary document first, but treat [docs/MODULAR_ARCHITECTURE.md](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/docs/MODULAR_ARCHITECTURE.md) as the current repo-wide architecture summary.

Recommended reasoning order:
1. check the modular architecture doc;
2. check the current pipeline config and pipeline implementation;
3. check downstream mesh or analytics code only if the task requires it;
4. use the phase docs only as historical or transition context;
5. update this living context section only after confirming the change is stable enough to matter.

If the repo changes in a way that affects file layout, naming, or data flow, add a short note here so future agents do not need to rediscover the new pattern from scratch.