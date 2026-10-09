using UnityEngine;
using UnityEngine.InputSystem;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Controller-button shortcuts in VR:
    ///
    ///   A (right primary)      copy what the ray points at (else the
    ///                          selection); same as the Copy tool
    ///   B (right secondary)    unselect (= Esc)
    ///   Y (left secondary)     show / hide the Info panel
    ///   Right thumbstick ↑ ↓   bigger / smaller table (held)
    ///
    /// Views are changed on the table toolbar or the wrist menu (no
    /// thumbstick flick: it switched views by accident). Copy and
    /// Compare are click tools on the toolbar (XRClickTools). While a
    /// controller ray is on a panel, the thumbstick scrolls the panel
    /// (XRI) and is ignored here. X (left primary) belongs to the wrist
    /// menu (XRHandMenu). B and resizing are locked by the tutorial
    /// until it teaches them (XRFeatureLock).
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
        private string infoPanelBinding =
            "<XRController>{LeftHand}/secondaryButton";

        [SerializeField]
        private string thumbstickBinding =
            "<XRController>{RightHand}/{Primary2DAxis}";


        [Header("Thumbstick")]
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
        private TabletopRig tabletopRig;

        [SerializeField]
        private XRControllerPointer pointer;

        [SerializeField]
        private XRInfoPanel infoPanel;

        [SerializeField]
        private XRSelectionCopies copies;

        [SerializeField]
        private StudySession studySession;


        // =========================================================
        // RUNTIME
        // =========================================================

        private InputAction copyAction;

        private InputAction clearAction;

        private InputAction infoPanelAction;

        private InputAction thumbstickAction;

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

            if (copies == null)
            {
                copies =
                    FindFirstObjectByType<XRSelectionCopies>();
            }

            if (infoPanel == null)
            {
                infoPanel =
                    FindFirstObjectByType<XRInfoPanel>();
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
                CreateButton("XR Copy", copyBinding, HandleCopy);

            clearAction =
                CreateButton("XR Unselect", clearBinding, HandleClear);

            infoPanelAction =
                CreateButton("XR Toggle Info Panel", infoPanelBinding, HandleInfoPanel);


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

            DisposeButton(ref infoPanelAction, HandleInfoPanel);

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
            if ((pointer != null && pointer.AnyHandOnUi) ||
                !XRFeatureLock.Allows(XRFeature.ResizeTable))
            {
                stick =
                    Vector2.zero;
            }


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
            if (copies == null ||
                interactionManager == null ||
                !XRFeatureLock.Allows(XRFeature.CopyTool))
            {
                return;
            }


            // What the ray points at; with nothing hovered, the selection.
            UrbanAnalytics.Interaction.EntityReference target =
                interactionManager.Hovered.IsValid
                    ? interactionManager.Hovered.Entity
                    : interactionManager.Selected;

            copies.Copy(
                target
            );
        }


        private void HandleClear(
            InputAction.CallbackContext context
        )
        {
            if (XRFeatureLock.Allows(XRFeature.Select))
            {
                interactionManager?.ClearSelection();
            }
        }


        private void HandleInfoPanel(
            InputAction.CallbackContext context
        )
        {
            if (infoPanel != null)
            {
                infoPanel.SetVisible(
                    !infoPanel.IsVisible
                );
            }
        }


        // =========================================================
        // THUMBSTICK
        // =========================================================

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
