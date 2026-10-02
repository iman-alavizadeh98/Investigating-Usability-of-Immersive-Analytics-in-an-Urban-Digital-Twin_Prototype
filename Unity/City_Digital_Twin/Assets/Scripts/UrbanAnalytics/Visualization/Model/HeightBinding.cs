using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Data;

namespace UrbanAnalytics.Visualization
{
    public enum HeightBindingMode
    {
        /// <summary>One Primary Height variable, upward only.</summary>
        Unidirectional = 0,

        /// <summary>
        /// One Primary Height variable with a Diverging scale:
        /// above the centre goes up, below goes down.
        /// </summary>
        Signed = 1,

        /// <summary>
        /// A Positive-role variable goes up and a Negative-role
        /// variable goes down, on one shared scale.
        /// </summary>
        TwoSided = 2
    }


    /// <summary>
    /// Vertical extent of a mark relative to its base, in Unity
    /// units. Bottom ≤ 0 ≤ Top.
    /// </summary>
    public readonly struct HeightExtent
    {
        public float Bottom
        {
            get;
        }

        public float Top
        {
            get;
        }

        public bool HasData
        {
            get;
        }


        public HeightExtent(
            float bottom,
            float top,
            bool hasData
        )
        {
            Bottom =
                Mathf.Min(
                    0.0f,
                    bottom
                );

            Top =
                Mathf.Max(
                    0.0f,
                    top
                );

            HasData =
                hasData;
        }


        public static HeightExtent None =>
            new HeightExtent(
                0.0f,
                0.0f,
                false
            );


        public bool IsBelowBase =>
            Bottom < 0.0f;
    }


    /// <summary>
    /// Pure mapping from normalized scale output to extents.
    /// </summary>
    public static class HeightExtentMath
    {
        public static HeightExtent Unidirectional(
            float normalized,
            float maximumHeight
        )
        {
            return new HeightExtent(
                0.0f,
                Mathf.Clamp01(normalized) * maximumHeight,
                true
            );
        }


        /// <summary>
        /// normalized 0.5 = the diverging centre (zero height);
        /// 1 = +maximumHeight; 0 = -maximumHeight.
        /// </summary>
        public static HeightExtent Signed(
            float normalized,
            float maximumHeight
        )
        {
            float signed =
                (Mathf.Clamp01(normalized) - 0.5f) *
                2.0f *
                maximumHeight;


            return new HeightExtent(
                Mathf.Min(0.0f, signed),
                Mathf.Max(0.0f, signed),
                true
            );
        }


        public static HeightExtent TwoSided(
            float positiveNormalized,
            float negativeNormalized,
            float maximumHeight
        )
        {
            return new HeightExtent(
                -Mathf.Clamp01(negativeNormalized) * maximumHeight,
                Mathf.Clamp01(positiveNormalized) * maximumHeight,
                true
            );
        }
    }


    /// <summary>
    /// The Height encoding(s) of one layer, resolved against data.
    /// Shared by HeightSurface and BarGlyph so both read heights
    /// the same way.
    /// </summary>
    public sealed class ResolvedHeightBinding
    {
        private readonly DataLayer positiveLayer;

        private readonly DataLayer negativeLayer;


        public HeightBindingMode Mode
        {
            get;
        }

        public ResolvedNumericScale Scale
        {
            get;
        }

        public float MaximumHeight
        {
            get;
        }

        /// <summary>Primary (Unidirectional / Signed) or Positive variable.</summary>
        public DataVariableReference PositiveVariable
        {
            get;
        }

        /// <summary>TwoSided only.</summary>
        public DataVariableReference NegativeVariable
        {
            get;
        }

        public bool IsBidirectional =>
            Mode != HeightBindingMode.Unidirectional;


