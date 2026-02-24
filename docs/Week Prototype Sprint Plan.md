# 1-Week Prototype Sprint Plan

**Duration**: 1 week (Feb 24 – Mar 2, 2026)  
**Goal**: Build a minimal viable 3D city model in Unity from raw GIS data

---

## 1. Week Overview

**Day 1–2**: GIS data processing (Python)  
**Day 3–4**: Unity scene setup & terrain import  
**Day 5–6**: Building meshes & basic textures  
**Day 7**: Camera controls, validation, polish

**Target Deliverable**: Interactive 3D city prototype (walkable scene with terrain + buildings)

---

## 2. Day-by-Day Breakdown

### Day 1: GIS Setup & Building Data Extract

**Tasks**:
1. Install Python libraries: `geopandas`, `rasterio`, `laspy`
2. Load `byggnad_sverige.gpkg` and inspect structure
3. Define study area bounds (pick 1 km² in Gothenburg, e.g., city center)
4. Clip building footprints to area → `buildings_clipped.geojson`
5. Sample heights from LiDAR (read one `.laz` file, extract max Z per building)
6. Export buildings with heights as `buildings_with_heights.geojson`

**Output**: GeoJSON with building polygons + height attributes

---

### Day 2: Terrain & Orthophoto Prep

**Tasks**:
1. Clip elevation GeoTIFF to study area → `dem_clipped.tif`
2. Normalize heights (min → 0, max → max height)
3. Export as raw heightmap (16-bit grayscale PNG or binary)
4. Clip one orthophoto tile (2018) to study area → `ortho_clipped.tif`
5. Resize to 2048×2048 (memory-efficient for quick test)
6. Generate JSON metadata with bounds, min/max height, scale info

**Output**: Heightmap + orthophoto texture + metadata JSON

---

### Day 3: Unity Project & Terrain

**Tasks**:
1. Create new Unity 3D project (URP pipeline)
2. Organize folders: `Assets/Models/`, `Assets/Textures/`, `Assets/Scripts/`, `Assets/Data/`
3. Import heightmap as terrain using ProBuilder or custom mesh
4. Create terrain material with orthophoto as albedo
5. Set up basic lighting (directional light, skybox)
6. Test: camera can move around terrain, FPS target >30

**Output**: Rendered terrain with orthophoto

---

### Day 4: Building Extrusion & Import

**Tasks**:
1. Python script: Read `buildings_with_heights.geojson`
2. Create simple 3D mesh per building (quad base + extrude to height)
3. Combine all into single `.fbx` file or chunked `.fbx` files (3–5 chunks)
4. In Unity: Import `.fbx`, position at origin
5. Create simple gray building material
6. Test: buildings visible, aligned with terrain

**Output**: Buildings rendered on terrain

---

### Day 5: Basic Interaction

**Tasks**:
1. Write `CameraController.cs` script:
   - WASD movement (speed ~5 m/s)
   - Mouse look
   - Height adjustment (simulate eye level)
2. Add coordinate transform utility (SWEREF99 → local coords)
3. Store study area metadata (center, bounds, scale)
4. Test: smooth navigation, no performance drop

**Output**: Walkable 3D prototype

---

### Day 6: Building Textures & Detail

**Tasks**:
1. Assign orthophoto texture to buildings (simple repeat or unique per building)
2. Add simple building outlines (edge highlighting or low-poly silhouettes)
3. Optional: Color-code buildings by height (debug visualization)
4. Optimize: Use texture atlasing or material batching if FPS drops
5. Test on target hardware (record FPS, identify bottlenecks)

**Output**: Textured, detailed buildings

---

### Day 7: Final Polish & Validation

**Tasks**:
1. Verify data alignment (no offsets between terrain & buildings)
2. Add simple UI: 
   - FPS counter
   - Info text (location, view direction)
   - Screenshot button
3. Create README for running the prototype
4. Package scene as `.unity` file + all assets
5. Document any issues/next steps
6. Test: walkthrough video showing 360° view

**Output**: Complete prototype ready for demo

---

## 3. Scope (In / Out)

### In Scope ✓
- Terrain mesh from elevation data
- Building footprints extruded to heights
- Orthophoto texture mapping
- WASD camera controls
- Basic materials (no advanced PBR)
- Single study area, single timestamp

### Out of Scope ✗
- LOD optimization (do simple single-mesh first)
- Temporal data (2009 vs. 2018)
- Interactive UI (info cards, measurements)
- VR/immersive features
- User evaluation
- Advanced lighting (no baked shadows)

---

## 4. Tools & Commands

### Python (Days 1–2)

```bash
pip install geopandas rasterio laspy numpy pillow
python clip_buildings.py
python extract_heights.py
python process_ortho.py