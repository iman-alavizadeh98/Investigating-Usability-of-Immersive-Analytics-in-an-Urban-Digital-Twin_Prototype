using System;
using System.Collections.Generic;
using System.Linq;

namespace UrbanAnalytics.Spatial.Geometry
{
    public sealed class MultiPolygonGeometry
        : ISpatialGeometry
    {
        private readonly PolygonGeometry[] polygons;


        public IReadOnlyList<PolygonGeometry> Polygons =>
            polygons;


        public SpatialGeometryType GeometryType =>
            SpatialGeometryType.MultiPolygon;


        public SpatialBounds Bounds
        {
            get;
        }


        public int CoordinateCount
        {
            get;
        }


        public double Area
        {
            get;
        }


        public MultiPolygonGeometry(
            IEnumerable<PolygonGeometry> polygons
        )
        {
            if (polygons == null)
            {
                throw new ArgumentNullException(
                    nameof(polygons)
                );
            }


            this.polygons =
                polygons.ToArray();


            if (this.polygons.Length == 0)
            {
                throw new ArgumentException(
                    "MultiPolygon requires at least one polygon.",
                    nameof(polygons)
                );
            }


            if (this.polygons.Any(
                    polygon => polygon == null
                ))
            {
                throw new ArgumentException(
                    "MultiPolygon cannot contain null geometries.",
                    nameof(polygons)
                );
            }


            CoordinateCount =
                this.polygons.Sum(
                    polygon =>
                        polygon.CoordinateCount
                );


            Area =
                this.polygons.Sum(
                    polygon =>
                        polygon.Area
                );


            IEnumerable<SpatialCoordinate>
                allCoordinates =
                    this.polygons
                    .SelectMany(
                        polygon =>
                            polygon
                            .Exterior
                            .Coordinates
                    );


            Bounds =
                SpatialBounds.FromCoordinates(
                    allCoordinates
                );
        }
    }
}