        private ResolvedHeightBinding(
            HeightBindingMode mode,
            ResolvedNumericScale scale,
            float maximumHeight,
            DataLayer positiveLayer,
            DataVariableReference positiveVariable,
            DataLayer negativeLayer,
            DataVariableReference negativeVariable
        )
        {
            Mode =
                mode;

            Scale =
                scale;

            MaximumHeight =
                maximumHeight;

            this.positiveLayer =
                positiveLayer;

            PositiveVariable =
                positiveVariable;

            this.negativeLayer =
                negativeLayer;

            NegativeVariable =
                negativeVariable;
        }


        /// <summary>
        /// Extent for one spatial unit. Two-sided marks need both
        /// values; a unit missing either has no data (no mark),
        /// so a missing side is never drawn as zero.
        /// </summary>
        public HeightExtent Evaluate(
            string unitId
        )
        {
            if (!positiveLayer.TryGetDouble(
                    unitId,
                    PositiveVariable.VariableId,
                    out double positive
                ))
            {
                return HeightExtent.None;
            }


            switch (Mode)
            {
                case HeightBindingMode.Signed:
                    return HeightExtentMath.Signed(
                        Scale.Normalize(positive),
                        MaximumHeight
                    );


                case HeightBindingMode.TwoSided:
                    {
                        if (!negativeLayer.TryGetDouble(
                                unitId,
                                NegativeVariable.VariableId,
                                out double negative
                            ))
                        {
                            return HeightExtent.None;
                        }


                        return HeightExtentMath.TwoSided(
                            Scale.Normalize(positive),
                            Scale.Normalize(negative),
                            MaximumHeight
                        );
                    }


                default:
                    return HeightExtentMath.Unidirectional(
                        Scale.Normalize(positive),
                        MaximumHeight
                    );
            }
        }


        /// <summary>
        /// Categorical legend for the up/down colours, used when a
        /// bidirectional mark has no Color encoding.
        /// </summary>
        public VisualizationLegendInfo CreateDirectionLegend(
            string targetLayerId,
            BidirectionalHeightSettings colors
        )
        {
            string upLabel;
            string downLabel;
            string title;


            if (Mode == HeightBindingMode.TwoSided)
            {
                upLabel =
                    "Up: " +
                    DisplayName(
                        positiveLayer,
                        PositiveVariable
                    );

                downLabel =
                    "Down: " +
                    DisplayName(
                        negativeLayer,
                        NegativeVariable
                    );

                title =
                    DisplayName(
                        positiveLayer,
                        PositiveVariable
                    ) +
                    " / " +
                    DisplayName(
                        negativeLayer,
                        NegativeVariable
                    );
            }
            else
            {
                string center =
                    Scale.Center.ToString(
                        "0.###",
                        System.Globalization.CultureInfo
                            .InvariantCulture
                    );

                upLabel =
                    "Above " + center;

                downLabel =
                    "Below " + center;

                title =
                    DisplayName(
                        positiveLayer,
                        PositiveVariable
                    );
            }


            return VisualizationLegendInfo.ForCategories(
                PositiveVariable.DataLayerId,
                title,
                UnitOf(
                    positiveLayer,
                    PositiveVariable
                ),
                targetLayerId,
                new[]
                {
                    new LegendCategory(
                        upLabel,
                        colors.PositiveColor
                    ),
                    new LegendCategory(
                        downLabel,
                        colors.NegativeColor
                    )
                },
                Mode == HeightBindingMode.TwoSided
                    ? "height up/down, " + Scale.Description
                    : "height " + Scale.Description,
                Color.gray
            );
        }


        internal static string DisplayName(
            DataLayer layer,
            DataVariableReference variable
        )
        {
            return layer.TryGetVariableDefinition(
                       variable.VariableId,
                       out DataVariableDefinition definition
                   ) &&
                   !string.IsNullOrWhiteSpace(
                       definition.DisplayName
                   )
                ? definition.DisplayName
                : variable.VariableId;
        }


        internal static string UnitOf(
            DataLayer layer,
            DataVariableReference variable
        )
        {
            return layer.TryGetVariableDefinition(
                       variable.VariableId,
                       out DataVariableDefinition definition
                   )
                ? definition.Unit ?? string.Empty
                : string.Empty;
        }


