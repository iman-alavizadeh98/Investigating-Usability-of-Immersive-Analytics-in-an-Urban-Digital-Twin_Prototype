using System;

namespace UrbanAnalytics.Spatial.Geometry
{
    /// <summary>
    /// Coordinate expressed in the project's declared source CRS.
    ///
    /// For the current project:
    /// Easting  = EPSG:3006 X
    /// Northing = EPSG:3006 Y
    /// Elevation = metres
    ///
    /// This is NOT a Unity coordinate.
    /// </summary>
    [Serializable]
    public readonly struct SpatialCoordinate
        : IEquatable<SpatialCoordinate>
    {
        public double Easting { get; }
        public double Northing { get; }
        public double Elevation { get; }

        public SpatialCoordinate(
            double easting,
            double northing,
            double elevation = 0.0
        )
        {
            if (double.IsNaN(easting) ||
                double.IsInfinity(easting))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(easting),
                    "Easting must be a finite number."
                );
            }

            if (double.IsNaN(northing) ||
                double.IsInfinity(northing))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(northing),
                    "Northing must be a finite number."
                );
            }

            if (double.IsNaN(elevation) ||
                double.IsInfinity(elevation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elevation),
                    "Elevation must be a finite number."
                );
            }

            Easting = easting;
            Northing = northing;
            Elevation = elevation;
        }


        public bool Equals(
            SpatialCoordinate other
        )
        {
            return
                Easting.Equals(other.Easting) &&
                Northing.Equals(other.Northing) &&
                Elevation.Equals(other.Elevation);
        }


        public override bool Equals(
            object obj
        )
        {
            return
                obj is SpatialCoordinate other &&
                Equals(other);
        }


        public override int GetHashCode()
        {
            return HashCode.Combine(
                Easting,
                Northing,
                Elevation
            );
        }


        public static bool operator ==(
            SpatialCoordinate left,
            SpatialCoordinate right
        )
        {
            return left.Equals(right);
        }


        public static bool operator !=(
            SpatialCoordinate left,
            SpatialCoordinate right
        )
        {
            return !left.Equals(right);
        }


        public override string ToString()
        {
            return
                $"({Easting}, " +
                $"{Northing}, " +
                $"{Elevation})";
        }
    }
}