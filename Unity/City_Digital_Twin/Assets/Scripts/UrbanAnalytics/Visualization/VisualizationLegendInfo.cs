using System;
using UnityEngine;

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

        public double Minimum { get; }

        public double Maximum { get; }

        public Gradient Gradient { get; }

        public bool ReverseGradient { get; }

        public Color NoDataColor { get; }


        public VisualizationLegendInfo(
            string dataLayerId,
            string variableId,
            string variableDisplayName,
            string unit,
            string spatialLayerId,
            double minimum,
            double maximum,
            Gradient gradient,
            bool reverseGradient,
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

            Gradient = gradient
                ?? throw new ArgumentNullException(
                    nameof(gradient)
                );

            ReverseGradient = reverseGradient;

            NoDataColor = noDataColor;
        }
    }
}