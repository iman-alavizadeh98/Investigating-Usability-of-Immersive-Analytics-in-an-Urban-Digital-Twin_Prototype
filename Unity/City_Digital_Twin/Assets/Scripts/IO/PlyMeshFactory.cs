using UnityEngine;
using UnityEngine.Rendering;

namespace CityDigitalTwin.IO
{
    /// <summary>
    /// Turns parsed <see cref="PlyMeshData"/> into a Unity <see cref="Mesh"/>.
    ///
    /// Kept separate from the parser so the parsing step stays free of Unity API
    /// calls and can run on a background thread; only this class must run on the
    /// main thread.
    /// </summary>
    public static class PlyMeshFactory
    {
        /// <summary>
        /// Unity's default 16-bit index buffer addresses at most 65535 vertices.
        /// District meshes from this pipeline run to ~240k vertices, so the index
        /// format is chosen from the actual vertex count rather than assumed.
        /// </summary>
        private const int UInt16IndexLimit = 65535;

        public static Mesh CreateMesh(PlyMeshData data, string meshName = "PlyMesh",
                                      PlyImportSettings settings = null)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            settings = settings ?? PlyImportSettings.Default;

            var mesh = new Mesh { name = meshName };

            if (data.Vertices.Length > UInt16IndexLimit)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(data.Vertices);

            if (data.Triangles != null && data.Triangles.Length > 0)
            {
                // calculateBounds:true — we want correct culling bounds, and the
                // cost is negligible next to parsing.
                mesh.SetTriangles(data.Triangles, 0, calculateBounds: true);
            }
            else
            {
                // No faces: keep the geometry visible as a point cloud rather than
                // silently producing an empty mesh.
                var pointIndices = new int[data.Vertices.Length];
                for (int i = 0; i < pointIndices.Length; i++) pointIndices[i] = i;
                mesh.SetIndices(pointIndices, MeshTopology.Points, 0, calculateBounds: true);
            }

            if (settings.RecalculateNormals || !data.HasNormals)
                mesh.RecalculateNormals();
            else
                mesh.SetNormals(data.Normals);

            if (settings.RecalculateTangents)
                mesh.RecalculateTangents();

            if (settings.OptimizeMesh)
                mesh.Optimize();

            // MarkNoLongerReadable is applied by the caller after any MeshCollider
            // has been assigned — doing it here would break collider generation.
            return mesh;
        }
    }
}
