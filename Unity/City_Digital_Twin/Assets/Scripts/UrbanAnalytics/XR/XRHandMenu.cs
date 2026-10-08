using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Study;
using UrbanAnalytics.Visualization;
using UrbanAnalytics.Visualization.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// The VR panel: a "tablet" held above the left controller,
    /// operated with the other hand's ray (trigger = click, thumbstick
    /// = scroll). It follows the user around the table, so it is
    /// always readable.
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
    ///
    /// Built from code with RuntimeUi and the desktop UI's formatting
    /// helpers, so values read exactly as on the desktop. Sized in real
    /// metres: the canvas sits under the controller, which is under
    /// the scaled XR Origin, so the rig scale is inherited.
    ///
    /// The left primary button (X) shows/hides the panel.
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
            Task
        }


        private const float ButtonHeight =
            44.0f;


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Placement (real metres, controller space)")]
        [Tooltip("The panel is parented here (left controller).")]
        [SerializeField]
        private Transform anchor;

        [SerializeField]
        private Vector3 localPositionMeters =
            new Vector3(0.0f, 0.10f, 0.06f);

        [Tooltip("Tilt so the panel faces the eyes when the hand is raised.")]
        [SerializeField]
        private Vector3 localEulerAngles =
            new Vector3(35.0f, 0.0f, 0.0f);

        [Tooltip("Real metres per canvas unit (0.0006 = 0.6 mm per unit).")]
        [SerializeField]
        [Range(0.0002f, 0.002f)]
        private float metersPerUnit =
            0.0006f;

        [Tooltip("Canvas size in units (520 × 640 at 0.6 mm = 31 × 38 cm).")]
        [SerializeField]
        private Vector2 sizeUnits =
            new Vector2(520.0f, 640.0f);

        [Tooltip(
            "Draws after other transparent objects (the selection " +
            "highlight is Transparent+10 and would show through)."
        )]
        [SerializeField]
        private int sortingOrder =
            110;


        [Header("Input")]
        [Tooltip("Shows/hides the panel.")]
        [SerializeField]
        private string toggleBinding =
            "<XRController>{LeftHand}/primaryButton";

        [SerializeField]
        private bool startVisible =
            true;


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


        public void SetVisible(
            bool visible
        )
        {
            if (root != null)
            {
                root.SetActive(
                    visible
                );
            }
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
            SetVisible(
                !IsVisible
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

            root.transform.SetParent(
                anchor,
                false
            );

            root.transform.localPosition =
                localPositionMeters;

            root.transform.localEulerAngles =
                localEulerAngles;

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


            root.SetActive(
                startVisible
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
                    "Select an area, then Info → Copy → A / B.",
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
                        (open ? "▾ " : "▸ ") + section.Title,
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
                scenario.question;


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
