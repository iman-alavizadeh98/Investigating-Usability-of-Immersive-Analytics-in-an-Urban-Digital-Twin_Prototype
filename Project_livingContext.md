# Project Living Context

This document is the working reference for future AI agents. Update it incrementally as the project changes. The goal is fast orientation: what is canonical now, what is legacy, and where a future change should be made without reading the whole repository.

## Project Snapshot

This repository is a Gothenburg urban digital twin prototype focused on Swedish geospatial data, buildings, preprocessing pipelines, mesh generation, and Unity-ready analytical assets.

The current project direction is pipeline-first. The modular architecture is now the main source of truth for how the repo is structured, while the older phase-based documents are still useful as historical snapshots of how the work evolved.

The main intent is to preserve Swedish source semantics while creating processed English-facing outputs for development, analytics, and runtime use. The key interaction pattern remains building-level spatial lookup: a building can later be linked to demographic, geographic, and metadata layers.

## Current Status

The current work is centered on the modular pipeline system and its downstream mesh and analytics workflows, with a new focus on LOD1 input enrichment via LiDAR height estimation.

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
- [Src/pipelines/buildings/pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/buildings/pipeline.py)
- [Src/pipelines/buildings/config.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/buildings/config.py)
- [Src/pipelines/lidar_heights/pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/pipeline.py) (NEW)
- [Src/pipelines/lidar_heights/config.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/pipelines/lidar_heights/config.py) (NEW)
- [Src/mesh_generation/generator.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/generator.py)
- [Src/mesh_generation/builder.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/mesh_generation/builder.py)
- [Src/Scripts/run_buildings_pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_buildings_pipeline.py)
- [Src/Scripts/run_lidar_height_pipeline.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_lidar_height_pipeline.py) (NEW)
- [Src/Scripts/run_mesh_generation.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/run_mesh_generation.py)
- [Src/Scripts/preprocess_spatial_joins.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/preprocess_spatial_joins.py)
- [Src/Scripts/validate_meshes.py](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Src/Scripts/validate_meshes.py)
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

It converts building footprints into 3D meshes and supports multiple grouping strategies. The strategy layer exists to trade off interaction fidelity against file count and load performance.

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
- `Processed_data/buildings_with_heights.gpkg` — enriched buildings with height_m, quality flags, point metadata
- `Processed_data/buildings_with_heights.parquet` — same data in Parquet format
- `Processed_data/building_height_qc.csv` — QC subset for validation

**Key Design Constraints**:
- Preserves all building IDs (no filtering)
- Uses 10m fallback height for buildings with insufficient LiDAR coverage
- Marks quality="low" when coverage is poor (enables downstream prioritization)
- Batch-processes by tile to manage memory (~500MB–1GB per tile)
- Uses PDAL for explicit preprocessing; every step is auditable
- All heights remain in EPSG:3006; coordinates unchanged

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

## Data Contracts And Assumptions

These assumptions should be changed only when code or newer handoff docs explicitly show a new convention.

### Spatial Reference

EPSG:3006 is the default spatial reference for the project. Any exception must be documented explicitly and converted deliberately.

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
- [Processed_data/buildings_with_heights.gpkg](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_with_heights.gpkg)
- [Processed_data/buildings_with_heights.parquet](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/buildings_with_heights.parquet)
- [Processed_data/building_height_qc.csv](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/building_height_qc.csv)

Downstream outputs may include:
- [Processed_data/building_meshes/](w:/Investigating%20Usability%20of%20Immersive%20Analytics%20in%20an%20Urban%20Digital%20Twin/Portotype/Processed_data/building_meshes)
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