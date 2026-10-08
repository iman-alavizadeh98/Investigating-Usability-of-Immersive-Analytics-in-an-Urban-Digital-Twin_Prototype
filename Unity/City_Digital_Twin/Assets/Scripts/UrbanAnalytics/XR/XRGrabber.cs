using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Picks up XRGrabbable objects (pop-out copies, panel "Move"
    /// handles) with a ray: point at the grab collider and press the
    /// side's grab action (XRI "&lt;side&gt; Interaction/Select": the
    /// controller grip, or a pinch / fist with hand tracking). The object
    /// keeps its distance along the ray and its offset to the hit point
    /// while the action is held; panels also turn to face the user.
    /// Release drops it where it is.
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
        private StudySession studySession;

        [SerializeField]
        private InputActionProperty rightGrabAction;

        [SerializeField]
        private InputActionProperty leftGrabAction;


        private readonly Dictionary<string, Grab> grabs =
            new Dictionary<string, Grab>();


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
                }

                return;
            }


            if (!action.WasPressedThisFrame() ||
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

            if (grabbable == null)
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
    }
}
