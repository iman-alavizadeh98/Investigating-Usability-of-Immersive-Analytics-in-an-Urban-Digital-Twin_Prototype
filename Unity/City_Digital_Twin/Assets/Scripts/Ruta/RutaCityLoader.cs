using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RutaCityLoader : MonoBehaviour
{
    // ============================================================
    // JSON STRUCTURE
    // ============================================================

    [Serializable]
    public class VertexData
    {
        public float x;
        public float z;
    }

    [Serializable]
    public class ExteriorData
    {
        public VertexData[] vertices;
    }

    [Serializable]
    public class RutaData
    {
        public int kvartil1;
        public int kvartil2;
        public int kvartil3;
        public int kvartil4;
        public int totalt;
        public float medianInk;
    }

    [Serializable]
    public class RutaUnit
    {
        public string id;
        public string ruta;
        public int gridSize;

        public ExteriorData exterior;
        public RutaData data;
    }

    [Serializable]
    public class MedianIncomeStatistics
    {
        public float minimum;
        public float maximum;
        public float p01;
        public float p99;
        public float median;
    }

    [Serializable]
    public class Statistics
    {
        public MedianIncomeStatistics medianInk;
    }

    [Serializable]
    public class Metadata
    {
        public string name;
        public int unitCount;
        public Statistics statistics;
    }

    [Serializable]
    public class RutaDataset
    {
        public Metadata metadata;
        public RutaUnit[] units;
    }


    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Data")]
    public string jsonFileName = "ruta_2023_unity.json";

    [Header("Scale")]
    [Tooltip("0.001 means 1 km = 1 Unity unit")]
    public float metersToUnity = 0.001f;

    [Header("Extrusion")]
    public float minimumHeight = 0.05f;
    public float maximumHeight = 3.0f;

    [Header("Loading")]
    public int cellsPerFrame = 100;


    // ============================================================
    // STATE
    // ============================================================

    public bool IsReady { get; private set; } = false;

    private RutaDataset dataset;

    private float incomeLow;
    private float incomeHigh;


    // Stores:
    //
    // Ruta ID → current analytical extrusion height
    //
    // Example:
    // "250_3175006390000" → 1.43f

    private readonly Dictionary<string, float> rutaHeights =
        new Dictionary<string, float>();


    // ============================================================
    // PUBLIC ACCESS
    // ============================================================

    public bool TryGetRutaHeight(
        string rutaId,
        out float height
    )
    {
        return rutaHeights.TryGetValue(
            rutaId,
            out height
        );
    }


    // ============================================================
    // START
    // ============================================================

    private IEnumerator Start()
    {
        IsReady = false;

        rutaHeights.Clear();


        string filePath = Path.Combine(
            Application.streamingAssetsPath,
            jsonFileName
        );


        if (!File.Exists(filePath))
        {
            Debug.LogError(
                "Ruta JSON not found at:\n" +
                filePath
            );

            yield break;
        }


        // --------------------------------------------------------
        // READ JSON
        // --------------------------------------------------------

        string json =
            File.ReadAllText(filePath);


        dataset =
            JsonUtility.FromJson<RutaDataset>(
                json
            );


        if (
            dataset == null ||
            dataset.units == null ||
            dataset.units.Length == 0
        )
        {
            Debug.LogError(
                "Ruta JSON could not be loaded."
            );

            yield break;
        }


        // --------------------------------------------------------
        // VISUALIZATION RANGE
        // --------------------------------------------------------

        incomeLow =
            dataset
            .metadata
            .statistics
            .medianInk
            .p01;


        incomeHigh =
            dataset
            .metadata
            .statistics
            .medianInk
            .p99;


        Debug.Log(
            $"Ruta dataset loaded.\n" +
            $"Cells: {dataset.units.Length}\n" +
            $"Income visualization range: " +
            $"{incomeLow} - {incomeHigh}"
        );


        // --------------------------------------------------------
        // BUILD ALL RUTA CELLS
        // --------------------------------------------------------

        int created = 0;


        foreach (RutaUnit unit in dataset.units)
        {
            if (
                unit.exterior == null ||
                unit.exterior.vertices == null ||
                unit.exterior.vertices.Length < 3
            )
            {
                continue;
            }


            CreateRuta(unit);

            created++;


            if (
                created % cellsPerFrame == 0
            )
            {
                Debug.Log(
                    $"Created {created} / " +
                    $"{dataset.units.Length}"
                );

                yield return null;
            }
        }


        // --------------------------------------------------------
        // READY
        // --------------------------------------------------------

        IsReady = true;


        Debug.Log(
            $"Ruta generation complete: " +
            $"{created} cells.\n" +
            $"Stored Ruta heights: " +
            $"{rutaHeights.Count}"
        );
    }


    // ============================================================
    // CREATE ONE RUTA
    // ============================================================

    private void CreateRuta(
        RutaUnit unit
    )
    {
        // --------------------------------------------------------
        // POLYGON VERTICES
        // --------------------------------------------------------

        int vertexCount =
            unit.exterior.vertices.Length;


        Vector2[] vertices =
            new Vector2[vertexCount];


        for (
            int i = 0;
            i < vertexCount;
            i++
        )
        {
            VertexData vertex =
                unit.exterior.vertices[i];


            vertices[i] =
                new Vector2(
                    vertex.x * metersToUnity,
                    vertex.z * metersToUnity
                );
        }


        // --------------------------------------------------------
        // NORMALIZE MEDIAN INCOME
        // --------------------------------------------------------

        float normalized =
            Mathf.InverseLerp(
                incomeLow,
                incomeHigh,
                unit.data.medianInk
            );


        // --------------------------------------------------------
        // EXTRUSION HEIGHT
        // --------------------------------------------------------

        float height =
            Mathf.Lerp(
                minimumHeight,
                maximumHeight,
                normalized
            );


        // --------------------------------------------------------
        // STORE HEIGHT FOR BUILDINGS
        // --------------------------------------------------------

        rutaHeights[unit.id] =
            height;


        // --------------------------------------------------------
        // COLOR
        // --------------------------------------------------------

        Color lowColor =
            new Color(
                0.15f,
                0.2f,
                0.7f
            );


        Color highColor =
            new Color(
                0.95f,
                0.85f,
                0.15f
            );


        Color prismColor =
            Color.Lerp(
                lowColor,
                highColor,
                normalized
            );


        // --------------------------------------------------------
        // CREATE RUTA OBJECT
        // --------------------------------------------------------

        GameObject rutaObject =
            new GameObject(
                unit.id
            );


        rutaObject.transform.SetParent(
            transform,
            false
        );


        // --------------------------------------------------------
        // EXTRUDE
        // --------------------------------------------------------

        PolyExtruderLight extruder =
            rutaObject.AddComponent<PolyExtruderLight>();


        extruder.createPrism(
            unit.id,
            height,
            vertices,
            prismColor
        );
    }
}