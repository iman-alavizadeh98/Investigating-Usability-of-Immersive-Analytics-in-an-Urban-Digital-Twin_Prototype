using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Renders an analytical surface whose height is
    /// controlled by a numerical variable.
    ///
    /// Also publishes the actual generated top surface
    /// for every semantic spatial unit.
    /// </summary>
    public sealed class HeightSurfaceRenderer :
        IVisualizationRenderer
    {
        private sealed class RendererState
        {
            public MeshRenderer Renderer;

            public bool Enabled;
        }


        public bool CanRender(
            VisualizationLayerSpec spec
        )
        {
            return
                spec != null &&
                spec.Mark ==
                    VisualizationMark
                        .HeightSurface &&
                spec.Target != null &&
                spec.Target.Kind ==
                    VisualizationTargetKind
                        .SpatialLayer;
        }


        public async Task<
            VisualizationLayerInstance
        > RenderAsync(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec,
            CancellationToken cancellationToken
        )
        {
            ValidateTarget(
                spec
            );


            // =====================================================
            // HEIGHT
            // =====================================================

            // Unidirectional, Signed (Diverging scale: centre at the
            // base, above goes up, below goes down) or TwoSided
            // (Positive role up, Negative role down).
            ResolvedHeightBinding height =
                await ResolvedHeightBinding.ResolveAsync(
                    context,
                    spec,
                    spec.Target.LayerId,
                    false,
                    cancellationToken
                );


            HeightVisualizationMethod method =
                spec.HeightSurface.Method;


            if (height.IsBidirectional &&
                method ==
                    HeightVisualizationMethod
                        .DownwardExtrusion)
            {
                throw new NotSupportedException(
                    $"HeightSurface '{spec.Id}': " +
                    $"DownwardExtrusion cannot show a " +
                    $"bidirectional height. Use FullExtrusion " +
                    $"or InsetExtrusion."
                );
            }


            if (height.Mode ==
                    HeightBindingMode.TwoSided &&
                method ==
                    HeightVisualizationMethod
                        .SurfaceDisplacement)
            {
                throw new NotSupportedException(
                    $"HeightSurface '{spec.Id}': a displaced " +
                    $"plateau has one level, so it cannot show " +
                    $"two sides. Use FullExtrusion or " +
                    $"InsetExtrusion."
                );
            }


            // =====================================================
            // OPTIONAL COLOR
            // =====================================================

            DataLayer colorDataLayer =
                null;


            DataVariableReference colorVariable =
                null;


            VisualizationEncodingSpec colorEncoding =
                null;


            ResolvedNumericScale? colorScale =
                null;


            if (spec.TryGetEncoding(
                    VisualizationChannel.Color,
                    out colorEncoding
                ))
            {
                if (!colorEncoding.Data.TryGetSingle(
                        out colorVariable
                    ))
                {
                    throw new InvalidOperationException(
                        "Color currently requires exactly " +
                        "one variable."
                    );
                }


                colorDataLayer =
                    await context.GetDataLayerAsync(
                        colorVariable.DataLayerId,
                        cancellationToken
                    );


                ValidateDirectSpatialAssociation(
                    colorDataLayer,
                    spec.Target.LayerId
                );


                if (!colorDataLayer.ContainsVariable(
                        colorVariable.VariableId
                    ))
                {
                    throw new InvalidOperationException(
                        $"Data layer " +
                        $"'{colorDataLayer.Id}' does not " +
                        $"contain " +
                        $"'{colorVariable.VariableId}'."
                    );
                }


                colorScale =
                    VisualizationScaleUtility.Resolve(
                        colorDataLayer,
                        colorVariable.VariableId,
                        colorEncoding.Scale
                    );
            }


            // =====================================================
            // SOURCE
            // =====================================================

            GameObject sourceRoot =
                await context
                    .GetRenderedSpatialLayerAsync(
                        spec.Target.LayerId,
                        cancellationToken
                    );


            SpatialMeshChunk[] sourceChunks =
                sourceRoot
                    .GetComponentsInChildren<
                        SpatialMeshChunk
                    >(
                        true
                    );


            if (sourceChunks == null ||
                sourceChunks.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Spatial layer " +
                    $"'{spec.Target.LayerId}' contains " +
                    $"no rendered mesh chunks."
                );
            }


            // =====================================================
            // ROOT
            // =====================================================

            GameObject visualizationRoot =
                new GameObject(
                    $"__Visualization_{spec.Id}"
                );


            Transform rootTransform =
                visualizationRoot.transform;


            rootTransform.SetParent(
                sourceRoot.transform.parent,
                false
            );


            rootTransform.localPosition =
                sourceRoot
                    .transform
                    .localPosition;


            rootTransform.localRotation =
                sourceRoot
                    .transform
                    .localRotation;


            rootTransform.localScale =
                sourceRoot
                    .transform
                    .localScale;


            // =====================================================
            // HIDE ORIGINAL
            // =====================================================

            var originalRendererStates =
                new List<RendererState>(
                    sourceChunks.Length
                );


            foreach (
                SpatialMeshChunk sourceChunk
                in sourceChunks
            )
            {
                if (sourceChunk == null ||
                    sourceChunk.MeshRenderer == null)
                {
                    continue;
                }


                originalRendererStates.Add(
                    new RendererState
                    {
                        Renderer =
                            sourceChunk
                                .MeshRenderer,

                        Enabled =
                            sourceChunk
                                .MeshRenderer
                                .enabled
                    }
                );


                sourceChunk
                    .MeshRenderer
                    .enabled =
                        false;
            }


            // =====================================================
            // GENERATE
            // =====================================================

            var surfaceTopOffsets =
                new Dictionary<string, float>(
                    StringComparer.Ordinal
                );


            // InsetExtrusion shrinks each column about its unit
            // centroid; publish that anchor (world space) so
            // following context layers can shrink identically.
            float horizontalScale =
                HeightSurfaceMeshBuilder
                    .ResolveInsetFactor(
                        spec.HeightSurface
                    );


            var horizontalAnchors =
                new Dictionary<string, Vector3>(
                    StringComparer.Ordinal
                );


            // Colour per unit: the Color encoding if any (whole
            // column); otherwise bidirectional marks use the
            // positive/negative colours and unidirectional marks
            // keep the source surface colour (so a Surface colour
            // layer placed before this one shows through).
            BidirectionalHeightSettings directionColors =
                spec.Bidirectional;


            UnitHeightColors ColorsOf(
                SpatialMeshUnitRange range,
                Color32[] sourceColors
            )
            {
                if (colorDataLayer != null &&
                    colorScale.HasValue)
                {
                    Color32 dataColor =
                        colorDataLayer.TryGetDouble(
                            range.UnitId,
                            colorVariable.VariableId,
                            out double value
                        )
                            ? colorEncoding.Color.Evaluate(
                                colorScale.Value.Normalize(
                                    value
                                )
                            )
                            : (Color32)colorEncoding
                                .Color
                                .NoDataColor;


                    return new UnitHeightColors(
                        dataColor,
                        dataColor
                    );
                }


                if (height.IsBidirectional)
                {
                    return new UnitHeightColors(
                        directionColors.PositiveColor,
                        directionColors.NegativeColor
                    );
                }


                Color32 sourceColor =
                    sourceColors != null &&
                    range.VertexStart >= 0 &&
                    range.VertexStart < sourceColors.Length
                        ? sourceColors[range.VertexStart]
                        : new Color32(255, 255, 255, 255);


                return new UnitHeightColors(
                    sourceColor,
                    sourceColor
                );
            }


            try
            {
                foreach (
                    SpatialMeshChunk sourceChunk
                    in sourceChunks
                )
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();


                    if (sourceChunk == null ||
                        !sourceChunk.IsInitialized ||
                        sourceChunk.Mesh == null)
                    {
                        continue;
                    }


                    HeightSurfaceMeshBuilder.Result
                        result =
                            HeightSurfaceMeshBuilder
                                .Build(
                                    sourceChunk,
                                    height.Evaluate,
                                    ColorsOf,
                                    spec.HeightSurface
                                );


                    CollectActualTopOffsets(
                        sourceChunk,
                        result,
                        surfaceTopOffsets
                    );


                    GameObject chunkObject =
                        new GameObject(
                            sourceChunk.name +
                            "_Visualization"
                        );


                    Transform chunkTransform =
                        chunkObject.transform;


                    chunkTransform.SetParent(
                        rootTransform,
                        false
                    );


                    chunkTransform.localPosition =
                        Vector3.zero;


                    chunkTransform.localRotation =
                        Quaternion.identity;


                    chunkTransform.localScale =
                        Vector3.one;


                    SpatialMeshChunk generatedChunk =
                        chunkObject.AddComponent<
                            SpatialMeshChunk
                        >();


                    generatedChunk.Initialize(
                        spec.Target.LayerId,
                        sourceChunk.ChunkIndex,
                        result.Mesh,
                        result.UnitRanges,
                        result.TriangleUnitIds,
                        context.VertexColorMaterial,
                        false,
                        true
                    );


                    ConfigureRenderer(
                        generatedChunk
                            .MeshRenderer
                    );


                    if (horizontalScale < 1.0f)
                    {
                        CollectHorizontalAnchors(
                            sourceChunk,
                            chunkTransform,
                            horizontalAnchors
                        );
                    }


                    await Task.Yield();
                }


                context
                    .RuntimeState
                    .RegisterSurfaceElevation(
                        spec.Id,
                        spec.Target.LayerId,
                        surfaceTopOffsets,
                        horizontalScale,
                        horizontalScale < 1.0f
                            ? horizontalAnchors
                            : null
                    );
            }
            catch
            {
                context
                    .RuntimeState
                    .RemoveSurfaceElevation(
                        spec.Id
                    );


                foreach (
                    RendererState state
                    in originalRendererStates
                )
                {
                    if (state.Renderer != null)
                    {
                        state.Renderer.enabled =
                            state.Enabled;
                    }
                }


                UnityEngine.Object.Destroy(
                    visualizationRoot
                );


                throw;
            }


            // =====================================================
            // LEGEND
            // =====================================================

            var legends =
                new List<
                    VisualizationLegendInfo
                >();


            if (colorEncoding != null &&
                colorDataLayer != null &&
                colorVariable != null &&
                colorScale.HasValue)
            {
                legends.Add(
                    VisualizationLegendInfo.ForColorEncoding(
                        colorDataLayer,
                        colorVariable,
                        colorEncoding,
                        spec.Target.LayerId,
                        colorScale.Value
                    ).WithChannel(
                        "Column colour"
                    )
                );
            }
            else if (height.IsBidirectional)
            {
                legends.Add(
                    height.CreateDirectionLegend(
                        spec.Target.LayerId,
                        spec.Bidirectional
                    ).WithChannel(
                        "Column colour"
                    )
                );
            }


            // One legend per height variable, so every encoded
            // variable is named on screen.
            legends.AddRange(
                height.CreateHeightLegends(
                    spec.Target.LayerId
                )
            );


            Debug.Log(
                $"Height visualization rendered:\n" +
                $"Layer: {spec.Id}\n" +
                $"Spatial target: " +
                $"{spec.Target.LayerId}\n" +
                $"Height ({height.Mode}): " +
                $"{height.Describe()}\n" +
                $"Method: " +
                $"{spec.HeightSurface.Method}\n" +
                $"Height range: " +
                $"[{height.Scale.Minimum}, " +
                $"{height.Scale.Maximum}]\n" +
                $"Max visual height: " +
                $"{height.MaximumHeight}\n" +
                $"Published surface offsets: " +
                $"{surfaceTopOffsets.Count}",
                visualizationRoot
            );


            return new VisualizationLayerInstance(
                spec.Id,
                VisualizationMark.HeightSurface,
                visualizationRoot,
                legends,
                cleanup:
                    () =>
                    {
                        context
                            .RuntimeState
                            .RemoveSurfaceElevation(
                                spec.Id
                            );


                        foreach (
                            RendererState state
                            in originalRendererStates
                        )
                        {
                            if (state.Renderer != null)
                            {
                                state.Renderer.enabled =
                                    state.Enabled;
                            }
                        }
                    }
            );
        }


        // =========================================================
        // GENERATED TOP SURFACE
        // =========================================================

        private static void
            CollectActualTopOffsets(
                SpatialMeshChunk sourceChunk,
                HeightSurfaceMeshBuilder.Result
                    result,
                IDictionary<string, float> output
            )
        {
            if (sourceChunk == null ||
                result == null ||
                result.Mesh == null)
            {
                return;
            }


            Vector3[] sourceVertices =
                sourceChunk.Mesh.vertices;


            Vector3[] generatedVertices =
                result.Mesh.vertices;


            int count =
                Mathf.Min(
                    sourceChunk.UnitRanges.Count,
                    result.UnitRanges.Count
                );


            for (
                int i = 0;
                i < count;
                i++
            )
            {
                SpatialMeshUnitRange sourceRange =
                    sourceChunk.UnitRanges[i];


                SpatialMeshUnitRange generatedRange =
                    result.UnitRanges[i];


                float sourceTop =
                    FindMaximumY(
                        sourceVertices,
                        sourceRange.VertexStart,
                        sourceRange.VertexCount
                    );


                float generatedTop =
                    FindMaximumY(
                        generatedVertices,
                        generatedRange.VertexStart,
                        generatedRange.VertexCount
                    );


                float offset =
                    Mathf.Max(
                        0.0f,
                        generatedTop -
                        sourceTop
                    );


                output[
                    sourceRange.UnitId
                ] =
                    offset;
            }
        }


        /// <summary>
        /// World-space inset anchor per unit: the same centroid
        /// HeightSurfaceMeshBuilder shrinks the column about,
        /// transformed by the generated chunk (which shares the
        /// source chunk's local space).
        /// </summary>
        private static void
            CollectHorizontalAnchors(
                SpatialMeshChunk sourceChunk,
                Transform generatedChunkTransform,
                IDictionary<string, Vector3> output
            )
        {
            Vector3[] sourceVertices =
                sourceChunk.Mesh.vertices;


            foreach (
                SpatialMeshUnitRange range
                in sourceChunk.UnitRanges
            )
            {
                Vector3 centroidLocal =
                    HeightSurfaceMeshBuilder
                        .CalculateCentroid(
                            sourceVertices,
                            range
                        );


                output[
                    range.UnitId
                ] =
                    generatedChunkTransform
                        .TransformPoint(
                            centroidLocal
                        );
            }
        }


        private static float FindMaximumY(
            Vector3[] vertices,
            int start,
            int count
        )
        {
            if (vertices == null ||
                vertices.Length == 0 ||
                count <= 0)
            {
                return 0.0f;
            }


            float maximum =
                float.NegativeInfinity;


            int end =
                Mathf.Min(
                    vertices.Length,
                    start + count
                );


            for (
                int i =
                    Mathf.Max(
                        0,
                        start
                    );
                i < end;
                i++
            )
            {
                maximum =
                    Mathf.Max(
                        maximum,
                        vertices[i].y
                    );
            }


            return
                float.IsNegativeInfinity(
                    maximum
                )
                    ? 0.0f
                    : maximum;
        }


        // =========================================================
        // RENDERER
        // =========================================================

        private static void ConfigureRenderer(
            MeshRenderer renderer
        )
        {
            if (renderer == null)
            {
                return;
            }


            renderer.shadowCastingMode =
                ShadowCastingMode.Off;


            renderer.receiveShadows =
                false;


            renderer.lightProbeUsage =
                LightProbeUsage.Off;


            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.Off;
        }


        // =========================================================
        // VALIDATION
        // =========================================================

        private static void ValidateTarget(
            VisualizationLayerSpec spec
        )
        {
            if (spec.Target.Kind !=
                VisualizationTargetKind
                    .SpatialLayer)
            {
                throw new NotSupportedException(
                    "HeightSurfaceRenderer supports " +
                    "SpatialLayer targets only."
                );
            }


            if (spec.Target.Mapping.Mode !=
                SpatialMappingMode.Direct)
            {
                throw new NotSupportedException(
                    "HeightSurfaceRenderer supports " +
                    "direct spatial mappings only."
                );
            }
        }


        private static void
            ValidateDirectSpatialAssociation(
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