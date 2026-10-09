using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.UI;

using UrbanAnalytics.Interaction;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// VR picking input for InteractionManager: a ray from a tracked
    /// controller.
    ///
    /// The ray is the one the XRI Near-Far Interactor draws (its
    /// stabilized curve origin), so what the user sees is what gets
    /// picked. Trigger press selects; holding grip while pressing the
    /// trigger selects the cell (block) of a building; pressing the
    /// trigger on empty table clears the selection (same as a desktop
    /// click on empty space). While the ray points at UI the city is
    /// not hovered.
    ///
    /// Both hands can be set up. The hand whose trigger was pressed
    /// last is the active one; only it hovers and selects.
    ///
    /// The pointer draws its own line and reticle, sized in real
    /// metres. XRI's line visual works in world units, which would be
    /// wrong on the scaled tabletop rig, so the scene setup turns it
    /// off. The interactor's cast distance is kept equal to the
    /// pointer's so UI can be reached at the same range.
    /// </summary>
    public sealed class XRControllerPointer :
        MonoBehaviour,
        IInteractionPointer
    {
        [Serializable]
        public sealed class Hand
        {
            public string name =
                "Right";

            [Tooltip(
                "Near-Far Interactor of this hand. Its stabilized " +
                "ray origin is used, and it reports UI hits."
            )]
            public NearFarInteractor interactor;

            [Tooltip(
                "Fallback ray origin when no interactor is set " +
                "(ray = its forward axis)."
            )]
            public Transform rayOrigin;

            [Tooltip("Selects (XRI '... Interaction/Activate' = trigger).")]
            public InputActionProperty selectAction;

            [Tooltip(
                "Held while selecting = select the building's cell " +
                "(XRI '... Interaction/Select' = grip)."
            )]
            public InputActionProperty blockModifierAction;
        }


        // =========================================================
        // INSPECTOR
        // =========================================================

        [SerializeField]
        private Hand[] hands =
            Array.Empty<Hand>();

        [Tooltip("Index of the hand that is active at start.")]
        [SerializeField]
        [Min(0)]
        private int startHand =
            0;

        [Tooltip(
            "Ray length in real metres (converted with the rig " +
            "scale, i.e. the ray origin's world scale)."
        )]
        [SerializeField]
        [Range(0.5f, 20.0f)]
        private float maxDistanceMeters =
            4.0f;


        [Tooltip(
            "Objects on these layers block the city like a panel does " +
            "(the copies' grab boxes are on Ignore Raycast)."
        )]
        [SerializeField]
        private LayerMask blockingLayers =
            1 << 2;


        [Header("Visual (real metres)")]
        [SerializeField]
        private bool drawRay =
            true;

        [SerializeField]
        [Range(0.0005f, 0.01f)]
        private float lineWidthMeters =
            0.002f;

        [SerializeField]
        [Range(0.002f, 0.05f)]
        private float reticleDiameterMeters =
            0.008f;

        [Tooltip("Length of the ray of a hand that is not active.")]
        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float idleRayLengthMeters =
            0.06f;

        [SerializeField]
        private Color rayColor =
            new Color(0.95f, 0.95f, 0.95f, 1.0f);

        [SerializeField]
        private Color idleRayColor =
            new Color(0.45f, 0.45f, 0.45f, 1.0f);

        [Tooltip(
            "Line material; must use vertex colours " +
            "(UrbanAnalytics/VertexColorUnlit). Created when empty."
        )]
        [SerializeField]
        private Material rayMaterial;

        [Tooltip("Reticle material. Created (URP Unlit) when empty.")]
        [SerializeField]
        private Material reticleMaterial;


        // =========================================================
        // RUNTIME
        // =========================================================

        private int activeHand;

        private LineRenderer[] lines =
            Array.Empty<LineRenderer>();

        private Transform reticle;


        /// <summary>Name of the hand that hovers and selects.</summary>
        public string ActiveHandName =>
            IsValidHand(activeHand)
                ? hands[activeHand].name
                : null;


        /// <summary>
        /// True while the active hand's ray is on UI (then the
        /// thumbstick scrolls the panel instead of driving shortcuts).
        /// </summary>
        public bool ActiveHandOnUi =>
            IsValidHand(activeHand) &&
            IsPointingAtUi(hands[activeHand]);


        /// <summary>
        /// The ray of the first tracked hand whose name starts with
        /// <paramref name="side"/> ("Right" / "Left": the controller or
        /// the tracked hand of that side, whichever is active).
        /// </summary>
        public bool TryGetSideRay(
            string side,
            out Ray ray,
            out float maxDistance
        )
        {
            foreach (Hand hand in hands)
            {
                if (hand != null &&
                    hand.name.StartsWith(side, StringComparison.Ordinal) &&
                    TryGetRay(hand, out ray, out float worldScale))
                {
                    maxDistance =
                        maxDistanceMeters * worldScale;

                    return true;
                }
            }

            ray =
                default;

            maxDistance =
                0.0f;

            return false;
        }


        /// <summary>True while the side's ray is on a blocking object (a copy).</summary>
        public bool IsSideRayOnBlocker(
            string side
        )
        {
            return TryGetSideRay(side, out Ray ray, out float maxDistance) &&
                   IsOnBlocker(ray, maxDistance);
        }


        private bool IsOnBlocker(
            Ray ray,
            float maxDistance
        )
        {
            return blockingLayers.value != 0 &&
                   Physics.Raycast(
                       ray,
                       maxDistance,
                       blockingLayers,
                       QueryTriggerInteraction.Collide
                   );
        }


        /// <summary>True while any hand's ray is on UI.</summary>
        public bool AnyHandOnUi
        {
            get
            {
                foreach (Hand hand in hands)
                {
                    if (hand != null &&
                        IsPointingAtUi(hand))
                    {
                        return true;
                    }
                }

                return false;
            }
        }


        /// <summary>True while any hand's ray is on UI under <paramref name="uiRoot"/>.</summary>
        public bool IsAnyHandPointingAt(
            Transform uiRoot
        )
        {
            if (uiRoot == null)
            {
                return false;
            }


            foreach (Hand hand in hands)
            {
                if (hand?.interactor is IUIInteractor uiInteractor &&
                    uiInteractor.TryGetUIModel(
                        out TrackedDeviceModel model
                    ) &&
                    model.currentRaycast.isValid &&
                    model.currentRaycast.gameObject != null &&
                    model.currentRaycast.gameObject.transform.IsChildOf(
                        uiRoot
                    ))
                {
                    return true;
                }
            }


            return false;
        }


        // =========================================================
        // UNITY
        // =========================================================

        private void OnEnable()
        {
            activeHand =
                Mathf.Clamp(
                    startHand,
                    0,
                    Mathf.Max(0, hands.Length - 1)
                );


            foreach (Hand hand in hands)
            {
                hand?.selectAction.action?.Enable();

                hand?.blockModifierAction.action?.Enable();
            }


            if (drawRay)
            {
                CreateVisuals();
            }
        }


        private void OnDisable()
        {
            foreach (LineRenderer line in lines)
            {
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }


            lines =
                Array.Empty<LineRenderer>();


            if (reticle != null)
            {
                Destroy(reticle.gameObject);

                reticle =
                    null;
            }
        }


        private void LateUpdate()
        {
            for (int i = 0; i < hands.Length; i++)
            {
                Hand hand =
                    hands[i];


                if (hand == null ||
                    !TryGetRay(
                        hand,
                        out Ray ray,
                        out float worldScale
                    ))
                {
                    if (i < lines.Length)
                    {
                        lines[i].enabled =
                            false;
                    }


                    if (i == activeHand &&
                        reticle != null)
                    {
                        reticle.gameObject.SetActive(false);
                    }

                    continue;
                }


                float maxDistance =
                    maxDistanceMeters * worldScale;


                // Keep XRI's UI ray as long as ours.
                if (hand.interactor != null &&
                    hand.interactor.farInteractionCaster is
                        CurveInteractionCaster caster)
                {
                    caster.castDistance =
                        maxDistance;
                }


                if (i < lines.Length)
                {
                    DrawHand(
                        i,
                        hand,
                        ray,
                        maxDistance,
                        worldScale
                    );
                }
            }
        }


        // =========================================================
        // IInteractionPointer
        // =========================================================

        public bool TryGetFrame(
            out PointerFrame frame
        )
        {
            frame =
                default;


            if (hands.Length == 0)
            {
                return false;
            }


            // The hand whose trigger went down this frame becomes
            // the active one (and that press is its click).
            bool clicked =
                false;


            for (int i = 0; i < hands.Length; i++)
            {
                InputAction select =
                    hands[i]?.selectAction.action;


                // Only hands that are tracked now (controller or hand
                // tracking, switched by XRInputModalityManager) count.
                if (select != null &&
                    select.WasPressedThisFrame() &&
                    TryGetRay(hands[i], out _, out _))
                {
                    activeHand =
                        i;

                    clicked =
                        true;

                    break;
                }
            }


            if (!IsValidHand(activeHand))
            {
                return false;
            }


            // Controllers put down / hands appeared: fall back to the
            // first entry that is tracked now.
            if (!TryGetRay(hands[activeHand], out _, out _))
            {
                for (int i = 0; i < hands.Length; i++)
                {
                    if (hands[i] != null &&
                        TryGetRay(hands[i], out _, out _))
                    {
                        activeHand =
                            i;

                        break;
                    }
                }
            }


            Hand hand =
                hands[activeHand];


            if (!TryGetRay(
                    hand,
                    out Ray ray,
                    out float worldScale
                ))
            {
                return true;
            }


            InputAction modifier =
                hand.blockModifierAction.action;


            frame =
                new PointerFrame
                {
                    HasRay = true,
                    Ray = ray,
                    MaxDistance = maxDistanceMeters * worldScale,
                    Blocked =
                        IsPointingAtUi(hand) ||
                        IsOnBlocker(ray, maxDistanceMeters * worldScale),
                    Clicked = clicked,
                    SelectBlock =
                        modifier != null &&
                        modifier.IsPressed()
                };


            return true;
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private bool IsValidHand(
            int index
        )
        {
            return index >= 0 &&
                   index < hands.Length &&
                   hands[index] != null;
        }


        private void CreateVisuals()
        {
            if (rayMaterial == null)
            {
                Shader shader =
                    Shader.Find(
                        "UrbanAnalytics/VertexColorUnlit"
                    );


                if (shader != null)
                {
                    rayMaterial =
                        new Material(
                            shader
                        );
                }
            }


            if (reticleMaterial == null)
            {
                Shader shader =
                    Shader.Find(
                        "Universal Render Pipeline/Unlit"
                    );


                if (shader != null)
                {
                    reticleMaterial =
                        new Material(
                            shader
                        );
                }
            }


            lines =
                new LineRenderer[hands.Length];


            for (int i = 0; i < hands.Length; i++)
            {
                GameObject lineObject =
                    new GameObject(
                        $"Ray ({hands[i]?.name})"
                    );

                lineObject.transform.SetParent(
                    transform,
                    false
                );


                LineRenderer line =
                    lineObject.AddComponent<LineRenderer>();

                line.useWorldSpace =
                    true;

                line.positionCount =
                    2;

                line.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;

                line.receiveShadows =
                    false;

                line.sharedMaterial =
                    rayMaterial;

                line.enabled =
                    false;


                lines[i] =
                    line;
            }


            GameObject reticleObject =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere
                );

            reticleObject.name =
                "Pointer Reticle";

            // Visual only: it must never catch picking rays.
            Destroy(
                reticleObject.GetComponent<Collider>()
            );

            reticleObject.transform.SetParent(
                transform,
                false
            );

            reticleObject.GetComponent<Renderer>().sharedMaterial =
                reticleMaterial;

            reticleObject.SetActive(false);


            reticle =
                reticleObject.transform;
        }


        private void DrawHand(
            int index,
            Hand hand,
            Ray ray,
            float maxDistance,
            float worldScale
        )
        {
            LineRenderer line =
                lines[index];


            bool isActive =
                index == activeHand;


            float length;

            bool hasHit;


            if (!isActive)
            {
                length =
                    idleRayLengthMeters * worldScale;

                hasHit =
                    false;
            }
            else if (TryGetUiHitDistance(
                         hand,
                         ray,
                         out float uiDistance
                     ))
            {
                length =
                    uiDistance;

                hasHit =
                    true;
            }
            else if (Physics.Raycast(
                         ray,
                         out RaycastHit hit,
                         maxDistance,
                         Physics.DefaultRaycastLayers,
                         QueryTriggerInteraction.Ignore
                     ))
            {
                length =
                    hit.distance;

                hasHit =
                    true;
            }
            else
            {
                length =
                    maxDistance;

                hasHit =
                    false;
            }


            Vector3 end =
                ray.GetPoint(
                    length
                );


            line.enabled =
                length > 0.0f;

            line.SetPosition(
                0,
                ray.origin
            );

            line.SetPosition(
                1,
                end
            );

            line.widthMultiplier =
                lineWidthMeters * worldScale;


            Color color =
                isActive
                    ? rayColor
                    : idleRayColor;

            line.startColor =
                color;

            // The line shader is opaque: alpha is not used, the
            // idle colour is simply darker.
            line.endColor =
                color;


            if (isActive &&
                reticle != null)
            {
                reticle.gameObject.SetActive(
                    hasHit
                );

                reticle.position =
                    end;

                reticle.localScale =
                    Vector3.one *
                    (reticleDiameterMeters * worldScale /
                     Mathf.Max(1e-6f, transform.lossyScale.x));
            }
        }


        private static bool TryGetUiHitDistance(
            Hand hand,
            Ray ray,
            out float distance
        )
        {
            distance =
                0.0f;


            if (!(hand.interactor is IUIInteractor uiInteractor) ||
                !uiInteractor.TryGetUIModel(
                    out TrackedDeviceModel model
                ) ||
                !model.currentRaycast.isValid)
            {
                return false;
            }


            distance =
                Vector3.Dot(
                    model.currentRaycast.worldPosition - ray.origin,
                    ray.direction
                );


            return distance > 0.0f;
        }


        private static bool TryGetRay(
            Hand hand,
            out Ray ray,
            out float worldScale
        )
        {
            ray =
                default;

            worldScale =
                1.0f;


            Transform origin =
                hand.interactor != null &&
                hand.interactor.isActiveAndEnabled
                    ? hand.interactor.curveOrigin
                    : hand.rayOrigin;


            if (origin == null ||
                !origin.gameObject.activeInHierarchy)
            {
                return false;
            }


            ray =
                new Ray(
                    origin.position,
                    origin.forward
                );


            // The rig is scaled uniformly (TabletopRig), so any axis
            // of the origin's world scale is world units per metre.
            worldScale =
                Mathf.Max(
                    1e-4f,
                    origin.lossyScale.x
                );


            return true;
        }


        private static bool IsPointingAtUi(
            Hand hand
        )
        {
            return hand.interactor is IUIInteractor uiInteractor &&
                   uiInteractor.TryGetUIModel(
                       out TrackedDeviceModel model
                   ) &&
                   model.currentRaycast.isValid;
        }
    }
}
