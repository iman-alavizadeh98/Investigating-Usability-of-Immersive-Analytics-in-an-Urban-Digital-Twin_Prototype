using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// The wrist menu on the left wrist, operated with the other hand's
    /// ray (trigger = click, thumbstick = scroll).
    ///
    /// It stands just above the left wrist, facing the eyes, and opens
    /// when the user raises the wrist and looks at it (like reading a
    /// watch); it closes shortly after the wrist is lowered, but stays
    /// open while a controller ray is on it. X pins it where it is
    /// (open, fixed in the room) and X again sends it back to the wrist.
    /// With <see cref="AutoShow"/> off (XR Device Simulator) it is
    /// always shown on the wrist. Locked by the tutorial until it
    /// teaches it (XRFeatureLock, <see cref="XRFeature.WristMenu"/>).
    ///
    /// Tabs:
    ///   Views  — the visualization list (same as the desktop list).
    ///   Task   — the study session: start the training and the tasks,
    ///            show the view, answer, confidence, submit / skip.
    ///            Opens automatically when a scenario view is shown.
    ///   Panels — show / hide the Info, Legend and Compare panels, Help
    ///            (picture brochure), Reset panels, Restart tutorial,
    ///            hand tracking on/off (experimental).
    /// What is selected, the legend and the comparison are separate
    /// panels at the table (XRInfoPanel, XRTableLegend, XRComparePanel);
    /// help is the XRHelpPanel brochure.
    ///
    /// Built from code with RuntimeUi. Sized in real metres: the canvas
    /// sits under the controller's parent, which is under the scaled XR
    /// Origin, so the rig scale is inherited.
    ///
    /// Visibility, pin and tab changes are logged as study events
    /// (vr_menu, vr_menu_tab) while a session runs.
    /// </summary>
    public sealed class XRHandMenu :
        MonoBehaviour
    {
        private enum Tab
        {
            Views,
            Task,
            Panels
        }


        private const float ButtonHeight =
            44.0f;


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Wrist placement (real metres)")]
        [Tooltip(
            "Left controller. The panel stands above its wrist and " +
            "lives in the controller's parent space (the scaled rig)."
        )]
        [SerializeField]
        private Transform anchor;

        [Tooltip("Wrist point in controller space (behind the grip).")]
        [SerializeField]
        private Vector3 wristOffsetMeters =
            new Vector3(0.0f, -0.01f, -0.09f);

        [Tooltip("Gap between the wrist and the panel's bottom edge.")]
        [SerializeField]
        [Range(0.0f, 0.2f)]
        private float panelGapMeters =
            0.03f;

        [Tooltip("How fast the panel follows the wrist (1/s).")]
        [SerializeField]
        [Range(1.0f, 40.0f)]
        private float followSharpness =
            14.0f;

        [Tooltip("Real metres per canvas unit (0.0005 = 0.5 mm per unit).")]
        [SerializeField]
        [Range(0.0002f, 0.002f)]
        private float metersPerUnit =
            0.0005f;

        [Tooltip("Canvas size in units (520 × 640 at 0.5 mm = 26 × 32 cm).")]
        [SerializeField]
        private Vector2 sizeUnits =
            new Vector2(520.0f, 640.0f);


        [Header("Look at the wrist to open")]
        [Tooltip(
            "On: the panel opens while the user looks at the raised " +
            "wrist. Off: always shown on the wrist (simulator)."
        )]
        [SerializeField]
        private bool autoShow =
            true;

        [Tooltip("The wrist must be within this angle of the view centre.")]
        [SerializeField]
        [Range(10.0f, 70.0f)]
        private float viewConeDegrees =
            40.0f;

        [Tooltip("The wrist must be closer to the eyes than this.")]
        [SerializeField]
        [Range(0.3f, 1.2f)]
        private float maxWristDistanceMeters =
            0.75f;

        [Tooltip(
            "Back-of-wrist direction in left-controller space; it " +
            "must point roughly at the eyes (like reading a watch)."
        )]
        [SerializeField]
        private Vector3 wristFaceNormal =
            new Vector3(-1.0f, 0.5f, 0.0f);

        [Tooltip(
            "Hand tracking: back-of-wrist direction in the palm joint's " +
            "space (+Y = back of the hand)."
        )]
        [SerializeField]
        private Vector3 handFaceNormal =
            Vector3.up;

        [Tooltip("Minimum dot product of the wrist normal and the eye direction.")]
        [SerializeField]
        [Range(-1.0f, 1.0f)]
        private float facingThreshold =
            0.25f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float showDelaySeconds =
            0.15f;

        [Tooltip("Kept open this long after the gesture ends (and while a ray is on it).")]
        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float hideDelaySeconds =
            0.6f;

        [Tooltip(
            "Draws after other transparent objects (the selection " +
            "highlight is Transparent+10 and would show through)."
        )]
        [SerializeField]
        private int sortingOrder =
            110;


        [Header("Input")]
        [Tooltip(
            "Pins the panel where it is (stays open, world-locked) / " +
            "sends it back to the wrist."
        )]
        [SerializeField]
        private string toggleBinding =
            "<XRController>{LeftHand}/primaryButton";


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private Camera eventCamera;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private StudySession studySession;

        [Tooltip("Keeps the panel open while a controller ray is on it.")]
        [SerializeField]
        private XRControllerPointer pointer;

        [SerializeField]
        private XRInfoPanel infoPanel;

        [SerializeField]
        private XRTableLegend legend;

        [SerializeField]
        private XRComparePanel comparePanel;

        [SerializeField]
        private XRHelpPanel helpPanel;

        [SerializeField]
        private XRTableToolbar toolbar;

        [SerializeField]
        private XRTutorial tutorial;

        [SerializeField]
        private XRInputModeSwitch inputModeSwitch;


        // =========================================================
        // RUNTIME
        // =========================================================

        private GameObject root;

        private TMP_Text headerView;

        private readonly Dictionary<Tab, Button> tabButtons =
            new Dictionary<Tab, Button>();

        private readonly Dictionary<Tab, GameObject> tabPages =
            new Dictionary<Tab, GameObject>();

        private Tab currentTab =
            Tab.Views;

        private InputAction toggleAction;

        private bool started;

        // Wrist behaviour
        private static readonly List<XRHandSubsystem> HandSubsystems =
            new List<XRHandSubsystem>();

        private XRHandSubsystem handSubsystem;

        private bool pinned;

        private float gestureOnSeconds;

        private float gestureOffSeconds;

        private bool snapNextFollow =
            true;

        private bool wasLocked;


        // Views
        private TMP_Text viewsStatus;

        private RectTransform viewsList;

        private readonly List<Button> viewButtons =
            new List<Button>();


        // Panels
        private readonly List<(Button Button, XRTablePanel Panel)> panelButtons =
            new List<(Button, XRTablePanel)>();

        private Button handsButton;


        // Task
        private TMP_Text taskHeader;

        private TMP_Text taskQuestion;

        private RectTransform taskBody;

        private RectTransform taskActions;

        private TMP_Text taskStatus;


        /// <summary>The canvas root (for tests and the scene builder).</summary>
        public GameObject Root =>
            root;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (visualizationSwitcher == null)
            {
                visualizationSwitcher =
                    FindFirstObjectByType<VisualizationSwitcher>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }

            if (eventCamera == null)
            {
                eventCamera =
                    Camera.main;
            }

            if (pointer == null)
            {
                pointer =
                    FindFirstObjectByType<XRControllerPointer>();
            }

            if (infoPanel == null)
            {
                infoPanel =
                    FindFirstObjectByType<XRInfoPanel>();
            }

            if (legend == null)
            {
                legend =
                    FindFirstObjectByType<XRTableLegend>();
            }

            if (comparePanel == null)
            {
                comparePanel =
                    FindFirstObjectByType<XRComparePanel>();
            }

            if (helpPanel == null)
            {
                helpPanel =
                    FindFirstObjectByType<XRHelpPanel>();
            }

            if (toolbar == null)
            {
                toolbar =
                    FindFirstObjectByType<XRTableToolbar>();
            }

            if (tutorial == null)
            {
                tutorial =
                    FindFirstObjectByType<XRTutorial>();
            }

            if (inputModeSwitch == null)
            {
                inputModeSwitch =
                    FindFirstObjectByType<XRInputModeSwitch>();
            }


            if (anchor == null)
            {
                Debug.LogError(
                    "XRHandMenu: assign the left controller as anchor.",
                    this
                );

                enabled =
                    false;

                return;
            }


            Build();
        }


        private void OnEnable()
        {
            if (root == null)
            {
                return;
            }


            if (!string.IsNullOrEmpty(toggleBinding))
            {
                toggleAction =
                    new InputAction(
                        "XRHandMenu Toggle",
                        InputActionType.Button,
                        toggleBinding
                    );

                toggleAction.performed +=
                    HandleToggle;

                toggleAction.Enable();
            }


            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed +=
                    RefreshViews;
            }

            if (studySession != null)
            {
                studySession.Changed +=
                    HandleStudyChanged;
            }

            foreach (XRTablePanel panel in Panels())
            {
                panel.VisibilityChanged +=
                    HandlePanelVisibility;
            }

            XRFeatureLock.Changed +=
                RefreshViews;


            // Other systems finish initialising in their own Awake /
            // Start; the first refresh waits for Start.
            if (started)
            {
                RefreshAll();
            }
        }


        private void Start()
        {
            started =
                true;

            RefreshAll();
        }


        private void OnDisable()
        {
            if (toggleAction != null)
            {
                toggleAction.performed -=
                    HandleToggle;

                toggleAction.Dispose();

                toggleAction =
                    null;
            }


            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed -=
                    RefreshViews;
            }

            if (studySession != null)
            {
                studySession.Changed -=
                    HandleStudyChanged;
            }

            foreach (XRTablePanel panel in Panels())
            {
                panel.VisibilityChanged -=
                    HandlePanelVisibility;
            }

            XRFeatureLock.Changed -=
                RefreshViews;
        }


        private IEnumerable<XRTablePanel> Panels()
        {
            if (infoPanel != null) yield return infoPanel;
            if (legend != null) yield return legend;
            if (comparePanel != null) yield return comparePanel;
            if (helpPanel != null) yield return helpPanel;
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        public bool IsVisible =>
            root != null &&
            root.activeSelf;


        /// <summary>Pinned: open and fixed in the room (X).</summary>
        public bool IsPinned =>
            pinned;


        /// <summary>
        /// On: opens on the look-at-wrist gesture. Off: always shown on
        /// the wrist (used with the XR Device Simulator).
        /// </summary>
        public bool AutoShow
        {
            get => autoShow;
            set
            {
                autoShow =
                    value;

                gestureOnSeconds =
                    0.0f;

                gestureOffSeconds =
                    0.0f;

                if (!autoShow &&
                    !pinned)
                {
                    SetVisible(true);
                }
            }
        }


        public void SetVisible(
            bool visible
        )
        {
            if (root == null ||
                root.activeSelf == visible)
            {
                return;
            }


            root.SetActive(
                visible
            );

            snapNextFollow =
                true;

            studySession?.LogEvent(
                "vr_menu",
                ("visible", visible),
                ("pinned", pinned),
                ("tab", currentTab.ToString())
            );
        }


        /// <summary>
        /// Opens the panel pinned 45 cm in front of the eyes (a little
        /// below eye level), on the given tab. Used by the table toolbar,
        /// e.g. when only hands are used.
        /// </summary>
        public void PinInFront(
            string tabName
        )
        {
            if (root == null ||
                eventCamera == null)
            {
                return;
            }


            Transform space =
                root.transform.parent;

            Vector3 head =
                space.InverseTransformPoint(
                    eventCamera.transform.position
                );

            Vector3 forward =
                space.InverseTransformDirection(
                    eventCamera.transform.forward
                );

            forward.y =
                0.0f;

            if (forward.sqrMagnitude < 1e-6f)
            {
                forward =
                    Vector3.forward;
            }

            forward.Normalize();


            SetVisible(true);

            root.transform.localPosition =
                head + forward * 0.45f + Vector3.down * 0.12f;

            root.transform.localRotation =
                Quaternion.LookRotation(
                    root.transform.localPosition - head,
                    Vector3.up
                );

            SetPinned(true);

            if (!string.IsNullOrEmpty(tabName))
            {
                ShowTab(tabName);
            }
        }


        /// <summary>
        /// Pins the panel where it is (opening it at the wrist first if
        /// it was closed), or sends a pinned panel back to the wrist.
        /// </summary>
        public void SetPinned(
            bool pin
        )
        {
            if (root == null)
            {
                return;
            }


            if (pin &&
                !root.activeSelf)
            {
                SetVisible(true);

                FollowWrist(true);
            }


            pinned =
                pin;

            snapNextFollow =
                !pin;

            gestureOffSeconds =
                0.0f;

            studySession?.LogEvent(
                "vr_menu",
                ("visible", root.activeSelf),
                ("pinned", pinned),
                ("tab", currentTab.ToString())
            );
        }


        /// <summary>Opens a tab by name (Views, Task, Panels).</summary>
        public void ShowTab(
            string tabName
        )
        {
            if (System.Enum.TryParse(
                    tabName,
                    true,
                    out Tab tab
                ))
            {
                ShowTab(tab);
            }
        }


        // =========================================================
        // EVENTS
        // =========================================================

        private void HandleToggle(
            InputAction.CallbackContext context
        )
        {
            if (!XRFeatureLock.Allows(XRFeature.WristMenu))
            {
                return;
            }

            SetPinned(
                !pinned
            );
        }


        // =========================================================
        // WRIST
        // =========================================================

        private void LateUpdate()
        {
            if (root == null ||
                anchor == null ||
                eventCamera == null)
            {
                return;
            }


            // Locked by the tutorial: closed, also when pinned or always
            // shown (simulator).
            if (!XRFeatureLock.Allows(XRFeature.WristMenu))
            {
                if (pinned)
                {
                    SetPinned(false);
                }

                SetVisible(false);

                wasLocked =
                    true;

                return;
            }


            if (wasLocked)
            {
                wasLocked =
                    false;

                // Always shown on the wrist (simulator) once unlocked.
                if (!autoShow)
                {
                    SetVisible(true);
                }
            }


            if (pinned)
            {
                return;
            }


            if (autoShow)
            {
                UpdateGesture(
                    Time.unscaledDeltaTime
                );
            }


            if (root.activeSelf)
            {
                FollowWrist(
                    snapNextFollow
                );

                snapNextFollow =
                    false;
            }
        }


        /// <summary>
        /// Opens the panel while the user looks at the raised wrist and
        /// closes it after <see cref="hideDelaySeconds"/> once they stop
        /// (never while a controller ray is on it).
        /// </summary>
        private void UpdateGesture(
            float deltaTime
        )
        {
            Transform space =
                root.transform.parent;

            bool tracked =
                TryGetWrist(
                    space,
                    out Vector3 wrist,
                    out Vector3 faceNormal,
                    out Vector3 controllerForward
                );

            Vector3 head =
                space.InverseTransformPoint(
                    eventCamera.transform.position
                );

            Vector3 toHead =
                head - wrist;

            float distance =
                toHead.magnitude;


            bool looking =
                false;


            // Locked by the tutorial until it teaches the wrist menu.
            if (tracked &&
                XRFeatureLock.Allows(XRFeature.WristMenu) &&
                distance > 1e-4f &&
                distance < maxWristDistanceMeters)
            {
                Vector3 toHeadDirection =
                    toHead / distance;

                Vector3 headForward =
                    space.InverseTransformDirection(
                        eventCamera.transform.forward
                    );

                looking =
                    Vector3.Angle(headForward, -toHeadDirection) < viewConeDegrees &&
                    Vector3.Dot(faceNormal, toHeadDirection) > facingThreshold &&
                    // Not aiming the left controller away (pointing
                    // at the table with the left hand).
                    Vector3.Dot(controllerForward, toHeadDirection) > -0.6f;
            }


            if (looking)
            {
                gestureOnSeconds +=
                    deltaTime;

                gestureOffSeconds =
                    0.0f;
            }
            else
            {
                gestureOffSeconds +=
                    deltaTime;

                gestureOnSeconds =
                    0.0f;
            }


            if (!root.activeSelf &&
                gestureOnSeconds >= showDelaySeconds)
            {
                SetVisible(true);
            }
            else if (root.activeSelf &&
                     gestureOffSeconds >= hideDelaySeconds &&
                     (pointer == null ||
                      !pointer.IsAnyHandPointingAt(root.transform)))
            {
                SetVisible(false);
            }
        }


        /// <summary>
        /// The left wrist in the rig's tracking space (the controllers'
        /// parent): from hand tracking while the left hand is tracked
        /// (wrist joint; back of the hand = palm joint's up), otherwise
        /// from the left controller. False when neither is tracked.
        /// </summary>
        private bool TryGetWrist(
            Transform space,
            out Vector3 wrist,
            out Vector3 faceNormal,
            out Vector3 forward
        )
        {
            // XR Hands joint poses are in the same tracking space as
            // the tracked controllers, i.e. local to Camera Offset.
            if (TryGetTrackedLeftHand(out Pose wristPose, out Pose palmPose))
            {
                wrist =
                    wristPose.position;

                faceNormal =
                    palmPose.rotation * handFaceNormal.normalized;

                forward =
                    palmPose.forward;

                return true;
            }


            if (anchor != null &&
                anchor.gameObject.activeInHierarchy)
            {
                wrist =
                    space.InverseTransformPoint(
                        anchor.TransformPoint(wristOffsetMeters)
                    );

                faceNormal =
                    space.InverseTransformDirection(
                        anchor.TransformDirection(
                            wristFaceNormal.normalized
                        )
                    ).normalized;

                forward =
                    space.InverseTransformDirection(
                        anchor.forward
                    ).normalized;

                return true;
            }


            wrist =
                default;

            faceNormal =
                default;

            forward =
                default;

            return false;
        }


        private bool TryGetTrackedLeftHand(
            out Pose wrist,
            out Pose palm
        )
        {
            wrist =
                default;

            palm =
                default;


            if (handSubsystem == null ||
                !handSubsystem.running)
            {
                handSubsystem =
                    null;

                SubsystemManager.GetSubsystems(
                    HandSubsystems
                );

                foreach (XRHandSubsystem subsystem in HandSubsystems)
                {
                    if (subsystem.running)
                    {
                        handSubsystem =
                            subsystem;

                        break;
                    }
                }
            }


            if (handSubsystem == null)
            {
                return false;
            }


            XRHand hand =
                handSubsystem.leftHand;

            return hand.isTracked &&
                   hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out wrist) &&
                   hand.GetJoint(XRHandJointID.Palm).TryGetPose(out palm);
        }


        /// <summary>
        /// Places the panel just above the wrist, upright and facing the
        /// eyes (smoothed unless <paramref name="snap"/>).
        /// </summary>
        private void FollowWrist(
            bool snap
        )
        {
            Transform space =
                root.transform.parent;

            if (!TryGetWrist(space, out Vector3 wrist, out _, out _))
            {
                return;
            }

            Vector3 head =
                space.InverseTransformPoint(
                    eventCamera.transform.position
                );


            Vector3 position =
                wrist +
                Vector3.up *
                (0.5f * sizeUnits.y * metersPerUnit + panelGapMeters);


            Vector3 view =
                position - head;

            if (view.sqrMagnitude < 1e-6f)
            {
                return;
            }


            // A canvas is read from its -Z side: forward points away
            // from the eyes.
            Quaternion rotation =
                Quaternion.LookRotation(
                    view.normalized,
                    Vector3.up
                );


            float t =
                snap
                    ? 1.0f
                    : 1.0f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);

            root.transform.localPosition =
                Vector3.Lerp(
                    root.transform.localPosition,
                    position,
                    t
                );

            root.transform.localRotation =
                Quaternion.Slerp(
                    root.transform.localRotation,
                    rotation,
                    t
                );
        }



        private void HandleStudyChanged()
        {
            RefreshTask();


            if (studySession != null &&
                studySession.IsRunning &&
                studySession.ViewShown)
            {
                ShowTab(Tab.Task);
            }
        }




        private void RefreshAll()
        {
            RefreshViews();

            RefreshTask();

            RefreshPanels();

            ShowTab(currentTab);
        }


        // =========================================================
        // BUILD
        // =========================================================

        private void Build()
        {
            root =
                new GameObject(
                    "XRHandMenuCanvas",
                    typeof(RectTransform)
                );

            // Lives in the controller's parent space (Camera Offset under
            // the scaled XR Origin): local units are real metres, and a
            // pinned panel stays put in the room, also when the table is
            // resized.
            root.transform.SetParent(
                anchor.parent != null
                    ? anchor.parent
                    : anchor,
                false
            );

            root.transform.localScale =
                Vector3.one * metersPerUnit;


            Canvas canvas =
                root.AddComponent<Canvas>();

            canvas.renderMode =
                RenderMode.WorldSpace;

            canvas.worldCamera =
                eventCamera;

            canvas.sortingOrder =
                sortingOrder;

            ((RectTransform)root.transform).sizeDelta =
                sizeUnits;

            root.AddComponent<TrackedDeviceGraphicRaycaster>();


            // Grab bar: grabbing it pins the menu where it is let go.
            XRGrabbable grabbable =
                XRGrabbable.AddPanelHandle(
                    (RectTransform)root.transform
                );

            grabbable.Grabbed +=
                () =>
                {
                    if (!pinned)
                    {
                        SetPinned(true);
                    }
                };


            // Opaque: in linear colour space even 6 % transparency
            // lets the bright map show clearly through the panel.
            Image background =
                RuntimeUi.CreatePanel(
                    "Panel",
                    root.transform,
                    Opaque(RuntimeUi.PanelColor)
                );

            RuntimeUi.Stretch(
                background.rectTransform,
                0.0f
            );

            background.rectTransform.offsetMin =
                Vector2.zero;

            background.rectTransform.offsetMax =
                Vector2.zero;

            RuntimeUi.Vertical(
                background.gameObject,
                14,
                10.0f
            );


            Transform panel =
                background.transform;


            // ----- header: current view + Close -----

            RectTransform headerRow =
                RuntimeUi.CreateRect(
                    "Header",
                    panel
                );

            RuntimeUi.Horizontal(
                headerRow.gameObject,
                0,
                6.0f
            );

            headerView =
                RuntimeUi.CreateText(
                    headerRow,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );

            RuntimeUi.Layout(
                headerView.gameObject,
                flexibleWidth: 1.0f
            );

            RuntimeUi.CreateButton(
                headerRow,
                "Close",
                () =>
                {
                    SetPinned(false);

                    SetVisible(false);
                },
                90.0f,
                36.0f,
                RuntimeUi.SmallSize
            );


            // ----- tabs -----

            RectTransform tabRow =
                RuntimeUi.CreateRect(
                    "Tabs",
                    panel
                );

            RuntimeUi.Horizontal(
                tabRow.gameObject,
                0,
                6.0f
            ).childForceExpandWidth =
                true;

            foreach (Tab tab in System.Enum.GetValues(typeof(Tab)))
            {
                Tab captured =
                    tab;

                Button button =
                    RuntimeUi.CreateButton(
                        tabRow,
                        tab.ToString(),
                        () => ShowTab(captured),
                        -1.0f,
                        ButtonHeight,
                        RuntimeUi.BodySize
                    );

                RuntimeUi.Layout(
                    button.gameObject,
                    -1.0f,
                    ButtonHeight,
                    1.0f
                );

                tabButtons[tab] =
                    button;
            }


            // ----- pages -----

            tabPages[Tab.Views] =
                BuildViewsPage(panel);

            tabPages[Tab.Task] =
                BuildTaskPage(panel);

            tabPages[Tab.Panels] =
                BuildPanelsPage(panel);


            // Opens on the look-at-wrist gesture; always on the wrist
            // without it.
            root.SetActive(
                !autoShow
            );
        }


        private RectTransform CreatePage(
            Transform parent,
            string name
        )
        {
            RectTransform page =
                RuntimeUi.CreateRect(
                    name,
                    parent
                );

            RuntimeUi.Vertical(
                page.gameObject,
                0,
                8.0f
            );

            RuntimeUi.Layout(
                page.gameObject,
                flexibleHeight: 1.0f
            );

            return page;
        }


        private RectTransform CreateScroll(
            Transform parent,
            out ScrollRect scroll
        )
        {
            RectTransform content =
                RuntimeUi.CreateScrollView(
                    parent,
                    out scroll
                );

            RuntimeUi.Layout(
                scroll.gameObject,
                flexibleHeight: 1.0f
            );

            // Thumbstick scrolling gives small deltas.
            scroll.scrollSensitivity =
                60.0f;

            return content;
        }



        private GameObject BuildViewsPage(
            Transform panel
        )
        {
            RectTransform page =
                CreatePage(panel, "ViewsPage");


            viewsStatus =
                RuntimeUi.CreateText(
                    page,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );


            viewsList =
                CreateScroll(
                    page,
                    out _
                );


            RuntimeUi.CreateButton(
                page,
                "Clear table",
                () =>
                {
                    if (toolbar != null)
                    {
                        toolbar.ClearTable();
                    }
                    else
                    {
                        visualizationSwitcher?.Clear();
                    }
                },
                -1.0f,
                ButtonHeight
            );


            return page.gameObject;
        }




        private GameObject BuildTaskPage(
            Transform panel
        )
        {
            RectTransform page =
                CreatePage(panel, "TaskPage");


            taskHeader =
                RuntimeUi.CreateText(
                    page,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.AccentColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );


            // Question, answer buttons and confidence scroll together;
            // the action buttons below always stay in reach.
            RectTransform content =
                CreateScroll(
                    page,
                    out _
                );

            taskQuestion =
                RuntimeUi.CreateText(
                    content,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.TextColor
                );

            taskBody =
                RuntimeUi.CreateRect(
                    "TaskBody",
                    content
                );

            RuntimeUi.Vertical(
                taskBody.gameObject,
                0,
                6.0f
            );


            taskActions =
                RuntimeUi.CreateRect(
                    "TaskActions",
                    page
                );

            RuntimeUi.Horizontal(
                taskActions.gameObject,
                0,
                6.0f
            ).childForceExpandWidth =
                true;


            taskStatus =
                RuntimeUi.CreateText(
                    page,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );


            return page.gameObject;
        }




        private GameObject BuildPanelsPage(
            Transform panel
        )
        {
            RectTransform page =
                CreatePage(panel, "PanelsPage");


            RuntimeUi.CreateText(
                page,
                "Show or hide a panel. Move a panel by its  Grab  bar.",
                RuntimeUi.SmallSize,
                RuntimeUi.MutedColor
            );


            foreach (XRTablePanel target in Panels())
            {
                XRTablePanel captured =
                    target;

                Button button =
                    RuntimeUi.CreateButton(
                        page,
                        target.PanelName,
                        () => captured.SetVisible(!captured.IsVisible),
                        -1.0f,
                        ButtonHeight
                    );

                panelButtons.Add(
                    (button, target)
                );
            }


            RuntimeUi.CreateButton(
                page,
                "Reset panels",
                ResetPanels,
                -1.0f,
                ButtonHeight
            );

            RuntimeUi.CreateButton(
                page,
                "Restart tutorial",
                () =>
                {
                    SetPinned(false);

                    SetVisible(false);

                    tutorial?.Restart();
                },
                -1.0f,
                ButtonHeight
            );

            handsButton =
                RuntimeUi.CreateButton(
                    page,
                    string.Empty,
                    ToggleHands,
                    -1.0f,
                    ButtonHeight
                );


            return page.gameObject;
        }


        // =========================================================
        // PANELS
        // =========================================================

        private void HandlePanelVisibility(
            bool visible
        )
        {
            RefreshPanels();
        }


        private void RefreshPanels()
        {
            foreach ((Button button, XRTablePanel target) in panelButtons)
            {
                RuntimeUi.SetButton(
                    button,
                    target.PanelName + (target.IsVisible ? ": shown" : ": hidden"),
                    target.IsVisible
                        ? RuntimeUi.ActiveButtonColor
                        : RuntimeUi.ButtonColor
                );
            }


            if (handsButton != null)
            {
                bool on =
                    inputModeSwitch != null &&
                    inputModeSwitch.HandsEnabled;

                RuntimeUi.SetButton(
                    handsButton,
                    on ? "Hand tracking: on (experimental)" : "Hand tracking: off (experimental)",
                    on ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor
                );

                handsButton.gameObject.SetActive(
                    inputModeSwitch != null
                );
            }
        }


        /// <summary>Toolbar, panels and this menu back to their own places.</summary>
        private void ResetPanels()
        {
            toolbar?.ResetPlacement();

            foreach (XRTablePanel panel in Panels())
            {
                panel.ResetPlacement();
            }

            SetPinned(false);

            studySession?.LogEvent("vr_reset_panels");
        }


        private void ToggleHands()
        {
            if (inputModeSwitch == null)
            {
                return;
            }

            inputModeSwitch.HandsEnabled =
                !inputModeSwitch.HandsEnabled;

            RefreshPanels();
        }


        private static Color Opaque(
            Color color
        )
        {
            color.a =
                1.0f;

            return color;
        }


        // =========================================================
        // TABS
        // =========================================================

        private void ShowTab(
            Tab tab
        )
        {
            if (tab == Tab.Task &&
                studySession == null)
            {
                tab = Tab.Views;
            }


            if (tab != currentTab)
            {
                studySession?.LogEvent(
                    "vr_menu_tab",
                    ("tab", tab.ToString())
                );
            }


            currentTab =
                tab;


            foreach (KeyValuePair<Tab, GameObject> page in tabPages)
            {
                page.Value.SetActive(
                    page.Key == tab
                );
            }


            foreach (KeyValuePair<Tab, Button> button in tabButtons)
            {
                button.Value.GetComponent<Image>().color =
                    button.Key == tab
                        ? RuntimeUi.ActiveButtonColor
                        : RuntimeUi.ButtonColor;
            }


            if (tabButtons.TryGetValue(
                    Tab.Task,
                    out Button taskButton
                ))
            {
                taskButton.gameObject.SetActive(
                    studySession != null
                );
            }
        }



        // =========================================================
        // VIEWS
        // =========================================================

        private void RefreshViews()
        {
            if (viewsList == null)
            {
                return;
            }


            if (visualizationSwitcher == null)
            {
                viewsStatus.text =
                    "No VisualizationSwitcher in the scene.";

                headerView.text =
                    string.Empty;

                return;
            }


            IReadOnlyList<VisualizationOption> options =
                visualizationSwitcher.Options;


            if (viewButtons.Count != options.Count)
            {
                RuntimeUi.ClearChildren(
                    viewsList
                );

                viewButtons.Clear();


                for (int i = 0; i < options.Count; i++)
                {
                    int index =
                        i;

                    Button button =
                        RuntimeUi.CreateButton(
                            viewsList,
                            string.Empty,
                            () => visualizationSwitcher.Apply(index),
                            -1.0f,
                            ButtonHeight
                        );

                    RuntimeUi.SetAlignment(
                        button.GetComponentInChildren<TMP_Text>(),
                        TextAnchor.MiddleLeft
                    );

                    viewButtons.Add(
                        button
                    );
                }
            }


            for (int i = 0; i < options.Count; i++)
            {
                bool active =
                    i == visualizationSwitcher.ActiveIndex;

                bool pending =
                    i == visualizationSwitcher.PendingIndex;

                RuntimeUi.SetButton(
                    viewButtons[i],
                    options[i].DisplayName +
                    (pending ? "   …" : string.Empty),
                    active || pending
                        ? RuntimeUi.ActiveButtonColor
                        : RuntimeUi.ButtonColor
                );

                viewButtons[i].interactable =
                    XRFeatureLock.Allows(XRFeature.ChangeView);
            }


            string shown =
                visualizationSwitcher.ActiveIndex >= 0 &&
                visualizationSwitcher.ActiveIndex < options.Count
                    ? options[visualizationSwitcher.ActiveIndex].DisplayName
                    : null;


            if (!string.IsNullOrEmpty(visualizationSwitcher.LastError))
            {
                viewsStatus.text =
                    RuntimeUi.Colorize(
                        visualizationSwitcher.LastError,
                        RuntimeUi.ErrorColor
                    );
            }
            else if (visualizationSwitcher.IsApplying)
            {
                viewsStatus.text =
                    "Applying…";
            }
            else if (!visualizationSwitcher.IsLoaded)
            {
                viewsStatus.text =
                    "Loading catalog…";
            }
            else
            {
                viewsStatus.text =
                    shown != null
                        ? "Shown: " + shown
                        : "No visualization shown.";
            }


            headerView.text =
                shown != null
                    ? "View: " + RuntimeUi.Colorize(shown, RuntimeUi.TextColor)
                    : "No view shown";
        }



        // =========================================================
        // TASK
        // =========================================================

        /// <summary>
        /// The study session, driven from VR: Start (the first task is the
        /// training), Show view, answer options, confidence 1–5, Submit /
        /// Done and Skip. The facilitator's PC panel drives the same
        /// StudySession, so either can be used.
        /// </summary>
        private void RefreshTask()
        {
            if (taskQuestion == null)
            {
                return;
            }


            RuntimeUi.ClearChildren(taskBody);

            RuntimeUi.ClearChildren(taskActions);

            taskStatus.text =
                studySession != null
                    ? studySession.Status ?? string.Empty
                    : string.Empty;


            if (studySession == null)
            {
                taskHeader.text =
                    "Study";

                taskQuestion.text =
                    "No study session in this scene.";

                return;
            }


            if (!studySession.IsRunning)
            {
                taskHeader.text =
                    "Study";

                bool finished =
                    studySession.ScenarioIndex > 0;

                taskQuestion.text =
                    finished
                        ? "Session finished. Thank you!"
                        : "Press Start to begin. The first task is a short " +
                          "training to try the controls; then the tasks follow.";

                if (!finished)
                {
                    TaskButton("Start", studySession.StartSession);
                }

                return;
            }


            StudyScenario scenario =
                studySession.Current;

            if (scenario == null)
            {
                taskHeader.text =
                    "Study";

                taskQuestion.text =
                    "Session finished. Thank you!";

                return;
            }


            taskHeader.text =
                $"Task {studySession.ScenarioIndex + 1} / " +
                $"{studySession.ScenarioCount}" +
                (scenario.training ? "  ·  training" : string.Empty) +
                (string.IsNullOrEmpty(scenario.title) ? string.Empty : "  ·  " + scenario.title);


            if (!studySession.ViewShown)
            {
                taskQuestion.text =
                    RuntimeUi.Colorize(
                        "Press Show view to load the view of this task and start the timer.",
                        RuntimeUi.MutedColor
                    );

                TaskButton("Show view", studySession.ShowCurrentView);

                TaskButton("Skip", studySession.Skip);

                return;
            }


            taskQuestion.text =
                scenario.QuestionFor(
                    studySession.Condition
                );


            bool hasOptions =
                scenario.options != null &&
                scenario.options.Length > 0;

            if (hasOptions)
            {
                foreach (string option in scenario.options)
                {
                    string captured =
                        option;

                    Button button =
                        RuntimeUi.CreateButton(
                            taskBody,
                            option,
                            () => studySession.SelectOption(captured),
                            -1.0f,
                            ButtonHeight
                        );

                    RuntimeUi.SetAlignment(
                        button.GetComponentInChildren<TMP_Text>(),
                        TextAnchor.MiddleLeft
                    );

                    if (option == studySession.SelectedOption)
                    {
                        RuntimeUi.SetButton(
                            button,
                            option,
                            RuntimeUi.ActiveButtonColor
                        );
                    }
                }


                RuntimeUi.CreateText(
                    taskBody,
                    "How sure are you? (1 = not at all, 5 = very)",
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );

                RectTransform confidenceRow =
                    RuntimeUi.CreateRect(
                        "Confidence",
                        taskBody
                    );

                RuntimeUi.Horizontal(
                    confidenceRow.gameObject,
                    0,
                    6.0f
                ).childForceExpandWidth =
                    true;

                for (int value = 1; value <= 5; value++)
                {
                    int captured =
                        value;

                    Button button =
                        RuntimeUi.CreateButton(
                            confidenceRow,
                            value.ToString(),
                            () => studySession.SetConfidence(captured),
                            -1.0f,
                            ButtonHeight
                        );

                    RuntimeUi.Layout(
                        button.gameObject,
                        -1.0f,
                        ButtonHeight,
                        1.0f
                    );

                    if (value == studySession.Confidence)
                    {
                        RuntimeUi.SetButton(
                            button,
                            value.ToString(),
                            RuntimeUi.ActiveButtonColor
                        );
                    }
                }
            }
            else
            {
                RuntimeUi.CreateText(
                    taskBody,
                    "Explore freely and say what you notice. Press Done when you are ready.",
                    RuntimeUi.BodySize,
                    RuntimeUi.MutedColor
                );
            }


            TaskButton(hasOptions ? "Submit" : "Done", studySession.Submit);

            TaskButton("Skip", studySession.Skip);
        }


        private void TaskButton(
            string label,
            UnityEngine.Events.UnityAction onClick
        )
        {
            Button button =
                RuntimeUi.CreateButton(
                    taskActions,
                    label,
                    onClick,
                    -1.0f,
                    ButtonHeight
                );

            RuntimeUi.Layout(
                button.gameObject,
                -1.0f,
                ButtonHeight,
                1.0f
            );
        }
    }
}