        public string Describe()
        {
            switch (Mode)
            {
                case HeightBindingMode.Signed:
                    return
                        $"signed {PositiveVariable.DataLayerId}." +
                        $"{PositiveVariable.VariableId} " +
                        $"({Scale.Description})";

                case HeightBindingMode.TwoSided:
                    return
                        $"up {PositiveVariable.DataLayerId}." +
                        $"{PositiveVariable.VariableId} / down " +
                        $"{NegativeVariable.DataLayerId}." +
                        $"{NegativeVariable.VariableId} " +
                        $"({Scale.Description})";

                default:
                    return
                        $"{PositiveVariable.DataLayerId}." +
                        $"{PositiveVariable.VariableId} " +
                        $"({Scale.Description})";
            }
        }


        /// <summary>
        /// Resolves the layer's Height encoding(s).
        ///
        /// - Positive and/or Negative roles → TwoSided (both are
        ///   required; mixing with Primary is an error). The
        ///   Positive encoding's scale and maximum height apply to
        ///   both sides, resolved over the pooled values.
        /// - Primary with a Diverging scale → Signed.
        /// - Primary otherwise → Unidirectional.
        ///
        /// includeZero: extend Linear domains to 0 (bar-like marks).
        /// </summary>
        public static async Task<ResolvedHeightBinding> ResolveAsync(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec,
            string targetSpatialLayerId,
            bool includeZero,
            CancellationToken cancellationToken
        )
        {
            spec.TryGetEncoding(
                VisualizationChannel.Height,
                VisualizationEncodingRole.Primary,
                out VisualizationEncodingSpec primary
            );

            spec.TryGetEncoding(
                VisualizationChannel.Height,
                VisualizationEncodingRole.Positive,
                out VisualizationEncodingSpec positive
            );

            spec.TryGetEncoding(
                VisualizationChannel.Height,
                VisualizationEncodingRole.Negative,
                out VisualizationEncodingSpec negative
            );


            if (positive != null ||
                negative != null)
            {
                if (positive == null ||
                    negative == null)
                {
                    throw new InvalidOperationException(
                        $"Layer '{spec.Id}': bidirectional height " +
                        $"needs both a Positive and a Negative " +
                        $"Height encoding."
                    );
                }


                if (primary != null)
                {
                    throw new InvalidOperationException(
                        $"Layer '{spec.Id}': a Primary Height " +
                        $"encoding cannot be combined with " +
                        $"Positive/Negative Height encodings."
                    );
                }


                return await ResolveTwoSidedAsync(
                    context,
                    spec,
                    positive,
                    negative,
                    targetSpatialLayerId,
                    includeZero,
                    cancellationToken
                );
            }


            if (primary == null)
            {
                throw new InvalidOperationException(
                    $"Layer '{spec.Id}' requires a Height encoding."
                );
            }


            DataVariableReference variable =
                RequireSingle(
                    spec,
                    primary
                );


            DataLayer layer =
                await LoadAsync(
                    context,
                    variable,
                    targetSpatialLayerId,
                    cancellationToken
                );


            if (primary.Scale.Type ==
                ScaleType.Diverging)
            {
                ResolvedNumericScale signedScale =
                    VisualizationScaleUtility.Resolve(
                        layer,
                        variable.VariableId,
                        primary.Scale
                    );


                return new ResolvedHeightBinding(
                    HeightBindingMode.Signed,
                    signedScale,
                    primary.Height.MaximumVisualHeight,
                    layer,
                    variable,
                    null,
                    null
                );
            }


            layer.TryGetNumericValues(
                variable.VariableId,
                out double[] values
            );


            ResolvedNumericScale scale =
                VisualizationScaleUtility.Resolve(
                    values ?? Array.Empty<double>(),
                    primary.Scale,
                    includeZero
                );


            return new ResolvedHeightBinding(
                HeightBindingMode.Unidirectional,
                scale,
                primary.Height.MaximumVisualHeight,
                layer,
                variable,
                null,
                null
            );
        }


