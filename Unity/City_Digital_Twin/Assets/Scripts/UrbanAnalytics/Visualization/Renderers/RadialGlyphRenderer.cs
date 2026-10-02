using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// One flat radial ("rose") glyph per derived anchor.
    ///
    /// Each variable of the multi-variable Segments encoding gets
    /// an equal-angle wedge, starting at north and going
    /// clockwise in declared order. All wedges share one scale,
    /// resolved over the pooled values of every segment, so
    /// wedges are comparable within and across glyphs. Radius =
    /// sqrt(normalized) × max radius, so a wedge's area (what the
    /// eye compares) is proportional to its value. Linear domains
    /// are extended to zero.
    ///
    /// A missing value leaves its wedge out (a visible gap); a
    /// unit with no valid value gets no glyph. A small neutral hub
    /// marks every unit that has data, so all-zero units are still
    /// visible.
    /// </summary>
    public sealed class RadialGlyphRenderer :
        IVisualizationRenderer
    {
        /// <summary>Hub radius as a fraction of the maximum radius.</summary>
        private const float HubFraction =
            0.08f;

        /// <summary>Angular gap between wedges, fraction of a wedge.</summary>
        private const float GapFraction =
            0.06f;


        public bool CanRender(
            VisualizationLayerSpec spec
        )
        {
            return
                spec != null &&
                spec.Mark ==
                    VisualizationMark.RadialGlyph &&
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


            if (segments.Encoding.Scale.Type ==
                ScaleType.Diverging)
            {
                throw new NotSupportedException(
                    $"Radial layer '{spec.Id}': a radius cannot " +
                    $"be negative, so a Diverging scale is not " +
                    $"supported."
                );
            }


            ResolvedNumericScale scale =
                VisualizationScaleUtility.Resolve(
                    segments.PooledValues(),
                    segments.Encoding.Scale,
                    true
                );


            float maximumRadius =
                spec.Glyph.RadiusFraction *
                anchors.TypicalSize;

            float thickness =
                spec.Glyph.RadialThickness;

            int count =
                segments.Count;

            float wedgeAngle =
                2.0f * Mathf.PI / count;

            float gap =
                wedgeAngle * GapFraction * 0.5f;


            SurfaceElevationField follow =
                GlyphRendering.ResolveFollowField(
                    context,
                    spec
                );


            GameObject root =
                await GlyphRendering.CreateRootAsync(
                    context,
                    spec,
                    cancellationToken
                );


            int glyphs = 0;
            int noData = 0;
            int missingWedges = 0;
            int chunkIndex = 0;


            try
            {
                var accumulator =
                    new GlyphMeshAccumulator();


                foreach (DerivedAnchor anchor in anchors.Anchors)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();


                    Vector3 center =
                        GlyphRendering.PlaceAnchor(
                            anchor,
                            follow,
                            root.transform
                        );


                    accumulator.BeginUnit(
                        anchor.UnitId
                    );


                    int valid =
                        0;


                    for (
                        int i = 0;
                        i < count;
                        i++
                    )
                    {
                        if (!segments.TryGetValue(
                                anchor.UnitId,
                                i,
                                out double value
                            ))
                        {
                            missingWedges++;
                            continue;
                        }


                        valid++;


                        float start =
                            GlyphMath.WedgeStartAngle(
                                i,
                                count
                            );


                        accumulator.AppendWedge(
                            center,
                            GlyphMath.RadialRadius(
                                scale.Normalize(value),
                                maximumRadius
                            ),
                            start + gap,
                            start + wedgeAngle - gap,
                            0.0f,
                            thickness,
                            spec.Glyph.ArcSteps,
                            segments.Colors[i]
                        );
                    }


                    if (valid > 0)
                    {
                        // Hub: half thickness so wedges stand
                        // proud of it.
                        accumulator.AppendWedge(
                            center,
                            maximumRadius * HubFraction,
                            0.0f,
                            2.0f * Mathf.PI,
                            0.0f,
                            thickness * 0.5f,
                            Mathf.Max(
                                8,
                                spec.Glyph.ArcSteps
                            ),
                            segments.Encoding.Color.NoDataColor
                        );
                    }


                    if (accumulator.EndUnit())
                    {
                        glyphs++;
                    }
                    else
                    {
                        noData++;
                    }


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
                        "clockwise from north; area ~ value, " +
                        scale.Description
                    )
                };


            Debug.Log(
                $"Radial glyphs rendered:\n" +
                $"Layer: {spec.Id}\n" +
                $"Segments: {string.Join(", ", segments.Labels)}\n" +
                $"Scale: {scale.Description} " +
                $"[{scale.Minimum}, {scale.Maximum}], " +
                $"max radius {maximumRadius:0.####}\n" +
                $"Glyphs: {glyphs}  No data: {noData}  " +
                $"Missing wedges: {missingWedges}  " +
                $"Chunks: {chunkIndex}",
                root
            );


            return new VisualizationLayerInstance(
                spec.Id,
                VisualizationMark.RadialGlyph,
                root,
                legends
            );
        }
    }
}
