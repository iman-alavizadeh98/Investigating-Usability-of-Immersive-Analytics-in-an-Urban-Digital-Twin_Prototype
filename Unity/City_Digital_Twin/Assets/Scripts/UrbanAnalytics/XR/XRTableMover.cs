using UnityEngine;
using UnityEngine.InputSystem;

using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Lets the user move the table instead of walking around it:
    ///
    ///   Left grip (hold)          grab the table: it follows the hand
    ///                             and turns with the wrist
    ///   Left thumbstick ← →       turn the table about its centre
    ///   Left thumbstick press     bring the table in front of you
    ///
    /// Everything goes through TabletopRig.MoveTable, which places the
    /// rig, not the city (picking, highlights and logging in world units
    /// are unaffected). The hand is read in XR Origin space, which does
    /// not change when the rig is re-placed, so dragging is stable.
    ///
    /// While a controller ray is on a panel the thumbstick scrolls the
    /// panel and is ignored here. Every finished move is logged
    /// (Debug.Log and the study event vr_table_move).
    /// </summary>
    public sealed class XRTableMover :
        MonoBehaviour
    {
        // =========================================================
        // INSPECTOR
        // =========================================================

        [Tooltip("The left controller (its pose drives the grab).")]
        [SerializeField]
        private Transform controller;

        [SerializeField]
        private Camera viewCamera;


        [Header("Bindings (Input System paths)")]
        [SerializeField]
        private string grabBinding =
            "<XRController>{LeftHand}/gripPressed";

        [SerializeField]
        private string thumbstickBinding =
            "<XRController>{LeftHand}/{Primary2DAxis}";

        [SerializeField]
        private string recallBinding =
            "<XRController>{LeftHand}/{Primary2DAxisClick}";


        [Header("Behaviour")]
        [Tooltip("While grabbing, turning the wrist turns the table.")]
        [SerializeField]
        private bool rotateWithHand =
            true;

        [Tooltip("Thumbstick turn speed at full deflection (degrees/s).")]
        [SerializeField]
        [Range(10.0f, 180.0f)]
        private float turnSpeedDegrees =
            60.0f;

        [SerializeField]
        [Range(0.05f, 0.6f)]
        private float turnDeadzone =
            0.25f;


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private TabletopRig tabletopRig;

        [SerializeField]
        private XRControllerPointer pointer;

        [SerializeField]
        private StudySession studySession;


        // =========================================================
        // RUNTIME
        // =========================================================

        private InputAction grabAction;

        private InputAction thumbstickAction;

        private InputAction recallAction;

        private bool grabbing;

        private bool turning;

        private Vector3 grabHandStart;

        private float grabHandYawStart;

        private Vector3 grabCenterStart;

        private float grabTableYawStart;


        /// <summary>True while the left grip holds the table.</summary>
        public bool IsGrabbing =>
            grabbing;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (tabletopRig == null)
            {
                tabletopRig =
                    FindFirstObjectByType<TabletopRig>();
            }

            if (pointer == null)
            {
                pointer =
                    FindFirstObjectByType<XRControllerPointer>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }

            if (viewCamera == null)
            {
                viewCamera =
                    Camera.main;
            }
        }


        private void OnEnable()
        {
            grabAction =
                CreateAction("XR Table Grab", InputActionType.Button, grabBinding, null);

            thumbstickAction =
                CreateAction("XR Table Turn", InputActionType.Value, thumbstickBinding, "Vector2");

            recallAction =
                CreateAction("XR Table Recall", InputActionType.Button, recallBinding, null);

            if (recallAction != null)
            {
                recallAction.performed +=
                    HandleRecall;
            }
        }


        private void OnDisable()
        {
            if (recallAction != null)
            {
                recallAction.performed -=
                    HandleRecall;
            }

            grabAction?.Dispose();

            thumbstickAction?.Dispose();

            recallAction?.Dispose();

            grabAction =
                null;

            thumbstickAction =
                null;

            recallAction =
                null;

            grabbing =
                false;
        }


        private void Update()
        {
            if (tabletopRig == null ||
                !tabletopRig.IsPlaced ||
                controller == null)
            {
                return;
            }


            UpdateGrab();


            if (!grabbing)
            {
                UpdateTurn(
                    Time.unscaledDeltaTime
                );
            }
        }


        // =========================================================
        // GRAB
        // =========================================================

        private void UpdateGrab()
        {
            bool held =
                grabAction != null &&
                grabAction.IsPressed();


            if (held &&
                !grabbing)
            {
                grabbing =
                    true;

                grabHandStart =
                    HandRig();

                grabHandYawStart =
                    HandYawRig();

                grabCenterStart =
                    tabletopRig.TableCenterRig;

                grabTableYawStart =
                    tabletopRig.TableYawRig;
            }
            else if (!held &&
                     grabbing)
            {
                grabbing =
                    false;

                LogMove("grab");

                return;
            }


            if (!grabbing)
            {
                return;
            }


            float turn =
                rotateWithHand
                    ? Mathf.DeltaAngle(grabHandYawStart, HandYawRig())
                    : 0.0f;

            Vector3 offset =
                grabCenterStart - grabHandStart;

            offset.y =
                0.0f;


            // The table keeps its offset to the hand, turned with it.
            tabletopRig.MoveTable(
                HandRig() + Quaternion.Euler(0.0f, turn, 0.0f) * offset,
                grabTableYawStart + turn
            );
        }


        /// <summary>Left controller position in XR Origin space (metres).</summary>
        private Vector3 HandRig()
        {
            return tabletopRig.Origin.InverseTransformPoint(
                controller.position
            );
        }


        /// <summary>Heading of the left controller in XR Origin space.</summary>
        private float HandYawRig()
        {
            Vector3 forward =
                tabletopRig.Origin.InverseTransformDirection(
                    controller.forward
                );

            // Pointing straight up/down has no heading; use the
            // controller's up axis then.
            if (forward.x * forward.x + forward.z * forward.z < 0.04f)
            {
                forward =
                    tabletopRig.Origin.InverseTransformDirection(
                        controller.up
                    );
            }

            return Mathf.Atan2(
                forward.x,
                forward.z
            ) * Mathf.Rad2Deg;
        }


        // =========================================================
        // THUMBSTICK
        // =========================================================

        private void UpdateTurn(
            float deltaTime
        )
        {
            Vector2 stick =
                thumbstickAction != null
                    ? thumbstickAction.ReadValue<Vector2>()
                    : Vector2.zero;


            // On a panel the thumbstick scrolls it (XRI UI input).
            if (pointer != null &&
                pointer.AnyHandOnUi)
            {
                stick =
                    Vector2.zero;
            }


            float x =
                Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
                    ? stick.x
                    : 0.0f;


            if (Mathf.Abs(x) < turnDeadzone)
            {
                if (turning)
                {
                    turning =
                        false;

                    LogMove("turn");
                }

                return;
            }


            float amount =
                Mathf.Sign(x) *
                (Mathf.Abs(x) - turnDeadzone) /
                (1.0f - turnDeadzone);

            // Pushing right moves the near edge to the right, like
            // turning a lazy Susan (counter-clockwise seen from above).
            tabletopRig.MoveTable(
                tabletopRig.TableCenterRig,
                tabletopRig.TableYawRig - amount * turnSpeedDegrees * deltaTime
            );

            turning =
                true;
        }


        private void HandleRecall(
            InputAction.CallbackContext context
        )
        {
            if (tabletopRig == null ||
                !tabletopRig.IsPlaced ||
                viewCamera == null)
            {
                return;
            }


            tabletopRig.BringTableTo(
                tabletopRig.Origin.InverseTransformPoint(
                    viewCamera.transform.position
                ),
                tabletopRig.Origin.InverseTransformDirection(
                    viewCamera.transform.forward
                )
            );

            LogMove("recall");
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private void LogMove(
            string how
        )
        {
            Vector3 center =
                tabletopRig.TableCenterRig;

            Debug.Log(
                $"XRTableMover: table {how} → centre ({center.x:0.00}, " +
                $"{center.z:0.00}) m, turned {tabletopRig.TableYawRig:0} ° " +
                "(XR Origin space).",
                this
            );

            studySession?.LogEvent(
                "vr_table_move",
                ("how", how),
                ("centerX", center.x),
                ("centerZ", center.z),
                ("yawDegrees", tabletopRig.TableYawRig)
            );
        }


        private static InputAction CreateAction(
            string name,
            InputActionType type,
            string binding,
            string controlType
        )
        {
            if (string.IsNullOrEmpty(binding))
            {
                return null;
            }


            var action =
                new InputAction(
                    name,
                    type,
                    binding,
                    expectedControlType: controlType
                );

            action.Enable();

            return action;
        }
    }
}
