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
from datetime import datetime
import pandas as pd

from .strategies.base import MeshStrategy
from .strategies.individual import IndividualBuildingStrategy, IndividualBuildingConfig
from .strategies.district import DistrictStrategy, DistrictConfig
from .strategies.grid import GridStrategy, GridConfig
from .strategies.quadtree import QuadtreeStrategy, QuadtreeConfig
from .builder import MeshBuilder

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
        """Build mesh for a group of buildings with proper height sourcing and metadata."""
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
                    
                    # Track height source
                    height_sources[height_source] = height_sources.get(height_source, 0) + 1
            
            if not all_vertices:
                logger.warning(f"  ✗ No valid meshes generated for {group.group_name}")
                return None
            
            # Combine all vertices and faces
            combined_vertices = np.vstack(all_vertices)
            combined_faces = np.vstack(all_faces)
            
            # Export to GLB
            glb_filename = f"{group.group_id}_lod1.glb"
            glb_path = output_dir / glb_filename
            
            # Export with normals
            normals = self.builder.compute_normals(combined_vertices, combined_faces)
            success = self.builder.export_glb(
                glb_path,
                combined_vertices,
                combined_faces,
                group.group_id,
                normals=normals
            )
            
            if not success:
                logger.warning(f"  ✗ Export failed for {group.group_name}")
                return None
            
            # Get file size
            file_size_mb = glb_path.stat().st_size / (1024 * 1024)
            
            # Create NEW semantic-rich metadata with all required fields
            metadata = {
                "group_id": group.group_id,
                "group_name": group.group_name,
                "lod_level": 1,
                "glb_filename": glb_filename,
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
                "triangle_count": len(combined_faces),
                "vertex_count": len(combined_vertices),
                "file_size_mb": round(file_size_mb, 2)
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
            "meshes": manifests
        }
        
        manifest_path = output_dir / "meshes_manifest.json"
        with open(manifest_path, 'w') as f:
            json.dump(manifest_data, f, indent=2)
        
        return manifest_path
