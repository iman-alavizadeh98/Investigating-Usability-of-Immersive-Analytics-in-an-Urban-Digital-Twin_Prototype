using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UrbanAnalytics.Interaction.UI
{
    /// <summary>
    /// Desktop screen-space UI for the interaction features, built
    /// in code at startup:
    /// - Visualizations (top left, collapsible): switch at runtime;
    /// - hover tooltip at the cursor;
    /// - Selection (right, under the legend): every value of the
    ///   selected entity, its percentile rank, copy/focus buttons;
    /// - Compare (bottom left): the two block copies side by side
    ///   with a value table (A, B, B − A);
    /// - help line (top centre, H toggles).
    ///
    /// Sharpness and size: text is TextMeshPro (SDF), and the
    /// canvas uses a pixel scale factor = UI scale ×
    /// clamp(min(width / referenceWidth, height / referenceHeight),
    /// minimumAutoScale), so the UI grows on large screens but
    /// never shrinks into unreadable sizes on small windows; the
    /// panels then adapt their size to the free space. [ and ] change the UI
    /// scale (remembered per machine). The comparison views are
    /// rendered at exactly their on-screen pixel size.
    ///
    /// The view only reads InteractionManager, ComparisonManager
    /// and VisualizationSwitcher; it holds no analytical state.
    /// </summary>
    public sealed class DesktopInteractionUI :
        MonoBehaviour
    {
        private const string HelpText =
            "Click: select   Alt+click: select cell   " +
            "Drag: pan   Right-drag: orbit   Wheel: zoom   " +
            "F: focus   Home: overview   C: copy to compare   " +
            "1–9: visualizations   0: clear   Esc: deselect   " +
            "[ ]: UI size   H: hide help";


        private const string UiScalePreference =
            "UrbanAnalytics.DesktopUiScale";


        // Comparison view size limits in canvas units; the actual
        // size follows the free width (see UpdateLayout).
        private const float MaximumViewWidth = 440.0f;

        private const float MinimumViewWidth = 200.0f;

        private const float ViewAspect = 0.64f;

        private const float VisualizationPanelWidth = 360.0f;

        private const float SelectionPanelWidth = 460.0f;

        private const float Margin = 16.0f;


        [Header("Dependencies")]
        [SerializeField]
        private InteractionManager interactionManager;

        [SerializeField]
        private ComparisonManager comparisonManager;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;


        [Header("Layout")]
        [SerializeField]
        private int sortingOrder =
            10;

        [Tooltip(
            "Screen size (pixels) at which the UI is drawn at " +
            "scale 1. The automatic scale is the smaller of " +
            "width/referenceWidth and height/referenceHeight."
        )]
        [SerializeField]
        [Min(200.0f)]
        private float referenceWidth =
            1600.0f;

        [SerializeField]
        [Min(200.0f)]
        private float referenceHeight =
            900.0f;

        [Tooltip(
            "The automatic scale never goes below this, so text " +
            "stays readable in small windows (panels then shrink " +
            "or overlap instead)."
        )]
        [SerializeField]
        [Range(0.5f, 2.0f)]
        private float minimumAutoScale =
            0.75f;

        [Tooltip(
            "User multiplier on top of the automatic scale " +
            "([ and ] at runtime)."
        )]
        [SerializeField]
        [Range(0.5f, 2.5f)]
        private float uiScale =
            1.0f;

        [Tooltip(
            "GameObject name of the colour legend panel; the " +
            "selection panel starts below it."
        )]
        [SerializeField]
        private string legendPanelName =
            "LegendPanel";

        [SerializeField]
        private bool showHelp =
            true;


        // Canvas
        private CanvasScaler canvasScaler;
        private RectTransform canvasRect;
        private RectTransform legendPanel;
        private CanvasScaler legendScaler;

        // Visualizations
        private GameObject visualizationBody;
        private RectTransform visualizationButtons;
        private TMP_Text visualizationStatus;
        private Button visualizationToggle;
        private bool visualizationCollapsedByUser;
        private readonly List<Button> optionButtons =
            new List<Button>();

        // Tooltip
        private RectTransform tooltip;
        private TMP_Text tooltipText;

        // Selection
        private GameObject selectionPanel;
        private RectTransform selectionRect;
        private TMP_Text selectionTitle;
        private TMP_Text selectionSubtitle;
        private RectTransform selectionContent;
        private ScrollRect selectionScroll;
        private Button selectCellButton;

        // Detail sections the user opened (by title); kept across selections.
        private readonly HashSet<string> expandedSections =
            new HashSet<string>(System.StringComparer.Ordinal);

        // Compare
        private GameObject comparePanel;
        private RectTransform compareTable;
        private readonly SlotView[] slotViews =
            new SlotView[ComparisonManager.SlotCount];

        // Help
        private TMP_Text helpText;
        private RectTransform helpRect;
        private bool helpFits =
            true;

        // Panels resized by UpdateLayout
        private RectTransform visualizationRect;
        private RectTransform compareRect;


        private sealed class SlotView
        {
            public TMP_Text Title;

            public TMP_Text Caption;

            public RawImage View;

            public TMP_Text Placeholder;

            public Button ClearButton;

            public GameObject Column;

            public GameObject Frame;
        }


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


            if (interactionManager == null)
            {
                Debug.LogError(
                    "DesktopInteractionUI needs an " +
                    "InteractionManager in the scene.",
                    this
                );

                enabled =
                    false;
            }


            // Per-viewer convenience only; the UI works without it.
            try
            {
                uiScale =
                    PlayerPrefs.GetFloat(
                        UiScalePreference,
                        uiScale
                    );
            }
            catch (Exception)
            {
                // Keep the Inspector value.
            }
        }


        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.HoverChanged +=
                    HandleHoverChanged;

                interactionManager.SelectionChanged +=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed +=
                    RefreshSelection;
            }


            if (comparisonManager != null)
            {
                comparisonManager.Changed +=
                    RefreshComparison;
            }


            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed +=
                    RefreshVisualizations;
            }
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.HoverChanged -=
                    HandleHoverChanged;

                interactionManager.SelectionChanged -=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed -=
                    RefreshSelection;
            }


            if (comparisonManager != null)
            {
                comparisonManager.Changed -=
                    RefreshComparison;
            }


            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed -=
                    RefreshVisualizations;
            }
        }


        private void Start()
        {
            // Built in Start, not Awake: the comparison slots
            // (colours, textures) exist only after every Awake.
            BuildCanvas();


            RefreshVisualizations();

            RefreshSelection();

            RefreshComparison();
        }


        private void Update()
        {
            if (canvasRect == null)
            {
                return;
            }


            HandleKeys();

            UpdateScale();

            UpdateSelectionPanelTop();

            UpdateLayout();

            UpdateHelp();

            UpdateTooltipPosition();
        }


        // =========================================================
        // SCALE AND LAYOUT
        // =========================================================

        private void HandleKeys()
        {
            Keyboard keyboard =
                Keyboard.current;


            if (keyboard == null)
            {
                return;
            }


            if (keyboard.hKey.wasPressedThisFrame)
            {
                showHelp =
                    !showHelp;
            }


            if (keyboard.leftBracketKey.wasPressedThisFrame)
            {
                ChangeUiScale(
                    1.0f / 1.1f
                );
            }


            if (keyboard.rightBracketKey.wasPressedThisFrame)
            {
                ChangeUiScale(
                    1.1f
                );
            }
        }


        private void ChangeUiScale(
            float factor
        )
        {
            uiScale =
                Mathf.Clamp(
                    uiScale * factor,
                    0.5f,
                    2.5f
                );


            try
            {
                PlayerPrefs.SetFloat(
                    UiScalePreference,
                    uiScale
                );
            }
            catch (Exception)
            {
                // Not remembered; still applied.
            }
        }


        /// <summary>
        /// Pixel scale of the canvas. Constant-pixel-size scaling
        /// with an explicit factor keeps text on whole pixels and
        /// avoids the tiny UI that a 1920×1080 reference gives in
        /// a small window.
        /// </summary>
        private float CurrentScale =>
            uiScale *
            Mathf.Clamp(
                Mathf.Min(
                    Screen.width / referenceWidth,
                    Screen.height / referenceHeight
                ),
                minimumAutoScale,
                4.0f
            );


        private void UpdateScale()
        {
            float scale =
                CurrentScale;


            if (!Mathf.Approximately(
                    canvasScaler.scaleFactor,
                    scale
                ))
            {
                canvasScaler.scaleFactor =
                    scale;
            }


            // The colour legend lives on its own canvas (scene
            // authored, 1920×1080 reference); give it the same
            // scale so both UIs match.
            if (legendScaler == null &&
                legendPanel != null)
            {
                legendScaler =
                    legendPanel.GetComponentInParent<CanvasScaler>();
            }


            if (legendScaler != null)
            {
                legendScaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ConstantPixelSize;

                legendScaler.scaleFactor =
                    scale;
            }
        }


        /// <summary>
        /// The selection panel fills the right side from just
        /// below the colour legend (its own canvas) to the bottom.
        /// </summary>
        private void UpdateSelectionPanelTop()
        {
            if (selectionRect == null)
            {
                return;
            }


            if (legendPanel == null)
            {
                GameObject legend =
                    GameObject.Find(
                        legendPanelName
                    );


                if (legend != null)
                {
                    legendPanel =
                        legend.transform as RectTransform;
                }
            }


            float topPixels =
                16.0f;


            if (legendPanel != null &&
                legendPanel.gameObject.activeInHierarchy)
            {
                var corners =
                    new Vector3[4];


                // Overlay canvas: world corners are screen pixels.
                legendPanel.GetWorldCorners(
                    corners
                );


                topPixels =
                    Screen.height -
                    corners[0].y +
                    12.0f;
            }


            float scale =
                CurrentScale;


            selectionRect.offsetMax =
                new Vector2(
                    -16.0f,
                    -topPixels / scale
                );
        }


        /// <summary>
        /// Fits the panels to the screen (in canvas units):
        /// - the selection panel narrows on narrow screens;
        /// - the comparison views take the width left beside the
        ///   selection panel (MinimumViewWidth..MaximumViewWidth);
        /// - the compare panel takes the height left under the
        ///   visualization list;
        /// - the help line hides when it has no room between the
        ///   side panels (the picking status still shows);
        /// - the view textures follow the views' pixel size.
        /// </summary>
        private void UpdateLayout()
        {
            float scale =
                CurrentScale;

            float width =
                Screen.width / scale;

            float height =
                Screen.height / scale;


            // ----- selection panel width -----

            float selectionWidth =
                Mathf.Clamp(
                    width * 0.38f,
                    300.0f,
                    SelectionPanelWidth
                );


            selectionRect.offsetMin =
                new Vector2(
                    -Margin - selectionWidth,
                    Margin
                );


            // ----- comparison views -----

            float free =
                width -
                2.0f * Margin -
                (selectionPanel.activeSelf
                    ? selectionWidth + Margin
                    : 0.0f);


            // Panel = 2 views + padding (2 × 12) + spacing (12).
            float viewWidth =
                Mathf.Clamp(
                    (free - 36.0f) * 0.5f,
                    MinimumViewWidth,
                    MaximumViewWidth
                );

            float viewHeight =
                viewWidth * ViewAspect;


            for (int i = 0; i < slotViews.Length; i++)
            {
                RuntimeUi.Layout(
                    slotViews[i].Column,
                    preferredWidth: viewWidth
                );

                RuntimeUi.Layout(
                    slotViews[i].Frame,
                    viewWidth,
                    viewHeight
                );
            }


            // Collapse the visualization list automatically when
            // the compare panel needs its room (not if the user
            // collapsed it); reopen it when compare closes.
            if (!visualizationCollapsedByUser)
            {
                int optionCount =
                    visualizationSwitcher != null
                        ? visualizationSwitcher.Options.Count
                        : 0;


                float expandedHeight =
                    34.0f +
                    (optionCount + 1) * 36.0f +
                    70.0f;


                bool collapse =
                    comparePanel.activeSelf &&
                    height - expandedHeight - 3.0f * Margin <
                        viewHeight + 300.0f;


                SetVisualizationListVisible(
                    !collapse
                );
            }


            float visualizationBottom =
                Margin +
                visualizationRect.rect.height +
                Margin;


            // header + slot headers + captions + table header +
            // a useful table + padding.
            float wanted =
                viewHeight + 400.0f;


            compareRect.sizeDelta =
                new Vector2(
                    viewWidth * 2.0f + 36.0f,
                    Mathf.Clamp(
                        height - visualizationBottom - Margin,
                        viewHeight + 160.0f,
                        wanted
                    )
                );


            if (comparisonManager != null &&
                comparePanel.activeSelf)
            {
                comparisonManager.SetViewPixelSize(
                    Mathf.RoundToInt(viewWidth * scale),
                    Mathf.RoundToInt(viewHeight * scale)
                );
            }


            // ----- help line between the side panels -----

            float helpWidth =
                width -
                2.0f * (VisualizationPanelWidth + 2.0f * Margin);


            helpFits =
                helpWidth >= 360.0f;


            helpRect.sizeDelta =
                new Vector2(
                    Mathf.Clamp(
                        helpWidth,
                        300.0f,
                        980.0f
                    ),
                    helpRect.sizeDelta.y
                );
        }


        // =========================================================
        // CANVAS
        // =========================================================

        private void BuildCanvas()
        {
            var canvasObject =
                new GameObject(
                    "InteractionCanvas",
                    typeof(RectTransform)
                );


            canvasObject.transform.SetParent(
                transform,
                false
            );


            Canvas canvas =
                canvasObject.AddComponent<Canvas>();


            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder =
                sortingOrder;

            canvas.pixelPerfect =
                false;


            canvasScaler =
                canvasObject.AddComponent<CanvasScaler>();


            canvasScaler.uiScaleMode =
                CanvasScaler.ScaleMode.ConstantPixelSize;

            canvasScaler.scaleFactor =
                CurrentScale;


            canvasObject.AddComponent<GraphicRaycaster>();


            canvasRect =
                (RectTransform)canvasObject.transform;


            BuildVisualizationPanel();

            BuildSelectionPanel();

            BuildComparePanel();

            BuildHelp();

            BuildTooltip();
        }


        // =========================================================
        // VISUALIZATIONS PANEL
        // =========================================================

        private void BuildVisualizationPanel()
        {
            Image panel =
                RuntimeUi.CreatePanel(
                    "VisualizationsPanel",
                    canvasRect,
                    RuntimeUi.PanelColor
                );


            visualizationRect =
                (RectTransform)panel.transform;


            RuntimeUi.Anchor(
                visualizationRect,
                new Vector2(0.0f, 1.0f),
                new Vector2(Margin, -Margin),
                new Vector2(VisualizationPanelWidth, 0.0f)
            );


            RuntimeUi.Vertical(
                panel.gameObject,
                10,
                5.0f
            );


            panel.gameObject
                .AddComponent<ContentSizeFitter>()
                .verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;


            // ----- header: title (click to collapse) + UI size -----

            RectTransform header =
                RuntimeUi.CreateRect(
                    "Header",
                    panel.transform
                );


            RuntimeUi.Horizontal(
                header.gameObject,
                0,
                6.0f
            );


            visualizationToggle =
                RuntimeUi.CreateButton(
                    header,
                    "▼  Visualizations",
                    ToggleVisualizationList,
                    -1.0f,
                    34.0f,
                    RuntimeUi.HeadingSize
                );


            visualizationToggle.GetComponent<Image>().color =
                new Color(0.0f, 0.0f, 0.0f, 0.0f);


            TMP_Text toggleText =
                visualizationToggle.GetComponentInChildren<TMP_Text>();

            RuntimeUi.SetAlignment(
                toggleText,
                TextAnchor.MiddleLeft
            );

            toggleText.fontStyle =
                FontStyles.Bold;


            RuntimeUi.Layout(
                visualizationToggle.gameObject,
                -1.0f,
                34.0f,
                1.0f
            );


            RuntimeUi.CreateButton(
                header,
                "A−",
                () => ChangeUiScale(1.0f / 1.1f),
                40.0f,
                30.0f,
                RuntimeUi.SmallSize
            );


            RuntimeUi.CreateButton(
                header,
                "A+",
                () => ChangeUiScale(1.1f),
                40.0f,
                30.0f,
                RuntimeUi.SmallSize
            );


            // ----- body -----

            RectTransform body =
                RuntimeUi.CreateRect(
                    "Body",
                    panel.transform
                );


            RuntimeUi.Vertical(
                body.gameObject,
                0,
                4.0f
            );


            visualizationBody =
                body.gameObject;


            visualizationButtons =
                RuntimeUi.CreateRect(
                    "Options",
                    body
                );


            RuntimeUi.Vertical(
                visualizationButtons.gameObject,
                0,
                4.0f
            );


            Button clearButton =
                RuntimeUi.CreateButton(
                    body,
                    "0   Clear",
                    () => visualizationSwitcher?.Clear()
                );


            RuntimeUi.SetAlignment(
                clearButton.GetComponentInChildren<TMP_Text>(),
                TextAnchor.MiddleLeft
            );


            visualizationStatus =
                RuntimeUi.CreateText(
                    panel.transform,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );
        }


        private void ToggleVisualizationList()
        {
            bool show =
                !visualizationBody.activeSelf;


            visualizationCollapsedByUser =
                !show;


            SetVisualizationListVisible(
                show
            );
        }


        private void SetVisualizationListVisible(
            bool show
        )
        {
            if (visualizationBody.activeSelf == show)
            {
                return;
            }


            visualizationBody.SetActive(
                show
            );


            visualizationToggle
                .GetComponentInChildren<TMP_Text>()
                .text =
                    (show ? "▼" : "►") +
                    "  Visualizations";
        }


        private void RefreshVisualizations()
        {
            if (visualizationButtons == null)
            {
                return;
            }


            if (visualizationSwitcher == null)
            {
                visualizationStatus.text =
                    "No VisualizationSwitcher in the scene.";

                return;
            }


            IReadOnlyList<VisualizationOption> options =
                visualizationSwitcher.Options;


            if (optionButtons.Count != options.Count)
            {
                RuntimeUi.ClearChildren(
                    visualizationButtons
                );

                optionButtons.Clear();


                for (int i = 0; i < options.Count; i++)
                {
                    int index =
                        i;


                    Button button =
                        RuntimeUi.CreateButton(
                            visualizationButtons,
                            string.Empty,
                            () => visualizationSwitcher.Apply(index)
                        );


                    RuntimeUi.SetAlignment(
                        button.GetComponentInChildren<TMP_Text>(),
                        TextAnchor.MiddleLeft
                    );


                    optionButtons.Add(
                        button
                    );
                }
            }


            for (int i = 0; i < options.Count; i++)
            {
                string key =
                    i < 9
                        ? (i + 1).ToString(CultureInfo.InvariantCulture)
                        : " ";


                bool active =
                    i == visualizationSwitcher.ActiveIndex;

                bool pending =
                    i == visualizationSwitcher.PendingIndex;


                RuntimeUi.SetButton(
                    optionButtons[i],
                    $"{key}   {options[i].DisplayName}" +
                    (pending ? "   …" : string.Empty),
                    active || pending
                        ? RuntimeUi.ActiveButtonColor
                        : RuntimeUi.ButtonColor
                );
            }


            if (!string.IsNullOrEmpty(
                    visualizationSwitcher.LastError
                ))
            {
                visualizationStatus.text =
                    RuntimeUi.Colorize(
                        visualizationSwitcher.LastError,
                        RuntimeUi.ErrorColor
                    );
            }
            else if (visualizationSwitcher.IsApplying)
            {
                visualizationStatus.text =
                    "Applying…";
            }
            else if (!visualizationSwitcher.IsLoaded)
            {
                visualizationStatus.text =
                    "Loading catalog…";
            }
            else
            {
                visualizationStatus.text =
                    visualizationSwitcher.ActiveIndex >= 0
                        ? "Shown: " +
                          options[visualizationSwitcher.ActiveIndex]
                              .DisplayName
                        : "No visualization shown.";
            }
        }


        // =========================================================
        // TOOLTIP
        // =========================================================

        private void BuildTooltip()
        {
            Image panel =
                RuntimeUi.CreatePanel(
                    "Tooltip",
                    canvasRect,
                    new Color(0.04f, 0.04f, 0.06f, 0.94f)
                );


            panel.raycastTarget =
                false;


            tooltip =
                (RectTransform)panel.transform;


            tooltip.anchorMin =
                Vector2.zero;

            tooltip.anchorMax =
                Vector2.zero;

            tooltip.pivot =
                new Vector2(0.0f, 1.0f);


            RuntimeUi.Vertical(
                panel.gameObject,
                10,
                2.0f
            );


            ContentSizeFitter fitter =
                panel.gameObject.AddComponent<ContentSizeFitter>();


            fitter.horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;


            tooltipText =
                RuntimeUi.CreateText(
                    panel.transform,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.TextColor
                );


            tooltipText.textWrappingMode =
                TextWrappingModes.NoWrap;


            tooltip.gameObject.SetActive(
                false
            );
        }


        private void HandleHoverChanged(
            PickResult pick
        )
        {
            if (tooltip == null)
            {
                return;
            }


            if (!pick.IsValid)
            {
                tooltip.gameObject.SetActive(
                    false
                );

                return;
            }


            EntityInfo info =
                interactionManager.BuildInfo(
                    pick.Entity
                );


            var lines =
                new List<string>
                {
                    $"<b>{info.Title}</b>"
                };


            if (!string.IsNullOrEmpty(info.Subtitle))
            {
                lines.Add(
                    RuntimeUi.Colorize(
                        info.Subtitle,
                        RuntimeUi.MutedColor
                    )
                );
            }


            int shown =
                0;


            foreach (EntityInfoRow row in info.Highlights)
            {
                if (shown >= 5)
                {
                    break;
                }

                lines.Add(
                    $"{row.Label}: <b>{ValueWithUnit(row)}</b>"
                );

                shown++;
            }


            tooltipText.text =
                string.Join(
                    "\n",
                    lines
                );


            tooltip.gameObject.SetActive(
                true
            );
        }


        private void UpdateTooltipPosition()
        {
            if (tooltip == null ||
                !tooltip.gameObject.activeSelf ||
                Mouse.current == null)
            {
                return;
            }


            Vector2 screen =
                Mouse.current.position.ReadValue();


            float scale =
                CurrentScale;


            Vector2 size =
                tooltip.rect.size * scale;


            // Below-right of the cursor; flip at screen edges.
            float x =
                screen.x + 20.0f;

            float y =
                screen.y - 20.0f;


            if (x + size.x > Screen.width)
            {
                x =
                    screen.x - 20.0f - size.x;
            }


            if (y - size.y < 0.0f)
            {
                y =
                    screen.y + 20.0f + size.y;
            }


            tooltip.anchoredPosition =
                new Vector2(
                    x,
                    y
                ) / Mathf.Max(0.0001f, scale);
        }


        // =========================================================
        // SELECTION PANEL
        // =========================================================

        private void BuildSelectionPanel()
        {
            Image panel =
                RuntimeUi.CreatePanel(
                    "SelectionPanel",
                    canvasRect,
                    RuntimeUi.PanelColor
                );


            // Right edge, from below the legend (set every frame)
            // to the bottom.
            selectionRect =
                (RectTransform)panel.transform;

            selectionRect.anchorMin =
                new Vector2(1.0f, 0.0f);

            selectionRect.anchorMax =
                new Vector2(1.0f, 1.0f);

            selectionRect.pivot =
                new Vector2(1.0f, 1.0f);

            selectionRect.offsetMin =
                new Vector2(-16.0f - 460.0f, 16.0f);

            selectionRect.offsetMax =
                new Vector2(-16.0f, -190.0f);


            RuntimeUi.Vertical(
                panel.gameObject,
                12,
                8.0f
            );


            selectionPanel =
                panel.gameObject;


            // ----- header -----

            RectTransform header =
                RuntimeUi.CreateRect(
                    "Header",
                    panel.transform
                );


            RuntimeUi.Horizontal(
                header.gameObject,
                0,
                6.0f
            );


            RectTransform titles =
                RuntimeUi.CreateRect(
                    "Titles",
                    header
                );


            RuntimeUi.Vertical(
                titles.gameObject,
                0,
                0.0f
            );


            RuntimeUi.Layout(
                titles.gameObject,
                flexibleWidth: 1.0f
            );


            selectionTitle =
                RuntimeUi.CreateText(
                    titles,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );


            selectionSubtitle =
                RuntimeUi.CreateText(
                    titles,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );


            RuntimeUi.CreateButton(
                header,
                "×",
                () => interactionManager.ClearSelection(),
                34.0f,
                32.0f,
                RuntimeUi.HeadingSize
            );


            // ----- actions -----

            RectTransform actions =
                RuntimeUi.CreateRect(
                    "Actions",
                    panel.transform
                );


            RuntimeUi.Horizontal(
                actions.gameObject,
                0,
                6.0f
            );


            RuntimeUi.CreateButton(
                actions,
                "Copy → A",
                () => CopySelection(0),
                100.0f
            );


            RuntimeUi.CreateButton(
                actions,
                "Copy → B",
                () => CopySelection(1),
                100.0f
            );


            RuntimeUi.CreateButton(
                actions,
                "Focus (F)",
                () => interactionManager.FocusSelection(),
                96.0f
            );


            selectCellButton =
                RuntimeUi.CreateButton(
                    actions,
                    "Select cell",
                    SelectCellOfSelection,
                    110.0f
                );




            // ----- values -----

            selectionContent =
                RuntimeUi.CreateScrollView(
                    panel.transform,
                    out selectionScroll
                );


            RuntimeUi.Layout(
                selectionScroll.gameObject,
                flexibleHeight: 1.0f
            );


            selectionPanel.SetActive(
                false
            );
        }


        private void HandleSelectionChanged(
            EntityReference entity
        )
        {
            RefreshSelection();
        }


        private void RefreshSelection()
        {
            if (selectionPanel == null)
            {
                return;
            }


            EntityReference selected =
                interactionManager.Selected;


            if (!selected.IsValid)
            {
                selectionPanel.SetActive(
                    false
                );

                return;
            }


            EntityInfo info =
                interactionManager.BuildInfo(
                    selected
                );


            selectionTitle.text =
                info.Title;

            selectionSubtitle.text =
                info.Subtitle;


            selectCellButton.gameObject.SetActive(
                selected.Kind == EntityKind.Building &&
                selected.HasUnit
            );


            RuntimeUi.ClearChildren(
                selectionContent
            );


            // 1) What the current view shows for this entity: large.
            foreach (EntityInfoRow row in info.Highlights)
            {
                CreateHighlightRow(
                    selectionContent,
                    row
                );
            }


            // 2) Everything else, folded under section headings.
            if (info.Sections.Count > 0)
            {
                TMP_Text more =
                    RuntimeUi.CreateText(
                        selectionContent,
                        "More details (click to open)",
                        RuntimeUi.SmallSize,
                        RuntimeUi.MutedColor
                    );

                RuntimeUi.Layout(
                    more.gameObject,
                    preferredHeight: 26.0f
                );
            }

            foreach (EntityInfoSection section in info.Sections)
            {
                string sectionKey =
                    section.Title;

                bool open =
                    !section.Collapsed ||
                    expandedSections.Contains(sectionKey);

                Button header =
                    RuntimeUi.CreateButton(
                        selectionContent,
                        (open ? "▾ " : "▸ ") + section.Title,
                        () =>
                        {
                            if (!expandedSections.Remove(sectionKey))
                            {
                                expandedSections.Add(sectionKey);
                            }

                            RefreshSelection();
                        },
                        -1.0f,
                        30.0f
                    );

                TMP_Text headerText =
                    header.GetComponentInChildren<TMP_Text>();

                if (headerText != null)
                {
                    headerText.alignment =
                        TextAlignmentOptions.Left;
                }

                if (!open)
                {
                    continue;
                }

                foreach (EntityInfoRow row in section.Rows)
                {
                    CreateValueRow(
                        selectionContent,
                        row
                    );
                }
            }


            // 3) Technical identifiers last, small.
            if (!string.IsNullOrEmpty(info.Footer))
            {
                RuntimeUi.CreateText(
                    selectionContent,
                    info.Footer,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );
            }


            selectionScroll.verticalNormalizedPosition =
                1.0f;


            selectionPanel.SetActive(
                true
            );
        }


        /// <summary>
        /// A value shown in the current view: small label (with the
        /// channel / area), large value with unit, and its rank in
        /// words.
        /// </summary>
        internal static void CreateHighlightRow(
            Transform parent,
            EntityInfoRow row
        )
        {
            RectTransform block =
                RuntimeUi.CreateRect(
                    "Highlight",
                    parent
                );

            RuntimeUi.Vertical(
                block.gameObject,
                2,
                0.0f
            );

            string note =
                string.IsNullOrEmpty(row.Note)
                    ? string.Empty
                    : "  " + RuntimeUi.Colorize(row.Note, RuntimeUi.AccentColor);

            RuntimeUi.CreateText(
                block,
                row.Label + note,
                RuntimeUi.SmallSize,
                RuntimeUi.MutedColor
            );

            RuntimeUi.CreateText(
                block,
                $"<b>{ValueWithUnit(row)}</b>",
                RuntimeUi.TitleSize,
                row.Value.HasValue || row.Key == null
                    ? RuntimeUi.TextColor
                    : RuntimeUi.MutedColor
            );

            if (row.Percentile.HasValue)
            {
                RuntimeUi.CreateText(
                    block,
                    RankInWords(row.Percentile.Value),
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );
            }
        }


        /// <summary>"higher than 72 % of areas" / "lowest" / "highest".</summary>
        public static string RankInWords(
            double percentile
        )
        {
            if (percentile >= 99.5)
            {
                return "highest of all areas";
            }

            if (percentile <= 0.5)
            {
                return "lowest of all areas";
            }

            return $"higher than {percentile:0} % of areas";
        }


        internal static void CreateValueRow(
            Transform parent,
            EntityInfoRow row
        )
        {
            RectTransform line =
                RuntimeUi.CreateRect(
                    "Row",
                    parent
                );


            RuntimeUi.Horizontal(
                line.gameObject,
                0,
                8.0f
            );


            TMP_Text label =
                RuntimeUi.CreateText(
                    line,
                    (row.IsEncoded
                        ? RuntimeUi.Colorize("● ", RuntimeUi.AccentColor)
                        : "   ") +
                    row.Label,
                    RuntimeUi.BodySize,
                    RuntimeUi.TextColor
                );


            // Columns shrink toward their minimum widths in a
            // narrow panel instead of squeezing the label.
            RuntimeUi.Layout(
                label.gameObject,
                flexibleWidth: 1.0f
            ).minWidth =
                110.0f;


            TMP_Text value =
                RuntimeUi.CreateText(
                    line,
                    ValueWithUnit(row),
                    RuntimeUi.BodySize,
                    row.Value.HasValue || row.Key == null
                        ? RuntimeUi.TextColor
                        : RuntimeUi.MutedColor,
                    TextAnchor.MiddleRight
                );


            RuntimeUi.Layout(
                value.gameObject,
                preferredWidth: 190.0f
            ).minWidth =
                110.0f;


            TMP_Text percentile =
                RuntimeUi.CreateText(
                    line,
                    EntityInfoBuilder.FormatPercentile(
                        row.Percentile
                    ),
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor,
                    TextAnchor.MiddleRight
                );


            RuntimeUi.Layout(
                percentile.gameObject,
                preferredWidth: 42.0f
            );
        }


        private void CopySelection(
            int slot
        )
        {
            if (comparisonManager != null &&
                interactionManager.HasSelection)
            {
                comparisonManager.CopyToSlot(
                    slot,
                    interactionManager.Selected
                );
            }
        }


        private void SelectCellOfSelection()
        {
            if (interactionManager.Selected.TryGetUnit(
                    out EntityReference cell
                ))
            {
                interactionManager.Select(
                    cell
                );
            }
        }


        // =========================================================
        // COMPARE PANEL
        // =========================================================

        private void BuildComparePanel()
        {
            Image panel =
                RuntimeUi.CreatePanel(
                    "ComparePanel",
                    canvasRect,
                    RuntimeUi.PanelColor
                );


            compareRect =
                (RectTransform)panel.transform;


            // Size set every frame by UpdateLayout.
            RuntimeUi.Anchor(
                compareRect,
                new Vector2(0.0f, 0.0f),
                new Vector2(Margin, Margin),
                new Vector2(MaximumViewWidth * 2.0f + 36.0f, 640.0f)
            );


            RuntimeUi.Vertical(
                panel.gameObject,
                12,
                6.0f
            );


            comparePanel =
                panel.gameObject;


            // ----- header -----

            RectTransform header =
                RuntimeUi.CreateRect(
                    "Header",
                    panel.transform
                );


            RuntimeUi.Horizontal(
                header.gameObject,
                0,
                6.0f
            );


            TMP_Text title =
                RuntimeUi.CreateText(
                    header,
                    "<b>Compare blocks</b>   " +
                    RuntimeUi.Colorize(
                        "same scale and view · drag to rotate · " +
                        "wheel to zoom",
                        RuntimeUi.MutedColor
                    ),
                    RuntimeUi.HeadingSize,
                    RuntimeUi.TextColor
                );


            RuntimeUi.Layout(
                title.gameObject,
                flexibleWidth: 1.0f
            );


            RuntimeUi.CreateButton(
                header,
                "Swap",
                () => comparisonManager?.Swap(),
                70.0f
            );


            RuntimeUi.CreateButton(
                header,
                "Reset view",
                () => comparisonManager?.ResetView(),
                104.0f
            );


            RuntimeUi.CreateButton(
                header,
                "Clear",
                () => comparisonManager?.ClearAll(),
                70.0f
            );


            // ----- slots -----

            RectTransform slotRow =
                RuntimeUi.CreateRect(
                    "Slots",
                    panel.transform
                );


            RuntimeUi.Horizontal(
                slotRow.gameObject,
                0,
                12.0f
            );


            for (int i = 0; i < ComparisonManager.SlotCount; i++)
            {
                slotViews[i] =
                    BuildSlotView(
                        slotRow,
                        i
                    );
            }


            // ----- table -----

            RectTransform tableHeader =
                RuntimeUi.CreateRect(
                    "TableHeader",
                    panel.transform
                );


            CreateTableRow(
                tableHeader,
                "<b>Value</b>",
                "<b>A</b>",
                "<b>B</b>",
                "<b>B − A</b>",
                RuntimeUi.MutedColor
            );


            compareTable =
                RuntimeUi.CreateScrollView(
                    panel.transform,
                    out ScrollRect tableScroll
                );


            RuntimeUi.Layout(
                tableScroll.gameObject,
                flexibleHeight: 1.0f
            );


            comparePanel.SetActive(
                false
            );
        }


        private SlotView BuildSlotView(
            Transform parent,
            int index
        )
        {
            var view =
                new SlotView();


            RectTransform column =
                RuntimeUi.CreateRect(
                    $"Slot{index}",
                    parent
                );


            RuntimeUi.Vertical(
                column.gameObject,
                0,
                4.0f
            );


            RuntimeUi.Layout(
                column.gameObject,
                preferredWidth: MaximumViewWidth
            );


            view.Column =
                column.gameObject;


            RectTransform header =
                RuntimeUi.CreateRect(
                    "Header",
                    column
                );


            RuntimeUi.Horizontal(
                header.gameObject,
                0,
                8.0f
            );


            Color slotColor =
                comparisonManager != null
                    ? comparisonManager.Slots[index].Color
                    : Color.white;


            slotColor.a =
                1.0f;


            Image chip =
                RuntimeUi.CreatePanel(
                    "Chip",
                    header,
                    slotColor
                );


            RuntimeUi.Layout(
                chip.gameObject,
                30.0f,
                28.0f
            );


            TMP_Text chipLabel =
                RuntimeUi.CreateText(
                    chip.transform,
                    index == 0
                        ? "A"
                        : "B",
                    RuntimeUi.HeadingSize,
                    Color.black,
                    TextAnchor.MiddleCenter,
                    FontStyles.Bold
                );


            RuntimeUi.Stretch(
                chipLabel.rectTransform,
                0.0f
            );


            view.Title =
                RuntimeUi.CreateText(
                    header,
                    string.Empty,
                    RuntimeUi.BodySize + 1.0f,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );


            RuntimeUi.Layout(
                view.Title.gameObject,
                flexibleWidth: 1.0f
            );


            view.ClearButton =
                RuntimeUi.CreateButton(
                    header,
                    "×",
                    () => comparisonManager?.ClearSlot(index),
                    32.0f,
                    28.0f,
                    RuntimeUi.HeadingSize
                );


            // 3D view, rendered at exactly its pixel size.
            Image frame =
                RuntimeUi.CreatePanel(
                    "Frame",
                    column,
                    new Color(0.12f, 0.13f, 0.15f, 1.0f)
                );


            RuntimeUi.Layout(
                frame.gameObject,
                MaximumViewWidth,
                MaximumViewWidth * ViewAspect
            );


            view.Frame =
                frame.gameObject;


            RectTransform viewRect =
                RuntimeUi.CreateRect(
                    "View",
                    frame.transform
                );


            RuntimeUi.Stretch(
                viewRect,
                0.0f
            );


            view.View =
                viewRect.gameObject.AddComponent<RawImage>();


            viewRect.gameObject
                .AddComponent<ComparisonViewInput>()
                .Comparison =
                    comparisonManager;


            view.Placeholder =
                RuntimeUi.CreateText(
                    frame.transform,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.MutedColor,
                    TextAnchor.MiddleCenter
                );


            RuntimeUi.Stretch(
                view.Placeholder.rectTransform,
                24.0f
            );


            view.Caption =
                RuntimeUi.CreateText(
                    column,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );


            return view;
        }


        private void RefreshComparison()
        {
            if (comparePanel == null ||
                comparisonManager == null)
            {
                return;
            }


            if (!comparisonManager.HasAnyFilled)
            {
                comparePanel.SetActive(
                    false
                );

                return;
            }


            IReadOnlyList<ComparisonManager.Slot> slots =
                comparisonManager.Slots;


            for (int i = 0; i < slots.Count; i++)
            {
                ComparisonManager.Slot slot =
                    slots[i];

                SlotView view =
                    slotViews[i];


                view.ClearButton.gameObject.SetActive(
                    slot.IsFilled
                );


                if (!slot.IsFilled)
                {
                    view.Title.text =
                        "empty";

                    view.Caption.text =
                        string.Empty;

                    view.View.texture =
                        null;

                    view.View.color =
                        new Color(0.0f, 0.0f, 0.0f, 0.0f);

                    view.Placeholder.text =
                        $"Select a cell and press " +
                        $"Copy → {slot.Label} (or C).";

                    continue;
                }


                view.Title.text =
                    slot.Info != null
                        ? slot.Info.Title
                        : slot.Entity.Id;


                view.View.texture =
                    slot.Texture;

                view.View.color =
                    Color.white;


                view.Placeholder.text =
                    slot.TriangleCount == 0
                        ? "Nothing is drawn for this block in " +
                          "the current visualization."
                        : string.Empty;


                view.Caption.text =
                    slot.Info != null
                        ? slot.Info.Subtitle +
                          $"   ·   {slot.TriangleCount:N0} triangles"
                        : string.Empty;
            }


            RefreshCompareTable(
                slots[0],
                slots[1]
            );


            comparePanel.SetActive(
                true
            );
        }


        private void RefreshCompareTable(
            ComparisonManager.Slot a,
            ComparisonManager.Slot b
        )
        {
            RuntimeUi.ClearChildren(
                compareTable
            );


            // Rows in A's order, then any only B has.
            var keys =
                new List<string>();

            var labels =
                new Dictionary<string, EntityInfoRow>(
                    StringComparer.Ordinal
                );


            foreach (ComparisonManager.Slot slot in new[] { a, b })
            {
                if (slot.Info == null)
                {
                    continue;
                }


                foreach (EntityInfoRow row in slot.Info.DataRows)
                {
                    if (!labels.ContainsKey(row.Key))
                    {
                        labels.Add(
                            row.Key,
                            row
                        );

                        keys.Add(
                            row.Key
                        );
                    }
                }
            }


            foreach (string key in keys)
            {
                EntityInfoRow rowA =
                    null;

                EntityInfoRow rowB =
                    null;


                a.Info?.TryGetDataRow(
                    key,
                    out rowA
                );

                b.Info?.TryGetDataRow(
                    key,
                    out rowB
                );


                EntityInfoRow template =
                    labels[key];


                string label =
                    (template.IsEncoded
                        ? RuntimeUi.Colorize("● ", RuntimeUi.AccentColor)
                        : "   ") +
                    template.Label +
                    (string.IsNullOrEmpty(template.Unit)
                        ? string.Empty
                        : RuntimeUi.Colorize(
                            $" ({template.Unit})",
                            RuntimeUi.MutedColor
                        ));


                RectTransform line =
                    RuntimeUi.CreateRect(
                        "Row",
                        compareTable
                    );


                CreateTableRow(
                    line,
                    label,
                    CompareCell(rowA, a.IsFilled),
                    CompareCell(rowB, b.IsFilled),
                    Difference(rowA, rowB),
                    RuntimeUi.TextColor
                );
            }
        }


        internal static void CreateTableRow(
            RectTransform line,
            string label,
            string valueA,
            string valueB,
            string difference,
            Color color
        )
        {
            RuntimeUi.Horizontal(
                line.gameObject,
                0,
                8.0f
            );


            TMP_Text labelText =
                RuntimeUi.CreateText(
                    line,
                    label,
                    RuntimeUi.BodySize,
                    color
                );


            RuntimeUi.Layout(
                labelText.gameObject,
                flexibleWidth: 1.0f
            ).minWidth =
                130.0f;


            foreach (
                string cell
                in new[] { valueA, valueB, difference }
            )
            {
                TMP_Text text =
                    RuntimeUi.CreateText(
                        line,
                        cell,
                        RuntimeUi.BodySize,
                        color,
                        TextAnchor.MiddleRight
                    );


                RuntimeUi.Layout(
                    text.gameObject,
                    preferredWidth: 160.0f
                ).minWidth =
                    90.0f;
            }
        }


        internal static string CompareCell(
            EntityInfoRow row,
            bool filled
        )
        {
            if (!filled)
            {
                return string.Empty;
            }


            if (row == null)
            {
                return RuntimeUi.Colorize(
                    "—",
                    RuntimeUi.MutedColor
                );
            }


            string percentile =
                EntityInfoBuilder.FormatPercentile(
                    row.Percentile
                );


            return
                (row.Value.HasValue
                    ? row.ValueText
                    : RuntimeUi.Colorize(
                        row.ValueText,
                        RuntimeUi.MutedColor
                    )) +
                (percentile.Length > 0
                    ? " " +
                      RuntimeUi.Colorize(
                          percentile,
                          RuntimeUi.MutedColor
                      )
                    : string.Empty);
        }


        /// <summary>
        /// B − A, and the change relative to A when A ≠ 0.
        /// </summary>
        internal static string Difference(
            EntityInfoRow rowA,
            EntityInfoRow rowB
        )
        {
            if (rowA?.Value == null ||
                rowB?.Value == null)
            {
                return string.Empty;
            }


            double a =
                rowA.Value.Value;

            double b =
                rowB.Value.Value;

            double difference =
                b - a;


            string text =
                (difference > 0.0 ? "+" : string.Empty) +
                EntityInfoBuilder.FormatNumber(
                    difference
                );


            if (Math.Abs(a) > 1e-12)
            {
                double relative =
                    100.0 *
                    difference /
                    Math.Abs(a);


                text +=
                    RuntimeUi.Colorize(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            " ({0}{1:0.#}%)",
                            relative > 0.0 ? "+" : string.Empty,
                            relative
                        ),
                        RuntimeUi.MutedColor
                    );
            }


            return text;
        }


        // =========================================================
        // HELP
        // =========================================================

        private void BuildHelp()
        {
            Image panel =
                RuntimeUi.CreatePanel(
                    "Help",
                    canvasRect,
                    new Color(0.0f, 0.0f, 0.0f, 0.6f)
                );


            panel.raycastTarget =
                false;


            helpRect =
                (RectTransform)panel.transform;


            // Width set by UpdateLayout (room between the side
            // panels).
            RuntimeUi.Anchor(
                helpRect,
                new Vector2(0.5f, 1.0f),
                new Vector2(0.0f, -12.0f),
                new Vector2(980.0f, 0.0f)
            );


            RuntimeUi.Vertical(
                panel.gameObject,
                8,
                0.0f
            );


            panel.gameObject
                .AddComponent<ContentSizeFitter>()
                .verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;


            helpText =
                RuntimeUi.CreateText(
                    panel.transform,
                    HelpText,
                    RuntimeUi.SmallSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleCenter
                );
        }


        private void UpdateHelp()
        {
            if (helpText == null)
            {
                return;
            }


            string status =
                interactionManager.PickingStatus;


            // The full help only where it fits between the side
            // panels; the picking status always shows.
            bool showFullHelp =
                showHelp &&
                helpFits;


            bool visible =
                showFullHelp ||
                !string.IsNullOrEmpty(status);


            GameObject panel =
                helpText.transform.parent.gameObject;


            if (panel.activeSelf != visible)
            {
                panel.SetActive(
                    visible
                );
            }


            if (!visible)
            {
                return;
            }


            string text =
                showFullHelp
                    ? HelpText
                    : string.Empty;


            if (!string.IsNullOrEmpty(status))
            {
                text =
                    RuntimeUi.Colorize(
                        status,
                        RuntimeUi.AccentColor
                    ) +
                    (text.Length > 0
                        ? "\n" + text
                        : string.Empty);
            }


            if (helpText.text != text)
            {
                helpText.text =
                    text;
            }
        }


        // =========================================================
        // FORMATTING
        // =========================================================

        internal static string ValueWithUnit(
            EntityInfoRow row
        )
        {
            // Missing data values ("no data") get no unit. A
            // zero-width space after ':' lets long IDs
            // (layer:number) wrap there instead of mid-number.
            string value =
                row.ValueText?.Replace(
                    ":",
                    ":​"
                );


            return
                string.IsNullOrEmpty(row.Unit) ||
                (row.Key != null && !row.Value.HasValue)
                    ? value
                    : $"{value} {row.Unit}";
        }
    }
}
