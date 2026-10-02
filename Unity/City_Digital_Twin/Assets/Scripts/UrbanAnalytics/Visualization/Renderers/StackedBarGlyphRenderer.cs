using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// One stacked bar per derived anchor.
    ///
    /// Segments: a multi-variable Segments encoding, stacked in
    /// declared order from the bottom. Total bar height = the
    /// Segments scale applied to the SUM of the segments (Linear
    /// only, domain extended to zero, so heights are proportional);
    /// each segment's share of the height equals its share of the
    /// sum. Maximum height = the Segments encoding's Height
    /// settings.
    ///
    /// Note the sum is not a published total: SCB perturbs small
    /// counts, so sub-groups need not add up to "Totalt".
    ///
    /// A unit with any missing or negative segment value gets no
    /// bar (an incomplete stack would misstate the total); such
    /// units are counted in the log.
    /// </summary>
    public sealed class StackedBarGlyphRenderer :
        IVisualizationRenderer
    {
        public bool CanRender(
            VisualizationLayerSpec spec
        )
        {
            return
                spec != null &&
                spec.Mark ==
                    VisualizationMark.StackedBarGlyph &&
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


            SegmentBinding segments =
                await SegmentBinding.ResolveAsync(
                    context,
                    spec,
                    spec.Target.LayerId,
                    cancellationToken
                );


            if (segments.Encoding.Scale.Type !=
                ScaleType.Linear)
            {
                throw new NotSupportedException(
                    $"Stacked bar layer '{spec.Id}': segment " +
                    $"heights are shares of a linear total, so the " +
                    $"Segments scale must be Linear (got " +
                    $"{segments.Encoding.Scale.Type})."
                );
            }


            // ---- per-unit values and totals ----------------------

            int count =
                segments.Count;

            var unitValues =
                new Dictionary<string, double[]>(
                    anchors.Anchors.Count,
                    StringComparer.Ordinal
                );

            var totals =
                new List<double>(
                    anchors.Anchors.Count
                );

            int incomplete = 0;
            int negative = 0;


            foreach (DerivedAnchor anchor in anchors.Anchors)
            {
                var values =
                    new double[count];

                bool complete =
                    true;

                bool hasNegative =
                    false;

                double sum =
                    0.0;


                for (
                    int i = 0;
                    i < count;
                    i++
                )
                {
                    if (!segments.TryGetValue(
                            anchor.UnitId,
                            i,
                            out values[i]
                        ))
                    {
                        complete =
                            false;

                        break;
                    }


                    if (values[i] < 0.0)
                    {
                        hasNegative =
                            true;
                    }


                    sum +=
                        values[i];
                }


                if (!complete)
                {
                    incomplete++;
                    continue;
                }


                if (hasNegative)
                {
                    negative++;
                    continue;
                }


                unitValues[anchor.UnitId] =
                    values;

                totals.Add(
                    sum
                );
            }


            if (totals.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Stacked bar layer '{spec.Id}': no anchor has " +
                    $"a complete, non-negative set of segment " +
                    $"values."
                );
            }


            ResolvedNumericScale scale =
                VisualizationScaleUtility.Resolve(
                    totals.ToArray(),
                    segments.Encoding.Scale,
                    true
                );


            float maximumHeight =
                segments.Encoding.Height.MaximumVisualHeight;


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
            int chunkIndex = 0;


            try
            {
                var accumulator =
                    new GlyphMeshAccumulator();


                foreach (DerivedAnchor anchor in anchors.Anchors)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();


                    if (!unitValues.TryGetValue(
                            anchor.UnitId,
                            out double[] values
                        ))
                    {
                        continue;
                    }


                    double sum =
                        0.0;

                    foreach (double value in values)
                    {
                        sum +=
                            value;
                    }


                    float totalHeight =
                        scale.Normalize(sum) *
                        maximumHeight;


                    float[] boundaries =
                        GlyphMath.StackBoundaries(
                            values,
                            totalHeight
                        );


                    Vector3 basePosition =
                        GlyphRendering.PlaceAnchor(
                            anchor,
                            follow,
                            root.transform
                        );


                    accumulator.BeginUnit(
                        anchor.UnitId
                    );


                    bool drewSegment =
                        false;


                    for (
                        int i = 0;
                        i < count;
                        i++
                    )
                    {
                        if (boundaries[i + 1] <= boundaries[i])
                        {
                            continue;
                        }


                        accumulator.AppendBox(
                            basePosition,
                            halfWidth,
                            boundaries[i],
                            boundaries[i + 1],
                            segments.Colors[i]
                        );

                        drewSegment =
                            true;
                    }


                    // Zero total: a flat neutral tile marks
                    // "zero" (as opposed to "no data").
                    if (!drewSegment)
                    {
                        accumulator.AppendBox(
                            basePosition,
                            halfWidth,
                            0.0f,
                            0.0f,
                            segments.Encoding.Color.NoDataColor
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


            var legends =
                new List<VisualizationLegendInfo>
                {
                    segments.CreateLegend(
                        spec.Target.LayerId,
                        "stacked bottom to top; height = sum, " +
                        scale.Description
                    )
                };


            Debug.Log(
                $"Stacked bar glyphs rendered:\n" +
                $"Layer: {spec.Id}\n" +
                $"Segments: {string.Join(", ", segments.Labels)}\n" +
                $"Total scale: {scale.Description} " +
                $"[{scale.Minimum}, {scale.Maximum}], " +
                $"max height {maximumHeight}\n" +
                $"Bars: {bars}  Incomplete (missing segment): " +
                $"{incomplete}  Negative values: {negative}  " +
                $"Chunks: {chunkIndex}",
                root
            );


            return new VisualizationLayerInstance(
                spec.Id,
                VisualizationMark.StackedBarGlyph,
                root,
                legends
            );
        }
    }
}
