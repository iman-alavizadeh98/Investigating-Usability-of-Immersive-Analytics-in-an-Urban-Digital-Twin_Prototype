using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Associations;
using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.UrbanContext;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Services available to visualization renderers.
    /// </summary>
    public sealed class VisualizationRenderContext
    {
        public DataLayerManager DataLayers
        {
            get;
        }


        public GeometryManager Geometry
        {
            get;
        }


        public UrbanContextManager UrbanContext
        {
            get;
        }


        public AssociationManager Associations
        {
            get;
        }


        public VisualizationRuntimeState RuntimeState
        {
            get;
        }


        public Material VertexColorMaterial
        {
            get;
        }


        public VisualizationRenderContext(
            DataLayerManager dataLayers,
            GeometryManager geometry,
            Material vertexColorMaterial,
            UrbanContextManager urbanContext,
            AssociationManager associations,
            VisualizationRuntimeState runtimeState
        )
        {
            DataLayers =
                dataLayers
                ?? throw new ArgumentNullException(
                    nameof(dataLayers)
                );


            Geometry =
                geometry
                ?? throw new ArgumentNullException(
                    nameof(geometry)
                );


            VertexColorMaterial =
                vertexColorMaterial
                ?? throw new ArgumentNullException(
                    nameof(vertexColorMaterial)
                );


            UrbanContext =
                urbanContext;


            Associations =
                associations;


            RuntimeState =
                runtimeState
                ?? throw new ArgumentNullException(
                    nameof(runtimeState)
                );
        }


        public async Task<DataLayer>
            GetDataLayerAsync(
                string dataLayerId,
                CancellationToken cancellationToken
            )
        {
            if (DataLayers.TryGetLayer(
                    dataLayerId,
                    out DataLayer existing
                ))
            {
                return existing;
            }


            return await DataLayers.LoadLayerAsync(
                dataLayerId,
                cancellationToken
            );
        }


        public async Task<GameObject>
            GetRenderedSpatialLayerAsync(
                string spatialLayerId,
                CancellationToken cancellationToken
            )
        {
            if (!Geometry.IsLayerRendered(
                    spatialLayerId
                ))
            {
                await Geometry.RenderLayerAsync(
                    spatialLayerId,
                    cancellationToken
                );
            }


            if (!Geometry.TryGetRenderedLayerRoot(
                    spatialLayerId,
                    out GameObject root
                ))
            {
                throw new InvalidOperationException(
                    $"Spatial layer '{spatialLayerId}' " +
                    $"has no rendered geometry."
                );
            }


            return root;
        }


        public async Task<
            IReadOnlyList<BuildingMeshChunk>
        > GetBuildingChunksAsync(
            CancellationToken cancellationToken
        )
        {
            if (UrbanContext == null)
            {
                throw new InvalidOperationException(
                    "No UrbanContextManager is available " +
                    "to the visualization system."
                );
            }


            if (UrbanContext.InitializationTask == null)
            {
                await Task.Yield();
            }


            cancellationToken
                .ThrowIfCancellationRequested();


            if (UrbanContext.InitializationTask == null)
            {
                throw new InvalidOperationException(
                    "UrbanContextManager did not start " +
                    "its initialization task."
                );
            }


            await UrbanContext.InitializationTask;


            cancellationToken
                .ThrowIfCancellationRequested();


            if (!UrbanContext.IsInitialized ||
                !UrbanContext.AreBuildingsLoaded)
            {
                throw new InvalidOperationException(
                    $"UrbanContext buildings are not available. " +
                    $"Error: {UrbanContext.LastError}"
                );
            }


            return UrbanContext.BuildingChunks;
        }
    }
}