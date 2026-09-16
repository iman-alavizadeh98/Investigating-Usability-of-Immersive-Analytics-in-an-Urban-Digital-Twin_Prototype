using System;
using System.Collections.Generic;

namespace UrbanAnalytics.Spatial.Geometry
{
    /// <summary>
    /// Axis-aligned bounds expressed in the project's source CRS.
    /// </summary>
    public readonly struct SpatialBounds
    {
        public double MinEasting { get; }
        public double MinNorthing { get; }
        public double MinElevation { get; }

        public double MaxEasting { get; }
        public double MaxNorthing { get; }
        public double MaxElevation { get; }


        public double Width =>
            MaxEasting - MinEasting;

        public double Depth =>
            MaxNorthing - MinNorthing;

        public double Height =>
            MaxElevation - MinElevation;


        public SpatialCoordinate Center =>
            new SpatialCoordinate(
                (MinEasting + MaxEasting) * 0.5,
                (MinNorthing + MaxNorthing) * 0.5,
                (MinElevation + MaxElevation) * 0.5
            );


        public SpatialBounds(
            double minEasting,
            double minNorthing,
            double minElevation,
            double maxEasting,
            double maxNorthing,
            double maxElevation
        )
        {
            if (maxEasting < minEasting)
            {
                throw new ArgumentException(
                    "Maximum Easting cannot be smaller than minimum Easting."
                );
            }

            if (maxNorthing < minNorthing)
            {
                throw new ArgumentException(
                    "Maximum Northing cannot be smaller than minimum Northing."
                );
            }

            if (maxElevation < minElevation)
            {
                throw new ArgumentException(
                    "Maximum elevation cannot be smaller than minimum elevation."
                );
            }

            MinEasting = minEasting;
            MinNorthing = minNorthing;
            MinElevation = minElevation;

            MaxEasting = maxEasting;
            MaxNorthing = maxNorthing;
            MaxElevation = maxElevation;
        }


        public bool Contains(
            SpatialCoordinate coordinate
        )
        {
            return
                coordinate.Easting >= MinEasting &&
                coordinate.Easting <= MaxEasting &&

                coordinate.Northing >= MinNorthing &&
                coordinate.Northing <= MaxNorthing &&

                coordinate.Elevation >= MinElevation &&
                coordinate.Elevation <= MaxElevation;
        }


        public static SpatialBounds FromCoordinates(
            IEnumerable<SpatialCoordinate> coordinates
        )
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(
                    nameof(coordinates)
                );
            }

            bool foundAny = false;

            double minE = double.MaxValue;
            double minN = double.MaxValue;
            double minZ = double.MaxValue;

            double maxE = double.MinValue;
            double maxN = double.MinValue;
            double maxZ = double.MinValue;


            foreach (SpatialCoordinate coordinate in coordinates)
            {
                foundAny = true;

                minE = Math.Min(
                    minE,
                    coordinate.Easting
                );

                minN = Math.Min(
                    minN,
                    coordinate.Northing
                );

                minZ = Math.Min(
                    minZ,
                    coordinate.Elevation
                );

                maxE = Math.Max(
                    maxE,
                    coordinate.Easting
                );

                maxN = Math.Max(
                    maxN,
                    coordinate.Northing
                );

                maxZ = Math.Max(
                    maxZ,
                    coordinate.Elevation
                );
            }


            if (!foundAny)
            {
                throw new ArgumentException(
                    "Cannot calculate bounds from an empty coordinate collection.",
                    nameof(coordinates)
                );
            }


            return new SpatialBounds(
                minE,
                minN,
                minZ,
                maxE,
                maxN,
                maxZ
            );
        }


        public override string ToString()
        {
            return
                $"E[{MinEasting}, {MaxEasting}] " +
                $"N[{MinNorthing}, {MaxNorthing}] " +
                $"Z[{MinElevation}, {MaxElevation}]";
        }
    }
}