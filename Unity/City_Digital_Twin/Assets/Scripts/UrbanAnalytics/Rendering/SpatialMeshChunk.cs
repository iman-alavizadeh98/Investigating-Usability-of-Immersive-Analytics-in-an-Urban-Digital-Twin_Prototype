using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace UrbanAnalytics.Rendering
{
    /// <summary>
    /// Describes the section of a chunk mesh belonging to one
    /// semantic SpatialUnit.
    ///
    /// Vertices are intentionally not shared between different
    /// spatial units. This allows visualization channels such as
    /// color and height to be changed independently for each unit.
    /// </summary>
    public readonly struct SpatialMeshUnitRange
    {
        public string UnitId
        {
            get;
        }

        public int VertexStart
        {
            get;
        }

        public int VertexCount
        {
            get;
        }

        /// <summary>
        /// Index of the first triangle, not the first mesh index.
        /// </summary>
        public int TriangleStart
        {
            get;
        }

        public int TriangleCount
        {
            get;
        }

        public int VertexEndExclusive =>
            VertexStart + VertexCount;

        public int TriangleEndExclusive =>
            TriangleStart + TriangleCount;

        public int IndexStart =>
            TriangleStart * 3;

        public int IndexCount =>
            TriangleCount * 3;


        public SpatialMeshUnitRange(
            string unitId,
            int vertexStart,
            int vertexCount,
            int triangleStart,
            int triangleCount
        )
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                throw new ArgumentException(
                    "Spatial unit ID cannot be null or empty.",
                    nameof(unitId)
                );
            }

            if (vertexStart < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(vertexStart)
                );
            }

            if (vertexCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(vertexCount),
                    "Vertex count must be greater than zero."
                );
            }

            if (triangleStart < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triangleStart)
                );
            }

            if (triangleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triangleCount),
                    "Triangle count must be greater than zero."
                );
            }

            UnitId =
                unitId.Trim();

            VertexStart =
                vertexStart;

            VertexCount =
                vertexCount;

            TriangleStart =
                triangleStart;

            TriangleCount =
                triangleCount;
        }


        public override string ToString()
        {
            return
                $"{UnitId}: " +
                $"vertices [{VertexStart}, {VertexEndExclusive}), " +
                $"triangles [{TriangleStart}, {TriangleEndExclusive})";
        }
    }


    /// <summary>
    /// Runtime Unity representation of a batch of SpatialUnits.
    ///
    /// A SpatialMeshChunk contains one Unity Mesh representing
    /// multiple semantic spatial units while retaining enough
    /// information to identify and update individual units.
    ///
    /// This component does not:
    /// - create or triangulate geometry;
    /// - contain analytical values;
    /// - decide visualization encodings;
    /// - perform CRS conversion;
    /// - manage spatial-layer lifecycle.
    ///
    /// Those responsibilities belong to other systems.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SpatialMeshChunk : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;

        private Mesh runtimeMesh;

        private bool ownsMesh;

        private string[] triangleToUnitId;

        private readonly Dictionary<string, SpatialMeshUnitRange>
            rangesByUnitId =
                new Dictionary<string, SpatialMeshUnitRange>(
                    StringComparer.Ordinal
                );

        private IReadOnlyList<SpatialMeshUnitRange>
            unitRanges =
                Array.Empty<SpatialMeshUnitRange>();


        public string SpatialLayerId
        {
            get;
            private set;
        }


        public int ChunkIndex
        {
            get;
            private set;
        }


        public bool IsInitialized
        {
            get;
            private set;
        }


        public Mesh Mesh =>
            runtimeMesh;


        public MeshFilter MeshFilter =>
            meshFilter;


        public MeshRenderer MeshRenderer =>
            meshRenderer;


        public MeshCollider MeshCollider =>
            meshCollider;


        public IReadOnlyList<SpatialMeshUnitRange>
            UnitRanges =>
                unitRanges;


        public int UnitCount =>
            unitRanges.Count;


        public int TriangleCount =>
            triangleToUnitId?.Length ?? 0;


        public bool ColliderEnabled =>
            meshCollider != null &&
            meshCollider.enabled;


        private void Awake()
        {
            CacheComponents();
        }


        /// <summary>
        /// Initializes this chunk with a completed runtime mesh.
        ///
        /// triangleUnitIds must contain exactly one entry for
        /// every triangle in the Unity mesh.
        ///
        /// unitMeshRanges must describe non-overlapping vertex
        /// and triangle regions for every unit represented by
        /// this chunk.
        /// </summary>
        public void Initialize(
            string spatialLayerId,
            int chunkIndex,
            Mesh mesh,
            IReadOnlyList<SpatialMeshUnitRange> unitMeshRanges,
            IReadOnlyList<string> triangleUnitIds,
            Material material = null,
            bool enableCollider = false,
            bool takeMeshOwnership = true
        )
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException(
                    $"SpatialMeshChunk '{name}' has already been initialized."
                );
            }

            if (string.IsNullOrWhiteSpace(
                    spatialLayerId
                ))
            {
                throw new ArgumentException(
                    "Spatial layer ID cannot be null or empty.",
                    nameof(spatialLayerId)
                );
            }

            if (chunkIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkIndex)
                );
            }

            if (mesh == null)
            {
                throw new ArgumentNullException(
                    nameof(mesh)
                );
            }

            if (unitMeshRanges == null)
            {
                throw new ArgumentNullException(
                    nameof(unitMeshRanges)
                );
            }

            if (triangleUnitIds == null)
            {
                throw new ArgumentNullException(
                    nameof(triangleUnitIds)
                );
            }


            CacheComponents();

            ValidateMesh(
                mesh
            );

            ValidateMappings(
                mesh,
                unitMeshRanges,
                triangleUnitIds
            );


            SpatialLayerId =
                spatialLayerId.Trim();

            ChunkIndex =
                chunkIndex;

            runtimeMesh =
                mesh;

            ownsMesh =
                takeMeshOwnership;


            meshFilter.sharedMesh =
                runtimeMesh;


            if (material != null)
            {
                meshRenderer.sharedMaterial =
                    material;
            }


            triangleToUnitId =
                new string[
                    triangleUnitIds.Count
                ];

            for (
                int i = 0;
                i < triangleUnitIds.Count;
                i++
            )
            {
                triangleToUnitId[i] =
                    triangleUnitIds[i];
            }


            var ranges =
                new List<SpatialMeshUnitRange>(
                    unitMeshRanges.Count
                );

            rangesByUnitId.Clear();


            foreach (
                SpatialMeshUnitRange range
                in unitMeshRanges
            )
            {
                ranges.Add(
                    range
                );

                rangesByUnitId.Add(
                    range.UnitId,
                    range
                );
            }


            unitRanges =
                new ReadOnlyCollection<SpatialMeshUnitRange>(
                    ranges
                );


            SetColliderEnabled(
                enableCollider
            );


            IsInitialized = true;
        }


        // =========================================================
        // SEMANTIC LOOKUP
        // =========================================================

        /// <summary>
        /// Resolves a Unity mesh triangle back to its semantic
        /// SpatialUnit ID.
        ///
        /// This can later be used directly with:
        /// RaycastHit.triangleIndex
        /// </summary>
        public bool TryGetUnitIdForTriangle(
            int triangleIndex,
            out string unitId
        )
        {
            unitId = null;

            if (!IsInitialized ||
                triangleToUnitId == null)
            {
                return false;
            }

            if (triangleIndex < 0 ||
                triangleIndex >=
                triangleToUnitId.Length)
            {
                return false;
            }

            unitId =
                triangleToUnitId[
                    triangleIndex
                ];

            return !string.IsNullOrWhiteSpace(
                unitId
            );
        }


        public string GetUnitIdForTriangle(
            int triangleIndex
        )
        {
            if (!TryGetUnitIdForTriangle(
                    triangleIndex,
                    out string unitId
                ))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triangleIndex),
                    triangleIndex,
                    $"Triangle {triangleIndex} does not exist " +
                    $"in spatial mesh chunk '{name}'."
                );
            }

            return unitId;
        }


        public bool TryGetUnitRange(
            string unitId,
            out SpatialMeshUnitRange range
        )
        {
            range =
                default;

            if (!IsInitialized ||
                string.IsNullOrWhiteSpace(
                    unitId
                ))
            {
                return false;
            }

            return rangesByUnitId.TryGetValue(
                unitId.Trim(),
                out range
            );
        }


        public SpatialMeshUnitRange GetUnitRange(
            string unitId
        )
        {
            if (!TryGetUnitRange(
                    unitId,
                    out SpatialMeshUnitRange range
                ))
            {
                throw new KeyNotFoundException(
                    $"Spatial unit '{unitId}' is not contained " +
                    $"in chunk {ChunkIndex} of layer " +
                    $"'{SpatialLayerId}'."
                );
            }

            return range;
        }


        public bool ContainsUnit(
            string unitId
        )
        {
            return
                !string.IsNullOrWhiteSpace(unitId) &&
                rangesByUnitId.ContainsKey(
                    unitId.Trim()
                );
        }


        // =========================================================
        // RENDERING
        // =========================================================

        public void SetVisible(
            bool visible
        )
        {
            CacheComponents();

            meshRenderer.enabled =
                visible;
        }


        public void SetMaterial(
            Material material
        )
        {
            if (material == null)
            {
                throw new ArgumentNullException(
                    nameof(material)
                );
            }

            CacheComponents();

            meshRenderer.sharedMaterial =
                material;
        }


        // =========================================================
        // COLLIDER
        // =========================================================

        /// <summary>
        /// Enables or disables a chunk-level MeshCollider.
        ///
        /// The collider is optional because visual chunks do not
        /// necessarily need physics interaction at all times.
        /// </summary>
        public void SetColliderEnabled(
            bool enabled
        )
        {
            CacheComponents();


            if (!enabled)
            {
                if (meshCollider != null)
                {
                    meshCollider.enabled =
                        false;
                }

                return;
            }


            if (runtimeMesh == null)
            {
                throw new InvalidOperationException(
                    "Cannot enable the collider before a mesh " +
                    "has been assigned to the chunk."
                );
            }


            if (meshCollider == null)
            {
                meshCollider =
                    GetComponent<MeshCollider>();

                if (meshCollider == null)
                {
                    meshCollider =
                        gameObject.AddComponent<MeshCollider>();
                }
            }


            meshCollider.convex =
                false;

            meshCollider.sharedMesh =
                null;

            meshCollider.sharedMesh =
                runtimeMesh;

            meshCollider.enabled =
                true;
        }


        // =========================================================
        // VALIDATION
        // =========================================================

        private static void ValidateMesh(
            Mesh mesh
        )
        {
            if (mesh.vertexCount == 0)
            {
                throw new ArgumentException(
                    "Spatial chunk mesh contains no vertices.",
                    nameof(mesh)
                );
            }


            if (mesh.subMeshCount != 1)
            {
                throw new ArgumentException(
                    "Spatial mesh chunks currently require exactly " +
                    "one submesh. Unit appearance will be controlled " +
                    "through vertex data rather than separate materials.",
                    nameof(mesh)
                );
            }


            if (mesh.GetTopology(0) !=
                MeshTopology.Triangles)
            {
                throw new ArgumentException(
                    "Spatial mesh chunk must use triangle topology.",
                    nameof(mesh)
                );
            }


            if (mesh.GetIndexCount(0) == 0 ||
                mesh.GetIndexCount(0) % 3 != 0)
            {
                throw new ArgumentException(
                    "Spatial mesh chunk contains an invalid " +
                    "triangle index buffer.",
                    nameof(mesh)
                );
            }
        }


        private static void ValidateMappings(
            Mesh mesh,
            IReadOnlyList<SpatialMeshUnitRange> ranges,
            IReadOnlyList<string> triangleUnitIds
        )
        {
            if (ranges.Count == 0)
            {
                throw new ArgumentException(
                    "Spatial mesh chunk must contain at least one unit.",
                    nameof(ranges)
                );
            }


            int triangleCount =
                checked(
                    (int)mesh.GetIndexCount(0) / 3
                );


            if (triangleUnitIds.Count !=
                triangleCount)
            {
                throw new ArgumentException(
                    $"Triangle-to-unit mapping contains " +
                    $"{triangleUnitIds.Count} entries, but the mesh " +
                    $"contains {triangleCount} triangles.",
                    nameof(triangleUnitIds)
                );
            }


            var unitIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            bool[] assignedVertices =
                new bool[
                    mesh.vertexCount
                ];

            bool[] assignedTriangles =
                new bool[
                    triangleCount
                ];


            foreach (
                SpatialMeshUnitRange range
                in ranges
            )
            {
                if (!unitIds.Add(
                        range.UnitId
                    ))
                {
                    throw new ArgumentException(
                        $"Duplicate unit mesh range for " +
                        $"'{range.UnitId}'.",
                        nameof(ranges)
                    );
                }


                if (range.VertexEndExclusive >
                    mesh.vertexCount)
                {
                    throw new ArgumentException(
                        $"Vertex range for unit '{range.UnitId}' " +
                        $"extends beyond the mesh vertex buffer.",
                        nameof(ranges)
                    );
                }


                if (range.TriangleEndExclusive >
                    triangleCount)
                {
                    throw new ArgumentException(
                        $"Triangle range for unit '{range.UnitId}' " +
                        $"extends beyond the mesh triangle buffer.",
                        nameof(ranges)
                    );
                }


                for (
                    int vertexIndex =
                        range.VertexStart;

                    vertexIndex <
                        range.VertexEndExclusive;

                    vertexIndex++
                )
                {
                    if (assignedVertices[
                            vertexIndex
                        ])
                    {
                        throw new ArgumentException(
                            $"Vertex {vertexIndex} is assigned to " +
                            $"more than one spatial unit.",
                            nameof(ranges)
                        );
                    }

                    assignedVertices[
                        vertexIndex
                    ] = true;
                }


                for (
                    int triangleIndex =
                        range.TriangleStart;

                    triangleIndex <
                        range.TriangleEndExclusive;

                    triangleIndex++
                )
                {
                    if (assignedTriangles[
                            triangleIndex
                        ])
                    {
                        throw new ArgumentException(
                            $"Triangle {triangleIndex} is assigned " +
                            $"to more than one spatial unit.",
                            nameof(ranges)
                        );
                    }


                    assignedTriangles[
                        triangleIndex
                    ] = true;


                    string mappedUnitId =
                        triangleUnitIds[
                            triangleIndex
                        ];


                    if (!string.Equals(
                            mappedUnitId,
                            range.UnitId,
                            StringComparison.Ordinal
                        ))
                    {
                        throw new ArgumentException(
                            $"Triangle {triangleIndex} belongs to " +
                            $"unit '{mappedUnitId}', but its mesh " +
                            $"range declares '{range.UnitId}'.",
                            nameof(triangleUnitIds)
                        );
                    }
                }
            }


            for (
                int triangleIndex = 0;
                triangleIndex < triangleCount;
                triangleIndex++
            )
            {
                if (!assignedTriangles[
                        triangleIndex
                    ])
                {
                    throw new ArgumentException(
                        $"Triangle {triangleIndex} is not assigned " +
                        $"to any spatial unit.",
                        nameof(ranges)
                    );
                }


                string unitId =
                    triangleUnitIds[
                        triangleIndex
                    ];


                if (string.IsNullOrWhiteSpace(
                        unitId
                    ))
                {
                    throw new ArgumentException(
                        $"Triangle {triangleIndex} has an empty " +
                        $"spatial unit ID.",
                        nameof(triangleUnitIds)
                    );
                }


                if (!unitIds.Contains(
                        unitId
                    ))
                {
                    throw new ArgumentException(
                        $"Triangle {triangleIndex} references " +
                        $"unknown unit '{unitId}'.",
                        nameof(triangleUnitIds)
                    );
                }
            }


            for (
                int vertexIndex = 0;
                vertexIndex < mesh.vertexCount;
                vertexIndex++
            )
            {
                if (!assignedVertices[
                        vertexIndex
                    ])
                {
                    throw new ArgumentException(
                        $"Vertex {vertexIndex} is not assigned " +
                        $"to any spatial unit.",
                        nameof(ranges)
                    );
                }
            }
        }


        // =========================================================
        // COMPONENT LIFECYCLE
        // =========================================================

        private void CacheComponents()
        {
            if (meshFilter == null)
            {
                meshFilter =
                    GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer =
                    GetComponent<MeshRenderer>();
            }

            if (meshCollider == null)
            {
                meshCollider =
                    GetComponent<MeshCollider>();
            }
        }


        private void OnDestroy()
        {
            if (meshCollider != null)
            {
                meshCollider.sharedMesh =
                    null;
            }

            if (meshFilter != null)
            {
                meshFilter.sharedMesh =
                    null;
            }


            if (ownsMesh &&
                runtimeMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(
                        runtimeMesh
                    );
                }
                else
                {
                    DestroyImmediate(
                        runtimeMesh
                    );
                }
            }


            runtimeMesh =
                null;

            triangleToUnitId =
                null;

            rangesByUnitId.Clear();

            unitRanges =
                Array.Empty<SpatialMeshUnitRange>();

            IsInitialized =
                false;
        }
    }
}