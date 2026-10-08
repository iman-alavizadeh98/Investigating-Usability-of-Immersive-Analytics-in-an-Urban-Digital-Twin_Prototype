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
using UrbanAnalytics.Visualization;
using UrbanAnalytics.Visualization.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// The VR panel: a wrist menu on the left wrist, operated with the
    /// other hand's ray (trigger = click, thumbstick = scroll).
    ///
    /// It stands just above the left wrist, facing the eyes, and opens
    /// when the user raises the wrist and looks at it (like reading a
    /// watch); it closes shortly after the wrist is lowered, but stays
    /// open while a controller ray is on it. X pins it where it is
    /// (open, fixed in the room) and X again sends it back to the wrist.
    /// With <see cref="AutoShow"/> off (XR Device Simulator) it is
    /// always shown on the wrist.
    ///
    /// Tabs:
    ///   Info    — the selection: values of the current view (large),
    ///             detail sections, actions (select cell, copy to A/B,
    ///             clear). Opens automatically on a new selection.
    ///   Views   — the visualization list (same as the desktop list).
    ///   Legend  — one legend per encoded variable.
    ///   Compare — slots A/B: both block views, value table A, B, B − A.
    ///   Task    — the current study question (read only; the
    ///             facilitator runs the session on the PC). Opens
    ///             automatically when a scenario view is shown.
    ///   Help    — the VR controls (no keyboard or mouse wording).
    ///
    /// Built from code with RuntimeUi and the desktop UI's formatting
    /// helpers, so values read exactly as on the desktop. Sized in real
    /// metres: the canvas sits under the controller, which is under
    /// the scaled XR Origin, so the rig scale is inherited.
    ///
    /// Visibility, pin and tab changes are logged as study events
    /// (vr_menu, vr_menu_tab) while a session runs.
    /// </summary>
    public sealed class XRHandMenu :
        MonoBehaviour
    {
        private enum Tab
        {
            Info,
            Views,
            Legend,
            Compare,
            Task,
            Help
        }


        /// <summary>The VR controls, shown on the Help tab.</summary>
        public const string ControlsText =
            "<b>Right controller</b>\n" +
            "• Point at the table: see the values\n" +
            "• Trigger: select · on empty table: clear\n" +
            "• Grip + trigger: select the area of a building\n" +
            "• A: pop-out copy of the selection\n" +
            "• Grip with the ray on a copy: grab it, move it, let go\n" +
            "• B: clear the selection\n" +
            "• Thumbstick left / right: previous / next view\n" +
            "• Thumbstick up / down: bigger / smaller table\n" +
            "\n" +
            "<b>Left controller</b>\n" +
            "• Look at your wrist: open this menu\n" +
            "• X: pin the menu in place / back to the wrist\n" +
            "• Y: show / hide the board at the table\n" +
            "• Grip (hold): grab the table, move and turn it\n" +
            "• Thumbstick left / right: turn the table\n" +
            "• Thumbstick press: bring the table to you\n" +
            "• Trigger: point with the left hand instead\n" +
            "\n" +
            "<b>Hands (controllers put down)</b>\n" +
            "• Point, then pinch (thumb + index): select\n" +
            "• Pinch with the ray on a copy: grab it, move it, let go\n" +
            "• Left fist: grab the table, move and turn it\n" +
            "• Poke a button with your finger\n" +
            "• Look at your left wrist: open this menu\n" +
            "\n" +
            "<b>Toolbar at the table edge</b>\n" +
            "• Views, buildings on/off, copy, clear, table size,\n" +
            "  turn, bring here, board, menu\n" +
            "\n" +
            "<b>On a panel</b>\n" +
            "• Point + trigger or pinch: press · thumbstick: scroll";


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
        private InteractionManager interactionManager;

        [SerializeField]
        private VisualizationManager visualizationManager;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private ComparisonManager comparisonManager;

        [SerializeField]
        private StudySession studySession;

        [Tooltip("Keeps the panel open while a controller ray is on it.")]
        [SerializeField]
        private XRControllerPointer pointer;

        [Tooltip(
            "Labelled controller diagram on the Help tab " +
            "(Assets/Textures/UrbanAnalytics/VR/vr_controls.png, drawn by " +
            "Tools/make_vr_controls_image.ps1). Text only when empty."
        )]
        [SerializeField]
        private Texture2D controlsImage;


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


        // Info
        private TMP_Text infoTitle;

        private TMP_Text infoSubtitle;

        private GameObject infoActions;

        private Button selectCellButton;

        private RectTransform infoContent;

        private ScrollRect infoScroll;

        private readonly HashSet<string> expandedSections =
            new HashSet<string>();


        // Views
        private TMP_Text viewsStatus;

        private RectTransform viewsList;

        private readonly List<Button> viewButtons =
            new List<Button>();


        // Legend
        private TMP_Text legendPlaceholder;

        private RectTransform legendContainer;


        // Compare
        private readonly RawImage[] slotImages =
            new RawImage[2];

        private readonly TMP_Text[] slotTitles =
            new TMP_Text[2];

        private TMP_Text comparePlaceholder;

        private GameObject compareBody;

        private RectTransform compareTable;


        // Task
        private TMP_Text taskHeader;

        private TMP_Text taskQuestion;

        private TMP_Text taskOptions;


        /// <summary>The canvas root (for tests and the scene builder).</summary>
        public GameObject Root =>
            root;


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

            if (visualizationManager == null)
            {
                visualizationManager =
                    FindFirstObjectByType<VisualizationManager>();
            }

            if (visualizationSwitcher == null)
            {
                visualizationSwitcher =
                    FindFirstObjectByType<VisualizationSwitcher>();
            }

            if (comparisonManager == null)
            {
                comparisonManager =
                    FindFirstObjectByType<ComparisonManager>();
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


            if (interactionManager != null)
            {
                interactionManager.SelectionChanged +=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed +=
                    RefreshInfo;
            }

            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed +=
                    RefreshViews;
            }

            if (visualizationManager != null)
            {
                visualizationManager.LegendsChanged +=
                    HandleLegendsChanged;

                HandleLegendsChanged(
                    visualizationManager.ActiveLegends
                );
            }

            if (comparisonManager != null)
            {
                comparisonManager.Changed +=
                    RefreshCompare;
            }

            if (studySession != null)
            {
                studySession.Changed +=
                    HandleStudyChanged;
            }


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


            if (interactionManager != null)
            {
                interactionManager.SelectionChanged -=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed -=
                    RefreshInfo;
            }

            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed -=
                    RefreshViews;
            }

            if (visualizationManager != null)
            {
                visualizationManager.LegendsChanged -=
                    HandleLegendsChanged;
            }

            if (comparisonManager != null)
            {
                comparisonManager.Changed -=
                    RefreshCompare;
            }

            if (studySession != null)
            {
                studySession.Changed -=
                    HandleStudyChanged;
            }
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


        /// <summary>Opens a tab by name (Info, Views, Legend, Compare, Task).</summary>
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


            if (tracked &&
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


        private void HandleSelectionChanged(
            EntityReference entity
        )
        {
            RefreshInfo();


            if (entity.IsValid)
            {
                ShowTab(Tab.Info);
            }
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
            RefreshInfo();

            RefreshViews();

            RefreshCompare();

            RefreshTask();

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


            // ----- header: current view -----

            headerView =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
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

            tabPages[Tab.Info] =
                BuildInfoPage(panel);

            tabPages[Tab.Views] =
                BuildViewsPage(panel);

            tabPages[Tab.Legend] =
                BuildLegendPage(panel);

            tabPages[Tab.Compare] =
                BuildComparePage(panel);

            tabPages[Tab.Task] =
                BuildTaskPage(panel);

            tabPages[Tab.Help] =
                BuildHelpPage(panel);


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


        private GameObject BuildInfoPage(
            Transform panel
        )
        {
            RectTransform page =
                CreatePage(panel, "InfoPage");


            infoTitle =
                RuntimeUi.CreateText(
                    page,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            infoSubtitle =
                RuntimeUi.CreateText(
                    page,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );


            RectTransform actions =
                RuntimeUi.CreateRect(
                    "Actions",
                    page
                );

            RuntimeUi.Horizontal(
                actions.gameObject,
                0,
                6.0f
            );

            infoActions =
                actions.gameObject;

            selectCellButton =
                RuntimeUi.CreateButton(
                    actions,
                    "Select cell",
                    SelectCellOfSelection,
                    130.0f,
                    ButtonHeight
                );

            RuntimeUi.CreateButton(
                actions,
                "Copy → A",
                () => CopySelection(0),
                118.0f,
                ButtonHeight
            );

            RuntimeUi.CreateButton(
                actions,
                "Copy → B",
                () => CopySelection(1),
                118.0f,
                ButtonHeight
            );

            RuntimeUi.CreateButton(
                actions,
                "Clear",
                () => interactionManager?.ClearSelection(),
                100.0f,
                ButtonHeight
            );


            infoContent =
                CreateScroll(
                    page,
                    out infoScroll
                );


            return page.gameObject;
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
                "Clear view",
                () => visualizationSwitcher?.Clear(),
                -1.0f,
                ButtonHeight
            );


            return page.gameObject;
        }


        private GameObject BuildLegendPage(
            Transform panel
        )
        {
            RectTransform page =
                CreatePage(panel, "LegendPage");


            legendPlaceholder =
                RuntimeUi.CreateText(
                    page,
                    "No visualization shown.",
                    RuntimeUi.BodySize,
                    RuntimeUi.MutedColor
                );


            RectTransform content =
                CreateScroll(
                    page,
                    out _
                );


            legendContainer =
                RuntimeUi.CreateRect(
                    "Legends",
                    content
                );

            RuntimeUi.Vertical(
                legendContainer.gameObject,
                4,
                12.0f
            );


            // A second legend view that fills this page; it shares
            // the visualization manager with the desktop legend.
            var legendObject =
                new GameObject("XRLegendView");

            legendObject.SetActive(false);

            legendObject.transform.SetParent(
                transform,
                false
            );

            LegendStackView legendView =
                legendObject.AddComponent<LegendStackView>();

            legendView.Container =
                legendContainer;

            legendObject.SetActive(true);


            return page.gameObject;
        }


        private GameObject BuildComparePage(
            Transform panel
        )
        {
            RectTransform page =
                CreatePage(panel, "ComparePage");


            comparePlaceholder =
                RuntimeUi.CreateText(
                    page,
                    "Select an area, then press A (or Info → Copy → A / B).",
                    RuntimeUi.BodySize,
                    RuntimeUi.MutedColor
                );


            RectTransform body =
                RuntimeUi.CreateRect(
                    "Body",
                    page
                );

            RuntimeUi.Vertical(
                body.gameObject,
                0,
                8.0f
            );

            RuntimeUi.Layout(
                body.gameObject,
                flexibleHeight: 1.0f
            );

            compareBody =
                body.gameObject;


            RectTransform views =
                RuntimeUi.CreateRect(
                    "Views",
                    body
                );

            RuntimeUi.Horizontal(
                views.gameObject,
                0,
                8.0f
            ).childForceExpandWidth =
                true;


            for (int i = 0; i < 2; i++)
            {
                int slot =
                    i;

                RectTransform column =
                    RuntimeUi.CreateRect(
                        "Slot" + i,
                        views
                    );

                RuntimeUi.Vertical(
                    column.gameObject,
                    0,
                    4.0f
                );

                RuntimeUi.Layout(
                    column.gameObject,
                    flexibleWidth: 1.0f
                );

                slotTitles[i] =
                    RuntimeUi.CreateText(
                        column,
                        string.Empty,
                        RuntimeUi.SmallSize,
                        RuntimeUi.TextColor
                    );

                RectTransform imageRect =
                    RuntimeUi.CreateRect(
                        "View",
                        column
                    );

                RawImage image =
                    imageRect.gameObject.AddComponent<RawImage>();

                RuntimeUi.Layout(
                    image.gameObject,
                    -1.0f,
                    190.0f
                );

                // Drag on a view rotates both copies (linked views).
                imageRect.gameObject
                    .AddComponent<ComparisonViewInput>()
                    .Comparison =
                    comparisonManager;

                slotImages[i] =
                    image;

                RuntimeUi.CreateButton(
                    column,
                    "Clear",
                    () => comparisonManager?.ClearSlot(slot),
                    -1.0f,
                    ButtonHeight
                );
            }


            compareTable =
                CreateScroll(
                    body,
                    out _
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

            taskQuestion =
                RuntimeUi.CreateText(
                    page,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.TextColor
                );

            taskOptions =
                RuntimeUi.CreateText(
                    page,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.TextColor
                );


            return page.gameObject;
        }


        private GameObject BuildHelpPage(
            Transform panel
        )
        {
            RectTransform page =
                CreatePage(panel, "HelpPage");


            RectTransform content =
                CreateScroll(
                    page,
                    out _
                );


            if (controlsImage != null)
            {
                RectTransform imageRect =
                    RuntimeUi.CreateRect(
                        "ControlsImage",
                        content
                    );

                imageRect.gameObject
                    .AddComponent<RawImage>()
                    .texture =
                    controlsImage;

                // Full panel width (minus padding and the scrollbar),
                // keeping the image's aspect ratio.
                float width =
                    sizeUnits.x - 50.0f;

                RuntimeUi.Layout(
                    imageRect.gameObject,
                    width,
                    width * controlsImage.height / controlsImage.width
                );
            }


            RuntimeUi.CreateText(
                content,
                ControlsText,
                RuntimeUi.BodySize,
                RuntimeUi.TextColor
            );


            return page.gameObject;
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
        // INFO
        // =========================================================

        private void RefreshInfo()
        {
            if (infoContent == null ||
                interactionManager == null)
            {
                return;
            }


            RuntimeUi.ClearChildren(
                infoContent
            );


            EntityReference selected =
                interactionManager.Selected;


            if (!selected.IsValid)
            {
                infoTitle.text =
                    "Nothing selected";

                infoSubtitle.text =
                    "Point at the table and press the trigger.\n" +
                    "Grip + trigger selects the area under a building.";

                infoActions.SetActive(
                    false
                );

                return;
            }


            EntityInfo info =
                interactionManager.BuildInfo(
                    selected
                );


            infoTitle.text =
                info.Title;

            infoSubtitle.text =
                info.Subtitle;

            infoActions.SetActive(
                true
            );

            selectCellButton.gameObject.SetActive(
                selected.Kind == EntityKind.Building &&
                selected.HasUnit
            );


            foreach (EntityInfoRow row in info.Highlights)
            {
                DesktopInteractionUI.CreateHighlightRow(
                    infoContent,
                    row
                );
            }


            foreach (EntityInfoSection section in info.Sections)
            {
                string key =
                    section.Title;

                bool open =
                    !section.Collapsed ||
                    expandedSections.Contains(key);

                Button header =
                    RuntimeUi.CreateButton(
                        infoContent,
                        (open ? "- " : "+ ") + section.Title,
                        () =>
                        {
                            if (!expandedSections.Remove(key))
                            {
                                expandedSections.Add(key);
                            }

                            RefreshInfo();
                        },
                        -1.0f,
                        ButtonHeight
                    );

                RuntimeUi.SetAlignment(
                    header.GetComponentInChildren<TMP_Text>(),
                    TextAnchor.MiddleLeft
                );

                if (!open)
                {
                    continue;
                }

                foreach (EntityInfoRow row in section.Rows)
                {
                    DesktopInteractionUI.CreateValueRow(
                        infoContent,
                        row
                    );
                }
            }


            if (!string.IsNullOrEmpty(info.Footer))
            {
                RuntimeUi.CreateText(
                    infoContent,
                    info.Footer,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );
            }


            infoScroll.verticalNormalizedPosition =
                1.0f;
        }


        private void SelectCellOfSelection()
        {
            if (interactionManager != null &&
                interactionManager.Selected.TryGetUnit(
                    out EntityReference cell
                ))
            {
                interactionManager.Select(
                    cell
                );
            }
        }


        private void CopySelection(
            int slot
        )
        {
            if (comparisonManager != null &&
                interactionManager != null &&
                interactionManager.HasSelection)
            {
                comparisonManager.CopyToSlot(
                    slot,
                    interactionManager.Selected
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


        private void HandleLegendsChanged(
            IReadOnlyList<VisualizationLegendInfo> legends
        )
        {
            if (legendPlaceholder != null)
            {
                legendPlaceholder.gameObject.SetActive(
                    legends == null ||
                    legends.Count == 0
                );
            }
        }


        // =========================================================
        // COMPARE
        // =========================================================

        private void RefreshCompare()
        {
            if (compareTable == null)
            {
                return;
            }


            bool any =
                comparisonManager != null &&
                comparisonManager.Slots != null &&
                comparisonManager.Slots.Count >= 2 &&
                comparisonManager.HasAnyFilled;


            comparePlaceholder.gameObject.SetActive(
                !any
            );

            compareBody.SetActive(
                any
            );


            if (!any)
            {
                return;
            }


            IReadOnlyList<ComparisonManager.Slot> slots =
                comparisonManager.Slots;


            for (int i = 0; i < 2 && i < slots.Count; i++)
            {
                ComparisonManager.Slot slot =
                    slots[i];

                slotTitles[i].text =
                    $"<b>{slot.Label}</b>  " +
                    (slot.IsFilled
                        ? slot.Info != null
                            ? slot.Info.Title
                            : slot.Entity.Id
                        : RuntimeUi.Colorize(
                            "empty",
                            RuntimeUi.MutedColor
                        ));

                slotImages[i].texture =
                    slot.IsFilled
                        ? slot.Texture
                        : null;

                slotImages[i].color =
                    slot.IsFilled
                        ? Color.white
                        : new Color(0.0f, 0.0f, 0.0f, 0.25f);
            }


            RuntimeUi.ClearChildren(
                compareTable
            );


            if (slots.Count < 2)
            {
                return;
            }


            ComparisonManager.Slot a =
                slots[0];

            ComparisonManager.Slot b =
                slots[1];


            CreateCompareRow(
                null,
                "<b>A</b>",
                "<b>B</b>",
                "<b>B − A</b>",
                RuntimeUi.MutedColor
            );


            // Rows in A's order, then any only B has.
            var keys =
                new List<string>();

            var templates =
                new Dictionary<string, EntityInfoRow>(
                    System.StringComparer.Ordinal
                );

            foreach (ComparisonManager.Slot slot in new[] { a, b })
            {
                if (slot.Info == null)
                {
                    continue;
                }

                foreach (EntityInfoRow row in slot.Info.DataRows)
                {
                    if (templates.TryAdd(row.Key, row))
                    {
                        keys.Add(row.Key);
                    }
                }
            }


            foreach (string key in keys)
            {
                EntityInfoRow rowA =
                    null;

                EntityInfoRow rowB =
                    null;

                a.Info?.TryGetDataRow(key, out rowA);

                b.Info?.TryGetDataRow(key, out rowB);

                EntityInfoRow template =
                    templates[key];

                string label =
                    (template.IsEncoded
                        ? RuntimeUi.Colorize("● ", RuntimeUi.AccentColor)
                        : "   ") +
                    template.Label;

                CreateCompareRow(
                    label,
                    DesktopInteractionUI.CompareCell(rowA, a.IsFilled),
                    DesktopInteractionUI.CompareCell(rowB, b.IsFilled),
                    DesktopInteractionUI.Difference(rowA, rowB),
                    RuntimeUi.TextColor
                );
            }
        }


        /// <summary>
        /// Narrow-panel table row: the label on its own line, then
        /// A | B | B − A in three equal columns (the desktop row puts
        /// all four side by side, which wraps badly at this width).
        /// </summary>
        private void CreateCompareRow(
            string label,
            string valueA,
            string valueB,
            string difference,
            Color color
        )
        {
            RectTransform block =
                RuntimeUi.CreateRect(
                    "Row",
                    compareTable
                );

            RuntimeUi.Vertical(
                block.gameObject,
                0,
                0.0f
            );


            if (!string.IsNullOrEmpty(label))
            {
                RuntimeUi.CreateText(
                    block,
                    label,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );
            }


            RectTransform values =
                RuntimeUi.CreateRect(
                    "Values",
                    block
                );

            RuntimeUi.Horizontal(
                values.gameObject,
                0,
                8.0f
            ).childForceExpandWidth =
                true;


            foreach (string cell in new[] { valueA, valueB, difference })
            {
                TMP_Text text =
                    RuntimeUi.CreateText(
                        values,
                        cell,
                        RuntimeUi.BodySize,
                        color,
                        TextAnchor.MiddleRight
                    );

                RuntimeUi.Layout(
                    text.gameObject,
                    -1.0f,
                    -1.0f,
                    1.0f
                );
            }
        }


        // =========================================================
        // TASK
        // =========================================================

        private void RefreshTask()
        {
            if (taskQuestion == null)
            {
                return;
            }


            if (studySession == null ||
                !studySession.IsRunning)
            {
                taskHeader.text =
                    "Study";

                taskQuestion.text =
                    studySession != null &&
                    studySession.ScenarioIndex >= 0
                        ? "Session finished. Thank you!"
                        : "Waiting for the facilitator to start.";

                taskOptions.text =
                    string.Empty;

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

                taskOptions.text =
                    string.Empty;

                return;
            }


            taskHeader.text =
                $"Task {studySession.ScenarioIndex + 1} / " +
                $"{studySession.ScenarioCount}" +
                (scenario.training ? "  ·  training" : string.Empty);


            if (!studySession.ViewShown)
            {
                taskQuestion.text =
                    RuntimeUi.Colorize(
                        "The next task is being prepared…",
                        RuntimeUi.MutedColor
                    );

                taskOptions.text =
                    string.Empty;

                return;
            }


            taskQuestion.text =
                scenario.QuestionFor(
                    studySession.Condition
                );


            bool hasOptions =
                scenario.options != null &&
                scenario.options.Length > 0;

            taskOptions.text =
                hasOptions
                    ? "• " + string.Join("\n• ", scenario.options) +
                      "\n\n" + RuntimeUi.Colorize(
                          "Answer aloud.",
                          RuntimeUi.MutedColor
                      )
                    : RuntimeUi.Colorize(
                        "Explore freely; tell the facilitator what you notice.",
                        RuntimeUi.MutedColor
                    );
        }
    }
}
