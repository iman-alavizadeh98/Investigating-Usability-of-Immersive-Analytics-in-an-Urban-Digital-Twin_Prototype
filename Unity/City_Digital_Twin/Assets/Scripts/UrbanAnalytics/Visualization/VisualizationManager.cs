using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Associations;
using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.UrbanContext;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Main visualization-library orchestrator.
    /// </summary>
    public sealed class VisualizationManager :
        MonoBehaviour
    {
        // =========================================================
        // DEPENDENCIES
        // =========================================================

        [Header("Dependencies")]
        [SerializeField]
        private DataLayerManager
            dataLayerManager;


        [SerializeField]
        private GeometryManager
            geometryManager;


        [SerializeField]
        private UrbanContextManager
            urbanContextManager;


        [SerializeField]
        private AssociationManager
            associationManager;


        // =========================================================
        // RENDERING
        // =========================================================

        [Header("Rendering")]
        [SerializeField]
        private Material
            vertexColorMaterial;


        // =========================================================
        // STARTUP
        // =========================================================

        [Header("Startup")]
        [SerializeField]
        private bool applyOnStart =
            true;


        [SerializeField]
        private VisualizationSpec
            startupVisualization =
                new VisualizationSpec();


        // =========================================================
        // RUNTIME
        // =========================================================

        private readonly List<
            VisualizationLayerInstance
        > activeInstances =
            new List<
                VisualizationLayerInstance
            >();


        private CancellationTokenSource
            lifetimeCancellation;


        private VisualizationRendererRegistry
            rendererRegistry;


        private VisualizationRenderContext
            renderContext;


        private VisualizationRuntimeState
            runtimeState;


        // =========================================================
        // PUBLIC
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


        public Task InitializationTask
        {
            get;
            private set;
        }


        public VisualizationSpec
            ActiveVisualization
        {
            get;
            private set;
        }


        public IReadOnlyList<
            VisualizationLayerInstance
        > ActiveInstances =>
            activeInstances;


        public VisualizationLegendInfo
            ActiveLegend
        {
            get;
            private set;
        }


        // =========================================================
        // EVENTS
        // =========================================================

        public event Action<
            VisualizationLegendInfo
        > VisualizationChanged;


        public event Action
            VisualizationCleared;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            ResolveDependencies();


            if (dataLayerManager == null)
            {
                DisableForMissingDependency(
                    "DataLayerManager"
                );

                return;
            }


            if (geometryManager == null)
            {
                DisableForMissingDependency(
                    "GeometryManager"
                );

                return;
            }


            if (vertexColorMaterial == null)
            {
                Debug.LogError(
                    "VisualizationManager requires " +
                    "a vertex-color material.",
                    this
                );


                enabled =
                    false;

                return;
            }


            lifetimeCancellation =
                new CancellationTokenSource();


            runtimeState =
                new VisualizationRuntimeState();


            rendererRegistry =
                new VisualizationRendererRegistry();


            RegisterRenderers();
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
            if (lifetimeCancellation != null)
            {
                lifetimeCancellation.Cancel();

                lifetimeCancellation.Dispose();

                lifetimeCancellation =
                    null;
            }


            ClearActiveVisualization();
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


            IsInitializing =
                true;


            LastError =
                null;


            try
            {
                if (
                    dataLayerManager
                        .InitializationTask ==
                        null ||
                    geometryManager
                        .InitializationTask ==
                        null
                )
                {
                    await Task.Yield();
                }


                cancellationToken
                    .ThrowIfCancellationRequested();


                if (
                    dataLayerManager
                        .InitializationTask ==
                        null
                )
                {
                    throw new InvalidOperationException(
                        "DataLayerManager did not start " +
                        "its initialization task."
                    );
                }


                if (
                    geometryManager
                        .InitializationTask ==
                        null
                )
                {
                    throw new InvalidOperationException(
                        "GeometryManager did not start " +
                        "its initialization task."
                    );
                }


                await dataLayerManager
                    .InitializationTask;


                await geometryManager
                    .InitializationTask;


                cancellationToken
                    .ThrowIfCancellationRequested();


                if (!dataLayerManager.IsInitialized)
                {
                    throw new InvalidOperationException(
                        $"DataLayerManager initialization " +
                        $"failed. Error: " +
                        $"{dataLayerManager.LastError}"
                    );
                }


                if (!geometryManager.IsInitialized)
                {
                    throw new InvalidOperationException(
                        $"GeometryManager initialization " +
                        $"failed. Error: " +
                        $"{geometryManager.LastError}"
                    );
                }


                renderContext =
                    new VisualizationRenderContext(
                        dataLayerManager,
                        geometryManager,
                        vertexColorMaterial,
                        urbanContextManager,
                        associationManager,
                        runtimeState
                    );


                IsInitialized =
                    true;


                Debug.Log(
                    "VisualizationManager initialized " +
                    "with target-aware modular " +
                    "renderer architecture.",
                    this
                );


                if (
                    applyOnStart &&
                    startupVisualization != null &&
                    startupVisualization
                        .IsConfigured
                )
                {
                    await ApplyVisualizationAsync(
                        startupVisualization,
                        cancellationToken
                    );
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
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
                IsInitializing =
                    false;
            }
        }


        // =========================================================
        // RENDERERS
        // =========================================================

        private void RegisterRenderers()
        {
            rendererRegistry.Register(
                new SurfaceRenderer()
            );


            rendererRegistry.Register(
                new HeightSurfaceRenderer()
            );


            rendererRegistry.Register(
                new BuildingSurfaceRenderer()
            );
        }


        // =========================================================
        // APPLY
        // =========================================================

        public async Task
            ApplyVisualizationAsync(
                VisualizationSpec visualization,
                CancellationToken cancellationToken =
                    default
            )
        {
            EnsureInitialized();


            if (visualization == null)
            {
                throw new ArgumentNullException(
                    nameof(visualization)
                );
            }


            if (!visualization.IsConfigured)
            {
                throw new InvalidOperationException(
                    "VisualizationSpec contains " +
                    "no configured layers."
                );
            }


            ClearActiveVisualization();


            try
            {
                foreach (
                    VisualizationLayerSpec layer
                    in visualization.Layers
                )
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();


                    if (layer == null ||
                        !layer.Enabled)
                    {
                        continue;
                    }


                    if (!layer.IsConfigured)
                    {
                        throw new InvalidOperationException(
                            "Visualization contains an " +
                            "enabled layer that is not " +
                            "configured."
                        );
                    }


                    IVisualizationRenderer renderer =
                        rendererRegistry.Get(
                            layer
                        );


                    VisualizationLayerInstance
                        instance =
                            await renderer.RenderAsync(
                                renderContext,
                                layer,
                                cancellationToken
                            );


                    activeInstances.Add(
                        instance
                    );
                }


                ActiveVisualization =
                    visualization;


                PublishPrimaryLegend();


                Debug.Log(
                    $"Visualization applied:\n" +
                    $"ID: {visualization.Id}\n" +
                    $"Name: " +
                    $"{visualization.DisplayName}\n" +
                    $"Runtime layers: " +
                    $"{activeInstances.Count}",
                    this
                );
            }
            catch
            {
                ClearActiveVisualization();

                throw;
            }
        }


        // =========================================================
        // CLEAR
        // =========================================================

        public void ClearActiveVisualization()
        {
            for (
                int i =
                    activeInstances.Count - 1;
                i >= 0;
                i--
            )
            {
                activeInstances[i]
                    ?.Dispose();
            }


            activeInstances.Clear();


            runtimeState
                ?.Clear();


            ActiveVisualization =
                null;


            ActiveLegend =
                null;


            VisualizationCleared
                ?.Invoke();
        }


        // =========================================================
        // LEGEND
        // =========================================================

        private void PublishPrimaryLegend()
        {
            ActiveLegend =
                null;


            foreach (
                VisualizationLayerInstance instance
                in activeInstances
            )
            {
                if (
                    instance == null ||
                    instance.Legends == null ||
                    instance.Legends.Count == 0
                )
                {
                    continue;
                }


                ActiveLegend =
                    instance.Legends[0];


                break;
            }


            if (ActiveLegend != null)
            {
                VisualizationChanged
                    ?.Invoke(
                        ActiveLegend
                    );
            }
            else
            {
                VisualizationCleared
                    ?.Invoke();
            }
        }


        // =========================================================
        // DEPENDENCIES
        // =========================================================

        private void ResolveDependencies()
        {
            if (dataLayerManager == null)
            {
                dataLayerManager =
                    FindFirstObjectByType<
                        DataLayerManager
                    >();
            }


            if (geometryManager == null)
            {
                geometryManager =
                    FindFirstObjectByType<
                        GeometryManager
                    >();
            }


            if (urbanContextManager == null)
            {
                urbanContextManager =
                    FindFirstObjectByType<
                        UrbanContextManager
                    >();
            }


            if (associationManager == null)
            {
                associationManager =
                    FindFirstObjectByType<
                        AssociationManager
                    >();
            }
        }


        private void
            DisableForMissingDependency(
                string dependencyName
            )
        {
            Debug.LogError(
                $"VisualizationManager could not " +
                $"find {dependencyName}.",
                this
            );


            enabled =
                false;
        }


        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "VisualizationManager has not " +
                    "finished initialization."
                );
            }
        }
    }
}