using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Core.IO;
using UrbanAnalytics.Spatial.Serialization;

namespace UrbanAnalytics.Spatial
{
    /// <summary>
    /// Loads and validates a spatial layer package from
    /// the runtime package stored in StreamingAssets.
    ///
    /// Expected structure:
    ///
    /// spatial_layers/
    /// └── layer_name/
    ///     ├── layer.json
    ///     └── geometry.json
    ///
    /// The loader is platform-independent and uses
    /// RuntimeAssetReader so it works in:
    /// - Unity Editor
    /// - Windows
    /// - Android / Meta Quest
    /// </summary>
    public static class SpatialLayerLoader
    {
        public const string SupportedSchemaVersion =
            "1.0";


        /// <summary>
        /// Loads one complete spatial layer.
        /// </summary>
        public static async Task<SpatialLayer> LoadAsync(
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
            // LOAD LAYER DEFINITION
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
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to read spatial layer definition " +
                    $"'{definitionPath}'.",
                    exception
                );
            }


            cancellationToken.ThrowIfCancellationRequested();


            if (string.IsNullOrWhiteSpace(
                    definitionJson
                ))
            {
                throw new InvalidDataException(
                    $"Spatial layer definition " +
                    $"'{definitionPath}' is empty."
                );
            }


            SpatialLayerDefinitionDto definitionDto;

