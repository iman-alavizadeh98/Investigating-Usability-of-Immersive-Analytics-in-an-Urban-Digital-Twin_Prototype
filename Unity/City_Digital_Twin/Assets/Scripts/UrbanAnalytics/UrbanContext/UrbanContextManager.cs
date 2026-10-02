using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Associations;
using UrbanAnalytics.Core;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.Spatial.Geometry;

namespace UrbanAnalytics.UrbanContext
{
    /// <summary>
    /// Loads physical city context independently from analytical
    /// visualization geometry.
    ///
    /// Current context:
    /// - buildings
    ///
    /// Buildings are:
    /// - spatially aligned using SpatialReferenceManager;
    /// - batched for performance;
    /// - semantically identifiable;
    /// - associated to Ruta units;
    /// - independent from Ruta extrusion height.
    /// </summary>
    public sealed class UrbanContextManager :
        MonoBehaviour
    {
        // =========================================================
        // DEPENDENCIES
        // =========================================================

        [Header("Dependencies")]
        [SerializeField]
        private ProjectManager projectManager;

        [SerializeField]
        private SpatialReferenceManager
            spatialReferenceManager;

        [SerializeField]
        private AssociationManager
            associationManager;

        [Tooltip(
            "Optional. Used to align the building base with " +
            "the analytical surface plane."
        )]
        [SerializeField]
        private GeometryManager
            geometryManager;

        [Tooltip(
            "Usually CityRoot."
        )]
        [SerializeField]
        private Transform renderParent;


        // =========================================================
        // BUILDING SOURCE
        // =========================================================

        [Header("Buildings")]
        [SerializeField]
        private bool loadBuildingsOnStart =
            true;

        [Tooltip(
            "Path relative to StreamingAssets."
        )]
        [SerializeField]
        private string buildingsBinaryPath =
            "buildings_runtime_ruta.bin";

        [SerializeField]
        private string buildingToRutaAssociationId =
            "buildings_to_ruta";


        [Tooltip(
            "The existing GBLD v2 binary stores X/Z " +
            "relative to its header origin."
        )]
        [SerializeField]
        private bool coordinatesRelativeToHeaderOrigin =
            true;


        [Tooltip(
            "Legacy loader ignored ground_z. Keep false " +
            "until ground elevation is validated."
        )]
        [SerializeField]
        private bool useGroundElevationFromBinary =
            false;


        [SerializeField]
        private string fallbackRutaLayerId =
            "ruta_250";


        // =========================================================
        // RENDERING
        // =========================================================

        [Header("Building Rendering")]
        [SerializeField]
        private Material buildingMaterial;

        [SerializeField]
        [Min(1)]
        private int buildingsPerChunk =
            750;

        [SerializeField]
        private bool enableBuildingColliders =
            false;

        [Tooltip(
            "Lift the Buildings root by GeometryManager's " +
            "Surface Y Offset so buildings stand on the " +
            "analytical surface instead of starting below it. " +
            "Surface-following lifts are measured from that plane."
        )]
        [SerializeField]
        private bool alignBaseWithAnalyticalSurface =
            true;

        [SerializeField]
        [Min(1)]
        private int chunksBeforeYield =
            1;


        // =========================================================
        // RUNTIME
        // =========================================================

        private readonly List<BuildingMeshChunk>
            buildingChunks =
                new List<BuildingMeshChunk>();


        private CancellationTokenSource
            lifetimeCancellation;


        private Transform urbanContextRoot;

        private Transform buildingsRoot;


        private Material fallbackBuildingMaterial;

        private bool ownsFallbackBuildingMaterial;


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


        public bool AreBuildingsLoaded
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


        public int BuildingCount
        {
            get;
            private set;
        }


        public int MatchedBuildingCount
        {
            get;
            private set;
        }


        public int UnmatchedBuildingCount =>
            Math.Max(
                0,
                BuildingCount -
                MatchedBuildingCount
            );


        public IReadOnlyList<BuildingMeshChunk>
            BuildingChunks =>
                buildingChunks;


        public GameObject BuildingsRoot =>
            buildingsRoot != null
                ? buildingsRoot.gameObject
                : null;


        public string BuildingToRutaAssociationId =>
            buildingToRutaAssociationId;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            ResolveDependencies();


            if (projectManager == null)
            {
                FailDependency(
                    "ProjectManager"
                );

                return;
            }


            if (spatialReferenceManager == null)
            {
                FailDependency(
                    "SpatialReferenceManager"
                );

                return;
            }


            if (associationManager == null)
            {
                FailDependency(
                    "AssociationManager"
                );

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

                lifetimeCancellation =
                    null;
            }


            buildingChunks.Clear();


            if (ownsFallbackBuildingMaterial &&
                fallbackBuildingMaterial != null)
            {
                Destroy(
                    fallbackBuildingMaterial
                );
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


            IsInitializing =
                true;

            LastError =
                null;


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
                        "ProjectManager failed to load " +
                        "the project manifest."
                    );
                }


