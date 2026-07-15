# Graph Report - .  (2026-07-15)

## Corpus Check
- 77 files · ~65,061 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 637 nodes · 1017 edges · 42 communities (34 shown, 8 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 117 edges (avg confidence: 0.63)
- Token cost: 195,455 input · 21,716 output

## Community Hubs (Navigation)
- Mesh Strategy Interface
- Mesh Builder & GLB Export
- Raw Dataset Profiling
- Pipeline Runners & Postprocess
- BasePipeline Abstraction
- Mesh Generator Orchestration
- Swedish Geospatial Datasets
- PDAL Cache & Pipeline
- Building Mesh Generation
- Byggnad Preprocessing
- LiDAR Pipeline Config & Validation
- Spatial Joins & Analytics
- Height Estimation Config
- LiDAR Tile Indexing
- LiDAR Height Pipeline
- Mesh Validation & Unity Import
- Height Estimator
- Phase 2-4 Unity Handoff
- Sprint Plan & Thesis Framing
- Height Export
- Building Semantics & Rebasing
- Pipeline Architecture Docs
- Mesh Grouping Strategies
- Dev Policy & Living Context
- Mesh Manifest Schema
- Height Fallback & PDAL Logic
- Height Extraction Config
- Swedish-English Aliasing
- Tile Batch Deduplication
- Package Init (29)
- Package Init (30)
- Byggnad Loader
- DataFrame Profiler
- Geometry Validation/Repair
- Unity Import Guide (empty)
- Deterministic Ownership Policy
- Ruta vs DeSO Grids

## God Nodes (most connected - your core abstractions)
1. `MeshStrategy` - 22 edges
2. `LiDARHeightPipeline` - 22 edges
3. `MeshGroup` - 20 edges
4. `MeshBuilder` - 19 edges
5. `HeightEstimator` - 18 edges
6. `profile_shapefile()` - 16 edges
7. `profile_geopackage()` - 16 edges
8. `ValidationSuite` - 16 edges
9. `DataFrameProfiler` - 16 edges
10. `StrategyConfig` - 15 edges

## Surprising Connections (you probably didn't know these)
- `Docs as Local Memory for AI Sessions` --semantically_similar_to--> `Project Living Context`  [INFERRED] [semantically similar]
  docs/Documentation System & Copilot Session.md → Project_livingContext.md
- `Buildings Manifest JSON` --semantically_similar_to--> `Semantic-Rich Mesh Manifest Schema`  [INFERRED] [semantically similar]
  docs/byggnad_ortnamn_pipeline.md → MESH_ROBUSTNESS_SUMMARY.md
- `Buildings (Byggnad) Profile — Gothenburg 2026-05-13 Processed` --references--> `Lantmäteriet: Byggnad (Building footprints)`  [INFERRED]
  reports/buildings_2026-05-13/buildings_profile.md → docs/swedish_geospatial_datasets_full.pdf
- `Buildings (Byggnad) Profile — Helsingborg 2026-05-19 Processed` --references--> `Lantmäteriet: Byggnad (Building footprints)`  [INFERRED]
  reports/helsingborg_buildings_2026-05-19/buildings_profile.md → docs/swedish_geospatial_datasets_full.pdf
- `geopandas` --conceptually_related_to--> `Lantmäteriet: Byggnad (Building footprints)`  [INFERRED]
  requirements.txt → docs/swedish_geospatial_datasets_full.pdf

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **LiDAR Height Enrichment to Mesh Generation Flow** — docs_buildings_pipeline_buildings_pipeline, docs_lidar_height_pipeline_summary_lidar_pipeline, lidar_pipeline_robustness_refactor_naming_convention, docs_modular_architecture_mesh_generator, implementation_complete_glb_export [INFERRED 0.85]
- **Pluggable Mesh Grouping Strategies** — docs_modular_architecture_mesh_strategy_pattern, docs_modular_architecture_individual_strategy, docs_modular_architecture_district_strategy, docs_modular_architecture_grid_strategy, docs_modular_architecture_quadtree_strategy [EXTRACTED 1.00]
- **Four-Phase Building Pipeline Delivery** — phase2_3_4_completion_summary_completion, docs_phase2_progress_phase2, docs_phase4_final_handoff_handoff, phase2_3_4_completion_summary_building_meshes, phase2_3_4_completion_summary_population_join [EXTRACTED 1.00]
- **Byggnad building-footprint profiling across cities and dates (processed vs postprocess snapshots)** — reports_buildings_2026_05_13_buildings_profile_byggnad_dataset, reports_buildings_2026_05_13_postprocess_buildings_profile_byggnad_dataset, reports_helsingborg_buildings_2026_05_19_buildings_profile_byggnad_dataset, reports_buildings_2026_06_29_buildings_profile_byggnad_dataset, reports_buildings_2026_06_29_postprocess_buildings_profile_byggnad_dataset [INFERRED 0.85]
- **Swedish geospatial data authorities cataloged for digital twin** — docs_swedish_geospatial_datasets_full_lantmateriet, docs_swedish_geospatial_datasets_full_scb, docs_swedish_geospatial_datasets_full_sgu, docs_swedish_geospatial_datasets_full_slu, docs_swedish_geospatial_datasets_full_sjofartsverket [EXTRACTED 1.00]

## Communities (42 total, 8 thin omitted)

### Community 0 - "Mesh Strategy Interface"
Cohesion: 0.05
Nodes (43): Mesh Generator: Main orchestrator for mesh generation with any strategy.  Coor, MeshGroup, MeshStrategy, ABC, Any, Base class for mesh generation strategies.  Defines the interface that all gro, Get statistics about partitioning., Represents a group of buildings to be meshed together. (+35 more)

### Community 1 - "Mesh Builder & GLB Export"
Cohesion: 0.07
Nodes (40): check_watertight(), MeshBuilder, MultiPolygon, ndarray, Path, Polygon, Mesh Builder: Core logic for converting 2D building geometries to 3D meshes., Check whether a mesh is a closed, edge-manifold surface.      A closed manifol (+32 more)

### Community 2 - "Raw Dataset Profiling"
Cohesion: 0.10
Nodes (43): compute_categorical_distribution(), compute_null_counts(), DatasetProfile, detect_duplicate_ids(), detect_geometry_issues(), discover_and_profile_datasets(), flag_swedish_field_anomalies(), generate_markdown_report() (+35 more)

### Community 3 - "Pipeline Runners & Postprocess"
Cohesion: 0.07
Nodes (24): DataFrame, build_postprocess_snapshot(), Any, GeoDataFrame, Path, Post-processing utilities for the buildings pipeline.  Creates an optional pos, Create a postprocess snapshot of the dataset.      Steps:     1. Drop exact d, main() (+16 more)

### Community 4 - "BasePipeline Abstraction"
Cohesion: 0.06
Nodes (24): BasePipeline, ABC, Path, Base pipeline class for all data processing pipelines.  Provides abstract inte, Abstract base class for data processing pipelines.          All dataset-specif, Get brief description of current data., Initialize pipeline.                  Args:             name: Human-readable, Load raw data from source.                  Must populate self.data with loade (+16 more)

### Community 5 - "Mesh Generator Orchestration"
Cohesion: 0.08
Nodes (27): Exception, GeneratorConfig, MeshGenerator, Any, GeoDataFrame, Path, Main pipeline: partition → mesh → export → report.                  Args:, Build mesh for a group of buildings with proper height sourcing and optional LOD (+19 more)

### Community 6 - "Swedish Geospatial Datasets"
Cohesion: 0.06
Nodes (38): Lantmäteriet: Byggnad (Building footprints), Lantmäteriet: Höjddata Grid 2+ (Elevation raster), Lantmäteriet: Hydrografi (Hydrography / water bodies), Lantmäteriet: Kommunikation (Transport networks), Lantmäteriet (Swedish Mapping, Cadastral and Land Registration Authority), Lantmäteriet: Laserdata NH 2019 (LiDAR point cloud), Lantmäteriet: Markhöjdmodell grid 1+ (Terrain elevation model), Lantmäteriet: Ortnamn (Place names) (+30 more)

### Community 7 - "PDAL Cache & Pipeline"
Cohesion: 0.09
Nodes (20): PDALCacheManager, PDALPipelineGenerator, Any, Path, Get cache statistics.                  Returns:             Dict with hits, m, Generate and execute PDAL processing pipelines for LiDAR preprocessing., Initialize pipeline generator.                  Args:             config: PDA, Generate PDAL JSON pipeline for LAZ preprocessing.                  Pipeline s (+12 more)

### Community 8 - "Building Mesh Generation"
Cohesion: 0.11
Nodes (31): BuildingMetadata, export_mesh_to_glb(), export_mesh_to_obj(), extract_height_fields(), filter_to_scope(), handle_multipolygon(), load_buildings_from_gpkg(), MeshGenerationReport (+23 more)

### Community 9 - "Byggnad Preprocessing"
Cohesion: 0.15
Nodes (21): assess_data_quality(), compute_geospatial_metrics(), export_processed_data(), import_raw_buildings(), main(), normalize_building_semantics(), normalize_geometry_and_crs(), NumpyEncoder (+13 more)

### Community 10 - "LiDAR Pipeline Config & Validation"
Cohesion: 0.15
Nodes (15): LiDARHeightPipelineConfig, Master configuration for the LiDAR height estimation pipeline., Validate and set defaults., create_default_config(), main(), Path, Test fallback logic: building with < MIN_POINTS should receive fallback height, Test CRS preservation: output GeoPackage must maintain EPSG:3006. (+7 more)

### Community 11 - "Spatial Joins & Analytics"
Cohesion: 0.16
Nodes (19): aggregate_building_analytics(), BuildingAnalytics, create_spatial_joins(), GridCellAggregates, load_buildings_subset(), load_population_data(), GeoDataFrame, Path (+11 more)

### Community 12 - "Height Estimation Config"
Cohesion: 0.20
Nodes (12): Enum, HeightSource, PDALConfig, QualityLevel, Configuration dataclasses for the LiDAR height estimation pipeline., Height estimation quality classification., Source of height estimate., Configuration for PDAL preprocessing pipeline. (+4 more)

### Community 13 - "LiDAR Tile Indexing"
Cohesion: 0.17
Nodes (10): Configuration for LiDAR tile indexing and spatial mapping., TileIndexConfig, GeoDataFrame, Spatial indexing of buildings to LiDAR tiles., Validate tile coverage against buildings.                  Returns:, Maps building footprints to intersecting LiDAR tiles., Initialize tile index.                  Args:             buildings_gdf: GeoD, Scan tile directory and extract bounding boxes from LAZ headers. (+2 more)

### Community 14 - "LiDAR Height Pipeline"
Cohesion: 0.18
Nodes (9): LiDARHeightPipeline, Path, Modular pipeline for enriching buildings with LiDAR-derived heights., Process a single tile in a worker process (for parallel execution)., Initialize LiDAR height estimation pipeline.                  Args:, Load processed buildings from GeoPackage., Export enriched buildings to GeoPackage, Parquet, and QC CSV., Execute full pipeline: load → validate → preprocess → export. (+1 more)

### Community 15 - "Mesh Validation & Unity Import"
Cohesion: 0.19
Nodes (14): create_unity_import_guide(), MeshValidationResult, Path, Validate building meshes and prepare for Unity import.  This script: 1. Check, Validate buildings_manifest.json structure., Validate all generated meshes., Create guide for importing meshes into Unity., Validation result for a single mesh. (+6 more)

### Community 16 - "Height Estimator"
Cohesion: 0.20
Nodes (9): HeightEstimator, GeoDataFrame, Path, Validate that building has adequate LiDAR coverage.                  Args:, Extract height for a single building.                  Args:             buil, Extract building heights from classified LiDAR point clouds., Classify height estimation quality.                  Args:             point_, Create a fallback height entry for a building with insufficient coverage. (+1 more)

### Community 17 - "Phase 2-4 Unity Handoff"
Cohesion: 0.20
Nodes (11): Phase 2 Progress and Architecture, Separate Meshes per Building Decision, Click-to-Inspect Building Interaction, Phase 4 Final Handoff, Unity Integration Workflow, Phase 4 Handoff Template, GLB (glTF 2.0 Binary) Export, Mesh Robustness Implementation Complete (+3 more)

### Community 18 - "Sprint Plan & Thesis Framing"
Cohesion: 0.18
Nodes (11): 1-Week Prototype Sprint Plan, Terrain Mesh from Elevation (DEM), Prototype README, Boje et al. 2020 (Digital Twin Semantics), Bunea & Dobre 2024 (VR Urban Twins), Empirical User Evaluation Study, Immersive Analytics, Reski et al. (2020-2025, Immersive VR Analytics) (+3 more)

### Community 19 - "Height Export"
Cohesion: 0.24
Nodes (7): HeightExporter, GeoDataFrame, Path, Export enriched building heights to output formats., Export enriched buildings with heights to GeoPackage, Parquet, and QC CSV., Compute summary statistics for enriched heights.                  Args:, Write enriched buildings to output files.                  Produces three outp

### Community 20 - "Building Semantics & Rebasing"
Cohesion: 0.22
Nodes (9): Building Object Type Classification, Byggnad (Buildings) Dataset, Lantmäteriet (Swedish Land Survey), Ortnamn (Place Names) Dataset, Local-Origin Rebasing, Vertex Normals Computation, Mesh Generation Robustness (Phase 3), 220k Building Mesh Assets (+1 more)

### Community 21 - "Pipeline Architecture Docs"
Cohesion: 0.29
Nodes (8): Buildings (Byggnad) Pipeline, LiDAR Height Estimation Pipeline, LiDAR Pipeline Performance Optimization, BasePipeline Abstract Class, Load-Validate-Preprocess-Export Pattern, LiDAR Pipeline Robustness Refactoring, Data Pipeline Layer, Validation Before Preprocessing

### Community 22 - "Mesh Grouping Strategies"
Cohesion: 0.29
Nodes (8): District Grouping Mesh Strategy, Grid Grouping Mesh Strategy, Individual Building Mesh Strategy, MeshGenerator Orchestrator, Mesh Generation Strategy Pattern, Quadtree Grouping Mesh Strategy, Level-of-Detail (LOD) Strategy, buildings_lidar_added Naming Convention

### Community 23 - "Dev Policy & Living Context"
Cohesion: 0.33
Nodes (7): Development Policy and Standards, Data Insufficiency Protocol, Keep the Pipeline Observable, Documentation System and Copilot Sessions, Docs as Local Memory for AI Sessions, Modular Pipeline Architecture, Project Living Context

### Community 24 - "Mesh Manifest Schema"
Cohesion: 0.29
Nodes (7): Buildings Manifest JSON, Semantic 3D Building Pipeline (Byggnad + Ortnamn), Stable Unique Building IDs, Quality Classification (high/medium/low), 2D Polygon to 3D Prism Algorithm, Height Source Distribution Tracking, Semantic-Rich Mesh Manifest Schema

### Community 25 - "Height Fallback & PDAL Logic"
Cohesion: 0.33
Nodes (6): 10m Fallback Height Logic, Per-Building Height Extraction Logic, PDAL Point Cloud Preprocessing, Config Fingerprinting Cache Invalidation, PDALCacheManager (PDAL Output Caching), Building LiDAR Coverage Validation

### Community 26 - "Height Extraction Config"
Cohesion: 0.33
Nodes (4): HeightExtractionConfig, Create config from dictionary (useful for CLI argument parsing)., Configuration for per-building height extraction and quality assessment., Initialize height estimator.                  Args:             config: Heigh

### Community 27 - "Swedish-English Aliasing"
Cohesion: 0.67
Nodes (3): Swedish to English Alias Mapping, Swedish Datasets as First-Class Inputs, Configuration-Driven Field Translations

### Community 28 - "Tile Batch Deduplication"
Cohesion: 0.67
Nodes (3): Batch Processing by LiDAR Tile, Parallel Tile Processing (ProcessPoolExecutor), Tile Boundary Deduplication

## Knowledge Gaps
- **51 isolated node(s):** `Ruta vs DeSO Analytical Grids`, `Load-Validate-Preprocess-Export Pattern`, `Grid Grouping Mesh Strategy`, `Quadtree Grouping Mesh Strategy`, `DataFrameProfiler` (+46 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `BuildingsPipeline` connect `BasePipeline Abstraction` to `Pipeline Runners & Postprocess`?**
  _High betweenness centrality (0.183) - this node is a cross-community bridge._
- **Why does `main()` connect `Pipeline Runners & Postprocess` to `BasePipeline Abstraction`?**
  _High betweenness centrality (0.180) - this node is a cross-community bridge._
- **Why does `DataFrameProfiler` connect `Pipeline Runners & Postprocess` to `Mesh Generator Orchestration`?**
  _High betweenness centrality (0.178) - this node is a cross-community bridge._
- **Are the 8 inferred relationships involving `MeshStrategy` (e.g. with `DistrictConfig` and `DistrictStrategy`) actually correct?**
  _`MeshStrategy` has 8 INFERRED edges - model-reasoned connections that need verification._
- **Are the 9 inferred relationships involving `LiDARHeightPipeline` (e.g. with `HeightExtractionConfig` and `LiDARHeightPipelineConfig`) actually correct?**
  _`LiDARHeightPipeline` has 9 INFERRED edges - model-reasoned connections that need verification._
- **Are the 8 inferred relationships involving `MeshGroup` (e.g. with `DistrictConfig` and `DistrictStrategy`) actually correct?**
  _`MeshGroup` has 8 INFERRED edges - model-reasoned connections that need verification._
- **Are the 7 inferred relationships involving `MeshBuilder` (e.g. with `GeneratorConfig` and `MeshGenerator`) actually correct?**
  _`MeshBuilder` has 7 INFERRED edges - model-reasoned connections that need verification._