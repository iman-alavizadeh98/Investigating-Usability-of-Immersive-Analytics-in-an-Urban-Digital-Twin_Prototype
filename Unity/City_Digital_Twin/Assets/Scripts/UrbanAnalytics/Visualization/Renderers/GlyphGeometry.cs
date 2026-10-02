using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Pure glyph maths (no Unity objects), unit-tested.
    /// </summary>
    public static class GlyphMath
    {
        /// <summary>
        /// Cumulative segment boundaries for a stacked bar whose
        /// total height is totalHeight: n + 1 values from 0 to
        /// totalHeight, segment i spanning [b[i], b[i+1]]. Each
        /// segment's share of the height equals its share of the
        /// sum. Values must be ≥ 0. A zero sum gives all zeros.
        /// </summary>
        public static float[] StackBoundaries(
            IReadOnlyList<double> values,
            float totalHeight
        )
        {
            if (values == null)
            {
                throw new ArgumentNullException(
                    nameof(values)
                );
            }


            var boundaries =
                new float[
                    values.Count + 1
                ];


            double sum =
                0.0;


            foreach (double value in values)
            {
                if (value < 0.0 ||
                    double.IsNaN(value))
                {
                    throw new ArgumentException(
                        "Stacked values must be non-negative.",
                        nameof(values)
                    );
                }


                sum +=
                    value;
            }


            if (sum <= 0.0)
            {
                return boundaries;
            }


            double running =
                0.0;


            for (
                int i = 0;
                i < values.Count;
                i++
            )
            {
                running +=
                    values[i];

                boundaries[i + 1] =
                    (float)(running / sum * totalHeight);
            }


            // Exact top despite float rounding.
            boundaries[values.Count] =
                totalHeight;


            return boundaries;
        }


        /// <summary>
        /// Wedge radius for a normalized value. Square root, so a
        /// wedge's AREA (what the eye compares) is proportional to
        /// the value, not its radius.
        /// </summary>
        public static float RadialRadius(
            float normalized,
            float maximumRadius
        )
        {
            return Mathf.Sqrt(
                       Mathf.Clamp01(
                           normalized
                       )
                   ) *
                   maximumRadius;
        }


        /// <summary>
        /// Angle (radians) where wedge i of n starts, measured
        /// clockwise from north (+Z) when seen from above; wedge 0
        /// starts at north.
        /// </summary>
        public static float WedgeStartAngle(
            int index,
            int count
        )
        {
            return 2.0f * Mathf.PI * index / count;
        }


        /// <summary>Horizontal unit direction for an angle from north, clockwise.</summary>
        public static Vector3 Direction(
            float angle
        )
        {
            return new Vector3(
                Mathf.Sin(angle),
                0.0f,
                Mathf.Cos(angle)
            );
        }
    }


    /// <summary>
    /// Accumulates glyph geometry for one chunk with per-unit
    /// semantic ranges (for picking) and flat per-face normals.
    /// Every triangle is oriented so its front face points along
    /// the given outward normal.
    /// </summary>
    internal sealed class GlyphMeshAccumulator
    {
        private readonly List<Vector3> vertices =
            new List<Vector3>();

        private readonly List<Vector3> normals =
            new List<Vector3>();

        private readonly List<Color32> colors =
            new List<Color32>();

        private readonly List<int> triangles =
            new List<int>();

        private readonly List<SpatialMeshUnitRange> ranges =
            new List<SpatialMeshUnitRange>();

        private readonly List<string> triangleUnitIds =
            new List<string>();

        private string currentUnit;

        private int unitVertexStart;

        private int unitTriangleStart;


        public int VertexCount =>
            vertices.Count;

        public int UnitCount =>
            ranges.Count;

        public bool IsEmpty =>
            ranges.Count == 0;

        public IReadOnlyList<SpatialMeshUnitRange> Ranges =>
            ranges;

        public IReadOnlyList<string> TriangleUnitIds =>
            triangleUnitIds;


        public void BeginUnit(
            string unitId
        )
        {
            if (currentUnit != null)
            {
                throw new InvalidOperationException(
                    "EndUnit was not called."
                );
            }


            currentUnit =
                unitId;

            unitVertexStart =
                vertices.Count;

            unitTriangleStart =
                triangles.Count / 3;
        }


        /// <summary>
        /// Closes the unit. A unit that produced no triangles gets
        /// no range (and so no glyph).
        /// </summary>
        public bool EndUnit()
        {
            int vertexCount =
                vertices.Count - unitVertexStart;

            int triangleCount =
                triangles.Count / 3 - unitTriangleStart;


            bool added =
                vertexCount > 0 &&
                triangleCount > 0;


            if (added)
            {
                ranges.Add(
                    new SpatialMeshUnitRange(
                        currentUnit,
                        unitVertexStart,
                        vertexCount,
                        unitTriangleStart,
                        triangleCount
                    )
                );
            }


            currentUnit =
                null;


            return added;
        }


        /// <summary>
        /// Axis-aligned square prism centred on baseCenter (x/z),
        /// spanning baseCenter.y + yBottom .. baseCenter.y + yTop.
        /// </summary>
        public void AppendBox(
            Vector3 baseCenter,
            float halfWidth,
            float yBottom,
            float yTop,
            Color32 color
        )
        {
            float x0 = baseCenter.x - halfWidth;
            float x1 = baseCenter.x + halfWidth;
            float z0 = baseCenter.z - halfWidth;
            float z1 = baseCenter.z + halfWidth;
            float y0 = baseCenter.y + yBottom;
            float y1 = baseCenter.y + yTop;


            var b00 = new Vector3(x0, y0, z0);
            var b10 = new Vector3(x1, y0, z0);
            var b11 = new Vector3(x1, y0, z1);
            var b01 = new Vector3(x0, y0, z1);
            var t00 = new Vector3(x0, y1, z0);
            var t10 = new Vector3(x1, y1, z0);
            var t11 = new Vector3(x1, y1, z1);
            var t01 = new Vector3(x0, y1, z1);


            AddQuad(t00, t10, t11, t01, Vector3.up, color);
            AddQuad(b00, b10, b11, b01, Vector3.down, color);
            AddQuad(b00, b10, t10, t00, Vector3.back, color);
            AddQuad(b01, b11, t11, t01, Vector3.forward, color);
            AddQuad(b00, b01, t01, t00, Vector3.left, color);
            AddQuad(b10, b11, t11, t10, Vector3.right, color);
        }


        /// <summary>
        /// Flat pie-slice prism around center between angle0 and
        /// angle1 (radians from north, clockwise).
        /// </summary>
        public void AppendWedge(
            Vector3 center,
            float radius,
            float angle0,
            float angle1,
            float yBottom,
            float yTop,
            int arcSteps,
            Color32 color
        )
        {
            if (radius <= 0.0f ||
                angle1 <= angle0)
            {
                return;
            }


            int steps =
                Mathf.Max(
                    1,
                    arcSteps
                );


            Vector3 bottomCenter =
                center + Vector3.up * yBottom;

            Vector3 topCenter =
                center + Vector3.up * yTop;


            Vector3 previousDirection =
                GlyphMath.Direction(
                    angle0
                );


            for (
                int s = 1;
                s <= steps;
                s++
            )
            {
                float a0 =
                    Mathf.Lerp(
                        angle0,
                        angle1,
                        (s - 1) / (float)steps
                    );

                float a1 =
                    Mathf.Lerp(
                        angle0,
                        angle1,
                        s / (float)steps
                    );


                Vector3 d0 =
                    previousDirection;

                Vector3 d1 =
                    GlyphMath.Direction(
                        a1
                    );


                previousDirection =
                    d1;


                Vector3 bottom0 = bottomCenter + d0 * radius;
                Vector3 bottom1 = bottomCenter + d1 * radius;
                Vector3 top0 = topCenter + d0 * radius;
                Vector3 top1 = topCenter + d1 * radius;


                AddTriangle(topCenter, top0, top1, Vector3.up, color);
                AddTriangle(bottomCenter, bottom0, bottom1, Vector3.down, color);

                AddQuad(
                    bottom0,
                    bottom1,
                    top1,
                    top0,
                    GlyphMath.Direction(
                        (a0 + a1) * 0.5f
                    ),
                    color
                );
            }


            // Flat sides at the start and end angles.
            Vector3 startDirection =
                GlyphMath.Direction(
                    angle0
                );

            Vector3 endDirection =
                GlyphMath.Direction(
                    angle1
                );


            AddQuad(
                bottomCenter,
                bottomCenter + startDirection * radius,
                topCenter + startDirection * radius,
                topCenter,
                new Vector3(
                    -startDirection.z,
                    0.0f,
                    startDirection.x
                ),
                color
            );


            AddQuad(
                bottomCenter,
                bottomCenter + endDirection * radius,
                topCenter + endDirection * radius,
                topCenter,
                new Vector3(
                    endDirection.z,
                    0.0f,
                    -endDirection.x
                ),
                color
            );
        }


        public Mesh BuildMesh(
            string name
        )
        {
            var mesh =
                new Mesh
                {
                    name =
                        name,

                    indexFormat =
                        vertices.Count > ushort.MaxValue
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };


            mesh.SetVertices(
                vertices
            );

            mesh.SetNormals(
                normals
            );

            mesh.SetColors(
                colors
            );

            mesh.SetTriangles(
                triangles,
                0,
                false
            );

            mesh.RecalculateBounds();


            return mesh;
        }


        // =========================================================
        // PRIMITIVES
        // =========================================================

        private void AddQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 outward,
            Color32 color
        )
        {
            AddTriangle(a, b, c, outward, color);
            AddTriangle(a, c, d, outward, color);
        }


        /// <summary>
        /// Adds a triangle whose Unity front face (clockwise as
        /// seen by the viewer, i.e. Cross(b-a, c-a) towards the
        /// viewer) points along outward.
        /// </summary>
        private void AddTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 outward,
            Color32 color
        )
        {
            if (currentUnit == null)
            {
                throw new InvalidOperationException(
                    "BeginUnit was not called."
                );
            }


            if (Vector3.Dot(
                    Vector3.Cross(b - a, c - a),
                    outward
                ) < 0.0f)
            {
                Vector3 swap =
                    b;

                b =
                    c;

                c =
                    swap;
            }


            int start =
                vertices.Count;


            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);

            normals.Add(outward);
            normals.Add(outward);
            normals.Add(outward);

            colors.Add(color);
            colors.Add(color);
            colors.Add(color);

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            triangleUnitIds.Add(
                currentUnit
            );
        }
    }


    /// <summary>
    /// Shared plumbing for glyph renderers: root object, anchor
    /// placement, chunk creation, material.
    /// </summary>
    internal static class GlyphRendering
    {
        /// <summary>Flush a chunk once it holds this many vertices.</summary>
        public const int MaxVerticesPerChunk =
            60000;


        public static async System.Threading.Tasks.Task<GameObject>
            CreateRootAsync(
                VisualizationRenderContext context,
                VisualizationLayerSpec spec,
                System.Threading.CancellationToken cancellationToken
            )
        {
            GameObject spatialRoot =
                await context.GetRenderedSpatialLayerAsync(
                    spec.Target.LayerId,
                    cancellationToken
                );


            var root =
                new GameObject(
                    $"__Visualization_{spec.Id}"
                );


            root.transform.SetParent(
                spatialRoot.transform.parent,
                false
            );


            return root;
        }


        /// <summary>
        /// FollowHeightSurface source field, or null for Fixed
        /// placement. Throws if the source surface is missing.
        /// </summary>
        public static SurfaceElevationField ResolveFollowField(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec
        )
        {
            if (spec.UrbanContextPlacement == null ||
                !spec.UrbanContextPlacement.IsFollowingHeightSurface)
            {
                return null;
            }


            string source =
                spec.UrbanContextPlacement
                    .SourceVisualizationLayerId;


            if (!context.RuntimeState.TryGetSurfaceElevation(
                    source,
                    out SurfaceElevationField field
                ))
            {
                throw new InvalidOperationException(
                    $"Glyph layer '{spec.Id}' wants to follow " +
                    $"HeightSurface '{source}', which is not " +
                    $"rendered yet. Put the HeightSurface layer " +
                    $"before the glyph layer."
                );
            }


            return field;
        }


        /// <summary>
        /// Anchor position in root-local space, lifted onto the
        /// followed surface when there is one.
        /// </summary>
        public static Vector3 PlaceAnchor(
            DerivedAnchor anchor,
            SurfaceElevationField follow,
            Transform root
        )
        {
            Vector3 world =
                anchor.WorldPosition;


            if (follow != null &&
                follow.TryGetTopOffset(
                    anchor.UnitId,
                    out float offset
                ))
            {
                world.y +=
                    offset;
            }


            return root.InverseTransformPoint(
                world
            );
        }


        /// <summary>
        /// Turns the accumulator into a SpatialMeshChunk under root.
        /// </summary>
        public static SpatialMeshChunk CreateChunk(
            GlyphMeshAccumulator accumulator,
            Transform root,
            string spatialLayerId,
            int chunkIndex,
            Material material
        )
        {
            var chunkObject =
                new GameObject(
                    $"{root.name}_chunk_{chunkIndex:D3}"
                );


            chunkObject.transform.SetParent(
                root,
                false
            );


            var chunk =
                chunkObject.AddComponent<SpatialMeshChunk>();


            chunk.Initialize(
                spatialLayerId,
                chunkIndex,
                accumulator.BuildMesh(
                    chunkObject.name
                ),
                accumulator.Ranges,
                accumulator.TriangleUnitIds,
                material,
                false,
                true
            );


            MeshRenderer renderer =
                chunk.MeshRenderer;

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            renderer.lightProbeUsage =
                LightProbeUsage.Off;

            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.Off;


            return chunk;
        }


        /// <summary>
        /// Colour for segment i of n from a palette: categorical
        /// palettes by index, others sampled evenly.
        /// </summary>
        public static Color32 SegmentColor(
            ColorPalette palette,
            int index,
            int count
        )
        {
            if (palette.Kind ==
                ColorPaletteKind.Categorical)
            {
                return palette.GetCategory(
                    index
                );
            }


            return palette.Evaluate(
                count <= 1
                    ? 0.5f
                    : index / (float)(count - 1)
            );
        }
    }
}
