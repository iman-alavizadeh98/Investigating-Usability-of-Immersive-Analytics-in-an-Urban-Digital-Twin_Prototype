using System;
using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    public enum VisualizationChannel
    {
        Color = 0
    }


    public enum VisualizationRangeMode
    {
        DataMinMax = 0,
        Manual = 1
    }


    /// <summary>
    /// Describes how one analytical variable maps to
    /// a visual channel.
    ///
    /// It contains configuration only.
    /// It does not contain data values or geometry.
    /// </summary>
    [Serializable]
    public sealed class VisualizationEncoding
    {
        [Header("Data")]
        [SerializeField]
        private string dataLayerId;

        [SerializeField]
        private string variableId;


        [Header("Visual Channel")]
        [SerializeField]
        private VisualizationChannel channel =
            VisualizationChannel.Color;


        [Header("Range")]
        [SerializeField]
        private VisualizationRangeMode rangeMode =
            VisualizationRangeMode.DataMinMax;

        [SerializeField]
        private double manualMinimum = 0.0;

        [SerializeField]
        private double manualMaximum = 1.0;


        [Header("Color")]
        [SerializeField]
        private Gradient colorGradient =
            CreateDefaultGradient();

        [SerializeField]
        private Color noDataColor =
            new Color(
                0.35f,
                0.35f,
                0.35f,
                1.0f
            );

        [SerializeField]
        private bool reverseGradient;


        public string DataLayerId =>
            dataLayerId;

        public string VariableId =>
            variableId;

        public VisualizationChannel Channel =>
            channel;

        public VisualizationRangeMode RangeMode =>
            rangeMode;

        public double ManualMinimum =>
            manualMinimum;

        public double ManualMaximum =>
            manualMaximum;

        public Gradient ColorGradient =>
            colorGradient;

        public Color NoDataColor =>
            noDataColor;

        public bool ReverseGradient =>
            reverseGradient;


        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(dataLayerId) &&
            !string.IsNullOrWhiteSpace(variableId);


        /// <summary>
        /// Empty constructor required for Unity serialization.
        /// </summary>
        public VisualizationEncoding()
        {
        }


        /// <summary>
        /// Creates a runtime color encoding.
        /// </summary>
        public VisualizationEncoding(
            string dataLayerId,
            string variableId
        )
        {
            SetDataSource(
                dataLayerId,
                variableId
            );

            channel =
                VisualizationChannel.Color;
        }


        // =========================================================
        // DATA SOURCE
        // =========================================================

        public void SetDataSource(
            string newDataLayerId,
            string newVariableId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    newDataLayerId
                ))
            {
                throw new ArgumentException(
                    "DataLayer ID cannot be null or empty.",
                    nameof(newDataLayerId)
                );
            }


            if (string.IsNullOrWhiteSpace(
                    newVariableId
                ))
            {
                throw new ArgumentException(
                    "Variable ID cannot be null or empty.",
                    nameof(newVariableId)
                );
            }


            dataLayerId =
                newDataLayerId.Trim();

            variableId =
                newVariableId.Trim();
        }


        // =========================================================
        // RANGE
        // =========================================================

        public void UseDataRange()
        {
            rangeMode =
                VisualizationRangeMode.DataMinMax;
        }


        public void UseManualRange(
            double minimum,
            double maximum
        )
        {
            if (double.IsNaN(minimum) ||
                double.IsInfinity(minimum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimum)
                );
            }


            if (double.IsNaN(maximum) ||
                double.IsInfinity(maximum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum)
                );
            }


            if (maximum <= minimum)
            {
                throw new ArgumentException(
                    "Manual visualization maximum must be " +
                    "greater than minimum."
                );
            }


            manualMinimum =
                minimum;

            manualMaximum =
                maximum;

            rangeMode =
                VisualizationRangeMode.Manual;
        }


        // =========================================================
        // COLOR
        // =========================================================

        public void SetGradient(
            Gradient gradient
        )
        {
            colorGradient =
                gradient
                ?? throw new ArgumentNullException(
                    nameof(gradient)
                );
        }


        public void SetNoDataColor(
            Color color
        )
        {
            noDataColor =
                color;
        }


        public void SetReverseGradient(
            bool reverse
        )
        {
            reverseGradient =
                reverse;
        }


        public Color32 EvaluateColor(
            float normalizedValue
        )
        {
            float t =
                Mathf.Clamp01(
                    normalizedValue
                );


            if (reverseGradient)
            {
                t =
                    1.0f - t;
            }


            Gradient gradient =
                colorGradient;


            if (gradient == null)
            {
                gradient =
                    CreateDefaultGradient();
            }


            return (Color32)gradient.Evaluate(
                t
            );
        }


        public Color32 GetNoDataColor()
        {
            return (Color32)noDataColor;
        }


        // =========================================================
        // DEFAULT GRADIENT
        // =========================================================

        private static Gradient CreateDefaultGradient()
        {
            var gradient =
                new Gradient();


            GradientColorKey[] colorKeys =
            {
                new GradientColorKey(
                    new Color32(
                        68,
                        1,
                        84,
                        255
                    ),
                    0.0f
                ),

                new GradientColorKey(
                    new Color32(
                        33,
                        145,
                        140,
                        255
                    ),
                    0.5f
                ),

                new GradientColorKey(
                    new Color32(
                        253,
                        231,
                        37,
                        255
                    ),
                    1.0f
                )
            };


            GradientAlphaKey[] alphaKeys =
            {
                new GradientAlphaKey(
                    1.0f,
                    0.0f
                ),

                new GradientAlphaKey(
                    1.0f,
                    1.0f
                )
            };


            gradient.SetKeys(
                colorKeys,
                alphaKeys
            );


            return gradient;
        }
    }
}