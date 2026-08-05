using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CityDigitalTwin.IO
{
    /// <summary>
    /// Loads a whole mesh-pipeline run into the scene, each group placed at its real
    /// geographic position.
    ///
    /// ── Why placement is needed at all ────────────────────────────────────────
    /// Every PLY is written in a LOCAL frame: the exporter subtracts that group's
    /// own origin from X and Y, so each file starts at (0,0). Load them raw and all
    /// 2211 grid cells pile up on top of each other at the world origin. The real
    /// coordinates live in meshes_manifest.json, one `origin` per group — that is
    /// what this component reads and applies.
    ///
    /// ── The Z subtlety ───────────────────────────────────────────────────────
    /// The exporter rebases X and Y only (builder.py subtracts origin[0]/origin[1]).
    /// Z is left as an ABSOLUTE height. The manifest still reports `origin.z`
    /// (0.5 m in the current runs), but it was never subtracted — so it must NOT be
    /// added back here, or the whole city rises by that amount. Verified against the
    /// 500 m grid run: local mesh z-min == origin.z == 0.5.
    ///
    /// ── Float precision ──────────────────────────────────────────────────────
    /// Raw SWEREF99 TM coordinates are ~300 000 E / 6 400 000 N. A float32 has ~7
    /// significant digits, so at 6.4M metres the spacing between representable
    /// values is ~0.5 m — vertices would visibly snap and z-fight. Everything is
    /// therefore rebased on a scene origin (default: the south-west corner of the
    /// run), keeping coordinates in the 0..31 000 m range where float precision is
    /// centimetre-level. <see cref="SceneOriginEasting"/> records the offset so
    /// scene coordinates can be converted back to EPSG:3006 for analytics.
    /// </summary>
    [AddComponentMenu("City Digital Twin/City Mesh Loader")]
    public class CityMeshLoader : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Path to meshes_manifest.json — absolute, or relative to the Unity " +
                 "project root. This file carries the coordinates; the PLYs alone cannot " +
                 "be placed correctly.\n\nExample:\n" +
                 "../../Processed_data/Gothenburg/building_meshes_grid_500m/meshes_manifest.json")]
        public string manifestPath =
            "../../Processed_data/Gothenburg/building_meshes_grid_500m_anchored/meshes_manifest.json";

        [Tooltip("Load automatically in Start().")]
        public bool loadOnStart = true;

        [Header("Scope")]
        [Tooltip("0 = load every group. Set a small number (e.g. 50) to try a subset first — " +
                 "the 500 m grid run is 2211 cells.")]
        public int maxGroups = 0;

        [Tooltip("Only load groups whose centre is within this many metres of the run's " +
                 "centre. 0 = no limit. Useful for working on one part of the city.")]
        public float radiusFilterMeters = 0f;

        [Header("Placement")]
        [Tooltip("Rebase all coordinates on a world anchor. Keep ON: raw SWEREF99 " +
                 "values (~6.4M m) exceed float32 precision and cause visible vertex " +
                 "snapping and z-fighting.\n\n" +
                 "Uses the manifest's frozen lattice anchor when present, so world " +
                 "coordinates stay identical across regenerated runs; otherwise falls " +
                 "back to this run's south-west corner.")]
        public bool rebaseOnSouthWestCorner = true;

        [Header("Performance")]
        [Tooltip("Merge groups into combined meshes instead of one GameObject each. " +
                 "Strongly recommended at grid scale: 2211 separate renderers is 2211 " +
                 "draw calls, while merging yields roughly a dozen. Turn OFF only when " +
                 "each cell must stay individually selectable.")]
        public bool combineMeshes = true;

        [Tooltip("Max vertices per combined mesh. 2.67M total verts across the 500m grid, " +
                 "so ~262k gives about a dozen meshes — each still well inside the UInt32 " +
                 "limit while keeping chunks small enough to frustum-cull usefully.")]
        public int verticesPerCombinedMesh = 262144;

        [Tooltip("Add MeshColliders. Costly at city scale — leave off until picking is needed.")]
        public bool addMeshColliders = false;

        [Header("Rendering")]
        public Material material;

        /// <summary>EPSG:3006 easting that scene X = 0 corresponds to.</summary>
        public double SceneOriginEasting { get; private set; }

        /// <summary>EPSG:3006 northing that scene Z = 0 corresponds to.</summary>
        public double SceneOriginNorthing { get; private set; }

        /// <summary>CRS reported by the manifest, e.g. "EPSG:3006".</summary>
        public string Crs { get; private set; }

        /// <summary>
        /// True when the scene origin came from the manifest's frozen lattice anchor
        /// rather than this run's own extent. Only then are world coordinates stable
        /// across regenerated data, and therefore comparable between sessions.
        /// </summary>
        public bool UsingFrozenAnchor { get; private set; }

        public MeshManifest Manifest { get; private set; }

        /// <summary>Raised when the whole run has finished loading.</summary>
        public event Action<CityMeshLoader> LoadCompleted;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Start()
        {
            if (loadOnStart) StartCoroutine(LoadCityAsync());
        }

        /// <summary>
        /// Load and place the whole run. Parsing happens on worker threads; mesh
        /// creation is spread across frames so the editor stays responsive.
        /// </summary>
        public IEnumerator LoadCityAsync()
        {
            var sw = Stopwatch.StartNew();

            MeshManifest manifest;
            try
            {
                manifest = MeshManifest.Load(manifestPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CityMeshLoader] {e.Message}", this);
                yield break;
            }

            Manifest = manifest;
            Crs = manifest.crs;

            List<MeshGroupEntry> groups = SelectGroups(manifest);
            if (groups.Count == 0)
            {
                Debug.LogWarning("[CityMeshLoader] Manifest contained no groups to load.", this);
                yield break;
            }

            // Establish the scene origin BEFORE any placement, so every group shares it.
            bool frozenAnchor = false;
            Vector2 anchor = rebaseOnSouthWestCorner
                ? manifest.ResolveWorldAnchor(out frozenAnchor)
                : Vector2.zero;
            SceneOriginEasting = anchor.x;
            SceneOriginNorthing = anchor.y;
            UsingFrozenAnchor = frozenAnchor;

            Debug.Log($"[CityMeshLoader] {manifest.strategy}: {groups.Count} of " +
                      $"{manifest.meshes.Count} groups, CRS {manifest.crs}. " +
                      $"Scene origin = ({SceneOriginEasting:F1}, {SceneOriginNorthing:F1}) {manifest.crs} " +
                      $"[{(frozenAnchor ? "frozen lattice anchor" : "run south-west corner")}].");

            if (rebaseOnSouthWestCorner && !frozenAnchor)
            {
                // Without a frozen anchor, every world coordinate shifts when the
                // data is regenerated, so positions logged in one session cannot be
                // compared with another.
                Debug.LogWarning(
                    "[CityMeshLoader] This manifest carries no grid_reference block, so the " +
                    "world origin was derived from this run's own extent and will MOVE if the " +
                    "data is regenerated. Interaction coordinates will not be comparable across " +
                    "runs. Regenerate the meshes to embed the frozen lattice anchor.", this);
            }

            // ---- Parse every PLY on worker threads -------------------------------
            var settings = PlyImportSettings.Default;
            var paths = new string[groups.Count];
            for (int i = 0; i < groups.Count; i++) paths[i] = manifest.GetPlyPath(groups[i]);

            var parsed = new PlyMeshData[groups.Count];
            var errors = new Exception[groups.Count];

            Task parseTask = Task.Run(() =>
            {
                Parallel.For(0, paths.Length, i =>
                {
                    try
                    {
                        if (paths[i] != null && File.Exists(paths[i]))
                            parsed[i] = PlyParser.Load(paths[i], settings);
                        else
                            errors[i] = new FileNotFoundException("missing", paths[i] ?? "<null>");
                    }
                    catch (Exception e) { errors[i] = e; }
                });
            });

            while (!parseTask.IsCompleted) yield return null;
            if (parseTask.IsFaulted)
            {
                Debug.LogError($"[CityMeshLoader] Parsing failed: {parseTask.Exception?.GetBaseException().Message}", this);
                yield break;
            }

            long parseMs = sw.ElapsedMilliseconds;

            // ---- Place into the scene -------------------------------------------
            IEnumerator build = combineMeshes
                ? BuildCombined(groups, parsed, errors)
                : BuildIndividual(groups, parsed, errors);
            while (build.MoveNext()) yield return build.Current;

            sw.Stop();

            int failed = 0;
            long verts = 0, tris = 0;
            for (int i = 0; i < parsed.Length; i++)
            {
                if (parsed[i] == null) { failed++; continue; }
                verts += parsed[i].VertexCount;
                tris += parsed[i].TriangleCount;
            }

            Debug.Log($"[CityMeshLoader] Done: {groups.Count - failed}/{groups.Count} groups, " +
                      $"{verts:N0} verts, {tris:N0} tris, {_spawned.Count} GameObject(s). " +
                      $"Parse {parseMs} ms, total {sw.ElapsedMilliseconds} ms.", this);

            if (failed > 0)
            {
                int shown = 0;
                for (int i = 0; i < errors.Length && shown < 5; i++)
                {
                    if (errors[i] == null) continue;
                    Debug.LogError($"[CityMeshLoader] {groups[i].group_id}: {errors[i].Message}", this);
                    shown++;
                }
            }

            LoadCompleted?.Invoke(this);
        }

        private List<MeshGroupEntry> SelectGroups(MeshManifest manifest)
        {
            var groups = new List<MeshGroupEntry>();
            foreach (var g in manifest.meshes)
                if (g?.origin != null) groups.Add(g);

            if (radiusFilterMeters > 0f && groups.Count > 0)
            {
                double cx = 0, cy = 0;
                foreach (var g in groups) { cx += g.origin.x; cy += g.origin.y; }
                cx /= groups.Count; cy /= groups.Count;

                var kept = new List<MeshGroupEntry>();
                foreach (var g in groups)
                {
                    double dx = g.origin.x - cx, dy = g.origin.y - cy;
                    if (Math.Sqrt(dx * dx + dy * dy) <= radiusFilterMeters) kept.Add(g);
                }
                groups = kept;
            }

            if (maxGroups > 0 && groups.Count > maxGroups)
                groups.RemoveRange(maxGroups, groups.Count - maxGroups);

            return groups;
        }

        /// <summary>
        /// Scene position for a group: its manifest origin, minus the shared scene
        /// origin, with northing mapped to Unity Z. Y stays 0 — heights are already
        /// absolute inside the mesh (see the class remarks on Z).
        /// </summary>
        private Vector3 PlacementFor(MeshGroupEntry group)
        {
            return new Vector3(
                (float)(group.origin.x - SceneOriginEasting),
                0f,
                (float)(group.origin.y - SceneOriginNorthing));
        }

        private IEnumerator BuildIndividual(List<MeshGroupEntry> groups, PlyMeshData[] parsed,
                                            Exception[] errors)
        {
            Material mat = material != null ? material : DefaultMaterial();
            var frame = Stopwatch.StartNew();

            for (int i = 0; i < groups.Count; i++)
            {
                if (parsed[i] == null) continue;

                var go = new GameObject(groups[i].group_id);
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = PlacementFor(groups[i]);

                Mesh mesh = PlyMeshFactory.CreateMesh(parsed[i], groups[i].group_id);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                if (addMeshColliders) go.AddComponent<MeshCollider>().sharedMesh = mesh;

                // Keeps the group's identity and coordinates queryable at runtime.
                var tag = go.AddComponent<CityMeshGroup>();
                tag.groupId = groups[i].group_id;
                tag.groupName = groups[i].group_name;
                tag.originEasting = groups[i].origin.x;
                tag.originNorthing = groups[i].origin.y;
                tag.crs = groups[i].crs ?? Crs;

                // Lattice address, when the run is grid-like. This is what joins to
                // the population/employment/income layers.
                if (groups[i].cell != null && groups[i].cell.size_m > 0)
                {
                    tag.gridCol = groups[i].cell.col;
                    tag.gridRow = groups[i].cell.row;
                    tag.cellOriginEasting = groups[i].cell.origin_x;
                    tag.cellOriginNorthing = groups[i].cell.origin_y;
                    tag.cellSizeM = groups[i].cell.size_m;
                }

                _spawned.Add(go);

                // Yield periodically rather than per object: 2211 single-object
                // frames would take ~37 s at 60 fps.
                if (frame.ElapsedMilliseconds > 16) { frame.Restart(); yield return null; }
            }
        }

        /// <summary>
        /// Merge groups into a handful of large meshes. At grid scale this is the
        /// difference between 2211 draw calls and roughly a dozen.
        /// </summary>
        private IEnumerator BuildCombined(List<MeshGroupEntry> groups, PlyMeshData[] parsed,
                                          Exception[] errors)
        {
            Material mat = material != null ? material : DefaultMaterial();
            var frame = Stopwatch.StartNew();

            int budget = Mathf.Max(1024, verticesPerCombinedMesh);
            var batch = new List<int>();
            int batchVerts = 0;
            int chunkIndex = 0;

            for (int i = 0; i <= groups.Count; i++)
            {
                bool last = i == groups.Count;

                if (!last)
                {
                    if (parsed[i] == null) continue;

                    // Flush before exceeding the budget, so a chunk never overflows.
                    if (batch.Count > 0 && batchVerts + parsed[i].VertexCount > budget)
                    {
                        CreateChunk(batch, groups, parsed, mat, chunkIndex++);
                        batch.Clear();
                        batchVerts = 0;
                        if (frame.ElapsedMilliseconds > 16) { frame.Restart(); yield return null; }
                    }

                    batch.Add(i);
                    batchVerts += parsed[i].VertexCount;
                }
                else if (batch.Count > 0)
                {
                    CreateChunk(batch, groups, parsed, mat, chunkIndex++);
                }
            }
        }

        private void CreateChunk(List<int> batch, List<MeshGroupEntry> groups, PlyMeshData[] parsed,
                                 Material mat, int chunkIndex)
        {
            int totalVerts = 0, totalTris = 0;
            foreach (int i in batch)
            {
                totalVerts += parsed[i].VertexCount;
                totalTris += parsed[i].Triangles.Length;
            }

            var vertices = new Vector3[totalVerts];
            var normals = new Vector3[totalVerts];
            var triangles = new int[totalTris];
            int vOff = 0, tOff = 0;
            bool anyNormals = false;

            // Chunk-local origin: the first group's placement. Keeps vertex values
            // small within the chunk and lets the transform carry the position.
            Vector3 chunkOrigin = PlacementFor(groups[batch[0]]);

            foreach (int i in batch)
            {
                PlyMeshData d = parsed[i];
                Vector3 offset = PlacementFor(groups[i]) - chunkOrigin;

                for (int v = 0; v < d.Vertices.Length; v++)
                    vertices[vOff + v] = d.Vertices[v] + offset;

                if (d.HasNormals)
                {
                    anyNormals = true;
                    Array.Copy(d.Normals, 0, normals, vOff, d.Normals.Length);
                }

                // Indices are per-file, so shift them into the combined buffer.
                for (int t = 0; t < d.Triangles.Length; t++)
                    triangles[tOff + t] = d.Triangles[t] + vOff;

                vOff += d.Vertices.Length;
                tOff += d.Triangles.Length;
            }

            var mesh = new Mesh
            {
                name = $"city_chunk_{chunkIndex:D3}",
                indexFormat = totalVerts > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            if (anyNormals) mesh.SetNormals(normals); else mesh.RecalculateNormals();

            var go = new GameObject($"city_chunk_{chunkIndex:D3} ({batch.Count} groups)");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = chunkOrigin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (addMeshColliders) go.AddComponent<MeshCollider>().sharedMesh = mesh;

            _spawned.Add(go);
        }

        private static Material DefaultMaterial()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) return rp.defaultMaterial;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(s) { name = "City Fallback Material" };
        }

        /// <summary>Remove everything this loader spawned.</summary>
        public void Clear()
        {
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        // ---- Coordinate conversion, for analytics and logging --------------------

        /// <summary>Scene position -> EPSG:3006 (easting, northing).</summary>
        public (double easting, double northing) SceneToCrs(Vector3 scenePosition)
        {
            Vector3 local = transform.InverseTransformPoint(scenePosition);
            return (local.x + SceneOriginEasting, local.z + SceneOriginNorthing);
        }

        /// <summary>EPSG:3006 (easting, northing) -> scene position. Y is left at 0.</summary>
        public Vector3 CrsToScene(double easting, double northing)
        {
            return transform.TransformPoint(new Vector3(
                (float)(easting - SceneOriginEasting), 0f,
                (float)(northing - SceneOriginNorthing)));
        }
    }

    /// <summary>
    /// Marker left on each loaded group so its identity and real-world coordinates
    /// survive into the runtime — needed for selection, hover metadata and
    /// interaction logging.
    /// </summary>
    public class CityMeshGroup : MonoBehaviour
    {
        public string groupId;
        public string groupName;

        /// <summary>Content bbox corner the mesh was rebased on (placement).</summary>
        public double originEasting;
        public double originNorthing;
        public string crs;

        [Header("Analytical cell")]
        [Tooltip("Lattice column/row. -1 when the run used a non-grid strategy.")]
        public int gridCol = -1;
        public int gridRow = -1;

        [Tooltip("Exact lattice corner — the join key to the cell-attribute layers " +
                 "(cell_attributes_500m.json), NOT the placement origin above.")]
        public double cellOriginEasting;
        public double cellOriginNorthing;
        public int cellSizeM;

        /// <summary>
        /// True when this group is a lattice cell, so its groupId can be used
        /// directly as a key into the cell-attribute data.
        /// </summary>
        public bool HasAnalyticalCell => cellSizeM > 0;
    }
}
