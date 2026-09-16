using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Core;
using UrbanAnalytics.Spatial;
using UrbanAnalytics.Spatial.Geometry;

namespace UrbanAnalytics.Rendering
{
    /// <summary>
    /// Creates and manages Unity render geometry for loaded
    /// analytical SpatialLayers.
    ///
    /// Current provider:
    /// - procedural Polygon / MultiPolygon rendering
    ///
    /// Responsibilities:
    /// - obtain loaded semantic spatial layers;
    /// - spatially order units for coherent mesh chunks;
    /// - divide layers into render chunks;
    /// - create batched Unity meshes;
    /// - create SpatialMeshChunk components;
    /// - manage visibility;
    /// - manage optional mesh colliders;
    /// - destroy/rebuild rendered layers.
    ///
    /// This manager does NOT:
    /// - own source GIS geometry;
    /// - own analytical values;
    /// - decide visualization encodings;
    /// - perform VR interaction;
    /// - change spatial-layer lifecycle.
    /// </summary>
    public sealed class GeometryManager : MonoBehaviour
    {
        // =========================================================
        // DEPENDENCIES
        // =========================================================

        [Header("Dependencies")]

        [SerializeField]
        private SpatialLayerManager spatialLayerManager;

        [SerializeField]
        private SpatialReferenceManager spatialReferenceManager;

        [Tooltip(
            "Usually CityRoot. Runtime spatial meshes will be " +
            "created beneath this transform."
        )]
        [SerializeField]
        private Transform renderParent;


        // =========================================================
        // RENDER CONFIGURATION
        // =========================================================

        [Header("Rendering")]

        [SerializeField]
        private Material spatialLayerMaterial;

        [SerializeField]
        private bool renderVisibleLayersOnStart = true;

        [SerializeField]
        [Min(1)]
        private int unitsPerChunk = 200;

        [Tooltip(
            "Small visual Y offset used to avoid coplanar " +
            "z-fighting with other flat city surfaces."
        )]
        [SerializeField]
        private float surfaceYOffset = 0.002f;


        // =========================================================
        // INTERACTION / COLLISION
        // =========================================================

        [Header("Interaction")]

        [Tooltip(
            "Mesh colliders allow triangleIndex -> SpatialUnit " +
            "lookup, but are more expensive on Quest. " +
            "Keep disabled until interaction requires them."
        )]
        [SerializeField]
        private bool enableMeshColliders = false;


        // =========================================================
        // RUNTIME STATE
        // =========================================================

        private readonly Dictionary<string, RenderedLayer>
            renderedLayers =
                new Dictionary<string, RenderedLayer>(
                    StringComparer.Ordinal
                );


        private readonly Dictionary<string, Task<GameObject>>
            renderingTasks =
                new Dictionary<string, Task<GameObject>>(
                    StringComparer.Ordinal
                );


        private CancellationTokenSource
            lifetimeCancellation;


        private Transform runtimeSpatialLayersRoot;

        private Material fallbackMaterial;

        private bool ownsFallbackMaterial;


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


        public int RenderedLayerCount =>
            renderedLayers.Count;


        // =========================================================
        // UNITY LIFECYCLE
        // =========================================================

