using UnityEngine;
using Newtonsoft.Json.Linq;

public class JsonTest : MonoBehaviour
{
    void Start()
    {
        JObject obj = JObject.Parse("{\"a\":1}");
        Debug.Log(obj["a"]);
    }
}