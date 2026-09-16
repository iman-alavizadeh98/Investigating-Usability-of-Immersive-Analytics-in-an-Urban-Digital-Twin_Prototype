using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using TriangleNet.Geometry;

using UrbanAnalytics.Spatial.Geometry;

namespace UrbanAnalytics.Rendering
{
    /// <summary>
    /// One triangulated vertex expressed in the project's
    /// source CRS.
    ///
    /// Coordinates intentionally remain double precision.
    /// Conversion to Unity-local float coordinates happens later
    /// through SpatialReferenceManager.
    /// </summary>
    public readonly struct TriangulatedVertex2D :
        IEquatable<TriangulatedVertex2D>
    {
        public double Easting
        {
            get;
        }

        public double Northing
        {
            get;
        }


        public TriangulatedVertex2D(
            double easting,
            double northing
        )
        {
            if (double.IsNaN(easting) ||
                double.IsInfinity(easting))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(easting),
                    easting,
                    "Easting must be finite."
                );
            }

            if (double.IsNaN(northing) ||
                double.IsInfinity(northing))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(northing),
                    northing,
                    "Northing must be finite."
                );
            }

            Easting =
                easting;

            Northing =
                northing;
        }


        public bool Equals(
            TriangulatedVertex2D other
        )
        {
            return
                Easting.Equals(other.Easting) &&
                Northing.Equals(other.Northing);
        }


        public override bool Equals(
            object obj
        )
        {
            return
                obj is TriangulatedVertex2D other &&
                Equals(other);
        }


        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                hash =
                    hash * 31 +
                    Easting.GetHashCode();

                hash =
                    hash * 31 +
                    Northing.GetHashCode();

                return hash;
            }
        }


        public static bool operator ==(
            TriangulatedVertex2D left,
            TriangulatedVertex2D right
        )
        {
            return left.Equals(
                right
            );
        }


        public static bool operator !=(
            TriangulatedVertex2D left,
            TriangulatedVertex2D right
        )
        {
            return !left.Equals(
                right
            );
        }


        public override string ToString()
        {
            return
                $"({Easting}, {Northing})";
        }
    }


    /// <summary>
    /// Immutable result of triangulating one PolygonGeometry.
    ///
    /// Vertices remain in source CRS coordinates.
    ///
    /// Indices are ordered so that after the standard mapping:
    ///
    /// Easting  -> Unity X
    /// Northing -> Unity Z
    ///
    /// the resulting surface faces Unity +Y.
    /// </summary>
    public sealed class PolygonTriangulationResult
    {
        private readonly IReadOnlyList<TriangulatedVertex2D>
            vertices;

        private readonly IReadOnlyList<int>
            indices;


        public IReadOnlyList<TriangulatedVertex2D>
            Vertices =>
                vertices;


        public IReadOnlyList<int>
            Indices =>
                indices;


        public int VertexCount =>
            vertices.Count;


        public int TriangleCount =>
            indices.Count / 3;


        internal PolygonTriangulationResult(
            IList<TriangulatedVertex2D> vertices,
            IList<int> indices
        )
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(
                    nameof(vertices)
                );
            }

            if (indices == null)
            {
                throw new ArgumentNullException(
                    nameof(indices)
                );
            }

            if (vertices.Count < 3)
            {
                throw new ArgumentException(
                    "Triangulation result requires at least " +
                    "three vertices.",
                    nameof(vertices)
                );
            }

            if (indices.Count < 3 ||
                indices.Count % 3 != 0)
            {
                throw new ArgumentException(
                    "Triangulation index count must be a " +
                    "positive multiple of three.",
                    nameof(indices)
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
                    throw new ArgumentException(
                        $"Triangle index {i} references invalid " +
                        $"vertex {index}.",
                        nameof(indices)
                    );
                }
            }


            var vertexCopy =
                new List<TriangulatedVertex2D>(
                    vertices
                );

            var indexCopy =
                new List<int>(
                    indices
                );


            this.vertices =
                new ReadOnlyCollection<TriangulatedVertex2D>(
                    vertexCopy
                );

            this.indices =
                new ReadOnlyCollection<int>(
                    indexCopy
                );
        }
    }


    /// <summary>
    /// Adapter between the UrbanAnalytics spatial geometry model
    /// and the project's existing Triangle.NET installation.
    ///
    /// Supports:
    /// - concave polygons;
    /// - polygon holes;
    /// - double-precision source CRS coordinates.
    ///
    /// Does not:
    /// - perform CRS conversion;
    /// - create Unity meshes;
    /// - create GameObjects;
    /// - perform extrusion.
    /// </summary>
    public static class PolygonTriangulator
    {
        /*
         * Twice the signed triangle area is used for detecting
         * degenerate triangles.
         *
         * Coordinates here are source-CRS doubles rather than
         * Unity floats.
         */
        private const double DegenerateAreaTolerance =
            1e-12;


        public static PolygonTriangulationResult Triangulate(
            PolygonGeometry polygon
        )
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(
                    nameof(polygon)
                );
            }


            Polygon triangleNetPolygon =
                CreateTriangleNetPolygon(
                    polygon
                );


            var mesh =
                triangleNetPolygon.Triangulate();


            if (mesh == null)
            {
                throw new InvalidOperationException(
                    "Triangle.NET returned no triangulation mesh."
                );
            }


            var vertices =
                new List<TriangulatedVertex2D>();

            var indices =
                new List<int>();


            /*
             * Unlike the previous triangulation implementation,
             * this gives approximately O(1) vertex lookup rather
             * than scanning the entire vertex list for each
             * triangle vertex.
             */
            var vertexLookup =
                new Dictionary<
                    TriangulatedVertex2D,
                    int
                >();


            foreach (
                var triangle
                in mesh.Triangles
            )
            {
                Vertex vertexA =
                    triangle.GetVertex(0);

                Vertex vertexB =
                    triangle.GetVertex(1);

                Vertex vertexC =
                    triangle.GetVertex(2);


                if (vertexA == null ||
                    vertexB == null ||
                    vertexC == null)
                {
                    throw new InvalidOperationException(
                        "Triangle.NET produced a triangle " +
                        "containing a null vertex."
                    );
                }


                double signedDoubleArea =
                    CalculateSignedDoubleArea(
                        vertexA.X,
                        vertexA.Y,
                        vertexB.X,
                        vertexB.Y,
                        vertexC.X,
                        vertexC.Y
                    );


                /*
                 * Triangle.NET should normally not emit
                 * degenerate triangles, but we protect the
                 * Unity mesh from them.
                 */
                if (Math.Abs(
                        signedDoubleArea
                    ) <=
                    DegenerateAreaTolerance)
                {
                    continue;
                }


                int indexA =
                    GetOrAddVertex(
                        vertexA.X,
                        vertexA.Y,
                        vertices,
                        vertexLookup
                    );

                int indexB =
                    GetOrAddVertex(
                        vertexB.X,
                        vertexB.Y,
                        vertices,
                        vertexLookup
                    );

                int indexC =
                    GetOrAddVertex(
                        vertexC.X,
                        vertexC.Y,
                        vertices,
                        vertexLookup
                    );


                /*
                 * Source polygon:
                 *
                 *     Easting  = X
                 *     Northing = Y
                 *
                 * Unity:
                 *
                 *     Easting  = X
                 *     Northing = Z
                 *
                 * A counter-clockwise triangle in source XY
                 * therefore becomes downward-facing on Unity's
                 * XZ plane.
                 *
                 * We normalize every triangle to clockwise
                 * source-CRS winding so the Unity surface faces +Y.
                 */
                if (signedDoubleArea > 0.0)
                {
                    indices.Add(
                        indexA
                    );

                    indices.Add(
                        indexC
                    );

                    indices.Add(
                        indexB
                    );
                }
                else
                {
                    indices.Add(
                        indexA
                    );

                    indices.Add(
                        indexB
                    );

                    indices.Add(
                        indexC
                    );
                }
            }


            if (indices.Count == 0)
            {
                throw new InvalidOperationException(
                    "Polygon triangulation produced no " +
                    "non-degenerate triangles."
                );
            }


            return new PolygonTriangulationResult(
                vertices,
                indices
            );
        }


        // =========================================================
        // TRIANGLE.NET POLYGON
        // =========================================================

        private static Polygon CreateTriangleNetPolygon(
            PolygonGeometry polygon
        )
        {
            if (polygon.Exterior == null)
            {
                throw new ArgumentException(
                    "Polygon has no exterior ring.",
                    nameof(polygon)
                );
            }


            Polygon triangleNetPolygon =
                new Polygon();


            AddExteriorRing(
                triangleNetPolygon,
                polygon.Exterior
            );


            if (polygon.Holes != null)
            {
                for (
                    int i = 0;
                    i < polygon.Holes.Count;
                    i++
                )
                {
                    PolygonRing hole =
                        polygon.Holes[i];


                    if (hole == null)
                    {
                        throw new ArgumentException(
                            $"Polygon contains a null hole " +
                            $"at index {i}.",
                            nameof(polygon)
                        );
                    }


                    AddHoleRing(
                        triangleNetPolygon,
                        hole,
                        i
                    );
                }
            }


            return triangleNetPolygon;
        }


        /// <summary>
        /// Uses the same exterior-ring construction strategy as
        /// the existing Triangulation.cs implementation.
        ///
        /// This avoids making assumptions about differences
        /// between Triangle.NET package versions.
        /// </summary>
        private static void AddExteriorRing(
            Polygon polygon,
            PolygonRing ring
        )
        {
            ValidateRing(
                ring,
                "exterior"
            );


            int count =
                ring.Coordinates.Count;


            /*
             * Add polygon vertices.
             */
            for (
                int i = 0;
                i < count;
                i++
            )
            {
                SpatialCoordinate coordinate =
                    ring.Coordinates[i];


                polygon.Add(
                    new Vertex(
                        coordinate.Easting,
                        coordinate.Northing
                    )
                );
            }


            /*
             * Add constrained boundary segments.
             */
            for (
                int i = 0;
                i < count;
                i++
            )
            {
                int next =
                    i + 1 < count
                        ? i + 1
                        : 0;


                SpatialCoordinate current =
                    ring.Coordinates[i];

                SpatialCoordinate following =
                    ring.Coordinates[next];


                polygon.Add(
                    new Segment(
                        new Vertex(
                            current.Easting,
                            current.Northing
                        ),
                        new Vertex(
                            following.Easting,
                            following.Northing
                        )
                    )
                );
            }
        }


        /// <summary>
        /// Uses Triangle.NET's hole-contour mechanism already
        /// used by the project's existing triangulation class.
        /// </summary>
        private static void AddHoleRing(
            Polygon polygon,
            PolygonRing ring,
            int holeIndex
        )
        {
            ValidateRing(
                ring,
                $"hole[{holeIndex}]"
            );


            var holeVertices =
                new List<Vertex>(
                    ring.Coordinates.Count
                );


            for (
                int i = 0;
                i < ring.Coordinates.Count;
                i++
            )
            {
                SpatialCoordinate coordinate =
                    ring.Coordinates[i];


                holeVertices.Add(
                    new Vertex(
                        coordinate.Easting,
                        coordinate.Northing
                    )
                );
            }


            polygon.Add(
                new Contour(
                    holeVertices
                ),
                true
            );
        }


        // =========================================================
        // OUTPUT VERTEX LOOKUP
        // =========================================================

        private static int GetOrAddVertex(
            double easting,
            double northing,
            List<TriangulatedVertex2D> vertices,
            Dictionary<TriangulatedVertex2D, int> lookup
        )
        {
            var vertex =
                new TriangulatedVertex2D(
                    easting,
                    northing
                );


            if (lookup.TryGetValue(
                    vertex,
                    out int existingIndex
                ))
            {
                return existingIndex;
            }


            int newIndex =
                vertices.Count;


            vertices.Add(
                vertex
            );


            lookup.Add(
                vertex,
                newIndex
            );


            return newIndex;
        }


        // =========================================================
        // VALIDATION
        // =========================================================

        private static void ValidateRing(
            PolygonRing ring,
            string ringName
        )
        {
            if (ring == null)
            {
                throw new ArgumentNullException(
                    nameof(ring)
                );
            }


            if (ring.Coordinates == null ||
                ring.Coordinates.Count < 3)
            {
                throw new ArgumentException(
                    $"Polygon {ringName} requires at least " +
                    $"three coordinates.",
                    nameof(ring)
                );
            }


            var unique =
                new HashSet<TriangulatedVertex2D>();


            for (
                int i = 0;
                i < ring.Coordinates.Count;
                i++
            )
            {
                SpatialCoordinate coordinate =
                    ring.Coordinates[i];


                var vertex =
                    new TriangulatedVertex2D(
                        coordinate.Easting,
                        coordinate.Northing
                    );


                unique.Add(
                    vertex
                );


                int nextIndex =
                    i + 1 <
                    ring.Coordinates.Count
                        ? i + 1
                        : 0;


                SpatialCoordinate next =
                    ring.Coordinates[nextIndex];


                if (coordinate.Easting.Equals(
                        next.Easting
                    ) &&
                    coordinate.Northing.Equals(
                        next.Northing
                    ))
                {
                    throw new ArgumentException(
                        $"Polygon {ringName} contains consecutive " +
                        $"duplicate coordinates at index {i}.",
                        nameof(ring)
                    );
                }
            }


            if (unique.Count < 3)
            {
                throw new ArgumentException(
                    $"Polygon {ringName} contains fewer than " +
                    $"three unique 2D coordinates.",
                    nameof(ring)
                );
            }
        }


        private static double CalculateSignedDoubleArea(
            double ax,
            double ay,
            double bx,
            double by,
            double cx,
            double cy
        )
        {
            return
                (bx - ax) *
                (cy - ay) -

                (by - ay) *
                (cx - ax);
        }
    }
}