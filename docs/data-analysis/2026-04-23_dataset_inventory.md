# Gothenburg Digital Twin — Dataset Inventory

**Generated**: 2026-04-23 (Phase 1 Profiling Complete)  
**Total Datasets**: 290  
**Total Features**: 253+ million

---

## Overview

This inventory catalogs all raw datasets available for the Gothenburg digital twin, organized by category and source. Each entry includes the Swedish name, English translation, format, feature count, and integration purpose.

---

## 1. Building Footprints (1 dataset)

| Swedish Name | English Name | Format | Features | CRS | Location | Purpose |
|---|---|---|---|---|---|---|
| byggnad_sverige::byggnad | Swedish Buildings | GeoPackage | ~2M | EPSG:3006 | byggnad_gpkg/ | Primary layer: 3D mesh generation, click-inspect |

**Key Fields**: objektidentitet (UUID), andamal1 (primary purpose), husnummer (house number), byggnadsnamn1 (building name)

**Status**: ✅ Ready for Phase 2 (preprocessing exists)

---

## 2. Elevation Data (162 GeoTIFF tiles)

| Tile ID Pattern | English Name | Format | Resolution | CRS | Location | Purpose |
|---|---|---|---|---|---|---|
| {NS}_{EW}_{offset_NS}_{offset_EW}_{year}.tif | Swedish 2m DEM Tiles | GeoTIFF | 2m pixels | EPSG:3006 | hojddata2m/ | Terrain mesh generation, elevation validation |

**Grid Coverage**: Swedish national grid (1.25km × 1.25km cells)  
**Year Range**: 2010-2011  
**Scope for Digital Twin**: ~40-50 tiles covering Gothenburg metro area

**Example Tiles**:
- 638_30_2550_2010.tif → Grid (638, 30) offset (2550, 2010)
- 639_31_2575_2010.tif → Grid (639, 31) offset (2575, 2010)

**Status**: ✅ Ready for Phase 3 (terrain generation)

---

## 3. Population Demographics (Ruta Grid Cells) (16 Shapefiles)

### 3.1 Age Distribution (Tab1)

| Swedish Name | English Name | Format | Cells | CRS | Location |
|---|---|---|---|---|---|
| Tab1_Ruta_2023_region | Population by Age (250m Grid) | Shapefile | 1000 | EPSG:3006 | befolkningShp/ |

**Fields**: Alder_0_6, Alder_7_15, Alder_16_1, Alder_20_2, Alder_25_4, Alder_45_6, Alder_65, Totalt

**Purpose**: Display population age structure per grid cell when querying

---

### 3.2 Population Summary (Tab2)

| Swedish Name | English Name | Format | Cells |
|---|---|---|---|
| Tab2_Ruta_2023_region | Population Summary | Shapefile | 1000 |

---

### 3.3 Education Levels (Tab10)

| Swedish Name | English Name | Format | Cells |
|---|---|---|---|
| Tab10_Ruta_2023_region | Education by Grid Cell | Shapefile | 1000 |

**Fields**: Forgymn, Gymnasial, Eftergymn2, Eftergymn3, UppgSakn, Totbef

**Purpose**: Show education attainment statistics

---

### 3.4 Employment (Tab3-Tab9, Tab11)

| Swedish Name | English Name |
|---|---|
| Tab3_Ruta_2023_region | Employment Type 1 |
| Tab4_Ruta_2023_region | Employment Type 2 |
| ... | ... |
| Tab11_Ruta_2023_region | Employment Type 9 |

**Purpose**: Display employment sector distribution

---

### 3.5 Demographic Statistical Areas (DeSO Grid)

| Swedish Name | English Name | Format | Areas |
|---|---|---|---|
| Tab1_DeSO_2023_region | Population by DeSO Unit | Shapefile | Various |
| Tab4_DeSO_2023_region | Employment by DeSO Unit | Shapefile | Various |
| ... | ... | Shapefile | ... |

