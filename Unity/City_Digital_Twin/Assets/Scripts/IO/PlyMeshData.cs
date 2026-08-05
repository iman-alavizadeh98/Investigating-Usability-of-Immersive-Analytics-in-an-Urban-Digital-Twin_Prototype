using System.Collections.Generic;
using UnityEngine;

namespace CityDigitalTwin.IO
{
    /// <summary>
    /// Engine-agnostic result of parsing a PLY file: raw vertex/normal/index arrays
    /// already converted into Unity's coordinate frame.
    ///
    /// The pipeline (Src/mesh_generation/builder.py, export_ply) writes a local,
    /// origin-rebased frame: X = easting - origin.x, Y = northing - origin.y,
    /// Z = height in metres above the origin's z datum. That frame is Z-up and
    /// right-handed (EPSG:3006 metres). Unity is Y-up and left-handed, so the
    /// parser maps (x, y, z) -> (x, z, y). That single swap is the whole
    /// conversion: it flips handedness too, so triangle winding must be left
    /// alone. See PlyImportSettings.ConvertAxes and FlipWinding.
    /// </summary>
    public sealed class PlyMeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;   // null when the file carried no nx/ny/nz
        public int[] Triangles;

        /// <summary>Comment lines from the PLY header, verbatim (provenance).</summary>
        public List<string> Comments = new List<string>();

        /// <summary>Vertex count declared in the header.</summary>
        public int VertexCount;

        /// <summary>Face count declared in the header (before triangulation of n-gons).</summary>
        public int FaceCount;

        /// <summary>Triangle count actually produced (n-gons are fanned).</summary>
        public int TriangleCount => Triangles != null ? Triangles.Length / 3 : 0;

        public bool HasNormals => Normals != null && Normals.Length == Vertices.Length;

        /// <summary>
        /// Axis-aligned bounds in Unity space. Useful for sanity-checking scale
        /// before the mesh ever reaches the scene.
        /// </summary>
        public Bounds ComputeBounds()
        {
            if (Vertices == null || Vertices.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Vector3 min = Vertices[0];
            Vector3 max = Vertices[0];
            for (int i = 1; i < Vertices.Length; i++)
            {
                min = Vector3.Min(min, Vertices[i]);
                max = Vector3.Max(max, Vertices[i]);
            }
            return new Bounds((min + max) * 0.5f, max - min);
        }
    }

    /// <summary>
    /// Options controlling how a PLY file is turned into a Unity mesh.
    /// Defaults match what the Gothenburg building pipeline emits.
    /// </summary>
    [System.Serializable]
    public class PlyImportSettings
    {
        [Tooltip("Convert the pipeline's Z-up right-handed frame to Unity's Y-up " +
                 "left-handed frame: (x, y, z) -> (x, z, y). The swap flips " +
                 "handedness on its own, so winding is left untouched. Disable " +
                 "only if the source file is already Y-up.")]
        public bool ConvertAxes = true;

        [Tooltip("Reverse triangle winding. Leave OFF for this project's meshes — " +
                 "the axis swap already handles handedness. Turn on only if a " +
                 "third-party PLY imports inside-out (surfaces invisible from " +
                 "outside, interior faces visible).")]
        public bool FlipWinding = false;

        [Tooltip("Uniform scale applied after axis conversion. Source units are metres; " +
                 "1.0 keeps 1 Unity unit = 1 metre.")]
        public float Scale = 1.0f;

        [Tooltip("Recalculate normals instead of using the nx/ny/nz in the file. " +
                 "The pipeline writes per-vertex normals, so this is normally off.")]
        public bool RecalculateNormals = false;

        [Tooltip("Recalculate a tangent basis. Only needed for normal-mapped shaders; " +
                 "costs time and memory on 200k+ vertex district meshes.")]
        public bool RecalculateTangents = false;

        [Tooltip("Optimize the mesh for GPU vertex-cache locality after loading. " +
                 "Slow on very large meshes; useful for assets imported once in-editor.")]
        public bool OptimizeMesh = false;

        [Tooltip("Mark the mesh non-readable after upload to free the CPU-side copy. " +
                 "Saves ~50% memory but breaks raycasting against MeshCollider and " +
                 "any runtime mesh queries — leave off if you need entity picking.")]
        public bool MarkNoLongerReadable = false;

        public static PlyImportSettings Default => new PlyImportSettings();
    }
}
