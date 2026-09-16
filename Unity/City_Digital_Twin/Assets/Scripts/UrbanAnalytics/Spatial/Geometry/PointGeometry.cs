namespace UrbanAnalytics.Spatial.Geometry
{
    public sealed class PointGeometry
        : ISpatialGeometry
    {
        public SpatialCoordinate Coordinate
        {
            get;
        }


        public SpatialGeometryType GeometryType =>
            SpatialGeometryType.Point;


        public SpatialBounds Bounds
        {
            get;
        }


        public int CoordinateCount =>
            1;


        public PointGeometry(
            SpatialCoordinate coordinate
        )
        {
            Coordinate = coordinate;

            Bounds = new SpatialBounds(
                coordinate.Easting,
                coordinate.Northing,
                coordinate.Elevation,

                coordinate.Easting,
                coordinate.Northing,
                coordinate.Elevation
            );
        }
    }
}