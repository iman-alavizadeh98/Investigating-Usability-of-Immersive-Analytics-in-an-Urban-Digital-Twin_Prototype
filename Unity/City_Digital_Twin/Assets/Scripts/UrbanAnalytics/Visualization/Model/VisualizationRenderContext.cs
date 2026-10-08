using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Associations;
using UrbanAnalytics.Core;
using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.Spatial;
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


        /// <summary>
        /// Shaded vertex-colour material for buildings.
        /// Falls back to VertexColorMaterial.
        /// </summary>
        public Material BuildingVertexColorMaterial
        {
            get;
        }


        public SpatialLayerManager SpatialLayers
        {
            get;
        }


        public SpatialReferenceManager SpatialReference
        {
            get;
        }


        private readonly Dictionary<string, DerivedAnchorSet>
            anchorCache =
                new Dictionary<string, DerivedAnchorSet>(
                    StringComparer.Ordinal
                );


        public VisualizationRenderContext(
            DataLayerManager dataLayers,
            GeometryManager geometry,
            Material vertexColorMaterial,
            UrbanContextManager urbanContext,
            AssociationManager associations,
            VisualizationRuntimeState runtimeState,
            Material buildingVertexColorMaterial = null,
            SpatialLayerManager spatialLayers = null,
            SpatialReferenceManager spatialReference = null
        )
        {
            SpatialLayers =
                spatialLayers;

            SpatialReference =
                spatialReference;

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


            BuildingVertexColorMaterial =
                buildingVertexColorMaterial != null
                    ? buildingVertexColorMaterial
                    : VertexColorMaterial;


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


            // A layer hidden by default (or hidden after an
            // earlier visualization) is shown while it is used.
            if (!root.activeSelf)
            {
                root.SetActive(
                    true
                );
            }

            usedSpatialLayers.Add(
                spatialLayerId
            );


            return root;
        }


        private readonly HashSet<string> usedSpatialLayers =
            new HashSet<string>(
                StringComparer.Ordinal
            );


        /// <summary>
        /// Spatial layers rendered for the current visualization.
        /// </summary>
        public IReadOnlyCollection<string> UsedSpatialLayers =>
            usedSpatialLayers;


        /// <summary>
        /// Hides the layers the cleared visualization used whose
        /// definition is not visible by default (e.g. DeSO and
        /// voting districts), so they do not stay on screen and
        /// overlap the next visualization.
        /// </summary>
        public void HideUnusedDefaultHiddenLayers()
        {
            foreach (string layerId in usedSpatialLayers)
            {
                bool visibleByDefault =
                    SpatialLayers != null &&
                    SpatialLayers.TryGetLayer(
                        layerId,
                        out SpatialLayer layer
                    ) &&
                    layer.Definition.VisibleByDefault;

                if (!visibleByDefault)
                {
                    Geometry.SetLayerVisible(
                        layerId,
                        false
                    );
                }
            }

            usedSpatialLayers.Clear();
        }


        /// <summary>
        /// One anchor per unit of a spatial layer (cached). The
        /// layer is rendered first so anchors sit on its surface.
        /// </summary>
        public async Task<DerivedAnchorSet>
            GetDerivedAnchorsAsync(
                string spatialLayerId,
                CancellationToken cancellationToken
            )
        {
            if (anchorCache.TryGetValue(
                    spatialLayerId,
                    out DerivedAnchorSet cached
                ))
            {
                return cached;
            }


            if (SpatialLayers == null ||
                SpatialReference == null)
            {
                throw new InvalidOperationException(
                    "Derived anchors need SpatialLayerManager " +
                    "and SpatialReferenceManager in the render " +
                    "context."
                );
            }


            GameObject root =
                await GetRenderedSpatialLayerAsync(
                    spatialLayerId,
                    cancellationToken
                );


            if (!SpatialLayers.TryGetLayer(
                    spatialLayerId,
                    out SpatialLayer layer
                ))
            {
                throw new InvalidOperationException(
                    $"Spatial layer '{spatialLayerId}' is not " +
                    $"loaded."
                );
            }


            DerivedAnchorSet anchors =
                DerivedAnchorSet.Build(
                    layer,
                    root.transform,
                    SpatialReference
                );


            anchorCache[spatialLayerId] =
                anchors;


            return anchors;
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