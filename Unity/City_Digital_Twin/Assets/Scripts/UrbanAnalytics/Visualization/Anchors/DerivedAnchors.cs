using System;
using System.Collections.Generic;
using UnityEngine;

using UrbanAnalytics.Core;
using UrbanAnalytics.Spatial;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// A point a glyph stands on, derived from one spatial unit.
    /// </summary>
    public readonly struct DerivedAnchor
    {
        public string UnitId
        {
            get;
        }

        /// <summary>
        /// World position on the rendered analytical surface
        /// (before any FollowHeightSurface lift).
        /// </summary>
        public Vector3 WorldPosition
        {
            get;
        }

        /// <summary>
        /// Approximate unit size in Unity units: square root of
        /// its bounding-box area.
        /// </summary>
        public float Size
        {
            get;
        }


        public DerivedAnchor(
            string unitId,
            Vector3 worldPosition,
            float size
        )
        {
            UnitId =
                unitId;

            WorldPosition =
                worldPosition;

            Size =
                size;
        }
    }


    /// <summary>
    /// Anchor points for every unit of one spatial layer.
    ///
    /// Anchor = the unit's centroid as stored in the spatial
    /// layer package (source CRS), converted with
    /// SpatialReferenceManager and placed on the rendered layer
    /// root, so anchors sit exactly on the analytical surface.
    /// Note the centroid of a strongly concave unit can lie
    /// outside it; Ruta cells are squares (or clipped squares).
    /// </summary>
    public sealed class DerivedAnchorSet
    {
        private readonly Dictionary<string, DerivedAnchor>
            byUnitId;


        public string SpatialLayerId
        {
            get;
        }

        public IReadOnlyList<DerivedAnchor> Anchors
        {
            get;
        }

        /// <summary>
        /// Median unit size (Unity units). Glyph dimensions are
        /// fractions of this, so glyphs are uniform across the
        /// layer and do not shrink in clipped boundary cells.
        /// </summary>
        public float TypicalSize
        {
            get;
        }


        private DerivedAnchorSet(
            string spatialLayerId,
            List<DerivedAnchor> anchors,
            float typicalSize
        )
        {
            SpatialLayerId =
                spatialLayerId;

            Anchors =
                anchors;

            TypicalSize =
                typicalSize;

            byUnitId =
                new Dictionary<string, DerivedAnchor>(
                    anchors.Count,
                    StringComparer.Ordinal
                );


            foreach (DerivedAnchor anchor in anchors)
            {
                byUnitId[anchor.UnitId] =
                    anchor;
            }
        }


        public bool TryGet(
            string unitId,
            out DerivedAnchor anchor
        )
        {
            return byUnitId.TryGetValue(
                unitId,
                out anchor
            );
        }


        public static DerivedAnchorSet Build(
            SpatialLayer layer,
            Transform renderedLayerRoot,
            SpatialReferenceManager spatialReference
        )
        {
            if (layer == null)
            {
                throw new ArgumentNullException(
                    nameof(layer)
                );
            }

            if (renderedLayerRoot == null)
            {
                throw new ArgumentNullException(
                    nameof(renderedLayerRoot)
                );
            }

            if (spatialReference == null)
            {
                throw new ArgumentNullException(
                    nameof(spatialReference)
                );
            }


            var anchors =
                new List<DerivedAnchor>(
                    layer.UnitCount
                );

            var sizes =
                new List<float>(
                    layer.UnitCount
                );


            foreach (SpatialUnit unit in layer.Units)
            {
                Vector3 local =
                    spatialReference.ToUnity(
                        unit.Centroid.Easting,
                        unit.Centroid.Northing,
                        0.0
                    );


                float size =
                    spatialReference.ScaleDistance(
                        Math.Sqrt(
                            Math.Max(
                                0.0,
                                unit.Bounds.Width *
                                unit.Bounds.Depth
                            )
                        )
                    );


                anchors.Add(
                    new DerivedAnchor(
                        unit.Id,
                        renderedLayerRoot.TransformPoint(
                            local
                        ),
                        size
                    )
                );

                sizes.Add(
                    size
                );
            }


            if (anchors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Spatial layer '{layer.Id}' has no units to " +
                    $"derive anchors from."
                );
            }


            sizes.Sort();


            return new DerivedAnchorSet(
                layer.Id,
                anchors,
                sizes[sizes.Count / 2]
            );
        }
    }
}
