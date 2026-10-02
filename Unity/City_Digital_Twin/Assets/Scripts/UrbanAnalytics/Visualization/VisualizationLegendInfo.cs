using System;
using System.Collections.Generic;
using UnityEngine;

using UrbanAnalytics.Data;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Runtime information describing the currently active
    /// visualization for presentation in UI components.
    ///
    /// This is presentation metadata only.
    /// It does not own analytical data or geometry.
    /// </summary>
    public sealed class VisualizationLegendInfo
    {
        public string DataLayerId { get; }

        public string VariableId { get; }

        public string VariableDisplayName { get; }

        public string Unit { get; }

        public string SpatialLayerId { get; }

        /// <summary>Value at the left end of the legend.</summary>
        public double Minimum { get; }

        /// <summary>Value at the right end of the legend.</summary>
        public double Maximum { get; }

        /// <summary>
        /// Colours to draw left → right, sampled through the
        /// scale and palette exactly as the renderers colour
        /// units (so stepped/quantile scales show their classes,
        /// and reversal is already applied).
        /// </summary>
        public IReadOnlyList<Color32> Ramp { get; }

        /// <summary>E.g. "2-98 percentile" or "5 quantile classes".</summary>
        public string ScaleDescription { get; }

        public ScaleType ScaleType { get; }

        /// <summary>Quantile only: inner class boundaries.</summary>
        public IReadOnlyList<double> ClassBreaks { get; }

        public Color NoDataColor { get; }


        public VisualizationLegendInfo(
            string dataLayerId,
            string variableId,
            string variableDisplayName,
            string unit,
            string spatialLayerId,
            double minimum,
            double maximum,
            IReadOnlyList<Color32> ramp,
            string scaleDescription,
            ScaleType scaleType,
            IReadOnlyList<double> classBreaks,
            Color noDataColor
        )
        {
            DataLayerId = dataLayerId
                ?? throw new ArgumentNullException(
                    nameof(dataLayerId)
                );

            VariableId = variableId
                ?? throw new ArgumentNullException(
                    nameof(variableId)
                );

            VariableDisplayName =
                string.IsNullOrWhiteSpace(variableDisplayName)
                    ? variableId
                    : variableDisplayName;

            Unit = unit ?? string.Empty;

            SpatialLayerId = spatialLayerId
                ?? throw new ArgumentNullException(
                    nameof(spatialLayerId)
                );

            Minimum = minimum;

            Maximum = maximum;

            if (ramp == null ||
                ramp.Count == 0)
            {
                throw new ArgumentException(
                    "Legend ramp needs at least one colour.",
                    nameof(ramp)
                );
            }

            Ramp = ramp;

            ScaleDescription = scaleDescription ?? string.Empty;

            ScaleType = scaleType;

            ClassBreaks = classBreaks
                ?? Array.Empty<double>();

            NoDataColor = noDataColor;
        }


        /// <summary>
        /// Builds the colour legend for one colour encoding. Shared
        /// by every renderer so legends cannot disagree with the
        /// colours actually drawn.
        /// </summary>
        public static VisualizationLegendInfo ForColorEncoding(
            DataLayer dataLayer,
            DataVariableReference variable,
            VisualizationEncodingSpec encoding,
            string targetLayerId,
            ResolvedNumericScale scale,
            int rampSamples = 256
        )
        {
            string displayName =
                variable.VariableId;


            string unit =
                string.Empty;


            if (dataLayer.TryGetVariableDefinition(
                    variable.VariableId,
                    out DataVariableDefinition
                        definition
                ))
            {
                if (!string.IsNullOrWhiteSpace(
                        definition.DisplayName
                    ))
                {
                    displayName =
                        definition.DisplayName;
                }


                unit =
                    definition.Unit
                    ?? string.Empty;
            }


            // Every colour encoding passes through here, so this is
            // the one place a poor palette/scale pairing is flagged.
            string mismatch =
                ColorPaletteLibrary.CheckCompatibility(
                    encoding.Color.ResolvePalette(),
                    scale.Type
                );


            if (mismatch != null)
            {
                Debug.LogWarning(
                    $"Colour encoding for '{targetLayerId}' " +
                    $"({dataLayer.Id}.{variable.VariableId}): " +
                    mismatch
                );
            }


            int samples =
                Mathf.Max(
                    2,
                    rampSamples
                );


            var ramp =
                new Color32[
                    samples
                ];


            for (
                int i = 0;
                i < samples;
                i++
            )
            {
                float x =
                    i / (float)(samples - 1);


                ramp[i] =
                    encoding.Color.Evaluate(
                        scale.LegendPositionToNormalized(
                            x
                        )
                    );
            }


            return new VisualizationLegendInfo(
                dataLayer.Id,
                variable.VariableId,
                displayName,
                unit,
                targetLayerId,
                scale.Minimum,
                scale.Maximum,
                ramp,
                scale.Description,
                scale.Type,
                scale.Breaks,
                encoding.Color.NoDataColor
            );
        }
    }
}
