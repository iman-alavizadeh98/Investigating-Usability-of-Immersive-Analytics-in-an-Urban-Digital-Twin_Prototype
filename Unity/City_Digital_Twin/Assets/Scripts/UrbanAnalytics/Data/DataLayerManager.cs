using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Core;
using UrbanAnalytics.Spatial;

namespace UrbanAnalytics.Data
{
    /// <summary>
    /// Central runtime manager for analytical data layers.
    ///
    /// Responsibilities:
    /// - discover DataLayers from project_manifest.json
    /// - load DataLayers through DataLayerLoader
    /// - cache loaded layers
    /// - validate their target SpatialLayer
    /// - validate DataLayer unit IDs against SpatialUnit IDs
    /// - manage the active analytical data layer
    ///
    /// It does NOT:
    /// - render data
    /// - modify geometry
    /// - define visualization mappings
    /// - own spatial geometry
    /// </summary>
    public sealed class DataLayerManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private ProjectManager projectManager;

        [SerializeField]
        private SpatialLayerManager spatialLayerManager;


        [Header("Startup")]
        [SerializeField]
        private bool loadAllOnStart = true;


        private readonly Dictionary<string, DataLayer>
            loadedLayers =
                new Dictionary<string, DataLayer>(
                    StringComparer.Ordinal
                );


        private readonly Dictionary<string, Task<DataLayer>>
            loadingTasks =
                new Dictionary<string, Task<DataLayer>>(
                    StringComparer.Ordinal
                );


        private DataLayerLoader loader;

        private string activeLayerId;


        // =========================================================
        // Public state
        // =========================================================

        public Task InitializationTask
        {
            get;
            private set;
        }


        public bool IsInitialized
        {
            get;
            private set;
        }


        public bool IsInitializing
        {
            get;
            private set;
        }


        public string LastError
        {
            get;
            private set;
        }


        public string ActiveLayerId =>
            activeLayerId;


        public int LoadedLayerCount =>
            loadedLayers.Count;


        public IEnumerable<DataLayer> LoadedLayers =>
            loadedLayers.Values;


        // =========================================================
        // Unity lifecycle
        // =========================================================

        private void Start()
        {
            InitializationTask =
                InitializeAsync();
        }


        // =========================================================
        // Initialization
        // =========================================================

        private async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            if (IsInitializing)
                return;


            IsInitializing = true;
            LastError = null;


