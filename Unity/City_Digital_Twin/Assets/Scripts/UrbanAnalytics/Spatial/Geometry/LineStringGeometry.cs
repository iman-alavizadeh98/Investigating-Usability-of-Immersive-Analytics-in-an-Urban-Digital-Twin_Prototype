using System;
using System.Collections.Generic;
using System.Linq;

namespace UrbanAnalytics.Spatial.Geometry
{
    public sealed class LineStringGeometry
        : ISpatialGeometry
    {
        private readonly SpatialCoordinate[] coordinates;


        public IReadOnlyList<SpatialCoordinate> Coordinates =>
            coordinates;


        public SpatialGeometryType GeometryType =>
            SpatialGeometryType.LineString;


        public SpatialBounds Bounds
        {
            get;
        }


        public int CoordinateCount =>
            coordinates.Length;


        public LineStringGeometry(
            IEnumerable<SpatialCoordinate> coordinates
        )
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(
                    nameof(coordinates)
                );
            }


            this.coordinates =
                coordinates.ToArray();


            if (this.coordinates.Length < 2)
            {
                throw new ArgumentException(
                    "A LineString requires at least two coordinates.",
                    nameof(coordinates)
                );
            }


            Bounds =
                SpatialBounds.FromCoordinates(
                    this.coordinates
                );
        }
    }
}