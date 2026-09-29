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
    /// Renders an analytical surface where Height is controlled
    /// by one variable and Color can independently be controlled
    /// by another variable.
    ///
    /// Example:
    ///
    /// Height -> population
    /// Color  -> income
    /// </summary>
    public sealed class HeightSurfaceRenderer :
        IVisualizationRenderer
    {
        private sealed class RendererState
        {
            public MeshRenderer Renderer;

            public bool Enabled;
        }


        public VisualizationMark Mark =>
            VisualizationMark.HeightSurface;


        public async Task<VisualizationLayerInstance> RenderAsync(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec,
            CancellationToken cancellationToken
        )
        {
            ValidateTarget(
                spec
            );


            // -----------------------------------------------------
            // HEIGHT ENCODING
            // -----------------------------------------------------

            if (!spec.TryGetEncoding(
                    VisualizationChannel.Height,
                    out VisualizationEncodingSpec heightEncoding
                ))
            {
                throw new InvalidOperationException(
                    $"HeightSurface '{spec.Id}' requires a " +
                    $"Height encoding."
                );
            }


            if (!heightEncoding.Data.TryGetSingle(
                    out DataVariableReference heightVariable
                ))
            {
                throw new InvalidOperationException(
                    "Height currently requires exactly one variable."
                );
            }


            DataLayer heightDataLayer =
                await context.GetDataLayerAsync(
                    heightVariable.DataLayerId,
                    cancellationToken
                );


            ValidateDirectSpatialAssociation(
                heightDataLayer,
                spec.Target.LayerId
            );


            if (!heightDataLayer.ContainsVariable(
                    heightVariable.VariableId
                ))
            {
                throw new InvalidOperationException(
                    $"Data layer '{heightDataLayer.Id}' does not " +
                    $"contain '{heightVariable.VariableId}'."
                );
            }


            ResolvedNumericScale heightScale =
                VisualizationScaleUtility.Resolve(
                    heightDataLayer,
                    heightVariable.VariableId,
                    heightEncoding.Scale
                );


            // -----------------------------------------------------
            // OPTIONAL COLOR ENCODING
            // -----------------------------------------------------

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
                        "Color currently requires exactly one variable."
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
                        $"Data layer '{colorDataLayer.Id}' does not " +
                        $"contain '{colorVariable.VariableId}'."
                    );
                }


                colorScale =
                    VisualizationScaleUtility.Resolve(
                        colorDataLayer,
                        colorVariable.VariableId,
                        colorEncoding.Scale
                    );
            }


            // -----------------------------------------------------
            // SOURCE GEOMETRY
            // -----------------------------------------------------

            GameObject sourceRoot =
                await context.GetRenderedSpatialLayerAsync(
                    spec.Target.LayerId,
                    cancellationToken
                );


            SpatialMeshChunk[] sourceChunks =
                sourceRoot.GetComponentsInChildren<
                    SpatialMeshChunk
                >(
                    true
                );


            if (sourceChunks == null ||
                sourceChunks.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Spatial layer '{spec.Target.LayerId}' " +
                    $"contains no rendered mesh chunks."
                );
            }


            // -----------------------------------------------------
            // CREATE VISUALIZATION ROOT
            // -----------------------------------------------------

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
                sourceRoot.transform.localPosition;

            rootTransform.localRotation =
                sourceRoot.transform.localRotation;

            rootTransform.localScale =
                sourceRoot.transform.localScale;


            // -----------------------------------------------------
            // HIDE SOURCE RENDERERS
            // -----------------------------------------------------

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
                            sourceChunk.MeshRenderer,

                        Enabled =
                            sourceChunk.MeshRenderer.enabled
                    }
                );


                sourceChunk.MeshRenderer.enabled =
                    false;
            }


            // -----------------------------------------------------
            // BUILD VISUALIZATION CHUNKS
            // -----------------------------------------------------

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


                    HeightSurfaceMeshBuilder.Result result =
                        HeightSurfaceMeshBuilder.Build(
                            sourceChunk,

                            heightDataLayer,
                            heightVariable.VariableId,
                            heightScale,
                            heightEncoding.Height,
                            spec.HeightSurface,

                            colorDataLayer,
                            colorVariable?.VariableId,
                            colorScale,
                            colorEncoding?.Color
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
                        generatedChunk.MeshRenderer
                    );


                    await Task.Yield();
                }
            }
            catch
            {
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


            // -----------------------------------------------------
            // LEGENDS
            // -----------------------------------------------------

            var legends =
                new List<VisualizationLegendInfo>();


            if (colorEncoding != null &&
                colorDataLayer != null &&
                colorVariable != null &&
                colorScale.HasValue)
            {
                legends.Add(
                    CreateColorLegend(
                        colorDataLayer,
                        colorVariable,
                        colorEncoding,
                        spec.Target.LayerId,
                        colorScale.Value
                    )
                );
            }


            Debug.Log(
                $"Height visualization rendered:\n" +
                $"Layer: {spec.Id}\n" +
                $"Spatial target: {spec.Target.LayerId}\n" +
                $"Height: " +
                $"{heightVariable.DataLayerId}." +
                $"{heightVariable.VariableId}\n" +
                $"Method: {spec.HeightSurface.Method}\n" +
                $"Height range: " +
                $"[{heightScale.Minimum}, " +
                $"{heightScale.Maximum}]\n" +
                $"Max visual height: " +
                $"{heightEncoding.Height.MaximumVisualHeight}",
                visualizationRoot
            );


            return new VisualizationLayerInstance(
                spec.Id,
                Mark,
                visualizationRoot,
                legends,
                cleanup:
                    () =>
                    {
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


        private static VisualizationLegendInfo CreateColorLegend(
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
                    "HeightSurfaceRenderer currently supports " +
                    "SpatialLayer targets only."
                );
            }


            if (spec.Target.Mapping.Mode !=
                SpatialMappingMode.Direct)
            {
                throw new NotSupportedException(
                    "HeightSurfaceRenderer currently supports " +
                    "direct spatial mappings only."
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