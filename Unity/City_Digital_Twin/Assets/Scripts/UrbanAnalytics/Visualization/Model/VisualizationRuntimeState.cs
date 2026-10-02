using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Describes the top-surface offset produced by one
    /// HeightSurface visualization.
    ///
    /// Values are Unity-local Y offsets relative to the
    /// original analytical surface.
    /// </summary>
    public sealed class SurfaceElevationField
    {
        private readonly IReadOnlyDictionary<string, float>
            topOffsets;


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
            IDictionary<string, float> topOffsets
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
            IDictionary<string, float> topOffsets
        )
        {
            var field =
                new SurfaceElevationField(
                    visualizationLayerId,
                    spatialLayerId,
                    topOffsets
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