"""
Mesh Generator: Main orchestrator for mesh generation with any strategy.

Coordinates:
- Strategy selection
- Partitioning
- Mesh building
- Export

Usage:
    config = GeneratorConfig(strategy="district")
    generator = MeshGenerator(buildings_gdf, config)
    report = generator.generate(output_dir)
"""

from pathlib import Path
from dataclasses import dataclass, asdict
from typing import Dict, Any, List, Optional
import json
import logging
import geopandas as gpd
import numpy as np
from datetime import datetime
import pandas as pd

from .strategies.base import MeshStrategy
from .strategies.individual import IndividualBuildingStrategy, IndividualBuildingConfig
from .strategies.district import DistrictStrategy, DistrictConfig
from .strategies.grid import GridStrategy, GridConfig
from .strategies.quadtree import QuadtreeStrategy, QuadtreeConfig
from .builder import MeshBuilder, check_watertight
from .lod_generation import LODGenerator, LODGenerationError

logger = logging.getLogger(__name__)


@dataclass
class GeneratorConfig:
    """Configuration for mesh generation."""
    strategy: str = "district"  # "individual", "district", "grid", "quadtree"
    strategy_config: Dict[str, Any] = None
    assumed_height_m: float = 10.0
    terrain_offset_m: float = 0.5
    material_color: tuple = (1.0, 1.0, 1.0)  # White
    crs: str = "EPSG:3006"  # Authoritative CRS

    # Mesh export formats. The FIRST entry is the primary/authoritative output
    # (its export success gates each LOD); any others are written alongside as
    # extra artifacts. Default is PLY-only; add "glb"/"obj" here if needed.
    export_formats: tuple = ("ply",)

    # LOD generation settings.
    # generate_lods controls VERTEX REDUCTION: when True, decimated LOD2/LOD3
    # meshes are produced alongside full-detail LOD1. When False (default), only
    # the full-detail LOD1 mesh is exported and no vertices are reduced.
    generate_lods: bool = False  # Generate reduced-vertex LOD2 and LOD3
    lod_decimation_library: str = "pyvista"  # "pyvista" or "trimesh"
    lod_target_reductions: tuple = (0.5, 0.1)  # (LOD2=50%, LOD3=10%)
    lod_decimation_quality: float = 0.7  # Decimation quality (0.0-1.0)


