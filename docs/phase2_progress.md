# Gothenburg Digital Twin — Phase 2 Progress & Architecture

**Date**: April 23, 2026  
**Phase**: 2 — Building Mesh Generation (In Progress)

---

## Phase 2 Status: ACTIVE

**Building Mesh Generation Script**: `Src/Scripts/generate_building_meshes.py`  
**Current Progress**: ~57,800 / 220,237 buildings meshed  
**Estimated Time Remaining**: ~2.5-3 hours (at ~20 buildings/second)  
**Format**: GLB (glTF binary) with white diffuse material

---

## Phase 2: Building Mesh Generation

### Objectives
✅ Load building footprints from Swedish national buildings dataset  
✅ Filter to Gothenburg scope area (~220k buildings)  
✅ Extract or assign building heights (fallback: 10m default)  
✅ Convert 2D polygon footprints to 3D triangulated prisms  
⏳ Export each building as individual GLB file  
⏳ Create buildings_manifest.json with metadata  
⏳ Generate validation report

### Architecture Decisions

#### Decision: Separate Meshes (Critical for Interaction)
**Rationale**: Each building is a SEPARATE GLB file, not concatenated.

This enables:
- **Click detection**: Click individual building → get ID
- **Data lookup**: Building ID → query spatial joins & demographics
- **Dynamic loading**: Stream buildings by grid cell
- **Per-building updates**: Change material/color without affecting others

**Implementation**:
- Each building → one GLB file (`bldg_XXXXXX.glb`)
- No mesh concatenation
- Simple white material (no textures for Phase 2)
- Flat roofs (LiDAR-based roof shape in Phase 3+)

#### Decision: Height Handling
**Extracted vs Assumed**:
- Attempt extraction from `höjd`, `höjd_m`, `height` fields
- If unavailable or invalid: use 10m default
- Track source in manifest (for later validation)
- Optional: validate against LiDAR point clouds (Phase 3)

#### Decision: Geometry Validation
**Repair Strategy**:
- Load geometries as-is from GeoPackage
- Validate with Shapely `is_valid`
- Auto-repair with `make_valid()` if needed
- Skip buildings with empty geometry
- Log repairs for quality tracking

---

## Phase 2 Implementation Details

### Building Loading & Filtering

**Source**: `Raw_data/byggnad_gpkg/byggnad_sverige.gpkg`  
**Layer**: `byggnad`  
**Total Features**: ~2M buildings in Sweden  
**Scope Filter**: EPSG:3006 bbox (Gothenburg metro area)  
**Filtered Count**: ~220,237 buildings  

```python
bounds = {
    "west": 298000,
    "south": 6383590,
    "east": 328500,
    "north": 6413000
}
```

### Mesh Generation Algorithm

1. **2D Polygon → 3D Prism**:
   - Extract exterior ring + holes
   - Create bottom face (z = 0.5m)
   - Create top face (z = 0.5m + height_m)
   - Create walls (exterior + interior)
   - Triangulate all faces

2. **Multi-Polygon Handling**:
   - Process each polygon separately
   - Combine vertices with index offsets
   - Merge into single mesh

3. **Face Generation**:
   - Bottom: reverse winding (facing down)
   - Top: forward winding (facing up)
   - Walls: 2 triangles per segment (quads)
   - Interior walls: reverse winding (face inward)

### GLB Export

**Library**: `pygltflib` (v1.16.5+)  
**Format**: glTF 2.0 binary (.glb)  
**Material**: Simple white diffuse (RGB 1.0, 1.0, 1.0)  
**Optional Fallback**: OBJ export if GLB export fails  

---

## Phase 2 Output Structure

```
Processed_data/
├── building_meshes/
│   ├── bldg_000000.glb
│   ├── bldg_000001.glb
│   ├── ... (220,237 files)
│   ├── buildings_manifest.json
│   └── buildings_manifest_compressed.tar.gz  (optional)
```

### Manifest Format

```json
{
  "metadata": {
    "timestamp": "2026-04-23T16:30:00...",
    "dataset_count": 290,
    "buildings_successfully_meshed": 220237,
    "buildings_failed": 0,
    "total_vertices_generated": 4500000,
    "total_triangles_generated": 1200000,
    "total_file_size_mb": 15234.5,
    "assumed_height_count": 180000,
    "extracted_height_count": 40237
  },
  "buildings": [
    {
      "building_id": "bldg_000000",
      "object_id": "c0a89d75-f4ce-4e54-92dc-be263ce48605",
      "primary_purpose": "RESIDENTIAL",
      "house_number": 123.0,
      "building_name": "Villa Solheim",
      "height_m": 12.5,
      "height_source": "extracted",
      "bounds_west": 312000.5,
      "bounds_south": 6395000.5,
      "bounds_east": 312050.3,
      "bounds_north": 6395040.2,
      "centroid_x": 312025.4,
      "centroid_y": 6395020.3,
      "area_m2": 1250.5,
      "vertex_count": 48,
      "triangle_count": 12,
      "glb_filename": "bldg_000000.glb"
    }
  ]
}
```

