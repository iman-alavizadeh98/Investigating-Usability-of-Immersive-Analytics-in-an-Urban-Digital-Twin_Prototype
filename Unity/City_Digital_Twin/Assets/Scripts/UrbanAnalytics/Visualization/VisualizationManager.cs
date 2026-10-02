using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Associations;
using UrbanAnalytics.Core;
using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.Spatial;
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


        // Needed for derived anchors (glyph layers). Resolved at
        // runtime; not serialized.
        private SpatialLayerManager
            spatialLayerManager;


        private SpatialReferenceManager
            spatialReferenceManager;


        // =========================================================
        // RENDERING
        // =========================================================

        [Header("Rendering")]
        [SerializeField]
        private Material
            vertexColorMaterial;


        [Tooltip(
            "Material for data-coloured buildings. Should use " +
            "UrbanAnalytics/BuildingVertexColorShaded so walls " +
            "and roofs stay distinguishable. Falls back to " +
            "Vertex Color Material when empty."
        )]
        [SerializeField]
        private Material
            buildingVertexColorMaterial;


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


        // Only one apply runs at a time. A new apply (or a
        // clear) cancels the one in flight and waits for it to
        // unwind before touching the scene.
        private CancellationTokenSource
            applyCancellation;


        private Task applyTask =
            Task.CompletedTask;


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


        /// <summary>
        /// The spec set in the Inspector (applied on start when
        /// Apply On Start is set). Runtime switchers list it first.
        /// </summary>
        public VisualizationSpec StartupVisualization =>
            startupVisualization;


        /// <summary>
        /// True while an apply is running (scene geometry may be
        /// changing).
        /// </summary>
        public bool IsApplying =>
            applyTask != null &&
            !applyTask.IsCompleted;


        /// <summary>
        /// Initializing or applying: chunk meshes may be created,
        /// destroyed or moved at any frame.
        /// </summary>
        public bool IsBusy =>
            IsInitializing ||
            IsApplying;


        // =========================================================
        // EVENTS
        // =========================================================

        public event Action<
            VisualizationLegendInfo
        > VisualizationChanged;


        public event Action
            VisualizationCleared;


        /// <summary>
        /// Raised synchronously before an apply or clear starts
        /// changing scene geometry. Listeners that read chunk
        /// meshes from worker threads must finish here.
        /// </summary>
        public event Action
            VisualizationChanging;


        /// <summary>
        /// Raised after every successful apply, whether or not the
        /// visualization has a legend.
        /// </summary>
        public event Action<
            VisualizationSpec
        > VisualizationApplied;


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


                if (buildingVertexColorMaterial == null)
                {
                    Debug.LogWarning(
                        "VisualizationManager: Building Vertex " +
                        "Color Material is not assigned; " +
                        "data-coloured buildings will use the " +
                        "unlit surface material and lose shading.",
                        this
                    );
                }


                renderContext =
                    new VisualizationRenderContext(
                        dataLayerManager,
                        geometryManager,
                        vertexColorMaterial,
                        urbanContextManager,
                        associationManager,
                        runtimeState,
                        buildingVertexColorMaterial,
                        spatialLayerManager,
                        spatialReferenceManager
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


            rendererRegistry.Register(
                new BarGlyphRenderer()
            );


            rendererRegistry.Register(
                new StackedBarGlyphRenderer()
            );


            rendererRegistry.Register(
                new RadialGlyphRenderer()
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


            VisualizationChanging
                ?.Invoke();


            // Cancel the apply in flight (if any). Its renderers
            // restore whatever they had already changed.
            applyCancellation?.Cancel();


            var cancellation =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken,
                        lifetimeCancellation != null
                            ? lifetimeCancellation.Token
                            : CancellationToken.None
                    );


            applyCancellation =
                cancellation;


            Task previous =
                applyTask;


            Task current =
                ApplyAfterAsync(
                    previous,
                    visualization,
                    cancellation.Token
                );


            applyTask =
                current;


            try
            {
                await current;
            }
            finally
            {
                if (applyCancellation == cancellation)
                {
                    applyCancellation =
                        null;
                }


                cancellation.Dispose();
            }
        }


        private async Task ApplyAfterAsync(
            Task previous,
            VisualizationSpec visualization,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await previous;
            }
            catch
            {
                // The previous request reports its own
                // outcome to its caller; here we only wait
                // for it to finish unwinding.
            }


            cancellationToken
                .ThrowIfCancellationRequested();


            ClearInstances();


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


                // A clear or newer apply may have arrived while
                // the last layer rendered.
                cancellationToken
                    .ThrowIfCancellationRequested();


                ActiveVisualization =
                    visualization;


                PublishPrimaryLegend();


                VisualizationApplied
                    ?.Invoke(
                        visualization
                    );


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
                ClearInstances();

                throw;
            }
        }


        // =========================================================
        // CLEAR
        // =========================================================

        /// <summary>
        /// Removes the active visualization and cancels any
        /// apply still in flight.
        /// </summary>
        public void ClearActiveVisualization()
        {
            VisualizationChanging
                ?.Invoke();


            applyCancellation?.Cancel();


            ClearInstances();
        }


        private void ClearInstances()
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


            spatialLayerManager =
                FindFirstObjectByType<
                    SpatialLayerManager
                >();


            spatialReferenceManager =
                FindFirstObjectByType<
                    SpatialReferenceManager
                >();
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