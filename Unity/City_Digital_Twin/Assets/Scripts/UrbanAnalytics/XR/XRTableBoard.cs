using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Visualization.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// A large read-only board standing just beyond the table edge on
    /// the far side from the user, readable without raising a hand:
    /// the current view, its legend and the selected area's values.
    ///
    /// It lives in the scaled XR Origin's space (real metres), so it
    /// keeps its size when the table is resized. When the user walks
    /// around the table it moves to stay opposite them, but only once
    /// they are more than <see cref="repositionDegrees"/> away from its
    /// side (no constant drifting while looking around).
    ///
    /// Y (left secondary, XRControllerShortcuts) shows/hides it; this
    /// is logged as the study event vr_board.
    /// </summary>
    public sealed class XRTableBoard :
        MonoBehaviour
    {
        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Placement (real metres)")]
        [SerializeField]
        private TabletopRig tabletopRig;

        [SerializeField]
        private Camera viewCamera;

        [Tooltip("Distance between the table edge and the board.")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float edgeSetbackMeters =
            0.15f;

        [Tooltip("Height of the board's bottom edge above the table top.")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float bottomAboveTableMeters =
            0.10f;

        [Tooltip("The board moves when the user is this far around the table.")]
        [SerializeField]
        [Range(10.0f, 120.0f)]
        private float repositionDegrees =
            50.0f;

        [Tooltip("How fast the board slides to its new side (1/s).")]
        [SerializeField]
        [Range(0.5f, 10.0f)]
        private float moveSharpness =
            3.0f;

        [Tooltip("Real metres per canvas unit (0.002 = 2 mm; body text about 3 cm, readable across the table).")]
        [SerializeField]
        [Range(0.0005f, 0.005f)]
        private float metersPerUnit =
            0.002f;

        [Tooltip("Canvas size in units (640 × 400 at 2 mm = 1.28 × 0.80 m).")]
        [SerializeField]
        private Vector2 sizeUnits =
            new Vector2(640.0f, 400.0f);

        [Tooltip("Draws after the selection highlight (Transparent+10).")]
        [SerializeField]
        private int sortingOrder =
            105;

        [SerializeField]
        private bool startVisible =
            true;


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private InteractionManager interactionManager;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private Study.StudySession studySession;


        // =========================================================
        // RUNTIME
        // =========================================================

        private GameObject root;

        private TMP_Text viewTitle;

        private TMP_Text selectionTitle;

        private TMP_Text selectionSubtitle;

        private RectTransform selectionRows;

        private bool wantVisible;

        private bool hasBearing;

        private float bearing;

        private float targetBearing;


        public bool IsVisible =>
            wantVisible;


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

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<Study.StudySession>();
            }

            if (viewCamera == null)
            {
                viewCamera =
                    Camera.main;
            }


            if (tabletopRig == null ||
                tabletopRig.Origin == null)
            {
                Debug.LogError(
                    "XRTableBoard: needs a TabletopRig with an XR Origin.",
                    this
                );

                enabled =
                    false;

                return;
            }


            wantVisible =
                startVisible;

            Build();
        }


        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.SelectionChanged +=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed +=
                    RefreshSelection;
            }

            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed +=
                    RefreshView;
            }
        }


        private void Start()
        {
            RefreshView();

            RefreshSelection();
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.SelectionChanged -=
                    HandleSelectionChanged;

                interactionManager.SceneRefreshed -=
                    RefreshSelection;
            }

            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed -=
                    RefreshView;
            }
        }


        private void LateUpdate()
        {
            if (root == null)
            {
                return;
            }


            bool show =
                wantVisible &&
                tabletopRig.IsPlaced &&
                viewCamera != null;

            if (root.activeSelf != show)
            {
                root.SetActive(
                    show
                );
            }

            if (show)
            {
                Place(
                    Time.unscaledDeltaTime
                );
            }
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        public void SetVisible(
            bool visible
        )
        {
            if (wantVisible == visible)
            {
                return;
            }


            wantVisible =
                visible;

            hasBearing =
                false;

            studySession?.LogEvent(
                "vr_board",
                ("visible", visible)
            );
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

            Vector3 head =
                space.InverseTransformPoint(
                    viewCamera.transform.position
                );

            Vector3 toUser =
                head - center;

            toUser.y =
                0.0f;


            if (toUser.sqrMagnitude > 1e-4f)
            {
                // The board's side is opposite the user.
                float opposite =
                    Mathf.Atan2(-toUser.x, -toUser.z) * Mathf.Rad2Deg;

                if (!hasBearing)
                {
                    bearing =
                        opposite;

                    targetBearing =
                        opposite;

                    hasBearing =
                        true;
                }
                else if (Mathf.Abs(Mathf.DeltaAngle(targetBearing, opposite)) >
                         repositionDegrees)
                {
                    targetBearing =
                        opposite;
                }
            }


            bearing =
                Mathf.LerpAngle(
                    bearing,
                    targetBearing,
                    1.0f - Mathf.Exp(-moveSharpness * deltaTime)
                );


            var direction =
                new Vector3(
                    Mathf.Sin(bearing * Mathf.Deg2Rad),
                    0.0f,
                    Mathf.Cos(bearing * Mathf.Deg2Rad)
                );


            // Distance from the centre to the table edge along the
            // direction (the table can be resized, moved and turned).
            float toEdge =
                tabletopRig.EdgeDistanceMeters(
                    direction
                );


            root.transform.localPosition =
                center +
                direction * (toEdge + edgeSetbackMeters) +
                Vector3.up *
                (bottomAboveTableMeters + 0.5f * sizeUnits.y * metersPerUnit);

            // Read from its -Z side: forward points away from the table.
            root.transform.localRotation =
                Quaternion.LookRotation(
                    direction,
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
                    "XRTableBoardCanvas",
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

            // Rays stop on the board instead of picking the city.
            root.AddComponent<TrackedDeviceGraphicRaycaster>();


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
                16,
                10.0f
            );


            Transform panel =
                background.transform;


            viewTitle =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.TitleSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );


            RectTransform body =
                RuntimeUi.CreateRect(
                    "Body",
                    panel
                );

            RuntimeUi.Horizontal(
                body.gameObject,
                0,
                16.0f
            );

            RuntimeUi.Layout(
                body.gameObject,
                flexibleHeight: 1.0f
            );


            // ----- left: legend -----

            RectTransform legendColumn =
                CreateColumn(
                    body,
                    "LegendColumn"
                );

            RectTransform legendContainer =
                RuntimeUi.CreateRect(
                    "Legends",
                    legendColumn
                );

            RuntimeUi.Vertical(
                legendContainer.gameObject,
                0,
                10.0f
            );


            var legendObject =
                new GameObject("XRBoardLegendView");

            legendObject.SetActive(false);

            legendObject.transform.SetParent(
                transform,
                false
            );

            legendObject.AddComponent<LegendStackView>().Container =
                legendContainer;

            legendObject.SetActive(true);


            // ----- right: selection -----

            RectTransform selectionColumn =
                CreateColumn(
                    body,
                    "SelectionColumn"
                );

            selectionTitle =
                RuntimeUi.CreateText(
                    selectionColumn,
                    string.Empty,
                    RuntimeUi.HeadingSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            selectionSubtitle =
                RuntimeUi.CreateText(
                    selectionColumn,
                    string.Empty,
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor
                );

            selectionRows =
                RuntimeUi.CreateRect(
                    "Rows",
                    selectionColumn
                );

            RuntimeUi.Vertical(
                selectionRows.gameObject,
                0,
                4.0f
            );


            RuntimeUi.CreateText(
                panel,
                "Y hides this board · look at your left wrist for the menu",
                RuntimeUi.SmallSize,
                RuntimeUi.MutedColor
            );
        }


        private static RectTransform CreateColumn(
            Transform parent,
            string name
        )
        {
            RectTransform column =
                RuntimeUi.CreateRect(
                    name,
                    parent
                );

            RuntimeUi.Vertical(
                column.gameObject,
                0,
                6.0f
            ).childAlignment =
                TextAnchor.UpperLeft;

            RuntimeUi.Layout(
                column.gameObject,
                flexibleWidth: 1.0f,
                flexibleHeight: 1.0f
            );

            return column;
        }


        // =========================================================
        // CONTENT
        // =========================================================

        private void HandleSelectionChanged(
            EntityReference entity
        )
        {
            RefreshSelection();
        }


        private void RefreshView()
        {
            if (viewTitle == null)
            {
                return;
            }


            string shown =
                visualizationSwitcher != null &&
                visualizationSwitcher.ActiveIndex >= 0 &&
                visualizationSwitcher.ActiveIndex < visualizationSwitcher.Options.Count
                    ? visualizationSwitcher.Options[visualizationSwitcher.ActiveIndex].DisplayName
                    : null;

            viewTitle.text =
                shown ?? "No view shown";
        }


        private void RefreshSelection()
        {
            if (selectionRows == null ||
                interactionManager == null)
            {
                return;
            }


            RuntimeUi.ClearChildren(
                selectionRows
            );


            EntityReference selected =
                interactionManager.Selected;


            if (!selected.IsValid)
            {
                selectionTitle.text =
                    "Nothing selected";

                selectionSubtitle.text =
                    "Point at the table and pull the trigger.";

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

            foreach (EntityInfoRow row in info.Highlights)
            {
                DesktopInteractionUI.CreateHighlightRow(
                    selectionRows,
                    row
                );
            }
        }
    }
}