**Purpose**: Neighborhood-level statistics (DO NOT consolidate with Ruta grid)

**Status**: ⏳ Ready for Phase 3 (demographic aggregation)

---

## 4. Income Data (Inkomster) (8 Shapefiles)

| Swedish Name | English Name | Format | Location |
|---|---|---|---|
| inkomster_*_Ruta_2023_region.shp | Income Statistics (Ruta) | Shapefile | inkomsterShp/ |

**Purpose**: Income distribution and socioeconomic indicators per grid cell

**Status**: ⏳ Ready for Phase 3

---

## 5. Employment Data (Arbutb) (8 Shapefiles)

| Swedish Name | English Name | Format | Location |
|---|---|---|---|
| (Already counted in Tab3-Tab11 above) | Employment/Work Statistics | Shapefile | arbutbShp/ |

**Purpose**: Detailed employment sector breakdown

**Status**: ⏳ Ready for Phase 3

---

## 6. Place Names (Ortnamn) (1 GeoPackage)

| Swedish Name | English Name | Format | Features | CRS | Location |
|---|---|---|---|---|---|
| ortnamn_sverige::ortnamn | Swedish Place Names | GeoPackage | ~300K | EPSG:3006 | ortnamn/ |

**Fields**: namn (name), objekttyp (type: city, village, landmark, etc.)

**Purpose**: Labels and geographic references for 3D scene

**Status**: ✅ Ready for Phase 3 (labeling)

---

## 7. Road Network (Vagkarta) (8 Shapefiles)

| Swedish Name | English Name | Format | Features | Location |
|---|---|---|---|---|
| vagkarta_*_sverige.shp | Swedish Road Network | Shapefile | Varies | vagkartaVektorShp/ |

**Fields**: vagnamn (road name), vagtyp (road type), funktionell (functional class)

**Purpose**: Navigation context, traffic routing reference

**Status**: ✅ Ready for Phase 3 (reference layer)

---

## 8. Property & Land Data (Fastighet)

### 8.1 Property Boundaries (Fastighet Mark)

| Swedish Name | English Name | Format | Features | Location |
|---|---|---|---|---|
| fastighetsmark_sverige.shp | Property Boundaries | Shapefile | ~2M | fastighetMarkShp/ |

**Fields**: fastighet (property ID), marktyp (land type: built-up, forest, water, etc.)

**Purpose**: Property-level context for building click-inspect

---

### 8.2 Property Communication (Fastighet Kommunikation)

| Swedish Name | English Name | Format | Location |
|---|---|---|---|
| fastighet_kommunikation_sverige.shp | Property Communication | Shapefile | fastighetKommunikation/ |

**Purpose**: Utilities, infrastructure associated with properties

---

## 9. Terrain & Topography (Terrang)

| Swedish Name | English Name | Format | Features | Location |
|---|---|---|---|---|
| terrangvektor_sverige.shp | Topographic Features | Shapefile | Various | terrangVektorShp/ |

**Purpose**: Supplementary terrain representation (contours, slopes)

---

## 10. Administrative Features (Anläggning, Hydrografi, etc.)

| Swedish Name | English Name | Features | Location |
|---|---|---|---|
| anlaggningsomrade_sverige.gpkg | Facility Areas | Various | (Multiple) |
| hydro_sverige.gpkg | Hydrographic Features | Water bodies, streams | hydro data dirs |
| naturvard_sverige.gpkg | Nature Protection Areas | Protected zones | natura data |

**Purpose**: Environmental and administrative context layers

---

## 11. LiDAR Point Cloud Data (Laserdata)

| Swedish Name | English Name | Format | Location | Status |
|---|---|---|---|---|
| laserdata_nh/ | Swedish LiDAR Scans | LAS/LAZ point clouds | laserdata_nh/ | ⏳ Optional validation |

