using System;

namespace UrbanAnalytics.Spatial
{
    public enum SpatialGeometryType
    {
        Polygon,
        MultiPolygon,
        Point,
        MultiPoint,
        LineString,
        MultiLineString
    }

    public enum SpatialGeometryMode
    {
        Procedural,
        Precomputed,
        Hybrid
    }


    /// <summary>
    /// Serializable representation of a spatial layer definition
    /// as stored in layer.json.
    ///
    /// This class is only used for deserialization.
    /// Runtime systems should use SpatialLayerDefinition.
    /// </summary>
    [Serializable]
    public class SpatialLayerDefinitionDto
    {
        public string id;
        public string displayName;
        public string description;

        public string geometryType;
        public string geometryMode;

        public string geometrySource;

        public int unitCount;

        public bool visibleByDefault = true;
        public bool selectable = true;
        public bool extractable = true;
    }


    /// <summary>
    /// Validated runtime definition of a spatial analytical layer.
    ///
    /// Examples:
    /// - Ruta grid
    /// - DeSO
    /// - neighborhoods
    /// - custom polygons
    /// - sensor points
    /// - road segments
    ///
    /// Analytical values do not belong here.
    /// They are managed separately by the data-layer system.
    /// </summary>
    public sealed class SpatialLayerDefinition
    {
        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public SpatialGeometryType GeometryType { get; }

        public SpatialGeometryMode GeometryMode { get; }

        public string GeometrySource { get; }

        public int UnitCount { get; }

        public bool VisibleByDefault { get; }

        public bool Selectable { get; }

        public bool Extractable { get; }


        private SpatialLayerDefinition(
            string id,
            string displayName,
            string description,
            SpatialGeometryType geometryType,
            SpatialGeometryMode geometryMode,
            string geometrySource,
            int unitCount,
            bool visibleByDefault,
            bool selectable,
            bool extractable
        )
        {
            Id = id;
            DisplayName = displayName;
            Description = description;

            GeometryType = geometryType;
            GeometryMode = geometryMode;

            GeometrySource = geometrySource;

            UnitCount = unitCount;

            VisibleByDefault = visibleByDefault;
            Selectable = selectable;
            Extractable = extractable;
        }


        /// <summary>
        /// Creates and validates a runtime definition from
        /// a deserialized JSON DTO.
        /// </summary>
        public static SpatialLayerDefinition FromDto(
            SpatialLayerDefinitionDto dto
        )
        {
            if (dto == null)
            {
                throw new ArgumentNullException(
                    nameof(dto),
                    "Spatial layer definition is null."
                );
            }

            ValidateRequiredString(
                dto.id,
                nameof(dto.id)
            );

            ValidateRequiredString(
                dto.displayName,
                nameof(dto.displayName)
            );

            ValidateRequiredString(
                dto.geometryType,
                nameof(dto.geometryType)
            );

            ValidateRequiredString(
                dto.geometryMode,
                nameof(dto.geometryMode)
            );

            ValidateRequiredString(
                dto.geometrySource,
                nameof(dto.geometrySource)
            );


            if (dto.unitCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dto.unitCount),
                    dto.unitCount,
                    "Spatial layer unit count cannot be negative."
                );
            }


            if (!Enum.TryParse(
                    dto.geometryType,
                    true,
                    out SpatialGeometryType geometryType
                ))
            {
                throw new ArgumentException(
                    $"Unsupported geometryType " +
                    $"'{dto.geometryType}' " +
                    $"for spatial layer '{dto.id}'."
                );
            }


            if (!Enum.TryParse(
                    dto.geometryMode,
                    true,
                    out SpatialGeometryMode geometryMode
                ))
            {
                throw new ArgumentException(
                    $"Unsupported geometryMode " +
                    $"'{dto.geometryMode}' " +
                    $"for spatial layer '{dto.id}'."
                );
            }


            return new SpatialLayerDefinition(
                dto.id.Trim(),
                dto.displayName.Trim(),
                dto.description?.Trim() ?? string.Empty,

                geometryType,
                geometryMode,

                dto.geometrySource.Trim(),

                dto.unitCount,

                dto.visibleByDefault,
                dto.selectable,
                dto.extractable
            );
        }


        private static void ValidateRequiredString(
            string value,
            string fieldName
        )
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"Required spatial-layer field " +
                    $"'{fieldName}' is missing or empty."
                );
            }
        }


        public override string ToString()
        {
            return
                $"{DisplayName} " +
                $"[{Id}] - " +
                $"{GeometryType}, " +
                $"{GeometryMode}, " +
                $"{UnitCount} units";
        }
    }
}