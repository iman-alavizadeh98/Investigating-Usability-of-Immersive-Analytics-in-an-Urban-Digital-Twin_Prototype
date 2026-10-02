using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// Draws named, single-colour overlays on top of entities
    /// (hover, selection, comparison slots) without touching the
    /// visualization's own meshes or vertex colours.
    ///
    /// Each overlay is a copy of the entity's shown triangles,
    /// rendered with the HighlightOverlay shader (depth offset so
    /// it wins against the identical source triangles, plus a
    /// faint pass where it is hidden behind other geometry).
    /// </summary>
    public sealed class EntityHighlighter :
        IDisposable
    {
        private sealed class Overlay
        {
            public GameObject Object;

            public Mesh Mesh;

            public Material Material;

            public EntityReference Entity;

            public bool IncludeBuildings;
        }


        private static readonly int ColorProperty =
            Shader.PropertyToID(
                "_Color"
            );


        private readonly Transform root;

        private readonly Material baseMaterial;


        private readonly Dictionary<string, Overlay>
            overlays =
                new Dictionary<string, Overlay>(
                    StringComparer.Ordinal
                );


        public EntityHighlighter(
            Transform parent,
            Material baseMaterial
        )
        {
            this.baseMaterial =
                baseMaterial
                ?? throw new ArgumentNullException(
                    nameof(baseMaterial)
                );


            var rootObject =
                new GameObject(
                    "__InteractionHighlights"
                );


            rootObject.transform.SetParent(
                parent,
                false
            );


            root =
                rootObject.transform;
        }


        /// <summary>
        /// Shows (or replaces) the overlay with this key.
        /// </summary>
        public void Show(
            string key,
            EntityReference entity,
            bool includeBuildings,
            EntityGeometry geometry,
            Color color
        )
        {
            if (geometry == null ||
                geometry.IsEmpty)
            {
                Hide(
                    key
                );

                return;
            }


            if (!overlays.TryGetValue(
                    key,
                    out Overlay overlay
                ))
            {
                overlay =
                    new Overlay
                    {
                        Object =
                            new GameObject(
                                $"Highlight_{key}"
                            ),

                        Material =
                            new Material(
                                baseMaterial
                            )
                            {
                                name =
                                    $"Highlight_{key}"
                            }
                    };


                overlay.Object.transform.SetParent(
                    root,
                    false
                );


                overlay.Object.AddComponent<MeshFilter>();


                MeshRenderer renderer =
                    overlay.Object.AddComponent<MeshRenderer>();


                renderer.sharedMaterial =
                    overlay.Material;

                renderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                renderer.receiveShadows =
                    false;

                renderer.lightProbeUsage =
                    LightProbeUsage.Off;

                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.Off;


                overlays.Add(
                    key,
                    overlay
                );
            }


            if (overlay.Mesh != null)
            {
                UnityEngine.Object.Destroy(
                    overlay.Mesh
                );
            }


            // Vertices relative to the bounds centre keep float
            // precision for metre-sized details.
            Vector3 origin =
                geometry.Bounds.center;


            overlay.Mesh =
                geometry.BuildMergedMesh(
                    origin,
                    $"Highlight_{key}"
                );


            overlay.Object.transform.position =
                origin;


            overlay.Object
                .GetComponent<MeshFilter>()
                .sharedMesh =
                    overlay.Mesh;


            overlay.Material.SetColor(
                ColorProperty,
                color
            );


            overlay.Entity =
                entity;

            overlay.IncludeBuildings =
                includeBuildings;


            overlay.Object.SetActive(
                true
            );
        }


        public void Hide(
            string key
        )
        {
            if (!overlays.TryGetValue(
                    key,
                    out Overlay overlay
                ))
            {
                return;
            }


            overlays.Remove(
                key
            );


            Destroy(
                overlay
            );
        }


        public bool TryGetEntity(
            string key,
            out EntityReference entity,
            out bool includeBuildings
        )
        {
            entity =
                default;

            includeBuildings =
                false;


            if (!overlays.TryGetValue(
                    key,
                    out Overlay overlay
                ))
            {
                return false;
            }


            entity =
                overlay.Entity;

            includeBuildings =
                overlay.IncludeBuildings;


            return true;
        }


        /// <summary>
        /// Keys of the overlays currently shown.
        /// </summary>
        public List<string> GetKeys()
        {
            return new List<string>(
                overlays.Keys
            );
        }


        public void Dispose()
        {
            foreach (Overlay overlay in overlays.Values)
            {
                Destroy(
                    overlay
                );
            }


            overlays.Clear();


            if (root != null)
            {
                UnityEngine.Object.Destroy(
                    root.gameObject
                );
            }
        }


        private static void Destroy(
            Overlay overlay
        )
        {
            if (overlay.Mesh != null)
            {
                UnityEngine.Object.Destroy(
                    overlay.Mesh
                );
            }


            if (overlay.Material != null)
            {
                UnityEngine.Object.Destroy(
                    overlay.Material
                );
            }


            if (overlay.Object != null)
            {
                UnityEngine.Object.Destroy(
                    overlay.Object
                );
            }
        }
    }
}
