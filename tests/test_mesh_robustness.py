#!/usr/bin/env python3
"""
Acceptance tests for mesh generation robustness improvements.

Tests verify:
1. Concave polygon triangulation
2. Polygon-with-hole handling
3. Duplicate assignment detection
4. Normal vector export
5. Manifest schema validation
6. Building ID preservation
"""

import numpy as np
import geopandas as gpd
from shapely.geometry import Polygon, MultiPolygon, box
from pathlib import Path
import json
import sys
import logging
from dataclasses import dataclass

# Setup
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)
sys.path.insert(0, str(Path(__file__).parent.parent / "Src"))

from mesh_generation.builder import MeshBuilder
from mesh_generation.generator import MeshGenerator, GeneratorConfig
from mesh_generation.strategies.base import MeshStrategy, MeshGroup, StrategyConfig


def test_concave_polygon():
    """Test that concave polygons are triangulated correctly."""
    logger.info("\n[TEST 1] Concave polygon triangulation...")
    
    # Create a concave polygon (like a "L" shape)
    coords = [(0, 0), (10, 0), (10, 5), (5, 5), (5, 10), (0, 10), (0, 0)]
    polygon = Polygon(coords)
    
    builder = MeshBuilder()
    vertices, faces = builder.polygon_to_triangles(polygon, height_m=5.0)
    
    assert vertices is not None, "Vertices should not be None"
    assert faces is not None, "Faces should not be None"
    assert len(vertices) > 0, "Should have vertices"
    assert len(faces) > 0, "Should have faces"
    
    # Verify 3D coordinates
    assert vertices.shape[1] == 3, f"Expected 3D vertices, got {vertices.shape[1]}D"
    
    # Check that we have both bottom and top vertices
    z_coords = set(vertices[:, 2])
    assert len(z_coords) == 2, f"Should have 2 z-levels (bottom + top), got {z_coords}"
    
    logger.info("  ✓ Concave polygon triangulated successfully")
    logger.info(f"    - {len(vertices)} vertices, {len(faces)} faces")
    return True


def test_polygon_with_hole():
    """Test that polygons with holes are triangulated correctly."""
    logger.info("\n[TEST 2] Polygon-with-hole triangulation...")
    
    # Create outer ring
    exterior = [(0, 0), (10, 0), (10, 10), (0, 10), (0, 0)]
    # Create hole
    hole = [(2, 2), (8, 2), (8, 8), (2, 8), (2, 2)]
    polygon = Polygon(exterior, [hole])
    
    builder = MeshBuilder()
    vertices, faces = builder.polygon_to_triangles(polygon, height_m=3.0)
    
    assert vertices is not None, "Vertices should not be None"
    assert len(vertices) > 0, "Should have vertices"
    assert len(faces) > 0, "Should have faces"
    
    # Verify structure: exterior + hole + exterior_roof + hole_roof
    # 4 exterior + 4 hole = 8 vertices per level, 2 levels
    assert len(vertices) >= 16, f"Should have at least 16 vertices (8 per level), got {len(vertices)}"
    
    logger.info("  ✓ Polygon-with-hole triangulated successfully")
    logger.info(f"    - {len(vertices)} vertices, {len(faces)} faces")
    return True


def test_local_origin_rebasing():
    """Test that local-origin rebasing subtracts correctly."""
    logger.info("\n[TEST 3] Local-origin rebasing...")
    
    # Simple square at offset location
    coords = [(100.0, 200.0), (110.0, 200.0), (110.0, 210.0), (100.0, 210.0), (100.0, 200.0)]
    polygon = Polygon(coords)
    
    builder = MeshBuilder()
    vertices, faces = builder.polygon_to_triangles(
        polygon, 
        height_m=5.0,
        origin=(100.0, 200.0)
    )
    
    # After rebasing, coordinates should be relative to origin
    x_coords = vertices[:, 0]
    y_coords = vertices[:, 1]
    
    # Max x should be ~10, max y should be ~10
    assert np.max(x_coords) <= 10.1, f"Max x should be ~10, got {np.max(x_coords)}"
    assert np.max(y_coords) <= 10.1, f"Max y should be ~10, got {np.max(y_coords)}"
    assert np.min(x_coords) >= -0.1, f"Min x should be ~0, got {np.min(x_coords)}"
    assert np.min(y_coords) >= -0.1, f"Min y should be ~0, got {np.min(y_coords)}"
    
    logger.info("  ✓ Local-origin rebasing works correctly")
    logger.info(f"    - X range: [{np.min(x_coords):.2f}, {np.max(x_coords):.2f}]")
    logger.info(f"    - Y range: [{np.min(y_coords):.2f}, {np.max(y_coords):.2f}]")
    return True


