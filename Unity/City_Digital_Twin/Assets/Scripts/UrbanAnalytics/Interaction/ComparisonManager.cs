using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// Copies of blocks for side-by-side comparison.
    ///
    /// "Copy" takes the selected cell (or a building's cell) and
    /// builds a detached 3D copy of everything currently drawn for
    /// it: the cell surface or height column, glyphs on it and the
    /// buildings associated with it, with their current colours.
    /// Each of the two slots (A, B) shows its copy through its own
    /// camera into a RenderTexture for the UI, plus the block's
    /// full data (EntityInfo).
    ///
    /// Fair comparison rules:
    /// - both copies use the same world scale (1 unit = 1 km),
    ///   the same base plane and the same camera pose and
    ///   distance, so heights and sizes compare directly;
    /// - rotating or zooming one view moves both (linked views);
    /// - copies are rebuilt when the visualization changes, so
    ///   both always show the active encoding.
    ///
    /// Copies live on a dedicated layer far below the city; the
    /// main camera's culling mask excludes that layer.
    /// </summary>
    public sealed class ComparisonManager :
        MonoBehaviour
    {
        public const int SlotCount =
            2;


        public sealed class Slot
        {
            public int Index
            {
                get;
                internal set;
            }

            public string Label
            {
                get;
                internal set;
            }

            public Color Color
            {
                get;
                internal set;
            }

            public EntityReference Entity
            {
                get;
                internal set;
            }

            public EntityInfo Info
            {
                get;
                internal set;
            }

            public RenderTexture Texture
            {
                get;
                internal set;
            }

            public bool IsFilled =>
                Entity.IsValid;

            /// <summary>
            /// Triangles in the copy (0 when nothing is drawn for
            /// the block in the active visualization).
            /// </summary>
            public int TriangleCount
            {
                get;
                internal set;
            }

            internal Camera Camera;

            internal Transform Stage;

            internal GameObject Replica;

            internal readonly List<Mesh> Meshes =
                new List<Mesh>();

            internal Bounds LocalBounds;
        }


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Dependencies")]
        [SerializeField]
        private InteractionManager interactionManager;


        [Header("Stage")]
        [Tooltip(
            "Layer that only the comparison cameras render. Add " +
            "it under Project Settings > Tags and Layers."
        )]
        [SerializeField]
        private string stageLayerName =
            "ComparisonStage";

        [Tooltip(
            "World position of the stages, far from the city " +
            "(1 unit = 1 km). Keep within a few hundred units for " +
            "float precision."
        )]
        [SerializeField]
        private Vector3 stageOrigin =
            new Vector3(0.0f, -100.0f, 0.0f);

        [SerializeField]
        [Min(1.0f)]
        private float slotSpacing =
            20.0f;


        [Header("Views")]
        [SerializeField]
        [Min(64)]
        private int textureWidth =
            640;

        [SerializeField]
        [Min(64)]
        private int textureHeight =
            480;

        [SerializeField]
        private Color backgroundColor =
            new Color(0.12f, 0.13f, 0.15f, 1.0f);

        [SerializeField]
        [Range(10.0f, 90.0f)]
        private float fieldOfView =
            35.0f;

        [SerializeField]
        private float defaultYaw =
            -30.0f;

        [SerializeField]
        [Range(5.0f, 89.0f)]
        private float defaultPitch =
            35.0f;

        [Tooltip(
            "Colour of the cell's ground footprint drawn under " +
            "each copy as a height reference."
        )]
        [SerializeField]
        private Color32 footprintColor =
            new Color32(70, 74, 82, 255);

        [Tooltip(
            "Vertex-colour material for the footprint (e.g. " +
            "SpatialVertexColor). The base layer's own material " +
            "may ignore vertex colours."
        )]
        [SerializeField]
        private Material footprintMaterial;

        [Tooltip(
            "How far below the base plane the footprint sits, " +
            "so it never z-fights a flat coloured cell (Unity " +
            "units; 0.0005 = 0.5 m)."
        )]
        [SerializeField]
        private float footprintDepth =
            0.0005f;


        [Header("Slots")]
        [SerializeField]
        private Color slotAColor =
            new Color(1.0f, 0.5f, 0.05f, 0.55f);

        [SerializeField]
        private Color slotBColor =
            new Color(0.09f, 0.75f, 0.81f, 0.55f);


        // =========================================================
        // RUNTIME
        // =========================================================

        private readonly Slot[] slots =
            new Slot[SlotCount];


        private int stageLayer =
            -1;

        private int nextSlot;

        private bool refreshPending;


        public IReadOnlyList<Slot> Slots =>
            slots;


        public float Yaw
        {
            get;
            private set;
        }


        public float Pitch
        {
            get;
            private set;
        }


        /// <summary>
        /// 1 = both copies fit; larger = closer.
        /// </summary>
        public float Zoom
        {
            get;
            private set;
        } = 1.0f;


        public bool HasAnyFilled =>
            slots[0].IsFilled ||
            slots[1].IsFilled;


        public InteractionManager Interaction =>
            interactionManager;


        /// <summary>
        /// Slot contents changed (copied, cleared, swapped or
        /// rebuilt after a visualization change).
        /// </summary>
        public event Action Changed;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (interactionManager == null)
            {
                interactionManager =
                    FindFirstObjectByType<InteractionManager>();
            }


            stageLayer =
                LayerMask.NameToLayer(
                    stageLayerName
                );


            if (stageLayer < 0)
            {
                Debug.LogWarning(
                    $"ComparisonManager: layer '{stageLayerName}' " +
                    $"does not exist. Block copies fall back to " +
                    $"the Default layer and the main camera may " +
                    $"see them below the city.",
                    this
                );
            }


            Yaw =
                defaultYaw;

            Pitch =
                defaultPitch;


            if (footprintMaterial == null)
            {
                Shader shader =
                    Shader.Find(
                        "UrbanAnalytics/VertexColorUnlit"
                    );


                if (shader != null)
                {
                    footprintMaterial =
                        new Material(
                            shader
                        )
                        {
                            name =
                                "ComparisonFootprint"
                        };
                }
            }


            CreateSlots();
        }


        private void Start()
        {
            if (stageLayer >= 0 &&
                interactionManager != null &&
                interactionManager.InteractionCamera != null)
            {
                interactionManager.InteractionCamera.cullingMask &=
                    ~(1 << stageLayer);
            }
        }


        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.SceneRefreshed +=
                    HandleSceneRefreshed;
            }
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.SceneRefreshed -=
                    HandleSceneRefreshed;
            }
        }


        private void OnDestroy()
        {
            foreach (Slot slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }


                DestroyReplica(
                    slot
                );


                if (slot.Texture != null)
                {
                    slot.Texture.Release();

                    Destroy(
                        slot.Texture
                    );
                }
            }
        }


        private void Update()
        {
            // C: copy the selection's block into the next slot.
            Keyboard keyboard =
                Keyboard.current;


            if (keyboard != null &&
                keyboard.cKey.wasPressedThisFrame &&
                interactionManager != null &&
                interactionManager.HasSelection)
            {
                CopyToNextSlot(
                    interactionManager.Selected
                );
            }
        }


        private void LateUpdate()
        {
            if (refreshPending)
            {
                refreshPending =
                    false;

                RebuildAll();
            }


            UpdateCameras();
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        /// <summary>
        /// Copies the block of an entity into a slot: a cell
        /// itself, the cell of a building, or (for a building
        /// outside every cell) the building alone.
        /// </summary>
        public void CopyToSlot(
            int index,
            EntityReference entity
        )
        {
            if (index < 0 ||
                index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index)
                );
            }


            if (!entity.IsValid)
            {
                return;
            }


            EntityReference block =
                entity.TryGetUnit(
                    out EntityReference unit
                )
                    ? unit
                    : entity;


            Slot slot =
                slots[index];


            slot.Entity =
                block;


            Rebuild(
                slot
            );


            interactionManager.SetPersistentHighlight(
                SlotKey(index),
                block,
                slot.Color
            );


            nextSlot =
                (index + 1) % SlotCount;


            Debug.Log(
                $"Comparison: copied {block} into slot " +
                $"{slot.Label} ({slot.TriangleCount} triangles).",
                this
            );


            Changed?.Invoke();
        }


        /// <summary>
        /// Copies into the first empty slot, or alternates when
        /// both are filled. Returns the slot index used.
        /// </summary>
        public int CopyToNextSlot(
            EntityReference entity
        )
        {
            int index =
                !slots[0].IsFilled
                    ? 0
                    : !slots[1].IsFilled
                        ? 1
                        : nextSlot;


            CopyToSlot(
                index,
                entity
            );


            return index;
        }


        public void ClearSlot(
            int index
        )
        {
            Slot slot =
                slots[index];


            if (!slot.IsFilled)
            {
                return;
            }


            slot.Entity =
                default;

            slot.Info =
                null;

            slot.TriangleCount =
                0;


            DestroyReplica(
                slot
            );


            interactionManager.ClearPersistentHighlight(
                SlotKey(index)
            );


            nextSlot =
                index;


            Changed?.Invoke();
        }


        public void ClearAll()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                ClearSlot(
                    i
                );
            }
        }


        public void Swap()
        {
            EntityReference a =
                slots[0].Entity;

            EntityReference b =
                slots[1].Entity;


            for (int i = 0; i < SlotCount; i++)
            {
                slots[i].Entity =
                    i == 0
                        ? b
                        : a;


                if (slots[i].IsFilled)
                {
                    Rebuild(
                        slots[i]
                    );

                    interactionManager.SetPersistentHighlight(
                        SlotKey(i),
                        slots[i].Entity,
                        slots[i].Color
                    );
                }
                else
                {
                    slots[i].Info =
                        null;

                    slots[i].TriangleCount =
                        0;

                    DestroyReplica(
                        slots[i]
                    );

                    interactionManager.ClearPersistentHighlight(
                        SlotKey(i)
                    );
                }
            }


            Changed?.Invoke();
        }


        /// <summary>
        /// Rotates both views (pixels of pointer drag).
        /// </summary>
        public void Orbit(
            Vector2 dragPixels
        )
        {
            Yaw +=
                dragPixels.x * 0.4f;

            Pitch =
                Mathf.Clamp(
                    Pitch - dragPixels.y * 0.4f,
                    5.0f,
                    89.0f
                );
        }


        public void ZoomBy(
            float factor
        )
        {
            Zoom =
                Mathf.Clamp(
                    Zoom * factor,
                    0.4f,
                    6.0f
                );
        }


        /// <summary>
        /// Resizes both view textures to the pixel size they are
        /// shown at, so the copies are rendered 1:1 (no upscaling
        /// blur). Called by the UI when the canvas scale or screen
        /// size changes.
        /// </summary>
        public void SetViewPixelSize(
            int width,
            int height
        )
        {
            width =
                Mathf.Clamp(
                    width,
                    64,
                    4096
                );

            height =
                Mathf.Clamp(
                    height,
                    64,
                    4096
                );


            if (width == textureWidth &&
                height == textureHeight)
            {
                return;
            }


            textureWidth =
                width;

            textureHeight =
                height;


            foreach (Slot slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }


                RenderTexture old =
                    slot.Texture;


                slot.Texture =
                    CreateViewTexture(
                        slot.Label
                    );

                slot.Camera.targetTexture =
                    slot.Texture;


                if (old != null)
                {
                    old.Release();

                    Destroy(
                        old
                    );
                }
            }


            Changed?.Invoke();
        }


        private RenderTexture CreateViewTexture(
            string label
        )
        {
            var texture =
                new RenderTexture(
                    textureWidth,
                    textureHeight,
                    24,
                    RenderTextureFormat.ARGB32
                )
                {
                    name =
                        $"ComparisonView_{label}",

                    antiAliasing =
                        4
                };


            texture.Create();


            return texture;
        }


        public void ResetView()
        {
            Yaw =
                defaultYaw;

            Pitch =
                defaultPitch;

            Zoom =
                1.0f;
        }


        // =========================================================
        // BUILD
        // =========================================================

        private void HandleSceneRefreshed()
        {
            // Rebuild after the highlights (same frame is fine;
            // LateUpdate runs after InteractionManager.Update).
            if (HasAnyFilled)
            {
                refreshPending =
                    true;
            }
        }


        private void RebuildAll()
        {
            foreach (Slot slot in slots)
            {
                if (slot.IsFilled)
                {
                    Rebuild(
                        slot
                    );
                }
            }


            Changed?.Invoke();
        }


        private void Rebuild(
            Slot slot
        )
        {
            DestroyReplica(
                slot
            );


            slot.Info =
                interactionManager.BuildInfo(
                    slot.Entity
                );


            EntityGeometry geometry =
                interactionManager.CollectGeometry(
                    slot.Entity,
                    true
                );


            slot.TriangleCount =
                geometry.TriangleCount;


            // Ground reference: the cell's flat footprint just
            // below the base plane, so a raised or floating mark
            // (e.g. a displaced surface) reads as a height.
            EntityGeometry footprint =
                interactionManager.CollectFootprint(
                    slot.Entity
                );


            if (geometry.IsEmpty &&
                footprint.IsEmpty)
            {
                slot.LocalBounds =
                    new Bounds(
                        Vector3.zero,
                        Vector3.one * 0.25f
                    );

                return;
            }


            Vector3 anchor =
                ResolveAnchor(
                    slot.Entity,
                    geometry.IsEmpty
                        ? footprint
                        : geometry
                );


            var replica =
                new GameObject(
                    $"Copy_{slot.Label}_{EntityReference.ShortId(slot.Entity.Id)}"
                );


            replica.transform.SetParent(
                slot.Stage,
                false
            );


            SetLayer(
                replica
            );


            slot.Replica =
                replica;


            bool hasBounds =
                false;

            var localBounds =
                new Bounds();


            void AddPart(
                EntityGeometry.Part part,
                Vector3 origin,
                string name
            )
            {
                Mesh mesh =
                    EntityGeometry.BuildMesh(
                        part,
                        origin,
                        replica.name
                    );


                slot.Meshes.Add(
                    mesh
                );


                var partObject =
                    new GameObject(
                        name
                    );


                partObject.transform.SetParent(
                    replica.transform,
                    false
                );


                SetLayer(
                    partObject
                );


                partObject
                    .AddComponent<MeshFilter>()
                    .sharedMesh =
                        mesh;


                MeshRenderer renderer =
                    partObject.AddComponent<MeshRenderer>();


                renderer.sharedMaterial =
                    part.Material;

                renderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                renderer.receiveShadows =
                    false;

                renderer.lightProbeUsage =
                    LightProbeUsage.Off;

                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.Off;


                if (!hasBounds)
                {
                    localBounds =
                        mesh.bounds;

                    hasBounds =
                        true;
                }
                else
                {
                    localBounds.Encapsulate(
                        mesh.bounds
                    );
                }
            }


            foreach (EntityGeometry.Part part in geometry.Parts)
            {
                AddPart(
                    part,
                    anchor,
                    part.Material != null
                        ? part.Material.name
                        : "part"
                );
            }


            foreach (EntityGeometry.Part part in footprint.Parts)
            {
                for (int i = 0; i < part.Colors.Count; i++)
                {
                    part.Colors[i] =
                        footprintColor;
                }


                if (footprintMaterial != null)
                {
                    part.Material =
                        footprintMaterial;
                }


                AddPart(
                    part,
                    anchor +
                    Vector3.up * footprintDepth,
                    "GroundFootprint"
                );
            }


            slot.LocalBounds =
                localBounds;
        }


        /// <summary>
        /// Copy origin: the cell centroid on the base plane, so
        /// y = 0 in the copy is the city ground in both slots.
        /// </summary>
        private Vector3 ResolveAnchor(
            EntityReference entity,
            EntityGeometry geometry
        )
        {
            if (entity.Kind == EntityKind.SpatialUnit &&
                interactionManager.Context.TryGetUnitAnchorWorld(
                    entity.SpatialLayerId,
                    entity.Id,
                    out Vector3 anchor
                ))
            {
                return anchor;
            }


            Bounds bounds =
                geometry.Bounds;


            return new Vector3(
                bounds.center.x,
                interactionManager.Context.Geometry.SurfaceYOffset,
                bounds.center.z
            );
        }


        private void DestroyReplica(
            Slot slot
        )
        {
            if (slot.Replica != null)
            {
                Destroy(
                    slot.Replica
                );

                slot.Replica =
                    null;
            }


            foreach (Mesh mesh in slot.Meshes)
            {
                if (mesh != null)
                {
                    Destroy(
                        mesh
                    );
                }
            }


            slot.Meshes.Clear();
        }


        // =========================================================
        // CAMERAS
        // =========================================================

        private void CreateSlots()
        {
            var stageRoot =
                new GameObject(
                    "__ComparisonStage"
                );


            stageRoot.transform.SetParent(
                transform,
                false
            );

            stageRoot.transform.position =
                stageOrigin;


            for (int i = 0; i < SlotCount; i++)
            {
                var slot =
                    new Slot
                    {
                        Index =
                            i,

                        Label =
                            i == 0
                                ? "A"
                                : "B",

                        Color =
                            i == 0
                                ? slotAColor
                                : slotBColor
                    };


                var stage =
                    new GameObject(
                        $"Stage_{slot.Label}"
                    );


                stage.transform.SetParent(
                    stageRoot.transform,
                    false
                );

                stage.transform.localPosition =
                    new Vector3(
                        (i - 0.5f) * slotSpacing,
                        0.0f,
                        0.0f
                    );


                slot.Stage =
                    stage.transform;


                slot.Texture =
                    CreateViewTexture(
                        slot.Label
                    );


                var cameraObject =
                    new GameObject(
                        $"Camera_{slot.Label}"
                    );


                cameraObject.transform.SetParent(
                    stage.transform,
                    false
                );


                Camera slotCamera =
                    cameraObject.AddComponent<Camera>();


                slotCamera.targetTexture =
                    slot.Texture;

                slotCamera.clearFlags =
                    CameraClearFlags.SolidColor;

                slotCamera.backgroundColor =
                    backgroundColor;

                slotCamera.fieldOfView =
                    fieldOfView;

                slotCamera.cullingMask =
                    stageLayer >= 0
                        ? 1 << stageLayer
                        : 1;

                slotCamera.enabled =
                    false;


                slot.Camera =
                    slotCamera;


                slots[i] =
                    slot;
            }
        }


        /// <summary>
        /// Both cameras get the same pose relative to their copy:
        /// a common pivot height and a distance that fits the
        /// larger copy. Equal pixels therefore mean equal metres.
        /// </summary>
        private void UpdateCameras()
        {
            bool any =
                false;

            float horizontal =
                0.05f;

            // Common vertical extent, including parts below the
            // base plane (bidirectional / downward marks).
            float bottom =
                0.0f;

            float top =
                0.0f;


            foreach (Slot slot in slots)
            {
                if (!slot.IsFilled)
                {
                    continue;
                }


                any =
                    true;


                Bounds bounds =
                    slot.LocalBounds;


                // Extent about the vertical axis through the
                // anchor.
                horizontal =
                    Mathf.Max(
                        horizontal,
                        new Vector2(
                            Mathf.Max(
                                Mathf.Abs(bounds.min.x),
                                Mathf.Abs(bounds.max.x)
                            ),
                            Mathf.Max(
                                Mathf.Abs(bounds.min.z),
                                Mathf.Abs(bounds.max.z)
                            )
                        ).magnitude
                    );


                bottom =
                    Mathf.Min(
                        bottom,
                        bounds.min.y
                    );

                top =
                    Mathf.Max(
                        top,
                        bounds.max.y
                    );
            }


            float pivotHeight =
                0.5f * (bottom + top);


            float radius =
                new Vector2(
                    horizontal,
                    0.5f * (top - bottom)
                ).magnitude;


            float distance =
                radius /
                Mathf.Sin(
                    0.5f * fieldOfView * Mathf.Deg2Rad
                ) /
                Zoom;


            Quaternion rotation =
                Quaternion.Euler(
                    Pitch,
                    Yaw,
                    0.0f
                );


            foreach (Slot slot in slots)
            {
                bool render =
                    any &&
                    slot.IsFilled;


                slot.Camera.enabled =
                    render;


                if (!render)
                {
                    continue;
                }


                Vector3 pivot =
                    slot.Stage.position +
                    Vector3.up * pivotHeight;


                slot.Camera.transform.SetPositionAndRotation(
                    pivot -
                    rotation * Vector3.forward * distance,
                    rotation
                );


                slot.Camera.nearClipPlane =
                    Mathf.Max(
                        0.0005f,
                        distance * 0.01f
                    );

                slot.Camera.farClipPlane =
                    distance * 4.0f +
                    radius * 2.0f;
            }
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private void SetLayer(
            GameObject target
        )
        {
            if (stageLayer >= 0)
            {
                target.layer =
                    stageLayer;
            }
        }


        private static string SlotKey(
            int index
        )
        {
            return $"slot{index}";
        }
    }
}
