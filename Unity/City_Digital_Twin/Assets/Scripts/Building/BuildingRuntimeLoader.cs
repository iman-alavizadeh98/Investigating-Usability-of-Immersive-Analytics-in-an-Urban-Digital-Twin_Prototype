using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public class BuildingRuntimeLoader : MonoBehaviour
{
    [Header("Data")]
    public string fileName = "buildings_runtime_ruta.bin";

    [Header("Ruta")]
    public RutaCityLoader rutaCityLoader;

    [Header("Scale")]
    public float metersToUnity = 0.001f;

    [Header("Loading")]
    public int rutaGroupsPerFrame = 20;

    [Header("Mesh batching")]
    public int maxBuildingsPerMesh = 1000;

    [Header("Material")]
    public Material buildingMaterial;


    // ============================================================
    // FILE DATA
    // ============================================================

    private string[] rutaKeys;

    // Ruta index -> positions of building records in file
    private readonly Dictionary<int, List<long>> buildingOffsetsByRuta =
        new Dictionary<int, List<long>>();

    // Buildings without Ruta
    private readonly List<long> unmatchedOffsets =
        new List<long>();


    // ============================================================
    // START
    // ============================================================

    private IEnumerator Start()
    {
        string path = Path.Combine(
            Application.streamingAssetsPath,
            fileName
        );


        if (!File.Exists(path))
        {
            Debug.LogError(
                "Building binary not found:\n" +
                path
            );

            yield break;
        }


        // --------------------------------------------------------
        // FIND RUTA LOADER
        // --------------------------------------------------------

        if (rutaCityLoader == null)
        {
            rutaCityLoader =
                FindFirstObjectByType<RutaCityLoader>();
        }


        if (rutaCityLoader == null)
        {
            Debug.LogError(
                "RutaCityLoader not found."
            );

            yield break;
        }


        // --------------------------------------------------------
        // MATERIAL
        // --------------------------------------------------------

        if (buildingMaterial == null)
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit"
                );


            if (shader == null)
            {
                Debug.LogError(
                    "URP Lit shader not found."
                );

                yield break;
            }


            buildingMaterial =
                new Material(shader);


            buildingMaterial.color =
                new Color(
                    0.75f,
                    0.75f,
                    0.75f
                );
        }


        using BinaryReader reader =
            new BinaryReader(
                File.Open(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                )
            );


        // ========================================================
        // HEADER
        // ========================================================

        string magic =
            Encoding.ASCII.GetString(
                reader.ReadBytes(4)
            );


        if (magic != "GBLD")
        {
            Debug.LogError(
                "Invalid building binary magic."
            );

            yield break;
        }


        uint version =
            reader.ReadUInt32();


        if (version != 2)
        {
            Debug.LogError(
                $"This loader expects building binary version 2, " +
                $"but file version is {version}."
            );

            yield break;
        }


        uint buildingCount =
            reader.ReadUInt32();


        uint rutaKeyCount =
            reader.ReadUInt32();


        double originEasting =
            reader.ReadDouble();


        double originNorthing =
            reader.ReadDouble();


        Debug.Log(
            $"Building binary loaded\n" +
            $"Version: {version}\n" +
            $"Buildings: {buildingCount}\n" +
            $"Ruta keys: {rutaKeyCount}\n" +
            $"Origin: {originEasting}, {originNorthing}"
        );


        // ========================================================
        // RUTA KEY TABLE
        // ========================================================

        rutaKeys =
            new string[rutaKeyCount];


        for (
            int i = 0;
            i < rutaKeyCount;
            i++
        )
        {
            ushort byteLength =
                reader.ReadUInt16();


            byte[] bytes =
                reader.ReadBytes(
                    byteLength
                );


            rutaKeys[i] =
                Encoding.UTF8.GetString(
                    bytes
                );
        }


        Debug.Log(
            $"Read {rutaKeys.Length} Ruta keys."
        );


        // ========================================================
        // FIRST PASS
        //
        // We DO NOT create geometry here.
        //
        // We only remember where each building lives in the
        // binary file.
        // ========================================================

        Debug.Log(
            "Indexing buildings by Ruta..."
        );


        for (
            uint i = 0;
            i < buildingCount;
            i++
        )
        {
            long buildingOffset =
                reader.BaseStream.Position;


            // Building header
            reader.ReadUInt32(); // building ID
            reader.ReadSingle(); // height
            reader.ReadSingle(); // ground_z

            int rutaIndex =
                reader.ReadInt32();

            ushort partCount =
                reader.ReadUInt16();


            // Store the file position
            if (
                rutaIndex >= 0 &&
                rutaIndex < rutaKeys.Length
            )
            {
                if (
                    !buildingOffsetsByRuta.TryGetValue(
                        rutaIndex,
                        out List<long> offsets
                    )
                )
                {
                    offsets =
                        new List<long>();

                    buildingOffsetsByRuta.Add(
                        rutaIndex,
                        offsets
                    );
                }


                offsets.Add(
                    buildingOffset
                );
            }
            else
            {
                unmatchedOffsets.Add(
                    buildingOffset
                );
            }


            // Skip polygon geometry
            for (
                int p = 0;
                p < partCount;
                p++
            )
            {
                uint vertexCount =
                    reader.ReadUInt32();


                long bytesToSkip =
                    (long)vertexCount * 8L;


                reader.BaseStream.Seek(
                    bytesToSkip,
                    SeekOrigin.Current
                );
            }
        }


        Debug.Log(
            $"Building index complete.\n" +
            $"Ruta groups with buildings: " +
            $"{buildingOffsetsByRuta.Count}\n" +
            $"Unmatched buildings: " +
            $"{unmatchedOffsets.Count}"
        );


        // ========================================================
        // WAIT FOR RUTA
        // ========================================================

        Debug.Log(
            "Waiting for Ruta layer..."
        );


        while (!rutaCityLoader.IsReady)
        {
            yield return null;
        }


        Debug.Log(
            "Ruta layer ready. Generating buildings..."
        );


        // ========================================================
        // SECOND PASS
        //
        // Generate one movable BuildingRoot per Ruta.
        // ========================================================

        int processedGroups = 0;
        int processedBuildings = 0;


        foreach (
            KeyValuePair<int, List<long>> pair
            in buildingOffsetsByRuta
        )
        {
            int rutaIndex =
                pair.Key;


            string rutaKey =
                rutaKeys[rutaIndex];


            float rutaHeight = 0f;


            rutaCityLoader.TryGetRutaHeight(
                rutaKey,
                out rutaHeight
            );


            // ----------------------------------------------------
            // RUTA BUILDING ROOT
            //
            // This root moves vertically.
            // Building geometry itself remains scale 1.
            // ----------------------------------------------------

            GameObject rutaBuildingsRoot =
                new GameObject(
                    $"Buildings_{rutaKey}"
                );


            rutaBuildingsRoot.transform.SetParent(
                transform,
                false
            );


            rutaBuildingsRoot.transform.localPosition =
                new Vector3(
                    0f,
                    rutaHeight,
                    0f
                );


            // ----------------------------------------------------
            // BUILD MESHES FOR THIS RUTA
            // ----------------------------------------------------

            List<Vector3> meshVertices =
                new List<Vector3>();


            List<int> meshTriangles =
                new List<int>();


            int buildingsInBatch = 0;
            int batchNumber = 0;


            foreach (
                long offset
                in pair.Value
            )
            {
                AddBuildingFromFile(
                    reader,
                    offset,
                    meshVertices,
                    meshTriangles
                );


                processedBuildings++;
                buildingsInBatch++;


                if (
                    buildingsInBatch
                    >= maxBuildingsPerMesh
                )
                {
                    CreateMeshObject(
                        rutaBuildingsRoot.transform,
                        batchNumber,
                        meshVertices,
                        meshTriangles
                    );


                    batchNumber++;


                    meshVertices.Clear();
                    meshTriangles.Clear();


                    buildingsInBatch = 0;
                }
            }


            // Final partial mesh
            if (meshVertices.Count > 0)
            {
                CreateMeshObject(
                    rutaBuildingsRoot.transform,
                    batchNumber,
                    meshVertices,
                    meshTriangles
                );
            }


            processedGroups++;


            if (
                processedGroups
                % rutaGroupsPerFrame
                == 0
            )
            {
                Debug.Log(
                    $"Building Ruta groups: " +
                    $"{processedGroups}/" +
                    $"{buildingOffsetsByRuta.Count}\n" +
                    $"Buildings generated: " +
                    $"{processedBuildings}"
                );


                yield return null;
            }
        }


        // ========================================================
        // UNMATCHED BUILDINGS
        // ========================================================

        if (unmatchedOffsets.Count > 0)
        {
            Debug.Log(
                $"Generating {unmatchedOffsets.Count} " +
                $"unmatched buildings at ground level..."
            );


            GameObject unmatchedRoot =
                new GameObject(
                    "Buildings_Unmatched"
                );


            unmatchedRoot.transform.SetParent(
                transform,
                false
            );


            List<Vector3> vertices =
                new List<Vector3>();


            List<int> triangles =
                new List<int>();


            int buildingsInBatch = 0;
            int batchNumber = 0;


            foreach (
                long offset
                in unmatchedOffsets
            )
            {
                AddBuildingFromFile(
                    reader,
                    offset,
                    vertices,
                    triangles
                );


                buildingsInBatch++;


                if (
                    buildingsInBatch
                    >= maxBuildingsPerMesh
                )
                {
                    CreateMeshObject(
                        unmatchedRoot.transform,
                        batchNumber,
                        vertices,
                        triangles
                    );


                    batchNumber++;


                    vertices.Clear();
                    triangles.Clear();


                    buildingsInBatch = 0;


                    yield return null;
                }
            }


            if (vertices.Count > 0)
            {
                CreateMeshObject(
                    unmatchedRoot.transform,
                    batchNumber,
                    vertices,
                    triangles
                );
            }
        }


        Debug.Log(
            "BUILDING CITY COMPLETE\n" +
            $"Matched buildings generated: " +
            $"{processedBuildings}\n" +
            $"Ruta building groups: " +
            $"{processedGroups}\n" +
            $"Unmatched: " +
            $"{unmatchedOffsets.Count}"
        );
    }


    // ============================================================
    // READ AND ADD ONE BUILDING
    // ============================================================

    private void AddBuildingFromFile(
        BinaryReader reader,
        long offset,
        List<Vector3> meshVertices,
        List<int> meshTriangles
    )
    {
        reader.BaseStream.Seek(
            offset,
            SeekOrigin.Begin
        );


        reader.ReadUInt32(); // ID


        float heightMeters =
            reader.ReadSingle();


        reader.ReadSingle(); // ground_z
        reader.ReadInt32();  // ruta index


        ushort partCount =
            reader.ReadUInt16();


        float height =
            heightMeters
            * metersToUnity;


        for (
            int p = 0;
            p < partCount;
            p++
        )
        {
            uint vertexCount =
                reader.ReadUInt32();


            if (
                vertexCount < 3 ||
                vertexCount > 100000
            )
            {
                Debug.LogError(
                    $"Invalid vertex count: " +
                    $"{vertexCount}"
                );

                return;
            }


            Vector2[] polygon =
                new Vector2[vertexCount];


            for (
                int i = 0;
                i < vertexCount;
                i++
            )
            {
                float x =
                    reader.ReadSingle();


                float z =
                    reader.ReadSingle();


                polygon[i] =
                    new Vector2(
                        x * metersToUnity,
                        z * metersToUnity
                    );
            }


            AddExtrudedPolygon(
                polygon,
                height,
                meshVertices,
                meshTriangles
            );
        }
    }


    // ============================================================
    // EXTRUDE FOOTPRINT
    // ============================================================

    private void AddExtrudedPolygon(
        Vector2[] polygon,
        float height,
        List<Vector3> vertices,
        List<int> triangles
    )
    {
        if (
            polygon == null ||
            polygon.Length < 3
        )
        {
            return;
        }


        // --------------------------------------------------------
        // ROOF
        // --------------------------------------------------------

        List<Vector2> points =
            new List<Vector2>(
                polygon
            );


        List<List<Vector2>> holes =
            new List<List<Vector2>>();


        bool success =
            Triangulation.triangulate(
                points,
                holes,
                height,
                out List<int> roofIndices,
                out List<Vector3> roofVertices
            );


        if (!success)
        {
            return;
        }


        int roofOffset =
            vertices.Count;


        vertices.AddRange(
            roofVertices
        );


        for (
            int i = 0;
            i < roofIndices.Count;
            i++
        )
        {
            triangles.Add(
                roofOffset
                + roofIndices[i]
            );
        }


        // --------------------------------------------------------
        // WALLS
        // --------------------------------------------------------

        for (
            int i = 0;
            i < polygon.Length;
            i++
        )
        {
            int next =
                (i + 1)
                % polygon.Length;


            Vector2 a =
                polygon[i];


            Vector2 b =
                polygon[next];


            int offset =
                vertices.Count;


            vertices.Add(
                new Vector3(
                    a.x,
                    0f,
                    a.y
                )
            );


            vertices.Add(
                new Vector3(
                    b.x,
                    0f,
                    b.y
                )
            );


            vertices.Add(
                new Vector3(
                    a.x,
                    height,
                    a.y
                )
            );


            vertices.Add(
                new Vector3(
                    b.x,
                    height,
                    b.y
                )
            );


            triangles.Add(
                offset
            );

            triangles.Add(
                offset + 2
            );

            triangles.Add(
                offset + 1
            );


            triangles.Add(
                offset + 1
            );

            triangles.Add(
                offset + 2
            );

            triangles.Add(
                offset + 3
            );
        }
    }


    // ============================================================
    // CREATE MESH
    // ============================================================

    private void CreateMeshObject(
        Transform parent,
        int batchNumber,
        List<Vector3> vertices,
        List<int> triangles
    )
    {
        if (
            vertices.Count == 0 ||
            triangles.Count == 0
        )
        {
            return;
        }


        GameObject meshObject =
            new GameObject(
                $"Mesh_{batchNumber:D3}"
            );


        meshObject.transform.SetParent(
            parent,
            false
        );


        MeshFilter filter =
            meshObject.AddComponent<MeshFilter>();


        MeshRenderer renderer =
            meshObject.AddComponent<MeshRenderer>();


        Mesh mesh =
            new Mesh();


        mesh.name =
            $"BuildingsMesh_{batchNumber:D3}";


        mesh.indexFormat =
            IndexFormat.UInt32;


        mesh.SetVertices(
            vertices
        );


        mesh.SetTriangles(
            triangles,
            0
        );


        mesh.RecalculateNormals();
        mesh.RecalculateBounds();


        filter.sharedMesh =
            mesh;


        renderer.sharedMaterial =
            buildingMaterial;
    }
}