using System;
using System.Collections.Generic;
using UrbanAnalytics.Spatial.Serialization;

namespace UrbanAnalytics.Spatial.Geometry
{
    /// <summary>
    /// Converts serialized spatial geometry DTOs into
    /// validated runtime geometry objects.
    ///
    /// Geometry type is determined by the validated spatial-layer
    /// definition rather than by inspecting unused DTO fields.
    ///
    /// This class does not perform:
    /// - CRS reprojection
    /// - Unity coordinate conversion
    /// - rendering
    /// - triangulation
    ///
    /// All coordinates remain in the project's declared source CRS.
    /// </summary>
    public static class SpatialGeometryFactory
    {
        public static ISpatialGeometry Create(
            GeometryDto dto,
            SpatialGeometryType expectedGeometryType
        )
        {
            if (dto == null)
            {
                throw new ArgumentNullException(
                    nameof(dto),
                    "Geometry DTO cannot be null."
                );
            }

            return expectedGeometryType switch
            {
                SpatialGeometryType.Point =>
                    CreatePoint(dto),

                SpatialGeometryType.MultiPoint =>
                    CreateMultiPoint(dto),

                SpatialGeometryType.LineString =>
                    CreateLineString(dto),

                SpatialGeometryType.MultiLineString =>
                    CreateMultiLineString(dto),

                SpatialGeometryType.Polygon =>
                    CreatePolygon(dto),

                SpatialGeometryType.MultiPolygon =>
                    CreateMultiPolygon(dto),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(expectedGeometryType),
                    expectedGeometryType,
                    "Unsupported spatial geometry type."
                )
            };
        }


        // =========================================================
        // POINT
        // =========================================================

        private static PointGeometry CreatePoint(
            GeometryDto dto
        )
        {
            if (dto.point == null)
            {
                throw new ArgumentException(
                    "Point geometry is missing its point definition."
                );
            }

            if (dto.point.coordinate == null)
            {
                throw new ArgumentException(
                    "Point geometry is missing its coordinate."
                );
            }

            return new PointGeometry(
                CreateCoordinate(
                    dto.point.coordinate
                )
            );
        }


        // =========================================================
        // MULTI POINT
        // =========================================================

        private static MultiPointGeometry CreateMultiPoint(
            GeometryDto dto
        )
        {
            if (dto.multiPoint == null)
            {
                throw new ArgumentException(
                    "MultiPoint geometry is missing its definition."
                );
            }

            if (dto.multiPoint.coordinates == null ||
                dto.multiPoint.coordinates.Length == 0)
            {
                throw new ArgumentException(
                    "MultiPoint geometry requires at least one coordinate."
                );
            }

            var points =
                new List<PointGeometry>(
                    dto.multiPoint.coordinates.Length
                );

            for (
                int i = 0;
                i < dto.multiPoint.coordinates.Length;
                i++
            )
            {
                SpatialCoordinateDto coordinateDto =
                    dto.multiPoint.coordinates[i];

                if (coordinateDto == null)
                {
                    throw new ArgumentException(
                        $"MultiPoint geometry contains " +
                        $"a null coordinate at index {i}."
                    );
                }

                points.Add(
                    new PointGeometry(
                        CreateCoordinate(
                            coordinateDto
                        )
                    )
                );
            }

            return new MultiPointGeometry(
                points
            );
        }


        // =========================================================
        // LINE STRING
        // =========================================================

        private static LineStringGeometry CreateLineString(
            GeometryDto dto
        )
        {
            if (dto.lineString == null)
            {
                throw new ArgumentException(
                    "LineString geometry is missing its definition."
                );
            }

            return CreateLineString(
                dto.lineString
            );
        }


