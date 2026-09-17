using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Core.IO;
using UrbanAnalytics.Data.Serialization;

namespace UrbanAnalytics.Data
{
    /// <summary>
    /// Loads and validates an analytical DataLayer package from
    /// the runtime package stored in StreamingAssets.
    ///
    /// Expected structure:
    ///
    /// data_layers/
    /// └── layer_name/
    ///     ├── layer.json
    ///     └── values.json
    ///
    /// The loader is platform-independent because all runtime
    /// file access goes through RuntimeAssetReader.
    /// </summary>
    public static class DataLayerLoader
    {
        public const string SupportedSchemaVersion =
            "1.0";


        public static async Task<DataLayer> LoadAsync(
            string layerDefinitionRelativePath,
            RuntimeAssetReader assetReader,
            CancellationToken cancellationToken = default
        )
        {
            if (assetReader == null)
            {
                throw new ArgumentNullException(
                    nameof(assetReader)
                );
            }


            string definitionPath =
                RuntimeAssetReader.NormalizeRelativePath(
                    layerDefinitionRelativePath
                );


            // =====================================================
            // LOAD DEFINITION
            // =====================================================

            string definitionJson;

            try
            {
                definitionJson =
                    await assetReader.ReadTextAsync(
                        definitionPath,
                        cancellationToken
                    );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to read data layer definition " +
                    $"'{definitionPath}'.",
                    exception
                );
            }


            cancellationToken
                .ThrowIfCancellationRequested();


            if (string.IsNullOrWhiteSpace(
                    definitionJson
                ))
            {
                throw new InvalidDataException(
                    $"Data layer definition " +
                    $"'{definitionPath}' is empty."
                );
            }


            DataLayerDefinition definition;

            try
            {
                definition =
                    JsonUtility.FromJson<DataLayerDefinition>(
                        definitionJson
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to deserialize data layer " +
                    $"definition '{definitionPath}'.",
                    exception
                );
            }


            if (definition == null)
            {
                throw new InvalidDataException(
                    $"Data layer definition " +
                    $"'{definitionPath}' could not be parsed."
                );
            }


            ValidateDefinition(
                definition
            );


            // =====================================================
            // RESOLVE VALUES FILE
            // =====================================================

            string valuesPath;

            try
            {
                valuesPath =
                    RuntimeAssetReader.ResolveSiblingPath(
                        definitionPath,
                        definition.DataFile
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Invalid dataFile " +
                    $"'{definition.DataFile}' for data layer " +
                    $"'{definition.Id}'.",
                    exception
                );
            }


            // =====================================================
            // LOAD VALUES
            // =====================================================

            string valuesJson;

            try
            {
                valuesJson =
                    await assetReader.ReadTextAsync(
                        valuesPath,
                        cancellationToken
                    );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to read values file " +
                    $"'{valuesPath}' for data layer " +
                    $"'{definition.Id}'.",
                    exception
                );
            }


            cancellationToken
                .ThrowIfCancellationRequested();


            if (string.IsNullOrWhiteSpace(
                    valuesJson
                ))
            {
                throw new InvalidDataException(
                    $"Values file '{valuesPath}' for " +
                    $"data layer '{definition.Id}' is empty."
                );
            }


            DataLayerFileDto valuesFile;

            try
            {
                valuesFile =
                    JsonUtility.FromJson<DataLayerFileDto>(
                        valuesJson
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to deserialize values file " +
                    $"'{valuesPath}' for data layer " +
                    $"'{definition.Id}'.",
                    exception
                );
            }


            if (valuesFile == null)
            {
                throw new InvalidDataException(
                    $"Values file '{valuesPath}' for " +
                    $"data layer '{definition.Id}' " +
                    $"could not be parsed."
                );
            }


            cancellationToken
                .ThrowIfCancellationRequested();


            ValidateValuesFileHeader(
                valuesFile,
                definition
            );