        private static async Task<ResolvedHeightBinding>
            ResolveTwoSidedAsync(
                VisualizationRenderContext context,
                VisualizationLayerSpec spec,
                VisualizationEncodingSpec positive,
                VisualizationEncodingSpec negative,
                string targetSpatialLayerId,
                bool includeZero,
                CancellationToken cancellationToken
            )
        {
            ScaleType type =
                positive.Scale.Type;


            if (type != ScaleType.Linear &&
                type != ScaleType.Log)
            {
                throw new NotSupportedException(
                    $"Layer '{spec.Id}': two-sided height needs a " +
                    $"Linear or Log scale (got {type})."
                );
            }


            DataVariableReference positiveVariable =
                RequireSingle(
                    spec,
                    positive
                );

            DataVariableReference negativeVariable =
                RequireSingle(
                    spec,
                    negative
                );


            DataLayer positiveLayer =
                await LoadAsync(
                    context,
                    positiveVariable,
                    targetSpatialLayerId,
                    cancellationToken
                );

            DataLayer negativeLayer =
                await LoadAsync(
                    context,
                    negativeVariable,
                    targetSpatialLayerId,
                    cancellationToken
                );


            positiveLayer.TryGetNumericValues(
                positiveVariable.VariableId,
                out double[] positiveValues
            );

            negativeLayer.TryGetNumericValues(
                negativeVariable.VariableId,
                out double[] negativeValues
            );


            // One scale over both sides, so equal values have
            // equal lengths up and down.
            var pooled =
                new List<double>(
                    (positiveValues?.Length ?? 0) +
                    (negativeValues?.Length ?? 0)
                );

            pooled.AddRange(
                positiveValues ?? Array.Empty<double>()
            );

            pooled.AddRange(
                negativeValues ?? Array.Empty<double>()
            );


            ResolvedNumericScale scale =
                VisualizationScaleUtility.Resolve(
                    pooled.ToArray(),
                    positive.Scale,
                    includeZero
                );


            if (!Mathf.Approximately(
                    positive.Height.MaximumVisualHeight,
                    negative.Height.MaximumVisualHeight
                ))
            {
                Debug.LogWarning(
                    $"Layer '{spec.Id}': the Negative Height " +
                    $"encoding's maximum height is ignored; the " +
                    $"Positive encoding's value " +
                    $"({positive.Height.MaximumVisualHeight}) " +
                    $"applies to both sides."
                );
            }


            return new ResolvedHeightBinding(
                HeightBindingMode.TwoSided,
                scale,
                positive.Height.MaximumVisualHeight,
                positiveLayer,
                positiveVariable,
                negativeLayer,
                negativeVariable
            );
        }


        private static DataVariableReference RequireSingle(
            VisualizationLayerSpec spec,
            VisualizationEncodingSpec encoding
        )
        {
            if (!encoding.Data.TryGetSingle(
                    out DataVariableReference variable
                ))
            {
                throw new InvalidOperationException(
                    $"Layer '{spec.Id}': each Height encoding " +
                    $"needs exactly one variable."
                );
            }


            return variable;
        }


        private static async Task<DataLayer> LoadAsync(
            VisualizationRenderContext context,
            DataVariableReference variable,
            string targetSpatialLayerId,
            CancellationToken cancellationToken
        )
        {
            DataLayer layer =
                await context.GetDataLayerAsync(
                    variable.DataLayerId,
                    cancellationToken
                );


            if (!string.Equals(
                    layer.TargetSpatialLayerId,
                    targetSpatialLayerId,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{layer.Id}' targets " +
                    $"'{layer.TargetSpatialLayerId}', but the " +
                    $"visualization target is " +
                    $"'{targetSpatialLayerId}'."
                );
            }


            if (!layer.ContainsVariable(
                    variable.VariableId
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{layer.Id}' does not contain " +
                    $"'{variable.VariableId}'."
                );
            }


            return layer;
        }
    }
}
