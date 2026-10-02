using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Describes the top surface produced by one
    /// HeightSurface visualization.
    ///
    /// Top offsets are Unity-local Y offsets relative to the
    /// original analytical surface.
    ///
    /// For InsetExtrusion the top is also smaller than the
    /// unit: it is the unit scaled horizontally by
    /// HorizontalScale about a per-unit anchor (world space).
    /// Context entities following the surface apply the same
    /// scale so they stay on the column.
    /// </summary>
    public sealed class SurfaceElevationField
    {
        private readonly IReadOnlyDictionary<string, float>
            topOffsets;


        private readonly IReadOnlyDictionary<string, Vector3>
            horizontalAnchors;


        /// <summary>
        /// Horizontal scale of the top surface about each
        /// unit's anchor. 1 = top covers the whole unit.
        /// </summary>
        public float HorizontalScale
        {
            get;
        }


        public string VisualizationLayerId
        {
            get;
        }


        public string SpatialLayerId
        {
            get;
        }


        public IReadOnlyDictionary<string, float>
            TopOffsets =>
                topOffsets;


        public SurfaceElevationField(
            string visualizationLayerId,
            string spatialLayerId,
            IDictionary<string, float> topOffsets,
            float horizontalScale = 1.0f,
            IDictionary<string, Vector3> horizontalAnchors = null
        )
        {
            if (string.IsNullOrWhiteSpace(
                    visualizationLayerId
                ))
            {
                throw new ArgumentException(
                    "Visualization layer ID is required.",
                    nameof(visualizationLayerId)
                );
            }


            if (string.IsNullOrWhiteSpace(
                    spatialLayerId
                ))
            {
                throw new ArgumentException(
                    "Spatial layer ID is required.",
                    nameof(spatialLayerId)
                );
            }


            if (topOffsets == null)
            {
                throw new ArgumentNullException(
                    nameof(topOffsets)
                );
            }


            VisualizationLayerId =
                visualizationLayerId.Trim();


            SpatialLayerId =
                spatialLayerId.Trim();


            // "this." is required: the parameter has the same
            // name, and assigning without it left the field null.
            this.topOffsets =
                new ReadOnlyDictionary<string, float>(
                    new Dictionary<string, float>(
                        topOffsets,
                        StringComparer.Ordinal
                    )
                );


            if (horizontalScale <= 0.0f ||
                horizontalScale > 1.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(horizontalScale),
                    "Horizontal scale must be in (0, 1]."
                );
            }


            if (horizontalScale < 1.0f &&
                horizontalAnchors == null)
            {
                throw new ArgumentNullException(
                    nameof(horizontalAnchors),
                    "An inset surface needs per-unit anchors."
                );
            }


            HorizontalScale =
                horizontalScale;


            this.horizontalAnchors =
                new ReadOnlyDictionary<string, Vector3>(
                    horizontalAnchors != null
                        ? new Dictionary<string, Vector3>(
                            horizontalAnchors,
                            StringComparer.Ordinal
                        )
                        : new Dictionary<string, Vector3>(
                            StringComparer.Ordinal
                        )
                );
        }


        /// <summary>
        /// True when the top surface of this unit is smaller
        /// than the unit (InsetExtrusion). The anchor is in
        /// world space.
        /// </summary>
        public bool TryGetHorizontalInset(
            string spatialUnitId,
            out Vector3 anchorWorld,
            out float scale
        )
        {
            anchorWorld =
                default;

            scale =
                HorizontalScale;


            if (HorizontalScale >= 1.0f ||
                string.IsNullOrWhiteSpace(
                    spatialUnitId
                ))
            {
                return false;
            }


            return horizontalAnchors.TryGetValue(
                spatialUnitId.Trim(),
                out anchorWorld
            );
        }


        public bool TryGetTopOffset(
            string spatialUnitId,
            out float offset
        )
        {
            offset =
                0.0f;


            if (string.IsNullOrWhiteSpace(
                    spatialUnitId
                ))
            {
                return false;
            }


            return topOffsets.TryGetValue(
                spatialUnitId.Trim(),
                out offset
            );
        }
    }


    /// <summary>
    /// Runtime communication state between visualization
    /// renderers participating in one VisualizationSpec.
    ///
    /// This is intentionally not a MonoBehaviour.
    /// </summary>
    public sealed class VisualizationRuntimeState
    {
        private readonly Dictionary<
            string,
            SurfaceElevationField
        > elevationFields =
            new Dictionary<
                string,
                SurfaceElevationField
            >(
                StringComparer.Ordinal
            );


        public int ElevationFieldCount =>
            elevationFields.Count;


        public void RegisterSurfaceElevation(
            string visualizationLayerId,
            string spatialLayerId,
            IDictionary<string, float> topOffsets,
            float horizontalScale = 1.0f,
            IDictionary<string, Vector3> horizontalAnchors = null
        )
        {
            var field =
                new SurfaceElevationField(
                    visualizationLayerId,
                    spatialLayerId,
                    topOffsets,
                    horizontalScale,
                    horizontalAnchors
                );


            elevationFields[
                field.VisualizationLayerId
            ] =
                field;
        }


        public bool TryGetSurfaceElevation(
            string visualizationLayerId,
            out SurfaceElevationField field
        )
        {
            field =
                null;


            if (string.IsNullOrWhiteSpace(
                    visualizationLayerId
                ))
            {
                return false;
            }


            return elevationFields.TryGetValue(
                visualizationLayerId.Trim(),
                out field
            );
        }


        public bool RemoveSurfaceElevation(
            string visualizationLayerId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    visualizationLayerId
                ))
            {
                return false;
            }


            return elevationFields.Remove(
                visualizationLayerId.Trim()
            );
        }


        public void Clear()
        {
            elevationFields.Clear();
        }
    }
}