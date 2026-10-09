using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// VR tooltip: a small label floating above the hovered point on
    /// the table, turned towards the user. Shows the entity's name and
    /// the first values of the current view (same text as the desktop
    /// tooltip).
    ///
    /// When nothing is hovered, the label stays above the selected
    /// entity (marked "Selected"), so a selection always shows its
    /// values where the user is looking.
    ///
    /// Sized in real metres and multiplied by the rig scale (the XR
    /// camera's world scale), so it reads the same at any table scale.
    /// The label is visual only: it has no raycaster or collider, so it
    /// never blocks picking or UI rays.
    /// </summary>
    public sealed class XRHoverLabel :
        MonoBehaviour
    {
        [SerializeField]
        private InteractionManager interactionManager;

        [Tooltip("XR camera; the label faces it and uses its world scale.")]
        [SerializeField]
        private Camera viewCamera;

        [Tooltip("Real metres per canvas unit.")]
        [SerializeField]
        [Range(0.0002f, 0.002f)]
        private float metersPerUnit =
            0.0006f;

        [Tooltip("Draws over the selection highlight; below the hand menu.")]
        [SerializeField]
        private int sortingOrder =
            100;

        [Tooltip("Height of the label's bottom edge above the hovered point.")]
        [SerializeField]
        [Range(0.0f, 0.2f)]
        private float heightAboveMeters =
            0.03f;

        [SerializeField]
        [Range(0, 8)]
        private int maxValues =
            3;

        [SerializeField]
        [Min(120.0f)]
        private float widthUnits =
            380.0f;

        [Tooltip("Shows the active click tool (Copy / Compare) on the hover label.")]
        [SerializeField]
        private XRClickTools clickTools;


        private RectTransform canvasRect;

        private TMP_Text text;

        private string hoverText;

        private string selectionText;

        private Vector3 selectionPoint;

        private bool showingHover;


        private void Awake()
        {
            if (clickTools == null)
            {
                clickTools =
                    FindFirstObjectByType<XRClickTools>();
            }

            if (interactionManager == null)
            {
                interactionManager =
                    FindFirstObjectByType<InteractionManager>();
            }

            if (viewCamera == null)
            {
                viewCamera =
                    Camera.main;
            }

            Build();
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

            if (canvasRect != null)
            {
                canvasRect.gameObject.SetActive(false);
            }
        }


        private void Build()
        {
            var canvasObject =
                new GameObject(
                    "XRHoverLabelCanvas",
                    typeof(RectTransform)
                );

            canvasObject.transform.SetParent(
                transform,
                false
            );

            Canvas canvas =
                canvasObject.AddComponent<Canvas>();

            canvas.renderMode =
                RenderMode.WorldSpace;

            canvas.sortingOrder =
                sortingOrder;

            canvasRect =
                (RectTransform)canvasObject.transform;

            // Pivot at the bottom centre: the label stands on its
            // anchor point.
            canvasRect.pivot =
                new Vector2(0.5f, 0.0f);

            canvasRect.sizeDelta =
                new Vector2(widthUnits, 0.0f);


            Color opaque =
                RuntimeUi.PanelColor;

            opaque.a =
                1.0f;

            Image background =
                RuntimeUi.CreatePanel(
                    "Label",
                    canvasRect,
                    opaque
                );

            background.raycastTarget =
                false;

            RectTransform backgroundRect =
                background.rectTransform;

            backgroundRect.anchorMin =
                new Vector2(0.5f, 0.0f);

            backgroundRect.anchorMax =
                new Vector2(0.5f, 0.0f);

            backgroundRect.pivot =
                new Vector2(0.5f, 0.0f);

            backgroundRect.anchoredPosition =
                Vector2.zero;

            RuntimeUi.Vertical(
                background.gameObject,
                10,
                0.0f
            );

            ContentSizeFitter fitter =
                background.gameObject.AddComponent<ContentSizeFitter>();

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            fitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;

            backgroundRect.sizeDelta =
                new Vector2(widthUnits, 0.0f);


            text =
                RuntimeUi.CreateText(
                    background.transform,
                    string.Empty,
                    RuntimeUi.BodySize,
                    RuntimeUi.TextColor
                );


            canvasObject.SetActive(false);
        }


        private void HandleHoverChanged(
            PickResult pick
        )
        {
            hoverText =
                pick.IsValid
                    ? BuildText(pick.Entity, false)
                    : null;
        }


        private void HandleSelectionChanged(
            EntityReference entity
        )
        {
            RefreshSelection();
        }


        /// <summary>
        /// Text and anchor (top centre of everything drawn for it) of
        /// the current selection; rebuilt when the view changes.
        /// </summary>
        private void RefreshSelection()
        {
            selectionText =
                null;

            if (interactionManager == null ||
                !interactionManager.HasSelection)
            {
                return;
            }


            EntityReference selected =
                interactionManager.Selected;

            EntityGeometry geometry =
                interactionManager.CollectGeometry(
                    selected,
                    true
                );

            if (geometry.IsEmpty)
            {
                geometry =
                    interactionManager.CollectFootprint(
                        selected
                    );
            }

            if (geometry.IsEmpty)
            {
                return;
            }


            Bounds bounds =
                geometry.Bounds;

            selectionPoint =
                new Vector3(
                    bounds.center.x,
                    bounds.max.y,
                    bounds.center.z
                );

            selectionText =
                BuildText(
                    selected,
                    true
                );
        }


        private string BuildText(
            EntityReference entity,
            bool selected
        )
        {
            EntityInfo info =
                interactionManager.BuildInfo(
                    entity
                );


            var lines =
                new List<string>();

            if (selected)
            {
                lines.Add(
                    RuntimeUi.Colorize(
                        "Selected",
                        RuntimeUi.AccentColor
                    )
                );
            }

            lines.Add(
                $"<b>{info.Title}</b>"
            );

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
                if (shown >= maxValues)
                {
                    break;
                }

                lines.Add(
                    $"{row.Label}: <b>" +
                    $"{DesktopInteractionUI.ValueWithUnit(row)}</b>"
                );

                shown++;
            }


            return string.Join(
                "\n",
                lines
            );
        }


        /// <summary>First hover line when a click tool other than Select is on.</summary>
        private string ToolLine()
        {
            if (clickTools == null)
            {
                return string.Empty;
            }

            switch (clickTools.Tool)
            {
                case XRClickTool.Copy:
                    return RuntimeUi.Colorize("Click to copy", RuntimeUi.AccentColor) + "\n";

                case XRClickTool.Compare:
                    return RuntimeUi.Colorize("Click to compare", RuntimeUi.AccentColor) + "\n";

                default:
                    return string.Empty;
            }
        }


        private void LateUpdate()
        {
            if (canvasRect == null ||
                interactionManager == null ||
                viewCamera == null)
            {
                return;
            }


            // Hover wins; otherwise the selection keeps its label.
            PickResult hovered =
                interactionManager.Hovered;

            bool hover =
                hovered.IsValid &&
                hoverText != null;

            bool selection =
                !hover &&
                selectionText != null &&
                interactionManager.HasSelection;


            if (!hover &&
                !selection)
            {
                canvasRect.gameObject.SetActive(false);

                return;
            }


            string wanted =
                hover
                    ? ToolLine() + hoverText
                    : selectionText;

            if (text.text != wanted ||
                showingHover != hover)
            {
                text.text =
                    wanted;

                showingHover =
                    hover;
            }

            canvasRect.gameObject.SetActive(true);


            // World units per real metre (the rig is scaled).
            float worldScale =
                Mathf.Max(
                    1e-4f,
                    viewCamera.transform.lossyScale.x
                );


            Vector3 position =
                (hover ? hovered.Point : selectionPoint) +
                Vector3.up * (heightAboveMeters * worldScale);


            // Turn to the camera around the vertical axis only, so
            // the text stays upright.
            Vector3 toLabel =
                position - viewCamera.transform.position;

            toLabel.y =
                0.0f;


            if (toLabel.sqrMagnitude < 1e-8f)
            {
                toLabel =
                    viewCamera.transform.forward;
            }


            canvasRect.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(
                    toLabel.normalized,
                    Vector3.up
                )
            );

            canvasRect.localScale =
                Vector3.one *
                (metersPerUnit * worldScale /
                 Mathf.Max(1e-6f, transform.lossyScale.x));
        }
    }
}
