using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Controls built into the table edge (like a bezel), on the user's
    /// side and tilted toward them, so everything works without
    /// controller buttons (hand tracking: poke with a finger or pinch
    /// with the ray; controllers: ray + trigger):
    ///
    ///   &lt; View · current view · View &gt; · Buildings on/off · Copy ·
    ///   Clear · Remove copies
    ///   Smaller · Bigger · Turn left · Turn right · Bring here ·
    ///   Board · Menu
    ///   Hands on/off · Reset panels · Help
    ///
    /// Lives in the scaled XR Origin's space (real metres). It follows
    /// the user around the table, but only once they are more than
    /// <see cref="repositionDegrees"/> away from its side.
    /// </summary>
    public sealed class XRTableToolbar :
        MonoBehaviour
    {
        private const float ButtonHeight =
            60.0f;


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Placement (real metres)")]
        [SerializeField]
        private TabletopRig tabletopRig;

        [SerializeField]
        private Camera viewCamera;

        [Tooltip("Distance between the table edge and the toolbar's centre.")]
        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float edgeSetbackMeters =
            0.10f;

        [Tooltip("Height of the toolbar's centre above the table top.")]
        [SerializeField]
        [Range(-0.2f, 0.5f)]
        private float heightAboveTableMeters =
            0.04f;

        [Tooltip("0 = upright, 90 = lying flat (facing up).")]
        [SerializeField]
        [Range(0.0f, 90.0f)]
        private float tiltDegrees =
            50.0f;

        [SerializeField]
        [Range(10.0f, 120.0f)]
        private float repositionDegrees =
            50.0f;

        [SerializeField]
        [Range(0.5f, 10.0f)]
        private float moveSharpness =
            3.0f;

        [Tooltip("Real metres per canvas unit (0.0012 = 1.2 mm).")]
        [SerializeField]
        [Range(0.0005f, 0.004f)]
        private float metersPerUnit =
            0.0012f;

        [Tooltip("Canvas size in units (960 × 222 at 1.2 mm = 1.15 × 0.27 m).")]
        [SerializeField]
        private Vector2 sizeUnits =
            new Vector2(960.0f, 222.0f);

        [SerializeField]
        private int sortingOrder =
            105;


        [Header("Actions")]
        [SerializeField]
        [Range(1.02f, 1.5f)]
        private float resizeStep =
            1.15f;

        [SerializeField]
        [Range(5.0f, 90.0f)]
        private float turnStepDegrees =
            30.0f;


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private InteractionManager interactionManager;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private XRSelectionCopies copies;

        [SerializeField]
        private XRTableBoard board;

        [SerializeField]
        private XRHandMenu menu;

        [SerializeField]
        private XRInputModeSwitch inputModeSwitch;

        [SerializeField]
        private StudySession studySession;


        // =========================================================
        // RUNTIME
        // =========================================================

        private GameObject root;

        private TMP_Text viewName;

        private Button buildingsButton;

        private Button boardButton;

        private Button handsButton;

        private bool hasBearing;

        private float bearing;

        private float targetBearing;

        private XRGrabbable grabbable;


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

            if (viewCamera == null)
            {
                viewCamera =
                    Camera.main;
            }

            if (interactionManager == null)
            {
                interactionManager =
                    FindFirstObjectByType<InteractionManager>();
            }

            if (visualizationSwitcher == null)
            {
                visualizationSwitcher =
                    FindFirstObjectByType<VisualizationSwitcher>();
            }

            if (copies == null)
            {
                copies =
                    FindFirstObjectByType<XRSelectionCopies>();
            }

            if (board == null)
            {
                board =
                    FindFirstObjectByType<XRTableBoard>();
            }

            if (menu == null)
            {
                menu =
                    FindFirstObjectByType<XRHandMenu>();
            }

            if (inputModeSwitch == null)
            {
                inputModeSwitch =
                    FindFirstObjectByType<XRInputModeSwitch>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }


            if (tabletopRig == null ||
                tabletopRig.Origin == null)
            {
                Debug.LogError(
                    "XRTableToolbar: needs a TabletopRig with an XR Origin.",
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
            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed +=
                    RefreshLabels;
            }

            if (interactionManager != null)
            {
                interactionManager.BuildingsSelectableChanged +=
                    HandleBuildingsChanged;
            }
        }


        private void Start()
        {
            RefreshLabels();
        }


        private void OnDisable()
        {
            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed -=
                    RefreshLabels;
            }

            if (interactionManager != null)
            {
                interactionManager.BuildingsSelectableChanged -=
                    HandleBuildingsChanged;
            }
        }


        private void LateUpdate()
        {
            if (root == null)
            {
                return;
            }


            bool show =
                tabletopRig.IsPlaced &&
                viewCamera != null;

            if (root.activeSelf != show)
            {
                root.SetActive(show);
            }

            if (show &&
                (grabbable == null || !grabbable.IsUserPlaced))
            {
                Place(
                    Time.unscaledDeltaTime
                );
            }
        }


        // =========================================================
        // PLACEMENT
        // =========================================================

        private void Place(
            float deltaTime
        )
        {
            Transform space =
                root.transform.parent;

            Vector3 center =
                tabletopRig.TableCenterRig;

            Vector3 toUser =
                space.InverseTransformPoint(
                    viewCamera.transform.position
                ) - center;

            toUser.y =
                0.0f;


            if (toUser.sqrMagnitude > 1e-4f)
            {
                float user =
                    Mathf.Atan2(toUser.x, toUser.z) * Mathf.Rad2Deg;

                if (!hasBearing)
                {
                    bearing =
                        user;

                    targetBearing =
                        user;

                    hasBearing =
                        true;
                }
                else if (Mathf.Abs(Mathf.DeltaAngle(targetBearing, user)) >
                         repositionDegrees)
                {
                    targetBearing =
                        user;
                }
            }


            bearing =
                Mathf.LerpAngle(
                    bearing,
                    targetBearing,
                    1.0f - Mathf.Exp(-moveSharpness * deltaTime)
                );


            var toward =
                new Vector3(
                    Mathf.Sin(bearing * Mathf.Deg2Rad),
                    0.0f,
                    Mathf.Cos(bearing * Mathf.Deg2Rad)
                );


            root.transform.localPosition =
                center +
                toward * (tabletopRig.EdgeDistanceMeters(toward) + edgeSetbackMeters) +
                Vector3.up * heightAboveTableMeters;


            // Read from its -Z side: forward points away from the user,
            // tilted down so the face looks up toward them.
            float tilt =
                tiltDegrees * Mathf.Deg2Rad;

            root.transform.localRotation =
                Quaternion.LookRotation(
                    -toward * Mathf.Cos(tilt) + Vector3.down * Mathf.Sin(tilt),
                    Vector3.up
                );
        }


        // =========================================================
        // BUILD
        // =========================================================

        private void Build()
        {
            root =
                new GameObject(
                    "XRTableToolbarCanvas",
                    typeof(RectTransform)
                );

            root.SetActive(false);

            root.transform.SetParent(
                tabletopRig.Origin,
                false
            );

            root.transform.localScale =
                Vector3.one * metersPerUnit;


            Canvas canvas =
                root.AddComponent<Canvas>();

            canvas.renderMode =
                RenderMode.WorldSpace;

            canvas.worldCamera =
                viewCamera;

            canvas.sortingOrder =
                sortingOrder;

            ((RectTransform)root.transform).sizeDelta =
                sizeUnits;

            // Ray (trigger / pinch) and poke (fingertip) both work.
            root.AddComponent<TrackedDeviceGraphicRaycaster>();

            // "Move" handle: grab it to put the panel anywhere; it then
            // stays there until ResetPlacement.
            grabbable =
                XRGrabbable.AddPanelHandle(
                    (RectTransform)root.transform
                );


            Color panelColor =
                RuntimeUi.PanelColor;

            panelColor.a =
                1.0f;

            Image background =
                RuntimeUi.CreatePanel(
                    "Panel",
                    root.transform,
                    panelColor
                );

            RuntimeUi.Stretch(background.rectTransform, 0.0f);

            background.rectTransform.offsetMin =
                Vector2.zero;

            background.rectTransform.offsetMax =
                Vector2.zero;

            RuntimeUi.Vertical(
                background.gameObject,
                10,
                8.0f
            );


            // ----- row 1: views and selection -----

            Transform row1 =
                CreateRow(background.transform, "ViewsAndSelection");

            Button(row1, "< View", 100.0f, () => visualizationSwitcher?.Step(-1));

            viewName =
                RuntimeUi.CreateText(
                    row1,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleCenter,
                    FontStyles.Bold
                );

            RuntimeUi.Layout(
                viewName.gameObject,
                200.0f,
                ButtonHeight
            );

            Button(row1, "View >", 100.0f, () => visualizationSwitcher?.Step(1));

            buildingsButton =
                Button(row1, string.Empty, 150.0f, ToggleBuildings);

            Button(row1, "Copy", 90.0f, () => copies?.CopySelected());

            Button(row1, "Clear", 90.0f, () => interactionManager?.ClearSelection());

            Button(row1, "Remove copies", 125.0f, () => copies?.RemoveAll());


            // ----- row 2: the table -----

            Transform row2 =
                CreateRow(background.transform, "Table");

            Button(row2, "Smaller", 110.0f, () => Resize(1.0f / resizeStep));

            Button(row2, "Bigger", 110.0f, () => Resize(resizeStep));

            Button(row2, "Turn left", 120.0f, () => Turn(turnStepDegrees));

            Button(row2, "Turn right", 120.0f, () => Turn(-turnStepDegrees));

            Button(row2, "Bring here", 130.0f, BringHere);

            boardButton =
                Button(row2, "Board", 110.0f, ToggleBoard);

            Button(row2, "Menu", 110.0f, ToggleMenu);


            // ----- row 3: input and layout -----

            Transform row3 =
                CreateRow(background.transform, "InputAndLayout");

            handsButton =
                Button(row3, string.Empty, 160.0f, ToggleHands);

            Button(row3, "Reset panels", 150.0f, ResetPanels);

            Button(row3, "Help", 110.0f, () => menu?.PinInFront("Help"));
        }


        private static Transform CreateRow(
            Transform parent,
            string name
        )
        {
            RectTransform row =
                RuntimeUi.CreateRect(
                    name,
                    parent
                );

            RuntimeUi.Horizontal(
                row.gameObject,
                0,
                8.0f
            ).childAlignment =
                TextAnchor.MiddleCenter;

            RuntimeUi.Layout(
                row.gameObject,
                -1.0f,
                ButtonHeight
            );

            return row;
        }


        private static Button Button(
            Transform row,
            string label,
            float width,
            UnityEngine.Events.UnityAction onClick
        )
        {
            Button button =
                RuntimeUi.CreateButton(
                    row,
                    label,
                    onClick,
                    width,
                    ButtonHeight,
                    RuntimeUi.BodySize
                );

            RuntimeUi.Layout(
                button.gameObject,
                width,
                ButtonHeight
            );

            return button;
        }


        // =========================================================
        // ACTIONS
        // =========================================================

        /// <summary>Back to automatic placement after the user moved it.</summary>
        public void ResetPlacement()
        {
            grabbable?.ResetPlacement();

            hasBearing =
                false;
        }


        private void ToggleBuildings()
        {
            if (interactionManager == null)
            {
                return;
            }


            interactionManager.BuildingsSelectable =
                !interactionManager.BuildingsSelectable;

            studySession?.LogEvent(
                "vr_buildings_selectable",
                ("selectable", interactionManager.BuildingsSelectable)
            );
        }


        private void Resize(
            float factor
        )
        {
            float length =
                tabletopRig.SetTableLength(
                    tabletopRig.TableLengthMeters * factor
                );

            studySession?.LogEvent(
                "vr_table_resize",
                ("lengthMeters", length),
                ("worldUnitsPerMeter", tabletopRig.WorldUnitsPerMeter)
            );
        }


        private void Turn(
            float degrees
        )
        {
            tabletopRig.MoveTable(
                tabletopRig.TableCenterRig,
                tabletopRig.TableYawRig + degrees
            );

            LogMove("toolbar turn");
        }


        private void BringHere()
        {
            Transform origin =
                tabletopRig.Origin;

            tabletopRig.BringTableTo(
                origin.InverseTransformPoint(viewCamera.transform.position),
                origin.InverseTransformDirection(viewCamera.transform.forward)
            );

            // The toolbar follows straight away.
            hasBearing =
                false;

            LogMove("toolbar recall");
        }


        private void ToggleBoard()
        {
            if (board == null)
            {
                return;
            }

            board.SetVisible(!board.IsVisible);

            RefreshLabels();
        }


        private void ToggleHands()
        {
            if (inputModeSwitch == null)
            {
                return;
            }

            inputModeSwitch.HandsEnabled =
                !inputModeSwitch.HandsEnabled;

            RefreshLabels();
        }


        /// <summary>Board, toolbar and wrist menu back to automatic placement.</summary>
        private void ResetPanels()
        {
            ResetPlacement();

            board?.ResetPlacement();

            if (menu != null && menu.IsPinned)
            {
                menu.SetPinned(false);
            }

            studySession?.LogEvent("vr_reset_panels");
        }


        private void ToggleMenu()
        {
            if (menu == null)
            {
                return;
            }

            if (menu.IsPinned)
            {
                menu.SetPinned(false);
            }
            else
            {
                menu.PinInFront("Info");
            }
        }


        private void LogMove(
            string how
        )
        {
            Vector3 center =
                tabletopRig.TableCenterRig;

            studySession?.LogEvent(
                "vr_table_move",
                ("how", how),
                ("centerX", center.x),
                ("centerZ", center.z),
                ("yawDegrees", tabletopRig.TableYawRig)
            );
        }


        // =========================================================
        // LABELS
        // =========================================================

        private void HandleBuildingsChanged(
            bool selectable
        )
        {
            RefreshLabels();
        }


        private void RefreshLabels()
        {
            if (viewName != null)
            {
                viewName.text =
                    visualizationSwitcher != null &&
                    visualizationSwitcher.ActiveIndex >= 0 &&
                    visualizationSwitcher.ActiveIndex < visualizationSwitcher.Options.Count
                        ? visualizationSwitcher.Options[visualizationSwitcher.ActiveIndex].DisplayName
                        : "No view";
            }


            if (buildingsButton != null &&
                interactionManager != null)
            {
                bool on =
                    interactionManager.BuildingsSelectable;

                RuntimeUi.SetButton(
                    buildingsButton,
                    on ? "Buildings: on" : "Buildings: off",
                    on ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor
                );
            }


            if (handsButton != null &&
                inputModeSwitch != null)
            {
                bool on =
                    inputModeSwitch.HandsEnabled;

                RuntimeUi.SetButton(
                    handsButton,
                    on ? "Hands: on" : "Hands: off",
                    on ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor
                );
            }


            if (boardButton != null &&
                board != null)
            {
                RuntimeUi.SetButton(
                    boardButton,
                    "Board",
                    board.IsVisible ? RuntimeUi.ActiveButtonColor : RuntimeUi.ButtonColor
                );
            }
        }
    }
}