            return BuildDataLayer(
                definition,
                valuesFile,
                cancellationToken
            );
        }


        // =========================================================
        // DEFINITION VALIDATION
        // =========================================================

        private static void ValidateDefinition(
            DataLayerDefinition definition
        )
        {
            if (string.IsNullOrWhiteSpace(
                    definition.SchemaVersion
                ))
            {
                throw new InvalidDataException(
                    "Data layer definition is missing schemaVersion."
                );
            }


            if (!string.Equals(
                    definition.SchemaVersion,
                    SupportedSchemaVersion,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidDataException(
                    $"Unsupported data layer schema version " +
                    $"'{definition.SchemaVersion}'. " +
                    $"Supported version is " +
                    $"'{SupportedSchemaVersion}'."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    definition.Id
                ))
            {
                throw new InvalidDataException(
                    "Data layer definition is missing an ID."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    definition.DisplayName
                ))
            {
                throw new InvalidDataException(
                    $"Data layer '{definition.Id}' " +
                    $"is missing displayName."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    definition.TargetSpatialLayerId
                ))
            {
                throw new InvalidDataException(
                    $"Data layer '{definition.Id}' " +
                    $"is missing targetSpatialLayerId."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    definition.DataFile
                ))
            {
                throw new InvalidDataException(
                    $"Data layer '{definition.Id}' " +
                    $"is missing dataFile."
                );
            }


            if (!definition.TryGetTemporalMode(
                    out DataTemporalMode temporalMode
                ))
            {
                throw new InvalidDataException(
                    $"Data layer '{definition.Id}' " +
                    $"contains an invalid temporalMode."
                );
            }


            if (temporalMode !=
                DataTemporalMode.Static)
            {
                throw new NotSupportedException(
                    $"Data layer '{definition.Id}' uses " +
                    $"temporal mode '{temporalMode}'. " +
                    $"Only Static data layers are currently supported."
                );
            }


            IReadOnlyList<DataVariableDefinition> variables =
                definition.Variables;


            if (variables == null ||
                variables.Count == 0)
            {
                throw new InvalidDataException(
                    $"Data layer '{definition.Id}' " +
                    $"defines no variables."
                );
            }


            var variableIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            for (
                int i = 0;
                i < variables.Count;
                i++
            )
            {
                DataVariableDefinition variable =
                    variables[i];


                if (variable == null)
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a null variable definition " +
                        $"at index {i}."
                    );
                }


                if (string.IsNullOrWhiteSpace(
                        variable.Id
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a variable without an ID."
                    );
                }


                if (!variableIds.Add(
                        variable.Id
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"defines duplicate variable " +
                        $"'{variable.Id}'."
                    );
                }


                if (!variable.TryGetValueType(
                        out _
                    ))
                {
                    throw new InvalidDataException(
                        $"Variable '{variable.Id}' in data layer " +
                        $"'{definition.Id}' contains an invalid " +
                        $"valueType."
                    );
                }
            }
        }


        // =========================================================
        // VALUES FILE VALIDATION
        // =========================================================

        private static void ValidateValuesFileHeader(
            DataLayerFileDto valuesFile,
            DataLayerDefinition definition
        )
        {
            if (string.IsNullOrWhiteSpace(
                    valuesFile.schemaVersion
                ))
            {
                throw new InvalidDataException(
                    $"Values file for data layer " +
                    $"'{definition.Id}' is missing schemaVersion."
                );
            }


            if (!string.Equals(
                    valuesFile.schemaVersion,
                    SupportedSchemaVersion,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidDataException(
                    $"Values file for data layer " +
                    $"'{definition.Id}' uses unsupported schema " +
                    $"version '{valuesFile.schemaVersion}'."
                );
            }


            if (!string.Equals(
                    valuesFile.dataLayerId,
                    definition.Id,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidDataException(
                    $"Data layer ID mismatch. " +
                    $"Definition declares '{definition.Id}', " +
                    $"but values file declares " +
                    $"'{valuesFile.dataLayerId}'."
                );
            }


            if (!string.Equals(
                    valuesFile.targetSpatialLayerId,
                    definition.TargetSpatialLayerId,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidDataException(
                    $"Target spatial layer mismatch for " +
                    $"data layer '{definition.Id}'. " +
                    $"Definition declares " +
                    $"'{definition.TargetSpatialLayerId}', " +
                    $"but values file declares " +
                    $"'{valuesFile.targetSpatialLayerId}'."
                );
            }
        }


        // =========================================================
        // RUNTIME CONSTRUCTION
        // =========================================================

        private static DataLayer BuildDataLayer(
            DataLayerDefinition definition,
            DataLayerFileDto valuesFile,
            CancellationToken cancellationToken
        )
        {
            string[] unitIds =
                valuesFile.unitIds
                ?? Array.Empty<string>();


            if (unitIds.Length == 0)
            {
                throw new InvalidDataException(
                    $"Data layer '{definition.Id}' " +
                    $"contains no unit IDs."
                );
            }


            ValidateUnitIds(
                definition.Id,
                unitIds,
                cancellationToken
            );


            DataColumnDto[] serializedColumnArray =
                valuesFile.columns
                ?? Array.Empty<DataColumnDto>();


            if (serializedColumnArray.Length == 0)
            {
                throw new InvalidDataException(
                    $"Data layer '{definition.Id}' " +
                    $"contains no value columns."
                );
            }


            var serializedColumns =
                new Dictionary<string, DataColumnDto>(
                    StringComparer.Ordinal
                );


            for (
                int i = 0;
                i < serializedColumnArray.Length;
                i++
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                DataColumnDto column =
                    serializedColumnArray[i];


                if (column == null)
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a null column at index {i}."
                    );
                }


                if (string.IsNullOrWhiteSpace(
                        column.variableId
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a column without variableId."
                    );
                }


                if (!serializedColumns.TryAdd(
                        column.variableId,
                        column
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"contains duplicate column " +
                        $"'{column.variableId}'."
                    );
                }
            }


            var runtimeColumns =
                new Dictionary<string, DataColumn>(
                    StringComparer.Ordinal
                );


            IReadOnlyList<DataVariableDefinition> variables =
                definition.Variables;


            for (
                int i = 0;
                i < variables.Count;
                i++
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                DataVariableDefinition variable =
                    variables[i];


                if (!serializedColumns.TryGetValue(
                        variable.Id,
                        out DataColumnDto serializedColumn
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"does not contain a values column for " +
                        $"variable '{variable.Id}'."
                    );
                }


                DataColumn runtimeColumn =
                    BuildColumn(
                        definition.Id,
                        variable,
                        serializedColumn,
                        unitIds.Length
                    );


                runtimeColumns.Add(
                    variable.Id,
                    runtimeColumn
                );
            }


            foreach (
                string serializedVariableId
                in serializedColumns.Keys
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                if (!definition.TryGetVariable(
                        serializedVariableId,
                        out _
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{definition.Id}' " +
                        $"contains values for undeclared variable " +
                        $"'{serializedVariableId}'."
                    );
                }
            }


            return new DataLayer(
                definition,
                unitIds,
                runtimeColumns
            );
        }


        // =========================================================
        // COLUMN CONSTRUCTION
        // =========================================================

        private static DataColumn BuildColumn(
            string dataLayerId,
            DataVariableDefinition variable,
            DataColumnDto dto,
            int rowCount
        )
        {
            if (!variable.TryGetValueType(
                    out DataValueType valueType
                ))
            {
                throw new InvalidDataException(
                    $"Variable '{variable.Id}' in data layer " +
                    $"'{dataLayerId}' has an invalid value type."
                );
            }


            bool[] validMask =
                dto.valid;


            if (validMask != null &&
                validMask.Length != 0 &&
                validMask.Length != rowCount)
            {
                throw new InvalidDataException(
                    $"Validity mask for variable " +
                    $"'{variable.Id}' contains " +
                    $"{validMask.Length} values, but " +
                    $"{rowCount} rows were expected."
                );
            }


            switch (valueType)
            {
                case DataValueType.Float:
                    {
                        double[] values =
                            dto.floatValues
                            ?? Array.Empty<double>();


                        ValidateValueCount(
                            dataLayerId,
                            variable.Id,
                            values.Length,
                            rowCount
                        );


                        return new FloatDataColumn(
                            values,
                            validMask
                        );
                    }


                case DataValueType.Integer:
                    {
                        long[] values =
                            dto.integerValues
                            ?? Array.Empty<long>();


                        ValidateValueCount(
                            dataLayerId,
                            variable.Id,
                            values.Length,
                            rowCount
                        );


                        return new IntegerDataColumn(
                            values,
                            validMask
                        );
                    }


                case DataValueType.Boolean:
                    {
                        bool[] values =
                            dto.booleanValues
                            ?? Array.Empty<bool>();


                        ValidateValueCount(
                            dataLayerId,
                            variable.Id,
                            values.Length,
                            rowCount
                        );


                        return new BooleanDataColumn(
                            values,
                            validMask
                        );
                    }


                case DataValueType.String:
                    {
                        string[] values =
                            dto.stringValues
                            ?? Array.Empty<string>();


                        ValidateValueCount(
                            dataLayerId,
                            variable.Id,
                            values.Length,
                            rowCount
                        );


                        return new StringDataColumn(
                            values,
                            validMask
                        );
                    }


                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(valueType),
                        valueType,
                        null
                    );
            }
        }


        // =========================================================
        // UNIT VALIDATION
        // =========================================================

        private static void ValidateUnitIds(
            string dataLayerId,
            string[] unitIds,
            CancellationToken cancellationToken
        )
        {
            var seenIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            for (
                int i = 0;
                i < unitIds.Length;
                i++
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                string unitId =
                    unitIds[i];


                if (string.IsNullOrWhiteSpace(
                        unitId
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{dataLayerId}' " +
                        $"contains an empty unit ID at row {i}."
                    );
                }


                if (!seenIds.Add(
                        unitId
                    ))
                {
                    throw new InvalidDataException(
                        $"Data layer '{dataLayerId}' " +
                        $"contains duplicate unit ID " +
                        $"'{unitId}'."
                    );
                }
            }
        }


        private static void ValidateValueCount(
            string dataLayerId,
            string variableId,
            int actual,
            int expected
        )
        {
            if (actual == expected)
            {
                return;
            }


            throw new InvalidDataException(
                $"Variable '{variableId}' in data layer " +
                $"'{dataLayerId}' contains {actual} values, " +
                $"but {expected} were expected."
            );
        }
    }
}