using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Core.IO;
using UrbanAnalytics.Data.Serialization;

namespace UrbanAnalytics.Data
{
    /// <summary>
    /// Loads and validates analytical DataLayers from StreamingAssets.
    ///
    /// Loading flow:
    ///
    /// layer.json
    ///     ↓
    /// DataLayerDefinition
    ///     ↓
    /// values.json
    ///     ↓
    /// DataLayerFileDto
    ///     ↓
    /// validated runtime DataLayer
    /// </summary>
    public sealed class DataLayerLoader
    {
        private readonly RuntimeAssetReader assetReader;


        public DataLayerLoader(
            RuntimeAssetReader assetReader)
        {
            this.assetReader = assetReader
                ?? throw new ArgumentNullException(
                    nameof(assetReader)
                );
        }


        /// <summary>
        /// Loads a DataLayer definition and its associated values file.
        ///
        /// The supplied path is relative to StreamingAssets.
        ///
        /// Example:
        /// data_layers/income_2023/layer.json
        /// </summary>
        public async Task<DataLayer> LoadAsync(
            string definitionPath)
        {
            if (string.IsNullOrWhiteSpace(definitionPath))
            {
                throw new ArgumentException(
                    "Data layer definition path cannot be empty.",
                    nameof(definitionPath)
                );
            }

            string normalizedDefinitionPath =
                NormalizePath(definitionPath);


            // -----------------------------------------------------
            // Load definition
            // -----------------------------------------------------

            string definitionJson =
                await assetReader.ReadTextAsync(
                    normalizedDefinitionPath
                );

            if (string.IsNullOrWhiteSpace(definitionJson))
            {
                throw new InvalidOperationException(
                    $"Data layer definition is empty: " +
                    $"{normalizedDefinitionPath}"
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
                throw new InvalidOperationException(
                    $"Failed to parse data layer definition: " +
                    $"{normalizedDefinitionPath}",
                    exception
                );
            }

            ValidateDefinition(definition);


            // -----------------------------------------------------
            // Resolve values file
            // -----------------------------------------------------

            string valuesPath =
                ResolveRelativePath(
                    normalizedDefinitionPath,
                    definition.DataFile
                );


            // -----------------------------------------------------
            // Load values
            // -----------------------------------------------------

            string valuesJson =
                await assetReader.ReadTextAsync(
                    valuesPath
                );

            if (string.IsNullOrWhiteSpace(valuesJson))
            {
                throw new InvalidOperationException(
                    $"Data values file is empty: {valuesPath}"
                );
            }

            DataLayerFileDto valuesDto;

            try
            {
                valuesDto =
                    JsonUtility.FromJson<DataLayerFileDto>(
                        valuesJson
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Failed to parse data values file: " +
                    $"{valuesPath}",
                    exception
                );
            }


            // -----------------------------------------------------
            // Validate and construct runtime layer
            // -----------------------------------------------------

            return BuildDataLayer(
                definition,
                valuesDto
            );
        }


        // =========================================================
        // Runtime construction
        // =========================================================

        private static DataLayer BuildDataLayer(
            DataLayerDefinition definition,
            DataLayerFileDto valuesDto)
        {
            if (valuesDto == null)
            {
                throw new InvalidOperationException(
                    $"Values DTO for data layer " +
                    $"'{definition.Id}' is null."
                );
            }


            // -----------------------------------------------------
            // Validate layer identity
            // -----------------------------------------------------

            if (!string.Equals(
                    valuesDto.dataLayerId,
                    definition.Id,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Data layer ID mismatch. " +
                    $"Definition='{definition.Id}', " +
                    $"Values='{valuesDto.dataLayerId}'."
                );
            }


            // -----------------------------------------------------
            // Validate target spatial layer
            // -----------------------------------------------------

            if (!string.Equals(
                    valuesDto.targetSpatialLayerId,
                    definition.TargetSpatialLayerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Target spatial layer mismatch for " +
                    $"data layer '{definition.Id}'. " +
                    $"Definition=" +
                    $"'{definition.TargetSpatialLayerId}', " +
                    $"Values=" +
                    $"'{valuesDto.targetSpatialLayerId}'."
                );
            }


            // -----------------------------------------------------
            // Validate unit IDs
            // -----------------------------------------------------

            string[] unitIds =
                valuesDto.unitIds
                ?? Array.Empty<string>();

            if (unitIds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Data layer '{definition.Id}' " +
                    $"contains no unit IDs."
                );
            }

            ValidateUnitIds(
                definition.Id,
                unitIds
            );


            // -----------------------------------------------------
            // Index serialized columns
            // -----------------------------------------------------

            DataColumnDto[] columnDtos =
                valuesDto.columns
                ?? Array.Empty<DataColumnDto>();

            Dictionary<string, DataColumnDto>
                serializedColumns =
                    new Dictionary<string, DataColumnDto>(
                        StringComparer.Ordinal
                    );

            for (int i = 0; i < columnDtos.Length; i++)
            {
                DataColumnDto column =
                    columnDtos[i];

                if (column == null)
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a null column at index {i}."
                    );
                }

