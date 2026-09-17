using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Core;
using UrbanAnalytics.Spatial;

namespace UrbanAnalytics.Data
{
    /// <summary>
    /// Central runtime manager for analytical DataLayers.
    ///
    /// Responsibilities:
    /// - discover data layers declared by the project manifest;
    /// - load layers on demand;
    /// - cache loaded layers;
    /// - validate their target SpatialLayer;
    /// - validate SpatialUnit ID associations;
    /// - unload layers;
    /// - track the currently active DataLayer.
    ///
    /// This manager does not:
    /// - render data;
    /// - modify geometry;
    /// - define visualization encodings;
    /// - own spatial geometry;
    /// - perform VR interaction.
    /// </summary>
    public sealed class DataLayerManager : MonoBehaviour
    {
        // =========================================================
        // DEPENDENCIES
        // =========================================================

        [Header("Dependencies")]

        [SerializeField]
        private ProjectManager projectManager;

        [SerializeField]
        private SpatialLayerManager spatialLayerManager;


        // =========================================================
        // STARTUP
        // =========================================================

        [Header("Startup")]

        [SerializeField]
        private bool loadAllLayersOnStart = true;


        // =========================================================
        // RUNTIME STATE
        // =========================================================

        private readonly Dictionary<string, DataLayer>
            loadedLayers =
                new Dictionary<string, DataLayer>(
                    StringComparer.Ordinal
                );


        private readonly Dictionary<string, ResourceReference>
            layerReferences =
                new Dictionary<string, ResourceReference>(
                    StringComparer.Ordinal
                );


        private readonly Dictionary<string, Task<DataLayer>>
            loadingTasks =
                new Dictionary<string, Task<DataLayer>>(
                    StringComparer.Ordinal
                );


        private CancellationTokenSource
            lifetimeCancellation;


        // =========================================================
        // PUBLIC STATE
        // =========================================================

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


        public string ActiveLayerId
        {
            get;
            private set;
        }


        public Task InitializationTask
        {
            get;
            private set;
        }


        public DataLayer ActiveLayer
        {
            get
            {
                if (string.IsNullOrWhiteSpace(
                        ActiveLayerId
                    ))
                {
                    return null;
                }


                return loadedLayers.TryGetValue(
                    ActiveLayerId,
                    out DataLayer layer
                )
                    ? layer
                    : null;
            }
        }


        public IReadOnlyDictionary<string, DataLayer>
            LoadedLayers =>
                loadedLayers;


        // =========================================================
        // UNITY LIFECYCLE
        // =========================================================

        private void Awake()
        {
            ResolveDependencies();


            if (projectManager == null)
            {
                Debug.LogError(
                    "DataLayerManager could not find ProjectManager.",
                    this
                );

                enabled = false;
                return;
            }


            if (spatialLayerManager == null)
            {
                Debug.LogError(
                    "DataLayerManager could not find SpatialLayerManager.",
                    this
                );

                enabled = false;
                return;
            }


            lifetimeCancellation =
                new CancellationTokenSource();
        }


        private void Start()
        {
            if (!enabled)
            {
                return;
            }


            InitializationTask =
                InitializeAsync(
                    lifetimeCancellation.Token
                );
        }


        private void OnDestroy()
        {
            if (lifetimeCancellation == null)
            {
                return;
            }


            lifetimeCancellation.Cancel();
            lifetimeCancellation.Dispose();

            lifetimeCancellation = null;
        }


        // =========================================================
        // INITIALIZATION
        // =========================================================

