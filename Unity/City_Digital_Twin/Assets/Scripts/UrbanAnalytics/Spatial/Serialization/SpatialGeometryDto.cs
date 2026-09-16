using System;

namespace UrbanAnalytics.Spatial.Serialization
{
    /// <summary>
    /// Root structure of a spatial geometry file.
    ///
    /// This is a serialization DTO only.
    /// It should not be used directly by runtime systems.
    /// </summary>
    [Serializable]
    public class SpatialGeometryFileDto
    {
        public string schemaVersion;

        // Must match the ID in layer.json.
        public string spatialLayerId;

        // Declared for validation against the layer definition.
        public string geometryType;

        // Number of units expected in this file.
        public int unitCount;

        public SpatialUnitDto[] units;
    }


    /// <summary>
    /// Serializable representation of one spatial unit.
    /// </summary>
    [Serializable]
    public class SpatialUnitDto
    {
        public string id;
        public string displayName;
        public string parentUnitId;

        public SpatialCoordinateDto centroid;

        public GeometryDto geometry;
    }


    /// <summary>
    /// Source-CRS coordinate.
    ///
    /// For the current project:
    /// easting/northing are EPSG:3006 coordinates.
    /// </summary>
    [Serializable]
    public class SpatialCoordinateDto
    {
        public double easting;
        public double northing;
        public double elevation;
    }


    /// <summary>
    /// Generic serialized geometry container.
    ///
    /// Only the field appropriate for the geometry type
    /// should be populated.
    /// </summary>
    [Serializable]
    public class GeometryDto
    {
        public PointDto point;

        public MultiPointDto multiPoint;

        public LineStringDto lineString;

        public MultiLineStringDto multiLineString;

        public PolygonDto polygon;

        public MultiPolygonDto multiPolygon;
    }


    // =========================================================
    // POINT
    // =========================================================

    [Serializable]
    public class PointDto
    {
        public SpatialCoordinateDto coordinate;
    }


    [Serializable]
    public class MultiPointDto
    {
        public SpatialCoordinateDto[] coordinates;
    }


    // =========================================================
    // LINE
    // =========================================================

    [Serializable]
    public class LineStringDto
    {
        public SpatialCoordinateDto[] coordinates;
    }


    [Serializable]
    public class MultiLineStringDto
    {
        public LineStringDto[] lines;
    }


    // =========================================================
    // POLYGON
    // =========================================================

    [Serializable]
    public class PolygonRingDto
    {
        public SpatialCoordinateDto[] coordinates;
    }


    [Serializable]
    public class PolygonDto
    {
        public PolygonRingDto exterior;

        public PolygonRingDto[] holes;
    }


    [Serializable]
    public class MultiPolygonDto
    {
        public PolygonDto[] polygons;
    }
}