class MeshGenerator:
    """Main orchestrator for mesh generation with pluggable strategies."""
    
    STRATEGIES = {
        "individual": (IndividualBuildingStrategy, IndividualBuildingConfig),
        "district": (DistrictStrategy, DistrictConfig),
        "grid": (GridStrategy, GridConfig),
        "quadtree": (QuadtreeStrategy, QuadtreeConfig),
    }
    
    def __init__(self, buildings_gdf: gpd.GeoDataFrame, config: GeneratorConfig = None):
        """
        Initialize generator.
        
        Args:
            buildings_gdf: GeoDataFrame with building geometries
            config: GeneratorConfig instance
        """
        self.buildings_gdf = buildings_gdf
        self.config = config or GeneratorConfig()
        self.strategy: MeshStrategy = None
        self.builder = MeshBuilder(self.config.__dict__)
        
        # Initialize LOD generator if LOD generation enabled
        self.lod_generator = None
        if self.config.generate_lods:
            try:
                self.lod_generator = LODGenerator(
                    library=self.config.lod_decimation_library,
                    quality=self.config.lod_decimation_quality
                )
                logger.info(f"LOD generation enabled: library={self.config.lod_decimation_library}")
            except Exception as e:
                logger.warning(f"Failed to initialize LOD generator: {e}. LOD generation disabled.")
                self.lod_generator = None
        
        self._initialize_strategy()
    
    def _initialize_strategy(self):
        """Create strategy instance based on config."""
        strategy_name = self.config.strategy.lower()
        
        if strategy_name not in self.STRATEGIES:
            raise ValueError(
                f"Unknown strategy: {strategy_name}\n"
                f"Available: {', '.join(self.STRATEGIES.keys())}"
            )
        
        strategy_class, config_class = self.STRATEGIES[strategy_name]
        
        # Create strategy config
        strategy_config = config_class()
        if self.config.strategy_config:
            for key, value in self.config.strategy_config.items():
                if hasattr(strategy_config, key):
                    setattr(strategy_config, key, value)
        
        self.strategy = strategy_class(self.buildings_gdf, strategy_config)
        logger.info(f"Initialized strategy: {self.strategy.get_strategy_name()}")
    
    def generate(self, output_dir: Path) -> Dict[str, Any]:
        """
        Main pipeline: partition → mesh → export → report.
        
        Args:
            output_dir: Path to output directory
        
        Returns:
            dict with generation report
        """
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        logger.info("=" * 80)
        logger.info(f"MESH GENERATION: {self.strategy.get_strategy_name()}")
        logger.info(f"Input buildings: {len(self.buildings_gdf):,}")
        logger.info(f"CRS: {self.config.crs}")
        logger.info("=" * 80)
        
        # Step 1: Partition
        logger.info("\n[1/4] Partitioning buildings...")
        groups = self.strategy.partition()
        
        # Validate
        validation_report = self.strategy.validate()
        if validation_report["validation_errors"]:
            logger.warning("Validation issues:")
            for error in validation_report["validation_errors"]:
                logger.warning(f"  ⚠ {error}")
        
        stats = self.strategy.get_statistics()
        logger.info(
            f"✓ Created {stats['group_count']} groups: "
            f"avg {stats['avg_buildings_per_group']:.0f} buildings/group"
        )
        
        # Step 2: Generate meshes
        logger.info(f"\n[2/4] Generating {len(groups)} mesh(es)...")
        manifests = []
        total_vertices = 0
        total_triangles = 0
        failed_count = 0
        
        for group_idx, group in enumerate(groups):
            progress = (group_idx + 1) / len(groups) * 100
            logger.info(
                f"  [{progress:3.0f}%] {group.group_name} "
                f"({group.building_count} buildings)..."
            )
            
            # Get buildings for this group
            group_buildings = self.buildings_gdf.iloc[group.building_indices]
            
            # Build group mesh
            try:
                metadata = self._build_group_mesh(
                    group=group,
                    buildings=group_buildings,
                    output_dir=output_dir
                )
                
                if metadata:
                    manifests.append(metadata)
                    total_vertices += metadata.get("vertex_count", 0)
                    total_triangles += metadata.get("triangle_count", 0)
                else:
                    failed_count += 1
            
            except Exception as e:
                logger.error(f"  ✗ Failed: {e}")
                failed_count += 1
        
        logger.info(f"✓ Mesh generation: {len(manifests)} succeeded, {failed_count} failed")
        
        # Step 3: Save manifest
        logger.info(f"\n[3/4] Saving manifest...")
        manifest_path = self._save_manifest(manifests, output_dir)
        logger.info(f"✓ Manifest: {manifest_path}")
        
        # Step 4: Generate report
        logger.info(f"\n[4/4] Generating report...")
        report = {
            "timestamp": datetime.now().isoformat(),
            "strategy": self.strategy.get_strategy_name(),
            "strategy_description": self.strategy.get_strategy_description(),
            "total_buildings_input": len(self.buildings_gdf),
            "total_groups": len(groups),
            "groups_successfully_meshed": len(manifests),
            "groups_failed": failed_count,
            "total_vertices": total_vertices,
            "total_triangles": total_triangles,
            "output_directory": str(output_dir),
            "manifest_file": str(manifest_path),
            "validation_report": validation_report,
            "strategy_statistics": stats
        }
        
        # Print summary
        logger.info("\n" + "=" * 80)
        logger.info("MESH GENERATION COMPLETE")
        logger.info("=" * 80)
        logger.info(f"Strategy:         {report['strategy']}")
        logger.info(f"Input buildings:  {report['total_buildings_input']:,}")
        logger.info(f"Mesh groups:      {report['total_groups']}")
        logger.info(f"Successful:       {report['groups_successfully_meshed']}")
        logger.info(f"Failed:           {report['groups_failed']}")
        logger.info(f"Total vertices:   {report['total_vertices']:,}")
        logger.info(f"Total triangles:  {report['total_triangles']:,}")
        logger.info(f"Output:           {output_dir}")
        logger.info("=" * 80)
        
        return report
    
    def _build_group_mesh(
        self,
        group,
        buildings: gpd.GeoDataFrame,
        output_dir: Path
    ) -> Optional[Dict[str, Any]]:
        """Build mesh for a group of buildings with proper height sourcing and optional LOD generation."""
        try:
            # Compute local origin from group bounds (subtract this from all vertices)
            origin_x = float(group.bounds[0])
            origin_y = float(group.bounds[1])
            origin_z = self.config.terrain_offset_m
            
            # Combine all building geometries
            all_vertices = []
            all_faces = []
            vertex_offset = 0
            building_count = 0
            building_ids = []
            height_sources = {}  # Track distribution: {"lidar_hag_p95": 143, "fallback_default": 7}
            # Per-building watertightness. Checking each building separately (rather
            # than the welded group) avoids false non-manifold flags where adjacent
            # buildings in a cell share a wall edge — each building is still a closed
            # solid, which is what matters for rendering.
            buildings_open = 0
            open_building_boundary_edges = 0

            for _, row in buildings.iterrows():
                geom = row.geometry
                
                # EXPLICIT height field consumption from row
                height_m = row.get("height_m", self.config.assumed_height_m)
                height_source = row.get("height_source", "assumed_default")
                building_id = row.get("object_id", f"unknown_{building_count}")
                
                # Generate triangles with local-origin rebasing
                vertices, faces = self.builder.geometry_to_triangles(
                    geom, 
                    height_m,
                    origin=(origin_x, origin_y)
                )
                
                if vertices is not None and len(vertices) > 0:
                    all_vertices.append(vertices)
                    all_faces.append(faces + vertex_offset)
                    vertex_offset += len(vertices)
                    building_count += 1
                    building_ids.append(building_id)

                    # Per-building watertight check (boundary edges = open surface).
                    bw = check_watertight(vertices, faces)
                    if bw["boundary_edges"] > 0:
                        buildings_open += 1
                        open_building_boundary_edges += bw["boundary_edges"]

                    # Track height source
                    height_sources[height_source] = height_sources.get(height_source, 0) + 1

            if not all_vertices:
                logger.warning(f"  ✗ No valid meshes generated for {group.group_name}")
                return None
            
            # Combine all vertices and faces
            combined_vertices = np.vstack(all_vertices)
            combined_faces = np.vstack(all_faces)

            # ========== WATERTIGHT SUMMARY ==========
            # A group is "watertight" when every building in it is a closed solid
            # (no open/boundary edges). Shared walls between adjacent buildings are
            # expected and are NOT counted as defects.
            watertight_report = {
                "all_buildings_closed": buildings_open == 0,
                "buildings_total": building_count,
                "buildings_open": buildings_open,
                "open_boundary_edges": open_building_boundary_edges,
            }
            if buildings_open > 0:
                logger.warning(
                    f"  ⚠ {group.group_name}: {buildings_open}/{building_count} "
                    f"building(s) not closed "
                    f"({open_building_boundary_edges} boundary edge(s) total)"
                )

            # ========== LOD GENERATION ==========
            lod_export_results = {}
            vertices_by_lod = {"lod1": (combined_vertices.copy(), combined_faces.copy())}
            
            if self.lod_generator:
                try:
                    logger.debug(f"  → Generating LOD2/LOD3...")
                    lod_suite = self.lod_generator.generate_lod_suite(
                        combined_vertices,
                        combined_faces,
                        target_reductions=self.config.lod_target_reductions
                    )
                    vertices_by_lod = lod_suite
                except LODGenerationError as e:
                    logger.warning(f"  ⚠ LOD generation failed: {e}. Proceeding with LOD1 only.")
                    # Fall back to LOD1 only; lod_export_results will only have lod1
            
            # ========== EXPORT ALL LODS ==========
            mesh_dir = output_dir / "mesh_files"
            lod_export_results = self.builder.export_mesh_suite(
                mesh_dir,
                vertices_by_lod,
                group.group_id,
                compute_normals_per_lod=True,
                export_formats=self.config.export_formats,
            )
            
            # Check if LOD1 export succeeded
            if "lod1" not in lod_export_results or not lod_export_results["lod1"]["success"]:
                logger.warning(f"  ✗ LOD1 export failed for {group.group_name}")
                return None
            
            # ========== BUILD METADATA ==========
            # Get LOD1 stats (primary mesh)
            lod1_stats = lod_export_results["lod1"]
            file_size_mb = lod1_stats["file_size_mb"]
            
            # Build LOD levels list (will have at least lod1, possibly lod2/lod3)
            lod_levels = list(sorted([k for k in lod_export_results.keys() if lod_export_results[k]["success"]]))
            
            # Build per-format file maps for each successful LOD.
            # mesh_files is the authoritative map ({lod: {fmt: filename}}) covering
            # every exported format. glb_files/ply_files are convenience views kept
            # for backward compatibility with existing manifest consumers; each is
            # only populated for LODs that actually produced that format (empty
            # otherwise — e.g. glb_files is empty in the default PLY-only export).
            primary_format = (
                self.config.export_formats[0].lower()
                if self.config.export_formats else "ply"
            )
            mesh_files = {}
            glb_files = {}
            ply_files = {}
            mesh_stats = {}
            for lod_level in lod_levels:
                result = lod_export_results[lod_level]
                # Primary filename + any secondary formats, merged into one map.
                per_format = {primary_format: result["filename"]}
                per_format.update(result.get("extra_files", {}))
                mesh_files[lod_level] = per_format
                if "glb" in per_format:
                    glb_files[lod_level] = per_format["glb"]
                if "ply" in per_format:
                    ply_files[lod_level] = per_format["ply"]
                mesh_stats[lod_level] = {
                    "vertices": result["vertex_count"],
                    "faces": result["face_count"],
                    "file_size_mb": result["file_size_mb"]
                }
            
            # Create semantic-rich metadata with LOD support
            metadata = {
                "group_id": group.group_id,
                "group_name": group.group_name,
                "lod_levels": lod_levels,  # e.g., ["lod1", "lod2", "lod3"] or ["lod1"] when reduction is off
                "primary_format": primary_format,  # authoritative export format (default "ply")
                "mesh_files": mesh_files,   # {"lod1": {"ply": "filename", ...}, ...} — all formats
                "glb_files": glb_files,     # {"lod1": "filename", ...}; empty when GLB not exported
                "ply_files": ply_files,     # {"lod1": "filename", ...}; empty when PLY not exported
                "crs": self.config.crs,
                "origin": {
                    "x": origin_x,
                    "y": origin_y,
                    "z": origin_z
                },
                "bounds_epsg3006": {
                    "west": float(group.bounds[0]),
                    "south": float(group.bounds[1]),
                    "east": float(group.bounds[2]),
                    "north": float(group.bounds[3])
                },
                "building_ids": building_ids,
                "height_sources": height_sources,
                "mesh_stats": mesh_stats,
                "watertight": watertight_report,
                # Primary mesh stats (LOD1) for backward compatibility
                "triangle_count": lod1_stats["face_count"],
                "vertex_count": lod1_stats["vertex_count"],
                "file_size_mb": file_size_mb
            }
            
            return metadata
        
        except Exception as e:
            logger.error(f"  ✗ Error: {e}")
            return None
    
    def _save_manifest(self, manifests: List[Dict], output_dir: Path) -> Path:
        """Save mesh manifest to JSON with semantic metadata."""
        manifest_data = {
            "timestamp": datetime.now().isoformat(),
            "strategy": self.strategy.get_strategy_name(),
            "strategy_description": self.strategy.get_strategy_description(),
            "crs": self.config.crs,
            "total_groups": len(manifests),
            "total_vertices": sum(m.get("vertex_count", 0) for m in manifests),
            "total_triangles": sum(m.get("triangle_count", 0) for m in manifests),
            "total_file_size_mb": round(sum(m.get("file_size_mb", 0) for m in manifests), 2),
            "groups_all_buildings_closed": sum(
                1 for m in manifests if m.get("watertight", {}).get("all_buildings_closed")
            ),
            "groups_with_open_buildings": sum(
                1 for m in manifests
                if not m.get("watertight", {}).get("all_buildings_closed", True)
            ),
            "total_open_buildings": sum(
                m.get("watertight", {}).get("buildings_open", 0) for m in manifests
            ),
            "meshes": manifests
        }
        
        manifest_path = output_dir / "meshes_manifest.json"
        with open(manifest_path, 'w') as f:
            json.dump(manifest_data, f, indent=2)
        
        return manifest_path
