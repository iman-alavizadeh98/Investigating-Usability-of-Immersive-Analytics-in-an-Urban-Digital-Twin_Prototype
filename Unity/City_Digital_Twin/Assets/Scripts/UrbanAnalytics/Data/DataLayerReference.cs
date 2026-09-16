using System;
using UnityEngine;

namespace UrbanAnalytics.Data
{
    /// <summary>
    /// Reference to a DataLayer definition inside the runtime package.
    ///
    /// Stored in project_manifest.json.
    ///
    /// Example:
    /// {
    ///     "id": "income_2023",
    ///     "definition": "data_layers/income_2023/layer.json"
    /// }
    /// </summary>
    [Serializable]
    public class DataLayerReference
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string definition;


        public string Id => id;

        public string Definition => definition;
    }
}