                if (!spatialReferenceManager.IsReady)
                {
                    throw new InvalidOperationException(
                        "SpatialReferenceManager is not ready."
                    );
                }


                ResolveRenderParent();

                CreateRuntimeRoots();


                IsInitialized =
                    true;


                if (loadBuildingsOnStart)
                {
                    await LoadBuildingsAsync(
                        cancellationToken
                    );
                }


                Debug.Log(
                    $"UrbanContextManager initialized.\n" +
                    $"Buildings loaded: {AreBuildingsLoaded}\n" +
                    $"Buildings: {BuildingCount}\n" +
                    $"Chunks: {buildingChunks.Count}",
                    this
                );
            }
            catch (OperationCanceledException)
            {
                // Normal during shutdown.
            }
            catch (Exception exception)
            {
                LastError =
                    exception.Message;

                IsInitialized =
                    false;


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
        // BUILDING LOAD
        // =========================================================

        public async Task LoadBuildingsAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (AreBuildingsLoaded)
            {
                return;
            }


            byte[] bytes =
                await projectManager
                    .AssetReader
                    .ReadBytesAsync(
                        buildingsBinaryPath,
                        cancellationToken
                    );


            cancellationToken
                .ThrowIfCancellationRequested();


            associationManager.ClearAssociation(
                buildingToRutaAssociationId
            );


            ClearBuildingObjects();


            using var stream =
                new MemoryStream(
                    bytes,
                    false
                );


            using var reader =
                new BinaryReader(
                    stream,
                    Encoding.UTF8,
                    false
                );


            // =====================================================
            // HEADER
            // =====================================================

            string magic =
                Encoding.ASCII.GetString(
                    reader.ReadBytes(4)
                );


            if (magic != "GBLD")
            {
                throw new InvalidDataException(
                    $"Invalid building binary magic " +
                    $"'{magic}'. Expected GBLD."
                );
            }


            uint version =
                reader.ReadUInt32();


            if (version != 2)
            {
                throw new InvalidDataException(
                    $"Unsupported building binary version " +
                    $"{version}. Expected 2."
                );
            }


            uint declaredBuildingCount =
                reader.ReadUInt32();


            uint rutaKeyCount =
                reader.ReadUInt32();


            double headerOriginEasting =
                reader.ReadDouble();


            double headerOriginNorthing =
                reader.ReadDouble();


            string[] rutaKeys =
                ReadRutaKeys(
                    reader,
                    rutaKeyCount
                );


            // =====================================================
            // CHUNK BUFFERS
            // =====================================================

            var vertices =
                new List<Vector3>();


            var triangles =
                new List<int>();


            var colors =
                new List<Color32>();


            var ranges =
                new List<BuildingMeshUnitRange>(
                    buildingsPerChunk
                );


            int chunkIndex =
                0;


            int chunksSinceYield =
                0;


            int loadedBuildings =
                0;


            int matchedBuildings =
                0;


            // =====================================================
            // BUILDINGS
            // =====================================================

            for (
                uint recordIndex = 0;
                recordIndex <
                declaredBuildingCount;
                recordIndex++
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                uint rawBuildingId =
                    reader.ReadUInt32();


                float heightMeters =
                    reader.ReadSingle();


                float groundZ =
                    reader.ReadSingle();


                int rutaIndex =
                    reader.ReadInt32();


                ushort partCount =
                    reader.ReadUInt16();


                string buildingId =
                    $"building:{rawBuildingId}";


                string associatedSpatialUnitId =
                    ResolveRutaSemanticId(
                        rutaKeys,
                        rutaIndex
                    );


                int vertexStart =
                    vertices.Count;


                int triangleStart =
                    triangles.Count / 3;


                // -------------------------------------------------
                // PARTS
                // -------------------------------------------------

                for (
                    int partIndex = 0;
                    partIndex < partCount;
                    partIndex++
                )
                {
                    uint vertexCount =
                        reader.ReadUInt32();


                    if (vertexCount >
                        1000000)
                    {
                        throw new InvalidDataException(
                            $"Building '{buildingId}' has " +
                            $"implausible vertex count " +
                            $"{vertexCount}."
                        );
                    }


                    var sourceCoordinates =
                        new List<SpatialCoordinate>(
                            (int)vertexCount
                        );


                    for (
                        uint vertexIndex = 0;
                        vertexIndex <
                        vertexCount;
                        vertexIndex++
                    )
                    {
                        float x =
                            reader.ReadSingle();


                        float z =
                            reader.ReadSingle();


                        double easting =
                            coordinatesRelativeToHeaderOrigin
                                ? headerOriginEasting +
                                  x
                                : x;


                        double northing =
                            coordinatesRelativeToHeaderOrigin
                                ? headerOriginNorthing +
                                  z
                                : z;


                        sourceCoordinates.Add(
                            new SpatialCoordinate(
                                easting,
                                northing
                            )
                        );
                    }


                    if (sourceCoordinates.Count <
                        3)
                    {
                        continue;
                    }


                    try
                    {
                        var ring =
                            new PolygonRing(
                                sourceCoordinates
                            );


                        AppendBuildingPart(
                            ring,
                            groundZ,
                            heightMeters,
                            vertices,
                            triangles,
                            colors
                        );
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            $"Skipped invalid geometry for " +
                            $"'{buildingId}', part " +
                            $"{partIndex}: " +
                            $"{exception.Message}",
                            this
                        );
                    }
                }


                // -------------------------------------------------
                // SEMANTIC RANGE
                // -------------------------------------------------

                int addedVertexCount =
                    vertices.Count -
                    vertexStart;


                int addedTriangleCount =
                    triangles.Count / 3 -
                    triangleStart;


                if (addedVertexCount <= 0 ||
                    addedTriangleCount <= 0)
                {
                    continue;
                }


                ranges.Add(
                    new BuildingMeshUnitRange(
                        buildingId,
                        associatedSpatialUnitId,
                        vertexStart,
                        addedVertexCount,
                        triangleStart,
                        addedTriangleCount,
                        heightMeters
                    )
                );


                loadedBuildings++;


                if (!string.IsNullOrWhiteSpace(
                        associatedSpatialUnitId
                    ))
                {
                    associationManager.Register(
                        buildingToRutaAssociationId,
                        buildingId,
                        associatedSpatialUnitId
                    );


                    matchedBuildings++;
                }


                // -------------------------------------------------
                // FINISH CHUNK
                // -------------------------------------------------

                if (ranges.Count >=
                    buildingsPerChunk)
                {
                    CreateBuildingChunk(
                        chunkIndex,
                        vertices,
                        triangles,
                        colors,
                        ranges
                    );


                    chunkIndex++;

                    chunksSinceYield++;


                    vertices.Clear();

                    triangles.Clear();

                    colors.Clear();

                    ranges.Clear();


                    if (chunksSinceYield >=
                        chunksBeforeYield)
                    {
                        chunksSinceYield =
                            0;

                        await Task.Yield();
                    }
                }
            }


            // Final partial chunk.
            if (ranges.Count > 0)
            {
                CreateBuildingChunk(
                    chunkIndex,
                    vertices,
                    triangles,
                    colors,
                    ranges
                );
            }


            BuildingCount =
                loadedBuildings;


            MatchedBuildingCount =
                matchedBuildings;


            AreBuildingsLoaded =
                true;


            Debug.Log(
                $"Buildings loaded into UrbanContext:\n" +
                $"Binary: {buildingsBinaryPath}\n" +
                $"Declared records: " +
                $"{declaredBuildingCount}\n" +
                $"Rendered buildings: " +
                $"{BuildingCount}\n" +
                $"Associated buildings: " +
                $"{MatchedBuildingCount}\n" +
                $"Unmatched buildings: " +
                $"{UnmatchedBuildingCount}\n" +
                $"Chunks: " +
                $"{buildingChunks.Count}\n" +
                $"Association: " +
                $"{buildingToRutaAssociationId}\n" +
                $"Binary origin: " +
                $"{headerOriginEasting}, " +
                $"{headerOriginNorthing}",
                this
            );
        }


        // =========================================================
        // RUTA KEYS
        // =========================================================

        private static string[] ReadRutaKeys(
            BinaryReader reader,
            uint rutaKeyCount
        )
        {
            string[] keys =
                new string[
                    (int)rutaKeyCount
                ];


            for (
                int i = 0;
                i < keys.Length;
                i++
            )
            {
                ushort byteLength =
                    reader.ReadUInt16();


                byte[] bytes =
                    reader.ReadBytes(
                        byteLength
                    );


                if (bytes.Length !=
                    byteLength)
                {
                    throw new EndOfStreamException(
                        "Building binary ended while " +
                        "reading Ruta keys."
                    );
                }


                keys[i] =
                    Encoding.UTF8.GetString(
                        bytes
                    );
            }


            return keys;
        }


        // =========================================================
        // BUILD GEOMETRY
        // =========================================================

        private void AppendBuildingPart(
            PolygonRing ring,
            float groundZ,
            float heightMeters,
            List<Vector3> vertices,
            List<int> triangles,
            List<Color32> colors
        )
        {
            var polygon =
                new PolygonGeometry(
                    ring
                );


            PolygonTriangulationResult triangulation =
                PolygonTriangulator.Triangulate(
                    polygon
                );


            float height =
                spatialReferenceManager.ScaleDistance(
                    Math.Max(
                        0.0,
                        heightMeters
                    )
                );


            double baseElevation =
                useGroundElevationFromBinary
                    ? groundZ
                    : 0.0;


            // -----------------------------------------------------
            // ROOF
            // -----------------------------------------------------

            int roofOffset =
                vertices.Count;


            foreach (
                TriangulatedVertex2D sourceVertex
                in triangulation.Vertices
            )
            {
                Vector3 top =
                    spatialReferenceManager.ToUnity(
                        sourceVertex.Easting,
                        sourceVertex.Northing,
                        baseElevation
                    );


                if (!useGroundElevationFromBinary)
                {
                    top.y =
                        0.0f;
                }


                top.y +=
                    height;


                vertices.Add(
                    top
                );


                colors.Add(
                    Color.white
                );
            }


            foreach (
                int localIndex
                in triangulation.Indices
            )
            {
                triangles.Add(
                    roofOffset +
                    localIndex
                );
            }


            // -----------------------------------------------------
            // WALLS
            // -----------------------------------------------------

            IReadOnlyList<SpatialCoordinate>
                ringCoordinates =
                    ring.Coordinates;


            // The wall winding below faces outward for
            // counter-clockwise rings. Clockwise rings must be
            // flipped, otherwise their walls face inward and are
            // back-face culled from outside.
            bool flipWalls =
                ring.Orientation ==
                RingOrientation.Clockwise;


            for (
                int i = 0;
                i < ringCoordinates.Count;
                i++
            )
            {
                SpatialCoordinate a =
                    ringCoordinates[i];


                SpatialCoordinate b =
                    ringCoordinates[
                        (i + 1) %
                        ringCoordinates.Count
                    ];


                Vector3 baseA =
                    spatialReferenceManager.ToUnity(
                        a.Easting,
                        a.Northing,
                        baseElevation
                    );


                Vector3 baseB =
                    spatialReferenceManager.ToUnity(
                        b.Easting,
                        b.Northing,
                        baseElevation
                    );


                if (!useGroundElevationFromBinary)
                {
                    baseA.y =
                        0.0f;

                    baseB.y =
                        0.0f;
                }


                Vector3 topA =
                    baseA +
                    Vector3.up *
                    height;


                Vector3 topB =
                    baseB +
                    Vector3.up *
                    height;


                int offset =
                    vertices.Count;


                vertices.Add(
                    baseA
                );

                vertices.Add(
                    baseB
                );

                vertices.Add(
                    topA
                );

                vertices.Add(
                    topB
                );


                colors.Add(
                    Color.white
                );

                colors.Add(
                    Color.white
                );

                colors.Add(
                    Color.white
                );

                colors.Add(
                    Color.white
                );


                // Outward winding for counter-clockwise rings
                // (same as the previous BuildingRuntimeLoader);
                // swapped for clockwise rings.
                int second =
                    flipWalls
                        ? offset + 1
                        : offset + 2;

                int third =
                    flipWalls
                        ? offset + 2
                        : offset + 1;


                triangles.Add(
                    offset
                );

                triangles.Add(
                    second
                );

                triangles.Add(
                    third
                );


                triangles.Add(
                    offset + 1
                );

                triangles.Add(
                    flipWalls
                        ? offset + 3
                        : offset + 2
                );

                triangles.Add(
                    flipWalls
                        ? offset + 2
                        : offset + 3
                );
            }
        }


        // =========================================================
        // CREATE CHUNK
        // =========================================================

        private void CreateBuildingChunk(
            int chunkIndex,
            List<Vector3> vertices,
            List<int> triangles,
            List<Color32> colors,
            List<BuildingMeshUnitRange> ranges
        )
        {
            if (vertices.Count == 0 ||
                triangles.Count == 0 ||
                ranges.Count == 0)
            {
                return;
            }


            var mesh =
                new Mesh
                {
                    name =
                        $"buildings_chunk_" +
                        $"{chunkIndex:D3}"
                };


            mesh.indexFormat =
                vertices.Count >
                ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;


            mesh.SetVertices(
                vertices
            );


            mesh.SetTriangles(
                triangles,
                0,
                false
            );


            mesh.SetColors(
                colors
            );


            mesh.RecalculateNormals();

            mesh.RecalculateBounds();


            GameObject chunkObject =
                new GameObject(
                    mesh.name
                );


            chunkObject
                .transform
                .SetParent(
                    buildingsRoot,
                    false
                );


            BuildingMeshChunk chunk =
                chunkObject.AddComponent<
                    BuildingMeshChunk
                >();


            chunk.Initialize(
                chunkIndex,
                mesh,
                ranges,
                ResolveBuildingMaterial(),
                enableBuildingColliders,
                true
            );


            ConfigureRenderer(
                chunk.MeshRenderer
            );


            buildingChunks.Add(
                chunk
            );
        }


        private static void ConfigureRenderer(
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
        // RUTA SEMANTIC IDS
        // =========================================================

        private string ResolveRutaSemanticId(
            string[] rutaKeys,
            int rutaIndex
        )
        {
            if (rutaIndex < 0 ||
                rutaIndex >=
                rutaKeys.Length)
            {
                return null;
            }


            string raw =
                rutaKeys[rutaIndex];


            if (string.IsNullOrWhiteSpace(
                    raw
                ))
            {
                return null;
            }


            raw =
                raw.Trim();


            // Already new-format semantic ID.
            if (raw.StartsWith(
                    "ruta_",
                    StringComparison.Ordinal
                ) &&
                raw.Contains(":"))
            {
                return raw;
            }


            // Legacy format:
            //
            // 250_3175006390000
            // ->
            // ruta_250:3175006390000
            int separator =
                raw.IndexOf('_');


            if (separator > 0 &&
                separator <
                raw.Length - 1)
            {
                string prefix =
                    raw.Substring(
                        0,
                        separator
                    );


                string rawId =
                    raw.Substring(
                        separator + 1
                    );


                if (int.TryParse(
                        prefix,
                        out int gridSize
                    ))
                {
                    return
                        $"ruta_{gridSize}:" +
                        $"{rawId}";
                }
            }


            // If already some other semantic ID,
            // preserve it.
            if (raw.Contains(":"))
            {
                return raw;
            }


            // Safest fallback for raw numeric IDs.
            return
                $"{fallbackRutaLayerId}:" +
                $"{raw}";
        }


        // =========================================================
        // ROOTS
        // =========================================================

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
                    "UrbanContextManager requires " +
                    "CityRoot as its render parent."
                );
            }


            renderParent =
                cityRoot.transform;
        }


        private void CreateRuntimeRoots()
        {
            Transform existingContext =
                renderParent.Find(
                    "UrbanContext"
                );


            if (existingContext != null)
            {
                urbanContextRoot =
                    existingContext;
            }
            else
            {
                GameObject contextObject =
                    new GameObject(
                        "UrbanContext"
                    );


                contextObject
                    .transform
                    .SetParent(
                        renderParent,
                        false
                    );


                urbanContextRoot =
                    contextObject.transform;
            }


            Transform existingBuildings =
                urbanContextRoot.Find(
                    "Buildings"
                );


            if (existingBuildings != null)
            {
                buildingsRoot =
                    existingBuildings;
            }
            else
            {
                GameObject buildingsObject =
                    new GameObject(
                        "Buildings"
                    );


                buildingsObject
                    .transform
                    .SetParent(
                        urbanContextRoot,
                        false
                    );


                buildingsRoot =
                    buildingsObject.transform;
            }


            buildingsRoot.localPosition =
                new Vector3(
                    0.0f,
                    ResolveBuildingBaseYOffset(),
                    0.0f
                );
        }


        /// <summary>
        /// GeometryManager raises every analytical layer by
        /// Surface Y Offset (default 0.002 = 2 m). Buildings
        /// built at y = 0 would start below that plane: the
        /// lowest 2 m are hidden and 2 m roofs z-fight with it.
        /// </summary>
        private float ResolveBuildingBaseYOffset()
        {
            if (!alignBaseWithAnalyticalSurface)
            {
                return 0.0f;
            }


            if (geometryManager == null)
            {
                Debug.LogWarning(
                    "UrbanContextManager: no GeometryManager " +
                    "found, so buildings are not aligned with " +
                    "the analytical surface.",
                    this
                );

                return 0.0f;
            }


            return geometryManager.SurfaceYOffset;
        }


        // =========================================================
        // MATERIAL
        // =========================================================

        private Material ResolveBuildingMaterial()
        {
            if (buildingMaterial != null)
            {
                return buildingMaterial;
            }


            if (fallbackBuildingMaterial != null)
            {
                return fallbackBuildingMaterial;
            }


            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit"
                );


            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Could not find URP Lit shader."
                );
            }


            fallbackBuildingMaterial =
                new Material(
                    shader
                );


            fallbackBuildingMaterial.name =
                "Runtime_Building_Context";


            fallbackBuildingMaterial.color =
                new Color(
                    0.72f,
                    0.72f,
                    0.72f,
                    1.0f
                );


            ownsFallbackBuildingMaterial =
                true;


            return fallbackBuildingMaterial;
        }


        // =========================================================
        // CLEANUP
        // =========================================================

        private void ClearBuildingObjects()
        {
            buildingChunks.Clear();


            BuildingCount =
                0;


            MatchedBuildingCount =
                0;


            AreBuildingsLoaded =
                false;


            if (buildingsRoot == null)
            {
                return;
            }


            for (
                int i =
                    buildingsRoot.childCount - 1;
                i >= 0;
                i--
            )
            {
                Destroy(
                    buildingsRoot
                        .GetChild(i)
                        .gameObject
                );
            }
        }


        // =========================================================
        // DEPENDENCIES
        // =========================================================

        private void ResolveDependencies()
        {
            if (projectManager == null)
            {
                projectManager =
                    FindFirstObjectByType<
                        ProjectManager
                    >();
            }


            if (spatialReferenceManager == null)
            {
                spatialReferenceManager =
                    FindFirstObjectByType<
                        SpatialReferenceManager
                    >();
            }


            if (associationManager == null)
            {
                associationManager =
                    FindFirstObjectByType<
                        AssociationManager
                    >();
            }


            if (geometryManager == null)
            {
                geometryManager =
                    FindFirstObjectByType<
                        GeometryManager
                    >();
            }
        }


        private void FailDependency(
            string dependencyName
        )
        {
            Debug.LogError(
                $"UrbanContextManager could not " +
                $"find {dependencyName}.",
                this
            );


            enabled =
                false;
        }
    }
}