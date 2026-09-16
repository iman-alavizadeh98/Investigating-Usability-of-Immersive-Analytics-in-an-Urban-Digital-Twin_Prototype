using System;
using System.Collections.Generic;

namespace UrbanAnalytics.Data
{
    /// <summary>
    /// Runtime representation of one analytical data layer.
    ///
    /// A DataLayer contains:
    /// - metadata/schema,
    /// - unit IDs,
    /// - typed variable columns,
    /// - fast unit lookup.
    ///
    /// It contains no Unity rendering objects and no spatial geometry.
    /// </summary>
    public sealed class DataLayer
    {
        private readonly string[] unitIds;

        private readonly Dictionary<string, int> unitIndexById;

        private readonly Dictionary<string, DataColumn> columns;


        public DataLayerDefinition Definition { get; }

        public string Id => Definition.Id;

        public string DisplayName => Definition.DisplayName;

        public string TargetSpatialLayerId =>
            Definition.TargetSpatialLayerId;

        public int UnitCount => unitIds.Length;

        public int VariableCount => columns.Count;

        public IReadOnlyList<string> UnitIds => unitIds;


        internal DataLayer(
            DataLayerDefinition definition,
            string[] unitIds,
            Dictionary<string, DataColumn> columns)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));

            this.unitIds = unitIds
                ?? throw new ArgumentNullException(nameof(unitIds));

            this.columns = columns
                ?? throw new ArgumentNullException(nameof(columns));

            unitIndexById =
                new Dictionary<string, int>(
                    unitIds.Length,
                    StringComparer.Ordinal
                );

            for (int i = 0; i < unitIds.Length; i++)
            {
                string unitId = unitIds[i];

                if (string.IsNullOrWhiteSpace(unitId))
                {
                    throw new ArgumentException(
                        $"Data layer '{definition.Id}' contains " +
                        $"an empty unit ID at row {i}."
                    );
                }

                if (!unitIndexById.TryAdd(unitId, i))
                {
                    throw new ArgumentException(
                        $"Data layer '{definition.Id}' contains " +
                        $"duplicate unit ID '{unitId}'."
                    );
                }
            }
        }


        // ---------------------------------------------------------
        // Unit lookup
        // ---------------------------------------------------------

        public bool ContainsUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                return false;

            return unitIndexById.ContainsKey(unitId);
        }


        public bool TryGetRowIndex(
            string unitId,
            out int rowIndex)
        {
            rowIndex = -1;

            if (string.IsNullOrWhiteSpace(unitId))
                return false;

            return unitIndexById.TryGetValue(
                unitId,
                out rowIndex
            );
        }


        // ---------------------------------------------------------
        // Variable lookup
        // ---------------------------------------------------------

        public bool ContainsVariable(string variableId)
        {
            if (string.IsNullOrWhiteSpace(variableId))
                return false;

            return columns.ContainsKey(variableId);
        }


        public bool TryGetVariableDefinition(
            string variableId,
            out DataVariableDefinition definition)
        {
            return Definition.TryGetVariable(
                variableId,
                out definition
            );
        }


        // ---------------------------------------------------------
        // Validity / no-data
        // ---------------------------------------------------------

        public bool IsValueValid(
            string unitId,
            string variableId)
        {
            if (!TryResolve(
                    unitId,
                    variableId,
                    out int rowIndex,
                    out DataColumn column))
            {
                return false;
            }

            return column.IsValid(rowIndex);
        }


        // ---------------------------------------------------------
        // Floating-point values
        // ---------------------------------------------------------

        public bool TryGetDouble(
            string unitId,
            string variableId,
            out double value)
        {
            value = default;

            if (!TryResolve(
                    unitId,
                    variableId,
                    out int rowIndex,
                    out DataColumn column))
            {
                return false;
            }

            if (!column.IsValid(rowIndex))
                return false;

            switch (column)
            {
                case FloatDataColumn floatColumn:
                    value = floatColumn.Values[rowIndex];
                    return true;

                case IntegerDataColumn integerColumn:
                    value = integerColumn.Values[rowIndex];
                    return true;

                default:
                    return false;
            }
        }


        public bool TryGetFloat(
            string unitId,
            string variableId,
            out float value)
        {
            value = default;

            if (!TryGetDouble(
                    unitId,
                    variableId,
                    out double doubleValue))
            {
                return false;
            }

            if (doubleValue > float.MaxValue ||
                doubleValue < -float.MaxValue)
            {
                return false;
            }

            value = (float)doubleValue;
            return true;
        }


        // ---------------------------------------------------------
        // Integer values
        // ---------------------------------------------------------

        public bool TryGetInteger(
            string unitId,
            string variableId,
            out long value)
        {
            value = default;

            if (!TryResolve(
                    unitId,
                    variableId,
                    out int rowIndex,
                    out DataColumn column))
            {
                return false;
            }

            if (!column.IsValid(rowIndex))
                return false;

            if (column is not IntegerDataColumn integerColumn)
                return false;

            value = integerColumn.Values[rowIndex];
            return true;
        }


        // ---------------------------------------------------------
        // Boolean values
        // ---------------------------------------------------------

        public bool TryGetBoolean(
            string unitId,
            string variableId,
            out bool value)
        {
            value = default;

            if (!TryResolve(
                    unitId,
                    variableId,
                    out int rowIndex,
                    out DataColumn column))
            {
                return false;
            }

            if (!column.IsValid(rowIndex))
                return false;

            if (column is not BooleanDataColumn booleanColumn)
                return false;

            value = booleanColumn.Values[rowIndex];
            return true;
        }


        // ---------------------------------------------------------
        // String values
        // ---------------------------------------------------------

        public bool TryGetString(
            string unitId,
            string variableId,
            out string value)
        {
            value = null;

            if (!TryResolve(
                    unitId,
                    variableId,
                    out int rowIndex,
                    out DataColumn column))
            {
                return false;
            }

            if (!column.IsValid(rowIndex))
                return false;

            if (column is not StringDataColumn stringColumn)
                return false;

            value = stringColumn.Values[rowIndex];

            return value != null;
        }


        // ---------------------------------------------------------
        // Numeric statistics
        // ---------------------------------------------------------

        /// <summary>
        /// Returns min/max across valid values for a numeric variable.
        ///
        /// This will later be used directly by visualization scales.
        /// </summary>
        public bool TryGetNumericRange(
            string variableId,
            out double minimum,
            out double maximum)
        {
            minimum = default;
            maximum = default;

            if (string.IsNullOrWhiteSpace(variableId))
                return false;

            if (!columns.TryGetValue(
                    variableId,
                    out DataColumn column))
            {
                return false;
            }

            return column.TryGetNumericRange(
                out minimum,
                out maximum
            );
        }


        // ---------------------------------------------------------
        // Internal lookup
        // ---------------------------------------------------------

        private bool TryResolve(
            string unitId,
            string variableId,
            out int rowIndex,
            out DataColumn column)
        {
            rowIndex = -1;
            column = null;

            if (string.IsNullOrWhiteSpace(unitId) ||
                string.IsNullOrWhiteSpace(variableId))
            {
                return false;
            }

            if (!unitIndexById.TryGetValue(
                    unitId,
                    out rowIndex))
            {
                return false;
            }

            return columns.TryGetValue(
                variableId,
                out column
            );
        }
    }


    // =============================================================
    // Runtime column model
    // =============================================================

    internal abstract class DataColumn
    {
        private readonly bool[] validMask;


        protected DataColumn(bool[] validMask)
        {
            this.validMask = validMask;
        }


        public bool IsValid(int rowIndex)
        {
            if (validMask == null ||
                validMask.Length == 0)
            {
                return true;
            }

            if (rowIndex < 0 ||
                rowIndex >= validMask.Length)
            {
                return false;
            }

            return validMask[rowIndex];
        }


        public virtual bool TryGetNumericRange(
            out double minimum,
            out double maximum)
        {
            minimum = default;
            maximum = default;

            return false;
        }
    }


    internal sealed class FloatDataColumn : DataColumn
    {
        public double[] Values { get; }

        private readonly bool hasRange;
        private readonly double minimum;
        private readonly double maximum;


        public FloatDataColumn(
            double[] values,
            bool[] validMask)
            : base(validMask)
        {
            Values = values
                ?? throw new ArgumentNullException(nameof(values));

            hasRange = CalculateRange(
                values,
                validMask,
                out minimum,
                out maximum
            );
        }


        public override bool TryGetNumericRange(
            out double minimum,
            out double maximum)
        {
            minimum = this.minimum;
            maximum = this.maximum;

            return hasRange;
        }


        private static bool CalculateRange(
            double[] values,
            bool[] validMask,
            out double minimum,
            out double maximum)
        {
            minimum = double.PositiveInfinity;
            maximum = double.NegativeInfinity;

            bool found = false;

            for (int i = 0; i < values.Length; i++)
            {
                if (validMask != null &&
                    validMask.Length > 0 &&
                    !validMask[i])
                {
                    continue;
                }

                double value = values[i];

                if (double.IsNaN(value) ||
                    double.IsInfinity(value))
                {
                    continue;
                }

                if (value < minimum)
                    minimum = value;

                if (value > maximum)
                    maximum = value;

                found = true;
            }

            if (!found)
            {
                minimum = default;
                maximum = default;
            }

            return found;
        }
    }


    internal sealed class IntegerDataColumn : DataColumn
    {
        public long[] Values { get; }

        private readonly bool hasRange;
        private readonly double minimum;
        private readonly double maximum;


        public IntegerDataColumn(
            long[] values,
            bool[] validMask)
            : base(validMask)
        {
            Values = values
                ?? throw new ArgumentNullException(nameof(values));

            hasRange = CalculateRange(
                values,
                validMask,
                out minimum,
                out maximum
            );
        }


        public override bool TryGetNumericRange(
            out double minimum,
            out double maximum)
        {
            minimum = this.minimum;
            maximum = this.maximum;

            return hasRange;
        }


        private static bool CalculateRange(
            long[] values,
            bool[] validMask,
            out double minimum,
            out double maximum)
        {
            minimum = double.PositiveInfinity;
            maximum = double.NegativeInfinity;

            bool found = false;

            for (int i = 0; i < values.Length; i++)
            {
                if (validMask != null &&
                    validMask.Length > 0 &&
                    !validMask[i])
                {
                    continue;
                }

                long value = values[i];

                if (value < minimum)
                    minimum = value;

                if (value > maximum)
                    maximum = value;

                found = true;
            }

            if (!found)
            {
                minimum = default;
                maximum = default;
            }

            return found;
        }
    }


    internal sealed class BooleanDataColumn : DataColumn
    {
        public bool[] Values { get; }


        public BooleanDataColumn(
            bool[] values,
            bool[] validMask)
            : base(validMask)
        {
            Values = values
                ?? throw new ArgumentNullException(nameof(values));
        }
    }


    internal sealed class StringDataColumn : DataColumn
    {
        public string[] Values { get; }


        public StringDataColumn(
            string[] values,
            bool[] validMask)
            : base(validMask)
        {
            Values = values
                ?? throw new ArgumentNullException(nameof(values));
        }
    }
}