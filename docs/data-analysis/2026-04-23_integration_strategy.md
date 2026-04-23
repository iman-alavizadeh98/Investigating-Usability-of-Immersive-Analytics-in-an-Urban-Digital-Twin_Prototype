# Gothenburg Digital Twin — Data Integration Strategy

**Document Date**: 2026-04-23  
**Phase**: 1 (Data Discovery & Profiling) Complete → Phase 2 (Building Meshes) Ready to Begin

---

## 1. Overview

This document outlines the spatial integration strategy for combining 290+ raw datasets into a coherent 3D digital twin of Gothenburg. Integration follows four principles:

1. **Preserve Semantics**: Maintain original Swedish data context and field meanings
2. **Observable Pipeline**: Track all transformations reproducibly
3. **Separate Concerns**: Keep DeSO and Ruta grids separate (do NOT consolidate)
4. **Unity Compatibility**: Export structures that enable interactive click-to-inspect gameplay

---

## 2. Spatial Reference System (CRS)

All datasets use **EPSG:3006 (Swedish Transverse Mercator)**:
- Projection: Transverse Mercator
- Coordinate system: Swedish national grid
- Benefits: Minimal distortion in Sweden, easy distance/area calculations
- Unity Implementation: Convert to local UTM or game-world coordinates during GLB export

---

## 3. Core Dataset Layers

### 3.1 Building Footprints & Heights (Layer 1)

**Source**: `Raw_data/byggnad_gpkg/byggnad_sverige.gpkg::byggnad`  
**Format**: GeoPackage with MultiPolygon geometries  
**Feature Count**: ~1M buildings in region  
**Key Fields** (Swedish → English):

| Swedish | English | Purpose | Data Type |
|---------|---------|---------|-----------|
| `objektidentitet` | `object_id` | Unique building identifier (UUID) | String |
| `andamal1` | `primary_purpose` | Building function (e.g., RESIDENTIAL) | String |
| `husnummer` | `house_number` | Street address number | Float |
| `byggnadsnamn1` | `building_name` | Official building name | String |

**Integration Plan**:
1. Filter buildings to Gothenburg region (spatial intersection with bounding box)
2. Extract height from schema (if available) or use `ASSUMED_HEIGHT_M = 10.0`
3. Create separate mesh for each building (essential for click-interaction)
4. Generate GLB files with white material (no textures for Phase 2)

**Phase 2 Tasks**:
- [ ] Load all buildings in scope area
- [ ] Validate and repair geometry (already done in preprocess_byggnad.py)
- [ ] Triangulate 2D footprints to 3D prisms
- [ ] Export as individual GLB meshes + manifest

---

### 3.2 Elevation Data (Layer 2)

**Source**: `Raw_data/hojddata2m/` (162 GeoTIFF files)  
**Format**: GeoTIFF raster tiles (2m resolution)  
**Grid Pattern**: Swedish national grid tiles (1.25km × 1.25km cells)  
**Naming Convention**: `{NS}_{EW}_{offset_NS}_{offset_EW}_{year}.tif`

Example tiles in scope:
- `638_30_2550_2010.tif` → Grid cell (638, 30), offset (2550m, 2010m)
- `639_31_2575_2010.tif` → Adjacent cells with varying year coverage (2010-2011)

**Integration Plan**:
1. Mosaic all tiles covering Gothenburg into single elevation model
2. Use as source for terrain mesh (Phase 3)
3. Optional: validate building heights against LiDAR point clouds (laserdata_nh)

**Phase 3 Tasks**:
- [ ] Create terrain mesh from elevation raster
- [ ] Apply uniform white material (no texturing)

---

### 3.3 Population Demographics (Layer 3)

**Sources**:
- **Age distribution**: `Raw_data/befolkningShp/Tab1_Ruta_2023_region.shp`
- **Education levels**: `Raw_data/arbutbShp/Tab10_Ruta_2023_region.shp`
- **Employment**: `Raw_data/arbutbShp/Tab*_Ruta_2023_region.shp` (multiple tables)
- **Income**: `Raw_data/inkomsterShp/Tab*_Ruta_2023_region.shp` (multiple tables)

**Geography**: 250m grid cells (Ruta) across Sweden  
**Data Structure**: Each table is a Shapefile with grid cell polygons + numeric attributes

**Key Fields Example (Age Distribution)**:

