using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using UrbanAnalytics.Rendering;
using UrbanAnalytics.UrbanContext;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// A world-space copy of the geometry currently drawn for one
    /// entity, grouped by the material it is drawn with.
    ///
    /// Used for hover/selection highlights, camera focus and the
    /// detached block copies of the comparison view. It is a
    /// snapshot: collect again after the visualization changes.
    /// </summary>
    public sealed class EntityGeometry
    {
        public sealed class Part
        {
            public Material Material;

            public readonly List<Vector3> Vertices =
                new List<Vector3>();

            public readonly List<Vector3> Normals =
                new List<Vector3>();

            public readonly List<Color32> Colors =
                new List<Color32>();

            public readonly List<int> Triangles =
                new List<int>();

            /// <summary>
            /// False when any source mesh had no normals; the
            /// built mesh then recalculates them.
            /// </summary>
            public bool HasNormals =
                true;
        }


        private readonly List<Part> parts =
            new List<Part>();


        private Bounds bounds;

        private bool hasBounds;


        public IReadOnlyList<Part> Parts =>
            parts;


        public bool IsEmpty =>
            !hasBounds;


        /// <summary>
        /// World-space bounds of all copied vertices.
        /// </summary>
        public Bounds Bounds =>
            bounds;


        public int TriangleCount
        {
            get
            {
                int count =
                    0;

                foreach (Part part in parts)
                {
                    count +=
                        part.Triangles.Count / 3;
                }

                return count;
            }
        }


        internal Part GetPart(
            Material material
        )
        {
            foreach (Part part in parts)
            {
                if (part.Material == material)
                {
                    return part;
                }
            }


            var created =
                new Part
                {
                    Material =
                        material
                };


            parts.Add(
                created
            );


            return created;
        }


        internal void Encapsulate(
            Vector3 point
        )
        {
            if (!hasBounds)
            {
                bounds =
                    new Bounds(
                        point,
                        Vector3.zero
                    );

                hasBounds =
                    true;

                return;
            }


            bounds.Encapsulate(
                point
            );
        }


        /// <summary>
        /// Builds a mesh for one part with vertices relative to
        /// origin (keeps float precision for small objects far
        /// from the world origin).
        /// </summary>
        public static Mesh BuildMesh(
            Part part,
            Vector3 origin,
            string name
        )
        {
            var vertices =
                new List<Vector3>(
                    part.Vertices.Count
                );


            foreach (Vector3 vertex in part.Vertices)
            {
                vertices.Add(
                    vertex - origin
                );
            }


            var mesh =
                new Mesh
                {
                    name =
                        name,

                    indexFormat =
                        vertices.Count > ushort.MaxValue
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };


            mesh.SetVertices(
                vertices
            );

            mesh.SetColors(
                part.Colors
            );

            mesh.SetTriangles(
                part.Triangles,
                0,
                true
            );


            if (part.HasNormals &&
                part.Normals.Count == vertices.Count)
            {
                mesh.SetNormals(
                    part.Normals
                );
            }
            else
            {
                mesh.RecalculateNormals();
            }


            return mesh;
        }


        /// <summary>
        /// All parts in one mesh (materials ignored), relative to
        /// origin. Used for single-colour overlays.
        /// </summary>
        public Mesh BuildMergedMesh(
            Vector3 origin,
            string name
        )
        {
            var vertices =
                new List<Vector3>();

            var triangles =
                new List<int>();


            foreach (Part part in parts)
            {
                int offset =
                    vertices.Count;


                foreach (Vector3 vertex in part.Vertices)
                {
                    vertices.Add(
                        vertex - origin
                    );
                }


                foreach (int index in part.Triangles)
                {
                    triangles.Add(
                        offset + index
                    );
                }
            }


            var mesh =
                new Mesh
                {
                    name =
                        name,

                    indexFormat =
                        vertices.Count > ushort.MaxValue
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };


            mesh.SetVertices(
                vertices
            );

            mesh.SetTriangles(
                triangles,
                0,
                true
            );


            return mesh;
        }
    }


    /// <summary>
    /// Copies the triangles of one entity out of the batched
    /// chunks that draw it, using the chunks' per-entity vertex
    /// and triangle ranges.
    ///
    /// Mesh arrays are cached per mesh; call Invalidate() after
    /// the visualization changed (colours or positions moved).
    /// </summary>
    public sealed class EntityGeometryCollector
    {
        private const int MaximumCachedMeshes =
            48;


        private sealed class MeshArrays
        {
            public Vector3[] Vertices;

            public Vector3[] Normals;

            public Color32[] Colors;

            public int[] Triangles;
        }


        private readonly InteractionContext context;


        private readonly Dictionary<Mesh, MeshArrays>
            cache =
                new Dictionary<Mesh, MeshArrays>();


        public EntityGeometryCollector(
            InteractionContext context
        )
        {
            this.context =
                context
                ?? throw new ArgumentNullException(
                    nameof(context)
                );
        }


        public void Invalidate()
        {
            cache.Clear();
        }


        /// <summary>
        /// Geometry of an entity as currently shown.
        ///
        /// A unit collects every shown chunk of its spatial layer
        /// that contains it (base surface, height column, glyph);
        /// with includeBuildings also the buildings associated to
        /// it. A building collects only itself.
        /// </summary>
        public EntityGeometry Collect(
            EntityReference entity,
            bool includeBuildings
        )
        {
            var geometry =
                new EntityGeometry();


            if (!entity.IsValid)
            {
                return geometry;
            }


            if (entity.Kind == EntityKind.Building)
            {
                BuildingIndex index =
                    context.Buildings;


                if (index != null &&
                    index.TryGet(
                        entity.Id,
                        out BuildingIndex.Location location
                    ) &&
                    InteractionContext.IsShown(
                        location.Chunk
                    ))
                {
                    AppendBuilding(
                        geometry,
                        location
                    );
                }


                return geometry;
            }


            foreach (
                SpatialMeshChunk chunk
                in context.SpatialChunks
            )
            {
                if (!InteractionContext.IsShown(chunk) ||
                    !string.Equals(
                        chunk.SpatialLayerId,
                        entity.SpatialLayerId,
                        StringComparison.Ordinal
                    ) ||
                    !chunk.TryGetUnitRange(
                        entity.Id,
                        out SpatialMeshUnitRange range
                    ))
                {
                    continue;
                }


                Append(
                    geometry,
                    chunk.Mesh,
                    chunk.transform.localToWorldMatrix,
                    chunk.MeshRenderer.sharedMaterial,
                    range.VertexStart,
                    range.VertexCount,
                    range.IndexStart,
                    range.IndexCount
                );
            }


            if (includeBuildings &&
                context.Buildings != null)
            {
                foreach (
                    BuildingIndex.Location location
                    in context.Buildings.GetForUnit(
                        entity.Id
                    )
                )
                {
                    if (InteractionContext.IsShown(
                            location.Chunk
                        ))
                    {
                        AppendBuilding(
                            geometry,
                            location
                        );
                    }
                }
            }


            return geometry;
        }


        /// <summary>
        /// The unit's flat footprint from its base spatial layer,
        /// whether or not that layer is currently shown (a height
        /// surface hides it). Used as a ground reference.
        /// </summary>
        public EntityGeometry CollectFootprint(
            EntityReference unit
        )
        {
            var geometry =
                new EntityGeometry();


            if (unit.Kind != EntityKind.SpatialUnit ||
                !context.Geometry.TryGetRenderedLayerRoot(
                    unit.SpatialLayerId,
                    out GameObject root
                ))
            {
                return geometry;
            }


            foreach (
                SpatialMeshChunk chunk
                in root.GetComponentsInChildren<SpatialMeshChunk>(
                    true
                )
            )
            {
                if (chunk.IsInitialized &&
                    chunk.TryGetUnitRange(
                        unit.Id,
                        out SpatialMeshUnitRange range
                    ))
                {
                    Append(
                        geometry,
                        chunk.Mesh,
                        chunk.transform.localToWorldMatrix,
                        chunk.MeshRenderer.sharedMaterial,
                        range.VertexStart,
                        range.VertexCount,
                        range.IndexStart,
                        range.IndexCount
                    );

                    break;
                }
            }


            return geometry;
        }


        private void AppendBuilding(
            EntityGeometry geometry,
            BuildingIndex.Location location
        )
        {
            BuildingMeshUnitRange range =
                location.Range;


            Append(
                geometry,
                location.Chunk.Mesh,
                location.Chunk.transform.localToWorldMatrix,
                location.Chunk.MeshRenderer.sharedMaterial,
                range.VertexStart,
                range.VertexCount,
                range.TriangleStart * 3,
                range.TriangleCount * 3
            );
        }


        private void Append(
            EntityGeometry geometry,
            Mesh mesh,
            Matrix4x4 localToWorld,
            Material material,
            int vertexStart,
            int vertexCount,
            int indexStart,
            int indexCount
        )
        {
            if (mesh == null ||
                vertexCount <= 0 ||
                indexCount <= 0)
            {
                return;
            }


            MeshArrays arrays =
                GetArrays(
                    mesh
                );


            EntityGeometry.Part part =
                geometry.GetPart(
                    material
                );


            int offset =
                part.Vertices.Count;


            bool hasNormals =
                arrays.Normals.Length ==
                arrays.Vertices.Length;


            bool hasColors =
                arrays.Colors.Length ==
                arrays.Vertices.Length;


            if (!hasNormals)
            {
                part.HasNormals =
                    false;
            }


            for (
                int i = vertexStart;
                i < vertexStart + vertexCount;
                i++
            )
            {
                Vector3 world =
                    localToWorld.MultiplyPoint3x4(
                        arrays.Vertices[i]
                    );


                part.Vertices.Add(
                    world
                );


                geometry.Encapsulate(
                    world
                );


                part.Normals.Add(
                    hasNormals
                        ? localToWorld
                            .MultiplyVector(
                                arrays.Normals[i]
                            )
                            .normalized
                        : Vector3.up
                );


                part.Colors.Add(
                    hasColors
                        ? arrays.Colors[i]
                        : new Color32(255, 255, 255, 255)
                );
            }


            for (
                int i = indexStart;
                i < indexStart + indexCount;
                i++
            )
            {
                part.Triangles.Add(
                    offset +
                    arrays.Triangles[i] -
                    vertexStart
                );
            }
        }


        private MeshArrays GetArrays(
            Mesh mesh
        )
        {
            if (cache.TryGetValue(
                    mesh,
                    out MeshArrays arrays
                ))
            {
                return arrays;
            }


            // Bounded cache: hovering across the city must not
            // keep a copy of every chunk alive.
            if (cache.Count >= MaximumCachedMeshes)
            {
                cache.Clear();
            }


            arrays =
                new MeshArrays
                {
                    Vertices =
                        mesh.vertices,

                    Normals =
                        mesh.normals,

                    Colors =
                        mesh.colors32,

                    Triangles =
                        mesh.triangles
                };


            cache.Add(
                mesh,
                arrays
            );


            return arrays;
        }
    }
}
