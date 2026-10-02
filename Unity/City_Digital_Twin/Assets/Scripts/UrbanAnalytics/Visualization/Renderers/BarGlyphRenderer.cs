using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Data;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// One square bar per derived anchor (e.g. per Ruta cell).
    ///
    /// Height: the layer's Height encoding(s) through
    /// ResolvedHeightBinding — upward, signed (Diverging scale) or
    /// two-sided (Positive up / Negative down). Linear domains are
    /// extended to include zero so bar lengths are proportional.
    ///
    /// Colour: optional single-variable Color encoding; otherwise
    /// the bidirectional up/down colours, or Glyph.DefaultColor.
    ///
    /// Units without height data get no bar (zero values get a
    /// flat bar, so "zero" and "no data" stay distinguishable).
    /// </summary>
    public sealed class BarGlyphRenderer :
        IVisualizationRenderer
    {
        public bool CanRender(
            VisualizationLayerSpec spec
        )
        {
            return
                spec != null &&
                spec.Mark ==
                    VisualizationMark.BarGlyph &&
                spec.Target != null &&
                spec.Target.Kind ==
                    VisualizationTargetKind.DerivedAnchors;
        }


        public async Task<
            VisualizationLayerInstance
        > RenderAsync(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec,
            CancellationToken cancellationToken
        )
        {
            GlyphTargets.ValidateDirect(
                spec
            );


            DerivedAnchorSet anchors =
                await context.GetDerivedAnchorsAsync(
                    spec.Target.LayerId,
                    cancellationToken
                );


            ResolvedHeightBinding height =
                await ResolvedHeightBinding.ResolveAsync(
                    context,
                    spec,
                    spec.Target.LayerId,
                    true,
                    cancellationToken
                );


            // ---- optional colour ---------------------------------

            DataLayer colorLayer =
                null;

            DataVariableReference colorVariable =
                null;

            ResolvedNumericScale colorScale =
                default;


            bool hasColor =
                spec.TryGetEncoding(
                    VisualizationChannel.Color,
                    out VisualizationEncodingSpec colorEncoding
                );


            if (hasColor)
            {
                if (!colorEncoding.Data.TryGetSingle(
                        out colorVariable
                    ))
                {
                    throw new InvalidOperationException(
                        $"Bar layer '{spec.Id}': Color needs " +
                        $"exactly one variable."
                    );
                }


                colorLayer =
                    await context.GetDataLayerAsync(
                        colorVariable.DataLayerId,
                        cancellationToken
                    );


                GlyphTargets.ValidateDataTarget(
                    colorLayer,
                    spec.Target.LayerId
                );


                colorScale =
                    VisualizationScaleUtility.Resolve(
                        colorLayer,
                        colorVariable.VariableId,
                        colorEncoding.Scale
                    );
            }


            SurfaceElevationField follow =
                GlyphRendering.ResolveFollowField(
                    context,
                    spec
                );


            float halfWidth =
                0.5f *
                spec.Glyph.WidthFraction *
                anchors.TypicalSize;


            // ---- geometry ----------------------------------------

            GameObject root =
                await GlyphRendering.CreateRootAsync(
                    context,
                    spec,
                    cancellationToken
                );


            int bars = 0;
            int noData = 0;
            int chunkIndex = 0;


            try
            {
                var accumulator =
                    new GlyphMeshAccumulator();


                foreach (DerivedAnchor anchor in anchors.Anchors)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();


                    HeightExtent extent =
                        height.Evaluate(
                            anchor.UnitId
                        );


                    if (!extent.HasData)
                    {
                        noData++;
                        continue;
                    }


                    Color32 upper;
                    Color32 lower;


                    if (hasColor)
                    {
                        upper =
                            colorLayer.TryGetDouble(
                                anchor.UnitId,
                                colorVariable.VariableId,
                                out double value
                            )
                                ? colorEncoding.Color.Evaluate(
                                    colorScale.Normalize(value)
                                )
                                : (Color32)colorEncoding
                                    .Color
                                    .NoDataColor;

                        lower =
                            upper;
                    }
                    else if (height.IsBidirectional)
                    {
                        upper =
                            spec.Bidirectional.PositiveColor;

                        lower =
                            spec.Bidirectional.NegativeColor;
                    }
                    else
                    {
                        upper =
                            spec.Glyph.DefaultColor;

                        lower =
                            upper;
                    }


                    Vector3 basePosition =
                        GlyphRendering.PlaceAnchor(
                            anchor,
                            follow,
                            root.transform
                        );


                    accumulator.BeginUnit(
                        anchor.UnitId
                    );


                    if (extent.Top > 0.0f ||
                        !extent.IsBelowBase)
                    {
                        accumulator.AppendBox(
                            basePosition,
                            halfWidth,
                            0.0f,
                            extent.Top,
                            upper
                        );
                    }


                    if (extent.IsBelowBase)
                    {
                        accumulator.AppendBox(
                            basePosition,
                            halfWidth,
                            extent.Bottom,
                            0.0f,
                            lower
                        );
                    }


                    accumulator.EndUnit();

                    bars++;


                    if (accumulator.VertexCount >=
                        GlyphRendering.MaxVerticesPerChunk)
                    {
                        GlyphRendering.CreateChunk(
                            accumulator,
                            root.transform,
                            spec.Target.LayerId,
                            chunkIndex++,
                            context.BuildingVertexColorMaterial
                        );

                        accumulator =
                            new GlyphMeshAccumulator();

                        await Task.Yield();
                    }
                }


                if (!accumulator.IsEmpty)
                {
                    GlyphRendering.CreateChunk(
                        accumulator,
                        root.transform,
                        spec.Target.LayerId,
                        chunkIndex++,
                        context.BuildingVertexColorMaterial
                    );
                }
            }
            catch
            {
                UnityEngine.Object.Destroy(
                    root
                );

                throw;
            }


            // ---- legend ------------------------------------------

            var legends =
                new List<VisualizationLegendInfo>();


            if (hasColor)
            {
                legends.Add(
                    VisualizationLegendInfo.ForColorEncoding(
                        colorLayer,
                        colorVariable,
                        colorEncoding,
                        spec.Target.LayerId,
                        colorScale
                    )
                );
            }
            else if (height.IsBidirectional)
            {
                legends.Add(
                    height.CreateDirectionLegend(
                        spec.Target.LayerId,
                        spec.Bidirectional
                    )
                );
            }


            Debug.Log(
                $"Bar glyphs rendered:\n" +
                $"Layer: {spec.Id}\n" +
                $"Anchors: {spec.Target.LayerId} " +
                $"({anchors.Anchors.Count}, typical size " +
                $"{anchors.TypicalSize:0.####})\n" +
                $"Height ({height.Mode}): {height.Describe()}\n" +
                $"Bars: {bars}  No data: {noData}  " +
                $"Chunks: {chunkIndex}\n" +
                $"Placement: {spec.UrbanContextPlacement.Mode}",
                root
            );


            return new VisualizationLayerInstance(
                spec.Id,
                VisualizationMark.BarGlyph,
                root,
                legends
            );
        }
    }


    /// <summary>Target validation shared by glyph renderers.</summary>
    internal static class GlyphTargets
    {
        public static void ValidateDirect(
            VisualizationLayerSpec spec
        )
        {
            if (spec.Target.Mapping != null &&
                spec.Target.Mapping.Mode !=
                    SpatialMappingMode.Direct)
            {
                throw new NotSupportedException(
                    $"Glyph layer '{spec.Id}': DerivedAnchors " +
                    $"targets support Direct mapping only (data " +
                    $"layers must target the anchor spatial layer)."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    spec.Target.LayerId
                ))
            {
                throw new InvalidOperationException(
                    $"Glyph layer '{spec.Id}': Target Layer Id " +
                    $"must name the spatial layer to anchor on."
                );
            }
        }


        public static void ValidateDataTarget(
            DataLayer layer,
            string spatialLayerId
        )
        {
            if (!string.Equals(
                    layer.TargetSpatialLayerId,
                    spatialLayerId,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{layer.Id}' targets " +
                    $"'{layer.TargetSpatialLayerId}', but the " +
                    $"glyph anchors come from '{spatialLayerId}'."
                );
            }
        }
    }
}
