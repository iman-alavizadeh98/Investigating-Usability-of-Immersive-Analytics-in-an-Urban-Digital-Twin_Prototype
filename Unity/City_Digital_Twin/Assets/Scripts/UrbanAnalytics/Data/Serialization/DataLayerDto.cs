using System;

namespace UrbanAnalytics.Data.Serialization
{
    /// <summary>
    /// Serialized value file for a DataLayer.
    ///
    /// Values are stored column-wise.
    ///
    /// Every column must have the same number of entries
    /// as unitIds.
    /// </summary>
    [Serializable]
    public class DataLayerFileDto
    {
        public string schemaVersion;

        public string dataLayerId;

        public string targetSpatialLayerId;

        public string[] unitIds;

        public DataColumnDto[] columns;
    }


    /// <summary>
    /// Serialized values for one analytical variable.
    ///
    /// Only the array matching the variable's declared
    /// DataValueType should be populated.
    ///
    /// Examples:
    ///
    /// Float:
    ///     floatValues
    ///
    /// Integer:
    ///     integerValues
    ///
    /// Boolean:
    ///     booleanValues
    ///
    /// String:
    ///     stringValues
    ///
    /// valid is optional.
    /// If absent or empty, every row is considered valid.
    /// </summary>
    [Serializable]
    public class DataColumnDto
    {
        public string variableId;

        public double[] floatValues;

        public long[] integerValues;

        public bool[] booleanValues;

        public string[] stringValues;

        public bool[] valid;
    }
}