        private static LineStringGeometry CreateLineString(
            LineStringDto dto
        )
        {
            if (dto == null)
            {
                throw new ArgumentNullException(
                    nameof(dto)
                );
            }

            if (dto.coordinates == null ||
                dto.coordinates.Length < 2)
            {
                throw new ArgumentException(
                    "LineString geometry requires at least two coordinates."
                );
            }

            var coordinates =
                new List<SpatialCoordinate>(
                    dto.coordinates.Length
                );

            for (
                int i = 0;
                i < dto.coordinates.Length;
                i++
            )
            {
                SpatialCoordinateDto coordinateDto =
                    dto.coordinates[i];

                if (coordinateDto == null)
                {
                    throw new ArgumentException(
                        $"LineString geometry contains " +
                        $"a null coordinate at index {i}."
                    );
                }

                coordinates.Add(
                    CreateCoordinate(
                        coordinateDto
                    )
                );
            }

            return new LineStringGeometry(
                coordinates
            );
        }


        // =========================================================
        // MULTI LINE STRING
        // =========================================================

        private static MultiLineStringGeometry CreateMultiLineString(
            GeometryDto dto
        )
        {
            if (dto.multiLineString == null)
            {
                throw new ArgumentException(
                    "MultiLineString geometry is missing its definition."
                );
            }

            if (dto.multiLineString.lines == null ||
                dto.multiLineString.lines.Length == 0)
            {
                throw new ArgumentException(
                    "MultiLineString geometry requires at least one line."
                );
            }

            var lines =
                new List<LineStringGeometry>(
                    dto.multiLineString.lines.Length
                );

            for (
                int i = 0;
                i < dto.multiLineString.lines.Length;
                i++
            )
            {
                LineStringDto lineDto =
                    dto.multiLineString.lines[i];

                if (lineDto == null)
                {
                    throw new ArgumentException(
                        $"MultiLineString contains " +
                        $"a null line at index {i}."
                    );
                }

                lines.Add(
                    CreateLineString(
                        lineDto
                    )
                );
            }

            return new MultiLineStringGeometry(
                lines
            );
        }


        // =========================================================
        // POLYGON
        // =========================================================

        private static PolygonGeometry CreatePolygon(
            GeometryDto dto
        )
        {
            if (dto.polygon == null)
            {
                throw new ArgumentException(
                    "Polygon geometry is missing its definition."
                );
            }

            return CreatePolygon(
                dto.polygon
            );
        }


        private static PolygonGeometry CreatePolygon(
            PolygonDto dto
        )
        {
            if (dto == null)
            {
                throw new ArgumentNullException(
                    nameof(dto)
                );
            }

            if (dto.exterior == null)
            {
                throw new ArgumentException(
                    "Polygon geometry requires an exterior ring."
                );
            }


            PolygonRing exterior =
                CreateRing(
                    dto.exterior,
                    "exterior"
                );


            var holes =
                new List<PolygonRing>();


            if (dto.holes != null)
            {
                for (
                    int i = 0;
                    i < dto.holes.Length;
                    i++
                )
                {
                    PolygonRingDto holeDto =
                        dto.holes[i];

                    if (holeDto == null)
                    {
                        throw new ArgumentException(
                            $"Polygon contains a null hole " +
                            $"at index {i}."
                        );
                    }


                    PolygonRing hole =
                        CreateRing(
                            holeDto,
                            $"hole[{i}]"
                        );


                    ValidateHoleBounds(
                        exterior,
                        hole,
                        i
                    );


                    holes.Add(
                        hole
                    );
                }
            }


            return new PolygonGeometry(
                exterior,
                holes
            );
        }


        // =========================================================
        // MULTI POLYGON
        // =========================================================

        private static MultiPolygonGeometry CreateMultiPolygon(
            GeometryDto dto
        )
        {
            if (dto.multiPolygon == null)
            {
                throw new ArgumentException(
                    "MultiPolygon geometry is missing its definition."
                );
            }

            if (dto.multiPolygon.polygons == null ||
                dto.multiPolygon.polygons.Length == 0)
            {
                throw new ArgumentException(
                    "MultiPolygon requires at least one polygon."
                );
            }


            var polygons =
                new List<PolygonGeometry>(
                    dto.multiPolygon.polygons.Length
                );


            for (
                int i = 0;
                i < dto.multiPolygon.polygons.Length;
                i++
            )
            {
                PolygonDto polygonDto =
                    dto.multiPolygon.polygons[i];

                if (polygonDto == null)
                {
                    throw new ArgumentException(
                        $"MultiPolygon contains a null polygon " +
                        $"at index {i}."
                    );
                }


                polygons.Add(
                    CreatePolygon(
                        polygonDto
                    )
                );
            }


            return new MultiPolygonGeometry(
                polygons
            );
        }


