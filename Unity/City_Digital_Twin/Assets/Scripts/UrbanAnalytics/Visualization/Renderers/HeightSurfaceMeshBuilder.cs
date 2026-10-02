using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Colours of one unit's height mark: Upper for the part
    /// above the base, Lower for the part below it (bidirectional
    /// marks). Unidirectional marks use Upper only.
    /// </summary>
    internal readonly struct UnitHeightColors
    {
        public readonly Color32 Upper;

        public readonly Color32 Lower;


        public UnitHeightColors(
            Color32 upper,
            Color32 lower
        )
        {
            Upper =
                upper;

            Lower =
                lower;
        }
    }


    /// <summary>
    /// Generates one data-driven height mesh from an existing
    /// flat SpatialMeshChunk.
    ///
    /// Data lookup and colouring are supplied by the caller
    /// (HeightSurfaceRenderer), so this class is geometry only.
    ///
    /// Supported:
    /// - SurfaceDisplacement (plateau at the signed height)
    /// - FullExtrusion / InsetExtrusion (column from Bottom to
    ///   Top; a bidirectional column is split at the base into
    ///   an upper and a lower part with their own colours)
    /// - DownwardExtrusion (column from -Top to the base)
    /// </summary>
    internal static class HeightSurfaceMeshBuilder
    {
        internal sealed class Result
        {
            public Mesh Mesh;

            public List<SpatialMeshUnitRange> UnitRanges;

            public List<string> TriangleUnitIds;
        }


        private struct EdgeInfo
        {
            public int A;

            public int B;

            public int Count;
        }


        public static Result Build(
            SpatialMeshChunk sourceChunk,
            Func<string, HeightExtent> extentOf,
            Func<SpatialMeshUnitRange, Color32[], UnitHeightColors> colorsOf,
            HeightSurfaceSettings surfaceSettings
        )
        {
            if (sourceChunk == null)
            {
                throw new ArgumentNullException(
                    nameof(sourceChunk)
                );
            }


            Mesh sourceMesh =
                sourceChunk.Mesh;


            if (sourceMesh == null)
            {
                throw new InvalidOperationException(
                    "Source SpatialMeshChunk has no mesh."
                );
            }


            Vector3[] sourceVertices =
                sourceMesh.vertices;

            int[] sourceTriangles =
                sourceMesh.triangles;

            Color32[] sourceColors =
                sourceMesh.colors32;


            var vertices =
                new List<Vector3>(
                    sourceVertices.Length * 4
                );

            var triangles =
                new List<int>(
                    sourceTriangles.Length * 6
                );

            var colors =
                new List<Color32>(
                    sourceVertices.Length * 4
                );

            var unitRanges =
                new List<SpatialMeshUnitRange>(
                    sourceChunk.UnitRanges.Count
                );

            var triangleUnitIds =
                new List<string>();


            foreach (
                SpatialMeshUnitRange range
                in sourceChunk.UnitRanges
            )
            {
                int outputVertexStart =
                    vertices.Count;

                int outputTriangleStart =
                    triangles.Count / 3;


                // Missing data → zero extent (flat), as before.
                HeightExtent extent =
                    extentOf(
                        range.UnitId
                    );


                UnitHeightColors unitColors =
                    colorsOf(
                        range,
                        sourceColors.Length == sourceMesh.vertexCount
                            ? sourceColors
                            : null
                    );


                AppendUnit(
                    sourceVertices,
                    sourceTriangles,
                    range,
                    extent,
                    unitColors,
                    surfaceSettings,
                    vertices,
                    triangles,
                    colors,
                    triangleUnitIds
                );


                int outputVertexCount =
                    vertices.Count -
                    outputVertexStart;

                int outputTriangleCount =
                    triangles.Count / 3 -
                    outputTriangleStart;


                unitRanges.Add(
                    new SpatialMeshUnitRange(
                        range.UnitId,
                        outputVertexStart,
                        outputVertexCount,
                        outputTriangleStart,
                        outputTriangleCount
                    )
                );
            }


            var mesh =
                new Mesh
                {
                    name =
                        sourceMesh.name +
                        "_HeightVisualization"
                };


            mesh.indexFormat =
                vertices.Count >
                ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;


            mesh.SetVertices(
                vertices
            );

            mesh.SetTriangles(
                triangles,
                0,
                false
            );

            mesh.SetColors(
                colors
            );

            mesh.RecalculateBounds();


            return new Result
            {
                Mesh =
                    mesh,

                UnitRanges =
                    unitRanges,

                TriangleUnitIds =
                    triangleUnitIds
            };
        }


        // =========================================================
        // UNIT GEOMETRY
        // =========================================================

        private static void AppendUnit(
            Vector3[] sourceVertices,
            int[] sourceTriangles,
            SpatialMeshUnitRange range,
            HeightExtent extent,
            UnitHeightColors unitColors,
            HeightSurfaceSettings settings,
            List<Vector3> outputVertices,
            List<int> outputTriangles,
            List<Color32> outputColors,
            List<string> triangleUnitIds
        )
        {
            switch (settings.Method)
            {
                case HeightVisualizationMethod.SurfaceDisplacement:
                    {
                        // Exactly one of Top / Bottom is non-zero.
                        AppendSurfaceDisplacement(
                            sourceVertices,
                            sourceTriangles,
                            range,
                            extent.Top + extent.Bottom,
                            extent.IsBelowBase
                                ? unitColors.Lower
                                : unitColors.Upper,
                            outputVertices,
                            outputTriangles,
                            outputColors,
                            triangleUnitIds
                        );

                        return;
                    }


                case HeightVisualizationMethod.FullExtrusion:
                case HeightVisualizationMethod.InsetExtrusion:
                    {
                        float inset =
                            ResolveInsetFactor(
                                settings
                            );


                        // Upper part (always emitted, so every unit
                        // keeps at least one prism even at zero
                        // height — needed for picking and ranges).
                        if (extent.Top > 0.0f ||
                            !extent.IsBelowBase)
                        {
                            AppendPrism(
                                sourceVertices,
                                sourceTriangles,
                                range,
                                0.0f,
                                extent.Top,
                                inset,
                                unitColors.Upper,
                                outputVertices,
                                outputTriangles,
                                outputColors,
                                triangleUnitIds
                            );
                        }


                        if (extent.IsBelowBase)
                        {
                            AppendPrism(
                                sourceVertices,
                                sourceTriangles,
                                range,
                                extent.Bottom,
                                0.0f,
                                inset,
                                unitColors.Lower,
                                outputVertices,
                                outputTriangles,
                                outputColors,
                                triangleUnitIds
                            );
                        }

                        return;
                    }


                case HeightVisualizationMethod.DownwardExtrusion:
                    {
                        AppendPrism(
                            sourceVertices,
                            sourceTriangles,
                            range,
                            -extent.Top,
                            0.0f,
                            1.0f,
                            unitColors.Upper,
                            outputVertices,
                            outputTriangles,
                            outputColors,
                            triangleUnitIds
                        );

                        return;
                    }


                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        // =========================================================
        // SURFACE DISPLACEMENT
        // =========================================================

        private static void AppendSurfaceDisplacement(
            Vector3[] sourceVertices,
            int[] sourceTriangles,
            SpatialMeshUnitRange range,
            float height,
            Color32 color,
            List<Vector3> vertices,
            List<int> triangles,
            List<Color32> colors,
            List<string> triangleUnitIds
        )
        {
            int outputStart =
                vertices.Count;


            for (
                int i = 0;
                i < range.VertexCount;
                i++
            )
            {
                Vector3 position =
                    sourceVertices[
                        range.VertexStart + i
                    ];


                position.y +=
                    height;


                vertices.Add(
                    position
                );

                colors.Add(
                    color
                );
            }


            for (
                int triangleIndex = range.TriangleStart;
                triangleIndex < range.TriangleEndExclusive;
                triangleIndex++
            )
            {
                int sourceIndex =
                    triangleIndex * 3;


                int a =
                    ToLocalIndex(
                        sourceTriangles[
                            sourceIndex
                        ],
                        range
                    );

                int b =
                    ToLocalIndex(
                        sourceTriangles[
                            sourceIndex + 1
                        ],
                        range
                    );

                int c =
                    ToLocalIndex(
                        sourceTriangles[
                            sourceIndex + 2
                        ],
                        range
                    );


                AddTriangle(
                    outputStart + a,
                    outputStart + b,
                    outputStart + c,
                    range.UnitId,
                    triangles,
                    triangleUnitIds
                );
            }
        }


        // =========================================================
        // PRISM (EXTRUSION)
        // =========================================================

        /// <summary>
        /// Extrudes the unit polygon (optionally inset about its
        /// centroid) between source-y + bottomOffset and
        /// source-y + topOffset.
        /// </summary>
        private static void AppendPrism(
            Vector3[] sourceVertices,
            int[] sourceTriangles,
            SpatialMeshUnitRange range,
            float bottomOffset,
            float topOffset,
            float insetFactor,
            Color32 color,
            List<Vector3> vertices,
            List<int> triangles,
            List<Color32> colors,
            List<string> triangleUnitIds
        )
        {
            Vector3 centroid =
                CalculateCentroid(
                    sourceVertices,
                    range
                );


            int bottomStart =
                vertices.Count;


            /*
             * Bottom vertices.
             */
            for (
                int i = 0;
                i < range.VertexCount;
                i++
            )
            {
                Vector3 transformed =
                    ApplyInset(
                        sourceVertices[
                            range.VertexStart + i
                        ],
                        centroid,
                        insetFactor
                    );


                transformed.y +=
                    bottomOffset;


                vertices.Add(
                    transformed
                );

                colors.Add(
                    color
                );
            }


            int topStart =
                vertices.Count;


            /*
             * Top vertices.
             */
            for (
                int i = 0;
                i < range.VertexCount;
                i++
            )
            {
                Vector3 transformed =
                    ApplyInset(
                        sourceVertices[
                            range.VertexStart + i
                        ],
                        centroid,
                        insetFactor
                    );


                transformed.y +=
                    topOffset;


                vertices.Add(
                    transformed
                );

                colors.Add(
                    color
                );
            }


            var boundaryEdges =
                new Dictionary<ulong, EdgeInfo>();


            /*
             * Top + bottom triangles and boundary-edge detection.
             */
            for (
                int triangleIndex = range.TriangleStart;
                triangleIndex < range.TriangleEndExclusive;
                triangleIndex++
            )
            {
                int sourceIndex =
                    triangleIndex * 3;


                int a =
                    ToLocalIndex(
                        sourceTriangles[
                            sourceIndex
                        ],
                        range
                    );

                int b =
                    ToLocalIndex(
                        sourceTriangles[
                            sourceIndex + 1
                        ],
                        range
                    );

                int c =
                    ToLocalIndex(
                        sourceTriangles[
                            sourceIndex + 2
                        ],
                        range
                    );


                AddTriangle(
                    topStart + a,
                    topStart + b,
                    topStart + c,
                    range.UnitId,
                    triangles,
                    triangleUnitIds
                );


                /*
                 * Bottom winding is reversed.
                 */
                AddTriangle(
                    bottomStart + c,
                    bottomStart + b,
                    bottomStart + a,
                    range.UnitId,
                    triangles,
                    triangleUnitIds
                );


                RegisterEdge(
                    boundaryEdges,
                    a,
                    b
                );

                RegisterEdge(
                    boundaryEdges,
                    b,
                    c
                );

                RegisterEdge(
                    boundaryEdges,
                    c,
                    a
                );
            }


            /*
             * Side walls are generated only from boundary edges.
             *
             * Two-sided triangles are intentionally generated.
             * The current analytical material is unlit and this
             * keeps wall visibility robust regardless of polygon
             * winding.
             */
            foreach (
                EdgeInfo edge
                in boundaryEdges.Values
            )
            {
                if (edge.Count != 1)
                {
                    continue;
                }


                int bottomA =
                    bottomStart +
                    edge.A;

                int bottomB =
                    bottomStart +
                    edge.B;

                int topA =
                    topStart +
                    edge.A;

                int topB =
                    topStart +
                    edge.B;


                AddTriangle(
                    bottomA,
                    bottomB,
                    topB,
                    range.UnitId,
                    triangles,
                    triangleUnitIds
                );

                AddTriangle(
                    bottomA,
                    topB,
                    topA,
                    range.UnitId,
                    triangles,
                    triangleUnitIds
                );


                /*
                 * Reverse side for robust double-sided walls.
                 */
                AddTriangle(
                    topB,
                    bottomB,
                    bottomA,
                    range.UnitId,
                    triangles,
                    triangleUnitIds
                );

                AddTriangle(
                    topA,
                    topB,
                    bottomA,
                    range.UnitId,
                    triangles,
                    triangleUnitIds
                );
            }
        }


        // =========================================================
        // HELPERS
        // =========================================================

        /// <summary>
        /// Horizontal scale applied to each unit's column, about
        /// CalculateCentroid. 1 = no inset. Shared with
        /// HeightSurfaceRenderer so buildings following an inset
        /// surface are scaled identically.
        /// </summary>
        internal static float ResolveInsetFactor(
            HeightSurfaceSettings settings
        )
        {
            if (settings == null ||
                settings.Method !=
                    HeightVisualizationMethod
                        .InsetExtrusion)
            {
                return 1.0f;
            }


            return Mathf.Clamp(
                settings.InsetFactor,
                0.1f,
                1.0f
            );
        }


        internal static Vector3 CalculateCentroid(
            Vector3[] vertices,
            SpatialMeshUnitRange range
        )
        {
            Vector3 sum =
                Vector3.zero;


            for (
                int i = 0;
                i < range.VertexCount;
                i++
            )
            {
                sum +=
                    vertices[
                        range.VertexStart + i
                    ];
            }


            return sum /
                   Mathf.Max(
                       1,
                       range.VertexCount
                   );
        }


        private static Vector3 ApplyInset(
            Vector3 position,
            Vector3 centroid,
            float factor
        )
        {
            return new Vector3(
                centroid.x +
                (position.x - centroid.x) *
                factor,

                position.y,

                centroid.z +
                (position.z - centroid.z) *
                factor
            );
        }


        private static int ToLocalIndex(
            int globalIndex,
            SpatialMeshUnitRange range
        )
        {
            int local =
                globalIndex -
                range.VertexStart;


            if (local < 0 ||
                local >= range.VertexCount)
            {
                throw new InvalidOperationException(
                    $"Triangle for '{range.UnitId}' references " +
                    $"a vertex outside its semantic vertex range."
                );
            }


            return local;
        }


        private static void RegisterEdge(
            Dictionary<ulong, EdgeInfo> edges,
            int a,
            int b
        )
        {
            int minimum =
                Mathf.Min(
                    a,
                    b
                );

            int maximum =
                Mathf.Max(
                    a,
                    b
                );


            ulong key =
                ((ulong)(uint)minimum << 32) |
                (uint)maximum;


            if (edges.TryGetValue(
                    key,
                    out EdgeInfo existing
                ))
            {
                existing.Count++;

                edges[
                    key
                ] = existing;
            }
            else
            {
                edges.Add(
                    key,
                    new EdgeInfo
                    {
                        A = a,
                        B = b,
                        Count = 1
                    }
                );
            }
        }


        private static void AddTriangle(
            int a,
            int b,
            int c,
            string unitId,
            List<int> triangles,
            List<string> triangleUnitIds
        )
        {
            triangles.Add(
                a
            );

            triangles.Add(
                b
            );

            triangles.Add(
                c
            );


            triangleUnitIds.Add(
                unitId
            );
        }
    }
}
