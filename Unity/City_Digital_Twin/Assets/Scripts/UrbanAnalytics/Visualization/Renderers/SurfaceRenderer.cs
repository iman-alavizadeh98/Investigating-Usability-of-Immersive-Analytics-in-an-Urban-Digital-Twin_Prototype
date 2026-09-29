using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Colors an existing SpatialLayer surface.
    ///
    /// This is the equivalent of the old color visualization,
    /// implemented as one renderer inside the new library.
    /// </summary>
    public sealed class SurfaceRenderer :
        IVisualizationRenderer
    {
        private sealed class OriginalChunkState
        {
            public SpatialMeshChunk Chunk;

            public Color32[] Colors;

            public Material Material;
        }


        public VisualizationMark Mark =>
            VisualizationMark.Surface;


        public async Task<VisualizationLayerInstance> RenderAsync(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec,
            CancellationToken cancellationToken
        )
        {
            ValidateTarget(
                spec
            );


            if (!spec.TryGetEncoding(
                    VisualizationChannel.Color,
                    out VisualizationEncodingSpec colorEncoding
                ))
            {
                throw new InvalidOperationException(
                    $"Surface visualization layer '{spec.Id}' " +
                    $"requires a Color encoding."
                );
            }


            if (!colorEncoding.Data.TryGetSingle(
                    out DataVariableReference colorVariable
                ))
            {
                throw new InvalidOperationException(
                    "Color currently requires exactly one variable."
                );
            }


            DataLayer dataLayer =
                await context.GetDataLayerAsync(
                    colorVariable.DataLayerId,
                    cancellationToken
                );


            ValidateDirectSpatialAssociation(
                dataLayer,
                spec.Target.LayerId
            );


            if (!dataLayer.ContainsVariable(
                    colorVariable.VariableId
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{dataLayer.Id}' does not contain " +
                    $"'{colorVariable.VariableId}'."
                );
            }


            ResolvedNumericScale scale =
                VisualizationScaleUtility.Resolve(
                    dataLayer,
                    colorVariable.VariableId,
                    colorEncoding.Scale
                );


            GameObject spatialRoot =
                await context.GetRenderedSpatialLayerAsync(
                    spec.Target.LayerId,
                    cancellationToken
                );


            SpatialMeshChunk[] chunks =
                spatialRoot.GetComponentsInChildren<
                    SpatialMeshChunk
                >(
                    true
                );


            var originalStates =
                new List<OriginalChunkState>(
                    chunks.Length
                );


            Color32 noDataColor =
                colorEncoding.Color.NoDataColor;


            foreach (
                SpatialMeshChunk chunk
                in chunks
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                if (chunk == null ||
                    !chunk.IsInitialized ||
                    chunk.Mesh == null)
                {
                    continue;
                }


                Mesh mesh =
                    chunk.Mesh;


                var state =
                    new OriginalChunkState
                    {
                        Chunk = chunk,

                        Colors =
                            mesh.colors32,

                        Material =
                            chunk.MeshRenderer.sharedMaterial
                    };


                originalStates.Add(
                    state
                );


                Color32[] colors =
                    new Color32[
                        mesh.vertexCount
                    ];


                for (
                    int i = 0;
                    i < colors.Length;
                    i++
                )
                {
                    colors[i] =
                        noDataColor;
                }


                foreach (
                    SpatialMeshUnitRange range
                    in chunk.UnitRanges
                )
                {
                    Color32 unitColor =
                        noDataColor;


                    if (dataLayer.TryGetDouble(
                            range.UnitId,
                            colorVariable.VariableId,
                            out double value
                        ))
                    {
                        float normalized =
                            scale.Normalize(
                                value
                            );


                        unitColor =
                            colorEncoding.Color.Evaluate(
                                normalized
                            );
                    }


                    for (
                        int vertexIndex = range.VertexStart;
                        vertexIndex < range.VertexEndExclusive;
                        vertexIndex++
                    )
                    {
                        colors[
                            vertexIndex
                        ] = unitColor;
                    }
                }


                mesh.colors32 =
                    colors;


                chunk.SetMaterial(
                    context.VertexColorMaterial
                );


                await Task.Yield();
            }


            VisualizationLegendInfo legend =
                CreateLegend(
                    dataLayer,
                    colorVariable,
                    colorEncoding,
                    spec.Target.LayerId,
                    scale
                );


            return new VisualizationLayerInstance(
                spec.Id,
                Mark,
                null,
                new[]
                {
                    legend
                },
                cleanup:
                    () =>
                    {
                        foreach (
                            OriginalChunkState state
                            in originalStates
                        )
                        {
                            if (state.Chunk == null ||
                                state.Chunk.Mesh == null)
                            {
                                continue;
                            }


                            if (state.Colors != null &&
                                state.Colors.Length ==
                                state.Chunk.Mesh.vertexCount)
                            {
                                state.Chunk.Mesh.colors32 =
                                    state.Colors;
                            }


                            if (state.Material != null)
                            {
                                state.Chunk.SetMaterial(
                                    state.Material
                                );
                            }
                        }
                    }
            );
        }


        private static VisualizationLegendInfo CreateLegend(
            DataLayer dataLayer,
            DataVariableReference variable,
            VisualizationEncodingSpec encoding,
            string spatialLayerId,
            ResolvedNumericScale scale
        )
        {
            string displayName =
                variable.VariableId;

            string unit =
                string.Empty;


            if (dataLayer.TryGetVariableDefinition(
                    variable.VariableId,
                    out DataVariableDefinition definition
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


            return new VisualizationLegendInfo(
                dataLayer.Id,
                variable.VariableId,
                displayName,
                unit,
                spatialLayerId,
                scale.Minimum,
                scale.Maximum,
                encoding.Color.GetGradient(),
                encoding.Color.Reverse,
                encoding.Color.NoDataColor
            );
        }


        private static void ValidateTarget(
            VisualizationLayerSpec spec
        )
        {
            if (spec.Target.Kind !=
                VisualizationTargetKind.SpatialLayer)
            {
                throw new NotSupportedException(
                    "SurfaceRenderer currently supports " +
                    "SpatialLayer targets only."
                );
            }


            if (spec.Target.Mapping.Mode !=
                SpatialMappingMode.Direct)
            {
                throw new NotSupportedException(
                    "SurfaceRenderer currently supports " +
                    "direct spatial mapping only."
                );
            }
        }


        private static void ValidateDirectSpatialAssociation(
            DataLayer dataLayer,
            string spatialLayerId
        )
        {
            if (!string.Equals(
                    dataLayer.TargetSpatialLayerId,
                    spatialLayerId,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{dataLayer.Id}' targets " +
                    $"'{dataLayer.TargetSpatialLayerId}', " +
                    $"but visualization target is " +
                    $"'{spatialLayerId}'."
                );
            }
        }
    }
}