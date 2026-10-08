using System;
using System.Collections.Generic;
using UnityEngine;

using UrbanAnalytics.Core;
using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.Spatial;
using UrbanAnalytics.UrbanContext;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// Read-only access to the runtime systems that interaction
    /// features need: unit lookup, unit anchors, the building
    /// index and the chunks currently in the scene.
    ///
    /// Not a MonoBehaviour; owned by InteractionManager.
    /// </summary>
    public sealed class InteractionContext
    {
        private readonly Dictionary<string, string>
            unitLayerCache =
                new Dictionary<string, string>(
                    StringComparer.Ordinal
                );


        private List<SpatialMeshChunk>
            spatialChunks;


        private BuildingIndex
            buildingIndex;


        public DataLayerManager DataLayers
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


        public GeometryManager Geometry
        {
            get;
        }


        /// <summary>
        /// May be null when the scene has no urban context.
        /// </summary>
        public UrbanContextManager UrbanContext
        {
            get;
        }


        public VisualizationManager Visualization
        {
            get;
        }


        public InteractionContext(
            DataLayerManager dataLayers,
            SpatialLayerManager spatialLayers,
            SpatialReferenceManager spatialReference,
            GeometryManager geometry,
            UrbanContextManager urbanContext,
            VisualizationManager visualization
        )
        {
            DataLayers =
                dataLayers
                ?? throw new ArgumentNullException(
                    nameof(dataLayers)
                );

            SpatialLayers =
                spatialLayers
                ?? throw new ArgumentNullException(
                    nameof(spatialLayers)
                );

            SpatialReference =
                spatialReference
                ?? throw new ArgumentNullException(
                    nameof(spatialReference)
                );

            Geometry =
                geometry
                ?? throw new ArgumentNullException(
                    nameof(geometry)
                );

            UrbanContext =
                urbanContext;

            Visualization =
                visualization
                ?? throw new ArgumentNullException(
                    nameof(visualization)
                );
        }


        // =========================================================
        // UNITS
        // =========================================================

        /// <summary>
        /// Finds the loaded spatial layer that contains a unit.
        /// Unit IDs are normally "layerId:rawId"; the prefix is
        /// tried first, then every loaded layer.
        /// </summary>
        public bool TryResolveUnitLayer(
            string unitId,
            out string spatialLayerId
        )
        {
            spatialLayerId =
                null;


            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }


            if (unitLayerCache.TryGetValue(
                    unitId,
                    out spatialLayerId
                ))
            {
                return spatialLayerId != null;
            }


            int separator =
                unitId.IndexOf(':');


            if (separator > 0)
            {
                string prefix =
                    unitId.Substring(
                        0,
                        separator
                    );


                if (SpatialLayers.TryGetUnit(
                        prefix,
                        unitId,
                        out _
                    ))
                {
                    spatialLayerId =
                        prefix;
                }
            }


            if (spatialLayerId == null)
            {
                foreach (
                    KeyValuePair<string, SpatialLayer> pair
                    in SpatialLayers.LoadedLayers
                )
                {
                    if (pair.Value.TryGetUnit(
                            unitId,
                            out _
                        ))
                    {
                        spatialLayerId =
                            pair.Key;

                        break;
                    }
                }
            }


            unitLayerCache[unitId] =
                spatialLayerId;


            return spatialLayerId != null;
        }


        public bool TryGetUnit(
            EntityReference entity,
            out SpatialUnit unit
        )
        {
            unit =
                null;


            return
                entity.HasUnit &&
                SpatialLayers.TryGetUnit(
                    entity.SpatialLayerId,
                    entity.UnitId,
                    out unit
                );
        }


        /// <summary>
        /// The unit centroid on the base plane of its rendered
        /// spatial layer, in world space (the same anchor glyphs
        /// stand on).
        /// </summary>
        public bool TryGetUnitAnchorWorld(
            string spatialLayerId,
            string unitId,
            out Vector3 anchorWorld
        )
        {
            anchorWorld =
                default;


            if (!SpatialLayers.TryGetUnit(
                    spatialLayerId,
                    unitId,
                    out SpatialUnit unit
                ) ||
                !Geometry.TryGetRenderedLayerRoot(
                    spatialLayerId,
                    out GameObject root
                ))
            {
                return false;
            }


            anchorWorld =
                root.transform.TransformPoint(
                    SpatialReference.ToUnity(
                        unit.Centroid.Easting,
                        unit.Centroid.Northing,
                        0.0
                    )
                );


            return true;
        }


        /// <summary>
        /// Builds the reference for a picked building, resolving
        /// the layer of its associated unit.
        /// </summary>
        public EntityReference CreateBuildingReference(
            BuildingMeshUnitRange range
        )
        {
            string layerId =
                null;

            string unitId =
                ResolveBuildingUnit(
                    range
                );


            if (!string.IsNullOrWhiteSpace(
                    unitId
                ))
            {
                TryResolveUnitLayer(
                    unitId,
                    out layerId
                );
            }


            return EntityReference.ForBuilding(
                range.BuildingId,
                layerId != null
                    ? unitId
                    : null,
                layerId
            );
        }


        // =========================================================
        // BUILDINGS
        // =========================================================

        /// <summary>
        /// Building ID → chunk/range and unit → buildings, built
        /// on first use after the buildings have loaded. Null
        /// while no buildings are loaded.
        /// </summary>
        public BuildingIndex Buildings
        {
            get
            {
                if (buildingIndex != null)
                {
                    return buildingIndex;
                }


                if (UrbanContext == null ||
                    !UrbanContext.AreBuildingsLoaded)
                {
                    return null;
                }


                buildingIndex =
                    BuildingIndex.Build(
                        UrbanContext.BuildingChunks,
                        Associations
                    );


                return buildingIndex;
            }
        }


        private Associations.AssociationManager associations;


        /// <summary>
        /// Building → unit links (from the city package). Found on
        /// first use; null in scenes without an AssociationManager.
        /// </summary>
        public Associations.AssociationManager Associations
        {
            get
            {
                if (associations == null)
                {
                    associations =
                        UnityEngine.Object.FindFirstObjectByType<Associations.AssociationManager>();
                }

                return associations;
            }
        }


        /// <summary>
        /// A building's home unit: the unit stored with the building
        /// (legacy binary), else the target of the urban-context
        /// manager's building association (buildings_to_ruta).
        /// </summary>
        public string ResolveBuildingUnit(
            BuildingMeshUnitRange range
        )
        {
            if (!string.IsNullOrWhiteSpace(range.AssociatedSpatialUnitId))
            {
                return range.AssociatedSpatialUnitId;
            }

            string associationId =
                UrbanContext != null
                    ? UrbanContext.BuildingToRutaAssociationId
                    : null;

            return Associations != null &&
                   !string.IsNullOrWhiteSpace(associationId) &&
                   Associations.TryResolve(associationId, range.BuildingId, out string unitId)
                ? unitId
                : null;
        }


        // =========================================================
        // CHUNKS IN THE SCENE
        // =========================================================

        /// <summary>
        /// Every active SpatialMeshChunk in the scene: base
        /// spatial layers and visualization chunks (columns,
        /// glyphs). Cached until InvalidateScene().
        /// </summary>
        public IReadOnlyList<SpatialMeshChunk> SpatialChunks
        {
            get
            {
                if (spatialChunks == null)
                {
                    spatialChunks =
                        new List<SpatialMeshChunk>(
                            UnityEngine.Object.FindObjectsByType<
                                SpatialMeshChunk
                            >(
                                FindObjectsInactive.Exclude,
                                FindObjectsSortMode.None
                            )
                        );
                }


                spatialChunks.RemoveAll(
                    chunk => chunk == null
                );


                return spatialChunks;
            }
        }


        public IReadOnlyList<BuildingMeshChunk> BuildingChunks =>
            UrbanContext != null &&
            UrbanContext.AreBuildingsLoaded
                ? UrbanContext.BuildingChunks
                : Array.Empty<BuildingMeshChunk>();


        /// <summary>
        /// Call after the visualization changed: chunks were
        /// created or destroyed.
        /// </summary>
        public void InvalidateScene()
        {
            spatialChunks =
                null;
        }


        /// <summary>
        /// A chunk counts as shown when it is active, initialized
        /// and its renderer is on. Height surfaces hide the flat
        /// source chunks this way.
        /// </summary>
        public static bool IsShown(
            SpatialMeshChunk chunk
        )
        {
            return
                chunk != null &&
                chunk.isActiveAndEnabled &&
                chunk.IsInitialized &&
                chunk.Mesh != null &&
                chunk.MeshRenderer != null &&
                chunk.MeshRenderer.enabled;
        }


        public static bool IsShown(
            BuildingMeshChunk chunk
        )
        {
            return
                chunk != null &&
                chunk.isActiveAndEnabled &&
                chunk.IsInitialized &&
                chunk.MeshRenderer.enabled;
        }
    }


    /// <summary>
    /// Lookup tables over the batched building chunks.
    /// </summary>
    public sealed class BuildingIndex
    {
        public readonly struct Location
        {
            public BuildingMeshChunk Chunk
            {
                get;
            }


            public BuildingMeshUnitRange Range
            {
                get;
            }


            public Location(
                BuildingMeshChunk chunk,
                BuildingMeshUnitRange range
            )
            {
                Chunk =
                    chunk;

                Range =
                    range;
            }
        }


        private static readonly IReadOnlyList<Location>
            NoBuildings =
                Array.Empty<Location>();


        private readonly Dictionary<string, Location>
            byId =
                new Dictionary<string, Location>(
                    StringComparer.Ordinal
                );


        private readonly Dictionary<string, List<Location>>
            byUnit =
                new Dictionary<string, List<Location>>(
                    StringComparer.Ordinal
                );


        public int BuildingCount =>
            byId.Count;


        public static BuildingIndex Build(
            IReadOnlyList<BuildingMeshChunk> chunks
        )
        {
            return Build(
                chunks,
                null
            );
        }


        /// <summary>
        /// Also indexes every building under the target unit of each
        /// building association ("buildings_to_*"), so units of any
        /// layer (grid cell, DeSO area, voting district) know their
        /// buildings.
        /// </summary>
        public static BuildingIndex Build(
            IReadOnlyList<BuildingMeshChunk> chunks,
            Associations.AssociationManager associations
        )
        {
            var index =
                new BuildingIndex();

            var buildingAssociations =
                new List<string>();

            if (associations != null)
            {
                foreach (string id in associations.AssociationIds)
                {
                    if (id.StartsWith("buildings_to_", StringComparison.Ordinal))
                    {
                        buildingAssociations.Add(id);
                    }
                }
            }


            foreach (BuildingMeshChunk chunk in chunks)
            {
                if (chunk == null)
                {
                    continue;
                }


                foreach (
                    BuildingMeshUnitRange range
                    in chunk.UnitRanges
                )
                {
                    var location =
                        new Location(
                            chunk,
                            range
                        );


                    index.byId[range.BuildingId] =
                        location;


                    if (!string.IsNullOrWhiteSpace(
                            range.AssociatedSpatialUnitId
                        ))
                    {
                        index.AddToUnit(
                            range.AssociatedSpatialUnitId,
                            location
                        );
                    }


                    foreach (string associationId in buildingAssociations)
                    {
                        if (associations.TryResolve(
                                associationId,
                                range.BuildingId,
                                out string unitId
                            ) &&
                            unitId != range.AssociatedSpatialUnitId)
                        {
                            index.AddToUnit(
                                unitId,
                                location
                            );
                        }
                    }
                }
            }


            return index;
        }


        private void AddToUnit(
            string unitId,
            Location location
        )
        {
            if (!byUnit.TryGetValue(
                    unitId,
                    out List<Location> list
                ))
            {
                list =
                    new List<Location>();

                byUnit.Add(
                    unitId,
                    list
                );
            }

            list.Add(
                location
            );
        }


        public bool TryGet(
            string buildingId,
            out Location location
        )
        {
            location =
                default;


            return
                !string.IsNullOrWhiteSpace(buildingId) &&
                byId.TryGetValue(
                    buildingId,
                    out location
                );
        }


        public IReadOnlyList<Location> GetForUnit(
            string unitId
        )
        {
            return
                !string.IsNullOrWhiteSpace(unitId) &&
                byUnit.TryGetValue(
                    unitId,
                    out List<Location> list
                )
                    ? list
                    : NoBuildings;
        }
    }
}
