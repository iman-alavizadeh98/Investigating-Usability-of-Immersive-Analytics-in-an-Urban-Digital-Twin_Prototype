using System;
using System.Collections.Generic;
using System.Linq;

namespace UrbanAnalytics.Spatial.Geometry
{
    public enum RingOrientation
    {
        Clockwise,
        CounterClockwise,
        Undefined
    }


    /// <summary>
    /// One polygon ring.
    ///
    /// Internally the ring is stored WITHOUT duplicating
    /// the first coordinate at the end.
    /// </summary>
    public sealed class PolygonRing
    {
        private readonly SpatialCoordinate[] coordinates;


        public IReadOnlyList<SpatialCoordinate> Coordinates =>
            coordinates;


        public int CoordinateCount =>
            coordinates.Length;


        public double SignedArea
        {
            get;
        }


        public double Area =>
            Math.Abs(SignedArea);


        public RingOrientation Orientation
        {
            get;
        }


        public SpatialBounds Bounds
        {
            get;
        }


        public PolygonRing(
            IEnumerable<SpatialCoordinate> coordinates
        )
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(
                    nameof(coordinates)
                );
            }


            SpatialCoordinate[] input =
                coordinates.ToArray();


            if (input.Length >= 2 &&
                input[0] ==
                input[input.Length - 1])
            {
                input =
                    input
                    .Take(input.Length - 1)
                    .ToArray();
            }


            if (input.Length < 3)
            {
                throw new ArgumentException(
                    "A polygon ring requires at least three coordinates.",
                    nameof(coordinates)
                );
            }


            int uniqueCoordinateCount =
                input.Distinct().Count();


            if (uniqueCoordinateCount < 3)
            {
                throw new ArgumentException(
                    "A polygon ring requires at least three unique coordinates.",
                    nameof(coordinates)
                );
            }


            this.coordinates = input;

            SignedArea =
                CalculateSignedArea(
                    this.coordinates
                );


            if (Math.Abs(SignedArea) < 1e-9)
            {
                Orientation =
                    RingOrientation.Undefined;
            }
            else if (SignedArea < 0.0)
            {
                Orientation =
                    RingOrientation.Clockwise;
            }
            else
            {
                Orientation =
                    RingOrientation.CounterClockwise;
            }


            Bounds =
                SpatialBounds.FromCoordinates(
                    this.coordinates
                );
        }


        private static double CalculateSignedArea(
            IReadOnlyList<SpatialCoordinate> coordinates
        )
        {
            double sum = 0.0;


            for (
                int i = 0;
                i < coordinates.Count;
                i++
            )
            {
                SpatialCoordinate current =
                    coordinates[i];

                SpatialCoordinate next =
                    coordinates[
                        (i + 1) %
                        coordinates.Count
                    ];


                sum +=
                    current.Easting *
                    next.Northing;

                sum -=
                    next.Easting *
                    current.Northing;
            }


            return sum * 0.5;
        }
    }


    public sealed class PolygonGeometry
        : ISpatialGeometry
    {
        private readonly PolygonRing[] holes;


        public PolygonRing Exterior
        {
            get;
        }


        public IReadOnlyList<PolygonRing> Holes =>
            holes;


        public SpatialGeometryType GeometryType =>
            SpatialGeometryType.Polygon;


        public SpatialBounds Bounds =>
            Exterior.Bounds;


        public int CoordinateCount
        {
            get;
        }


        public double Area
        {
            get;
        }


        public PolygonGeometry(
            PolygonRing exterior,
            IEnumerable<PolygonRing> holes = null
        )
        {
            Exterior =
                exterior
                ?? throw new ArgumentNullException(
                    nameof(exterior)
                );


            this.holes =
                holes?.ToArray()
                ?? Array.Empty<PolygonRing>();


            int coordinateCount =
                Exterior.CoordinateCount;


            double holeArea = 0.0;


            foreach (
                PolygonRing hole
                in this.holes
            )
            {
                if (hole == null)
                {
                    throw new ArgumentException(
                        "Polygon holes cannot contain null rings.",
                        nameof(holes)
                    );
                }


                coordinateCount +=
                    hole.CoordinateCount;

                holeArea +=
                    hole.Area;
            }


            CoordinateCount =
                coordinateCount;


            Area =
                Math.Max(
                    0.0,
                    Exterior.Area - holeArea
                );
        }
    }
}