            try
            {
                definitionDto =
                    JsonUtility.FromJson
                        <SpatialLayerDefinitionDto>(
                            definitionJson
                        );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to deserialize spatial layer " +
                    $"definition '{definitionPath}'.",
                    exception
                );
            }


            if (definitionDto == null)
            {
                throw new InvalidDataException(
                    $"Spatial layer definition " +
                    $"'{definitionPath}' could not be parsed."
                );
            }


            SpatialLayerDefinition definition;

            try
            {
                definition =
                    SpatialLayerDefinition.FromDto(
                        definitionDto
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Spatial layer definition " +
                    $"'{definitionPath}' is invalid.",
                    exception
                );
            }


            // =====================================================
            // RESOLVE + LOAD GEOMETRY FILE
            // =====================================================

            string geometryPath;

            try
            {
                geometryPath =
                    RuntimeAssetReader.ResolveSiblingPath(
                        definitionPath,
                        definition.GeometrySource
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Invalid geometrySource " +
                    $"'{definition.GeometrySource}' " +
                    $"for spatial layer '{definition.Id}'.",
                    exception
                );
            }


            string geometryJson;

            try
            {
                geometryJson =
                    await assetReader.ReadTextAsync(
                        geometryPath,
                        cancellationToken
                    );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to read geometry file " +
                    $"'{geometryPath}' for spatial layer " +
                    $"'{definition.Id}'.",
                    exception
                );
            }


            cancellationToken.ThrowIfCancellationRequested();


            if (string.IsNullOrWhiteSpace(
                    geometryJson
                ))
            {
                throw new InvalidDataException(
                    $"Geometry file '{geometryPath}' for " +
                    $"spatial layer '{definition.Id}' is empty."
                );
            }


            SpatialGeometryFileDto geometryFile;

            try
            {
                geometryFile =
                    JsonUtility.FromJson
                        <SpatialGeometryFileDto>(
                            geometryJson
                        );
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Failed to deserialize geometry file " +
                    $"'{geometryPath}' for spatial layer " +
                    $"'{definition.Id}'.",
                    exception
                );
            }


            ValidateGeometryFile(
                geometryFile,
                definition
            );


            // =====================================================
            // CREATE RUNTIME SPATIAL UNITS
            // =====================================================

            var units =
                new List<SpatialUnit>(
                    geometryFile.units.Length
                );


            var seenIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            for (
                int i = 0;
                i < geometryFile.units.Length;
                i++
            )
            {
                cancellationToken.ThrowIfCancellationRequested();


                SpatialUnitDto unitDto =
                    geometryFile.units[i];


                if (unitDto == null)
                {
                    throw new InvalidDataException(
                        $"Spatial layer '{definition.Id}' " +
                        $"contains a null unit at index {i}."
                    );
                }


                SpatialUnit unit;

                try
                {
                    unit =
                        SpatialUnitFactory.Create(
                            unitDto,
                            definition
                        );
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        $"Failed to create spatial unit at " +
                        $"index {i} in layer " +
                        $"'{definition.Id}'.",
                        exception
                    );
                }


                if (!seenIds.Add(
                        unit.Id
                    ))
                {
                    throw new InvalidDataException(
                        $"Spatial layer '{definition.Id}' " +
                        $"contains duplicate unit ID " +
                        $"'{unit.Id}'."
                    );
                }


                units.Add(
                    unit
                );
            }


            // =====================================================
            // SEMANTIC VALIDATION
            // =====================================================

            ValidateParentReferences(
                units,
                definition
            );


            // =====================================================
            // CREATE COMPLETE RUNTIME LAYER
            // =====================================================

            return new SpatialLayer(
                definition,
                units
            );
        }


        // =========================================================
        // GEOMETRY FILE VALIDATION
        // =========================================================

        private static void ValidateGeometryFile(
            SpatialGeometryFileDto geometryFile,
            SpatialLayerDefinition definition
        )
        {
            if (geometryFile == null)
            {
                throw new InvalidDataException(
                    $"Geometry file for spatial layer " +
                    $"'{definition.Id}' could not be parsed."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    geometryFile.schemaVersion
                ))
            {
                throw new InvalidDataException(
                    $"Geometry file for spatial layer " +
                    $"'{definition.Id}' does not declare " +
                    $"schemaVersion."
                );
            }


            if (!string.Equals(
                    geometryFile.schemaVersion,
                    SupportedSchemaVersion,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidDataException(
                    $"Spatial layer '{definition.Id}' uses " +
                    $"geometry schema version " +
                    $"'{geometryFile.schemaVersion}', but " +
                    $"runtime version " +
                    $"'{SupportedSchemaVersion}' is required."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    geometryFile.spatialLayerId
                ))
            {
                throw new InvalidDataException(
                    $"Geometry file does not declare " +
                    $"spatialLayerId for layer " +
                    $"'{definition.Id}'."
                );
            }


            if (!string.Equals(
                    geometryFile.spatialLayerId,
                    definition.Id,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidDataException(
                    $"Geometry file declares spatial layer " +
                    $"'{geometryFile.spatialLayerId}', but " +
                    $"layer.json declares " +
                    $"'{definition.Id}'."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    geometryFile.geometryType
                ))
            {
                throw new InvalidDataException(
                    $"Geometry file for spatial layer " +
                    $"'{definition.Id}' does not declare " +
                    $"geometryType."
                );
            }


            if (!Enum.TryParse(
                    geometryFile.geometryType,
                    true,
                    out SpatialGeometryType geometryFileType
                ))
            {
                throw new InvalidDataException(
                    $"Geometry file for spatial layer " +
                    $"'{definition.Id}' declares unsupported " +
                    $"geometry type " +
                    $"'{geometryFile.geometryType}'."
                );
            }


            if (geometryFileType !=
                definition.GeometryType)
            {
                throw new InvalidDataException(
                    $"Geometry type mismatch for spatial layer " +
                    $"'{definition.Id}'. " +
                    $"layer.json declares " +
                    $"'{definition.GeometryType}', while " +
                    $"geometry.json declares " +
                    $"'{geometryFileType}'."
                );
            }


            if (geometryFile.units == null)
            {
                throw new InvalidDataException(
                    $"Geometry file for spatial layer " +
                    $"'{definition.Id}' has no units array."
                );
            }


            if (geometryFile.units.Length == 0)
            {
                throw new InvalidDataException(
                    $"Spatial layer '{definition.Id}' " +
                    $"contains zero spatial units."
                );
            }


            if (geometryFile.unitCount < 0)
            {
                throw new InvalidDataException(
                    $"Geometry file for spatial layer " +
                    $"'{definition.Id}' has invalid unitCount " +
                    $"{geometryFile.unitCount}."
                );
            }


            if (geometryFile.unitCount !=
                geometryFile.units.Length)
            {
                throw new InvalidDataException(
                    $"Geometry file for spatial layer " +
                    $"'{definition.Id}' declares " +
                    $"{geometryFile.unitCount} units but " +
                    $"contains {geometryFile.units.Length}."
                );
            }


            if (definition.UnitCount !=
                geometryFile.unitCount)
            {
                throw new InvalidDataException(
                    $"Unit-count mismatch for spatial layer " +
                    $"'{definition.Id}'. " +
                    $"layer.json declares " +
                    $"{definition.UnitCount}, while " +
                    $"geometry.json declares " +
                    $"{geometryFile.unitCount}."
                );
            }
        }


        // =========================================================
        // PARENT / HIERARCHY VALIDATION
        // =========================================================

        private static void ValidateParentReferences(
            IReadOnlyList<SpatialUnit> units,
            SpatialLayerDefinition definition
        )
        {
            var ids =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            foreach (SpatialUnit unit in units)
            {
                ids.Add(
                    unit.Id
                );
            }


            string sameLayerPrefix =
                definition.Id + ":";


            foreach (SpatialUnit unit in units)
            {
                if (string.IsNullOrWhiteSpace(
                        unit.ParentUnitId
                    ))
                {
                    continue;
                }


                if (string.Equals(
                        unit.Id,
                        unit.ParentUnitId,
                        StringComparison.Ordinal
                    ))
                {
                    throw new InvalidDataException(
                        $"Spatial unit '{unit.Id}' cannot " +
                        $"reference itself as its parent."
                    );
                }


                /*
                 * Parent IDs may refer to another spatial layer.
                 *
                 * Example:
                 * a neighborhood could reference a district.
                 *
                 * We can only verify the reference here when
                 * the parent belongs to this same layer.
                 */

                if (unit.ParentUnitId.StartsWith(
                        sameLayerPrefix,
                        StringComparison.Ordinal
                    ) &&
                    !ids.Contains(
                        unit.ParentUnitId
                    ))
                {
                    throw new InvalidDataException(
                        $"Spatial unit '{unit.Id}' references " +
                        $"missing parent unit " +
                        $"'{unit.ParentUnitId}'."
                    );
                }
            }
        }
    }
}