        private async Task InitializeAsync(
            CancellationToken cancellationToken
        )
        {
            if (IsInitialized ||
                IsInitializing)
            {
                return;
            }


            IsInitializing = true;
            LastError = null;


            try
            {
                // -------------------------------------------------
                // Wait for project manifest
                // -------------------------------------------------

                if (projectManager.LoadTask != null)
                {
                    await projectManager.LoadTask;
                }


                cancellationToken
                    .ThrowIfCancellationRequested();


                if (!projectManager.IsLoaded ||
                    projectManager.Manifest == null)
                {
                    throw new InvalidOperationException(
                        "ProjectManager failed to load the " +
                        "project manifest."
                    );
                }


                // -------------------------------------------------
                // Wait for spatial system
                //
                // Start execution order between MonoBehaviours
                // is not guaranteed.
                // -------------------------------------------------

                if (spatialLayerManager.InitializationTask == null)
                {
                    await Task.Yield();
                }


                cancellationToken
                    .ThrowIfCancellationRequested();


                if (spatialLayerManager.InitializationTask == null)
                {
                    throw new InvalidOperationException(
                        "SpatialLayerManager did not start its " +
                        "initialization task."
                    );
                }


                await spatialLayerManager.InitializationTask;


                cancellationToken
                    .ThrowIfCancellationRequested();


                if (!spatialLayerManager.IsInitialized)
                {
                    throw new InvalidOperationException(
                        $"SpatialLayerManager did not initialize " +
                        $"successfully. Error: " +
                        $"{spatialLayerManager.LastError}"
                    );
                }


                // -------------------------------------------------
                // Register manifest data layers
                // -------------------------------------------------

                RegisterManifestLayers(
                    projectManager.Manifest
                );


                IsInitialized = true;


                Debug.Log(
                    $"DataLayerManager initialized. " +
                    $"Available data layers: " +
                    $"{layerReferences.Count}",
                    this
                );


                // -------------------------------------------------
                // Optional startup loading
                // -------------------------------------------------

                if (loadAllLayersOnStart)
                {
                    await LoadAllLayersAsync(
                        cancellationToken
                    );
                }
            }
            catch (OperationCanceledException)
            {
                // Normal during scene shutdown.
            }
            catch (Exception exception)
            {
                LastError =
                    exception.Message;


                Debug.LogException(
                    exception,
                    this
                );
            }
            finally
            {
                IsInitializing = false;
            }
        }


        private void RegisterManifestLayers(
            ProjectManifest manifest
        )
        {
            layerReferences.Clear();


            if (manifest.dataLayers == null)
            {
                return;
            }


            foreach (
                ResourceReference reference
                in manifest.dataLayers
            )
            {
                ValidateReference(
                    reference
                );


                string normalizedId =
                    reference.id.Trim();


                if (!layerReferences.TryAdd(
                        normalizedId,
                        reference
                    ))
                {
                    throw new InvalidOperationException(
                        $"Duplicate data layer reference " +
                        $"'{reference.id}' in project manifest."
                    );
                }
            }
        }


        // =========================================================
        // LOADING
        // =========================================================

        public async Task<DataLayer> LoadLayerAsync(
            string layerId,
            CancellationToken cancellationToken = default
        )
        {
            EnsureInitialized();


            string normalizedId =
                NormalizeLayerId(
                    layerId
                );


            if (loadedLayers.TryGetValue(
                    normalizedId,
                    out DataLayer existingLayer
                ))
            {
                return existingLayer;
            }


            if (loadingTasks.TryGetValue(
                    normalizedId,
                    out Task<DataLayer> existingTask
                ))
            {
                return await existingTask;
            }


            if (!layerReferences.TryGetValue(
                    normalizedId,
                    out ResourceReference reference
                ))
            {
                throw new KeyNotFoundException(
                    $"Data layer '{normalizedId}' is not " +
                    $"declared in the project manifest."
                );
            }


            Task<DataLayer> loadingTask =
                LoadLayerInternalAsync(
                    normalizedId,
                    reference,
                    cancellationToken
                );


            loadingTasks.Add(
                normalizedId,
                loadingTask
            );


            try
            {
                return await loadingTask;
            }
            finally
            {
                loadingTasks.Remove(
                    normalizedId
                );
            }
        }


