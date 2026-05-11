# Modular Architecture: Refactored Pipeline System

**Date**: May 11, 2026  
**Status**: ✅ Complete

## Overview

The project has been refactored from monolithic scripts into a modular, pluggable architecture that addresses three major issues:

1. **Separation of Concerns**: Load → Validate → Preprocess → Export (not all in one script)
2. **Mesh Generation Strategies**: Pluggable strategies for different grouping approaches
3. **Data Profiling**: Automated dataframe analysis with Markdown/HTML export

---

## Architecture Diagram

```
Src/
├── pipelines/                           # Dataset-specific pipelines
│   ├── base.py                          # Abstract BasePipeline class
│   ├── byggnad/                         # Example: Byggnad pipeline
│   │   ├── loader.py                    # Load raw data only
│   │   ├── validator.py                 # Validate only
│   │   ├── preprocessor.py              # Transform/clean only
│   │   ├── exporter.py                  # Save processed data
│   │   └── pipeline.py                  # Orchestrator (uses base class)
│   └── population/                      # Similar structure for other datasets
│
├── mesh_generation/                     # 3D mesh generation engine
│   ├── builder.py                       # Core geometry → mesh conversion
│   ├── generator.py                     # Main orchestrator
│   └── strategies/                      # Pluggable grouping strategies
│       ├── base.py                      # MeshStrategy abstract class
│       ├── individual.py                # Each building = 1 mesh (220k+)
│       ├── district.py                  # Each district = 1 mesh (50-100)
│       ├── grid.py                      # Regular grid cells (100-200)
│       └── quadtree.py                  # Adaptive hierarchical (500-1k)
│
├── utils/                               # Shared utilities
│   ├── data_profiler.py                 # DataFrame profiling & reporting
│   ├── logger.py                        # Logging configuration
│   └── validators.py                    # Data validation helpers
│
└── scripts/                             # Entry points
    ├── run_mesh_generation.py           # Generate meshes (any strategy)
    ├── profile_dataframe.py             # Profile a dataframe
    ├── run_byggnad_pipeline.py          # Run byggnad pipeline
    └── run_population_pipeline.py       # Run population pipeline
```

---

## Part 1: Modular Pipelines

### Problem Before
```
generate_building_meshes.py (600 lines)
├─ Load
├─ Filter
├─ Validate
├─ Height extraction
├─ Mesh generation
├─ GLB export
└─ Manifest creation
```

All in one script → hard to reuse, test, debug.

### Solution: BasePipeline

Every dataset pipeline inherits from `BasePipeline`:

```python
from pipelines.base import BasePipeline

class ByggnadsLPipeline(BasePipeline):
    def load(self):
        # Just load
        pass
    
    def validate(self):
        # Just validate
        return report
    
    def preprocess(self):
        # Just transform
        pass
    
    def export(self, output_dir):
        # Just save
        pass
```

**Benefits**:
- Each method is focused and testable
- Easy to replace one step
- Consistent across all pipelines
- `run()` orchestrates automatically

**Usage**:
```python
from pipelines.byggnad_pipeline import ByggnadsLPipeline

pipeline = ByggnadsLPipeline(config)
report = pipeline.run(output_dir="Processed_data")
```

---

## Part 2: Mesh Generation Strategies

### Problem Before
```
# generate_building_meshes.py
for building in all_buildings:
    mesh = build_single_mesh(building)
    export_glb(mesh)
    
# Result: 220,000+ GLB files (~20GB)
```

- Slow to load
- Hard to manage
- Repeated code for different grouping approaches
- Not scalable to other cities

### Solution: Strategy Pattern

**Available Strategies**:

#### 1. Individual Buildings (Current)
```
220,000+ GLB files
├─ bldg_000000.glb    (1 building)
├─ bldg_000001.glb    (1 building)
└─ ...
```
- Pros: Per-building click detection
- Cons: Huge file count, slow loading (~20GB)

#### 2. District (Recommended for Gothenburg)
```
District-based (50-100 meshes)
├─ district_001.glb   (15,000 buildings)
├─ district_002.glb   (18,000 buildings)
└─ ...
```
- Pros: 50-100 files, fast loading, natural boundaries
- Cons: Less granular, needs district layer (or synthetic grid)

#### 3. Grid-Based
```
1km × 1km grid cells (100-200 meshes)
├─ grid_000_000.glb   (cells intersecting buildings)
├─ grid_001_000.glb
└─ ...
```
- Pros: Predictable, works anywhere, easy caching
- Cons: Grid doesn't follow natural boundaries

