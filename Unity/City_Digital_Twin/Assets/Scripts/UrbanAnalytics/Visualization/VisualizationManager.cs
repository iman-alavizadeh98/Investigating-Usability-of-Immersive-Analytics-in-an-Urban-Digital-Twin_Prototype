using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Applies analytical DataLayer values to rendered SpatialLayers.
    ///
    /// Current supported visualization channel:
    /// - Color
    ///
    /// Color encoding modifies existing mesh vertex colors.
    /// Spatial geometry is not rebuilt.
    ///
    /// This manager does not:
    /// - own analytical values;
    /// - own spatial geometry;
    /// - triangulate geometry;
    /// - create SpatialUnits;
    /// - perform VR interaction;
    /// - own UI presentation.
    /// </summary>
    public sealed class VisualizationManager : MonoBehaviour
    {
        // =========================================================
        // DEPENDENCIES
        // =========================================================

        [Header("Dependencies")]
        [SerializeField]
        private DataLayerManager dataLayerManager;

        [SerializeField]
        private GeometryManager geometryManager;


        // =========================================================
        // MATERIAL
        // =========================================================

        [Header("Rendering")]
        [Tooltip(
            "Material using the Custom/VertexColorUnlit shader."
        )]
        [SerializeField]
        private Material vertexColorMaterial;


        // =========================================================
        // STARTUP
        // =========================================================

        [Header("Startup")]
        [SerializeField]
        private bool applyOnStart = true;

        [SerializeField]
        private VisualizationEncoding startupEncoding =
            new VisualizationEncoding();


        // =========================================================
        // RUNTIME STATE
        // =========================================================

        private readonly Dictionary<SpatialMeshChunk, Color32[]>
            colorBuffers =
                new Dictionary<SpatialMeshChunk, Color32[]>();

        private readonly Dictionary<SpatialMeshChunk, Material>
            originalMaterials =
                new Dictionary<SpatialMeshChunk, Material>();

        private CancellationTokenSource lifetimeCancellation;


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

        public Task InitializationTask
        {
            get;
            private set;
        }

        public VisualizationEncoding ActiveEncoding
        {
            get;
            private set;
        }

        public VisualizationLegendInfo ActiveLegend
        {
            get;
            private set;
        }


        // =========================================================
        // EVENTS
        // =========================================================

        /// <summary>
        /// Raised whenever an active visualization changes.
        /// UI components such as legends can subscribe to this.
        /// </summary>
        public event Action<VisualizationLegendInfo>
            VisualizationChanged;

        /// <summary>
        /// Raised when the active visualization is removed.
        /// </summary>
        public event Action
            VisualizationCleared;


        // =========================================================
        // UNITY LIFECYCLE
        // =========================================================

        private void Awake()
        {
            ResolveDependencies();


            if (dataLayerManager == null)
            {
                Debug.LogError(
                    "VisualizationManager could not find DataLayerManager.",
                    this
                );

                enabled = false;
                return;
            }


            if (geometryManager == null)
            {
                Debug.LogError(
                    "VisualizationManager could not find GeometryManager.",
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
            if (lifetimeCancellation != null)
            {
                lifetimeCancellation.Cancel();
                lifetimeCancellation.Dispose();

                lifetimeCancellation = null;
            }


            colorBuffers.Clear();
            originalMaterials.Clear();

            ActiveEncoding = null;
            ActiveLegend = null;
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
                /*
                 * Start() order between MonoBehaviours is not
                 * guaranteed.
                 *
                 * Allow the dependent managers to create their
                 * initialization tasks.
                 */
                if (dataLayerManager.InitializationTask == null ||
                    geometryManager.InitializationTask == null)
                {
                    await Task.Yield();
                }


                cancellationToken.ThrowIfCancellationRequested();


                if (dataLayerManager.InitializationTask == null)
                {
                    throw new InvalidOperationException(
                        "DataLayerManager did not start its " +
                        "initialization task."
                    );
                }


                if (geometryManager.InitializationTask == null)
                {
                    throw new InvalidOperationException(
                        "GeometryManager did not start its " +
                        "initialization task."
                    );
                }


                await dataLayerManager.InitializationTask;
                await geometryManager.InitializationTask;


                cancellationToken.ThrowIfCancellationRequested();


                if (!dataLayerManager.IsInitialized)
                {
                    throw new InvalidOperationException(
                        "DataLayerManager did not initialize successfully. " +
                        $"Error: {dataLayerManager.LastError}"
                    );
                }


                if (!geometryManager.IsInitialized)
                {
                    throw new InvalidOperationException(
                        "GeometryManager did not initialize successfully. " +
                        $"Error: {geometryManager.LastError}"
                    );
                }


                IsInitialized = true;


                Debug.Log(
                    "VisualizationManager initialized.",
                    this
                );


                if (applyOnStart &&
                    startupEncoding != null &&
                    startupEncoding.IsConfigured)
                {
                    await ApplyEncodingInternalAsync(
                        startupEncoding,
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


        // =========================================================
        // PUBLIC API
        // =========================================================

        /// <summary>
        /// Applies a complete visualization encoding.
        /// </summary>
        public async Task ApplyEncodingAsync(
            VisualizationEncoding encoding,
            CancellationToken cancellationToken = default
        )
        {
            EnsureInitialized();


            await ApplyEncodingInternalAsync(
                encoding,
                cancellationToken
            );
        }


        /// <summary>
        /// Applies a color encoding using the full numeric
        /// range of the selected variable.
        /// </summary>
        public async Task ApplyColorAsync(
            string dataLayerId,
            string variableId,
            CancellationToken cancellationToken = default
        )
        {
            var encoding =
                new VisualizationEncoding(
                    dataLayerId,
                    variableId
                );


            encoding.UseDataRange();


            await ApplyEncodingAsync(
                encoding,
                cancellationToken
            );
        }


        /// <summary>
        /// Applies a color encoding using a manually
        /// specified numeric range.
        /// </summary>
        public async Task ApplyColorAsync(
            string dataLayerId,
            string variableId,
            double minimum,
            double maximum,
            CancellationToken cancellationToken = default
        )
        {
            var encoding =
                new VisualizationEncoding(
                    dataLayerId,
                    variableId
                );


            encoding.UseManualRange(
                minimum,
                maximum
            );


            await ApplyEncodingAsync(
                encoding,
                cancellationToken
            );
        }


        /// <summary>
        /// Changes the numeric domain used by the active
        /// color visualization.
        /// </summary>
        public async Task SetActiveColorRangeAsync(
            double minimum,
            double maximum,
            CancellationToken cancellationToken = default
        )
        {
            EnsureActiveColorEncoding();


            ActiveEncoding.UseManualRange(
                minimum,
                maximum
            );


            await ApplyEncodingInternalAsync(
                ActiveEncoding,
                cancellationToken
            );
        }


        /// <summary>
        /// Returns the active color visualization to automatic
        /// data min/max normalization.
        /// </summary>
        public async Task UseActiveDataRangeAsync(
            CancellationToken cancellationToken = default
        )
        {
            EnsureActiveColorEncoding();


            ActiveEncoding.UseDataRange();


            await ApplyEncodingInternalAsync(
                ActiveEncoding,
                cancellationToken
            );
        }


        /// <summary>
        /// Changes the gradient used by the active visualization.
        /// </summary>
        public async Task SetActiveGradientAsync(
            Gradient gradient,
            CancellationToken cancellationToken = default
        )
        {
            EnsureActiveColorEncoding();


            ActiveEncoding.SetGradient(
                gradient
            );


            await ApplyEncodingInternalAsync(
                ActiveEncoding,
                cancellationToken
            );
        }


        /// <summary>
        /// Reverses or restores the current gradient direction.
        /// </summary>
        public async Task SetGradientReversedAsync(
            bool reversed,
            CancellationToken cancellationToken = default
        )
        {
            EnsureActiveColorEncoding();


            ActiveEncoding.SetReverseGradient(
                reversed
            );


            await ApplyEncodingInternalAsync(
                ActiveEncoding,
                cancellationToken
            );
        }


        /// <summary>
        /// Changes the color used for units with no valid value.
        /// </summary>
        public async Task SetActiveNoDataColorAsync(
            Color color,
            CancellationToken cancellationToken = default
        )
        {
            EnsureActiveColorEncoding();


            ActiveEncoding.SetNoDataColor(
                color
            );


            await ApplyEncodingInternalAsync(
                ActiveEncoding,
                cancellationToken
            );
        }


        // =========================================================
        // ENCODING DISPATCH
        // =========================================================

        private async Task ApplyEncodingInternalAsync(
            VisualizationEncoding encoding,
            CancellationToken cancellationToken
        )
        {
            if (encoding == null)
            {
                throw new ArgumentNullException(
                    nameof(encoding)
                );
            }


            if (!encoding.IsConfigured)
            {
                throw new InvalidOperationException(
                    "VisualizationEncoding requires both a " +
                    "DataLayer ID and variable ID."
                );
            }


            switch (encoding.Channel)
            {
                case VisualizationChannel.Color:
                    {
                        await ApplyColorEncodingAsync(
                            encoding,
                            cancellationToken
                        );

                        break;
                    }


                default:
                    throw new NotSupportedException(
                        $"Visualization channel " +
                        $"'{encoding.Channel}' is not supported."
                    );
            }


            ActiveEncoding =
                encoding;
        }


        // =========================================================
        // COLOR ENCODING
        // =========================================================

        private async Task ApplyColorEncodingAsync(
            VisualizationEncoding encoding,
            CancellationToken cancellationToken
        )
        {
            if (vertexColorMaterial == null)
            {
                throw new InvalidOperationException(
                    "VisualizationManager requires a material " +
                    "using the Custom/VertexColorUnlit shader."
                );
            }


            // -----------------------------------------------------
            // Resolve DataLayer
            // -----------------------------------------------------

            DataLayer dataLayer;


            if (!dataLayerManager.TryGetLayer(
                    encoding.DataLayerId,
                    out dataLayer
                ))
            {
                dataLayer =
                    await dataLayerManager.LoadLayerAsync(
                        encoding.DataLayerId
                    );
            }


            cancellationToken.ThrowIfCancellationRequested();


            // -----------------------------------------------------
            // Validate variable
            // -----------------------------------------------------

            if (!dataLayer.ContainsVariable(
                    encoding.VariableId
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{dataLayer.Id}' does not contain " +
                    $"variable '{encoding.VariableId}'."
                );
            }


            // -----------------------------------------------------
            // Resolve visualization range
            // -----------------------------------------------------

            ResolveValueRange(
                dataLayer,
                encoding,
                out double minimum,
                out double maximum
            );


            // -----------------------------------------------------
            // Resolve target SpatialLayer
            // -----------------------------------------------------

            string spatialLayerId =
                dataLayer.TargetSpatialLayerId;


            if (!geometryManager.IsLayerRendered(
                    spatialLayerId
                ))
            {
                await geometryManager.RenderLayerAsync(
                    spatialLayerId,
                    cancellationToken
                );
            }


            cancellationToken.ThrowIfCancellationRequested();


            if (!geometryManager.TryGetRenderedLayerRoot(
                    spatialLayerId,
                    out GameObject layerRoot
                ))
            {
                throw new InvalidOperationException(
                    $"Spatial layer '{spatialLayerId}' is loaded " +
                    $"but has no rendered layer root."
                );
            }


            SpatialMeshChunk[] chunks =
                layerRoot.GetComponentsInChildren<SpatialMeshChunk>(
                    true
                );


            if (chunks == null ||
                chunks.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Rendered spatial layer '{spatialLayerId}' " +
                    $"contains no SpatialMeshChunks."
                );
            }


            // -----------------------------------------------------
            // Apply colors
            // -----------------------------------------------------

            int coloredUnits = 0;
            int noDataUnits = 0;
            int processedVertices = 0;
            int updatedChunks = 0;


            for (
                int i = 0;
                i < chunks.Length;
                i++
            )
            {
                cancellationToken.ThrowIfCancellationRequested();


                SpatialMeshChunk chunk =
                    chunks[i];


                if (chunk == null ||
                    !chunk.IsInitialized)
                {
                    continue;
                }


                if (!string.Equals(
                        chunk.SpatialLayerId,
                        spatialLayerId,
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }


                ApplyColorsToChunk(
                    chunk,
                    dataLayer,
                    encoding,
                    minimum,
                    maximum,
                    out int chunkColoredUnits,
                    out int chunkNoDataUnits
                );


                coloredUnits +=
                    chunkColoredUnits;

                noDataUnits +=
                    chunkNoDataUnits;

                processedVertices +=
                    chunk.Mesh.vertexCount;

                updatedChunks++;


                /*
                 * Avoid updating every chunk in one
                 * uninterrupted frame.
                 */
                await Task.Yield();
            }


            cancellationToken.ThrowIfCancellationRequested();


            // -----------------------------------------------------
            // Publish current visualization metadata
            // -----------------------------------------------------

            PublishLegend(
                dataLayer,
                encoding,
                spatialLayerId,
                minimum,
                maximum
            );


            Debug.Log(
                $"Visualization applied:\n" +
                $"Data layer: {dataLayer.Id}\n" +
                $"Variable: {encoding.VariableId}\n" +
                $"Spatial layer: {spatialLayerId}\n" +
                $"Channel: Color\n" +
                $"Range: [{minimum}, {maximum}]\n" +
                $"Colored units: {coloredUnits}\n" +
                $"No-data units: {noDataUnits}\n" +
                $"Vertices updated: {processedVertices}\n" +
                $"Chunks updated: {updatedChunks}",
                this
            );
        }


        // =========================================================
        // CHUNK COLOR UPDATE
        // =========================================================

        private void ApplyColorsToChunk(
            SpatialMeshChunk chunk,
            DataLayer dataLayer,
            VisualizationEncoding encoding,
            double minimum,
            double maximum,
            out int coloredUnits,
            out int noDataUnits
        )
        {
            coloredUnits = 0;
            noDataUnits = 0;


            Mesh mesh =
                chunk.Mesh;


            if (mesh == null)
            {
                throw new InvalidOperationException(
                    $"SpatialMeshChunk '{chunk.name}' has no mesh."
                );
            }


            // -----------------------------------------------------
            // Remember original material
            // -----------------------------------------------------

            if (!originalMaterials.ContainsKey(
                    chunk
                ))
            {
                originalMaterials.Add(
                    chunk,
                    chunk.MeshRenderer.sharedMaterial
                );
            }


            // -----------------------------------------------------
            // Reuse vertex-color buffer
            // -----------------------------------------------------

            if (!colorBuffers.TryGetValue(
                    chunk,
                    out Color32[] colors
                ) ||
                colors == null ||
                colors.Length != mesh.vertexCount)
            {
                colors =
                    new Color32[
                        mesh.vertexCount
                    ];


                colorBuffers[
                    chunk
                ] = colors;


                mesh.MarkDynamic();
            }


            Color32 noDataColor =
                encoding.GetNoDataColor();


            // Initially mark everything as no-data.
            for (
                int i = 0;
                i < colors.Length;
                i++
            )
            {
                colors[i] =
                    noDataColor;
            }


            // -----------------------------------------------------
            // Apply one color to each semantic unit
            // -----------------------------------------------------

            IReadOnlyList<SpatialMeshUnitRange> ranges =
                chunk.UnitRanges;


            for (
                int i = 0;
                i < ranges.Count;
                i++
            )
            {
                SpatialMeshUnitRange range =
                    ranges[i];


                Color32 unitColor;


                if (dataLayer.TryGetDouble(
                        range.UnitId,
                        encoding.VariableId,
                        out double value
                    ))
                {
                    float normalized =
                        NormalizeValue(
                            value,
                            minimum,
                            maximum
                        );


                    unitColor =
                        encoding.EvaluateColor(
                            normalized
                        );


                    coloredUnits++;
                }
                else
                {
                    unitColor =
                        noDataColor;


                    noDataUnits++;
                }


                for (
                    int vertexIndex = range.VertexStart;
                    vertexIndex < range.VertexEndExclusive;
                    vertexIndex++
                )
                {
                    colors[
                        vertexIndex
                    ] = unitColor;
                }
            }


            /*
             * Only the vertex-color buffer changes.
             *
             * Mesh positions, indices, normals and semantic
             * mappings stay unchanged.
             */
            mesh.colors32 =
                colors;


            chunk.SetMaterial(
                vertexColorMaterial
            );
        }


        // =========================================================
        // VALUE RANGE
        // =========================================================

        private static void ResolveValueRange(
            DataLayer dataLayer,
            VisualizationEncoding encoding,
            out double minimum,
            out double maximum
        )
        {
            switch (encoding.RangeMode)
            {
                case VisualizationRangeMode.DataMinMax:
                    {
                        if (!dataLayer.TryGetNumericRange(
                                encoding.VariableId,
                                out minimum,
                                out maximum
                            ))
                        {
                            throw new InvalidOperationException(
                                $"Could not calculate numeric range for " +
                                $"'{dataLayer.Id}.{encoding.VariableId}'."
                            );
                        }

                        break;
                    }


                case VisualizationRangeMode.Manual:
                    {
                        minimum =
                            encoding.ManualMinimum;

                        maximum =
                            encoding.ManualMaximum;

                        break;
                    }


                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(encoding.RangeMode)
                    );
            }


            if (double.IsNaN(minimum) ||
                double.IsInfinity(minimum) ||
                double.IsNaN(maximum) ||
                double.IsInfinity(maximum))
            {
                throw new InvalidOperationException(
                    "Visualization range contains a non-finite value."
                );
            }


            if (maximum < minimum)
            {
                throw new InvalidOperationException(
                    $"Visualization maximum ({maximum}) cannot be " +
                    $"smaller than minimum ({minimum})."
                );
            }
        }


        private static float NormalizeValue(
            double value,
            double minimum,
            double maximum
        )
        {
            if (maximum <= minimum)
            {
                return 0.5f;
            }


            double normalized =
                (value - minimum) /
                (maximum - minimum);


            if (normalized < 0.0)
            {
                normalized = 0.0;
            }
            else if (normalized > 1.0)
            {
                normalized = 1.0;
            }


            return (float)normalized;
        }


        // =========================================================
        // LEGEND STATE
        // =========================================================

        private void PublishLegend(
            DataLayer dataLayer,
            VisualizationEncoding encoding,
            string spatialLayerId,
            double minimum,
            double maximum
        )
        {
            string displayName =
                encoding.VariableId;

            string unit =
                string.Empty;


            if (dataLayer.TryGetVariableDefinition(
                    encoding.VariableId,
                    out DataVariableDefinition variableDefinition
                ))
            {
                if (!string.IsNullOrWhiteSpace(
                        variableDefinition.DisplayName
                    ))
                {
                    displayName =
                        variableDefinition.DisplayName;
                }


                unit =
                    variableDefinition.Unit
                    ?? string.Empty;
            }


            ActiveLegend =
                new VisualizationLegendInfo(
                    dataLayer.Id,
                    encoding.VariableId,
                    displayName,
                    unit,
                    spatialLayerId,
                    minimum,
                    maximum,
                    encoding.ColorGradient,
                    encoding.ReverseGradient,
                    encoding.NoDataColor
                );


            VisualizationChanged?.Invoke(
                ActiveLegend
            );
        }


        private void ClearLegend()
        {
            ActiveLegend =
                null;


            VisualizationCleared?.Invoke();
        }


        // =========================================================
        // RESET
        // =========================================================

        /// <summary>
        /// Removes analytical coloring from a rendered spatial
        /// layer and restores its original material.
        /// </summary>
        public bool ResetLayerVisualization(
            string spatialLayerId
        )
        {
            EnsureInitialized();


            if (string.IsNullOrWhiteSpace(
                    spatialLayerId
                ))
            {
                return false;
            }


            if (!geometryManager.TryGetRenderedLayerRoot(
                    spatialLayerId,
                    out GameObject layerRoot
                ))
            {
                return false;
            }


            SpatialMeshChunk[] chunks =
                layerRoot.GetComponentsInChildren<SpatialMeshChunk>(
                    true
                );


            Color32 white =
                new Color32(
                    255,
                    255,
                    255,
                    255
                );


            foreach (
                SpatialMeshChunk chunk
                in chunks
            )
            {
                if (chunk == null ||
                    chunk.Mesh == null)
                {
                    continue;
                }


                Mesh mesh =
                    chunk.Mesh;


                if (!colorBuffers.TryGetValue(
                        chunk,
                        out Color32[] colors
                    ) ||
                    colors == null ||
                    colors.Length != mesh.vertexCount)
                {
                    colors =
                        new Color32[
                            mesh.vertexCount
                        ];


                    colorBuffers[
                        chunk
                    ] = colors;
                }


                for (
                    int i = 0;
                    i < colors.Length;
                    i++
                )
                {
                    colors[i] =
                        white;
                }


                mesh.colors32 =
                    colors;


                if (originalMaterials.TryGetValue(
                        chunk,
                        out Material originalMaterial
                    ) &&
                    originalMaterial != null)
                {
                    chunk.SetMaterial(
                        originalMaterial
                    );
                }
            }


            bool clearedActiveVisualization =
                false;


            if (ActiveEncoding != null &&
                dataLayerManager.TryGetLayer(
                    ActiveEncoding.DataLayerId,
                    out DataLayer activeDataLayer
                ))
            {
                if (string.Equals(
                        activeDataLayer.TargetSpatialLayerId,
                        spatialLayerId,
                        StringComparison.Ordinal
                    ))
                {
                    ActiveEncoding =
                        null;

                    clearedActiveVisualization =
                        true;
                }
            }


            if (clearedActiveVisualization)
            {
                ClearLegend();
            }


            Debug.Log(
                $"Visualization reset for spatial layer: " +
                $"{spatialLayerId}",
                this
            );


            return true;
        }


        /// <summary>
        /// Resets the spatial layer targeted by the currently
        /// active visualization.
        /// </summary>
        public bool ResetActiveVisualization()
        {
            EnsureInitialized();


            if (ActiveEncoding == null)
            {
                return false;
            }


            if (!dataLayerManager.TryGetLayer(
                    ActiveEncoding.DataLayerId,
                    out DataLayer dataLayer
                ))
            {
                return false;
            }


            return ResetLayerVisualization(
                dataLayer.TargetSpatialLayerId
            );
        }


        // =========================================================
        // ACTIVE ENCODING VALIDATION
        // =========================================================

        private void EnsureActiveColorEncoding()
        {
            EnsureInitialized();


            if (ActiveEncoding == null)
            {
                throw new InvalidOperationException(
                    "There is no active visualization encoding."
                );
            }


            if (ActiveEncoding.Channel !=
                VisualizationChannel.Color)
            {
                throw new InvalidOperationException(
                    "The active visualization is not a " +
                    "color encoding."
                );
            }
        }


        // =========================================================
        // DEPENDENCY RESOLUTION
        // =========================================================

        private void ResolveDependencies()
        {
            if (dataLayerManager == null)
            {
                dataLayerManager =
                    FindFirstObjectByType<DataLayerManager>();
            }


            if (geometryManager == null)
            {
                geometryManager =
                    FindFirstObjectByType<GeometryManager>();
            }
        }


        // =========================================================
        // VALIDATION
        // =========================================================

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "VisualizationManager has not finished initialization."
                );
            }
        }
    }
}