| Swedish | English | Type | Meaning |
|---------|---------|------|---------|
| `Ruta` | `grid_cell_id` | String | Unique 13-digit grid cell identifier |
| `Rutstorl` | `grid_cell_size_m` | Integer | Always 250m |
| `Alder_0_6` | `age_0_6` | Integer | Population aged 0-6 in cell |
| `Alder_7_15` | `age_7_15` | Integer | Population aged 7-15 |
| ... | ... | Integer | Other age brackets |
| `Totalt` | `total_population` | Integer | Total population in cell |

**Integration Plan**:
1. Load all Ruta grid Shapefiles
2. Join population tables on `Ruta` (grid cell ID)
3. Create spatial joins: building ↔ grid cells (for click-inspect gameplay)
4. Aggregate statistics by building (which grid cells overlap building?)

**Phase 3 Tasks**:
- [ ] Preprocess all demographic tables (consistent schema)
- [ ] Create join tables: `building_id` → `[grid_cell_ids]`
- [ ] Create join tables: `grid_cell_id` → `[building_ids]`
- [ ] Prepare JSON payload for Unity info panel

---

### 3.4 Place Names & Administrative Boundaries (Layer 4)

**Sources**:
- **Place names**: `Raw_data/ortnamn/ortnamn_sverige.gpkg`
- **Road network**: `Raw_data/vagkartaVektorShp/` (multiple Shapefiles)
- **Property/land data**: `Raw_data/fastighetMarkShp/` (land type, ownership)

**Integration Plan**:
1. Load place names (labels for 3D UI)
2. Load road network (reference for navigation in game)
3. Load property boundaries (context for building click-inspect)

**Phase 3 Tasks**:
- [ ] Extract place names within Gothenburg bounds
- [ ] Create label layer for terrain
- [ ] Integrate property information with buildings

---

### 3.5 Demographic Statistical Areas (Layer 5)

**Source**: Multiple Shapefiles with `DeSO` suffix  
**Geographic Unit**: DeSO = demographic statistical areas (neighborhood-level units)  
**Purpose**: Administrative grouping for population statistics

**Integration Plan**:
1. **Do NOT consolidate with Ruta grid** (keep separate per architecture decision)
2. Load DeSO boundaries as reference layer
3. Available for future aggregation if needed

**Note**: DeSO and Ruta serve different analytical purposes:
- **Ruta**: Regular grid for spatial analysis, click-inspect by location
- **DeSO**: Administrative units for neighborhood-level demographic reports

---

## 4. Spatial Join Strategy

### 4.1 Building ↔ Grid Cell Mapping

**Goal**: When user clicks building in Unity, display population statistics for all grid cells that overlap the building.

**Algorithm**:
```
FOR each building:
  FOR each Ruta grid cell:
    IF building.geometry.intersects(grid_cell.geometry):
      CREATE join_record(building_id, grid_cell_id, overlap_area)
      
CREATE index: building_id → [grid_cell_ids]
CREATE index: grid_cell_id → [building_ids]
```

**Output Tables**:
- `building_to_cells.json`: `{ "building_id": ["cell_1", "cell_2", ...] }`
- `cell_to_buildings.json`: `{ "cell_id": ["bldg_1", "bldg_2", ...] }`

**Storage**: In `Processed_data/spatial_joins/`

**Phase 3 Task**:
- [ ] Implement spatial join script (`preprocess_spatial_joins.py`)
- [ ] Test on sample buildings (verify correctness)
- [ ] Export join tables to JSON

---

### 4.2 Building Info Panel Structure

When user clicks a building in Unity, display:

```json
{
  "building_id": "c0a89d75-f4ce-4e54-92dc-be263ce48605",
  "name": "Building Name (if available)",
  "primary_purpose": "RESIDENTIAL",
  "house_number": 123,
  
  "population_grid_cells": [
    {
      "grid_cell_id": "3142506394750",
      "total_population": 348,
      "age_0_6": 17,
      "age_7_15": 35,
      "education_gymnasium": 142,
      "employment_sector": "Services"
    }
  ],
  
  "aggregate_population": 348,
  "aggregate_education": { "gymnasium": 142, "post_gymnasium": 181 },
  "aggregate_employment": { ... }
}
```

---

## 5. Data Quality Observations

### 5.1 Building Data (`byggnad_sverige`)
- ✅ All buildings have valid geometry (MultiPolygon)
- ✅ CRS consistent (EPSG:3006)
- ⚠️ Height field not always populated (fallback: 10m default)
- ⚠️ Some buildings missing name/purpose information
- ✅ Object IDs (UUIDs) are unique and stable

