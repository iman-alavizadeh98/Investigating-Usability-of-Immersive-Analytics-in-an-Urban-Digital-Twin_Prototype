using CityDigitalTwin.IO;
using UnityEditor;
using UnityEngine;

namespace CityDigitalTwin.IO.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="CityMeshLoader"/> with Load / Clear buttons, so a run
    /// can be loaded in edit mode without entering Play. Editing the city is far
    /// easier when the geometry is visible in the Scene view.
    /// </summary>
    [CustomEditor(typeof(CityMeshLoader))]
    public class CityMeshLoaderInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var loader = (CityMeshLoader)target;

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load Now", GUILayout.Height(28)))
                    RunLoad(loader);

                if (GUILayout.Button("Clear", GUILayout.Height(28)))
                {
                    loader.Clear();
                    EditorUtility.SetDirty(loader);
                }
            }

            if (loader.Manifest != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    $"{loader.Manifest.strategy}\n" +
                    $"{loader.Manifest.meshes.Count:N0} groups · CRS {loader.Crs}\n" +
                    $"Scene origin: ({loader.SceneOriginEasting:F1}, {loader.SceneOriginNorthing:F1})",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Placement comes from meshes_manifest.json — PLY vertices are rebased " +
                    "to each group's own origin and cannot be positioned without it.\n\n" +
                    "Tip: set Max Groups to ~50 for a first look.",
                    MessageType.None);
            }
        }

        /// <summary>
        /// Drive the loader's coroutine manually. In edit mode there is no coroutine
        /// runner, so it is pumped to completion here.
        /// </summary>
        private static void RunLoad(CityMeshLoader loader)
        {
            if (Application.isPlaying)
            {
                loader.StartCoroutine(loader.LoadCityAsync());
                return;
            }

            loader.Clear();

            var routine = loader.LoadCityAsync();
            try
            {
                EditorUtility.DisplayProgressBar("Loading city", "Parsing and placing meshes...", 0.5f);
                while (routine.MoveNext()) { }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.SetDirty(loader);
            if (SceneView.lastActiveSceneView != null)
            {
                Selection.activeGameObject = loader.gameObject;
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }
    }
}
