"""
Mesh Builder: Core logic for converting 2D building geometries to 3D meshes.

Handles:
- 2D polygon to 3D prism conversion
- Triangle generation
- Vertex and face management
- GLB export
"""

from pathlib import Path
from typing import List, Tuple, Optional
import numpy as np
from shapely.geometry import Polygon, MultiPolygon
from shapely.validation import make_valid
import logging

logger = logging.getLogger(__name__)

# mapbox_earcut gives correct cap triangulation for concave footprints AND holes.
# If it is not installed we fall back to naive fan triangulation (only correct for
# convex, hole-free polygons) and warn once.
try:
    import mapbox_earcut as _earcut
    _HAS_EARCUT = True
except ImportError:  # pragma: no cover - depends on environment
    _earcut = None
    _HAS_EARCUT = False

_warned_no_earcut = False


def _triangulate_cap(
    exterior: np.ndarray,
    holes: List[np.ndarray],
) -> np.ndarray:
    """
    Triangulate a (possibly concave, possibly holed) polygon footprint in 2D.

    Args:
        exterior: (n_exterior, 2) array of exterior ring vertices (no closing dup)
        holes: list of (n_hole, 2) arrays, one per interior ring (no closing dup)

    Returns:
        (T, 3) int array of triangle indices into the concatenated vertex order
        [exterior, hole_0, hole_1, ...] — i.e. the same local ordering the prism
        builder uses for the bottom ring vertices. Indices are local (0-based,
        relative to the start of this polygon's exterior ring).

    Uses mapbox_earcut when available (handles concavity + holes correctly);
    otherwise falls back to fan triangulation of the exterior only (legacy
    behaviour, correct only for convex hole-free polygons).
    """
    global _warned_no_earcut

    if _HAS_EARCUT:
        # earcut expects all rings stacked as float64 (N, 2), plus ring-end offsets.
        rings = [np.asarray(exterior, dtype=np.float64)]
        rings.extend(np.asarray(h, dtype=np.float64) for h in holes)
        verts = np.vstack(rings)
        # Ring boundary offsets: cumulative vertex counts marking the end of each ring.
        ring_ends = np.cumsum([len(r) for r in rings]).astype(np.uint32)
        indices = _earcut.triangulate_float64(verts, ring_ends)
        return np.asarray(indices, dtype=np.uint32).reshape(-1, 3)

    # Fallback: fan triangulation of exterior ring only (ignores holes/concavity).
    if not _warned_no_earcut:
        logger.warning(
            "mapbox_earcut not installed; roof/floor caps use fan triangulation, "
            "which is only correct for convex hole-free footprints. "
            "Install mapbox_earcut for correct caps."
        )
        _warned_no_earcut = True
    n_exterior = len(exterior)
    fan = [[0, i, i + 1] for i in range(1, n_exterior - 1)]
    return np.asarray(fan, dtype=np.uint32).reshape(-1, 3)


def weld_vertices(
    vertices: np.ndarray,
    faces: np.ndarray,
    decimals: int = 4,
) -> Tuple[np.ndarray, np.ndarray]:
    """
    Merge coincident vertices so a mesh becomes topologically connected.

    Building prisms are built with duplicated corner vertices (bottom/roof rings
    are separate points, and each merged building keeps its own vertex block).
    Welding by rounded position lets the watertight edge test see shared edges.

    Args:
        vertices: (N, 3) float array
        faces: (M, 3) int array
        decimals: rounding precision (1e-4 m = 0.1 mm) for treating points as equal

    Returns:
        (welded_vertices, remapped_faces)
    """
    if len(vertices) == 0:
        return vertices, faces
    keys = np.round(vertices.astype(np.float64), decimals)
    _, unique_idx, inverse = np.unique(
        keys, axis=0, return_index=True, return_inverse=True
    )
    welded = vertices[unique_idx]
    remapped = inverse[faces].astype(faces.dtype)
    return welded, remapped