### 5.2 Elevation Data (`hojddata2m`)
- ✅ Consistent 2m resolution across all tiles
- ✅ All tiles in EPSG:3006
- ⚠️ Some tiles from 2010, others from 2011 (minor temporal inconsistency acceptable)
- ⚠️ Mosaic needed to create seamless DEM

### 5.3 Population Data (Ruta grids)
- ✅ Consistent grid cell size (250m)
- ✅ All cells have valid geometry (Polygon)
- ✅ Complete coverage of Sweden
- ⚠️ Some cells with zero population (uninhabited areas, OK)
- ✅ Numeric fields validated (no NaN, consistent data types)

---

## 6. Phase 2 Prerequisites

Before starting building mesh generation:

- [x] Phase 1: Profile all datasets (COMPLETE)
- [x] Extract building footprints for Gothenburg region
- [ ] Validate `preprocess_byggnad.py` runs successfully
- [ ] Confirm height field extraction (or use default)
- [ ] Set up GLB export pipeline

---

## 7. Phase 3 Prerequisites

Before starting analytical dataset preparation:

- [ ] Complete Phase 2 (building meshes)
- [ ] Load all demographic tables (Tab1, Tab2, ... Tab11 for both Ruta and DeSO)
- [ ] Create schema-normalized version of each table
- [ ] Implement spatial join algorithm
- [ ] Generate building↔grid_cell join tables
- [ ] Create aggregate statistics payload

---

## 8. Known Limitations & Mitigations

| Issue | Impact | Mitigation |
|-------|--------|-----------|
| Building heights incomplete | 3D appearance | Use 10m default, validate with LiDAR if needed |
| Elevation tiles from 2 different years | Minor DEM inconsistency | Acceptable (2010-2011 difference negligible) |
| No building roof shapes | Flat-top appearance | Phase 2 will investigate LiDAR point clouds |
| Population data at 250m grid level | Loss of street-level detail | Acceptable for analytical view; aggregate by building |
| No texture/material data | Monochrome meshes | Acceptable for Phase 2; textures can be added later |

---

## 9. File Organization Summary

```
Processed_data/
├── profiles/                    # Individual dataset JSONs (Phase 1)
│   ├── byggnad_sverige_...
│   ├── Tab1_Ruta_...
│   └── (300+ files)
├── profile_summary.json         # Consolidated metadata (Phase 1)
├── building_meshes/             # GLB exports (Phase 2)
│   ├── building_{id}.glb
│   └── buildings_manifest.json
├── spatial_joins/               # Mapping tables (Phase 3)
│   ├── building_to_cells.json
│   └── cell_to_buildings.json
└── analytics/                   # Population/employment aggregates (Phase 3)
    ├── building_demographics.json
    └── grid_cell_demographics.json

Src/Scripts/
├── profile_raw_datasets.py      # Phase 1 profiler (COMPLETE)
├── preprocess_byggnad.py        # Building validation (existing)
├── generate_building_meshes.py  # Phase 2 (TO CREATE)
├── export_building_meshes_glb.py # Phase 2 (TO CREATE)
├── preprocess_befolkning.py     # Population prep (Phase 3)
├── preprocess_arbutb.py         # Employment prep (Phase 3)
├── preprocess_inkomster.py      # Income prep (Phase 3)
├── preprocess_spatial_joins.py  # Spatial mapping (Phase 3)
└── integrate_datasets.py        # Final assembly (Phase 3)
```

---

## 10. Next Actions

**Immediate (Phase 2)**:
1. ✅ Phase 1 complete — all datasets profiled
2. [ ] Review and validate `preprocess_byggnad.py`
3. [ ] Implement `generate_building_meshes.py`
4. [ ] Implement `export_building_meshes_glb.py`
5. [ ] Generate 100 sample building GLBs for Unity testing

**Phase 3**:
1. [ ] Implement spatial join algorithm
2. [ ] Preprocess population/employment/income tables
3. [ ] Generate join tables
4. [ ] Create info panel JSON payloads

---

## References

- [Phase 1 Profiling Report](../reports/data-quality/2026-04-23_dataset_profiling.md)
- [Field Translations](../../configs/field_translations.json)
- [CLAUDE.md Standards](../../CLAUDE.md)
- [Architecture Decisions](../../docs/Documentation%20System%20&%20Copilot%20Session.md)
