using UrbanAnalytics.Spatial;

namespace UrbanAnalytics.Spatial.Geometry
{
    /// <summary>
    /// Base contract for all spatial geometry used by the
    /// urban analytics runtime.
    ///
    /// Geometry coordinates remain in the project's source CRS.
    /// </summary>
    public interface ISpatialGeometry
    {
        SpatialGeometryType GeometryType
        {
            get;
        }

        SpatialBounds Bounds
        {
            get;
        }

        int CoordinateCount
        {
            get;
        }
    }
}