using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Services available to visualization renderers.
    ///
    /// Renderers receive dependencies through this context instead
    /// of searching the Unity scene themselves.
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

        public Material VertexColorMaterial
        {
            get;
        }


        public VisualizationRenderContext(
            DataLayerManager dataLayers,
            GeometryManager geometry,
            Material vertexColorMaterial
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
        }


        public async Task<DataLayer> GetDataLayerAsync(
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


        public async Task<GameObject> GetRenderedSpatialLayerAsync(
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
                    $"Spatial layer '{spatialLayerId}' has no " +
                    $"rendered geometry."
                );
            }


            return root;
        }
    }
}