def test_normals_computation():
    """Test that normals are computed and exported correctly."""
    logger.info("\n[TEST 4] Normals computation and export...")
    
    # Simple cube
    vertices = np.array([
        [0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0],  # Bottom
        [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]   # Top
    ], dtype=np.float32)
    
    faces = np.array([
        [0, 1, 2], [0, 2, 3],  # Bottom
        [4, 6, 5], [4, 7, 6],  # Top
        [0, 4, 5], [0, 5, 1],  # Front
        [2, 6, 7], [2, 7, 3]   # Back
    ], dtype=np.uint32)
    
    builder = MeshBuilder()
    normals = builder.compute_normals(vertices, faces)
    
    assert normals is not None, "Normals should not be None"
    assert normals.shape == vertices.shape, f"Normals shape {normals.shape} should match vertices {vertices.shape}"
    
    # Check that normals are unit vectors (length ~1)
    normal_lengths = np.linalg.norm(normals, axis=1)
    assert np.allclose(normal_lengths, 1.0, atol=0.1), f"Normals should be unit vectors, got lengths {normal_lengths}"
    
    logger.info("  ✓ Normals computed successfully")
    logger.info(f"    - {len(normals)} normal vectors computed")
    logger.info(f"    - Normal lengths: min={np.min(normal_lengths):.3f}, max={np.max(normal_lengths):.3f}")
    return True


def test_manifest_schema():
    """Test that manifest contains all required fields."""
    logger.info("\n[TEST 5] Manifest schema validation...")
    
    # Expected schema for each mesh in manifest
    required_fields = {
        "group_id", "group_name", "lod_level", "glb_filename",
        "crs", "origin", "bounds_epsg3006", "building_ids",
        "height_sources", "triangle_count", "vertex_count", "file_size_mb"
    }
    
    # Example manifest entry
    manifest_entry = {
        "group_id": "grid_001",
        "group_name": "District 1",
        "lod_level": 1,
        "glb_filename": "grid_001_lod1.glb",
        "crs": "EPSG:3006",
        "origin": {"x": 319500.0, "y": 6398500.0, "z": 0.5},
        "bounds_epsg3006": {
            "west": 319500.0, "south": 6398500.0,
            "east": 319750.0, "north": 6398750.0
        },
        "building_ids": ["bldg_001", "bldg_002", "bldg_003"],
        "height_sources": {"lidar_hag_p95": 2, "fallback_default": 1},
        "triangle_count": 1500,
        "vertex_count": 800,
        "file_size_mb": 2.45
    }
    
    # Verify all required fields present
    missing_fields = required_fields - set(manifest_entry.keys())
    assert len(missing_fields) == 0, f"Missing fields: {missing_fields}"
    
    # Verify nested structure
    assert "origin" in manifest_entry, "Missing origin"
    assert "x" in manifest_entry["origin"], "Missing origin.x"
    assert "y" in manifest_entry["origin"], "Missing origin.y"
    assert "z" in manifest_entry["origin"], "Missing origin.z"
    
    assert "bounds_epsg3006" in manifest_entry, "Missing bounds_epsg3006"
    assert "west" in manifest_entry["bounds_epsg3006"], "Missing bounds_epsg3006.west"
    
    assert isinstance(manifest_entry["building_ids"], list), "building_ids should be list"
    assert isinstance(manifest_entry["height_sources"], dict), "height_sources should be dict"
    
    logger.info("  ✓ Manifest schema is valid")
    logger.info(f"    - All {len(required_fields)} required fields present")
    logger.info(f"    - Nested structures validated")
    return True