        private void Awake()
        {
            ResolveDependencies();


            if (spatialLayerManager == null)
            {
                Debug.LogError(
                    "GeometryManager could not find SpatialLayerManager.",
                    this
                );

                enabled = false;
                return;
            }


            if (spatialReferenceManager == null)
            {
                Debug.LogError(
                    "GeometryManager could not find SpatialReferenceManager.",
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


            renderedLayers.Clear();
            renderingTasks.Clear();


            if (ownsFallbackMaterial &&
                fallbackMaterial != null)
            {
                Destroy(
                    fallbackMaterial
                );

                fallbackMaterial = null;
            }
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
                 * Start() execution order between different MonoBehaviours
                 * is not guaranteed.
                 *
                 * GeometryManager.Start() may execute before
                 * SpatialLayerManager.Start() has assigned InitializationTask.
                 *
                 * Yield once so all scene Start() methods have had the
                 * opportunity to execute.
                 */
                if (spatialLayerManager.InitializationTask == null)
                    {
                        await Task.Yield();
                    }


                    cancellationToken
                        .ThrowIfCancellationRequested();


                if (spatialLayerManager.InitializationTask == null)
                {
                    throw new InvalidOperationException(
                        "SpatialLayerManager did not start its initialization task."
                    );
                }


                await spatialLayerManager.InitializationTask;


                cancellationToken
                    .ThrowIfCancellationRequested();


                if (!spatialLayerManager.IsInitialized)
                {
                    throw new InvalidOperationException(
                        $"SpatialLayerManager did not initialize successfully. " +
                        $"Error: {spatialLayerManager.LastError}"
                    );
                }


                if (!spatialReferenceManager.IsReady)
                {
                    throw new InvalidOperationException(
                        "SpatialReferenceManager is not ready."
                    );
                }


                ResolveRenderParent();

                CreateRuntimeRoot();


                if (renderVisibleLayersOnStart)
                {
                    foreach (
                        KeyValuePair<string, SpatialLayer> pair
                        in spatialLayerManager.LoadedLayers
                    )
                    {
                        cancellationToken
                            .ThrowIfCancellationRequested();


                        SpatialLayer layer =
                            pair.Value;


                        if (!layer.Definition.VisibleByDefault)
                        {
                            continue;
                        }


                        await RenderLayerInternalAsync(
                            layer,
                            cancellationToken
                        );
                    }
                }


                IsInitialized = true;


                Debug.Log(
                    $"GeometryManager initialized. " +
                    $"Rendered spatial layers: " +
                    $"{renderedLayers.Count}",
                    this
                );
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
        // PUBLIC RENDER API
        // =========================================================

        public async Task<GameObject> RenderLayerAsync(
            string layerId,
            CancellationToken cancellationToken = default
        )
        {
            EnsureInitialized();


            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                throw new ArgumentException(
                    "Spatial layer ID cannot be null or empty.",
                    nameof(layerId)
                );
            }


            string normalizedId =
                layerId.Trim();


            if (renderedLayers.TryGetValue(
                    normalizedId,
                    out RenderedLayer existingLayer
                ))
            {
                return existingLayer.Root;
            }


            if (renderingTasks.TryGetValue(
                    normalizedId,
                    out Task<GameObject> existingTask
                ))
            {
                return await existingTask;
            }


            if (!spatialLayerManager.TryGetLayer(
                    normalizedId,
                    out SpatialLayer spatialLayer
                ))
            {
                throw new KeyNotFoundException(
                    $"Spatial layer '{normalizedId}' is not loaded."
                );
            }


            Task<GameObject> task =
                RenderLayerInternalAsync(
                    spatialLayer,
                    cancellationToken
                );


            renderingTasks.Add(
                normalizedId,
                task
            );


            try
            {
                return await task;
            }
            finally
            {
                renderingTasks.Remove(
                    normalizedId
                );
            }
        }


        public async Task<GameObject> RebuildLayerAsync(
            string layerId,
            CancellationToken cancellationToken = default
        )
        {
            RemoveRenderedLayer(
                layerId
            );


            return await RenderLayerAsync(
                layerId,
                cancellationToken
            );
        }


        // =========================================================
        // INTERNAL LAYER RENDERING
        // =========================================================

        private async Task<GameObject> RenderLayerInternalAsync(
            SpatialLayer layer,
            CancellationToken cancellationToken
        )
        {
            if (layer == null)
            {
                throw new ArgumentNullException(
                    nameof(layer)
                );
            }


            if (renderedLayers.TryGetValue(
                    layer.Id,
                    out RenderedLayer existing
                ))
            {
                return existing.Root;
            }


            ValidateLayerForProceduralRendering(
                layer
            );


            List<SpatialUnit> orderedUnits =
                OrderUnitsSpatially(
                    layer
                );


            GameObject layerObject =
                new GameObject(
                    layer.Id
                );


            Transform layerTransform =
                layerObject.transform;


            layerTransform.SetParent(
                runtimeSpatialLayersRoot,
                false
            );


            layerTransform.localPosition =
                new Vector3(
                    0.0f,
                    surfaceYOffset,
                    0.0f
                );

            layerTransform.localRotation =
                Quaternion.identity;

            layerTransform.localScale =
                Vector3.one;


            var chunks =
                new List<SpatialMeshChunk>();


            int totalVertices = 0;
            int totalTriangles = 0;


            try
            {
                int chunkIndex = 0;


                for (
                    int unitStart = 0;
                    unitStart < orderedUnits.Count;
                    unitStart += unitsPerChunk
                )
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();


                    int unitEnd =
                        Math.Min(
                            unitStart + unitsPerChunk,
                            orderedUnits.Count
                        );


                    SpatialMeshChunk chunk =
                        BuildChunk(
                            layer,
                            orderedUnits,
                            unitStart,
                            unitEnd,
                            chunkIndex,
                            layerTransform
                        );


                    chunks.Add(
                        chunk
                    );


                    totalVertices +=
                        chunk.Mesh.vertexCount;

                    totalTriangles +=
                        chunk.TriangleCount;


                    chunkIndex++;


                    /*
                     * Avoid generating every chunk in a single
                     * uninterrupted frame.
                     *
                     * This is particularly useful on Quest when
                     * loading larger layers.
                     */
                    await Task.Yield();
                }


                cancellationToken
                    .ThrowIfCancellationRequested();


                var renderedLayer =
                    new RenderedLayer(
                        layer,
                        layerObject,
                        chunks
                    );


                renderedLayers.Add(
                    layer.Id,
                    renderedLayer
                );


                Debug.Log(
                    $"Spatial layer rendered:\n" +
                    $"ID: {layer.Id}\n" +
                    $"Units: {layer.UnitCount}\n" +
                    $"Chunks: {chunks.Count}\n" +
                    $"Vertices: {totalVertices}\n" +
                    $"Triangles: {totalTriangles}",
                    this
                );


                return layerObject;
            }
            catch
            {
                DestroyGameObject(
                    layerObject
                );

                throw;
            }
        }


        // =========================================================
        // CHUNK CREATION
        // =========================================================

        private SpatialMeshChunk BuildChunk(
            SpatialLayer layer,
            IReadOnlyList<SpatialUnit> orderedUnits,
            int startIndex,
            int endIndexExclusive,
            int chunkIndex,
            Transform layerTransform
        )
        {
            if (startIndex < 0 ||
                endIndexExclusive > orderedUnits.Count ||
                startIndex >= endIndexExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startIndex),
                    "Invalid spatial-unit chunk range."
                );
            }


