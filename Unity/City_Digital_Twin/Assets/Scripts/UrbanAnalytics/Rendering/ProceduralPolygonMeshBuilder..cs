using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Core;
using UrbanAnalytics.Spatial;
using UrbanAnalytics.Spatial.Geometry;

namespace UrbanAnalytics.Rendering
{
    /// <summary>
    /// Builds batched Unity meshes from semantic polygon-based
    /// SpatialUnits.
    ///
    /// Supported runtime geometry:
    /// - PolygonGeometry
    /// - MultiPolygonGeometry
    ///
    /// Responsibilities:
    /// - triangulate source-CRS polygons;
    /// - convert source coordinates through SpatialReferenceManager;
    /// - combine many SpatialUnits into one Unity mesh;
    /// - maintain per-unit vertex/triangle ranges;
    /// - maintain triangle -> SpatialUnit ID mappings;
    /// - initialize vertex colors for later visualization encoding.
    ///
    /// This class does NOT:
    /// - modify semantic SpatialUnit geometry;
    /// - perform GIS reprojection;
    /// - create GameObjects;
    /// - manage layer lifecycle;
    /// - decide analytical visualization encodings;
    /// - perform extrusion.
    /// </summary>
    public sealed class ProceduralPolygonMeshBuilder
    {
        private const double ElevationTolerance =
            1e-6;


        private readonly SpatialReferenceManager
            spatialReferenceManager;


        private readonly List<Vector3>
            vertices;

        private readonly List<int>
            indices;

        private readonly List<Vector3>
            normals;

        private readonly List<Color32>
            colors;

        private readonly List<Vector2>
            uv0;

        private readonly List<string>
            triangleUnitIds;

        private readonly List<SpatialMeshUnitRange>
            unitRanges;

        private readonly HashSet<string>
            addedUnitIds;


        public int VertexCount =>
            vertices.Count;


        public int TriangleCount =>
            indices.Count / 3;


        public int UnitCount =>
            unitRanges.Count;


        public IReadOnlyList<SpatialMeshUnitRange>
            UnitRanges =>
                unitRanges;


        public IReadOnlyList<string>
            TriangleUnitIds =>
                triangleUnitIds;


        public bool IsEmpty =>
            unitRanges.Count == 0;


        public ProceduralPolygonMeshBuilder(
            SpatialReferenceManager spatialReferenceManager,
            int expectedUnitCapacity = 128
        )
        {
            if (spatialReferenceManager == null)
            {
                throw new ArgumentNullException(
                    nameof(spatialReferenceManager)
                );
            }


            if (expectedUnitCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedUnitCapacity),
                    expectedUnitCapacity,
                    "Expected unit capacity must be greater than zero."
                );
            }


            this.spatialReferenceManager =
                spatialReferenceManager;


            /*
             * These are initial capacities only.
             *
             * Lists will expand automatically when necessary.
             */
            int estimatedVertices =
                expectedUnitCapacity * 8;

            int estimatedTriangles =
                expectedUnitCapacity * 4;


            vertices =
                new List<Vector3>(
                    estimatedVertices
                );

            indices =
                new List<int>(
                    estimatedTriangles * 3
                );

            normals =
                new List<Vector3>(
                    estimatedVertices
                );

            colors =
                new List<Color32>(
                    estimatedVertices
                );

            uv0 =
                new List<Vector2>(
                    estimatedVertices
                );

            triangleUnitIds =
                new List<string>(
                    estimatedTriangles
                );

            unitRanges =
                new List<SpatialMeshUnitRange>(
                    expectedUnitCapacity
                );

            addedUnitIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );
        }


        // =========================================================
        // ADD SPATIAL UNIT
        // =========================================================

        public SpatialMeshUnitRange AddUnit(
            SpatialUnit unit
        )
        {
            return AddUnit(
                unit,
                new Color32(
                    255,
                    255,
                    255,
                    255
                )
            );
        }


        public SpatialMeshUnitRange AddUnit(
            SpatialUnit unit,
            Color32 initialColor
        )
        {
            if (unit == null)
            {
                throw new ArgumentNullException(
                    nameof(unit)
                );
            }


            if (!addedUnitIds.Add(
                    unit.Id
                ))
            {
                throw new InvalidOperationException(
                    $"Spatial unit '{unit.Id}' has already " +
                    $"been added to this mesh builder."
                );
            }


            int vertexStart =
                vertices.Count;

            int triangleStart =
                TriangleCount;


            try
            {
                switch (unit.Geometry)
                {
                    case PolygonGeometry polygon:
                        {
                            AppendPolygon(
                                polygon,
                                unit.Id,
                                initialColor
                            );

                            break;
                        }


                    case MultiPolygonGeometry multiPolygon:
                        {
                            AppendMultiPolygon(
                                multiPolygon,
                                unit.Id,
                                initialColor
                            );

                            break;
                        }


                    default:
                        {
                            throw new NotSupportedException(
                                $"Spatial unit '{unit.Id}' uses geometry " +
                                $"type '{unit.Geometry.GeometryType}', which " +
                                $"cannot be rendered by " +
                                $"{nameof(ProceduralPolygonMeshBuilder)}."
                            );
                        }
                }
            }
            catch
            {
                RollBack(
                    vertexStart,
                    triangleStart
                );

                addedUnitIds.Remove(
                    unit.Id
                );

                throw;
            }


            int generatedVertexCount =
                vertices.Count -
                vertexStart;

            int generatedTriangleCount =
                TriangleCount -
                triangleStart;


            if (generatedVertexCount <= 0 ||
                generatedTriangleCount <= 0)
            {
                RollBack(
                    vertexStart,
                    triangleStart
                );

                addedUnitIds.Remove(
                    unit.Id
                );

                throw new InvalidOperationException(
                    $"Spatial unit '{unit.Id}' generated no " +
                    $"renderable mesh geometry."
                );
            }


            var range =
                new SpatialMeshUnitRange(
                    unit.Id,
                    vertexStart,
                    generatedVertexCount,
                    triangleStart,
                    generatedTriangleCount
                );


            unitRanges.Add(
                range
            );


            return range;
        }


        // =========================================================
        // POLYGON
        // =========================================================

        private void AppendPolygon(
            PolygonGeometry polygon,
            string unitId,
            Color32 color
        )
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(
                    nameof(polygon)
                );
            }


            double elevation =
                ResolvePlanarElevation(
                    polygon
                );


            PolygonTriangulationResult result;

            try
            {
                result =
                    PolygonTriangulator.Triangulate(
                        polygon
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Failed to triangulate polygon for " +
                    $"spatial unit '{unitId}'.",
                    exception
                );
            }


            AppendTriangulation(
                result,
                elevation,
                unitId,
                color
            );
        }


        // =========================================================
        // MULTI POLYGON
        // =========================================================

        private void AppendMultiPolygon(
            MultiPolygonGeometry multiPolygon,
            string unitId,
            Color32 color
        )
        {
            if (multiPolygon == null)
            {
                throw new ArgumentNullException(
                    nameof(multiPolygon)
                );
            }


            if (multiPolygon.Polygons == null ||
                multiPolygon.Polygons.Count == 0)
            {
                throw new ArgumentException(
                    $"MultiPolygon for spatial unit '{unitId}' " +
                    $"contains no polygon parts.",
                    nameof(multiPolygon)
                );
            }


            for (
                int i = 0;
                i < multiPolygon.Polygons.Count;
                i++
            )
            {
                PolygonGeometry polygon =
                    multiPolygon.Polygons[i];


                if (polygon == null)
                {
                    throw new InvalidOperationException(
                        $"MultiPolygon for spatial unit '{unitId}' " +
                        $"contains a null polygon at index {i}."
                    );
                }


                AppendPolygon(
                    polygon,
                    unitId,
                    color
                );
            }
        }


        // =========================================================
        // TRIANGULATION -> UNITY
        // =========================================================

        private void AppendTriangulation(
            PolygonTriangulationResult triangulation,
            double elevation,
            string unitId,
            Color32 color
        )
        {
            if (triangulation == null)
            {
                throw new ArgumentNullException(
                    nameof(triangulation)
                );
            }


            if (triangulation.VertexCount == 0 ||
                triangulation.TriangleCount == 0)
            {
                throw new InvalidOperationException(
                    $"Triangulation for spatial unit '{unitId}' " +
                    $"contains no renderable geometry."
                );
            }


            int localVertexOffset =
                vertices.Count;


            // -----------------------------------------------------
            // Convert source CRS -> Unity-local coordinates
            // -----------------------------------------------------

            for (
                int i = 0;
                i < triangulation.Vertices.Count;
                i++
            )
            {
                TriangulatedVertex2D sourceVertex =
                    triangulation.Vertices[i];


                Vector3 unityVertex =
                    spatialReferenceManager.ToUnity(
                        sourceVertex.Easting,
                        sourceVertex.Northing,
                        elevation
                    );


                vertices.Add(
                    unityVertex
                );


                normals.Add(
                    Vector3.up
                );


                colors.Add(
                    color
                );


                /*
                 * Basic planar UVs based on local Unity position.
                 *
                 * These are not GIS texture coordinates.
                 * They simply provide valid UV data for materials.
                 */
                uv0.Add(
                    new Vector2(
                        unityVertex.x,
                        unityVertex.z
                    )
                );
            }


            // -----------------------------------------------------
            // Append triangle indices
            // -----------------------------------------------------

            for (
                int i = 0;
                i < triangulation.Indices.Count;
                i += 3
            )
            {
                int a =
                    triangulation.Indices[i];

                int b =
                    triangulation.Indices[i + 1];

                int c =
                    triangulation.Indices[i + 2];


                ValidateTriangulationIndex(
                    a,
                    triangulation.VertexCount,
                    unitId
                );

                ValidateTriangulationIndex(
                    b,
                    triangulation.VertexCount,
                    unitId
                );

                ValidateTriangulationIndex(
                    c,
                    triangulation.VertexCount,
                    unitId
                );


                indices.Add(
                    localVertexOffset + a
                );

                indices.Add(
                    localVertexOffset + b
                );

                indices.Add(
                    localVertexOffset + c
                );


                /*
                 * Exactly one semantic mapping entry exists
                 * for every Unity triangle.
                 */
                triangleUnitIds.Add(
                    unitId
                );
            }
        }


        private static void ValidateTriangulationIndex(
            int index,
            int vertexCount,
            string unitId
        )
        {
            if (index < 0 ||
                index >= vertexCount)
            {
                throw new InvalidOperationException(
                    $"Triangulation for unit '{unitId}' references " +
                    $"invalid vertex index {index}. " +
                    $"Vertex count: {vertexCount}."
                );
            }
        }


        // =========================================================
        // PLANAR ELEVATION
        // =========================================================

        /// <summary>
        /// Triangle.NET triangulates in 2D.
        ///
        /// Therefore a single analytical polygon surface must
        /// currently be planar in source elevation.
        ///
        /// Ruta and DeSO boundaries are 2D administrative /
        /// statistical geometries, so their elevation is normally 0.
        /// </summary>
        private static double ResolvePlanarElevation(
            PolygonGeometry polygon
        )
        {
            if (polygon.Exterior == null ||
                polygon.Exterior.Coordinates == null ||
                polygon.Exterior.Coordinates.Count == 0)
            {
                throw new InvalidOperationException(
                    "Polygon has no exterior coordinates."
                );
            }


            double elevation =
                polygon.Exterior
                    .Coordinates[0]
                    .Elevation;


            ValidateRingElevation(
                polygon.Exterior,
                elevation,
                "exterior"
            );


            if (polygon.Holes != null)
            {
                for (
                    int i = 0;
                    i < polygon.Holes.Count;
                    i++
                )
                {
                    ValidateRingElevation(
                        polygon.Holes[i],
                        elevation,
                        $"hole[{i}]"
                    );
                }
            }


            return elevation;
        }


        private static void ValidateRingElevation(
            PolygonRing ring,
            double expectedElevation,
            string ringName
        )
        {
            if (ring == null)
            {
                throw new ArgumentNullException(
                    nameof(ring)
                );
            }


            if (ring.Coordinates == null)
            {
                throw new InvalidOperationException(
                    $"Polygon {ringName} contains no coordinates."
                );
            }


            for (
                int i = 0;
                i < ring.Coordinates.Count;
                i++
            )
            {
                double elevation =
                    ring.Coordinates[i]
                        .Elevation;


                if (Math.Abs(
                        elevation -
                        expectedElevation
                    ) >
                    ElevationTolerance)
                {
                    throw new NotSupportedException(
                        $"Polygon {ringName} is not planar. " +
                        $"Coordinate {i} has elevation {elevation}, " +
                        $"while expected elevation is " +
                        $"{expectedElevation}."
                    );
                }
            }
        }


        // =========================================================
        // BUILD FINAL UNITY MESH
        // =========================================================

        public Mesh BuildMesh(
            string meshName
        )
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException(
                    "Cannot build a mesh because no spatial " +
                    "units have been added."
                );
            }


            ValidateInternalState();


            var mesh =
                new Mesh();


            mesh.name =
                string.IsNullOrWhiteSpace(
                    meshName
                )
                    ? "SpatialPolygonMesh"
                    : meshName.Trim();


            /*
             * UInt16 is slightly more compact.
             *
             * Switch automatically when a chunk contains
             * more than 65,535 vertices.
             */
            mesh.indexFormat =
                vertices.Count >
                ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;


            mesh.SetVertices(
                vertices
            );


            mesh.SetTriangles(
                indices,
                0,
                false
            );


            mesh.SetNormals(
                normals
            );


            mesh.SetColors(
                colors
            );


            mesh.SetUVs(
                0,
                uv0
            );


            mesh.RecalculateBounds();


            return mesh;
        }


        // =========================================================
        // CLEAR
        // =========================================================

        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
            normals.Clear();
            colors.Clear();
            uv0.Clear();

            triangleUnitIds.Clear();
            unitRanges.Clear();
            addedUnitIds.Clear();
        }


        // =========================================================
        // ROLLBACK
        // =========================================================

        private void RollBack(
            int vertexStart,
            int triangleStart
        )
        {
            if (vertexStart < 0 ||
                vertexStart > vertices.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(vertexStart)
                );
            }


            if (triangleStart < 0 ||
                triangleStart > TriangleCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triangleStart)
                );
            }


            int verticesToRemove =
                vertices.Count -
                vertexStart;


            if (verticesToRemove > 0)
            {
                vertices.RemoveRange(
                    vertexStart,
                    verticesToRemove
                );

                normals.RemoveRange(
                    vertexStart,
                    verticesToRemove
                );

                colors.RemoveRange(
                    vertexStart,
                    verticesToRemove
                );

                uv0.RemoveRange(
                    vertexStart,
                    verticesToRemove
                );
            }


            int indexStart =
                triangleStart * 3;


            int indicesToRemove =
                indices.Count -
                indexStart;


            if (indicesToRemove > 0)
            {
                indices.RemoveRange(
                    indexStart,
                    indicesToRemove
                );
            }


            int mappingsToRemove =
                triangleUnitIds.Count -
                triangleStart;


            if (mappingsToRemove > 0)
            {
                triangleUnitIds.RemoveRange(
                    triangleStart,
                    mappingsToRemove
                );
            }
        }


        // =========================================================
        // INTERNAL VALIDATION
        // =========================================================

        private void ValidateInternalState()
        {
            if (vertices.Count == 0)
            {
                throw new InvalidOperationException(
                    "Mesh contains no vertices."
                );
            }


            if (indices.Count == 0 ||
                indices.Count % 3 != 0)
            {
                throw new InvalidOperationException(
                    "Mesh contains an invalid triangle index buffer."
                );
            }


            if (normals.Count !=
                vertices.Count)
            {
                throw new InvalidOperationException(
                    "Normal count does not match vertex count."
                );
            }


            if (colors.Count !=
                vertices.Count)
            {
                throw new InvalidOperationException(
                    "Vertex-color count does not match vertex count."
                );
            }


            if (uv0.Count !=
                vertices.Count)
            {
                throw new InvalidOperationException(
                    "UV count does not match vertex count."
                );
            }


            if (triangleUnitIds.Count !=
                TriangleCount)
            {
                throw new InvalidOperationException(
                    "Triangle-to-unit mapping count does not " +
                    "match generated triangle count."
                );
            }


            if (unitRanges.Count !=
                addedUnitIds.Count)
            {
                throw new InvalidOperationException(
                    "Spatial unit registry is internally inconsistent."
                );
            }


            int expectedVertexStart =
                0;

            int expectedTriangleStart =
                0;


            foreach (
                SpatialMeshUnitRange range
                in unitRanges
            )
            {
                /*
                 * SpatialMeshChunk depends on every unit occupying
                 * one contiguous range in the combined mesh.
                 */
                if (range.VertexStart !=
                    expectedVertexStart)
                {
                    throw new InvalidOperationException(
                        $"Vertex range for unit '{range.UnitId}' " +
                        $"is not contiguous."
                    );
                }


                if (range.TriangleStart !=
                    expectedTriangleStart)
                {
                    throw new InvalidOperationException(
                        $"Triangle range for unit '{range.UnitId}' " +
                        $"is not contiguous."
                    );
                }


                expectedVertexStart =
                    range.VertexEndExclusive;

                expectedTriangleStart =
                    range.TriangleEndExclusive;
            }


            if (expectedVertexStart !=
                vertices.Count)
            {
                throw new InvalidOperationException(
                    "Spatial unit ranges do not cover the complete " +
                    "mesh vertex buffer."
                );
            }


            if (expectedTriangleStart !=
                TriangleCount)
            {
                throw new InvalidOperationException(
                    "Spatial unit ranges do not cover the complete " +
                    "mesh triangle buffer."
                );
            }


            for (
                int i = 0;
                i < indices.Count;
                i++
            )
            {
                int index =
                    indices[i];


                if (index < 0 ||
                    index >= vertices.Count)
                {
                    throw new InvalidOperationException(
                        $"Mesh index {i} references invalid " +
                        $"vertex {index}."
                    );
                }
            }
        }
    }
}