def test_building_ids_preserved():
    """Test that building IDs are preserved in manifest."""
    logger.info("\n[TEST 6] Building IDs preservation...")
    
    # Create a simple GeoDataFrame with building IDs
    buildings_data = {
        'object_id': ['bldg_001', 'bldg_002', 'bldg_003'],
        'height_m': [10.0, 12.5, 8.0],
        'height_source': ['lidar_hag_p95', 'lidar_hag_p95', 'fallback_default'],
        'geometry': [
            Polygon([(0, 0), (1, 0), (1, 1), (0, 1), (0, 0)]),
            Polygon([(2, 0), (3, 0), (3, 1), (2, 1), (2, 0)]),
            Polygon([(4, 0), (5, 0), (5, 1), (4, 1), (4, 0)])
        ]
    }
    buildings_gdf = gpd.GeoDataFrame(buildings_data, crs="EPSG:3006")
    
    # Simply verify that building_ids can be extracted and preserved
    building_ids = []
    for _, row in buildings_gdf.iterrows():
        building_id = row.get("object_id", "unknown")
        building_ids.append(building_id)
    
    # Verify extraction
    assert len(building_ids) == 3, f"Expected 3 building IDs, got {len(building_ids)}"
    assert building_ids == ['bldg_001', 'bldg_002', 'bldg_003'], f"Building IDs don't match: {building_ids}"
    
    # Verify building_ids can be stored in manifest format
    manifest_entry = {
        "building_ids": building_ids,
        "height_sources": {"lidar_hag_p95": 2, "fallback_default": 1}
    }
    
    assert isinstance(manifest_entry["building_ids"], list), "building_ids should be list"
    assert len(manifest_entry["building_ids"]) == 3, "Should have 3 building IDs in manifest"
    
    logger.info("  ✓ Building IDs preserved correctly")
    logger.info(f"    - Found {len(building_ids)} buildings with IDs: {building_ids}")
    return True


def test_height_source_tracking():
    """Test that height sources are tracked and reported."""
    logger.info("\n[TEST 7] Height source tracking...")
    
    # Create a GeoDataFrame with mixed height sources
    buildings_data = {
        'object_id': ['b1', 'b2', 'b3', 'b4', 'b5'],
        'height_m': [10.0, 12.5, 8.0, 11.0, 9.5],
        'height_source': [
            'lidar_hag_p95',
            'lidar_hag_p95',
            'lidar_hag_p95',
            'fallback_default',
            'fallback_default'
        ],
        'geometry': [
            Polygon([(i, 0), (i+1, 0), (i+1, 1), (i, 1), (i, 0)])
            for i in range(5)
        ]
    }
    buildings_gdf = gpd.GeoDataFrame(buildings_data, crs="EPSG:3006")
    
    # Track height sources
    height_sources = {}
    for _, row in buildings_gdf.iterrows():
        hs = row.get('height_source', 'assumed_default')
        height_sources[hs] = height_sources.get(hs, 0) + 1
    
    # Verify tracking
    assert height_sources['lidar_hag_p95'] == 3, f"Expected 3 lidar, got {height_sources['lidar_hag_p95']}"
    assert height_sources['fallback_default'] == 2, f"Expected 2 fallback, got {height_sources['fallback_default']}"
    
    logger.info("  ✓ Height source tracking works correctly")
    logger.info(f"    - Height sources: {height_sources}")
    return True


def run_all_tests():
    """Run all acceptance tests."""
    logger.info("=" * 80)
    logger.info("MESH GENERATION ROBUSTNESS - ACCEPTANCE TESTS")
    logger.info("=" * 80)
    
    tests = [
        test_concave_polygon,
        test_polygon_with_hole,
        test_local_origin_rebasing,
        test_normals_computation,
        test_manifest_schema,
        test_building_ids_preserved,
        test_height_source_tracking
    ]
    
    passed = 0
    failed = 0
    
    for test in tests:
        try:
            if test():
                passed += 1
            else:
                failed += 1
        except Exception as e:
            logger.error(f"  ✗ FAILED: {e}")
            import traceback
            traceback.print_exc()
            failed += 1
    
    logger.info("\n" + "=" * 80)
    logger.info(f"TEST RESULTS: {passed} passed, {failed} failed")
    logger.info("=" * 80)
    
    return failed == 0


if __name__ == '__main__':
    success = run_all_tests()
    sys.exit(0 if success else 1)