def check_watertight(vertices: np.ndarray, faces: np.ndarray) -> dict:
    """
    Check whether a mesh is a closed, edge-manifold surface.

    A closed manifold has every undirected edge shared by exactly two triangles.
    Vertices are welded by position first (see weld_vertices) because the prism
    builder emits duplicated corner vertices.

    Args:
        vertices: (N, 3) float array
        faces: (M, 3) int array

    Returns:
        dict: {
            "is_watertight": bool,
            "boundary_edges": int,   # edges used by exactly 1 face (holes/gaps)
            "nonmanifold_edges": int,# edges used by >2 faces
            "degenerate_faces": int, # faces with a repeated vertex (zero area)
        }
    """
    result = {
        "is_watertight": False,
        "boundary_edges": 0,
        "nonmanifold_edges": 0,
        "degenerate_faces": 0,
    }
    if len(faces) == 0:
        return result

    welded_v, welded_f = weld_vertices(vertices, faces)

    # Drop degenerate faces (a repeated vertex index -> zero-area triangle).
    non_degen_mask = (
        (welded_f[:, 0] != welded_f[:, 1])
        & (welded_f[:, 1] != welded_f[:, 2])
        & (welded_f[:, 0] != welded_f[:, 2])
    )
    result["degenerate_faces"] = int((~non_degen_mask).sum())
    f = welded_f[non_degen_mask]
    if len(f) == 0:
        return result

    # Build undirected edge list; each triangle contributes 3 edges.
    edges = np.vstack([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]])
    edges = np.sort(edges, axis=1)  # undirected: (min, max)
    _, counts = np.unique(edges, axis=0, return_counts=True)

    boundary = int((counts == 1).sum())
    nonmanifold = int((counts > 2).sum())
    result["boundary_edges"] = boundary
    result["nonmanifold_edges"] = nonmanifold
    result["is_watertight"] = (boundary == 0 and nonmanifold == 0)
    return result


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
        height_m: float,
        origin: tuple = None
    ) -> Tuple[np.ndarray, np.ndarray]:
        """
        Convert a 2D polygon to 3D triangulated prism (walls + flat roof).
        
        Args:
            polygon: 2D polygon (exterior ring + holes)
            height_m: Building height in meters
            origin: Optional (x, y) tuple to subtract from all vertices for local-origin rebasing
        
        Returns:
            (vertices, faces)
            - vertices: (N, 3) array of vertex coordinates [x, y, z], optionally rebased
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

        # Guard: a ring with < 3 distinct vertices, or effectively zero area,
        # cannot form a closed prism. Building walls without valid caps would
        # produce an open (non-watertight) sliver, so skip such footprints.
        n_distinct = len({(round(x, 6), round(y, 6)) for x, y in exterior})
        if n_exterior < 3 or n_distinct < 3 or polygon.area < 1e-6:
            logger.warning(
                f"Skipping degenerate footprint: {n_exterior} ring vertices "
                f"({n_distinct} distinct), area={polygon.area:.3g}"
            )
            return (
                np.empty((0, 3), dtype=np.float32),
                np.empty((0, 3), dtype=np.uint32),
            )
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
        # Cap triangulation: correct for concave footprints AND interior holes.
        # cap_tris indexes into the bottom-ring vertex order [exterior, hole_0, ...],
        # which matches local indices 0 .. current_idx-1.
        roof_base_idx = current_idx
        if n_exterior >= 3:
            exterior_arr = np.asarray(exterior, dtype=np.float64)
            cap_tris = _triangulate_cap(exterior_arr, holes)

            # Bottom face: reverse winding so it faces down (-z).
            for a, b_, c in cap_tris:
                faces_list.append([int(a), int(c), int(b_)])

            # Top face (roof): same triangulation shifted to roof vertices,
            # keep CCW winding so it faces up (+z).
            for a, b_, c in cap_tris:
                faces_list.append(
                    [roof_base_idx + int(a), roof_base_idx + int(b_), roof_base_idx + int(c)]
                )
        
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
        
        # Apply local-origin rebasing if provided
        if origin is not None:
            vertices[:, 0] -= origin[0]
            vertices[:, 1] -= origin[1]
        
        faces = np.array(faces_list, dtype=np.uint32)
        
        return vertices, faces
    
    def multipolygon_to_triangles(
        self,
        geom: MultiPolygon,
        height_m: float,
        origin: tuple = None
    ) -> Tuple[np.ndarray, np.ndarray]:
        """Convert MultiPolygon to combined mesh."""
        all_vertices = []
        all_faces = []
        vertex_offset = 0
        
        for polygon in geom.geoms:
            if isinstance(polygon, Polygon):
                vertices, faces = self.polygon_to_triangles(polygon, height_m, origin=origin)
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
        height_m: float,
        origin: tuple = None
    ) -> Tuple[Optional[np.ndarray], Optional[np.ndarray]]:
        """Convert any geometry to triangles, optionally with local-origin rebasing."""
        
        if geometry.is_empty:
            return None, None

        if not geometry.is_valid:
            geometry = make_valid(geometry)

        if isinstance(geometry, Polygon):
            return self.polygon_to_triangles(geometry, height_m, origin=origin)
        elif isinstance(geometry, MultiPolygon):
            return self.multipolygon_to_triangles(geometry, height_m, origin=origin)
        elif geometry.geom_type == "GeometryCollection":
            # make_valid() can return a collection mixing polygons with lines/points
            # (e.g. from self-intersecting footprints). Mesh only the polygonal parts;
            # dropping the 0/1-D leftovers keeps the result watertight.
            polys = [g for g in geometry.geoms if isinstance(g, (Polygon, MultiPolygon))]
            if not polys:
                logger.warning("GeometryCollection has no polygonal parts; skipping")
                return None, None
            if len(polys) == 1:
                return self.geometry_to_triangles(polys[0], height_m, origin=origin)
            return self.multipolygon_to_triangles(
                MultiPolygon(
                    [p for g in polys for p in (g.geoms if isinstance(g, MultiPolygon) else [g])]
                ),
                height_m,
                origin=origin,
            )
        else:
            logger.warning(f"Unsupported geometry type: {type(geometry)}")
            return None, None
    
    def compute_normals(self, vertices: np.ndarray, faces: np.ndarray) -> np.ndarray:
        """
        Compute per-vertex normals from face data.
        
        Args:
            vertices: (N, 3) array of vertex positions
            faces: (M, 3) array of face indices
        
        Returns:
            normals: (N, 3) array of per-vertex normals
        """
        normals = np.zeros_like(vertices)
        
        for face in faces:
            v0, v1, v2 = vertices[face]
            edge1 = v1 - v0
            edge2 = v2 - v0
            face_normal = np.cross(edge1, edge2)
            face_norm = np.linalg.norm(face_normal)
            if face_norm > 1e-8:
                face_normal = face_normal / face_norm
            
            normals[face] += face_normal
        
        # Normalize all vertex normals
        for i in range(len(normals)):
            norm = np.linalg.norm(normals[i])
            if norm > 1e-8:
                normals[i] = normals[i] / norm
        
        return normals.astype(np.float32)
    
    def export_glb(
        self,
        output_path: Path,
        vertices: np.ndarray,
        faces: np.ndarray,
        building_id: str,
        normals: np.ndarray = None
    ) -> bool:
        """
        Export mesh to GLB (glTF binary) format with optional normals.
        
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
            attributes_dict = {"POSITION": 0}
            accessor_count = 1
            
            # Add normals if provided
            if normals is not None:
                attributes_dict["NORMAL"] = 1
                accessor_count = 2
            
            mesh_primitive = pygltflib.Primitive(
                attributes=pygltflib.Attributes(**attributes_dict),
                indices=accessor_count,
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
            
            # Normals buffer (if provided)
            if normals is not None:
                normals_bytes = normals.tobytes()
                buffer_view_normals = pygltflib.BufferView(
                    buffer=0,
                    byteOffset=len(vertices_bytes),
                    byteLength=len(normals_bytes),
                    target=pygltflib.ARRAY_BUFFER
                )
                gltf.bufferViews.append(buffer_view_normals)
                
                accessor_normals = pygltflib.Accessor(
                    bufferView=1,
                    byteOffset=0,
                    componentType=pygltflib.FLOAT,
                    count=len(normals),
                    type=pygltflib.VEC3
                )
                gltf.accessors.append(accessor_normals)
            
            # Indices
            indices_bytes = faces.flatten().astype(np.uint32).tobytes()
            buffer_view_idx_offset = 2 if normals is not None else 1
            buffer_view_indices = pygltflib.BufferView(
                buffer=0,
                byteOffset=len(vertices_bytes) + (len(normals_bytes) if normals is not None else 0),
                byteLength=len(indices_bytes),
                target=pygltflib.ELEMENT_ARRAY_BUFFER
            )
            gltf.bufferViews.append(buffer_view_indices)
            
            accessor_indices = pygltflib.Accessor(
                bufferView=buffer_view_idx_offset,
                byteOffset=0,
                componentType=pygltflib.UNSIGNED_INT,
                count=faces.size,
                type=pygltflib.SCALAR
            )
            gltf.accessors.append(accessor_indices)
            
            # Create buffer
            buffer_data = vertices_bytes + (normals_bytes if normals is not None else b'') + indices_bytes
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
    
    def export_glb_suite(
        self,
        output_dir: Path,
        vertices_lod: dict,
        group_id: str,
        compute_normals_per_lod: bool = True
    ) -> dict:
        """
        Export LOD1, LOD2, and LOD3 to separate GLB files with shared metadata.
        
        Args:
            output_dir: Directory to save GLB files
            vertices_lod: dict with keys "lod1", "lod2", "lod3" 
                         each containing (vertices, faces) tuple
            group_id: Group/building identifier for naming
            compute_normals_per_lod: If True, compute normals for each LOD level
        
        Returns:
            dict with export results:
            {
                "lod1": {"filename": "...", "success": True, "file_size_mb": ...},
                "lod2": {...},
                "lod3": {...}
            }
        """
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        result = {}
        
        for lod_level in ["lod1", "lod2", "lod3"]:
            if lod_level not in vertices_lod:
                logger.warning(f"LOD level {lod_level} not provided; skipping")
                continue
            
            vertices, faces = vertices_lod[lod_level]
            
            # Compute normals if requested
            normals = None
            if compute_normals_per_lod:
                normals = self.compute_normals(vertices, faces)
            
            # Generate filename
            glb_filename = f"{group_id}_{lod_level}.glb"
            glb_path = output_dir / glb_filename
            
            # Export
            success = self.export_glb(
                glb_path,
                vertices,
                faces,
                f"{group_id}_{lod_level}",
                normals=normals
            )
            
            if success:
                file_size_mb = glb_path.stat().st_size / (1024 * 1024)
                result[lod_level] = {
                    "filename": glb_filename,
                    "success": True,
                    "file_size_mb": round(file_size_mb, 2),
                    "vertex_count": len(vertices),
                    "face_count": len(faces)
                }
                logger.debug(f"  ✓ {glb_filename} ({file_size_mb:.2f} MB)")
            else:
                result[lod_level] = {
                    "filename": glb_filename,
                    "success": False,
                    "error": "GLB export failed"
                }
                logger.warning(f"  ✗ Failed to export {glb_filename}")
        
        return result
