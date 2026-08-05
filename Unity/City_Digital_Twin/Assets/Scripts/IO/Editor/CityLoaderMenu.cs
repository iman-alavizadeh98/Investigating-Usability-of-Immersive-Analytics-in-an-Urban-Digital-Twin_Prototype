using System.IO;
using CityDigitalTwin.IO;
using UnityEditor;
using UnityEngine;

namespace CityDigitalTwin.IO.EditorTools
{
    /// <summary>
    /// Editor entry points for the georeferenced loading flow.
    ///
    /// Everything here goes through <c>meshes_manifest.json</c>, because that file
    /// is the only source of real-world coordinates — PLY vertices are rebased to
    /// each group's own origin and cannot be placed without it.
    ///
    /// Inspection commands parse and report without touching the scene, so a
    /// pipeline run can be checked before anything is rendered.
    /// </summary>
    public static class CityLoaderMenu
    {
        /// <summary>
        /// Add a configured CityMeshLoader to the scene. It does not load on its
        /// own — press Play, or use the button on the component — so the scope
        /// settings can be reviewed first.
        /// </summary>
        [MenuItem("City Digital Twin/Add City Mesh Loader to Scene", priority = 0)]
        public static void AddCityLoader()
        {
            string manifest = PickManifest();
            if (string.IsNullOrEmpty(manifest)) return;

            var go = new GameObject("City");
            var loader = go.AddComponent<CityMeshLoader>();
            loader.manifestPath = ToProjectRelative(manifest);

            Undo.RegisterCreatedObjectUndo(go, "Add City Mesh Loader");
            Selection.activeGameObject = go;

            Debug.Log($"[CityLoaderMenu] Added CityMeshLoader for '{loader.manifestPath}'. " +
                      "Press Play to load, or use 'Load Now' on the component. " +
                      "Set Max Groups to try a subset first.", go);
        }

        /// <summary>
        /// Parse a whole run and report totals, without touching the scene. Use this
        /// to verify a pipeline run before rendering it.
        /// </summary>
        [MenuItem("City Digital Twin/Inspect Mesh Run (no scene changes)...", priority = 1)]
        public static void InspectRun()
        {
            string manifestPath = PickManifest();
            if (string.IsNullOrEmpty(manifestPath)) return;

            MeshManifest manifest;
            try
            {
                manifest = MeshManifest.Load(manifestPath);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Manifest load failed", e.Message, "OK");
                return;
            }

            string folder = Path.Combine(Path.GetDirectoryName(manifestPath), "mesh_files");
            if (!Directory.Exists(folder))
                folder = Path.Combine(Path.GetDirectoryName(manifestPath), "glb_files");

            PlyFolderImporter.Result result;
            try
            {
                EditorUtility.DisplayProgressBar("Inspecting run", "Parsing PLY files...", 0.5f);
                result = PlyFolderImporter.ImportFolder(folder, PlyImportSettings.Default);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Folder import failed", e.Message, "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Vector2 swCorner = manifest.ComputeSouthWestOrigin();
            double extentX = 0, extentZ = 0;
            foreach (var g in manifest.meshes)
            {
                if (g?.origin == null) continue;
                extentX = System.Math.Max(extentX, g.origin.x - swCorner.x);
                extentZ = System.Math.Max(extentZ, g.origin.y - swCorner.y);
            }

            string report =
                $"{manifest.strategy}\n{manifest.strategy_description}\n\n" +
                $"Groups:     {manifest.meshes.Count:N0}\n" +
                $"Parsed OK:  {result.OkCount:N0}" +
                (result.FailCount > 0 ? $"   ({result.FailCount} FAILED)" : "") + "\n" +
                $"Vertices:   {result.TotalVertices:N0}\n" +
                $"Triangles:  {result.TotalTriangles:N0}\n" +
                $"Parse time: {result.ElapsedMs} ms\n\n" +
                $"CRS:          {manifest.crs}\n" +
                $"Scene origin: ({swCorner.x:F1}, {swCorner.y:F1})\n" +
                $"Extent:       {extentX / 1000.0:F1} x {extentZ / 1000.0:F1} km";

            EditorUtility.DisplayDialog("Mesh run contents", report, "OK");
            PlyFolderImporter.LogReport(result);
        }

        /// <summary>Parse one PLY and report what is in it. No scene changes.</summary>
        [MenuItem("City Digital Twin/Inspect Single PLY...", priority = 2)]
        public static void InspectPly()
        {
            string path = EditorUtility.OpenFilePanel(
                "Select a PLY mesh", ProjectPaths.ProcessedDataDirectory ?? Application.dataPath, "ply");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var data = PlyParser.Load(path, PlyImportSettings.Default);
                Bounds b = data.ComputeBounds();
                string report =
                    $"{Path.GetFileName(path)}\n\n" +
                    $"Vertices:  {data.VertexCount:N0}\n" +
                    $"Faces:     {data.FaceCount:N0}\n" +
                    $"Triangles: {data.TriangleCount:N0}\n" +
                    $"Normals:   {(data.HasNormals ? "in file" : "absent (recalculated)")}\n" +
                    $"Index fmt: {(data.VertexCount > 65535 ? "UInt32 (large mesh)" : "UInt16")}\n\n" +
                    $"Local size (metres):\n" +
                    $"  ground {b.size.x:F1} x {b.size.z:F1}\n" +
                    $"  height {b.size.y:F1}\n\n" +
                    "Coordinates are LOCAL — rebased on this group's own origin.\n" +
                    "Real-world placement comes from meshes_manifest.json.\n\n" +
                    $"Header:\n  {string.Join("\n  ", data.Comments)}";

                EditorUtility.DisplayDialog("PLY contents", report, "OK");
                Debug.Log("[CityLoaderMenu] " + report.Replace("\n", " | "));
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("PLY parse failed", e.Message, "OK");
            }
        }

        private static string PickManifest()
        {
            string start = ProjectPaths.ProcessedDataDirectory ?? Application.dataPath;
            return EditorUtility.OpenFilePanel("Select meshes_manifest.json", start, "json");
        }

        /// <summary>
        /// Store paths relative to the Unity project root so scenes stay portable
        /// across machines and checkouts.
        /// </summary>
        private static string ToProjectRelative(string absolutePath)
        {
            string root = ProjectPaths.ProjectRoot.Replace('\\', '/').TrimEnd('/');
            string full = Path.GetFullPath(absolutePath).Replace('\\', '/');

            // Walk up from the project root until the path shares a prefix.
            string prefix = root;
            string ups = "";
            while (!string.IsNullOrEmpty(prefix))
            {
                if (full.StartsWith(prefix + "/", System.StringComparison.OrdinalIgnoreCase))
                    return ups + full.Substring(prefix.Length + 1);

                int slash = prefix.LastIndexOf('/');
                if (slash < 0) break;
                prefix = prefix.Substring(0, slash);
                ups += "../";
            }
            return full;   // different drive: keep it absolute
        }
    }
}