            int unitCount =
                endIndexExclusive -
                startIndex;


            var builder =
                new ProceduralPolygonMeshBuilder(
                    spatialReferenceManager,
                    unitCount
                );


            for (
                int i = startIndex;
                i < endIndexExclusive;
                i++
            )
            {
                SpatialUnit unit =
                    orderedUnits[i];


                builder.AddUnit(
                    unit
                );
            }


            string meshName =
                $"{layer.Id}_chunk_{chunkIndex:D3}";


            Mesh mesh =
                builder.BuildMesh(
                    meshName
                );


            GameObject chunkObject =
                new GameObject(
                    meshName
                );


            Transform chunkTransform =
                chunkObject.transform;


            chunkTransform.SetParent(
                layerTransform,
                false
            );

            chunkTransform.localPosition =
                Vector3.zero;

            chunkTransform.localRotation =
                Quaternion.identity;

            chunkTransform.localScale =
                Vector3.one;


            SpatialMeshChunk chunk =
                chunkObject.AddComponent<SpatialMeshChunk>();


            Material material =
                ResolveMaterial();


            chunk.Initialize(
                layer.Id,
                chunkIndex,
                mesh,
                builder.UnitRanges,
                builder.TriangleUnitIds,
                material,
                enableMeshColliders,
                true
            );


            ConfigureRendererForAnalytics(
                chunk.MeshRenderer
            );


            return chunk;
        }


        // =========================================================
        // SPATIAL CHUNK ORDERING
        // =========================================================

        /// <summary>
        /// Orders units using a Morton / Z-order curve.
        ///
        /// Sequential units are therefore usually geographically
        /// close to one another.
        ///
        /// This makes each render chunk spatially coherent and
        /// improves frustum culling compared with arbitrary
        /// source-file ordering.
        /// </summary>
        private static List<SpatialUnit> OrderUnitsSpatially(
            SpatialLayer layer
        )
        {
            var ordered =
                new List<SpatialUnit>(
                    layer.Units
                );


            SpatialBounds bounds =
                layer.Bounds;


            double width =
                bounds.Width;

            double depth =
                bounds.Depth;


            ordered.Sort(
                (a, b) =>
                {
                    uint mortonA =
                        CalculateMortonCode(
                            a.Centroid.Easting,
                            a.Centroid.Northing,
                            bounds.MinEasting,
                            bounds.MinNorthing,
                            width,
                            depth
                        );


                    uint mortonB =
                        CalculateMortonCode(
                            b.Centroid.Easting,
                            b.Centroid.Northing,
                            bounds.MinEasting,
                            bounds.MinNorthing,
                            width,
                            depth
                        );


                    int comparison =
                        mortonA.CompareTo(
                            mortonB
                        );


                    if (comparison != 0)
                    {
                        return comparison;
                    }


                    return string.Compare(
                        a.Id,
                        b.Id,
                        StringComparison.Ordinal
                    );
                }
            );


            return ordered;
        }