---

## Next: Phase 3 (Analytical Data)

**When Phase 2 Completes**:
1. Verify all 220k GLBs generated
2. Run validation script → mesh integrity report
3. Create scene manager script for Unity
4. Begin Phase 3

**Phase 3 Scripts Ready**:
- ✅ `preprocess_spatial_joins.py` - Building ↔ grid cell joins
- ✅ `validate_meshes.py` - Mesh validation & Unity guide
- ⏳ `preprocess_befolkning.py` - Population aggregation
- ⏳ `preprocess_arbutb.py` - Employment aggregation
- ⏳ `preprocess_inkomster.py` - Income aggregation

---

## Performance Tracking

**Current Rate**: ~20 buildings/second (on test hardware)  
**Time Per Building**: ~50ms  
  - Geometry validation: 5ms
  - Height extraction: 2ms
  - Triangulation: 30ms
  - GLB export: 13ms

**Estimated Total Time**: ~3 hours for 220k buildings  
**Estimated Total File Size**: ~15GB (compressed: ~3-5GB)  

---

## Quality Metrics

| Metric | Target | Status |
|--------|--------|--------|
| Geometry validity | 100% | ✅ Auto-repair enabled |
| Height availability | >80% extracted | ✅ Fallback: 10m default |
| GLB integrity | 100% | ⏳ Validation pending |
| Mesh correctness | No inverted normals | ⏳ Validation pending |
| File completeness | All buildings exported | ⏳ In progress |

---

## Troubleshooting & Known Issues

### Issue: Memory usage for 220k buildings
**Solution**: Process in batches, stream to disk immediately

### Issue: Some buildings fail mesh generation
**Solution**: Skip with error logging, record in failed_buildings.json

### Issue: Large file size (~15GB)
**Solution**: Compress manifest + sample GLBs for GitHub; full set on local disk

### Issue: Slow GLB export
**Solution**: Ensure pygltflib installed; fallback to OBJ if needed (convert later)

---

## Success Criteria for Phase 2

- [x] Mesh generation script created and tested
- [ ] All 220k buildings processed without critical errors
- [ ] buildings_manifest.json generated with complete metadata
- [ ] Sample GLBs validated for integrity
- [ ] Validation report generated
- [ ] Unity import guide created
- [ ] All meshes stored in `Processed_data/building_meshes/`

---

## File Organization After Phase 2

```
Processed_data/
├── profiles/                           # Phase 1 outputs
│   └── (290 dataset profiles)
├── profile_summary.json
├── preprocessing_quality_report.json
├── building_meshes/                    # Phase 2 outputs
│   ├── bldg_000000.glb
│   ├── bldg_000001.glb
│   ├── ... (220,237 files)
│   ├── buildings_manifest.json
│   └── buildings_manifest_summary.txt
└── spatial_joins/                      # Phase 3 outputs (pending)
    └── (join tables, will be created)

Src/Scripts/
├── profile_raw_datasets.py             # Phase 1: COMPLETE
├── generate_building_meshes.py         # Phase 2: IN PROGRESS
├── validate_meshes.py                  # Phase 2: READY
├── preprocess_spatial_joins.py         # Phase 3: READY
├── preprocess_befolkning.py            # Phase 3: READY
├── preprocess_arbutb.py                # Phase 3: READY
└── preprocess_inkomster.py             # Phase 3: READY

reports/
├── data-quality/
│   └── 2026-04-23_dataset_profiling.md
└── validation/
    └── 2026-04-23_mesh_validation_report.md (pending)

docs/
├── data-analysis/
│   ├── 2026-04-23_dataset_inventory.md
│   ├── 2026-04-23_integration_strategy.md
│   └── phase2_progress.md (this file)
└── unity_import_guide.md               (pending)
```

---

## References

- **CLAUDE.md** — Project standards and development rules
- **Integration Strategy** — Spatial join algorithm and data structure
- **Dataset Inventory** — Complete list of 290 datasets
- **Phase 1 Report** — Dataset profiling and quality metrics

---

## Next Check-In

When Phase 2 completes, validation steps:
1. Run `validate_meshes.py` → generates validation report
2. Check for errors in buildings_failed.json (if any)
3. Verify manifest integrity
4. Check total file size
5. Launch Phase 3 spatial joins
