# Semantic 3D Building Pipeline (Byggnad + Ortnamn)

## 1. Objective

Build a system that:
- Generates 3D building meshes from building footprints
- Assigns unique IDs to each building
- Supports interaction in Unity (hover/click)
- Displays place names (Ortnamn) for labeling/UI

---

## 2. Data Used

### Byggnad (GPKG)
Vector polygons representing building footprints.
Used for mesh generation and object separation.

### Ortnamn (GPKG)
Vector dataset of place names.
Used for UI labels in Unity.

---

## 3. Pipeline Overview

Byggnad → Python → Mesh Generation → Export → Unity → Interaction  
Ortnamn → Python → Metadata → Unity → Labels

---

## 4. Folder Structure


---

## 5. Processing Steps

### 5.1 Load & Validate Data
- Load Byggnad GPKG using geopandas
- Ensure CRS consistency (EPSG:3006)
- Check and repair geometries
- Convert all MultiPolygons to Polygons (explode if needed)

### 5.2 Assign Unique IDs & Heights
- Assign stable IDs: `bld_000001`, `bld_000002`, etc.
- Calculate centroid for each building
- Assign placeholder height (constant, e.g., 10m)
- Store bbox per building

### 5.3 Generate Mesh (PyOpenGL)
For each building:
- Triangulate the footprint polygon using Delaunay or ear-clipping
- Create roof vertices at height H above footprint
- Create wall faces connecting each footprint edge to roof edge
- Combine all vertices and faces into single Mesh object
- Use indexed geometry for efficiency

### 5.4 Export to GLB
- For each building mesh:
  - Convert vertices and indices to glTF format
  - Save as `buildings_glb/bld_XXXXXX.glb` (binary glTF)
  - Include simple material (diffuse color)

### 5.5 Create Manifest
Write `Processed_data/buildings_manifest.json`:
```json
{
  "buildings": [
    {
      "id": "bld_000001",
      "height": 10.0,
      "centroid": [639500.5, 6390200.3],
      "bbox": [639450.0, 6390150.0, 639551.0, 6390251.0],
      "mesh_path": "buildings_glb/bld_000001.glb",
      "vertex_count": 42,
      "triangle_count": 24,
      "crs": "EPSG:3006"
    }
  ],
  "metadata": {
    "total_buildings": 1234,
    "generated_at": "2026-04-16T10:30:00Z",
    "source": "byggnad_sverige.gpkg",
    "height_method": "placeholder_constant"
  }
}
```

### 5.6 Metadata
Store building info:
{
  "id": "bld_000001",
  "height": 12,
  "centroid": [x, y]
}

### 5.7 Ortnamn
Extract:
- name
- position

---

## 6. Unity

### Import
- Import meshes
- Add collider + script

### Script
public class Building : MonoBehaviour
{
    public string buildingId;
}

### Interaction
- Raycast
- Fetch ID
- Display metadata

### Labels
- Place TextMeshPro objects at Ortnamn positions

---

## 7. Result

- 3D buildings
- Selectable objects
- Hover info
- Visible place names

---

## 8. Next Steps

- Add LiDAR for real heights
- Add classification
- Add performance optimizations