        private static uint CalculateMortonCode(
            double easting,
            double northing,
            double minEasting,
            double minNorthing,
            double width,
            double depth
        )
        {
            double normalizedX =
                width > 0.0
                    ? (easting - minEasting) /
                      width
                    : 0.0;


            double normalizedY =
                depth > 0.0
                    ? (northing - minNorthing) /
                      depth
                    : 0.0;


            normalizedX =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        normalizedX
                    )
                );


            normalizedY =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        normalizedY
                    )
                );


            uint x =
                (uint)Math.Round(
                    normalizedX *
                    ushort.MaxValue
                );


            uint y =
                (uint)Math.Round(
                    normalizedY *
                    ushort.MaxValue
                );


            return
                InterleaveBits16(x) |
                (InterleaveBits16(y) << 1);
        }


        private static uint InterleaveBits16(
            uint value
        )
        {
            value &=
                0x0000FFFF;


            value =
                (value |
                 value << 8) &
                0x00FF00FF;


            value =
                (value |
                 value << 4) &
                0x0F0F0F0F;


            value =
                (value |
                 value << 2) &
                0x33333333;


            value =
                (value |
                 value << 1) &
                0x55555555;


            return value;
        }


        // =========================================================
        // VISIBILITY
        // =========================================================

        public bool SetLayerVisible(
            string layerId,
            bool visible
        )
        {
            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                return false;
            }


            if (!renderedLayers.TryGetValue(
                    layerId.Trim(),
                    out RenderedLayer layer
                ))
            {
                return false;
            }


            layer.Root.SetActive(
                visible
            );


            return true;
        }


        public bool IsLayerRendered(
            string layerId
        )
        {
            return
                !string.IsNullOrWhiteSpace(
                    layerId
                ) &&
                renderedLayers.ContainsKey(
                    layerId.Trim()
                );
        }


        public bool TryGetRenderedLayerRoot(
            string layerId,
            out GameObject root
        )
        {
            root = null;


            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                return false;
            }


            if (!renderedLayers.TryGetValue(
                    layerId.Trim(),
                    out RenderedLayer layer
                ))
            {
                return false;
            }


            root =
                layer.Root;


            return true;
        }


        // =========================================================
        // COLLIDERS
        // =========================================================

        public bool SetLayerCollidersEnabled(
            string layerId,
            bool enabled
        )
        {
            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                return false;
            }


            if (!renderedLayers.TryGetValue(
                    layerId.Trim(),
                    out RenderedLayer layer
                ))
            {
                return false;
            }


            foreach (
                SpatialMeshChunk chunk
                in layer.Chunks
            )
            {
                chunk.SetColliderEnabled(
                    enabled
                );
            }


            return true;
        }


        // =========================================================
        // REMOVE / CLEAR
        // =========================================================

        public bool RemoveRenderedLayer(
            string layerId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    layerId
                ))
            {
                return false;
            }


            string normalizedId =
                layerId.Trim();


            if (!renderedLayers.TryGetValue(
                    normalizedId,
                    out RenderedLayer renderedLayer
                ))
            {
                return false;
            }


            renderedLayers.Remove(
                normalizedId
            );


            DestroyGameObject(
                renderedLayer.Root
            );


            return true;
        }


        public void RemoveAllRenderedLayers()
        {
            var roots =
                new List<GameObject>(
                    renderedLayers.Count
                );


            foreach (
                RenderedLayer layer
                in renderedLayers.Values
            )
            {
                roots.Add(
                    layer.Root
                );
            }


            renderedLayers.Clear();


            foreach (
                GameObject root
                in roots
            )
            {
                DestroyGameObject(
                    root
                );
            }
        }


        // =========================================================
        // MATERIAL
        // =========================================================

        private Material ResolveMaterial()
        {
            if (spatialLayerMaterial != null)
            {
                return spatialLayerMaterial;
            }


            if (fallbackMaterial != null)
            {
                return fallbackMaterial;
            }


            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit"
                );


            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Unlit/Color"
                    );
            }


            if (shader == null)
            {
                throw new InvalidOperationException(
                    "GeometryManager could not find an Unlit shader. " +
                    "Assign a material explicitly in the Inspector."
                );
            }


            fallbackMaterial =
                new Material(
                    shader
                );


            fallbackMaterial.name =
                "RuntimeSpatialLayerMaterial";


            ownsFallbackMaterial =
                true;


            return fallbackMaterial;
        }


        private static void ConfigureRendererForAnalytics(
            MeshRenderer renderer
        )
        {
            if (renderer == null)
            {
                return;
            }


            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            renderer.lightProbeUsage =
                LightProbeUsage.Off;

            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.Off;
        }


        // =========================================================
        // DEPENDENCIES / ROOTS
        // =========================================================

        private void ResolveDependencies()
        {
            if (spatialLayerManager == null)
            {
                spatialLayerManager =
                    FindFirstObjectByType<SpatialLayerManager>();
            }


            if (spatialReferenceManager == null)
            {
                spatialReferenceManager =
                    FindFirstObjectByType<SpatialReferenceManager>();
            }
        }


        private void ResolveRenderParent()
        {
            if (renderParent != null)
            {
                return;
            }


            GameObject cityRoot =
                GameObject.Find(
                    "CityRoot"
                );


            if (cityRoot == null)
            {
                throw new InvalidOperationException(
                    "GeometryManager requires a render parent. " +
                    "Assign Render Parent in the Inspector or create " +
                    "a scene GameObject named 'CityRoot'."
                );
            }


            renderParent =
                cityRoot.transform;
        }


        private void CreateRuntimeRoot()
        {
            if (runtimeSpatialLayersRoot != null)
            {
                return;
            }


            GameObject root =
                new GameObject(
                    "SpatialLayers"
                );


            runtimeSpatialLayersRoot =
                root.transform;


            runtimeSpatialLayersRoot.SetParent(
                renderParent,
                false
            );


            runtimeSpatialLayersRoot.localPosition =
                Vector3.zero;

            runtimeSpatialLayersRoot.localRotation =
                Quaternion.identity;

            runtimeSpatialLayersRoot.localScale =
                Vector3.one;
        }


        // =========================================================
        // VALIDATION
        // =========================================================

        private static void ValidateLayerForProceduralRendering(
            SpatialLayer layer
        )
        {
            switch (
                layer.Definition.GeometryType
            )
            {
                case SpatialGeometryType.Polygon:
                case SpatialGeometryType.MultiPolygon:
                    break;


                default:
                    throw new NotSupportedException(
                        $"GeometryManager's current procedural " +
                        $"provider cannot render geometry type " +
                        $"'{layer.Definition.GeometryType}' for " +
                        $"layer '{layer.Id}'."
                    );
            }


            switch (
                layer.Definition.GeometryMode
            )
            {
                case SpatialGeometryMode.Procedural:
                case SpatialGeometryMode.Hybrid:
                    break;


                case SpatialGeometryMode.Precomputed:
                    throw new NotSupportedException(
                        $"Spatial layer '{layer.Id}' is configured " +
                        $"for Precomputed geometry. A precomputed " +
                        $"mesh provider must be used for this layer."
                    );


                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(
                            layer.Definition.GeometryMode
                        )
                    );
            }
        }


        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "GeometryManager has not finished initialization."
                );
            }
        }


        private static void DestroyGameObject(
            GameObject gameObject
        )
        {
            if (gameObject == null)
            {
                return;
            }


            if (Application.isPlaying)
            {
                Destroy(
                    gameObject
                );
            }
            else
            {
                DestroyImmediate(
                    gameObject
                );
            }
        }


        // =========================================================
        // RUNTIME LAYER RECORD
        // =========================================================

        private sealed class RenderedLayer
        {
            public SpatialLayer SpatialLayer
            {
                get;
            }


            public GameObject Root
            {
                get;
            }


            public IReadOnlyList<SpatialMeshChunk>
                Chunks
            {
                get;
            }


            public RenderedLayer(
                SpatialLayer spatialLayer,
                GameObject root,
                IReadOnlyList<SpatialMeshChunk> chunks
            )
            {
                SpatialLayer =
                    spatialLayer
                    ?? throw new ArgumentNullException(
                        nameof(spatialLayer)
                    );


                Root =
                    root
                    ?? throw new ArgumentNullException(
                        nameof(root)
                    );


                Chunks =
                    chunks
                    ?? throw new ArgumentNullException(
                        nameof(chunks)
                    );
            }
        }
    }
}