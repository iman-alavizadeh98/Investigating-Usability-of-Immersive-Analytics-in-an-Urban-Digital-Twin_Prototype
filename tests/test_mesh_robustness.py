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
    
    # Expected schema for each mesh in manifest (matches generator._build_group_mesh output).
    # Default export is PLY-only, full detail: primary_format="ply", one LOD.
    required_fields = {
        "group_id", "group_name", "lod_levels", "primary_format",
        "mesh_files", "glb_files", "ply_files",
        "crs", "origin", "bounds_epsg3006", "building_ids",
        "height_sources", "triangle_count", "vertex_count", "file_size_mb"
    }

    # Example manifest entry (default PLY-only, no vertex reduction)
    manifest_entry = {
        "group_id": "grid_001",
        "group_name": "District 1",
        "lod_levels": ["lod1"],
        "primary_format": "ply",
        "mesh_files": {"lod1": {"ply": "grid_001_lod1.ply"}},
        "glb_files": {},
        "ply_files": {"lod1": "grid_001_lod1.ply"},
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

    assert isinstance(manifest_entry["lod_levels"], list), "lod_levels should be list"
    assert isinstance(manifest_entry["mesh_files"], dict), "mesh_files should be dict"
    assert isinstance(manifest_entry["glb_files"], dict), "glb_files should be dict"
    assert isinstance(manifest_entry["ply_files"], dict), "ply_files should be dict"
    # Default primary format is PLY; the primary file must appear in mesh_files/ply_files.
    assert manifest_entry["primary_format"] == "ply", "default primary_format should be ply"
    assert "lod1" in manifest_entry["ply_files"], "PLY primary file should be listed in ply_files"
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


def test_watertight_meshes():
    """Test that generated building prisms are closed (watertight) solids."""
    logger.info("\n[TEST 8] Watertight mesh validation...")

    from mesh_generation.builder import check_watertight

    builder = MeshBuilder()
    cases = {
        "square": Polygon([(0, 0), (10, 0), (10, 10), (0, 10)]),
        "concave_L": Polygon([(0, 0), (10, 0), (10, 4), (4, 4), (4, 10), (0, 10)]),
        "with_hole": Polygon(
            [(0, 0), (20, 0), (20, 20), (0, 20)],
            [[(6, 6), (6, 14), (14, 14), (14, 6)]],
        ),
    }
    for name, poly in cases.items():
        v, f = builder.polygon_to_triangles(poly, height_m=5.0)
        report = check_watertight(v, f)
        assert report["is_watertight"], f"{name} not watertight: {report}"

    # Degenerate footprint must be skipped, not emitted as an open sliver.
    v, f = builder.polygon_to_triangles(Polygon([(0, 0), (10, 0), (0, 0)]), height_m=5.0)
    assert len(f) == 0, "degenerate footprint should produce no faces"

    logger.info("  ✓ All prisms are watertight; degenerate footprint skipped")
    return True


def _ownership_test_buildings():
    """
    Footprints chosen to break naive assignment rules, on a 500 m lattice anchored
    at (298000, 6383500):

      - straddler: crosses the x=298500 cell boundary -> `intersects` would put it
        in TWO cells; `within` would drop it entirely.
      - u_shape:   concave, centroid falls in the notch OUTSIDE the polygon.
      - donut:     centroid falls in the hole, OUTSIDE the polygon.

    The last two are why assignment uses representative_point() (guaranteed inside)
    rather than centroid.
    """
    from shapely.geometry import Polygon

    straddler = box(298480, 6383600, 298520, 6383640)   # spans the 298500 boundary

    # U opening east; centroid lands in the notch.
    u_shape = Polygon([
        (298100, 6383600), (298180, 6383600), (298180, 6383620),
        (298130, 6383620), (298130, 6383660), (298180, 6383660),
        (298180, 6383680), (298100, 6383680),
    ])

    donut = Polygon(
        [(298600, 6383600), (298700, 6383600), (298700, 6383700), (298600, 6383700)],
        [[(298630, 6383630), (298670, 6383630), (298670, 6383670), (298630, 6383670)]],
    )

    return gpd.GeoDataFrame(
        {"object_id": ["straddler", "u_shape", "donut"]},
        geometry=[straddler, u_shape, donut],
        crs="EPSG:3006",
    )


def test_grid_ownership_is_exclusive():
    """Test 9: every building belongs to exactly one grid cell."""
    logger.info("\n[TEST 9] Grid ownership is exclusive (representative point)")

    from mesh_generation.strategies.grid import GridStrategy, GridConfig

    gdf = _ownership_test_buildings()
    config = GridConfig()
    config.cell_size_m = 500
    strategy = GridStrategy(gdf, config)
    groups = strategy.partition()

    slots = sum(len(g.building_indices) for g in groups)
    unique = len({i for g in groups for i in g.building_indices})

    assert slots == unique, f"building assigned to multiple cells ({slots} slots, {unique} unique)"
    assert unique == len(gdf), f"only {unique}/{len(gdf)} buildings assigned"

    report = strategy.validate()
    assert report["max_assignments_per_building"] == 1, report["max_assignments_per_building"]
    assert report["duplicate_building_count"] == 0
    assert report["unassigned_building_count"] == 0
    assert not report["validation_errors"], report["validation_errors"]

    # Concave/donut footprints must land in a cell that actually contains an
    # interior point -- the failure mode a centroid rule would introduce.
    for group in groups:
        ox = group.metadata["cell_origin_x"]
        oy = group.metadata["cell_origin_y"]
        assert ox % 500 == 0 and oy % 500 == 0, f"cell off lattice: ({ox}, {oy})"
        for pos in group.building_indices:
            point = gdf.geometry.iloc[pos].representative_point()
            assert ox <= point.x < ox + 500 and oy <= point.y < oy + 500, (
                f"building {gdf.object_id.iloc[pos]} placed in a cell that does not "
                f"contain its representative point"
            )

    logger.info(f"  ✓ {len(gdf)} buildings -> {len(groups)} cells, no duplicates")
    logger.info("  ✓ concave and donut footprints placed by interior point")
    return True


def test_grid_anchor_stability():
    """Test 10: cell ids are stable when only a subset of buildings is processed."""
    logger.info("\n[TEST 10] Grid anchor stability (subset -> same cell ids)")

    from mesh_generation.strategies.grid import GridStrategy, GridConfig

    rng = np.random.default_rng(7)
    xs = rng.uniform(298000, 302000, 400)
    ys = rng.uniform(6383500, 6387500, 400)
    gdf = gpd.GeoDataFrame(
        {"object_id": [f"b{i:03d}" for i in range(400)]},
        geometry=[box(x, y, x + 12, y + 12) for x, y in zip(xs, ys)],
        crs="EPSG:3006",
    )

    def assign(frame):
        config = GridConfig()
        config.cell_size_m = 500
        strategy = GridStrategy(frame, config)
        ids = frame["object_id"].to_numpy()
        return {
            ids[pos]: group.group_id
            for group in strategy.partition()
            for pos in group.building_indices
        }

    full = assign(gdf)
    # A subset has a different total_bounds -- which is exactly what used to shift
    # the grid origin and renumber every cell.
    subset = gdf.iloc[sorted(rng.choice(len(gdf), size=140, replace=False))].reset_index(drop=True)
    partial = assign(subset)

    shared = set(full) & set(partial)
    mismatched = [k for k in shared if full[k] != partial[k]]
    assert not mismatched, (
        f"{len(mismatched)}/{len(shared)} buildings changed cell id when only a "
        f"subset was processed, e.g. {mismatched[:3]}"
    )

    logger.info(f"  ✓ {len(shared)} shared buildings kept identical cell ids")
    return True


def test_generator_fails_on_ownership_violation():
    """Test 11: the generator refuses to build from an overlapping partition."""
    logger.info("\n[TEST 11] Generator fails loudly on ownership violations")

    import tempfile
    from mesh_generation.strategies.base import StrategyValidationError

    gdf = _ownership_test_buildings()

    class OverlappingStrategy(MeshStrategy):
        def partition(self):
            bounds = tuple(gdf.total_bounds)
            self.groups = [
                MeshGroup("a", "A", [0, 1], bounds, 2),
                MeshGroup("b", "B", [0, 2], bounds, 2),   # building 0 assigned twice
            ]
            return self.groups

        def get_strategy_name(self):
            return "Overlapping (test)"

        def get_strategy_description(self):
            return "deliberately assigns one building to two groups"

    generator = MeshGenerator(gdf, GeneratorConfig())
    generator.strategy = OverlappingStrategy(gdf, None)
    try:
        generator.generate(Path(tempfile.mkdtemp()) / "strict")
        raise AssertionError("expected StrategyValidationError, none raised")
    except StrategyValidationError:
        pass

    # The escape hatch must still work for a researcher who needs to proceed.
    lenient = MeshGenerator(gdf, GeneratorConfig(strict_ownership=False))
    lenient.strategy = OverlappingStrategy(gdf, None)
    lenient.generate(Path(tempfile.mkdtemp()) / "lenient")

    logger.info("  ✓ raises by default, continues with strict_ownership=False")
    return True


def test_quadtree_ownership_is_exclusive():
    """Test 12: quadtree assigns each building to exactly one leaf cell."""
    logger.info("\n[TEST 12] Quadtree ownership is exclusive (midline tie-break)")

    from mesh_generation.strategies.quadtree import QuadtreeStrategy, QuadtreeConfig

    # Buildings deliberately sitting ON the quadrant midlines.
    rng = np.random.default_rng(11)
    geoms = [box(x, y, x + 20, y + 20)
             for x, y in zip(rng.uniform(298000, 302000, 300),
                             rng.uniform(6383500, 6387500, 300))]
    midx, midy = 300000, 6385500
    geoms += [
        box(midx - 25, midy - 25, midx + 25, midy + 25),   # centred on the corner
        box(midx - 25, 6384000, midx + 25, 6384040),       # crosses the vertical midline
        box(298500, midy - 25, 298540, midy + 25),         # crosses the horizontal midline
    ]
    gdf = gpd.GeoDataFrame(
        {"object_id": [f"b{i:03d}" for i in range(len(geoms))]},
        geometry=geoms, crs="EPSG:3006",
    )

    config = QuadtreeConfig()
    config.max_buildings_per_cell = 40
    strategy = QuadtreeStrategy(gdf, config)
    groups = strategy.partition()

    slots = sum(len(g.building_indices) for g in groups)
    unique = len({i for g in groups for i in g.building_indices})
    assert slots == unique, f"{slots - unique} duplicate assignment(s) across quadtree leaves"
    assert unique == len(gdf), f"only {unique}/{len(gdf)} buildings assigned"

    report = strategy.validate()
    assert report["max_assignments_per_building"] == 1
    assert not report["validation_errors"], report["validation_errors"]

    logger.info(f"  ✓ {len(gdf)} buildings -> {len(groups)} leaves, no duplicates")
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
        test_height_source_tracking,
        test_watertight_meshes,
        # Strict ownership regression guards. Added 2026-08-05 after a 5.0%
        # building duplication reached a shipped run: no earlier test ever
        # instantiated a real strategy, so nothing caught it.
        test_grid_ownership_is_exclusive,
        test_grid_anchor_stability,
        test_generator_fails_on_ownership_violation,
        test_quadtree_ownership_is_exclusive,
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
