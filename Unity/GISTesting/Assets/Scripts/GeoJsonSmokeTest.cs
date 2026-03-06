using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class GeoJsonSmokeTest : MonoBehaviour
{
    public string geoJsonFileName = "buildings_with_heights_1km.geojson";

    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, geoJsonFileName);
        Debug.Log("GeoJSON path: " + path);

        if (!File.Exists(path))
        {
            Debug.LogError("File not found: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        var root = JObject.Parse(json);

        var features = (JArray)root["features"];
        Debug.Log("Feature count: " + features.Count);

        // Print one example feature type
        var geomType = features[0]["geometry"]?["type"]?.ToString();
        var h = features[0]["properties"]?["height_m"]?.ToString();
        Debug.Log("First feature geometry type: " + geomType + ", height_m: " + h);
    }
}