        // =========================================================
        // POLYGON RINGS
        // =========================================================

        private static PolygonRing CreateRing(
            PolygonRingDto dto,
            string ringName
        )
        {
            if (dto == null)
            {
                throw new ArgumentNullException(
                    nameof(dto)
                );
            }

            if (dto.coordinates == null ||
                dto.coordinates.Length < 3)
            {
                throw new ArgumentException(
                    $"Polygon {ringName} requires " +
                    $"at least three coordinates."
                );
            }


            var coordinates =
                new List<SpatialCoordinate>(
                    dto.coordinates.Length
                );


            for (
                int i = 0;
                i < dto.coordinates.Length;
                i++
            )
            {
                SpatialCoordinateDto coordinateDto =
                    dto.coordinates[i];

                if (coordinateDto == null)
                {
                    throw new ArgumentException(
                        $"Polygon {ringName} contains " +
                        $"a null coordinate at index {i}."
                    );
                }


                coordinates.Add(
                    CreateCoordinate(
                        coordinateDto
                    )
                );
            }


            PolygonRing ring =
                new PolygonRing(
                    coordinates
                );


            if (ring.Orientation ==
                RingOrientation.Undefined)
            {
                throw new ArgumentException(
                    $"Polygon {ringName} has zero " +
                    $"or near-zero area."
                );
            }


            return ring;
        }


        // =========================================================
        // COORDINATES
        // =========================================================

        public static SpatialCoordinate CreateCoordinate(
            SpatialCoordinateDto dto
        )
        {
            if (dto == null)
            {
                throw new ArgumentNullException(
                    nameof(dto),
                    "Spatial coordinate DTO cannot be null."
                );
            }


            ValidateFinite(
                dto.easting,
                "easting"
            );

            ValidateFinite(
                dto.northing,
                "northing"
            );

            ValidateFinite(
                dto.elevation,
                "elevation"
            );


            return new SpatialCoordinate(
                dto.easting,
                dto.northing,
                dto.elevation
            );
        }


        private static void ValidateFinite(
            double value,
            string fieldName
        )
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    $"{fieldName} must be a finite number."
                );
            }
        }


        // =========================================================
        // POLYGON HOLE VALIDATION
        // =========================================================

        /// <summary>
        /// Performs a basic bounds check for polygon holes.
        ///
        /// This is intentionally not a complete topological
        /// containment/intersection test.
        ///
        /// Full GIS topology validation belongs in the
        /// offline preprocessing pipeline.
        /// </summary>
        private static void ValidateHoleBounds(
            PolygonRing exterior,
            PolygonRing hole,
            int holeIndex
        )
        {
            if (exterior == null)
            {
                throw new ArgumentNullException(
                    nameof(exterior)
                );
            }

            if (hole == null)
            {
                throw new ArgumentNullException(
                    nameof(hole)
                );
            }


            SpatialBounds exteriorBounds =
                exterior.Bounds;

            SpatialBounds holeBounds =
                hole.Bounds;


            bool insideBounds =
                holeBounds.MinEasting >=
                    exteriorBounds.MinEasting &&

                holeBounds.MaxEasting <=
                    exteriorBounds.MaxEasting &&

                holeBounds.MinNorthing >=
                    exteriorBounds.MinNorthing &&

                holeBounds.MaxNorthing <=
                    exteriorBounds.MaxNorthing;


            if (!insideBounds)
            {
                throw new ArgumentException(
                    $"Polygon hole[{holeIndex}] extends " +
                    $"outside the exterior ring bounds."
                );
            }
        }
    }
}