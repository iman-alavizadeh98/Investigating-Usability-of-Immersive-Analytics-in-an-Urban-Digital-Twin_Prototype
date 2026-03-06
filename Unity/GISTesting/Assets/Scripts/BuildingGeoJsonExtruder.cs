using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class BuildingGeoJsonExtruder : MonoBehaviour
{
    public string geoJsonFileName = "buildings_with_heights_1km.geojson";

    [Header("Origin (can be wrong for now)")]
    public double originEasting = 320669.5;
    public double originNorthing = 6399427.0;

    [Header("Placement")]
    public float baseY = 0.0f;
    public float heightScale = 1.0f;
    public float minHeight = 3.0f;

    [Header("Performance")]
    public bool mergeIntoChunks = true;
    public int buildingsPerChunk = 150;

    [Header("Rendering")]
    public Material buildingMaterial;

    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, geoJsonFileName);
        if (!File.Exists(path))
        {
            Debug.LogError("GeoJSON not found: " + path);
            return;
        }

        if (buildingMaterial == null)
        {
            buildingMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            buildingMaterial.SetFloat("_Smoothness", 0.0f);
        }

        var root = JObject.Parse(File.ReadAllText(path));
        var features = (JArray)root["features"];
        Debug.Log("Features: " + features.Count);

        var chunkMeshes = new List<Mesh>();
        var chunkGO = new GameObject("Buildings_Chunk_0");
        chunkGO.transform.SetParent(transform, false);
        var chunkFilter = chunkGO.AddComponent<MeshFilter>();
        var chunkRenderer = chunkGO.AddComponent<MeshRenderer>();
        chunkRenderer.sharedMaterial = buildingMaterial;

        var combine = new List<CombineInstance>();
        int chunkIndex = 0;
        int processedBuildings = 0;
        int createdMeshes = 0;
        int skipped = 0;

        foreach (var feat in features)
        {
            var geom = feat["geometry"];
            if (geom == null) { skipped++; continue; }

            string type = geom.Value<string>("type");
            var coords = geom["coordinates"];
            if (coords == null) { skipped++; continue; }

            float h = minHeight;
            var props = feat["properties"];
            if (props != null && props["height_m"] != null && props["height_m"].Type != JTokenType.Null)
            {
                if (float.TryParse(props["height_m"].ToString(), out float parsed))
                    h = parsed;
            }
            h = Mathf.Max(minHeight, h * heightScale);

            if (type == "Polygon")
            {
                if (TryMakeMeshFromPolygon(coords, h, out Mesh m))
                {
                    AddToSceneOrChunk(m, mergeIntoChunks, combine, ref createdMeshes);
                    processedBuildings++;
                }
                else skipped++;
            }
            else if (type == "MultiPolygon")
            {
                foreach (var poly in coords)
                {
                    if (TryMakeMeshFromPolygon(poly, h, out Mesh m))
                    {
                        AddToSceneOrChunk(m, mergeIntoChunks, combine, ref createdMeshes);
                        processedBuildings++;
                    }
                    else skipped++;

                    if (mergeIntoChunks && processedBuildings % buildingsPerChunk == 0 && combine.Count > 0)
                    {
                        // bake current chunk
                        BakeChunk(chunkFilter, combine, ref chunkIndex, ref chunkGO, ref chunkFilter, buildingMaterial);
                    }
                }
            }
            else
            {
                skipped++;
            }
        }

        // final bake
        if (mergeIntoChunks && combine.Count > 0)
        {
            BakeChunk(chunkFilter, combine, ref chunkIndex, ref chunkGO, ref chunkFilter, buildingMaterial);
        }

        Debug.Log($"Processed buildings: {processedBuildings}, created meshes: {createdMeshes}, skipped: {skipped}");
    }

    void AddToSceneOrChunk(Mesh m, bool merge, List<CombineInstance> combine, ref int createdMeshes)
    {
        createdMeshes++;

        if (!merge)
        {
            var go = new GameObject("Building");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = buildingMaterial;
            return;
        }

        combine.Add(new CombineInstance
        {
            mesh = m,
            transform = Matrix4x4.identity
        });
    }

    void BakeChunk(MeshFilter chunkFilter, List<CombineInstance> combine,
                   ref int chunkIndex, ref GameObject chunkGO, ref MeshFilter newChunkFilter,
                   Material mat)
    {
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.CombineMeshes(combine.ToArray(), true, true);
        chunkFilter.sharedMesh = mesh;

        combine.Clear();

        chunkIndex++;
        chunkGO = new GameObject($"Buildings_Chunk_{chunkIndex}");
        chunkGO.transform.SetParent(transform, false);
        newChunkFilter = chunkGO.AddComponent<MeshFilter>();
        var mr = chunkGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
    }

    bool TryMakeMeshFromPolygon(JToken polygonCoords, float height, out Mesh mesh)
    {
        mesh = null;

        var exterior = polygonCoords[0];
        if (exterior == null || exterior.Count() < 3) return false;

        List<Vector2> footprint = new List<Vector2>();
        foreach (var pt in exterior)
        {
            double e = pt[0].Value<double>();
            double n = pt[1].Value<double>();

            float x = (float)(e - originEasting);
            float z = (float)(n - originNorthing);

            footprint.Add(new Vector2(x, z));
        }

        // remove closing point if duplicated
        if (footprint.Count > 3 && Vector2.Distance(footprint[0], footprint[footprint.Count - 1]) < 0.001f)
            footprint.RemoveAt(footprint.Count - 1);

        if (footprint.Count < 3) return false;

        int[] tri = EarClipTriangulator.Triangulate(footprint);
        if (tri == null || tri.Length < 3) return false;

        mesh = Extrude(footprint, tri, height);
        return true;
    }

    Mesh Extrude(List<Vector2> poly, int[] tri, float height)
    {
        int n = poly.Count;
        Vector3[] v = new Vector3[n * 2];

        for (int i = 0; i < n; i++)
        {
            v[i] = new Vector3(poly[i].x, baseY, poly[i].y);
            v[i + n] = new Vector3(poly[i].x, baseY + height, poly[i].y);
        }

        List<int> idx = new List<int>(tri.Length * 2 + n * 6);

        // bottom (reverse winding)
        for (int i = 0; i < tri.Length; i += 3)
        {
            idx.Add(tri[i + 0]);
            idx.Add(tri[i + 2]);
            idx.Add(tri[i + 1]);
        }

        // top
        for (int i = 0; i < tri.Length; i += 3)
        {
            idx.Add(tri[i + 0] + n);
            idx.Add(tri[i + 1] + n);
            idx.Add(tri[i + 2] + n);
        }

        // walls
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;

            int b0 = i;
            int b1 = j;
            int t0 = i + n;
            int t1 = j + n;

            idx.Add(b0); idx.Add(t0); idx.Add(t1);
            idx.Add(b0); idx.Add(t1); idx.Add(b1);
        }

        Mesh m = new Mesh();
        m.indexFormat = (v.Length > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.vertices = v;
        m.triangles = idx.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}