            try
            {
                /*
                 * Allow all scene Start() methods to run so their
                 * initialization tasks have been assigned.
                 */
                await Task.Yield();


                // -------------------------------------------------
                // Dependencies
                // -------------------------------------------------

                if (projectManager == null)
                {
                    throw new InvalidOperationException(
                        "DataLayerManager requires a ProjectManager."
                    );
                }


                if (spatialLayerManager == null)
                {
                    throw new InvalidOperationException(
                        "DataLayerManager requires a SpatialLayerManager."
                    );
                }


                // -------------------------------------------------
                // Wait for project manifest
                // -------------------------------------------------

                if (projectManager.LoadTask != null)
                {
                    await projectManager.LoadTask;
                }


                if (!projectManager.IsLoaded ||
                    projectManager.Manifest == null)
                {
                    throw new InvalidOperationException(
                        "ProjectManager did not successfully load " +
                        "the project manifest."
                    );
                }


                // -------------------------------------------------
                // Wait for spatial system
                // -------------------------------------------------

                if (spatialLayerManager.InitializationTask != null)
                {
                    await spatialLayerManager.InitializationTask;
                }


                // -------------------------------------------------
                // Create loader
                // -------------------------------------------------

                if (projectManager.AssetReader == null)
                {
                    throw new InvalidOperationException(
                        "ProjectManager AssetReader is null."
                    );
                }


                loader =
                    new DataLayerLoader(
                        projectManager.AssetReader
                    );


                // -------------------------------------------------
                // Manifest data-layer references
                // -------------------------------------------------

                ResourceReference[] references =
                    projectManager.Manifest.dataLayers;


                if (references == null ||
                    references.Length == 0)
                {
                    IsInitialized = true;


                    Debug.Log(
                        "DataLayerManager initialized. " +
                        "Available data layers: 0",
                        this
                    );


                    return;
                }


                // -------------------------------------------------
                // Validate manifest references
                // -------------------------------------------------

                ValidateManifestReferences(
                    references
                );


                // -------------------------------------------------
                // Load layers
                // -------------------------------------------------

                if (loadAllOnStart)
                {
                    for (int i = 0;
                         i < references.Length;
                         i++)
                    {
                        await LoadLayerAsync(
                            references[i].id
                        );
                    }
                }


                // -------------------------------------------------
                // Pick initial active layer
                // -------------------------------------------------

                if (loadedLayers.Count > 0 &&
                    string.IsNullOrWhiteSpace(activeLayerId))
                {
                    foreach (
                        KeyValuePair<string, DataLayer> pair
                        in loadedLayers)
                    {
                        activeLayerId =
                            pair.Key;

                        break;
                    }
                }


                IsInitialized = true;


                Debug.Log(
                    $"DataLayerManager initialized. " +
                    $"Available data layers: {references.Length}, " +
                    $"Loaded: {loadedLayers.Count}",
                    this
                );


                if (!string.IsNullOrWhiteSpace(
                        activeLayerId))
                {
                    Debug.Log(
                        $"Active data layer: {activeLayerId}",
                        this
                    );
                }
            }
            catch (Exception exception)
            {
                LastError =
                    exception.Message;


                Debug.LogException(
                    exception,
                    this
                );


                throw;
            }
            finally
            {
                IsInitializing = false;
            }
        }


        // =========================================================
        // Loading
        // =========================================================

        public Task<DataLayer> LoadLayerAsync(
            string dataLayerId)
        {
            if (string.IsNullOrWhiteSpace(
                    dataLayerId))
            {
                throw new ArgumentException(
                    "Data layer ID cannot be empty.",
                    nameof(dataLayerId)
                );
            }


            if (loadedLayers.TryGetValue(
                    dataLayerId,
                    out DataLayer existingLayer))
            {
                return Task.FromResult(
                    existingLayer
                );
            }


            if (loadingTasks.TryGetValue(
                    dataLayerId,
                    out Task<DataLayer> existingTask))
            {
                return existingTask;
            }


            Task<DataLayer> task =
                LoadLayerInternalAsync(
                    dataLayerId
                );


            loadingTasks.Add(
                dataLayerId,
                task
            );


            return task;
        }


        private async Task<DataLayer>
            LoadLayerInternalAsync(
                string dataLayerId)
        {
            try
            {
                if (loader == null)
                {
                    throw new InvalidOperationException(
                        "DataLayerLoader has not been initialized."
                    );
                }


                ResourceReference reference =
                    FindReference(
                        dataLayerId
                    );


                if (reference == null)
                {
                    throw new KeyNotFoundException(
                        $"Data layer '{dataLayerId}' is not " +
                        $"registered in project_manifest.json."
                    );
                }


                if (string.IsNullOrWhiteSpace(
                        reference.definition))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{dataLayerId}' does not " +
                        $"have a definition path."
                    );
                }


                // -------------------------------------------------
                // Load actual layer
                // -------------------------------------------------

                DataLayer layer =
                    await loader.LoadAsync(
                        reference.definition
                    );


                // -------------------------------------------------
                // Validate reference identity
                // -------------------------------------------------

                if (!string.Equals(
                        layer.Id,
                        reference.id,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Manifest data-layer ID " +
                        $"'{reference.id}' does not match " +
                        $"loaded DataLayer ID '{layer.Id}'."
                    );
                }


                // -------------------------------------------------
                // Validate against spatial domain
                // -------------------------------------------------

                ValidateSpatialAssociation(
                    layer
                );


                // -------------------------------------------------
                // Store
                // -------------------------------------------------

                loadedLayers.Add(
                    layer.Id,
                    layer
                );


                if (string.IsNullOrWhiteSpace(
                        activeLayerId))
                {
                    activeLayerId =
                        layer.Id;
                }


                LogLoadedLayer(
                    layer
                );


                return layer;
            }
            finally
            {
                loadingTasks.Remove(
                    dataLayerId
                );
            }
        }


        // =========================================================
        // Spatial association validation
        // =========================================================

        private void ValidateSpatialAssociation(
            DataLayer layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(
                    nameof(layer)
                );
            }


            /*
             * This assumes SpatialLayerManager exposes:
             *
             * TryGetLayer(
             *     string layerId,
             *     out SpatialLayer layer)
             *
             * which matches the architecture already established.
             */
            if (!spatialLayerManager.TryGetLayer(
                    layer.TargetSpatialLayerId,
                    out SpatialLayer spatialLayer))
            {
                throw new InvalidOperationException(
                    $"Data layer '{layer.Id}' targets spatial " +
                    $"layer '{layer.TargetSpatialLayerId}', but " +
                    $"that spatial layer is not loaded."
                );
            }


            int matchedCount = 0;
            int missingCount = 0;


            const int maxMissingExamples = 10;


            List<string> missingExamples =
                new List<string>(
                    maxMissingExamples
                );


            IReadOnlyList<string> unitIds =
                layer.UnitIds;


            for (int i = 0;
                 i < unitIds.Count;
                 i++)
            {
                string unitId =
                    unitIds[i];


                if (spatialLayer.ContainsUnit(
                        unitId))
                {
                    matchedCount++;
                }
                else
                {
                    missingCount++;


                    if (missingExamples.Count <
                        maxMissingExamples)
                    {
                        missingExamples.Add(
                            unitId
                        );
                    }
                }
            }


            // -----------------------------------------------------
            // Missing spatial IDs are an error
            // -----------------------------------------------------

            if (missingCount > 0)
            {
                string examples =
                    string.Join(
                        ", ",
                        missingExamples
                    );


                throw new InvalidOperationException(
                    $"Data layer '{layer.Id}' contains " +
                    $"{missingCount} unit IDs that do not " +
                    $"exist in spatial layer " +
                    $"'{layer.TargetSpatialLayerId}'.\n" +
                    $"Examples: {examples}"
                );
            }


            Debug.Log(
                $"Data/spatial association validated:\n" +
                $"{layer.Id} -> {layer.TargetSpatialLayerId}\n" +
                $"Matched units: " +
                $"{matchedCount}/{layer.UnitCount}",
                this
            );
        }


        // =========================================================
        // Lookup
        // =========================================================

        public bool ContainsLayer(
            string dataLayerId)
        {
            if (string.IsNullOrWhiteSpace(
                    dataLayerId))
            {
                return false;
            }


            return loadedLayers.ContainsKey(
                dataLayerId
            );
        }


        public bool TryGetLayer(
            string dataLayerId,
            out DataLayer layer)
        {
            layer = null;


            if (string.IsNullOrWhiteSpace(
                    dataLayerId))
            {
                return false;
            }


            return loadedLayers.TryGetValue(
                dataLayerId,
                out layer
            );
        }


        public DataLayer GetLayer(
            string dataLayerId)
        {
            if (!TryGetLayer(
                    dataLayerId,
                    out DataLayer layer))
            {
                throw new KeyNotFoundException(
                    $"Data layer '{dataLayerId}' is not loaded."
                );
            }


            return layer;
        }


        public bool TryGetActiveLayer(
            out DataLayer layer)
        {
            layer = null;


            if (string.IsNullOrWhiteSpace(
                    activeLayerId))
            {
                return false;
            }


            return loadedLayers.TryGetValue(
                activeLayerId,
                out layer
            );
        }


        // =========================================================
        // Active layer
        // =========================================================

        public void SetActiveLayer(
            string dataLayerId)
        {
            if (string.IsNullOrWhiteSpace(
                    dataLayerId))
            {
                throw new ArgumentException(
                    "Data layer ID cannot be empty.",
                    nameof(dataLayerId)
                );
            }


            if (!loadedLayers.ContainsKey(
                    dataLayerId))
            {
                throw new KeyNotFoundException(
                    $"Cannot activate data layer " +
                    $"'{dataLayerId}' because it is not loaded."
                );
            }


            activeLayerId =
                dataLayerId;


            Debug.Log(
                $"Active data layer: {activeLayerId}",
                this
            );
        }


        // =========================================================
        // Unloading
        // =========================================================

        public bool UnloadLayer(
            string dataLayerId)
        {
            if (string.IsNullOrWhiteSpace(
                    dataLayerId))
            {
                return false;
            }


            bool removed =
                loadedLayers.Remove(
                    dataLayerId
                );


            if (!removed)
                return false;


            if (string.Equals(
                    activeLayerId,
                    dataLayerId,
                    StringComparison.Ordinal))
            {
                activeLayerId =
                    null;


                foreach (
                    KeyValuePair<string, DataLayer> pair
                    in loadedLayers)
                {
                    activeLayerId =
                        pair.Key;

                    break;
                }
            }


            return true;
        }


        // =========================================================
        // Manifest lookup
        // =========================================================

        private ResourceReference FindReference(
            string dataLayerId)
        {
            ResourceReference[] references =
                projectManager.Manifest.dataLayers;


            if (references == null)
                return null;


            for (int i = 0;
                 i < references.Length;
                 i++)
            {
                ResourceReference reference =
                    references[i];


                if (reference == null)
                    continue;


                if (string.Equals(
                        reference.id,
                        dataLayerId,
                        StringComparison.Ordinal))
                {
                    return reference;
                }
            }


            return null;
        }


        private static void ValidateManifestReferences(
            ResourceReference[] references)
        {
            HashSet<string> ids =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            for (int i = 0;
                 i < references.Length;
                 i++)
            {
                ResourceReference reference =
                    references[i];


                if (reference == null)
                {
                    throw new InvalidOperationException(
                        $"Data-layer reference at index {i} is null."
                    );
                }


                if (string.IsNullOrWhiteSpace(
                        reference.id))
                {
                    throw new InvalidOperationException(
                        $"Data-layer reference at index {i} " +
                        $"has no ID."
                    );
                }


                if (string.IsNullOrWhiteSpace(
                        reference.definition))
                {
                    throw new InvalidOperationException(
                        $"Data-layer reference " +
                        $"'{reference.id}' has no definition path."
                    );
                }


                if (!ids.Add(
                        reference.id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate data-layer reference ID: " +
                        $"'{reference.id}'."
                    );
                }
            }
        }


        // =========================================================
        // Diagnostics
        // =========================================================

        private void LogLoadedLayer(
            DataLayer layer)
        {
            Debug.Log(
                $"Data layer loaded:\n" +
                $"ID: {layer.Id}\n" +
                $"Name: {layer.DisplayName}\n" +
                $"Target spatial layer: " +
                $"{layer.TargetSpatialLayerId}\n" +
                $"Units: {layer.UnitCount}\n" +
                $"Variables: {layer.VariableCount}",
                this
            );


            IReadOnlyList<DataVariableDefinition> variables =
                layer.Definition.Variables;


            for (int i = 0;
                 i < variables.Count;
                 i++)
            {
                DataVariableDefinition variable =
                    variables[i];


                if (variable == null)
                    continue;


                if (layer.TryGetNumericRange(
                        variable.Id,
                        out double minimum,
                        out double maximum))
                {
                    string unit =
                        string.IsNullOrWhiteSpace(
                            variable.Unit)
                            ? ""
                            : $" {variable.Unit}";


                    Debug.Log(
                        $"{layer.Id}.{variable.Id}: " +
                        $"range [{minimum}, {maximum}]" +
                        $"{unit}",
                        this
                    );
                }
            }
        }
    }
}