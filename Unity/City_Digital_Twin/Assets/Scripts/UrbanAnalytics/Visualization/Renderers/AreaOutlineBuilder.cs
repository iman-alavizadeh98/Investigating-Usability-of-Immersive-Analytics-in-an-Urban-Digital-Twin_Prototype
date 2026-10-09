using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Bold boundary lines of areas (AreaOutlineSettings): for every unit
    /// of a flat spatial-layer chunk, the polygon's boundary edges (edges
    /// used by only one of the unit's triangles, so holes are outlined
    /// too) become flat ribbons of the given width, lying just above the
    /// area, or above its column top when a height offset per unit is
    /// given (HeightSurface). Neighbouring areas each draw their own line,
    /// so a shared border is drawn twice in the same place (no gap).
    ///
    /// Ribbons are extended by half the width at both ends so corners
    /// close. One mesh per chunk, vertex-coloured, no collider (picking
    /// and copies ignore it).
    /// </summary>
    internal static class AreaOutlineBuilder
    {
        /// <summary>Lift above the surface, Unity units (0.5 m), against z-fighting.</summary>
        private const float Lift =
            0.0005f;

        /// <summary>Positions are matched on a 1 cm grid (Unity units are km).</summary>
        private const float Quantum =
            1e-5f;


        /// <summary>
        /// Adds an "Outline" child under <paramref name="parent"/>, which
        /// must share the chunk mesh's local space. Returns null when the
        /// chunk has no boundary edges.
        /// </summary>
        public static GameObject Create(
            Transform parent,
            SpatialMeshChunk chunk,
            AreaOutlineSettings settings,
            Material material,
            Func<string, float> topOffset
        )
        {
            if (chunk == null ||
                chunk.Mesh == null ||
                settings == null ||
                !settings.Enabled)
            {
                return null;
            }


            Vector3[] vertices =
                chunk.Mesh.vertices;

            int[] triangles =
                chunk.Mesh.triangles;

            // Width in the chunk's local units (the city is normally unscaled).
            float halfWidth =
                0.5f * settings.Width /
                Mathf.Max(1e-6f, Mathf.Abs(parent.lossyScale.x));


            var positions =
                new List<Vector3>();

            var indices =
                new List<int>();

            var edgeCounts =
                new Dictionary<(Vector3Int, Vector3Int), int>();

            var edgeEnds =
                new Dictionary<(Vector3Int, Vector3Int), (Vector3, Vector3)>();


            foreach (SpatialMeshUnitRange range in chunk.UnitRanges)
            {
                edgeCounts.Clear();

                edgeEnds.Clear();


                for (int t = range.TriangleStart; t < range.TriangleEndExclusive; t++)
                {
                    int i = t * 3;

                    if (i + 2 >= triangles.Length)
                    {
                        break;
                    }

                    CountEdge(vertices[triangles[i]], vertices[triangles[i + 1]], edgeCounts, edgeEnds);
                    CountEdge(vertices[triangles[i + 1]], vertices[triangles[i + 2]], edgeCounts, edgeEnds);
                    CountEdge(vertices[triangles[i + 2]], vertices[triangles[i]], edgeCounts, edgeEnds);
                }


                float offset =
                    (topOffset?.Invoke(range.UnitId) ?? 0.0f) + Lift;


                foreach (KeyValuePair<(Vector3Int, Vector3Int), int> edge in edgeCounts)
                {
                    if (edge.Value != 1)
                    {
                        continue;
                    }

                    (Vector3 a, Vector3 b) =
                        edgeEnds[edge.Key];

                    AddRibbon(
                        a + Vector3.up * offset,
                        b + Vector3.up * offset,
                        halfWidth,
                        positions,
                        indices
                    );
                }
            }


            if (positions.Count == 0)
            {
                return null;
            }


            var mesh =
                new Mesh
                {
                    name = chunk.name + "_Outline",
                    indexFormat = positions.Count > 65000
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16
                };

            mesh.SetVertices(positions);

            var colors =
                new Color32[positions.Count];

            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] =
                    settings.Color;
            }

            mesh.colors32 =
                colors;

            mesh.SetTriangles(indices, 0);

            mesh.RecalculateBounds();


            var outline =
                new GameObject("Outline");

            outline.transform.SetParent(
                parent,
                false
            );

            outline.AddComponent<MeshFilter>().sharedMesh =
                mesh;

            MeshRenderer renderer =
                outline.AddComponent<MeshRenderer>();

            renderer.sharedMaterial =
                material;

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            // The mesh is owned by this object.
            outline.AddComponent<OwnedMesh>().Mesh =
                mesh;

            return outline;
        }


        private static void CountEdge(
            Vector3 a,
            Vector3 b,
            Dictionary<(Vector3Int, Vector3Int), int> counts,
            Dictionary<(Vector3Int, Vector3Int), (Vector3, Vector3)> ends
        )
        {
            Vector3Int qa =
                Quantize(a);

            Vector3Int qb =
                Quantize(b);

            if (qa == qb)
            {
                return;
            }

            // Undirected: the smaller point first.
            (Vector3Int, Vector3Int) key =
                Less(qa, qb)
                    ? (qa, qb)
                    : (qb, qa);

            counts.TryGetValue(key, out int count);

            counts[key] =
                count + 1;

            ends[key] =
                (a, b);
        }


        private static Vector3Int Quantize(
            Vector3 p
        )
        {
            return new Vector3Int(
                Mathf.RoundToInt(p.x / Quantum),
                Mathf.RoundToInt(p.y / Quantum),
                Mathf.RoundToInt(p.z / Quantum)
            );
        }


        private static bool Less(
            Vector3Int a,
            Vector3Int b
        )
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z < b.z;
        }


        /// <summary>A flat quad along a→b, facing up, extended at both ends.</summary>
        private static void AddRibbon(
            Vector3 a,
            Vector3 b,
            float halfWidth,
            List<Vector3> positions,
            List<int> indices
        )
        {
            Vector3 along =
                b - a;

            along.y =
                0.0f;

            if (along.sqrMagnitude < 1e-12f)
            {
                return;
            }

            along.Normalize();

            Vector3 side =
                new Vector3(-along.z, 0.0f, along.x) * halfWidth;

            Vector3 extend =
                along * halfWidth;

            int start =
                positions.Count;

            positions.Add(a - extend - side);
            positions.Add(a - extend + side);
            positions.Add(b + extend + side);
            positions.Add(b + extend - side);

            // Both windings: visible from above whatever the edge direction.
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 3);

            indices.Add(start + 2);
            indices.Add(start + 1);
            indices.Add(start);
            indices.Add(start + 3);
            indices.Add(start + 2);
            indices.Add(start);
        }
    }
}
