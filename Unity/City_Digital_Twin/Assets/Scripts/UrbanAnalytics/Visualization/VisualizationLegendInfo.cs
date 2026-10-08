using System;
using System.Collections.Generic;
using UnityEngine;

using UrbanAnalytics.Data;

namespace UrbanAnalytics.Visualization
{
    /// <summary>One entry of a categorical legend.</summary>
    public readonly struct LegendCategory
    {
        public string Label
        {
            get;
        }

        public Color32 Color
        {
            get;
        }


        public LegendCategory(
            string label,
            Color32 color
        )
        {
            Label =
                label ?? string.Empty;

            Color =
                color;
        }
    }


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

        /// <summary>
        /// Non-empty for categorical legends (glyph segments,
        /// up/down colours). Ramp then holds one block per entry.
        /// </summary>
        public IReadOnlyList<LegendCategory> Categories { get; private set; }
            = Array.Empty<LegendCategory>();

        public bool IsCategorical =>
            Categories.Count > 0;

        /// <summary>
        /// Which visual channel this legend explains, e.g.
        /// "Building colour", "Column colour", "Height ↑",
        /// "Height ↓". Empty = not set by the renderer.
        /// </summary>
        public string Channel { get; private set; } = string.Empty;

        /// <summary>Display name of the data layer (carries its year).</summary>
        public string SourceName { get; private set; } = string.Empty;

        /// <summary>True for a height legend (a length scale, not colours).</summary>
        public bool IsHeight { get; private set; }

        /// <summary>
        /// Rank scales only: the value at the middle of the legend
        /// (the median), shown between minimum and maximum because
        /// a rank legend is not linear in value.
        /// </summary>
        public double? Median { get; private set; }


        /// <summary>Sets the channel label; returns this legend.</summary>
        public VisualizationLegendInfo WithChannel(
            string channel
        )
        {
            Channel = channel ?? string.Empty;

            return this;
        }


        /// <summary>
        /// Legend for a height encoding: the variable, its unit and
        /// the value range mapped from zero to the maximum height
        /// (drawn as a light-to-dark grey bar).
        /// </summary>
        public static VisualizationLegendInfo ForHeight(
            DataLayer dataLayer,
            DataVariableReference variable,
            string targetLayerId,
            ResolvedNumericScale scale,
            string channel
        )
        {
            string displayName = variable.VariableId;
            string unit = string.Empty;

            if (dataLayer.TryGetVariableDefinition(
                    variable.VariableId,
                    out DataVariableDefinition definition
                ))
            {
                if (!string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    displayName = definition.DisplayName;
                }

                unit = definition.Unit ?? string.Empty;
            }

            var legend =
                new VisualizationLegendInfo(
                    dataLayer.Id,
                    variable.VariableId,
                    displayName,
                    unit,
                    targetLayerId,
                    scale.Minimum,
                    scale.Maximum,
                    new Color32[]
                    {
                        new Color32(200, 204, 210, 255),
                        new Color32(70, 76, 86, 255)
                    },
                    "height " + scale.Description,
                    scale.Type,
                    null,
                    Color.gray
                )
                {
                    IsHeight = true,
                    SourceName = dataLayer.DisplayName ?? string.Empty,
                    Median = scale.Type == ScaleType.Rank ? scale.ValueAt(0.5f) : (double?)null
                };

            return legend.WithChannel(channel);
        }


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
        /// Categorical legend: one colour per labelled entry (e.g.
        /// stacked-bar segments, or the up/down parts of a
        /// bidirectional mark). Minimum/Maximum are not meaningful
        /// and are set to 0.
        /// </summary>
        public static VisualizationLegendInfo ForCategories(
            string dataLayerId,
            string title,
            string unit,
            string targetLayerId,
            IReadOnlyList<LegendCategory> categories,
            string description,
            Color noDataColor
        )
        {
            if (categories == null ||
                categories.Count == 0)
            {
                throw new ArgumentException(
                    "A categorical legend needs at least one entry.",
                    nameof(categories)
                );
            }


            var ramp =
                new Color32[
                    categories.Count
                ];


            for (
                int i = 0;
                i < categories.Count;
                i++
            )
            {
                ramp[i] =
                    categories[i].Color;
            }


            return new VisualizationLegendInfo(
                dataLayerId,
                title,
                title,
                unit,
                targetLayerId,
                0.0,
                0.0,
                ramp,
                description,
                ScaleType.Quantile,
                null,
                noDataColor
            )
            {
                Categories =
                    categories
            };
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
            )
            {
                SourceName = dataLayer.DisplayName ?? string.Empty,
                Median = scale.Type == ScaleType.Rank ? scale.ValueAt(0.5f) : (double?)null
            };
        }
    }
}
