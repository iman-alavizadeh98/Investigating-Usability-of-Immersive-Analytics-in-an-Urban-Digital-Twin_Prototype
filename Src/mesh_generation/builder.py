"""
Mesh Builder: Core logic for converting 2D building geometries to 3D meshes.

Handles:
- 2D polygon to 3D prism conversion
- Triangle generation
- Vertex and face management
- GLB export
"""

from pathlib import Path
from typing import Tuple, Optional
import numpy as np
from shapely.geometry import Polygon, MultiPolygon
from shapely.validation import make_valid
import logging

logger = logging.getLogger(__name__)


class MeshBuilder:
    """Build 3D meshes from building geometries."""
    
    def __init__(self, config=None):
        """
        Initialize builder.
        
        Args:
            config: Configuration with assumed_height_m, terrain_offset_m, material_color
        """
        self.config = config or {}
        self.assumed_height_m = self.config.get("assumed_height_m", 10.0)
        self.terrain_offset_m = self.config.get("terrain_offset_m", 0.5)
        self.material_color = self.config.get("material_color", (1.0, 1.0, 1.0))
    
    def polygon_to_triangles(
        self,
        polygon: Polygon,
        height_m: float
    ) -> Tuple[np.ndarray, np.ndarray]:
        """
        Convert a 2D polygon to 3D triangulated prism (walls + flat roof).
        
        Args:
            polygon: 2D polygon (exterior ring + holes)
            height_m: Building height in meters
        
        Returns:
            (vertices, faces)
            - vertices: (N, 3) array of vertex coordinates [x, y, z]
            - faces: (M, 3) array of triangle indices
        """
        vertices_list = []
        faces_list = []
        
        z_base = self.terrain_offset_m
        z_roof = z_base + height_m
        
        # Extract exterior ring and holes
        exterior = polygon.exterior.coords[:-1]  # Remove duplicate closing point
        holes = [np.array(hole.coords[:-1]) for hole in polygon.interiors]
        
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
        # Bottom face (triangulated using fan triangulation)
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
    
    def multipolygon_to_triangles(
        self,
        geom: MultiPolygon,
        height_m: float
    ) -> Tuple[np.ndarray, np.ndarray]:
        """Convert MultiPolygon to combined mesh."""
        all_vertices = []
        all_faces = []
        vertex_offset = 0
        
        for polygon in geom.geoms:
            if isinstance(polygon, Polygon):
                vertices, faces = self.polygon_to_triangles(polygon, height_m)
                all_vertices.append(vertices)
                all_faces.append(faces + vertex_offset)
                vertex_offset += len(vertices)
        
        if all_vertices:
            combined_vertices = np.vstack(all_vertices)
            combined_faces = np.vstack(all_faces)
            return combined_vertices, combined_faces
        else:
            return np.empty((0, 3), dtype=np.float32), np.empty((0, 3), dtype=np.uint32)
    
    def geometry_to_triangles(
        self,
        geometry,
        height_m: float
    ) -> Tuple[Optional[np.ndarray], Optional[np.ndarray]]:
        """Convert any geometry to triangles."""
        
        if geometry.is_empty:
            return None, None
        
        if not geometry.is_valid:
            geometry = make_valid(geometry)
        
        if isinstance(geometry, Polygon):
            return self.polygon_to_triangles(geometry, height_m)
        elif isinstance(geometry, MultiPolygon):
            return self.multipolygon_to_triangles(geometry, height_m)
        else:
            logger.warning(f"Unsupported geometry type: {type(geometry)}")
            return None, None
    
    def export_glb(
        self,
        output_path: Path,
        vertices: np.ndarray,
        faces: np.ndarray,
        building_id: str
    ) -> bool:
        """
        Export mesh to GLB (glTF binary) format.
        
        Uses pygltflib for simple mesh export.
        """
        try:
            import pygltflib
        except ImportError:
            logger.error("pygltflib not installed; falling back to OBJ export")
            return self.export_obj(output_path, vertices, faces, building_id)
        
        try:
            # Create glTF model
            gltf = pygltflib.GLTF2()
            
            # Create material (white, matte)
            material = pygltflib.Material(
                name=f"{building_id}_Material",
                pbrMetallicRoughness=pygltflib.PbrMetallicRoughness(
                    baseColorFactor=list(self.material_color) + [1.0],  # RGBA
                    metallicFactor=0.0,
                    roughnessFactor=0.8
                )
            )
            gltf.materials.append(material)
            
            # Create mesh primitive
            mesh_primitive = pygltflib.Primitive(
                attributes=pygltflib.Attributes(POSITION=0),
                indices=1,
                material=0
            )
            
            mesh = pygltflib.Mesh(name=building_id, primitives=[mesh_primitive])
            gltf.meshes.append(mesh)
            
            # Create node
            node = pygltflib.Node(name=f"{building_id}_Node", mesh=0)
            gltf.nodes.append(node)
            
            # Create scene
            scene = pygltflib.Scene(nodes=[0])
            gltf.scenes.append(scene)
            gltf.scene = 0
            
            # Create accessors and buffer views
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
            
            # Indices
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
            
            # Save
            output_path.parent.mkdir(parents=True, exist_ok=True)
            gltf.save(str(output_path))
            
            return True
        
        except Exception as e:
            logger.error(f"Failed to export GLB: {e}")
            return False
    
    def export_obj(
        self,
        output_path: Path,
        vertices: np.ndarray,
        faces: np.ndarray,
        building_id: str
    ) -> bool:
        """Fallback: export mesh to OBJ format."""
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
            
            return True
        
        except Exception as e:
            logger.error(f"Failed to export OBJ: {e}")
            return False
