using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Data;
using UrbanAnalytics.UrbanContext;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Visualizes analytical data on building geometry
    /// through semantic association.
    ///
    /// Example:
    ///
    /// building
    ///     -> Ruta
    ///     -> income
    ///     -> building color
    ///
    /// Buildings can optionally follow the top surface
    /// produced by a HeightSurface renderer.
    /// </summary>
    public sealed class BuildingSurfaceRenderer :
        IVisualizationRenderer
    {
        private sealed class OriginalChunkState
        {
            public BuildingMeshChunk Chunk;

            public Vector3[] Vertices;

            public Color32[] Colors;

            public Material Material;

            // Only chunks whose vertices were moved get them
            // written back, so a colour-only layer does not
            // invalidate picking colliders on clear.
            public bool VerticesModified;
        }


        public bool CanRender(
            VisualizationLayerSpec spec
        )
        {
            return
                spec != null &&
                spec.Mark ==
                    VisualizationMark.Surface &&
                spec.Target != null &&
                spec.Target.Kind ==
                    VisualizationTargetKind
                        .UrbanContextLayer &&
                string.Equals(
                    spec.Target.LayerId,
                    "buildings",
                    StringComparison
                        .OrdinalIgnoreCase
                );
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
                context,
                spec
            );


            // =====================================================
            // OPTIONAL COLOR
            // =====================================================

            VisualizationEncodingSpec
                colorEncoding =
                    null;


            DataVariableReference
                colorVariable =
                    null;


            DataLayer colorDataLayer =
                null;


            ResolvedNumericScale?
                colorScale =
                    null;


            bool hasColorEncoding =
                spec.TryGetEncoding(
                    VisualizationChannel.Color,
                    out colorEncoding
                );


            if (hasColorEncoding)
            {
                if (!colorEncoding.Data.TryGetSingle(
                        out colorVariable
                    ))
                {
                    throw new InvalidOperationException(
                        "Building color currently requires " +
                        "exactly one variable."
                    );
                }


                colorDataLayer =
                    await context.GetDataLayerAsync(
                        colorVariable.DataLayerId,
                        cancellationToken
                    );


                if (!colorDataLayer.ContainsVariable(
                        colorVariable.VariableId
                    ))
                {
                    throw new InvalidOperationException(
                        $"Data layer " +
                        $"'{colorDataLayer.Id}' " +
                        $"does not contain " +
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
            // OPTIONAL HEIGHT FOLLOWING
            // =====================================================

            SurfaceElevationField elevationField =
                ResolveElevationField(
                    context,
                    spec
                );


            IReadOnlyList<BuildingMeshChunk>
                chunks =
                    await context
                        .GetBuildingChunksAsync(
                            cancellationToken
                        );


            var originalStates =
                new List<OriginalChunkState>(
                    chunks.Count
                );


            int coloredBuildings =
                0;


            int liftedBuildings =
                0;


            int noDataBuildings =
                0;


            int insetBuildings =
                0;


            // =====================================================
            // CHUNKS
            // =====================================================

            // Chunks are modified in place, one per frame. If this
            // render is cancelled (e.g. a newer visualization was
            // requested) or fails part-way, restore the chunks
            // already changed before rethrowing.
            try
            {
                foreach (
                    BuildingMeshChunk chunk
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


                    Vector3[] originalVertices =
                        mesh.vertices;


                    Color32[] originalColors =
                        mesh.colors32;


                    var originalState =
                        new OriginalChunkState
                        {
                            Chunk =
                                chunk,

                            Vertices =
                                originalVertices,

                            Colors =
                                originalColors,

                            Material =
                                chunk
                                    .MeshRenderer
                                    .sharedMaterial
                        };


                    originalStates.Add(
                        originalState
                    );


                    Vector3[] outputVertices =
                        (Vector3[])
                        originalVertices.Clone();


                    Color32[] outputColors =
                        CreateInitialColorBuffer(
                            mesh,
                            originalColors,
                            hasColorEncoding
                                ? colorEncoding
                                    .Color
                                    .NoDataColor
                                : (Color32)Color.white
                        );


                    // =============================================
                    // BUILDINGS IN THIS CHUNK
                    // =============================================

                    foreach (
                        BuildingMeshUnitRange range
                        in chunk.UnitRanges
                    )
                    {
                        string spatialUnitId =
                            ResolveAssociatedSpatialUnitId(
                                context,
                                spec,
                                range
                            );


                        bool hasAssociation =
                            !string.IsNullOrWhiteSpace(
                                spatialUnitId
                            );


                        // The surface being followed may be on another
                        // unit than the colour data (e.g. colour per
                        // voting district, columns per grid cell): the
                        // lift then needs the building's own unit in the
                        // followed layer.
                        string positionUnitId =
                            ResolvePositionUnitId(
                                context,
                                elevationField,
                                spatialUnitId,
                                range
                            );

                        bool hasPositionUnit =
                            !string.IsNullOrWhiteSpace(
                                positionUnitId
                            );


                        // -----------------------------------------
                        // POSITION
                        // -----------------------------------------

                        // Inset surface: the column top is the
                        // cell shrunk about an anchor. Shrink the
                        // building's footprint and position about
                        // the same anchor so it stays on the column.
                        if (elevationField != null &&
                            hasPositionUnit &&
                            elevationField.TryGetHorizontalInset(
                                positionUnitId,
                                out Vector3 anchorWorld,
                                out float insetScale
                            ))
                        {
                            Vector3 anchor =
                                chunk.transform
                                    .InverseTransformPoint(
                                        anchorWorld
                                    );


                            for (
                                int vertexIndex =
                                    range.VertexStart;
                                vertexIndex <
                                    range
                                        .VertexEndExclusive;
                                vertexIndex++
                            )
                            {
                                Vector3 vertex =
                                    outputVertices[
                                        vertexIndex
                                    ];


                                vertex.x =
                                    anchor.x +
                                    (vertex.x - anchor.x) *
                                    insetScale;


                                vertex.z =
                                    anchor.z +
                                    (vertex.z - anchor.z) *
                                    insetScale;


                                outputVertices[
                                    vertexIndex
                                ] =
                                    vertex;
                            }


                            insetBuildings++;
                        }


                        if (elevationField != null &&
                            hasPositionUnit &&
                            elevationField.TryGetTopOffset(
                                positionUnitId,
                                out float topOffset
                            ))
                        {
                            if (Mathf.Abs(
                                    topOffset
                                ) >
                                0.000001f)
                            {
                                for (
                                    int vertexIndex =
                                        range.VertexStart;
                                    vertexIndex <
                                        range
                                            .VertexEndExclusive;
                                    vertexIndex++
                                )
                                {
                                    outputVertices[
                                        vertexIndex
                                    ].y +=
                                        topOffset;
                                }


                                liftedBuildings++;
                            }
                        }


                        // -----------------------------------------
                        // COLOR
                        // -----------------------------------------

                        if (hasColorEncoding)
                        {
                            Color32 buildingColor =
                                colorEncoding
                                    .Color
                                    .NoDataColor;


                            if (hasAssociation &&
                                colorDataLayer.TryGetDouble(
                                    spatialUnitId,
                                    colorVariable
                                        .VariableId,
                                    out double value
                                ))
                            {
                                buildingColor =
                                    colorEncoding
                                        .Color
                                        .Evaluate(
                                            colorScale
                                                .Value
                                                .Normalize(
                                                    value
                                                )
                                        );


                                coloredBuildings++;
                            }
                            else
                            {
                                noDataBuildings++;
                            }


                            for (
                                int vertexIndex =
                                    range.VertexStart;
                                vertexIndex <
                                    range
                                        .VertexEndExclusive;
                                vertexIndex++
                            )
                            {
                                outputColors[
                                    vertexIndex
                                ] =
                                    buildingColor;
                            }
                        }
                    }


                    // =============================================
                    // APPLY
                    // =============================================

                    if (elevationField != null)
                    {
                        mesh.vertices =
                            outputVertices;


                        mesh.RecalculateBounds();


                        originalState.VerticesModified =
                            true;


                        chunk.NotifyGeometryChanged();
                    }


                    if (hasColorEncoding)
                    {
                        mesh.colors32 =
                            outputColors;


                        chunk.SetMaterial(
                            context
                                .BuildingVertexColorMaterial
                        );
                    }


                    await Task.Yield();
                }
            }
            catch
            {
                RestoreChunks(
                    originalStates
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


            if (hasColorEncoding &&
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
                        "Building colour (from " +
                        colorDataLayer.TargetSpatialLayerId +
                        ")"
                    )
                );
            }


            string colorDescription =
    hasColorEncoding
        ? colorVariable.DataLayerId +
          "." +
          colorVariable.VariableId
        : "none";


            string followSource =
                elevationField != null
                    ? elevationField.VisualizationLayerId
                    : "none";


            Debug.Log(
                $"Building surface visualization applied:\n" +
                $"Layer: {spec.Id}\n" +
                $"Association: {spec.Target.Mapping.AssociationId}\n" +
                $"Color: {colorDescription}\n" +
                $"Placement: {spec.UrbanContextPlacement.Mode}\n" +
                $"Follow source: {followSource}\n" +
                $"Colored buildings: {coloredBuildings}\n" +
                $"Lifted buildings: {liftedBuildings}\n" +
                $"Inset-scaled buildings: {insetBuildings}\n" +
                $"No-data/unmatched buildings: {noDataBuildings}"
            );


            // =====================================================
            // RUNTIME INSTANCE
            // =====================================================

            return new VisualizationLayerInstance(
                spec.Id,
                VisualizationMark.Surface,
                null,
                legends,
                cleanup:
                    () =>
                        RestoreChunks(
                            originalStates
                        )
            );
        }


        // =========================================================
        // RESTORE
        // =========================================================

        private static void RestoreChunks(
            List<OriginalChunkState> originalStates
        )
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


                if (state.VerticesModified &&
                    state.Vertices != null &&
                    state.Vertices.Length ==
                        state
                            .Chunk
                            .Mesh
                            .vertexCount)
                {
                    state
                        .Chunk
                        .Mesh
                        .vertices =
                            state.Vertices;


                    state
                        .Chunk
                        .Mesh
                        .RecalculateBounds();


                    state.Chunk.NotifyGeometryChanged();
                }


                if (state.Colors != null &&
                    state.Colors.Length ==
                        state
                            .Chunk
                            .Mesh
                            .vertexCount)
                {
                    state
                        .Chunk
                        .Mesh
                        .colors32 =
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


        // =========================================================
        // ELEVATION
        // =========================================================

        private static SurfaceElevationField
            ResolveElevationField(
                VisualizationRenderContext context,
                VisualizationLayerSpec spec
            )
        {
            if (spec.UrbanContextPlacement ==
                    null ||
                !spec
                    .UrbanContextPlacement
                    .IsFollowingHeightSurface)
            {
                return null;
            }


            string sourceLayerId =
                spec
                    .UrbanContextPlacement
                    .SourceVisualizationLayerId;


            if (string.IsNullOrWhiteSpace(
                    sourceLayerId
                ))
            {
                throw new InvalidOperationException(
                    $"Building visualization layer " +
                    $"'{spec.Id}' is configured to " +
                    $"FollowHeightSurface, but " +
                    $"Source Visualization Layer Id " +
                    $"is empty."
                );
            }


            if (!context
                    .RuntimeState
                    .TryGetSurfaceElevation(
                        sourceLayerId,
                        out SurfaceElevationField
                            field
                    ))
            {
                throw new InvalidOperationException(
                    $"Building visualization layer " +
                    $"'{spec.Id}' wants to follow " +
                    $"HeightSurface '{sourceLayerId}', " +
                    $"but that surface elevation is " +
                    $"not available yet. Put the " +
                    $"HeightSurface layer BEFORE the " +
                    $"building layer."
                );
            }


            return field;
        }


        // =========================================================
        // ASSOCIATION
        // =========================================================

        private static string
            ResolveAssociatedSpatialUnitId(
                VisualizationRenderContext context,
                VisualizationLayerSpec spec,
                BuildingMeshUnitRange range
            )
        {
            if (context.Associations.TryResolve(
                    spec
                        .Target
                        .Mapping
                        .AssociationId,
                    range.BuildingId,
                    out string spatialUnitId
                ))
            {
                return spatialUnitId;
            }


            if (!string.IsNullOrWhiteSpace(
                    range
                        .AssociatedSpatialUnitId
                ))
            {
                return
                    range
                        .AssociatedSpatialUnitId;
            }


            return null;
        }


        /// <summary>
        /// The building's unit in the followed surface's layer: the
        /// colour unit when it belongs to that layer, otherwise the
        /// target of the association "buildings_to_&lt;layer&gt;".
        /// </summary>
        private static string ResolvePositionUnitId(
            VisualizationRenderContext context,
            SurfaceElevationField field,
            string colorUnitId,
            BuildingMeshUnitRange range
        )
        {
            if (field == null)
            {
                return null;
            }

            string prefix =
                field.SpatialLayerId + ":";

            if (!string.IsNullOrEmpty(colorUnitId) &&
                colorUnitId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return colorUnitId;
            }

            if (context.Associations.TryResolve(
                    "buildings_to_" + field.SpatialLayerId,
                    range.BuildingId,
                    out string unitId
                ))
            {
                return unitId;
            }

            return !string.IsNullOrEmpty(range.AssociatedSpatialUnitId) &&
                   range.AssociatedSpatialUnitId.StartsWith(prefix, StringComparison.Ordinal)
                ? range.AssociatedSpatialUnitId
                : null;
        }


        // =========================================================
        // COLOR BUFFER
        // =========================================================

        private static Color32[]
            CreateInitialColorBuffer(
                Mesh mesh,
                Color32[] existing,
                Color32 fallback
            )
        {
            if (existing != null &&
                existing.Length ==
                    mesh.vertexCount)
            {
                return
                    (Color32[])
                    existing.Clone();
            }


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
                    fallback;
            }


            return colors;
        }


        // =========================================================
        // VALIDATION
        // =========================================================

        private static void ValidateTarget(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec
        )
        {
            if (spec.Target.Mapping == null ||
                spec.Target.Mapping.Mode !=
                    SpatialMappingMode
                        .Association)
            {
                throw new InvalidOperationException(
                    "Building visualization requires " +
                    "Target.Mapping.Mode = Association."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    spec
                        .Target
                        .Mapping
                        .AssociationId
                ))
            {
                throw new InvalidOperationException(
                    "Building visualization requires " +
                    "an Association ID."
                );
            }


            if (context.Associations == null)
            {
                throw new InvalidOperationException(
                    "BuildingSurfaceRenderer requires " +
                    "AssociationManager."
                );
            }
        }
    }
}