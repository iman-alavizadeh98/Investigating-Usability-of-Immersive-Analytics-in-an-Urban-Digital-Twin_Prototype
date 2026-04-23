"""
Generate 3D building meshes from Swedish building footprints.

Phase 2: Building Mesh Generation

This script:
1. Loads building footprints from GeoPackage
2. Filters to Gothenburg scope area
3. Validates and repairs geometries
4. Extracts or assigns heights
5. Converts 2D polygons to 3D triangulated prisms
6. Exports as GLB with white material
7. Creates buildings_manifest.json for Unity import

Architecture Decision: Each building is a SEPARATE mesh (not concatenated)
This enables click-interaction and per-building data lookup in Unity.
"""

import json
import logging
from pathlib import Path
from dataclasses import dataclass, asdict
from typing import List, Dict, Any, Tuple, Optional
import numpy as np
from datetime import datetime

import geopandas as gpd
import pandas as pd
from shapely.geometry import Polygon, MultiPolygon, box
from shapely.ops import unary_union
from shapely.validation import make_valid

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s'
)
logger = logging.getLogger(__name__)

# ============================================================================
# Configuration
# ============================================================================

CONFIG = {
    "input_gpkg": "Raw_data/byggnad_gpkg/byggnad_sverige.gpkg",
    "input_layer": "byggnad",
    "output_meshes_dir": "Processed_data/building_meshes",
    "output_manifest": "Processed_data/building_meshes/buildings_manifest.json",
    "target_crs": "EPSG:3006",
    "assumed_height_m": 10.0,
    "scope_bounds": {
        "west": 298000,
        "south": 6383590,
        "east": 328500,
        "north": 6413000
    },
    "max_buildings_sample": None,  # Set to integer to limit for testing
    "mesh_material_color": (1.0, 1.0, 1.0),  # White (RGB)
    "terrain_offset_m": 0.5  # Offset buildings slightly above terrain
}

# ============================================================================
# Data Classes
# ============================================================================

@dataclass
class BuildingMetadata:
    """Metadata for a single building."""
    building_id: str
    object_id: str  # UUID from original data
    primary_purpose: Optional[str]
    house_number: Optional[float]
    building_name: Optional[str]
    height_m: float
    height_source: str  # "extracted" or "assumed"
    bounds_west: float
    bounds_south: float
    bounds_east: float
    bounds_north: float
    centroid_x: float
    centroid_y: float
    area_m2: float
    vertex_count: int
    triangle_count: int
    glb_filename: str


@dataclass
class MeshGenerationReport:
    """Summary of mesh generation run."""
    timestamp: str
    total_buildings_input: int
    buildings_in_scope: int
    buildings_successfully_meshed: int
    buildings_failed: int
    total_vertices_generated: int
    total_triangles_generated: int
    total_file_size_mb: float
    assumed_height_count: int
    extracted_height_count: int
    error_messages: List[str]


# ============================================================================
# Building Loading & Filtering
# ============================================================================

def load_buildings_from_gpkg(gpkg_path: str, layer: str) -> gpd.GeoDataFrame:
    """Load buildings from GeoPackage."""
    logger.info(f"Loading buildings from {gpkg_path}::{layer}")
    gdf = gpd.read_file(gpkg_path, layer=layer)
    logger.info(f"Loaded {len(gdf)} buildings, CRS: {gdf.crs}")
    return gdf


def filter_to_scope(gdf: gpd.GeoDataFrame, bounds: Dict[str, float]) -> gpd.GeoDataFrame:
    """Filter buildings to Gothenburg scope area."""
    scope_box = box(bounds["west"], bounds["south"], bounds["east"], bounds["north"])
    gdf_in_scope = gdf[gdf.geometry.intersects(scope_box)].copy()
    logger.info(f"Filtered to scope area: {len(gdf_in_scope)} buildings in bounds")
    return gdf_in_scope


