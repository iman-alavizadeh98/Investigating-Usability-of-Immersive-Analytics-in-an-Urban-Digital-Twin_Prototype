using System;
using System.Collections.Generic;
using System.Linq;

namespace UrbanAnalytics.Spatial.Geometry
{
    public sealed class MultiLineStringGeometry
        : ISpatialGeometry
    {
        private readonly LineStringGeometry[] lines;


        public IReadOnlyList<LineStringGeometry> Lines =>
            lines;


        public SpatialGeometryType GeometryType =>
            SpatialGeometryType.MultiLineString;


        public SpatialBounds Bounds
        {
            get;
        }


        public int CoordinateCount
        {
            get;
        }


        public MultiLineStringGeometry(
            IEnumerable<LineStringGeometry> lines
        )
        {
            if (lines == null)
            {
                throw new ArgumentNullException(
                    nameof(lines)
                );
            }


            this.lines =
                lines.ToArray();


            if (this.lines.Length == 0)
            {
                throw new ArgumentException(
                    "MultiLineString requires at least one line.",
                    nameof(lines)
                );
            }


            if (this.lines.Any(
                    line => line == null
                ))
            {
                throw new ArgumentException(
                    "MultiLineString cannot contain null geometries.",
                    nameof(lines)
                );
            }


            CoordinateCount =
                this.lines.Sum(
                    line =>
                        line.CoordinateCount
                );


            Bounds =
                SpatialBounds.FromCoordinates(
                    this.lines.SelectMany(
                        line =>
                            line.Coordinates
                    )
                );
        }
    }
}