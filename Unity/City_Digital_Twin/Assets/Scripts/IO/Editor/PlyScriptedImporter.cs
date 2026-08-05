using System.IO;
using CityDigitalTwin.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace CityDigitalTwin.IO.EditorTools
{
    /// <summary>
    /// Makes Unity treat *.ply files inside Assets/ as first-class mesh assets:
    /// drop a PLY in the project and it appears as a prefab with a Mesh sub-asset,
    /// re-imported automatically whenever the file changes.
    ///
    /// Use this only for one-off meshes stable enough to live in the project and
    /// ship inside a build. It imports each PLY in its own LOCAL frame, so it does
    /// NOT georeference anything — for pipeline output, use
    /// <see cref="CityMeshLoader"/>, which reads meshes_manifest.json and places
    /// every group at its real coordinates.
    ///
    /// The import settings are per-asset and editable in the Inspector.
    /// </summary>
    [ScriptedImporter(version: 1, ext: "ply")]
    public class PlyScriptedImporter : ScriptedImporter
    {
        [Tooltip("Convert the pipeline's Z-up right-handed frame to Unity's Y-up " +
                 "left-handed frame: (x, y, z) -> (x, z, y).")]
        public bool convertAxes = true;

        [Tooltip("Reverse triangle winding. Leave OFF for this project's meshes; " +
                 "turn on only if a third-party PLY imports inside-out.")]
        public bool flipWinding = false;

        [Tooltip("Uniform scale applied after axis conversion. Source units are metres.")]
        public float scale = 1.0f;

        [Tooltip("Ignore normals in the file and recalculate them.")]
        public bool recalculateNormals = false;

        [Tooltip("Generate a tangent basis (only needed for normal-mapped shaders).")]
        public bool recalculateTangents = false;

        [Tooltip("Optimize the mesh for vertex-cache locality. Slow on large meshes, " +
                 "but paid once at import rather than every run.")]
        public bool optimizeMesh = true;

        [Tooltip("Generate a MeshCollider on the imported prefab, for entity picking.")]
        public bool generateCollider = false;

        [Tooltip("Material assigned to the generated prefab. Leave empty for the URP default.")]
        public Material material;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var settings = new PlyImportSettings
            {
                ConvertAxes = convertAxes,
                FlipWinding = flipWinding,
                Scale = scale,
                RecalculateNormals = recalculateNormals,
                RecalculateTangents = recalculateTangents,
                OptimizeMesh = optimizeMesh,
                // Editor assets stay readable: MeshCollider baking and inspection
                // both need the CPU-side copy.
                MarkNoLongerReadable = false
            };

            PlyMeshData data;
            try
            {
                data = PlyParser.Load(ctx.assetPath, settings);
            }
            catch (System.Exception e)
            {
                // Surfaces in the Console and marks the asset as failed, rather
                // than importing a silently empty mesh.
                ctx.LogImportError($"Failed to import PLY '{ctx.assetPath}': {e.Message}");
                return;
            }

            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            Mesh mesh = PlyMeshFactory.CreateMesh(data, assetName, settings);

            var root = new GameObject(assetName);
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material != null ? material : GetDefaultMaterial();

            if (generateCollider)
                root.AddComponent<MeshCollider>().sharedMesh = mesh;

            ctx.AddObjectToAsset("mesh", mesh);
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);

            // Provenance: the PLY header comments carry the building/group id and
            // the note that coordinates are in a local origin-rebased frame. Kept
            // as a plain log (not LogImportWarning) so a healthy asset does not
            // show a warning icon in the project view.
            if (data.Comments.Count > 0)
            {
                Debug.Log($"[PlyImporter] {assetName}: {data.VertexCount:N0} verts, " +
                          $"{data.TriangleCount:N0} tris | " +
                          string.Join(" | ", data.Comments));
            }
        }

        private static Material GetDefaultMaterial()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultMaterial != null)
                return pipeline.defaultMaterial;

            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
        }
    }
}
