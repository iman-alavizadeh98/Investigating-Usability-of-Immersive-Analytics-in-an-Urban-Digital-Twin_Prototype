using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

using UrbanAnalytics.Rendering;
using UrbanAnalytics.UrbanContext;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// Keeps MeshColliders on the shown chunks in step with what
    /// is drawn, so a physics ray gives RaycastHit.triangleIndex
    /// on exactly the geometry the user sees.
    ///
    /// Rules:
    /// - a chunk is pickable only while it is shown (hidden source
    ///   chunks under a height surface must not catch rays);
    /// - colliders are cooked on worker threads
    ///   (Physics.BakeMesh in a job), then assigned on the main
    ///   thread, which reuses the baked data;
    /// - building chunks are re-baked when their GeometryVersion
    ///   changes (buildings moved onto a height surface).
    ///
    /// Meshes must not change while a bake runs: call
    /// CompleteNow() before an apply or clear modifies them.
    /// </summary>
    internal sealed class PickingColliders :
        IDisposable
    {
        // Same options for the bake and the collider, otherwise
        // the collider re-cooks synchronously on assignment.
        private const MeshColliderCookingOptions CookingOptions =
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.UseFastMidphase;


        private struct BakeJob :
            IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<EntityId> MeshIds;

            public MeshColliderCookingOptions Options;


            public void Execute(
                int index
            )
            {
                Physics.BakeMesh(
                    MeshIds[index],
                    false,
                    Options
                );
            }
        }


        private struct PendingCollider
        {
            public GameObject Owner;

            public Mesh Mesh;

            public BuildingMeshChunk Building;

            public int Version;
        }


        // Building chunk → geometry version its collider was
        // baked from.
        private readonly Dictionary<BuildingMeshChunk, int>
            bakedBuildingVersions =
                new Dictionary<BuildingMeshChunk, int>();


        private readonly List<PendingCollider> pending =
            new List<PendingCollider>();


        private readonly Stopwatch stopwatch =
            new Stopwatch();


        private const int MaximumBakeFrames =
            3;


        private NativeArray<EntityId> meshIds;

        private JobHandle handle;

        private int scheduledFrame;


        public bool IsBaking
        {
            get;
            private set;
        }


        public int LastBakedMeshCount
        {
            get;
            private set;
        }


        public long LastBakedTriangleCount
        {
            get;
            private set;
        }


        public double LastBakeSeconds
        {
            get;
            private set;
        }


        /// <summary>
        /// Disables colliders on hidden chunks and starts baking
        /// every shown chunk whose collider is missing or stale.
        /// Returns the number of meshes being baked.
        /// </summary>
        public int Begin(
            IReadOnlyList<SpatialMeshChunk> spatialChunks,
            IReadOnlyList<BuildingMeshChunk> buildingChunks,
            bool includeBuildings
        )
        {
            if (IsBaking)
            {
                throw new InvalidOperationException(
                    "A collider bake is already running."
                );
            }


            pending.Clear();


            long triangles =
                0;


            foreach (SpatialMeshChunk chunk in spatialChunks)
            {
                if (chunk == null)
                {
                    continue;
                }


                MeshCollider collider =
                    chunk.GetComponent<MeshCollider>();


                if (!InteractionContext.IsShown(chunk))
                {
                    if (collider != null)
                    {
                        collider.enabled =
                            false;
                    }

                    continue;
                }


                // Spatial chunk meshes never change after
                // creation; a matching collider is current.
                if (collider != null &&
                    collider.sharedMesh == chunk.Mesh)
                {
                    collider.enabled =
                        true;

                    continue;
                }


                pending.Add(
                    new PendingCollider
                    {
                        Owner =
                            chunk.gameObject,

                        Mesh =
                            chunk.Mesh
                    }
                );


                triangles +=
                    chunk.TriangleCount;
            }


            foreach (BuildingMeshChunk chunk in buildingChunks)
            {
                if (chunk == null)
                {
                    continue;
                }


                MeshCollider collider =
                    chunk.GetComponent<MeshCollider>();


                if (!includeBuildings ||
                    !InteractionContext.IsShown(chunk))
                {
                    if (collider != null)
                    {
                        collider.enabled =
                            false;
                    }

                    continue;
                }


                if (collider != null &&
                    collider.sharedMesh == chunk.Mesh &&
                    bakedBuildingVersions.TryGetValue(
                        chunk,
                        out int version
                    ) &&
                    version == chunk.GeometryVersion)
                {
                    collider.enabled =
                        true;

                    continue;
                }


                pending.Add(
                    new PendingCollider
                    {
                        Owner =
                            chunk.gameObject,

                        Mesh =
                            chunk.Mesh,

                        Building =
                            chunk,

                        Version =
                            chunk.GeometryVersion
                    }
                );


                triangles +=
                    (long)chunk.Mesh.GetIndexCount(0) / 3;
            }


            LastBakedMeshCount =
                pending.Count;

            LastBakedTriangleCount =
                triangles;


            if (pending.Count == 0)
            {
                LastBakeSeconds =
                    0.0;

                return 0;
            }


            meshIds =
                new NativeArray<EntityId>(
                    pending.Count,
                    Allocator.Persistent
                );


            for (int i = 0; i < pending.Count; i++)
            {
                meshIds[i] =
                    pending[i].Mesh.GetEntityId();
            }


            stopwatch.Restart();


            handle =
                new BakeJob
                {
                    MeshIds =
                        meshIds,

                    Options =
                        CookingOptions
                }
                .Schedule(
                    pending.Count,
                    1
                );


            scheduledFrame =
                Time.frameCount;


            // Start the workers now rather than at the next sync
            // point.
            JobHandle.ScheduleBatchedJobs();


            IsBaking =
                true;


            return pending.Count;
        }


        /// <summary>
        /// Finishes the bake if the job is done. Returns true when
        /// no bake is running any more.
        /// </summary>
        public bool TryFinish()
        {
            if (!IsBaking)
            {
                return true;
            }


            // Physics.BakeMesh uses Unity's temporary job memory,
            // which must not live longer than four frames. Let
            // the workers run for up to MaximumBakeFrames, then
            // wait for the rest on the main thread.
            if (!handle.IsCompleted &&
                Time.frameCount - scheduledFrame <
                    MaximumBakeFrames)
            {
                return false;
            }


            Finish();


            return true;
        }


        /// <summary>
        /// Blocks until the running bake is done and assigns its
        /// colliders. Call before meshes are modified.
        /// </summary>
        public void CompleteNow()
        {
            if (IsBaking)
            {
                Finish();
            }
        }


        private void Finish()
        {
            handle.Complete();


            foreach (PendingCollider item in pending)
            {
                // The chunk may have been destroyed meanwhile
                // (an old visualization chunk).
                if (item.Owner == null ||
                    item.Mesh == null)
                {
                    continue;
                }


                MeshCollider collider =
                    item.Owner.GetComponent<MeshCollider>();


                if (collider == null)
                {
                    collider =
                        item.Owner.AddComponent<MeshCollider>();
                }


                collider.convex =
                    false;

                collider.cookingOptions =
                    CookingOptions;


                // Re-assign so a mesh whose vertices moved picks
                // up the freshly baked data.
                collider.sharedMesh =
                    null;

                collider.sharedMesh =
                    item.Mesh;

                collider.enabled =
                    true;


                if (item.Building != null)
                {
                    bakedBuildingVersions[item.Building] =
                        item.Version;
                }
            }


            stopwatch.Stop();


            LastBakeSeconds =
                stopwatch.Elapsed.TotalSeconds;


            pending.Clear();


            if (meshIds.IsCreated)
            {
                meshIds.Dispose();
            }


            IsBaking =
                false;
        }


        public void Dispose()
        {
            if (IsBaking)
            {
                handle.Complete();

                IsBaking =
                    false;
            }


            if (meshIds.IsCreated)
            {
                meshIds.Dispose();
            }


            pending.Clear();

            bakedBuildingVersions.Clear();
        }
    }
}