        private async Task<DataLayer> LoadLayerInternalAsync(
            string layerId,
            ResourceReference reference,
            CancellationToken cancellationToken
        )
        {
            DataLayer layer =
                await DataLayerLoader.LoadAsync(
                    reference.definition,
                    projectManager.AssetReader,
                    cancellationToken
                );


            cancellationToken
                .ThrowIfCancellationRequested();


            if (!string.Equals(
                    layer.Id,
                    layerId,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    $"Project manifest declares data layer " +
                    $"'{layerId}', but loaded layer declares " +
                    $"'{layer.Id}'."
                );
            }


            SpatialLayer targetSpatialLayer =
                await ResolveTargetSpatialLayerAsync(
                    layer.TargetSpatialLayerId,
                    cancellationToken
                );


            ValidateSpatialAssociation(
                layer,
                targetSpatialLayer
            );


            loadedLayers.Add(
                layer.Id,
                layer
            );


            LogLoadedLayer(
                layer,
                targetSpatialLayer
            );


            if (ActiveLayer == null)
            {
                SetActiveLayer(
                    layer.Id
                );
            }


            return layer;
        }


        public async Task LoadAllLayersAsync(
            CancellationToken cancellationToken = default
        )
        {
            EnsureInitialized();


            foreach (
                string layerId
                in layerReferences.Keys
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                await LoadLayerAsync(
                    layerId,
                    cancellationToken
                );
            }
        }


        // =========================================================
        // SPATIAL ASSOCIATION
        // =========================================================

        private async Task<SpatialLayer>
            ResolveTargetSpatialLayerAsync(
                string spatialLayerId,
                CancellationToken cancellationToken
            )
        {
            if (spatialLayerManager.TryGetLayer(
                    spatialLayerId,
                    out SpatialLayer loadedSpatialLayer
                ))
            {
                return loadedSpatialLayer;
            }


            if (!spatialLayerManager.IsLayerAvailable(
                    spatialLayerId
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer targets spatial layer " +
                    $"'{spatialLayerId}', but that spatial layer " +
                    $"is not declared in the project manifest."
                );
            }


            return await spatialLayerManager.LoadLayerAsync(
                spatialLayerId,
                cancellationToken
            );
        }


        private void ValidateSpatialAssociation(
            DataLayer dataLayer,
            SpatialLayer spatialLayer
        )
        {
            if (dataLayer == null)
            {
                throw new ArgumentNullException(
                    nameof(dataLayer)
                );
            }


            if (spatialLayer == null)
            {
                throw new ArgumentNullException(
                    nameof(spatialLayer)
                );
            }


            if (!string.Equals(
                    dataLayer.TargetSpatialLayerId,
                    spatialLayer.Id,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{dataLayer.Id}' targets " +
                    $"'{dataLayer.TargetSpatialLayerId}', but " +
                    $"association validation received spatial " +
                    $"layer '{spatialLayer.Id}'."
                );
            }


            int matchedCount = 0;
            int missingCount = 0;


            const int maxMissingExamples =
                10;


            var missingExamples =
                new List<string>(
                    maxMissingExamples
                );


            IReadOnlyList<string> unitIds =
                dataLayer.UnitIds;


            for (
                int i = 0;
                i < unitIds.Count;
                i++
            )
            {
                string unitId =
                    unitIds[i];


                if (spatialLayer.ContainsUnit(
                        unitId
                    ))
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


            if (missingCount > 0)
            {
                string examples =
                    string.Join(
                        ", ",
                        missingExamples
                    );


                throw new InvalidOperationException(
                    $"Data layer '{dataLayer.Id}' contains " +
                    $"{missingCount} unit IDs that do not exist " +
                    $"in spatial layer '{spatialLayer.Id}'.\n" +
                    $"Examples: {examples}"
                );
            }


            Debug.Log(
                $"Data/spatial association validated:\n" +
                $"{dataLayer.Id} -> {spatialLayer.Id}\n" +
                $"Data units matched: " +
                $"{matchedCount}/{dataLayer.UnitCount}\n" +
                $"Spatial units available: " +
                $"{spatialLayer.UnitCount}",
                this
            );
        }


        // =========================================================
        // LOOKUP
        // =========================================================

        public bool IsLayerAvailable(
            string layerId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                return false;
            }


            return layerReferences.ContainsKey(
                layerId.Trim()
            );
        }


        public bool IsLayerLoaded(
            string layerId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                return false;
            }


            return loadedLayers.ContainsKey(
                layerId.Trim()
            );
        }


