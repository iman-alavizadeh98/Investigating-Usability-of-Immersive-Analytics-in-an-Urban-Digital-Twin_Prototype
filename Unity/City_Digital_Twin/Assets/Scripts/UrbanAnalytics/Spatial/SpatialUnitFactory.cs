using System;
using UrbanAnalytics.Spatial.Geometry;
using UrbanAnalytics.Spatial.Serialization;

namespace UrbanAnalytics.Spatial
{
    /// <summary>
    /// Converts serialized SpatialUnit DTOs into validated
    /// runtime SpatialUnit objects.
    /// </summary>
    public static class SpatialUnitFactory
    {
        public static SpatialUnit Create(
            SpatialUnitDto dto,
            SpatialLayerDefinition layerDefinition
        )
        {
            if (dto == null)
            {
                throw new ArgumentNullException(
                    nameof(dto)
                );
            }

            if (layerDefinition == null)
            {
                throw new ArgumentNullException(
                    nameof(layerDefinition)
                );
            }

            ValidateId(
                dto.id,
                layerDefinition.Id
            );

            if (dto.centroid == null)
            {
                throw new ArgumentException(
                    $"Spatial unit '{dto.id}' is missing its centroid."
                );
            }

            if (dto.geometry == null)
            {
                throw new ArgumentException(
                    $"Spatial unit '{dto.id}' is missing its geometry."
                );
            }

            SpatialCoordinate centroid =
                SpatialGeometryFactory.CreateCoordinate(
                    dto.centroid
                );

            ISpatialGeometry geometry =
                SpatialGeometryFactory.Create(
                    dto.geometry,
                    layerDefinition.GeometryType
                );

            ValidateCentroidAgainstGeometry(
                dto.id,
                centroid,
                geometry
            );

            return new SpatialUnit(
                dto.id.Trim(),
                layerDefinition.Id,
                centroid,
                geometry,
                dto.displayName,
                dto.parentUnitId
            );
        }


        private static void ValidateId(
            string id,
            string spatialLayerId
        )
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Spatial unit ID cannot be null or empty."
                );
            }

            string trimmedId =
                id.Trim();

            string expectedPrefix =
                spatialLayerId + ":";

            if (!trimmedId.StartsWith(
                    expectedPrefix,
                    StringComparison.Ordinal
                ))
            {
                throw new ArgumentException(
                    $"Spatial unit ID '{trimmedId}' does not follow " +
                    $"the required semantic ID convention for layer " +
                    $"'{spatialLayerId}'. Expected prefix " +
                    $"'{expectedPrefix}'."
                );
            }
        }


        private static void ValidateCentroidAgainstGeometry(
            string unitId,
            SpatialCoordinate centroid,
            ISpatialGeometry geometry
        )
        {
            SpatialBounds bounds =
                geometry.Bounds;

            if (!bounds.Contains(centroid))
            {
                throw new ArgumentException(
                    $"Spatial unit '{unitId}' has a centroid " +
                    $"outside its geometry bounds. " +
                    $"Centroid: {centroid}. Bounds: {bounds}."
                );
            }
        }
    }
}