using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Generates one data-driven height mesh from an existing
    /// flat SpatialMeshChunk.
    ///
    /// Supported:
    /// - SurfaceDisplacement
    /// - FullExtrusion
    /// - InsetExtrusion
    /// - DownwardExtrusion
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

            DataLayer heightDataLayer,
            string heightVariableId,
            ResolvedNumericScale heightScale,
            HeightEncodingSettings heightSettings,
            HeightSurfaceSettings surfaceSettings,

            DataLayer colorDataLayer,
            string colorVariableId,
            ResolvedNumericScale? colorScale,
            ColorEncodingSettings colorSettings
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


                float height =
                    ResolveHeight(
                        range.UnitId,
                        heightDataLayer,
                        heightVariableId,
                        heightScale,
                        heightSettings
                    );


                Color32 unitColor =
                    ResolveColor(
                        sourceMesh,
                        sourceColors,
                        range,
                        colorDataLayer,
                        colorVariableId,
                        colorScale,
                        colorSettings
                    );


                AppendUnit(
                    sourceVertices,
                    sourceTriangles,
                    range,
                    height,
                    unitColor,
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
        // HEIGHT
        // =========================================================

        private static float ResolveHeight(
            string unitId,
            DataLayer dataLayer,
            string variableId,
            ResolvedNumericScale scale,
            HeightEncodingSettings settings
        )
        {
            if (!dataLayer.TryGetDouble(
                    unitId,
                    variableId,
                    out double value
                ))
            {
                return 0.0f;
            }


            float normalized =
                scale.Normalize(
                    value
                );


            return normalized *
                   Mathf.Max(
                       0.0f,
                       settings.MaximumVisualHeight
                   );
        }


        // =========================================================
        // COLOR
        // =========================================================

        private static Color32 ResolveColor(
            Mesh sourceMesh,
            Color32[] sourceColors,
            SpatialMeshUnitRange range,
            DataLayer colorDataLayer,
            string colorVariableId,
            ResolvedNumericScale? colorScale,
            ColorEncodingSettings colorSettings
        )
        {
            /*
             * If this height visualization has an explicit Color
             * encoding, use it.
             */
            if (colorDataLayer != null &&
                !string.IsNullOrWhiteSpace(
                    colorVariableId
                ) &&
                colorScale.HasValue &&
                colorSettings != null)
            {
                if (colorDataLayer.TryGetDouble(
                        range.UnitId,
                        colorVariableId,
                        out double value
                    ))
                {
                    float normalized =
                        colorScale.Value.Normalize(
                            value
                        );


                    return colorSettings.Evaluate(
                        normalized
                    );
                }


                return colorSettings.NoDataColor;
            }


            /*
             * Otherwise preserve any existing source vertex color.
             *
             * This allows a Surface color renderer to be composed
             * before a HeightSurface renderer if desired.
             */
            if (sourceColors != null &&
                sourceColors.Length ==
                sourceMesh.vertexCount &&
                range.VertexStart >= 0 &&
                range.VertexStart <
                sourceColors.Length)
            {
                return sourceColors[
                    range.VertexStart
                ];
            }


            return new Color32(
                255,
                255,
                255,
                255
            );
        }


        // =========================================================
        // UNIT GEOMETRY
        // =========================================================

        private static void AppendUnit(
            Vector3[] sourceVertices,
            int[] sourceTriangles,
            SpatialMeshUnitRange range,
            float height,
            Color32 color,
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
                        AppendSurfaceDisplacement(
                            sourceVertices,
                            sourceTriangles,
                            range,
                            height,
                            color,
                            outputVertices,
                            outputTriangles,
                            outputColors,
                            triangleUnitIds
                        );

                        return;
                    }


                case HeightVisualizationMethod.FullExtrusion:
                    {
                        AppendExtrusion(
                            sourceVertices,
                            sourceTriangles,
                            range,
                            height,
                            1.0f,
                            false,
                            color,
                            outputVertices,
                            outputTriangles,
                            outputColors,
                            triangleUnitIds
                        );

                        return;
                    }


                case HeightVisualizationMethod.InsetExtrusion:
                    {
                        AppendExtrusion(
                            sourceVertices,
                            sourceTriangles,
                            range,
                            height,
                            ResolveInsetFactor(
                                settings
                            ),
                            false,
                            color,
                            outputVertices,
                            outputTriangles,
                            outputColors,
                            triangleUnitIds
                        );

                        return;
                    }


                case HeightVisualizationMethod.DownwardExtrusion:
                    {
                        AppendExtrusion(
                            sourceVertices,
                            sourceTriangles,
                            range,
                            height,
                            1.0f,
                            true,
                            color,
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
        // EXTRUSION
        // =========================================================

        private static void AppendExtrusion(
            Vector3[] sourceVertices,
            int[] sourceTriangles,
            SpatialMeshUnitRange range,
            float height,
            float insetFactor,
            bool downward,
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
                Vector3 source =
                    sourceVertices[
                        range.VertexStart + i
                    ];


                Vector3 transformed =
                    ApplyInset(
                        source,
                        centroid,
                        insetFactor
                    );


                if (downward)
                {
                    transformed.y -=
                        height;
                }


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
                Vector3 source =
                    sourceVertices[
                        range.VertexStart + i
                    ];


                Vector3 transformed =
                    ApplyInset(
                        source,
                        centroid,
                        insetFactor
                    );


                if (!downward)
                {
                    transformed.y +=
                        height;
                }


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