        public bool TryGetLayer(
            string layerId,
            out DataLayer layer
        )
        {
            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                layer = null;
                return false;
            }


            return loadedLayers.TryGetValue(
                layerId.Trim(),
                out layer
            );
        }


        public DataLayer GetLayer(
            string layerId
        )
        {
            if (!TryGetLayer(
                    layerId,
                    out DataLayer layer
                ))
            {
                throw new KeyNotFoundException(
                    $"Data layer '{layerId}' is not loaded."
                );
            }


            return layer;
        }


        // =========================================================
        // ACTIVE LAYER
        // =========================================================

        public void SetActiveLayer(
            string layerId
        )
        {
            string normalizedId =
                NormalizeLayerId(
                    layerId
                );


            if (!loadedLayers.ContainsKey(
                    normalizedId
                ))
            {
                throw new InvalidOperationException(
                    $"Cannot activate data layer " +
                    $"'{normalizedId}' because it is not loaded."
                );
            }


            ActiveLayerId =
                normalizedId;


            Debug.Log(
                $"Active data layer: {ActiveLayerId}",
                this
            );
        }


        public void ClearActiveLayer()
        {
            ActiveLayerId = null;
        }


        // =========================================================
        // UNLOADING
        // =========================================================

        public bool UnloadLayer(
            string layerId
        )
        {
            string normalizedId =
                NormalizeLayerId(
                    layerId
                );


            if (!loadedLayers.Remove(
                    normalizedId
                ))
            {
                return false;
            }


            if (string.Equals(
                    ActiveLayerId,
                    normalizedId,
                    StringComparison.Ordinal
                ))
            {
                ActiveLayerId = null;
            }


            Debug.Log(
                $"Data layer unloaded: {normalizedId}",
                this
            );


            return true;
        }


        public void UnloadAllLayers()
        {
            loadedLayers.Clear();
            ActiveLayerId = null;
        }


        // =========================================================
        // DEPENDENCY RESOLUTION
        // =========================================================

        private void ResolveDependencies()
        {
            if (projectManager == null)
            {
                projectManager =
                    FindFirstObjectByType<ProjectManager>();
            }


            if (spatialLayerManager == null)
            {
                spatialLayerManager =
                    FindFirstObjectByType<SpatialLayerManager>();
            }
        }


        // =========================================================
        // VALIDATION
        // =========================================================

        private static void ValidateReference(
            ResourceReference reference
        )
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "Project manifest contains a null " +
                    "data layer reference."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    reference.id
                ))
            {
                throw new InvalidOperationException(
                    "Data layer reference is missing an ID."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    reference.definition
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{reference.id}' is missing " +
                    $"its definition path."
                );
            }
        }


        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "DataLayerManager has not finished initialization."
                );
            }
        }


        private static string NormalizeLayerId(
            string layerId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                throw new ArgumentException(
                    "Data layer ID cannot be null or empty.",
                    nameof(layerId)
                );
            }


            return layerId.Trim();
        }


        // =========================================================
        // DIAGNOSTICS
        // =========================================================

        private void LogLoadedLayer(
            DataLayer layer,
            SpatialLayer spatialLayer
        )
        {
            Debug.Log(
                $"Data layer loaded:\n" +
                $"ID: {layer.Id}\n" +
                $"Name: {layer.DisplayName}\n" +
                $"Target spatial layer: " +
                $"{layer.TargetSpatialLayerId}\n" +
                $"Data units: {layer.UnitCount}\n" +
                $"Target spatial units: " +
                $"{spatialLayer.UnitCount}\n" +
                $"Variables: {layer.VariableCount}",
                this
            );


            IReadOnlyList<DataVariableDefinition> variables =
                layer.Definition.Variables;


            for (
                int i = 0;
                i < variables.Count;
                i++
            )
            {
                DataVariableDefinition variable =
                    variables[i];


                if (variable == null)
                {
                    continue;
                }


                if (layer.TryGetNumericRange(
                        variable.Id,
                        out double minimum,
                        out double maximum
                    ))
                {
                    string unit =
                        string.IsNullOrWhiteSpace(
                            variable.Unit
                        )
                            ? string.Empty
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