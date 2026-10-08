using UnityEngine;
using UnityEngine.InputSystem;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// VR equivalents of the desktop keyboard shortcuts, on the
    /// controller buttons and the right thumbstick:
    ///
    ///   A (right primary)      copy the selection to compare (= C)
    ///   B (right secondary)    clear the selection (= Esc)
    ///   Y (left secondary)     show / hide the table board
    ///   Right thumbstick ← →   previous / next view (= 1–9)
    ///   Right thumbstick ↑ ↓   bigger / smaller table (held)
    ///
    /// While a controller ray is on a panel, the thumbstick scrolls the
    /// panel (XRI) and is ignored here. X (left primary) belongs to the
    /// wrist menu (XRHandMenu).
    ///
    /// A finished resize is logged (Debug.Log and the study event
    /// vr_table_resize with the length and the rig scale).
    /// </summary>
    public sealed class XRControllerShortcuts :
        MonoBehaviour
    {
        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Bindings (Input System paths)")]
        [SerializeField]
        private string copyBinding =
            "<XRController>{RightHand}/primaryButton";

        [SerializeField]
        private string clearBinding =
            "<XRController>{RightHand}/secondaryButton";

        [SerializeField]
        private string boardBinding =
            "<XRController>{LeftHand}/secondaryButton";

        [SerializeField]
        private string thumbstickBinding =
            "<XRController>{RightHand}/{Primary2DAxis}";


        [Header("Thumbstick")]
        [Tooltip("Sideways deflection that switches the view once.")]
        [SerializeField]
        [Range(0.3f, 1.0f)]
        private float viewFlickThreshold =
            0.7f;

        [Tooltip("The stick must come back below this before the next switch.")]
        [SerializeField]
        [Range(0.0f, 0.6f)]
        private float viewRearmThreshold =
            0.3f;

        [Tooltip("Up/down deflection below this does not resize.")]
        [SerializeField]
        [Range(0.05f, 0.6f)]
        private float resizeDeadzone =
            0.25f;

        [Tooltip(
            "Resize speed at full deflection: the table length is " +
            "multiplied by e^(speed · seconds)."
        )]
        [SerializeField]
        [Range(0.1f, 2.0f)]
        private float resizeSpeed =
            0.6f;


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private InteractionManager interactionManager;

        [SerializeField]
        private ComparisonManager comparisonManager;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private TabletopRig tabletopRig;

        [SerializeField]
        private XRControllerPointer pointer;

        [SerializeField]
        private XRTableBoard tableBoard;

        [SerializeField]
        private StudySession studySession;


        // =========================================================
        // RUNTIME
        // =========================================================

        private InputAction copyAction;

        private InputAction clearAction;

        private InputAction boardAction;

        private InputAction thumbstickAction;

        private bool viewArmed =
            true;

        private bool resizing;


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

            if (comparisonManager == null)
            {
                comparisonManager =
                    FindFirstObjectByType<ComparisonManager>();
            }

            if (visualizationSwitcher == null)
            {
                visualizationSwitcher =
                    FindFirstObjectByType<VisualizationSwitcher>();
            }

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

            if (tableBoard == null)
            {
                tableBoard =
                    FindFirstObjectByType<XRTableBoard>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }
        }


        private void OnEnable()
        {
            copyAction =
                CreateButton("XR Copy To Compare", copyBinding, HandleCopy);

            clearAction =
                CreateButton("XR Clear Selection", clearBinding, HandleClear);

            boardAction =
                CreateButton("XR Toggle Board", boardBinding, HandleBoard);


            if (!string.IsNullOrEmpty(thumbstickBinding))
            {
                thumbstickAction =
                    new InputAction(
                        "XR Shortcut Thumbstick",
                        InputActionType.Value,
                        thumbstickBinding,
                        expectedControlType: "Vector2"
                    );

                thumbstickAction.Enable();
            }
        }


        private void OnDisable()
        {
            DisposeButton(ref copyAction, HandleCopy);

            DisposeButton(ref clearAction, HandleClear);

            DisposeButton(ref boardAction, HandleBoard);

            thumbstickAction?.Dispose();

            thumbstickAction =
                null;
        }


        private void Update()
        {
            if (thumbstickAction == null)
            {
                return;
            }


            Vector2 stick =
                thumbstickAction.ReadValue<Vector2>();


            // On a panel the thumbstick scrolls it (XRI UI input).
            if (pointer != null &&
                pointer.AnyHandOnUi)
            {
                stick =
                    Vector2.zero;
            }


            UpdateViewFlick(
                stick
            );

            UpdateResize(
                stick,
                Time.unscaledDeltaTime
            );
        }


        // =========================================================
        // BUTTONS
        // =========================================================

        private void HandleCopy(
            InputAction.CallbackContext context
        )
        {
            if (comparisonManager != null &&
                interactionManager != null &&
                interactionManager.HasSelection)
            {
                comparisonManager.CopyToNextSlot(
                    interactionManager.Selected
                );
            }
        }


        private void HandleClear(
            InputAction.CallbackContext context
        )
        {
            interactionManager?.ClearSelection();
        }


        private void HandleBoard(
            InputAction.CallbackContext context
        )
        {
            if (tableBoard != null)
            {
                tableBoard.SetVisible(
                    !tableBoard.IsVisible
                );
            }
        }


        // =========================================================
        // THUMBSTICK
        // =========================================================

        private void UpdateViewFlick(
            Vector2 stick
        )
        {
            float x =
                Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
                    ? stick.x
                    : 0.0f;


            if (Mathf.Abs(x) < viewRearmThreshold)
            {
                viewArmed =
                    true;

                return;
            }


            if (!viewArmed ||
                Mathf.Abs(x) < viewFlickThreshold)
            {
                return;
            }


            viewArmed =
                false;

            StepView(
                x > 0.0f
                    ? 1
                    : -1
            );
        }


        private void StepView(
            int step
        )
        {
            if (visualizationSwitcher == null ||
                visualizationSwitcher.IsApplying)
            {
                return;
            }


            int count =
                visualizationSwitcher.Options.Count;

            if (count == 0)
            {
                return;
            }


            int current =
                visualizationSwitcher.ActiveIndex;

            int next =
                current < 0
                    ? (step > 0 ? 0 : count - 1)
                    : ((current + step) % count + count) % count;


            visualizationSwitcher.Apply(
                next
            );
        }


        private void UpdateResize(
            Vector2 stick,
            float deltaTime
        )
        {
            float y =
                Mathf.Abs(stick.y) > Mathf.Abs(stick.x)
                    ? stick.y
                    : 0.0f;


            if (Mathf.Abs(y) < resizeDeadzone)
            {
                if (resizing)
                {
                    resizing =
                        false;

                    LogResize();
                }

                return;
            }


            if (tabletopRig == null ||
                !tabletopRig.IsPlaced)
            {
                return;
            }


            float amount =
                Mathf.Sign(y) *
                (Mathf.Abs(y) - resizeDeadzone) /
                (1.0f - resizeDeadzone);

            tabletopRig.SetTableLength(
                tabletopRig.TableLengthMeters *
                Mathf.Exp(resizeSpeed * amount * deltaTime)
            );

            resizing =
                true;
        }


        private void LogResize()
        {
            if (tabletopRig == null)
            {
                return;
            }


            Debug.Log(
                $"XRControllerShortcuts: table resized to " +
                $"{tabletopRig.TableLengthMeters:0.00} m " +
                $"(1 m = {tabletopRig.WorldUnitsPerMeter:0.000} km, " +
                $"scale 1:{tabletopRig.WorldUnitsPerMeter * 1000.0f:0}).",
                this
            );

            studySession?.LogEvent(
                "vr_table_resize",
                ("lengthMeters", tabletopRig.TableLengthMeters),
                ("worldUnitsPerMeter", tabletopRig.WorldUnitsPerMeter)
            );
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private static InputAction CreateButton(
            string name,
            string binding,
            System.Action<InputAction.CallbackContext> handler
        )
        {
            if (string.IsNullOrEmpty(binding))
            {
                return null;
            }


            var action =
                new InputAction(
                    name,
                    InputActionType.Button,
                    binding
                );

            action.performed +=
                handler;

            action.Enable();

            return action;
        }


        private static void DisposeButton(
            ref InputAction action,
            System.Action<InputAction.CallbackContext> handler
        )
        {
            if (action == null)
            {
                return;
            }


            action.performed -=
                handler;

            action.Dispose();

            action =
                null;
        }
    }
}