**Purpose**: Optional - validate building heights, extract roof shapes

**Status**: Available but not required for Phase 2 (fallback to flat roofs)

---

## 12. High-Resolution Data (Not Yet Explored)

| Type | Format | Location | Status |
|---|---|---|---|
| Aerial Imagery (orthophoto) | GeoTIFF | Not in current structure | ⏳ Future |
| 3D Building Models | Various | Not provided | ⏳ Future |

---

## Data Organization Diagram

```
Raw_data/
├── byggnad_gpkg/               ← BUILDINGS (primary layer)
├── hojddata2m/                 ← ELEVATION (162 tiles)
├── befolkningShp/              ← POPULATION demographics
├── arbutbShp/                  ← EMPLOYMENT statistics
├── inkomsterShp/               ← INCOME statistics
├── ortnamn/                    ← PLACE NAMES
├── vagkartaVektorShp/          ← ROAD NETWORK
├── fastighetMarkShp/           ← PROPERTY boundaries
├── fastighetKommunikation/     ← UTILITIES/INFRASTRUCTURE
├── terrangVektorShp/           ← TOPOGRAPHY
├── topografi10_gpkg/           ← ADDITIONAL TOPO data
├── laserdata_nh/               ← LIDAR (optional)
└── (Other administrative layers)
```

---

## Integration Sequence

### Phase 2: Core 3D Geometry
1. ✅ Profile buildings (`byggnad_sverige`)
2. ⏳ Extract heights and generate 3D meshes
3. ⏳ Export as GLB (white material, no texture)
4. ⏳ Create buildings_manifest.json

### Phase 3: Analytical Layers
1. ⏳ Load population/employment/income Shapefiles
2. ⏳ Perform spatial joins (building ↔ grid cells)
3. ⏳ Aggregate demographics by building
4. ⏳ Generate info panel JSON payloads
5. ⏳ Create terrain mesh from elevation tiles
6. ⏳ Integrate place names as labels

### Phase 4: Documentation & Handoff
1. ⏳ Validate all meshes and data structures
2. ⏳ Create Unity import guide
3. ⏳ Document interactive click-inspect behavior
4. ⏳ Package deliverables for game engine

---

## Quality Metrics Summary

| Metric | Status | Details |
|--------|--------|---------|
| **Spatial Consistency** | ✅ All in EPSG:3006 | No reprojection needed |
| **Temporal Consistency** | ⚠️ Mixed (2010-2011) | Acceptable for static terrain |
| **Completeness** | ✅ 290 datasets | Full regional coverage |
| **Geometry Validity** | ✅ Validated in Phase 1 | Repaired in preprocess_byggnad.py |
| **Attribute Completeness** | ⚠️ Varies | Height field optional, fallback exists |
| **Semantic Clarity** | ✅ Translated to English | Field translations available |

---

## Field Translation Reference

See [`configs/field_translations.json`](../../configs/field_translations.json) for complete Swedish↔English mapping.

---

## Dataset Profiling Details

Individual profile JSONs available in: `Processed_data/profiles/*.json`

Consolidated summary: `Processed_data/profile_summary.json`

Markdown report: `reports/data-quality/2026-04-23_dataset_profiling.md`

---

## Key Statistics

- **Total Datasets**: 290
- **Total Features/Cells/Points**: 253+ million
- **Spatial Coverage**: Sweden (scope: Gothenburg metro ~1000 km²)
- **Primary CRS**: EPSG:3006
- **Formats**: GeoPackage (43), GeoTIFF (162), Shapefile (85)
- **Time to Profile**: ~20 minutes (including JSON serialization)

---

## Next Steps

1. ✅ **Phase 1 COMPLETE**: All datasets profiled
2. **Phase 2 READY**: Building mesh generation can begin
3. **Phase 3 WAITING**: Spatial joins and aggregation
4. **Phase 4 WAITING**: Documentation and Unity integration

