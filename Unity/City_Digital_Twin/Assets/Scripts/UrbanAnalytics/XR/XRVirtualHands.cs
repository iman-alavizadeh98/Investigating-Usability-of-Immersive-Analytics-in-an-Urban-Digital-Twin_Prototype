using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Stylised "glove" hands holding the controllers, so users see their
    /// hands in VR. Built from capsules (no model asset): palm, wrist
    /// cuff, thumb and four fingers with three segments each. The index
    /// finger curls with the trigger, the other fingers with the grip.
    ///
    /// Geometry is in the controller's grip-pose space (OpenXR grip pose
    /// as Unity sees it: origin in the handle, +Z along the handle from
    /// little finger to thumb, +X to the right, −Y = where the fingers
    /// point), built for the right hand and mirrored (scale x = −1) for
    /// the left. Sizes are real metres; the controllers sit under the
    /// scaled XR Origin, so the rig scale is inherited.
    ///
    /// The XRI starter controller models are hidden while the hands are
    /// shown (their FBX is also excluded by the repo's *.fbx ignore rule,
    /// so on a fresh clone they render nothing).
    /// </summary>
    public sealed class XRVirtualHands :
        MonoBehaviour
    {
        private sealed class Finger
        {
            public Transform[] Joints;

            public float[] OpenDegrees;

            public float[] ClosedDegrees;
        }


        private sealed class HandRig
        {
            public Transform Root;

            public Finger Index;

            public readonly List<Finger> GripFingers =
                new List<Finger>();

            public InputAction Trigger;

            public InputAction Grip;

            public float TriggerValue;

            public float GripValue;
        }


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Controllers")]
        [SerializeField]
        private Transform leftController;

        [SerializeField]
        private Transform rightController;

        [Tooltip("Controller models hidden while the hands are shown.")]
        [SerializeField]
        private GameObject[] controllerVisuals =
            new GameObject[0];


        [Header("Look")]
        [Tooltip("Hand material (URP Lit). A light glove colour is created when empty.")]
        [SerializeField]
        private Material handMaterial;

        [SerializeField]
        private Color handColor =
            new Color(0.80f, 0.82f, 0.86f);

        [Tooltip("Hand size factor (1 = an average adult hand).")]
        [SerializeField]
        [Range(0.7f, 1.3f)]
        private float handScale =
            1.0f;

        [Tooltip("Hand offset in grip space (metres), to fit the real hand.")]
        [SerializeField]
        private Vector3 offsetMeters =
            Vector3.zero;

        [SerializeField]
        private Vector3 offsetEuler =
            Vector3.zero;


        [Header("Input (Input System paths; {0} = LeftHand / RightHand)")]
        [SerializeField]
        private string triggerBinding =
            "<XRController>{{{0}}}/trigger";

        [SerializeField]
        private string gripBinding =
            "<XRController>{{{0}}}/grip";

        [Tooltip("How fast fingers follow the trigger/grip (1/s).")]
        [SerializeField]
        [Range(1.0f, 60.0f)]
        private float fingerSharpness =
            25.0f;


        // =========================================================
        // RUNTIME
        // =========================================================

        private readonly List<HandRig> hands =
            new List<HandRig>();


        // =========================================================
        // UNITY
        // =========================================================

        private void OnEnable()
        {
            if (handMaterial == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Lit");

                if (shader != null)
                {
                    handMaterial =
                        new Material(shader)
                        {
                            color = handColor
                        };
                }
            }


            if (rightController != null)
            {
                hands.Add(
                    BuildHand(rightController, "RightHand", false)
                );
            }

            if (leftController != null)
            {
                hands.Add(
                    BuildHand(leftController, "LeftHand", true)
                );
            }


            foreach (GameObject visual in controllerVisuals)
            {
                if (visual != null)
                {
                    visual.SetActive(false);
                }
            }
        }


        private void OnDisable()
        {
            foreach (HandRig hand in hands)
            {
                hand.Trigger?.Dispose();

                hand.Grip?.Dispose();

                if (hand.Root != null)
                {
                    Destroy(hand.Root.gameObject);
                }
            }

            hands.Clear();


            foreach (GameObject visual in controllerVisuals)
            {
                if (visual != null)
                {
                    visual.SetActive(true);
                }
            }
        }


        private void LateUpdate()
        {
            float t =
                1.0f - Mathf.Exp(-fingerSharpness * Time.unscaledDeltaTime);


            foreach (HandRig hand in hands)
            {
                hand.TriggerValue =
                    Mathf.Lerp(
                        hand.TriggerValue,
                        ReadAxis(hand.Trigger),
                        t
                    );

                hand.GripValue =
                    Mathf.Lerp(
                        hand.GripValue,
                        ReadAxis(hand.Grip),
                        t
                    );


                Pose(hand.Index, hand.TriggerValue);

                foreach (Finger finger in hand.GripFingers)
                {
                    Pose(finger, hand.GripValue);
                }
            }
        }


        // =========================================================
        // POSING
        // =========================================================

        private static float ReadAxis(
            InputAction action
        )
        {
            return action != null
                ? Mathf.Clamp01(action.ReadValue<float>())
                : 0.0f;
        }


        /// <summary>
        /// Curls a finger: each joint turns about the handle axis (local
        /// Z), wrapping the finger around the handle.
        /// </summary>
        private static void Pose(
            Finger finger,
            float amount
        )
        {
            for (int i = 0; i < finger.Joints.Length; i++)
            {
                finger.Joints[i].localRotation =
                    Quaternion.AngleAxis(
                        -Mathf.Lerp(
                            finger.OpenDegrees[i],
                            finger.ClosedDegrees[i],
                            amount
                        ),
                        Vector3.forward
                    );
            }
        }


        // =========================================================
        // BUILD (right hand, grip space, metres)
        // =========================================================

        private HandRig BuildHand(
            Transform controller,
            string usage,
            bool mirror
        )
        {
            var root =
                new GameObject(usage + "Glove").transform;

            root.SetParent(
                controller,
                false
            );

            root.localPosition =
                new Vector3(
                    mirror ? -offsetMeters.x : offsetMeters.x,
                    offsetMeters.y,
                    offsetMeters.z
                );

            root.localRotation =
                Quaternion.Euler(
                    offsetEuler.x,
                    mirror ? -offsetEuler.y : offsetEuler.y,
                    mirror ? -offsetEuler.z : offsetEuler.z
                );

            // The left hand is the mirror image of the right.
            root.localScale =
                new Vector3(
                    mirror ? -handScale : handScale,
                    handScale,
                    handScale
                );


            var hand =
                new HandRig
                {
                    Root = root,
                    Trigger = CreateAxis(triggerBinding, usage),
                    Grip = CreateAxis(gripBinding, usage)
                };


            // Palm beside the handle (+X), back of the hand outward, a
            // flat oval reaching from the knuckles back to the wrist (+Y).
            // The hand ends at the wrist (no forearm).
            Segment(
                root,
                "Palm",
                PrimitiveType.Sphere,
                new Vector3(0.037f, 0.022f, 0.003f),
                Quaternion.identity,
                new Vector3(0.024f, 0.105f, 0.082f)
            );


            // Fingers: knuckles at the front-right of the handle, pointing
            // forward (−Y) when open, wrapping around the handle (about Z)
            // when closed. Lengths: proximal, middle, distal.
            hand.Index =
                BuildFinger(
                    root, "Index", new Vector3(0.030f, -0.028f, 0.034f), 0.0085f,
                    new[] { 0.042f, 0.025f, 0.022f },
                    new[] { 20.0f, 15.0f, 10.0f },
                    new[] { 62.0f, 70.0f, 45.0f }
                );

            hand.GripFingers.Add(
                BuildFinger(
                    root, "Middle", new Vector3(0.031f, -0.030f, 0.012f), 0.0088f,
                    new[] { 0.045f, 0.028f, 0.023f },
                    new[] { 30.0f, 25.0f, 15.0f },
                    new[] { 88.0f, 95.0f, 70.0f }
                )
            );

            hand.GripFingers.Add(
                BuildFinger(
                    root, "Ring", new Vector3(0.031f, -0.029f, -0.010f), 0.0083f,
                    new[] { 0.042f, 0.026f, 0.022f },
                    new[] { 32.0f, 27.0f, 15.0f },
                    new[] { 88.0f, 95.0f, 70.0f }
                )
            );

            hand.GripFingers.Add(
                BuildFinger(
                    root, "Pinky", new Vector3(0.030f, -0.026f, -0.030f), 0.0074f,
                    new[] { 0.034f, 0.020f, 0.019f },
                    new[] { 35.0f, 30.0f, 15.0f },
                    new[] { 88.0f, 95.0f, 70.0f }
                )
            );


            // Thumb: from the palm's upper index side over the top of the
            // controller (the thumbstick), slightly bent; not animated.
            Transform thumbBase =
                new GameObject("Thumb").transform;

            thumbBase.SetParent(root, false);

            thumbBase.localPosition =
                new Vector3(0.028f, 0.012f, 0.040f);

            thumbBase.localRotation =
                Quaternion.FromToRotation(
                    Vector3.down,
                    new Vector3(-0.85f, 0.05f, 0.52f).normalized
                );

            Transform thumbTip =
                Bone(thumbBase, "ThumbProximal", 0.036f, 0.0095f);

            thumbTip.localRotation =
                Quaternion.AngleAxis(-20.0f, Vector3.forward);

            Bone(thumbTip, "ThumbDistal", 0.030f, 0.0090f);


            Pose(hand.Index, 0.0f);

            foreach (Finger finger in hand.GripFingers)
            {
                Pose(finger, 0.0f);
            }


            return hand;
        }


        private Finger BuildFinger(
            Transform root,
            string name,
            Vector3 knuckle,
            float radius,
            float[] lengths,
            float[] openDegrees,
            float[] closedDegrees
        )
        {
            var joints =
                new Transform[lengths.Length];

            Transform parent =
                root;

            Vector3 position =
                knuckle;


            for (int i = 0; i < lengths.Length; i++)
            {
                var joint =
                    new GameObject($"{name}{i}").transform;

                joint.SetParent(parent, false);

                joint.localPosition =
                    position;

                Bone(
                    joint,
                    $"{name}{i}Bone",
                    lengths[i],
                    radius * (1.0f - 0.08f * i)
                );

                joints[i] =
                    joint;

                parent =
                    joint;

                position =
                    Vector3.down * lengths[i];
            }


            return new Finger
            {
                Joints = joints,
                OpenDegrees = openDegrees,
                ClosedDegrees = closedDegrees
            };
        }


        /// <summary>
        /// A capsule from the joint along its local −Y; returns a child
        /// transform at the bone's end (for the next joint).
        /// </summary>
        private Transform Bone(
            Transform joint,
            string name,
            float length,
            float radius
        )
        {
            // Unity's capsule: height 2 along Y, radius 0.5.
            Segment(
                joint,
                name,
                PrimitiveType.Capsule,
                Vector3.down * (0.5f * length),
                Quaternion.identity,
                new Vector3(
                    2.0f * radius,
                    0.5f * (length + 2.0f * radius),
                    2.0f * radius
                )
            );

            var end =
                new GameObject(name + "End").transform;

            end.SetParent(joint, false);

            end.localPosition =
                Vector3.down * length;

            return end;
        }


        private void Segment(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale
        )
        {
            GameObject segment =
                GameObject.CreatePrimitive(primitive);

            segment.name =
                name;

            // No colliders: controller rays must not hit the hands.
            Destroy(
                segment.GetComponent<Collider>()
            );

            segment.transform.SetParent(parent, false);

            segment.transform.localPosition =
                localPosition;

            segment.transform.localRotation =
                localRotation;

            segment.transform.localScale =
                localScale;


            var renderer =
                segment.GetComponent<MeshRenderer>();

            renderer.sharedMaterial =
                handMaterial;

            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }


        private static InputAction CreateAction(
            string binding
        )
        {
            var action =
                new InputAction(
                    binding,
                    InputActionType.Value,
                    binding,
                    expectedControlType: "Axis"
                );

            action.Enable();

            return action;
        }


        private static InputAction CreateAxis(
            string bindingFormat,
            string usage
        )
        {
            return string.IsNullOrEmpty(bindingFormat)
                ? null
                : CreateAction(
                    string.Format(bindingFormat, usage)
                );
        }
    }
}
