using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Data;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// A multi-variable Segments encoding (DataBindingMode.Multiple)
    /// resolved against data: one entry per segment, in declared
    /// order, with its colour. Used by stacked-bar and radial
    /// glyphs.
    /// </summary>
    public sealed class SegmentBinding
    {
        public const string DefaultPaletteId =
            "tableau10";


        private readonly DataLayer[] layers;


        public VisualizationEncodingSpec Encoding
        {
            get;
        }

        public IReadOnlyList<DataVariableReference> Variables
        {
            get;
        }

        public IReadOnlyList<Color32> Colors
        {
            get;
        }

        public IReadOnlyList<string> Labels
        {
            get;
        }

        public int Count =>
            Variables.Count;

        /// <summary>Display name of the first segment's data layer.</summary>
        public string Title
        {
            get;
        }

        public string Unit
        {
            get;
        }


        private SegmentBinding(
            VisualizationEncodingSpec encoding,
            IReadOnlyList<DataVariableReference> variables,
            DataLayer[] layers,
            Color32[] colors,
            string[] labels,
            string title,
            string unit
        )
        {
            Encoding =
                encoding;

            Variables =
                variables;

            this.layers =
                layers;

            Colors =
                colors;

            Labels =
                labels;

            Title =
                title;

            Unit =
                unit;
        }


        /// <summary>Value of segment i for a unit, if valid.</summary>
        public bool TryGetValue(
            string unitId,
            int segment,
            out double value
        )
        {
            return layers[segment].TryGetDouble(
                unitId,
                Variables[segment].VariableId,
                out value
            );
        }


        /// <summary>All valid values of every segment, pooled.</summary>
        public double[] PooledValues()
        {
            var pooled =
                new List<double>();


            for (
                int i = 0;
                i < Count;
                i++
            )
            {
                if (layers[i].TryGetNumericValues(
                        Variables[i].VariableId,
                        out double[] values
                    ))
                {
                    pooled.AddRange(
                        values
                    );
                }
            }


            return pooled.ToArray();
        }


        public VisualizationLegendInfo CreateLegend(
            string targetLayerId,
            string description
        )
        {
            var categories =
                new LegendCategory[
                    Count
                ];


            for (
                int i = 0;
                i < Count;
                i++
            )
            {
                categories[i] =
                    new LegendCategory(
                        Labels[i],
                        Colors[i]
                    );
            }


            return VisualizationLegendInfo.ForCategories(
                Variables[0].DataLayerId,
                Title,
                Unit,
                targetLayerId,
                categories,
                description,
                Encoding.Color.NoDataColor
            );
        }


        /// <summary>
        /// Resolves the layer's Segments encoding. Needs
        /// DataBindingMode.Multiple with 2+ numeric variables whose
        /// data layers target targetSpatialLayerId. Colours come
        /// from the encoding's palette (default tableau10).
        /// </summary>
        public static async Task<SegmentBinding> ResolveAsync(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec,
            string targetSpatialLayerId,
            CancellationToken cancellationToken
        )
        {
            if (!spec.TryGetEncoding(
                    VisualizationChannel.Segments,
                    out VisualizationEncodingSpec encoding
                ))
            {
                throw new InvalidOperationException(
                    $"Glyph layer '{spec.Id}' requires a Segments " +
                    $"encoding."
                );
            }


            if (!encoding.Data.TryGetMultiple(
                    out IReadOnlyList<DataVariableReference>
                        variables
                ))
            {
                throw new InvalidOperationException(
                    $"Glyph layer '{spec.Id}': the Segments " +
                    $"encoding needs Data Mode = Multiple with at " +
                    $"least two configured variables."
                );
            }


            var layers =
                new DataLayer[
                    variables.Count
                ];

            var labels =
                new string[
                    variables.Count
                ];

            var colors =
                new Color32[
                    variables.Count
                ];


            ColorPalette palette =
                encoding.Color.ResolvePalette();


            if (palette == null)
            {
                ColorPaletteLibrary.TryGet(
                    DefaultPaletteId,
                    out palette
                );
            }


            string unit =
                null;

            bool mixedUnits =
                false;


            for (
                int i = 0;
                i < variables.Count;
                i++
            )
            {
                DataVariableReference variable =
                    variables[i];


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
                        $"glyph anchors come from " +
                        $"'{targetSpatialLayerId}'."
                    );
                }


                if (!layer.TryGetNumericValues(
                        variable.VariableId,
                        out _
                    ))
                {
                    throw new InvalidOperationException(
                        $"'{layer.Id}.{variable.VariableId}' is " +
                        $"missing or not numeric."
                    );
                }


                layers[i] =
                    layer;

                labels[i] =
                    ResolvedHeightBinding.DisplayName(
                        layer,
                        variable
                    );

                colors[i] =
                    GlyphRendering.SegmentColor(
                        palette,
                        encoding.Color.Reverse
                            ? variables.Count - 1 - i
                            : i,
                        variables.Count
                    );


                string variableUnit =
                    ResolvedHeightBinding.UnitOf(
                        layer,
                        variable
                    );


                if (unit == null)
                {
                    unit =
                        variableUnit;
                }
                else if (!string.Equals(
                             unit,
                             variableUnit,
                             StringComparison.Ordinal
                         ))
                {
                    mixedUnits =
                        true;
                }
            }


            if (mixedUnits)
            {
                Debug.LogWarning(
                    $"Glyph layer '{spec.Id}': segments have " +
                    $"different units; stacking or comparing them " +
                    $"may be meaningless."
                );
            }


            return new SegmentBinding(
                encoding,
                variables,
                layers,
                colors,
                labels,
                layers[0].DisplayName,
                mixedUnits
                    ? string.Empty
                    : unit ?? string.Empty
            );
        }
    }
}
