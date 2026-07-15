"""
LOD Generation: Create lower-detail versions of meshes for runtime performance.

Responsible for:
- Mesh decimation (LOD2 ≈ 50% vertices, LOD3 ≈ 10% vertices)
- Quality verification
- Deterministic decimation (same input → same output)
"""

from typing import Any, Tuple, Dict, Optional
import numpy as np
import logging

logger = logging.getLogger(__name__)


class LODGenerationError(Exception):
    """Raised when LOD generation fails."""
    pass


class LODGenerator:
    """Generate multiple LOD versions of a mesh using adaptive decimation."""
    
    def __init__(self, library: str = "pyvista", quality: float = 0.7):
        """
        Initialize LOD generator.
        
        Args:
            library: "pyvista" (default, more robust) or "trimesh" (lighter-weight)
            quality: Decimation quality (0.0-1.0); higher = more vertices retained
        """
        self.library = library.lower()
        self.quality = max(0.0, min(1.0, quality))
        
        if self.library not in ("pyvista", "trimesh"):
            raise ValueError(f"Unknown library: {self.library}")
    
    def generate_lod_suite(
        self,
        vertices: np.ndarray,
        faces: np.ndarray,
        target_reductions: Tuple[float, float] = (0.5, 0.1)
    ) -> Dict[str, Tuple[np.ndarray, np.ndarray]]:
        """
        Generate LOD2 and LOD3 from LOD1 (original).
        
        Args:
            vertices: (N, 3) vertex array for LOD1
            faces: (M, 3) face/triangle array for LOD1
            target_reductions: (lod2_reduction, lod3_reduction)
                - 0.5 = LOD2 has ≈50% of LOD1 vertices
                - 0.1 = LOD3 has ≈10% of LOD1 vertices
        
        Returns:
            dict with keys "lod1", "lod2", "lod3":
            {
                "lod1": (vertices, faces),  # Original (not decimated)
                "lod2": (vertices_lod2, faces_lod2),  # ~50% reduction
                "lod3": (vertices_lod3, faces_lod3)   # ~90% reduction
            }
        
        Raises:
            LODGenerationError: If decimation fails or reduction targets not met
        """
        # Validate inputs
        if len(vertices) < 3 or len(faces) < 1:
            raise LODGenerationError(f"Invalid mesh: {len(vertices)} vertices, {len(faces)} faces")
        
        lod2_target, lod3_target = target_reductions
        
        result = {
            "lod1": (vertices.copy(), faces.copy())
        }
        
        try:
            # Generate LOD2 (medium detail)
            logger.debug(f"Decimating to LOD2 (target: {lod2_target:.1%})...")
            lod2_vertices, lod2_faces = self._decimate(vertices, faces, lod2_target)
            result["lod2"] = (lod2_vertices, lod2_faces)
            
            # Verify LOD2 reduction
            lod2_reduction = len(lod2_vertices) / len(vertices)
            if lod2_reduction > lod2_target * 1.25:  # Allow 25% tolerance above target
                logger.warning(
                    f"LOD2 reduction below target: {lod2_reduction:.1%} vs {lod2_target:.1%}"
                )
            
            # Generate LOD3 (low detail)
            logger.debug(f"Decimating to LOD3 (target: {lod3_target:.1%})...")
            lod3_vertices, lod3_faces = self._decimate(vertices, faces, lod3_target)
            result["lod3"] = (lod3_vertices, lod3_faces)
            
            # Verify LOD3 reduction
            lod3_reduction = len(lod3_vertices) / len(vertices)
            if lod3_reduction > lod3_target * 1.25:  # Allow 25% tolerance above target
                logger.warning(
                    f"LOD3 reduction below target: {lod3_reduction:.1%} vs {lod3_target:.1%}"
                )
            
            # Final sanity check: LOD levels should be monotonically decreasing
            if len(lod2_vertices) >= len(vertices):
                raise LODGenerationError("LOD2 has more or equal vertices than LOD1")
            if len(lod3_vertices) >= len(lod2_vertices):
                raise LODGenerationError("LOD3 has more or equal vertices than LOD2")
            
            logger.debug(
                f"LOD suite generated: "
                f"LOD1={len(vertices)} vertices, "
                f"LOD2={len(lod2_vertices)} vertices ({lod2_reduction:.1%}), "
                f"LOD3={len(lod3_vertices)} vertices ({lod3_reduction:.1%})"
            )
            
            return result
        
        except Exception as e:
            raise LODGenerationError(f"Failed to generate LOD suite: {e}")
    
    def _decimate(
        self,
        vertices: np.ndarray,
        faces: np.ndarray,
        target_reduction: float
    ) -> Tuple[np.ndarray, np.ndarray]:
        """
        Decimate mesh to target vertex reduction using configured library.
        
        Args:
            vertices: (N, 3) vertex positions
            faces: (M, 3) face indices
            target_reduction: Target reduction factor (0.5 = keep 50% of vertices)
        
        Returns:
            (decimated_vertices, decimated_faces)
        """
        if self.library == "pyvista":
            return self._decimate_pyvista(vertices, faces, target_reduction)
        elif self.library == "trimesh":
            return self._decimate_trimesh(vertices, faces, target_reduction)
    
    def _decimate_pyvista(
        self,
        vertices: np.ndarray,
        faces: np.ndarray,
        target_reduction: float
    ) -> Tuple[np.ndarray, np.ndarray]:
        """Decimate using pyvista (quadric edge collapse decimation)."""
        try:
            import pyvista as pv
        except ImportError:
            raise LODGenerationError("pyvista not installed; cannot decimate")
        
        try:
            # Convert to pyvista mesh
            # pyvista expects faces in format [3, v0, v1, v2, 3, v0, v1, v2, ...]
            face_array = np.hstack([np.full((len(faces), 1), 3), faces]).flatten()
            mesh = pv.PolyData(vertices, face_array)
            
            # Decimate: reduce = 1 - target_reduction
            # Example: target_reduction=0.5 means keep 50%, so reduce=0.5
            target_reduction = max(0.01, min(0.99, target_reduction))
            reduction_factor = 1.0 - target_reduction
            
            # Use basic decimation (compatible with all pyvista versions)
            decimated = mesh.decimate(reduction_factor)
            
            # Extract vertices and faces
            dec_vertices = decimated.points.astype(np.float32)
            # Handle face array format - pyvista stores as [3, v0, v1, v2, 3, v0, v1, v2, ...]
            faces_raw = decimated.faces
            if len(faces_raw) > 0:
                # Reshape to (-1, 4) to get [count, v0, v1, v2] then take columns 1,2,3
                dec_faces = faces_raw.reshape(-1, 4)[:, 1:].astype(np.uint32)
            else:
                dec_faces = np.empty((0, 3), dtype=np.uint32)
            
            return dec_vertices, dec_faces
        
        except Exception as e:
            raise LODGenerationError(f"pyvista decimation failed: {e}")
    
    def _decimate_trimesh(
        self,
        vertices: np.ndarray,
        faces: np.ndarray,
        target_reduction: float
    ) -> Tuple[np.ndarray, np.ndarray]:
        """Decimate using trimesh (quadric mesh simplification)."""
        try:
            import trimesh
        except ImportError:
            raise LODGenerationError("trimesh not installed; cannot decimate")
        
        try:
            # Create trimesh
            mesh = trimesh.Trimesh(vertices, faces, process=False)
            
            # Simplify: target_count = current_vertices * target_reduction
            target_count = max(3, int(len(vertices) * target_reduction))
            simplified = trimesh.simplification.simplify(mesh, target_count=target_count)
            
            # Extract data
            dec_vertices = simplified.vertices.astype(np.float32)
            dec_faces = simplified.faces.astype(np.uint32)
            
            return dec_vertices, dec_faces
        
        except Exception as e:
            raise LODGenerationError(f"trimesh decimation failed: {e}")
    
    def get_decimation_statistics(
        self,
        original_vertices: int,
        original_faces: int,
        lod2_vertices: int,
        lod2_faces: int,
        lod3_vertices: int,
        lod3_faces: int
    ) -> Dict[str, Any]:
        """
        Compute statistics about decimation effectiveness.
        
        Returns:
            dict with reduction percentages and file size estimates
        """
        lod2_vertex_reduction = (original_vertices - lod2_vertices) / original_vertices
        lod3_vertex_reduction = (original_vertices - lod3_vertices) / original_vertices
        
        # Rough file size estimate (bytes per vertex ≈ 12 for position + 4 for index overhead)
        lod1_size = original_vertices * 12 + original_faces * 12
        lod2_size = lod2_vertices * 12 + lod2_faces * 12
        lod3_size = lod3_vertices * 12 + lod3_faces * 12
        
        return {
            "lod1": {
                "vertices": original_vertices,
                "faces": original_faces,
                "estimated_bytes": lod1_size
            },
            "lod2": {
                "vertices": lod2_vertices,
                "faces": lod2_faces,
                "estimated_bytes": lod2_size,
                "vertex_reduction": lod2_vertex_reduction,
                "face_reduction": (original_faces - lod2_faces) / original_faces
            },
            "lod3": {
                "vertices": lod3_vertices,
                "faces": lod3_faces,
                "estimated_bytes": lod3_size,
                "vertex_reduction": lod3_vertex_reduction,
                "face_reduction": (original_faces - lod3_faces) / original_faces
            }
        }
