using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace UrbanAnalytics.UrbanContext
{
    /// <summary>
    /// Semantic geometry range for one building inside
    /// a batched building mesh.
    /// </summary>
    public readonly struct BuildingMeshUnitRange
    {
        public string BuildingId
        {
            get;
        }


        public string AssociatedSpatialUnitId
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


        public int TriangleStart
        {
            get;
        }


        public int TriangleCount
        {
            get;
        }


        /// <summary>
        /// Source building height in metres (NaN if unknown).
        /// </summary>
        public float HeightMeters
        {
            get;
        }


        public int VertexEndExclusive =>
            VertexStart +
            VertexCount;


        public int TriangleEndExclusive =>
            TriangleStart +
            TriangleCount;


        public BuildingMeshUnitRange(
            string buildingId,
            string associatedSpatialUnitId,
            int vertexStart,
            int vertexCount,
            int triangleStart,
            int triangleCount,
            float heightMeters = float.NaN
        )
        {
            HeightMeters =
                heightMeters;

            BuildingId =
                buildingId
                ?? throw new ArgumentNullException(
                    nameof(buildingId)
                );

            AssociatedSpatialUnitId =
                associatedSpatialUnitId;

            VertexStart =
                vertexStart;

            VertexCount =
                vertexCount;

            TriangleStart =
                triangleStart;

            TriangleCount =
                triangleCount;
        }
    }


    /// <summary>
    /// One batched building mesh.
    ///
    /// Hundreds of buildings can live inside one Unity
    /// GameObject while still retaining semantic identity.
    /// </summary>
    public sealed class BuildingMeshChunk :
        MonoBehaviour
    {
        private IReadOnlyList<BuildingMeshUnitRange>
            unitRanges =
                Array.Empty<BuildingMeshUnitRange>();


        private bool ownsMesh;

        private MeshCollider meshCollider;


        public int ChunkIndex
        {
            get;
            private set;
        }


        public Mesh Mesh
        {
            get;
            private set;
        }


        public MeshFilter MeshFilter
        {
            get;
            private set;
        }


        public MeshRenderer MeshRenderer
        {
            get;
            private set;
        }


        public IReadOnlyList<BuildingMeshUnitRange>
            UnitRanges =>
                unitRanges;


        public int BuildingCount =>
            unitRanges.Count;


        public bool IsInitialized =>
            Mesh != null &&
            MeshFilter != null &&
            MeshRenderer != null;


        /// <summary>
        /// Incremented whenever the mesh vertices are moved
        /// (e.g. buildings following a height surface), so
        /// derived data such as picking colliders can tell they
        /// are stale. Colour changes do not count.
        /// </summary>
        public int GeometryVersion
        {
            get;
            private set;
        }


        public void NotifyGeometryChanged()
        {
            GeometryVersion++;
        }


        public void Initialize(
            int chunkIndex,
            Mesh mesh,
            IList<BuildingMeshUnitRange> ranges,
            Material material,
            bool enableCollider,
            bool ownsMesh
        )
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(
                    nameof(mesh)
                );
            }


            if (ranges == null)
            {
                throw new ArgumentNullException(
                    nameof(ranges)
                );
            }


            ChunkIndex =
                chunkIndex;

            Mesh =
                mesh;

            this.ownsMesh =
                ownsMesh;


            unitRanges =
                new ReadOnlyCollection<BuildingMeshUnitRange>(
                    new List<BuildingMeshUnitRange>(
                        ranges
                    )
                );


            MeshFilter =
                GetComponent<MeshFilter>();


            if (MeshFilter == null)
            {
                MeshFilter =
                    gameObject.AddComponent<MeshFilter>();
            }


            MeshRenderer =
                GetComponent<MeshRenderer>();


            if (MeshRenderer == null)
            {
                MeshRenderer =
                    gameObject.AddComponent<MeshRenderer>();
            }


            MeshFilter.sharedMesh =
                Mesh;

            MeshRenderer.sharedMaterial =
                material;


            SetColliderEnabled(
                enableCollider
            );
        }


        public void SetMaterial(
            Material material
        )
        {
            if (MeshRenderer != null)
            {
                MeshRenderer.sharedMaterial =
                    material;
            }
        }


        public void SetVisible(
            bool visible
        )
        {
            if (MeshRenderer != null)
            {
                MeshRenderer.enabled =
                    visible;
            }
        }


        public void SetColliderEnabled(
            bool enabled
        )
        {
            if (!enabled)
            {
                if (meshCollider != null)
                {
                    meshCollider.enabled =
                        false;
                }

                return;
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


            meshCollider.sharedMesh =
                Mesh;

            meshCollider.enabled =
                true;
        }


        /// <summary>
        /// This is preparation for future mouse/XR ray selection.
        ///
        /// A raycast gives triangleIndex. We can then recover
        /// the semantic BuildingId without a GameObject per building.
        /// </summary>
        public bool TryGetBuildingForTriangle(
            int triangleIndex,
            out BuildingMeshUnitRange range
        )
        {
            range =
                default;


            if (triangleIndex < 0 ||
                unitRanges.Count == 0)
            {
                return false;
            }


            int low =
                0;

            int high =
                unitRanges.Count - 1;


            while (low <= high)
            {
                int middle =
                    low +
                    (
                        (high - low)
                        / 2
                    );


                BuildingMeshUnitRange candidate =
                    unitRanges[middle];


                if (triangleIndex <
                    candidate.TriangleStart)
                {
                    high =
                        middle - 1;
                }
                else if (
                    triangleIndex >=
                    candidate.TriangleEndExclusive
                )
                {
                    low =
                        middle + 1;
                }
                else
                {
                    range =
                        candidate;

                    return true;
                }
            }


            return false;
        }


        private void OnDestroy()
        {
            if (ownsMesh &&
                Mesh != null)
            {
                Destroy(
                    Mesh
                );
            }


            Mesh =
                null;
        }
    }
}