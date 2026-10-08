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


        private RectTransform canvasRect;

        private TMP_Text text;


        private void Awake()
        {
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
            }
        }


        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.HoverChanged -=
                    HandleHoverChanged;
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
            if (canvasRect == null)
            {
                return;
            }


            if (!pick.IsValid)
            {
                canvasRect.gameObject.SetActive(false);

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


            text.text =
                string.Join(
                    "\n",
                    lines
                );

            canvasRect.gameObject.SetActive(true);
        }


        private void LateUpdate()
        {
            if (canvasRect == null ||
                !canvasRect.gameObject.activeSelf ||
                interactionManager == null ||
                viewCamera == null)
            {
                return;
            }


            PickResult hovered =
                interactionManager.Hovered;


            if (!hovered.IsValid)
            {
                canvasRect.gameObject.SetActive(false);

                return;
            }


            // World units per real metre (the rig is scaled).
            float worldScale =
                Mathf.Max(
                    1e-4f,
                    viewCamera.transform.lossyScale.x
                );


            Vector3 position =
                hovered.Point +
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