                if (string.IsNullOrWhiteSpace(
                        column.variableId))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a column without a variable ID."
                    );
                }

                if (!serializedColumns.TryAdd(
                        column.variableId,
                        column))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"contains duplicate column " +
                        $"'{column.variableId}'."
                    );
                }
            }


            // -----------------------------------------------------
            // Build runtime columns
            // -----------------------------------------------------

            Dictionary<string, DataColumn>
                runtimeColumns =
                    new Dictionary<string, DataColumn>(
                        StringComparer.Ordinal
                    );

            IReadOnlyList<DataVariableDefinition>
                variableDefinitions =
                    definition.Variables;

            if (variableDefinitions == null ||
                variableDefinitions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Data layer '{definition.Id}' " +
                    $"defines no variables."
                );
            }

            HashSet<string> definedVariableIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            for (int i = 0;
                 i < variableDefinitions.Count;
                 i++)
            {
                DataVariableDefinition variable =
                    variableDefinitions[i];

                if (variable == null)
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a null variable definition."
                    );
                }

                if (string.IsNullOrWhiteSpace(
                        variable.Id))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"contains a variable without an ID."
                    );
                }

                if (!definedVariableIds.Add(
                        variable.Id))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"defines duplicate variable " +
                        $"'{variable.Id}'."
                    );
                }


                // -------------------------------------------------
                // Find matching serialized column
                // -------------------------------------------------

                if (!serializedColumns.TryGetValue(
                        variable.Id,
                        out DataColumnDto columnDto))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"does not contain values for variable " +
                        $"'{variable.Id}'."
                    );
                }


                // -------------------------------------------------
                // Convert serialized column → runtime column
                // -------------------------------------------------

                DataColumn runtimeColumn =
                    BuildColumn(
                        definition.Id,
                        variable,
                        columnDto,
                        unitIds.Length
                    );

                runtimeColumns.Add(
                    variable.Id,
                    runtimeColumn
                );
            }


            // -----------------------------------------------------
            // Detect undeclared columns
            // -----------------------------------------------------

            foreach (
                string serializedVariableId
                in serializedColumns.Keys)
            {
                if (!definedVariableIds.Contains(
                        serializedVariableId))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{definition.Id}' " +
                        $"contains values for undeclared variable " +
                        $"'{serializedVariableId}'."
                    );
                }
            }


            // -----------------------------------------------------
            // Construct final runtime layer
            // -----------------------------------------------------

            return new DataLayer(
                definition,
                unitIds,
                runtimeColumns
            );
        }


        // =========================================================
        // Column construction
        // =========================================================

        private static DataColumn BuildColumn(
            string dataLayerId,
            DataVariableDefinition variable,
            DataColumnDto dto,
            int rowCount)
        {
            if (!variable.TryGetValueType(
                    out DataValueType valueType))
            {
                throw new InvalidOperationException(
                    $"Variable '{variable.Id}' in data layer " +
                    $"'{dataLayerId}' has unsupported value type."
                );
            }


            // -----------------------------------------------------
            // Validate optional no-data mask
            // -----------------------------------------------------

            bool[] validMask =
                dto.valid;

            if (validMask != null &&
                validMask.Length != 0 &&
                validMask.Length != rowCount)
            {
                throw new InvalidOperationException(
                    $"Validity mask for variable " +
                    $"'{variable.Id}' contains " +
                    $"{validMask.Length} values but " +
                    $"expected {rowCount}."
                );
            }


            // -----------------------------------------------------
            // Build typed runtime column
            // -----------------------------------------------------

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
        // Definition validation
        // =========================================================

        private static void ValidateDefinition(
            DataLayerDefinition definition)
        {
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "Data layer definition is null."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    definition.Id))
            {
                throw new InvalidOperationException(
                    "Data layer definition has no ID."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    definition.TargetSpatialLayerId))
            {
                throw new InvalidOperationException(
                    $"Data layer '{definition.Id}' " +
                    $"has no target spatial layer ID."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    definition.DataFile))
            {
                throw new InvalidOperationException(
                    $"Data layer '{definition.Id}' " +
                    $"has no data file."
                );
            }


            if (!definition.TryGetTemporalMode(
                    out DataTemporalMode temporalMode))
            {
                throw new InvalidOperationException(
                    $"Data layer '{definition.Id}' " +
                    $"has an invalid temporal mode."
                );
            }


            // For now, only static datasets are supported.
            // TimeSeries will be added after the static
            // visualization pipeline is working.
            if (temporalMode != DataTemporalMode.Static)
            {
                throw new NotSupportedException(
                    $"Data layer '{definition.Id}' uses " +
                    $"temporal mode '{temporalMode}'. " +
                    $"Time-series loading has not yet " +
                    $"been implemented."
                );
            }


            if (definition.Variables == null ||
                definition.Variables.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Data layer '{definition.Id}' " +
                    $"defines no variables."
                );
            }
        }


        // =========================================================
        // Unit validation
        // =========================================================

        private static void ValidateUnitIds(
            string dataLayerId,
            string[] unitIds)
        {
            HashSet<string> ids =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            for (int i = 0;
                 i < unitIds.Length;
                 i++)
            {
                string id =
                    unitIds[i];


                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{dataLayerId}' " +
                        $"contains an empty unit ID " +
                        $"at row {i}."
                    );
                }


                if (!ids.Add(id))
                {
                    throw new InvalidOperationException(
                        $"Data layer '{dataLayerId}' " +
                        $"contains duplicate unit ID '{id}'."
                    );
                }
            }
        }


        // =========================================================
        // Value count validation
        // =========================================================

        private static void ValidateValueCount(
            string dataLayerId,
            string variableId,
            int actual,
            int expected)
        {
            if (actual == expected)
                return;


            throw new InvalidOperationException(
                $"Variable '{variableId}' in data layer " +
                $"'{dataLayerId}' contains {actual} values " +
                $"but expected {expected}."
            );
        }


        // =========================================================
        // Path resolution
        // =========================================================

        /// <summary>
        /// Resolves a file referenced from a layer definition.
        ///
        /// Example:
        ///
        /// definition:
        /// data_layers/income_2023/layer.json
        ///
        /// referenced file:
        /// values.json
        ///
        /// result:
        /// data_layers/income_2023/values.json
        /// </summary>
        private static string ResolveRelativePath(
            string definitionPath,
            string referencedFile)
        {
            string normalizedFile =
                NormalizePath(referencedFile);


            int lastSlash =
                definitionPath.LastIndexOf('/');


            if (lastSlash < 0)
                return normalizedFile;


            string directory =
                definitionPath.Substring(
                    0,
                    lastSlash
                );


            return $"{directory}/{normalizedFile}";
        }


        /// <summary>
        /// Normalizes runtime package paths to use
        /// forward slashes and removes leading slashes.
        /// </summary>
        private static string NormalizePath(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;


            return path
                .Replace('\\', '/')
                .TrimStart('/');
        }
    }
}