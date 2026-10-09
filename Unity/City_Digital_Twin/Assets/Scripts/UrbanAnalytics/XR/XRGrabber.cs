using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Picks up XRGrabbable objects (copies, panel "Grab" bars) with a
    /// ray: point at the grab collider and press the side's grab action
    /// (XRI "&lt;side&gt; Interaction/Select": the controller grip). The
    /// object keeps its distance along the ray and its offset to the hit
    /// point while the grip is held; panels also turn to face the user
    /// and are kept out of the table (TabletopRig.KeepOutOfTable).
    /// Release drops it where it is.
    ///
    /// Locked by the tutorial until it teaches grabbing
    /// (<see cref="XRFeature.Grab"/>).
    ///
    /// Logged as the study event vr_grab (object name, final position).
    /// </summary>
    public sealed class XRGrabber :
        MonoBehaviour
    {
        private sealed class Grab
        {
            public XRGrabbable Target;

            public float Distance;

            public Vector3 Offset;
        }


        [SerializeField]
        private XRControllerPointer pointer;

        [SerializeField]
        private Camera viewCamera;

        [SerializeField]
        private TabletopRig tabletopRig;

        [SerializeField]
        private StudySession studySession;

        [SerializeField]
        private InputActionProperty rightGrabAction;

        [SerializeField]
        private InputActionProperty leftGrabAction;

        [Tooltip(
            "Panels stay at least this high above the table top while " +
            "over the table (real metres; the city model stands there)."
        )]
        [SerializeField]
        [Range(0.0f, 0.6f)]
        private float panelClearanceMeters =
            0.25f;


        private readonly Dictionary<string, Grab> grabs =
            new Dictionary<string, Grab>();


        /// <summary>Raised when something is let go (tutorial).</summary>
        public event Action<XRGrabbable> Released;


        /// <summary>True while a hand on this side holds something.</summary>
        public bool IsGrabbing(
            string side
        )
        {
            return grabs.ContainsKey(side);
        }


        private void Awake()
        {
            if (pointer == null)
            {
                pointer =
                    FindFirstObjectByType<XRControllerPointer>();
            }

            if (viewCamera == null)
            {
                viewCamera =
                    Camera.main;
            }

            if (tabletopRig == null)
            {
                tabletopRig =
                    FindFirstObjectByType<TabletopRig>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }
        }


        private void OnEnable()
        {
            rightGrabAction.action?.Enable();

            leftGrabAction.action?.Enable();
        }


        private void Update()
        {
            UpdateSide("Right", rightGrabAction.action);

            UpdateSide("Left", leftGrabAction.action);
        }


        private void UpdateSide(
            string side,
            InputAction action
        )
        {
            if (action == null ||
                pointer == null)
            {
                return;
            }


            if (grabs.TryGetValue(side, out Grab grab))
            {
                if (grab.Target == null)
                {
                    grabs.Remove(side);

                    return;
                }


                if (!action.IsPressed())
                {
                    grabs.Remove(side);

                    grab.Target.NotifyReleased();

                    Released?.Invoke(grab.Target);

                    studySession?.LogEvent(
                        "vr_grab",
                        ("object", grab.Target.name),
                        ("position", grab.Target.transform.position)
                    );

                    return;
                }


                if (pointer.TryGetSideRay(side, out Ray ray, out _))
                {
                    Transform target =
                        grab.Target.transform;

                    target.position =
                        ray.GetPoint(grab.Distance) + grab.Offset;

                    if (grab.Target.FaceUser &&
                        viewCamera != null)
                    {
                        // A canvas is read from its -Z side: forward
                        // points away from the eyes.
                        Vector3 away =
                            target.position - viewCamera.transform.position;

                        away.y =
                            0.0f;

                        if (away.sqrMagnitude > 1e-8f)
                        {
                            target.rotation =
                                Quaternion.LookRotation(
                                    away.normalized,
                                    Vector3.up
                                );
                        }
                    }

                    KeepOutOfTable(grab.Target);
                }

                return;
            }


            if (!action.WasPressedThisFrame() ||
                !XRFeatureLock.Allows(XRFeature.Grab) ||
                !pointer.TryGetSideRay(side, out Ray startRay, out float maxDistance) ||
                !Physics.Raycast(
                    startRay,
                    out RaycastHit hit,
                    maxDistance,
                    1 << XRGrabbable.Layer,
                    QueryTriggerInteraction.Collide
                ))
            {
                return;
            }


            XRGrabbable grabbable =
                hit.collider.GetComponentInParent<XRGrabbable>();

            if (grabbable == null ||
                !grabbable.isActiveAndEnabled)
            {
                return;
            }


            grabs[side] =
                new Grab
                {
                    Target = grabbable,
                    Distance = hit.distance,
                    Offset = grabbable.transform.position - hit.point
                };

            grabbable.NotifyGrabbed();
        }


        /// <summary>Pushes a held panel out of the table volume.</summary>
        private void KeepOutOfTable(
            XRGrabbable target
        )
        {
            if (!target.KeepOutOfTable ||
                tabletopRig == null ||
                !tabletopRig.IsPlaced ||
                tabletopRig.Origin == null ||
                !target.TryGetPanelExtents(out float halfWidth, out float halfHeight))
            {
                return;
            }


            // World units → real metres (the rig is scaled).
            float metersPerUnit =
                1.0f / Mathf.Max(1e-6f, tabletopRig.Origin.lossyScale.x);

            target.transform.position =
                tabletopRig.KeepOutOfTable(
                    target.transform.position,
                    halfWidth * metersPerUnit,
                    halfHeight * metersPerUnit,
                    panelClearanceMeters
                );
        }
    }
}
