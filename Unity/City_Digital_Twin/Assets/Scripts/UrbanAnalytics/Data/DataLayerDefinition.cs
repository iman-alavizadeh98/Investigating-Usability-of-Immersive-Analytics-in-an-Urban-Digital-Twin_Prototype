using System;
using System.Collections.Generic;
using UnityEngine;

namespace UrbanAnalytics.Data
{
    /// <summary>
    /// Metadata and schema describing one analytical data layer.
    ///
    /// Example:
    ///     income_2023
    ///
    /// The definition describes WHAT the dataset contains.
    /// Actual per-unit values are stored separately.
    /// </summary>
    [Serializable]
    public class DataLayerDefinition
    {
        [SerializeField]
        private string schemaVersion = "1.0";

        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [SerializeField]
        [TextArea]
        private string description;

        [SerializeField]
        private string targetSpatialLayerId;

        [SerializeField]
        private string dataFile = "values.json";

        // Stored as string intentionally.
        // Unity JsonUtility handles this more predictably than enum names.
        [SerializeField]
        private string temporalMode = "Static";

        [SerializeField]
        private List<DataVariableDefinition> variables = new();


        public string SchemaVersion => schemaVersion;

        public string Id => id;

        public string DisplayName => displayName;

        public string Description => description;

        public string TargetSpatialLayerId => targetSpatialLayerId;

        public string DataFile => dataFile;

        public IReadOnlyList<DataVariableDefinition> Variables => variables;


        public bool TryGetTemporalMode(out DataTemporalMode mode)
        {
            return Enum.TryParse(
                temporalMode,
                true,
                out mode
            );
        }


        public bool TryGetVariable(
            string variableId,
            out DataVariableDefinition variable)
        {
            variable = null;

            if (string.IsNullOrWhiteSpace(variableId))
                return false;

            if (variables == null)
                return false;

            for (int i = 0; i < variables.Count; i++)
            {
                DataVariableDefinition candidate = variables[i];

                if (candidate == null)
                    continue;

                if (string.Equals(
                        candidate.Id,
                        variableId,
                        StringComparison.Ordinal))
                {
                    variable = candidate;
                    return true;
                }
            }

            return false;
        }
    }


    /// <summary>
    /// Metadata describing one variable inside a DataLayer.
    ///
    /// Examples:
    ///     median_income
    ///     population
    ///     temperature
    ///     traffic_volume
    /// </summary>
    [Serializable]
    public class DataVariableDefinition
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [SerializeField]
        [TextArea]
        private string description;

        [SerializeField]
        private string valueType = "Float";

        [SerializeField]
        private string unit;


        public string Id => id;

        public string DisplayName => displayName;

        public string Description => description;

        public string Unit => unit;


        public bool TryGetValueType(out DataValueType type)
        {
            return Enum.TryParse(
                valueType,
                true,
                out type
            );
        }
    }


    /// <summary>
    /// Defines how the dataset changes over time.
    ///
    /// Static:
    ///     One value per unit/variable.
    ///
    /// TimeSeries:
    ///     Multiple values across timestamps.
    ///
    /// TimeSeries is reserved for the next stage.
    /// </summary>
    public enum DataTemporalMode
    {
        Static = 0,
        TimeSeries = 1
    }


    /// <summary>
    /// Logical variable type.
    /// </summary>
    public enum DataValueType
    {
        Float = 0,
        Integer = 1,
        Boolean = 2,
        String = 3
    }
}