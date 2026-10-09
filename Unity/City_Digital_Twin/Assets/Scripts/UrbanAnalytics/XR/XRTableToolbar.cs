using System.Collections.Generic;
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
    /// side and tilted toward them. Every button changes the table:
    ///
    ///   DATA   &lt; View · current view · View &gt; · Clear table
    ///   CLICK  Select · Copy · Compare · Remove copies · Buildings on/off
    ///   TABLE  Smaller · Bigger · Turn left · Turn right · Bring here
    ///
    /// "Clear table" removes the data (view), the selection and the
    /// copies: only the plain buildings stay. The CLICK row chooses what
    /// a trigger click on the table does (XRClickTools). Panels, help
    /// and settings are on the wrist menu, not here.
    ///
    /// Buttons the tutorial has not taught yet are greyed out
    /// (XRFeatureLock); the tutorial can outline one
    /// (<see cref="Highlight"/>).
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

        private const float RowLabelWidth =
            82.0f;


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

        [Tooltip("Canvas size in units (900 × 222 at 1.2 mm = 1.08 × 0.27 m).")]
        [SerializeField]
        private Vector2 sizeUnits =
            new Vector2(900.0f, 222.0f);

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
        private XRClickTools clickTools;

        [SerializeField]
        private StudySession studySession;


        // =========================================================
        // RUNTIME
        // =========================================================

        private sealed class ToolbarButton
        {
            public Button Button;

            public XRFeature Feature;
        }


        private GameObject root;

        private TMP_Text viewName;

        private Button buildingsButton;

        private readonly Dictionary<XRClickTool, Button> toolButtons =
            new Dictionary<XRClickTool, Button>();

        private readonly Dictionary<string, ToolbarButton> buttons =
            new Dictionary<string, ToolbarButton>();

        private bool hasBearing;

        private float bearing;

        private float targetBearing;

        private XRGrabbable grabbable;


        /// <summary>"Clear table" was pressed (tutorial).</summary>
        public event System.Action Cleared;


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

            if (clickTools == null)
            {
                clickTools =
                    FindFirstObjectByType<XRClickTools>();
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

            if (clickTools != null)
            {
                clickTools.ToolChanged +=
                    HandleToolChanged;
            }

            XRFeatureLock.Changed +=
                RefreshLabels;
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

            if (clickTools != null)
            {
                clickTools.ToolChanged -=
                    HandleToolChanged;
            }

            XRFeatureLock.Changed -=
                RefreshLabels;
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

            root.AddComponent<TrackedDeviceGraphicRaycaster>();

            // Grab bar: carry the toolbar anywhere; it then stays there
            // until Reset panels (wrist menu).
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


            // ----- DATA: what is shown on the table -----

            Transform data =
                CreateRow(background.transform, "Data", "DATA");

            AddButton(data, "< View", 100.0f, XRFeature.ChangeView, () => visualizationSwitcher?.Step(-1));

            viewName =
                RuntimeUi.CreateText(
                    data,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleCenter,
                    FontStyles.Bold
                );

            RuntimeUi.Layout(
                viewName.gameObject,
                260.0f,
                ButtonHeight
            );

            AddButton(data, "View >", 100.0f, XRFeature.ChangeView, () => visualizationSwitcher?.Step(1));

            AddButton(data, "Clear table", 150.0f, XRFeature.ClearTable, ClearTable);


            // ----- CLICK: what the trigger does on the table -----

            Transform click =
                CreateRow(background.transform, "Click", "CLICK");

            toolButtons[XRClickTool.Select] =
                AddButton(click, "Select", 100.0f, XRFeature.Select, () => clickTools?.SetTool(XRClickTool.Select));

            toolButtons[XRClickTool.Copy] =
                AddButton(click, "Copy", 100.0f, XRFeature.CopyTool, () => clickTools?.SetTool(XRClickTool.Copy));

            toolButtons[XRClickTool.Compare] =
                AddButton(click, "Compare", 120.0f, XRFeature.CompareTool, () => clickTools?.SetTool(XRClickTool.Compare));

            AddButton(click, "Remove copies", 160.0f, XRFeature.CopyTool, () => copies?.RemoveAll());

            buildingsButton =
                AddButton(click, "Buildings", 170.0f, XRFeature.ClearTable, ToggleBuildings);


            // ----- TABLE: size and position -----

            Transform table =
                CreateRow(background.transform, "Table", "TABLE");

            AddButton(table, "Smaller", 110.0f, XRFeature.ResizeTable, () => Resize(1.0f / resizeStep));

            AddButton(table, "Bigger", 110.0f, XRFeature.ResizeTable, () => Resize(resizeStep));

            AddButton(table, "Turn left", 120.0f, XRFeature.ResizeTable, () => Turn(turnStepDegrees));

            AddButton(table, "Turn right", 120.0f, XRFeature.ResizeTable, () => Turn(-turnStepDegrees));

            AddButton(table, "Bring here", 130.0f, XRFeature.ResizeTable, BringHere);
        }


        private static Transform CreateRow(
            Transform parent,
            string name,
            string label
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
                TextAnchor.MiddleLeft;

            RuntimeUi.Layout(
                row.gameObject,
                -1.0f,
                ButtonHeight
            );

            TMP_Text text =
                RuntimeUi.CreateText(
                    row,
                    label,
                    RuntimeUi.SmallSize,
                    RuntimeUi.AccentColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            RuntimeUi.Layout(
                text.gameObject,
                RowLabelWidth,
                ButtonHeight
            );

            return row;
        }


        private Button AddButton(
            Transform row,
            string label,
            float width,
            XRFeature feature,
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

            // Locked by the tutorial: clearly dimmed (the default disabled
            // tint is barely visible on the dark buttons).
            ColorBlock colors =
                button.colors;

            colors.disabledColor =
                new Color(0.35f, 0.35f, 0.35f, 0.45f);

            button.colors =
                colors;

            Outline outline =
                button.gameObject.AddComponent<Outline>();

            outline.effectColor =
                RuntimeUi.AccentColor;

            outline.effectDistance =
                new Vector2(5.0f, -5.0f);

            outline.enabled =
                false;

            buttons[label] =
                new ToolbarButton
                {
                    Button = button,
                    Feature = feature
                };

            return button;
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        /// <summary>Back to automatic placement after the user moved it.</summary>
        public void ResetPlacement()
        {
            grabbable?.ResetPlacement();

            hasBearing =
                false;
        }


        /// <summary>
        /// Outlines the buttons with these labels (e.g. "View &gt;") to
        /// point the user at them; none removes the outlines.
        /// </summary>
        public void Highlight(
            params string[] labels
        )
        {
            var set =
                new HashSet<string>(labels ?? System.Array.Empty<string>());

            foreach (KeyValuePair<string, ToolbarButton> entry in buttons)
            {
                entry.Value.Button.GetComponent<Outline>().enabled =
                    set.Contains(entry.Key);
            }
        }


        /// <summary>
        /// Removes everything shown on the table: the view (data), the
        /// selection and the copies. Only the plain buildings stay.
        /// </summary>
        public void ClearTable()
        {
            visualizationSwitcher?.Clear();

            interactionManager?.ClearSelection();

            copies?.RemoveAll();

            Debug.Log(
                "XRTableToolbar: table cleared (no view, no selection, no copies).",
                this
            );

            studySession?.LogEvent(
                "vr_clear_table"
            );

            Cleared?.Invoke();
        }


        // =========================================================
        // ACTIONS
        // =========================================================

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


        private void HandleToolChanged(
            XRClickTool tool
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
                        : "No data shown";
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


            if (clickTools != null)
            {
                foreach (KeyValuePair<XRClickTool, Button> entry in toolButtons)
                {
                    entry.Value.GetComponent<Image>().color =
                        entry.Key == clickTools.Tool
                            ? RuntimeUi.ActiveButtonColor
                            : RuntimeUi.ButtonColor;
                }
            }


            // Not taught yet by the tutorial: greyed out.
            foreach (ToolbarButton entry in buttons.Values)
            {
                bool allowed =
                    XRFeatureLock.Allows(entry.Feature);

                entry.Button.interactable =
                    allowed;

                TMP_Text label =
                    entry.Button.GetComponentInChildren<TMP_Text>();

                if (label != null)
                {
                    label.alpha =
                        allowed ? 1.0f : 0.3f;
                }
            }
        }
    }
}
