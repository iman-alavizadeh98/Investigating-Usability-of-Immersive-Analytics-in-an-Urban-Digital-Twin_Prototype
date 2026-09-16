using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Core;

namespace UrbanAnalytics.Spatial
{
    /// <summary>
    /// Central runtime manager for spatial analytical layers.
    ///
    /// Responsibilities:
    /// - discover spatial layers declared by the project manifest;
    /// - load layers on demand;
    /// - cache loaded layers;
    /// - provide stable lookup by layer ID;
    /// - unload layers;
    /// - track the currently active spatial layer.
    ///
    /// This manager does not:
    /// - create meshes;
    /// - render layers;
    /// - store analytical data values;
    /// - perform CRS-to-Unity conversion;
    /// - handle VR interaction.
    /// </summary>
    public sealed class SpatialLayerManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private ProjectManager projectManager;


        [Header("Startup")]
        [SerializeField]
        private bool loadAllLayersOnStart = true;


        private readonly Dictionary<string, SpatialLayer>
            loadedLayers =
                new Dictionary<string, SpatialLayer>(
                    StringComparer.Ordinal
                );


        private readonly Dictionary<string, ResourceReference>
            layerReferences =
                new Dictionary<string, ResourceReference>(
                    StringComparer.Ordinal
                );


        private readonly Dictionary<string, Task<SpatialLayer>>
            loadingTasks =
                new Dictionary<string, Task<SpatialLayer>>(
                    StringComparer.Ordinal
                );


        private CancellationTokenSource
            lifetimeCancellation;


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


        public SpatialLayer ActiveLayer
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
                    out SpatialLayer layer
                )
                    ? layer
                    : null;
            }
        }


        public IReadOnlyDictionary<string, SpatialLayer>
            LoadedLayers =>
                loadedLayers;


        private void Start()
        {
            _ = InitializeAsync(
                lifetimeCancellation.Token
            );
        }

        private void Awake()
        {
            if (projectManager == null)
            {
                projectManager =
                    FindFirstObjectByType<ProjectManager>();
            }

            if (projectManager == null)
            {
                Debug.LogError(
                    "SpatialLayerManager could not find ProjectManager.",
                    this
                );

                enabled = false;
                return;
            }

            lifetimeCancellation =
                new CancellationTokenSource();
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
                        "ProjectManager failed to load the project manifest."
                    );
                }


                RegisterManifestLayers(
                    projectManager.Manifest
                );


                IsInitialized = true;


                Debug.Log(
                    $"SpatialLayerManager initialized. " +
                    $"Available spatial layers: " +
                    $"{layerReferences.Count}",
                    this
                );


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


            if (manifest.spatialLayers == null)
            {
                return;
            }


            foreach (
                ResourceReference reference
                in manifest.spatialLayers
            )
            {
                ValidateReference(
                    reference
                );


                if (!layerReferences.TryAdd(
                        reference.id.Trim(),
                        reference
                    ))
                {
                    throw new InvalidOperationException(
                        $"Duplicate spatial layer reference " +
                        $"'{reference.id}' in project manifest."
                    );
                }
            }
        }


        // =========================================================
        // LOADING
        // =========================================================

        public async Task<SpatialLayer> LoadLayerAsync(
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
                    out SpatialLayer existingLayer
                ))
            {
                return existingLayer;
            }


            if (loadingTasks.TryGetValue(
                    normalizedId,
                    out Task<SpatialLayer> existingTask
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
                    $"Spatial layer '{normalizedId}' is not " +
                    $"declared in the project manifest."
                );
            }


            Task<SpatialLayer> loadingTask =
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


        private async Task<SpatialLayer> LoadLayerInternalAsync(
            string layerId,
            ResourceReference reference,
            CancellationToken cancellationToken
        )
        {
            SpatialLayer layer =
                await SpatialLayerLoader.LoadAsync(
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
                    $"Project manifest declares spatial layer " +
                    $"'{layerId}', but loaded layer declares " +
                    $"'{layer.Id}'."
                );
            }


            loadedLayers.Add(
                layer.Id,
                layer
            );


            Debug.Log(
                $"Spatial layer loaded:\n" +
                $"ID: {layer.Id}\n" +
                $"Name: {layer.DisplayName}\n" +
                $"Units: {layer.UnitCount}\n" +
                $"Geometry: {layer.Definition.GeometryType}\n" +
                $"Bounds: {layer.Bounds}",
                this
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
            out SpatialLayer layer
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


        public SpatialLayer GetLayer(
            string layerId
        )
        {
            if (!TryGetLayer(
                    layerId,
                    out SpatialLayer layer
                ))
            {
                throw new KeyNotFoundException(
                    $"Spatial layer '{layerId}' is not loaded."
                );
            }


            return layer;
        }


        public bool TryGetUnit(
            string layerId,
            string unitId,
            out SpatialUnit unit
        )
        {
            unit = null;


            if (!TryGetLayer(
                    layerId,
                    out SpatialLayer layer
                ))
            {
                return false;
            }


            return layer.TryGetUnit(
                unitId,
                out unit
            );
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
                    $"Cannot activate spatial layer " +
                    $"'{normalizedId}' because it is not loaded."
                );
            }


            ActiveLayerId =
                normalizedId;


            Debug.Log(
                $"Active spatial layer: {ActiveLayerId}",
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
                $"Spatial layer unloaded: {normalizedId}",
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
                    "spatial layer reference."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    reference.id
                ))
            {
                throw new InvalidOperationException(
                    "Spatial layer reference is missing an ID."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    reference.definition
                ))
            {
                throw new InvalidOperationException(
                    $"Spatial layer '{reference.id}' is missing " +
                    $"its definition path."
                );
            }
        }


        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "SpatialLayerManager has not finished initialization."
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
                    "Spatial layer ID cannot be null or empty.",
                    nameof(layerId)
                );
            }


            return layerId.Trim();
        }
    }
}