def repair_geometries(gdf: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    """Repair invalid geometries."""
    gdf = gdf.copy()
    invalid_count = (~gdf.geometry.is_valid).sum()
    
    if invalid_count > 0:
        logger.warning(f"Found {invalid_count} invalid geometries, repairing...")
        gdf.loc[~gdf.geometry.is_valid, "geometry"] = gdf.loc[
            ~gdf.geometry.is_valid, "geometry"
        ].apply(make_valid)
        logger.info("Geometry repair complete")
    
    return gdf


# ============================================================================
# Height Extraction
# ============================================================================

def extract_height_fields(row: pd.Series) -> Tuple[float, str]:
    """
    Extract height from a building row.
    
    Returns: (height_m, source)
    """
    height_source = "assumed"
    height_m = CONFIG["assumed_height_m"]
    
    # Check common Swedish building height fields
    height_fields = ["hojd", "höjd", "höjd_m", "height", "height_m"]
    
    for field in height_fields:
        if field in row.index and pd.notna(row[field]):
            try:
                h = float(row[field])
                if 0 < h < 500:  # Sanity check: height between 0-500m
                    height_m = h
                    height_source = "extracted"
                    break
            except (ValueError, TypeError):
                continue
    
    return height_m, height_source


# ============================================================================
# Mesh Generation (2D → 3D)
# ============================================================================

def polygon_to_triangles(
    polygon: Polygon,
    height_m: float,
    terrain_offset_m: float = 0.5
) -> Tuple[np.ndarray, np.ndarray]:
    """
    Convert a 2D polygon to a 3D triangulated prism (walls + flat roof).
    
    Args:
        polygon: 2D polygon (exterior ring + holes)
        height_m: Building height in meters
        terrain_offset_m: Offset buildings above terrain
    
    Returns:
        (vertices, faces)
        - vertices: (N, 3) array of vertex coordinates [x, y, z]
        - faces: (M, 3) array of triangle indices
    """
    vertices_list = []
    faces_list = []
    
    z_base = terrain_offset_m
    z_roof = z_base + height_m
    
    # Extract exterior ring and holes
    exterior = polygon.exterior.coords[:-1]  # Remove duplicate closing point
    holes = [np.array(hole.coords[:-1]) for hole in polygon.interiors]
    
    # --- BOTTOM (facing down) ---
    # Create indices for bottom vertices
    n_exterior = len(exterior)
    bottom_exterior_indices = list(range(n_exterior))
    
    bottom_hole_indices = []
    current_idx = n_exterior
    for hole in holes:
        n_hole = len(hole)
        bottom_hole_indices.append(list(range(current_idx, current_idx + n_hole)))
        current_idx += n_hole
    
    # --- VERTICES ---
    # Add exterior bottom
    for x, y in exterior:
        vertices_list.append([x, y, z_base])
    
    # Add holes bottom
    for hole in holes:
        for x, y in hole:
            vertices_list.append([x, y, z_base])
    
    # Add exterior roof
    for x, y in exterior:
        vertices_list.append([x, y, z_roof])
    
    # Add holes roof
    for hole in holes:
        for x, y in hole:
            vertices_list.append([x, y, z_roof])
    
    # --- FACES ---
    # Bottom face (triangulated using simple fan triangulation)
    # Note: For complex polygons, proper triangulation (Delaunay) would be better
    if n_exterior >= 3:
        for i in range(1, n_exterior - 1):
            # Reverse winding for bottom face (facing down)
            faces_list.append([0, i + 1, i])
    
    # Top face (roof)
    roof_base_idx = current_idx
    for i in range(1, n_exterior - 1):
        # Forward winding for top face (facing up)
        faces_list.append([roof_base_idx + i, roof_base_idx + i + 1, roof_base_idx])
    
    # Walls (exterior)
    for i in range(n_exterior):
        v0_bot = i
        v1_bot = (i + 1) % n_exterior
        v0_roof = roof_base_idx + i
        v1_roof = roof_base_idx + (i + 1) % n_exterior
        
        # Two triangles per wall segment (quad)
        faces_list.append([v0_bot, v0_roof, v1_roof])
        faces_list.append([v0_bot, v1_roof, v1_bot])
    
    # Walls (holes) - interior walls
    for hole_idx, hole_indices in enumerate(bottom_hole_indices):
        n_hole = len(hole_indices)
        hole_roof_base = roof_base_idx + sum(len(h) for h in holes[:hole_idx]) + n_exterior
        
        for i in range(n_hole):
            v0_bot = hole_indices[i]
            v1_bot = hole_indices[(i + 1) % n_hole]
            v0_roof = hole_roof_base + i
            v1_roof = hole_roof_base + (i + 1) % n_hole
            
            # Reverse winding for interior walls (they face inward)
            faces_list.append([v0_bot, v1_roof, v0_roof])
            faces_list.append([v0_bot, v1_bot, v1_roof])
    
    vertices = np.array(vertices_list, dtype=np.float32)
    faces = np.array(faces_list, dtype=np.uint32)
    
    return vertices, faces


def handle_multipolygon(
    geom: MultiPolygon,
    height_m: float,
    terrain_offset_m: float = 0.5
) -> Tuple[np.ndarray, np.ndarray]:
    """Convert MultiPolygon to combined mesh."""
    all_vertices = []
    all_faces = []
    vertex_offset = 0
    
    for polygon in geom.geoms:
        if isinstance(polygon, Polygon):
            vertices, faces = polygon_to_triangles(polygon, height_m, terrain_offset_m)
            all_vertices.append(vertices)
            all_faces.append(faces + vertex_offset)
            vertex_offset += len(vertices)
    
    if all_vertices:
        combined_vertices = np.vstack(all_vertices)
        combined_faces = np.vstack(all_faces)
        return combined_vertices, combined_faces
    else:
        return np.empty((0, 3), dtype=np.float32), np.empty((0, 3), dtype=np.uint32)


# ============================================================================
# GLB Export
# ============================================================================

def export_mesh_to_glb(
    output_path: Path,
    vertices: np.ndarray,
    faces: np.ndarray,
    building_id: str,
    color: Tuple[float, float, float] = (1.0, 1.0, 1.0)
) -> bool:
    """
    Export mesh to GLB (glTF binary) format.
    
    Uses pygltflib for simple mesh export with white material.
    """
    try:
        import pygltflib
        
        # Create glTF model
        gltf = pygltflib.GLTF2()
        
        # Create material (white, diffuse)
        material = pygltflib.Material(
            name=f"Building_{building_id}_Material",
            pbrMetallicRoughness=pygltflib.PbrMetallicRoughness(
                baseColorFactor=list(color) + [1.0],  # RGBA
                metallicFactor=0.0,
                roughnessFactor=0.8
            )
        )
        gltf.materials.append(material)
        
        # Create mesh
        mesh_primitive = pygltflib.Primitive(
            attributes=pygltflib.Attributes(
                POSITION=0  # Vertex position accessor index
            ),
            indices=1,  # Index accessor
            material=0   # Material index
        )
        
        mesh = pygltflib.Mesh(
            name=f"Building_{building_id}",
            primitives=[mesh_primitive]
        )
        gltf.meshes.append(mesh)
        
        # Create node
        node = pygltflib.Node(
            name=f"Building_{building_id}_Node",
            mesh=0
        )
        gltf.nodes.append(node)
        
        # Create scene
        scene = pygltflib.Scene(nodes=[0])
        gltf.scenes.append(scene)
        gltf.scene = 0
        
        # Create accessors and buffer views for vertex data
        # Vertices accessor
        vertices_bytes = vertices.tobytes()
        buffer_view_vertices = pygltflib.BufferView(
            buffer=0,
            byteOffset=0,
            byteLength=len(vertices_bytes),
            target=pygltflib.ARRAY_BUFFER
        )
        gltf.bufferViews.append(buffer_view_vertices)
        
        accessor_vertices = pygltflib.Accessor(
            bufferView=0,
            byteOffset=0,
            componentType=pygltflib.FLOAT,
            count=len(vertices),
            type=pygltflib.VEC3,
            min=vertices.min(axis=0).tolist(),
            max=vertices.max(axis=0).tolist()
        )
        gltf.accessors.append(accessor_vertices)
        
        # Indices accessor
        indices_bytes = faces.flatten().astype(np.uint32).tobytes()
        buffer_view_indices = pygltflib.BufferView(
            buffer=0,
            byteOffset=len(vertices_bytes),
            byteLength=len(indices_bytes),
            target=pygltflib.ELEMENT_ARRAY_BUFFER
        )
        gltf.bufferViews.append(buffer_view_indices)
        
        accessor_indices = pygltflib.Accessor(
            bufferView=1,
            byteOffset=0,
            componentType=pygltflib.UNSIGNED_INT,
            count=faces.size,
            type=pygltflib.SCALAR
        )
        gltf.accessors.append(accessor_indices)
        
        # Create buffer
        buffer_data = vertices_bytes + indices_bytes
        buffer = pygltflib.Buffer(byteLength=len(buffer_data))
        gltf.buffers.append(buffer)
        
        # Set binary data
        gltf.set_binary_blob(buffer_data)
        
        # Save to file
        output_path.parent.mkdir(parents=True, exist_ok=True)
        gltf.save(str(output_path))
        
        return True
    
    except ImportError:
        logger.error("pygltflib not available, using fallback OBJ export")
        return export_mesh_to_obj(output_path, vertices, faces, building_id)
    except Exception as e:
        logger.error(f"Failed to export GLB for {building_id}: {e}")
        return False


def export_mesh_to_obj(
    output_path: Path,
    vertices: np.ndarray,
    faces: np.ndarray,
    building_id: str
) -> bool:
    """Fallback: export mesh to OBJ format (text-based)."""
    try:
        output_path = output_path.with_suffix(".obj")
        
        with open(output_path, 'w') as f:
            f.write(f"# Building {building_id}\n")
            f.write(f"# Vertices: {len(vertices)}, Faces: {len(faces)}\n\n")
            
            # Write vertices
            for x, y, z in vertices:
                f.write(f"v {x} {y} {z}\n")
            
            # Write faces (OBJ uses 1-based indexing)
            for face in faces:
                f.write(f"f {face[0]+1} {face[1]+1} {face[2]+1}\n")
        
        logger.info(f"Exported OBJ (fallback): {output_path}")
        return True
    
    except Exception as e:
        logger.error(f"Failed to export OBJ for {building_id}: {e}")
        return False


# ============================================================================
# Main Processing Pipeline
# ============================================================================

def process_building(
    building_idx: int,
    row: pd.Series,
    geometry: Polygon | MultiPolygon,
    output_dir: Path
) -> Tuple[Optional[BuildingMetadata], Optional[str]]:
    """
    Process a single building: validate, extract height, generate mesh, export.
    
    Returns: (metadata, error_message)
    """
    try:
        # Extract building ID and metadata
        object_id = str(row.get("objektidentitet", f"building_{building_idx}"))
        building_id = f"bldg_{building_idx:06d}"
        
        primary_purpose = row.get("andamal1")
        house_number = row.get("husnummer")
        building_name = row.get("byggnadsnamn1")
        
        # Extract or assign height
        height_m, height_source = extract_height_fields(row)
        
        # Validate geometry
        if geometry.is_empty:
            return None, f"Empty geometry for {building_id}"
        
        if not geometry.is_valid:
            geometry = make_valid(geometry)
        
        # Convert to proper geometry type
        if isinstance(geometry, MultiPolygon):
            vertices, faces = handle_multipolygon(geometry, height_m)
        elif isinstance(geometry, Polygon):
            vertices, faces = polygon_to_triangles(geometry, height_m)
        else:
            return None, f"Unsupported geometry type: {type(geometry)}"
        
        if len(vertices) == 0 or len(faces) == 0:
            return None, f"No valid triangles generated for {building_id}"
        
        # Calculate metadata
        bounds = geometry.bounds  # (minx, miny, maxx, maxy)
        centroid = geometry.centroid
        area_m2 = geometry.area
        
        # Export to GLB
        glb_filename = f"{building_id}.glb"
        glb_path = output_dir / glb_filename
        
        success = export_mesh_to_glb(glb_path, vertices, faces, building_id)
        if not success:
            return None, f"Failed to export GLB for {building_id}"
        
        # Create metadata record
        metadata = BuildingMetadata(
            building_id=building_id,
            object_id=object_id,
            primary_purpose=str(primary_purpose) if primary_purpose else None,
            house_number=float(house_number) if house_number else None,
            building_name=str(building_name) if building_name else None,
            height_m=float(height_m),
            height_source=height_source,
            bounds_west=float(bounds[0]),
            bounds_south=float(bounds[1]),
            bounds_east=float(bounds[2]),
            bounds_north=float(bounds[3]),
            centroid_x=float(centroid.x),
            centroid_y=float(centroid.y),
            area_m2=float(area_m2),
            vertex_count=len(vertices),
            triangle_count=len(faces),
            glb_filename=glb_filename
        )
        
        return metadata, None
    
    except Exception as e:
        return None, f"Error processing building {building_idx}: {str(e)}"


def run_mesh_generation():
    """Main pipeline: load buildings → generate meshes → export."""
    report = MeshGenerationReport(
        timestamp=datetime.now().isoformat(),
        total_buildings_input=0,
        buildings_in_scope=0,
        buildings_successfully_meshed=0,
        buildings_failed=0,
        total_vertices_generated=0,
        total_triangles_generated=0,
        total_file_size_mb=0.0,
        assumed_height_count=0,
        extracted_height_count=0,
        error_messages=[]
    )
    
    output_dir = Path(CONFIG["output_meshes_dir"])
    output_dir.mkdir(parents=True, exist_ok=True)
    
    try:
        # Load buildings
        gdf = load_buildings_from_gpkg(CONFIG["input_gpkg"], CONFIG["input_layer"])
        report.total_buildings_input = len(gdf)
        
        # Filter to scope
        gdf = filter_to_scope(gdf, CONFIG["scope_bounds"])
        report.buildings_in_scope = len(gdf)
        
        # Repair geometries
        gdf = repair_geometries(gdf)
        
        # Limit for testing if configured
        if CONFIG["max_buildings_sample"]:
            gdf = gdf.head(CONFIG["max_buildings_sample"])
            logger.info(f"Limited to {len(gdf)} buildings for testing")
        
        # Process each building
        manifests = []
        
        for idx, (_, row) in enumerate(gdf.iterrows()):
            if idx % 100 == 0:
                logger.info(f"Processing building {idx + 1}/{len(gdf)}...")
            
            metadata, error = process_building(
                idx, row, row.geometry, output_dir
            )
            
            if metadata:
                manifests.append(metadata)
                report.buildings_successfully_meshed += 1
                report.total_vertices_generated += metadata.vertex_count
                report.total_triangles_generated += metadata.triangle_count
                
                if metadata.height_source == "extracted":
                    report.extracted_height_count += 1
                else:
                    report.assumed_height_count += 1
            
            elif error:
                report.buildings_failed += 1
                report.error_messages.append(error)
        
        # Calculate total file size
        total_size = sum(
            (output_dir / m.glb_filename).stat().st_size 
            for m in manifests 
            if (output_dir / m.glb_filename).exists()
        )
        report.total_file_size_mb = total_size / (1024 * 1024)
        
        # Save manifest
        manifest_dict = {
            "metadata": asdict(report),
            "buildings": [asdict(m) for m in manifests]
        }
        
        manifest_path = Path(CONFIG["output_manifest"])
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(manifest_path, 'w') as f:
            json.dump(manifest_dict, f, indent=2)
        
        logger.info(f"Manifest saved to {manifest_path}")
        
        # Print summary
        logger.info("=" * 70)
        logger.info("BUILDING MESH GENERATION COMPLETE")
        logger.info("=" * 70)
        logger.info(f"Input buildings: {report.total_buildings_input}")
        logger.info(f"Buildings in scope: {report.buildings_in_scope}")
        logger.info(f"Successfully meshed: {report.buildings_successfully_meshed}")
        logger.info(f"Failed: {report.buildings_failed}")
        logger.info(f"Total vertices: {report.total_vertices_generated:,}")
        logger.info(f"Total triangles: {report.total_triangles_generated:,}")
        logger.info(f"Total file size: {report.total_file_size_mb:.1f} MB")
        logger.info(f"Height sources - Extracted: {report.extracted_height_count}, Assumed: {report.assumed_height_count}")
        logger.info("=" * 70)
        
        return report
    
    except Exception as e:
        logger.error(f"Pipeline failed: {e}", exc_info=True)
        raise


if __name__ == "__main__":
    run_mesh_generation()
