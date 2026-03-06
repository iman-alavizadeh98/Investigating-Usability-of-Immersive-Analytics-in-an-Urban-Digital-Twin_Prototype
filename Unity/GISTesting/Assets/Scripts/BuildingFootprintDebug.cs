using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class BuildingFootprintDebug : MonoBehaviour
{
    public string geoJsonFileName = "buildings_with_heights_1km.geojson";

    [Header("Origin from metadata_1km.json")]
    public double originEasting = 320669.5;
    public double originNorthing = 6399427.0;

    [Header("Debug")]
    public float y = 0.2f;                 // slightly above terrain
    public int maxBuildings = 50;          // start small
    public float lineWidth = 0.05f;

    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, geoJsonFileName);
        var root = JObject.Parse(File.ReadAllText(path));
        var features = (JArray)root["features"];

        int drawn = 0;

        foreach (var feat in features)
        {
            if (drawn >= maxBuildings) break;

            var geom = feat["geometry"];
            if (geom == null) continue;

            string type = geom.Value<string>("type");
            var coords = geom["coordinates"];
            if (coords == null) continue;

            if (type == "Polygon")
            {
                DrawPolygon(coords, drawn);
                drawn++;
            }
            else if (type == "MultiPolygon")
            {
                foreach (var poly in coords)
                {
                    if (drawn >= maxBuildings) break;
                    DrawPolygon(poly, drawn);
                    drawn++;
                }
            }
        }

        Debug.Log("Footprints drawn: " + drawn);
    }

    void DrawPolygon(JToken polygonCoords, int idx)
    {
        // GeoJSON Polygon: [ exteriorRing, hole1, hole2, ... ]
        var exterior = polygonCoords[0];
        if (exterior == null || exterior.Count() < 3) return;

        List<Vector3> pts = new List<Vector3>();

        foreach (var pt in exterior)
        {
            double e = pt[0].Value<double>();
            double n = pt[1].Value<double>();

            // SWEREF99TM -> Unity local
            float x = (float)(e - originEasting);
            float z = (float)(n - originNorthing);

            pts.Add(new Vector3(x, y, z));
        }

        // Remove closing duplicate
        if (pts.Count > 2 && Vector3.Distance(pts[0], pts[pts.Count - 1]) < 0.001f)
            pts.RemoveAt(pts.Count - 1);

        if (pts.Count < 3) return;

        var go = new GameObject($"Footprint_{idx}");
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = pts.Count + 1; // closed loop
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.loop = false;

        for (int i = 0; i < pts.Count; i++)
            lr.SetPosition(i, pts[i]);

        lr.SetPosition(pts.Count, pts[0]); // close
    }
}