#### 4. Quadtree (Advanced)
```
Adaptive hierarchical (500-1000 cells)
├─ Recursively splits based on building density
├─ Dense areas: many small cells
└─ Sparse areas: fewer large cells
```
- Pros: Adaptive, LOD-friendly, space-efficient
- Cons: Complex, variable mesh sizes

### Usage: Choose Any Strategy

```python
from mesh_generation.generator import MeshGenerator, GeneratorConfig
import geopandas as gpd

buildings = gpd.read_file("buildings.gpkg")

# ──── Individual buildings ────
config = GeneratorConfig(strategy="individual")

# ──── District-based ────
config = GeneratorConfig(
    strategy="district",
    strategy_config={"min_buildings_per_group": 100}
)

# ──── Grid-based (1km cells) ────
config = GeneratorConfig(
    strategy="grid",
    strategy_config={"cell_size_m": 1000}
)

# ──── Quadtree ────
config = GeneratorConfig(
    strategy="quadtree",
    strategy_config={
        "max_buildings_per_cell": 500,
        "min_cell_size_m": 250
    }
)

# Generate
generator = MeshGenerator(buildings, config)
report = generator.generate("Processed_data/building_meshes")

print(f"Generated {report['total_groups']} meshes")
print(f"Total triangles: {report['total_triangles']:,}")
```

### Comparison Table

| Strategy | Mesh Count | File Size | Load Time | Scalable |
|----------|-----------|-----------|-----------|----------|
| Individual | 220k+ | 20GB+ | Slow | ✗ |
| **District** | **50-100** | **1-5GB** | **Fast** | **✓** |
| Grid | 100-200 | 2-8GB | Medium | ✓✓ |
| Quadtree | 500-1k | 5-12GB | Adaptive | ✓✓ |

---

## Part 3: Data Profiling & Visualization

### Problem Before
```
buildings_gdf.head()        # OK but incomplete
buildings_gdf.info()        # Text wall, hard to read
buildings_gdf.describe()    # Only numeric columns
```

No structured way to:
- Understand column meanings
- See data quality issues
- Document datasets
- Create reports for humans

### Solution: DataFrameProfiler

```python
from utils.data_profiler import DataFrameProfiler

gdf = gpd.read_file("buildings.gpkg")
profiler = DataFrameProfiler(gdf, name="Byggnad (Buildings)")

# Save to Markdown
profiler.save_markdown("reports/buildings_profile.md")

# Save to HTML
profiler.save_html("reports/buildings_profile.html")

# Get dict
profile = profiler.profile()
```

**Generated Output** (Markdown example):

```markdown
# Byggnad (Buildings)

Type: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 2,547,283 |
| Columns | 23 |
| Memory | 1.2 GB |
| CRS | EPSG:3006 |

## Columns

| Name | Type | Non-Null | Null % | Unique |
|------|------|----------|--------|--------|
| `object_id` | str | 2,547,283 | 0.0% | 2,547,283 |
| `geometry` | geometry | 2,547,283 | 0.0% | - |
| `height_m` | float64 | 2,345,012 | 7.9% | 1,245 |
| `primary_usage` | str | 2,100,450 | 17.5% | 45 |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `height_m` | 0.50 | 250.00 | 9.85 | 5.23 |
| `area_m2` | 10.00 | 45000.00 | 285.45 | 1200.00 |

## Sample Rows

```
   object_id             geometry  height_m primary_usage
