using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// What is selected: name, the values of the current view first
    /// (large), then every other section (all information, scrolls with
    /// the thumbstick). Stands beside the user at their side of the
    /// table and can be carried anywhere by its Grab bar. Separate from
    /// the legend, which stays at the table.
    /// </summary>
    public sealed class XRInfoPanel :
        XRTablePanel
    {
        private const float ButtonHeight =
            44.0f;


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private InteractionManager interactionManager;


        private TMP_Text title;

        private TMP_Text subtitle;

        private GameObject actions;

        private Button selectAreaButton;

        private RectTransform rows;

        private ScrollRect scroll;


        public override string PanelName =>
            "Info";


        public XRInfoPanel()
        {
            // 520 × 620 units at 0.8 mm = 0.42 × 0.50 m, to the user's right.
            Configure(
                PanelPlacement.NearSide,
                new Vector2(520.0f, 620.0f),
                0.0008f,
                0.38f,
                0.95f,
                true,
                true
            );
        }


        protected override void Awake()
        {
            if (interactionManager == null)
            {
                interactionManager =
                    FindFirstObjectByType<InteractionManager>();
            }

            base.Awake();
        }


        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.SelectionChanged +=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed +=
                    Refresh;
            }
        }


        private void Start()
        {
            Refresh();
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.SelectionChanged -=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed -=
                    Refresh;
            }
        }


        protected override void BuildContent(
            Transform panel
        )
        {
            CreateHeader(
                panel,
                "Info",
                true
            );

            title =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            subtitle =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );


            RectTransform actionRow =
                RuntimeUi.CreateRect(
                    "Actions",
                    panel
                );

            RuntimeUi.Horizontal(
                actionRow.gameObject,
                0,
                6.0f
            );

            actions =
                actionRow.gameObject;

            selectAreaButton =
                RuntimeUi.CreateButton(
                    actionRow,
                    "Select its area",
                    SelectArea,
                    170.0f,
                    ButtonHeight
                );

            RuntimeUi.CreateButton(
                actionRow,
                "Unselect",
                () => interactionManager?.ClearSelection(),
                130.0f,
                ButtonHeight
            );


            // Most important first, then all the rest; scrolls (ray +
            // thumbstick) when it does not fit.
            rows =
                CreateScroll(
                    panel,
                    out scroll
                );
        }


        private void HandleSelectionChanged(
            EntityReference entity
        )
        {
            Refresh();

            // A new selection brings the panel back if it was hidden.
            if (entity.IsValid)
            {
                SetVisible(true);
            }
        }


        private void SelectArea()
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


        private void Refresh()
        {
            if (rows == null ||
                interactionManager == null)
            {
                return;
            }


            RuntimeUi.ClearChildren(
                rows
            );


            EntityReference selected =
                interactionManager.Selected;


            if (!selected.IsValid)
            {
                title.text =
                    "Nothing selected";

                subtitle.text =
                    "Point at a building or area on the table and pull the trigger.";

                actions.SetActive(false);

                return;
            }


            EntityInfo info =
                interactionManager.BuildInfo(
                    selected
                );

            title.text =
                info.Title;

            subtitle.text =
                info.Subtitle;

            actions.SetActive(true);

            selectAreaButton.gameObject.SetActive(
                selected.Kind == EntityKind.Building &&
                selected.HasUnit
            );


            foreach (EntityInfoRow row in info.Highlights)
            {
                DesktopInteractionUI.CreateHighlightRow(
                    rows,
                    row
                );
            }


            // Then everything else (the sections the desktop folds away).
            foreach (EntityInfoSection section in info.Sections)
            {
                RuntimeUi.CreateText(
                    rows,
                    section.Title,
                    RuntimeUi.SmallSize,
                    RuntimeUi.AccentColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

                foreach (EntityInfoRow row in section.Rows)
                {
                    DesktopInteractionUI.CreateValueRow(
                        rows,
                        row
                    );
                }
            }


            if (!string.IsNullOrEmpty(info.Footer))
            {
                RuntimeUi.CreateText(
                    rows,
                    info.Footer,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );
            }


            scroll.verticalNormalizedPosition =
                1.0f;
        }
    }
}
