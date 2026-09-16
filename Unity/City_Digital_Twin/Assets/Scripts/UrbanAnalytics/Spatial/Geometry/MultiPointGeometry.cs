using System;
using System.Collections.Generic;
using System.Linq;

namespace UrbanAnalytics.Spatial.Geometry
{
    public sealed class MultiPointGeometry
        : ISpatialGeometry
    {
        private readonly PointGeometry[] points;


        public IReadOnlyList<PointGeometry> Points =>
            points;


        public SpatialGeometryType GeometryType =>
            SpatialGeometryType.MultiPoint;


        public SpatialBounds Bounds
        {
            get;
        }


        public int CoordinateCount =>
            points.Length;


        public MultiPointGeometry(
            IEnumerable<PointGeometry> points
        )
        {
            if (points == null)
            {
                throw new ArgumentNullException(
                    nameof(points)
                );
            }


            this.points =
                points.ToArray();


            if (this.points.Length == 0)
            {
                throw new ArgumentException(
                    "MultiPoint requires at least one point.",
                    nameof(points)
                );
            }


            if (this.points.Any(
                    point => point == null
                ))
            {
                throw new ArgumentException(
                    "MultiPoint cannot contain null geometries.",
                    nameof(points)
                );
            }


            Bounds =
                SpatialBounds.FromCoordinates(
                    this.points.Select(
                        point =>
                            point.Coordinate
                    )
                );
        }
    }
}