0  obj_001     POLYGON (...)       10.5     Residence
1  obj_002     POLYGON (...)        8.2     Office
2  obj_003     POLYGON (...)       NULL     Shop
```

### Features

- **Column-by-column analysis**: Type, missingness, uniqueness
- **Numeric stats**: Min, max, mean, median, std
- **Categorical distributions**: Top N values with counts
- **Geometry info**: Types, validation, coverage
- **Memory usage**: Total and breakdown
- **Sample rows**: First 3 rows as reference

---

## Project Structure (Updated)

```
Portotype/
├── Src/
│   ├── pipelines/                  ← NEW: Modular pipeline structure
│   ├── mesh_generation/            ← NEW: Pluggable strategies
│   ├── utils/                      ← NEW: Shared utilities
│   ├── scripts/
│   │   ├── run_mesh_generation.py           (NEW)
│   │   ├── profile_dataframe.py             (NEW)
│   │   └── generate_building_meshes.py      (OLD - now optional)
│   └── Scripts/                    (legacy - still used)
├── Processed_data/
│   ├── building_meshes/
│   │   ├── meshes_manifest.json    (strategy-agnostic)
│   │   ├── district_001.glb        (or individual/*.glb, grid_*.glb, etc.)
│   │   └── ...
│   └── profiles/                   ← NEW: Per-dataset profiles
├── reports/
│   └── data-quality/
│       ├── byggnad_profile.md      ← NEW: Auto-generated profiles
│       └── byggnad_profile.html    ← NEW: Interactive HTML
└── docs/
    ├── MODULAR_ARCHITECTURE.md     ← This file
    └── ...
```

---

## Migration Guide

### For Byggnad Dataset

**Old way** (monolithic):
```bash
python Src/Scripts/generate_building_meshes.py
# → 220,000+ individual GLB files
```

**New way** (modular + strategies):
```bash
# Generate district-based meshes (recommended)
python Src/scripts/run_mesh_generation.py \
  --strategy district \
  --profile

# Or grid-based
python Src/scripts/run_mesh_generation.py \
  --strategy grid \
  --cell-size 1000

# Or use Python API
from mesh_generation.generator import MeshGenerator, GeneratorConfig
config = GeneratorConfig(strategy="district")
generator = MeshGenerator(buildings_gdf, config)
report = generator.generate("output_dir")
```

### For New Datasets (Population, etc.)

Create a new pipeline:

```python
# Src/pipelines/population_pipeline.py
from pipelines.base import BasePipeline

class PopulationPipeline(BasePipeline):
    def load(self):
        self.data = gpd.read_file(self.config["input"])
    
    def validate(self):
        # Check CRS, geometry, etc.
        return {}
    
    def preprocess(self):
        # Rename columns, fix types, etc.
        self.data = self.data.rename(columns={...})
    
    def export(self, output_dir):
        self.data.to_file(output_dir / "population_processed.gpkg")

# Usage
pipeline = PopulationPipeline(config)
report = pipeline.run()
```

---

## Testing & Validation

### Profile a Dataset
```bash
python Src/scripts/profile_dataframe.py
# → Generates reports/data-quality/byggnad_profile.md
```

### Test a Strategy
```python
from mesh_generation.strategies.district import DistrictStrategy

strategy = DistrictStrategy(buildings_gdf)
groups = strategy.partition()

# Validate
report = strategy.validate()
print(report["validation_errors"])

# Get stats
stats = strategy.get_statistics()
print(f"Avg buildings per group: {stats['avg_buildings_per_group']}")
```

### Try Different Strategies
```bash
# Individual (quick test)
python Src/scripts/run_mesh_generation.py \
  --strategy individual \
  --input path/to/small_subset.gpkg

# District (recommended for Gothenburg)
python Src/scripts/run_mesh_generation.py \
  --strategy district

# Grid (test different cell sizes)
python Src/scripts/run_mesh_generation.py \
  --strategy grid --cell-size 500
python Src/scripts/run_mesh_generation.py \
  --strategy grid --cell-size 2000

# Quadtree (test different density thresholds)
python Src/scripts/run_mesh_generation.py \
  --strategy quadtree --max-buildings 1000
```

---

## Key Benefits

### 1. Modularity
- Each step (load, validate, preprocess, export) is independent
- Easy to test, debug, replace
- Reusable across pipelines

### 2. Flexibility
- Choose any mesh grouping strategy
- Switch strategies without changing core code
- Easy to add new strategies

### 3. Scalability
- Works for any city
- Handles large datasets efficiently
- Strategies adapt to data density

### 4. Observability
- DataFrame profiling with Markdown/HTML export
- Automatic data quality reports
- Clear logging at every step
- Strategy validation reports

### 5. Maintainability
- Clear separation of concerns
- Consistent interfaces (BasePipeline, MeshStrategy)
- Well-documented code
- Easy to extend

---

## Next Steps

1. **Test the architecture**:
   - Try each mesh strategy on a subset of buildings
   - Profile different datasets
   - Verify output quality

2. **Create pipelines for other datasets**:
   - Population (befolkning)
   - Employment (arbetsställen)
   - Income (inkomster)
   - etc.

3. **Optimize for scale**:
   - Consider LOD (Level of Detail) for mesh sizes
   - Implement streaming for large datasets
   - Profile performance for different strategies

4. **Document domain knowledge**:
   - Swedish field names & translations
   - Data quality issues per dataset
   - Recommended settings per strategy

---

## File References

- **Base Pipeline**: `Src/pipelines/base.py`
- **Mesh Generator**: `Src/mesh_generation/generator.py`
- **Strategies**: `Src/mesh_generation/strategies/*.py`
- **Data Profiler**: `Src/utils/data_profiler.py`
- **Example Scripts**: `Src/scripts/*.py`

---

**Author**: Automated Refactoring  
**Last Updated**: May 11, 2026
