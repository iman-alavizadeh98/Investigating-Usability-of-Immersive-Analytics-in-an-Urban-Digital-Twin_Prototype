using System;
using UrbanAnalytics.Spatial.Geometry;

namespace UrbanAnalytics.Spatial
{
    /// <summary>
    /// Semantic runtime representation of one spatial unit.
    ///
    /// Examples:
    /// - one Ruta cell
    /// - one DeSO area
    /// - one neighborhood
    /// - one building
    /// - one sensor location
    /// - one road segment
    ///
    /// SpatialUnit does not contain analytical values and does
    /// not own Unity GameObjects.
    /// </summary>
    public sealed class SpatialUnit
    {
        /// <summary>
        /// Globally stable semantic identifier.
        ///
        /// Example:
        /// ruta_250:3175006390000
        /// </summary>
        public string Id
        {
            get;
        }


        /// <summary>
        /// ID of the SpatialLayer this unit belongs to.
        /// </summary>
        public string SpatialLayerId
        {
            get;
        }


        /// <summary>
        /// Optional human-readable label.
        /// </summary>
        public string DisplayName
        {
            get;
        }


        /// <summary>
        /// Optional semantic hierarchy.
        ///
        /// Example:
        /// neighborhood unit belonging to a district.
        /// </summary>
        public string ParentUnitId
        {
            get;
        }


        /// <summary>
        /// Centroid expressed in the project's source CRS.
        /// </summary>
        public SpatialCoordinate Centroid
        {
            get;
        }


        /// <summary>
        /// Source-CRS geometry of this unit.
        /// </summary>
        public ISpatialGeometry Geometry
        {
            get;
        }


        public SpatialBounds Bounds =>
            Geometry.Bounds;


        public SpatialGeometryType GeometryType =>
            Geometry.GeometryType;


        public SpatialUnit(
            string id,
            string spatialLayerId,
            SpatialCoordinate centroid,
            ISpatialGeometry geometry,
            string displayName = null,
            string parentUnitId = null
        )
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Spatial unit ID cannot be null or empty.",
                    nameof(id)
                );
            }


            if (string.IsNullOrWhiteSpace(
                    spatialLayerId
                ))
            {
                throw new ArgumentException(
                    "Spatial layer ID cannot be null or empty.",
                    nameof(spatialLayerId)
                );
            }


            Geometry =
                geometry
                ?? throw new ArgumentNullException(
                    nameof(geometry)
                );


            Id =
                id.Trim();


            SpatialLayerId =
                spatialLayerId.Trim();


            DisplayName =
                string.IsNullOrWhiteSpace(
                    displayName
                )
                    ? Id
                    : displayName.Trim();


            ParentUnitId =
                string.IsNullOrWhiteSpace(
                    parentUnitId
                )
                    ? null
                    : parentUnitId.Trim();


            Centroid =
                centroid;
        }


        public override string ToString()
        {
            return
                $"{DisplayName} " +
                $"[{Id}] " +
                $"({GeometryType})";
        }
    }
}