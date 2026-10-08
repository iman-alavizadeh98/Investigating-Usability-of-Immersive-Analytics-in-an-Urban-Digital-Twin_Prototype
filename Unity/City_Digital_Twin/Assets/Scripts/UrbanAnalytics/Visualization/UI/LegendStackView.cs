using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.Visualization.UI
{
    /// <summary>
    /// One legend per encoded variable of the active visualization
    /// (VisualizationManager.ActiveLegends), stacked in a panel:
    ///
    ///   CHANNEL (e.g. "Height ↑", "Building colour (from valdistrikt)")
    ///   Variable name (unit)
    ///   [colour ramp | grey height bar | category chips]
    ///   minimum                                         maximum
    ///   source layer (with its year) · scale
    ///
    /// Built from code (no prefab), on its own screen-space canvas.
    /// The panel is named "LegendPanel", so DesktopInteractionUI
    /// scales it with the rest of the UI and places the selection
    /// panel underneath.
    ///
    /// For VR, set <see cref="Container"/> before the component is
    /// enabled (add it to an inactive GameObject): the legends then
    /// fill that world-space container and no screen canvas is
    /// built. Each instance owns its ramp textures.
    /// </summary>
    public sealed class LegendStackView : MonoBehaviour
    {
        [SerializeField]
        private VisualizationManager visualizationManager;

        [SerializeField]
        private string panelName =
            "LegendPanel";

        [SerializeField]
        [Min(160.0f)]
        private float panelWidth =
            330.0f;

        [SerializeField]
        private Vector2 margin =
            new Vector2(12.0f, 12.0f);

        [SerializeField]
        private int sortingOrder =
            90;


        private RectTransform panel;


        /// <summary>
        /// Optional container (vertical layout) to fill instead of
        /// building a screen-space canvas. Set it before Awake.
        /// </summary>
        public RectTransform Container
        {
            get;
            set;
        }

        private readonly List<Texture2D> textures =
            new List<Texture2D>();


        private void Awake()
        {
            if (visualizationManager == null)
            {
                visualizationManager =
                    FindFirstObjectByType<VisualizationManager>();
            }

            if (Container != null)
            {
                panel =
                    Container;
            }
            else
            {
                BuildCanvas();
            }
        }


        private void OnEnable()
        {
            if (visualizationManager == null)
            {
                return;
            }

            visualizationManager.LegendsChanged +=
                Refresh;

            Refresh(
                visualizationManager.ActiveLegends
            );
        }


        private void OnDisable()
        {
            if (visualizationManager != null)
            {
                visualizationManager.LegendsChanged -=
                    Refresh;
            }
        }


        private void OnDestroy()
        {
            ReleaseTextures();
        }


        // =========================================================
        // BUILD
        // =========================================================

        private void BuildCanvas()
        {
            var canvasObject =
                new GameObject(
                    "LegendCanvas",
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

            CanvasScaler scaler =
                canvasObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ConstantPixelSize;

            canvasObject.AddComponent<GraphicRaycaster>();


            Image background =
                RuntimeUi.CreatePanel(
                    panelName,
                    canvasObject.transform,
                    RuntimeUi.PanelColor
                );

            panel =
                background.rectTransform;

            panel.anchorMin =
                new Vector2(1.0f, 1.0f);

            panel.anchorMax =
                new Vector2(1.0f, 1.0f);

            panel.pivot =
                new Vector2(1.0f, 1.0f);

            panel.anchoredPosition =
                new Vector2(-margin.x, -margin.y);

            panel.sizeDelta =
                new Vector2(panelWidth, 0.0f);

            RuntimeUi.Vertical(
                panel.gameObject,
                10,
                10.0f
            );

            ContentSizeFitter fitter =
                panel.gameObject.AddComponent<ContentSizeFitter>();

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            panel.gameObject.SetActive(
                false
            );
        }


        private void Refresh(
            IReadOnlyList<VisualizationLegendInfo> legends
        )
        {
            if (panel == null)
            {
                return;
            }

            RuntimeUi.ClearChildren(
                panel
            );

            ReleaseTextures();

            bool any =
                legends != null &&
                legends.Count > 0;

            panel.gameObject.SetActive(
                any
            );

            if (any)
            {
                BuildEntries(
                    panel,
                    legends
                );
            }
        }


        /// <summary>
        /// Adds one block per legend to a vertical-layout container.
        /// </summary>
        public void BuildEntries(
            RectTransform container,
            IReadOnlyList<VisualizationLegendInfo> legends
        )
        {
            foreach (VisualizationLegendInfo legend in legends)
            {
                BuildEntry(
                    container,
                    legend
                );
            }
        }


        private void BuildEntry(
            RectTransform container,
            VisualizationLegendInfo legend
        )
        {
            RectTransform block =
                RuntimeUi.CreateRect(
                    "Legend_" + legend.VariableId,
                    container
                );

            RuntimeUi.Vertical(
                block.gameObject,
                0,
                2.0f
            );


            RuntimeUi.CreateText(
                block,
                legend.Channel.ToUpperInvariant(),
                RuntimeUi.SmallSize,
                RuntimeUi.AccentColor,
                TextAnchor.MiddleLeft,
                FontStyles.Bold
            );

            string unit =
                string.IsNullOrWhiteSpace(legend.Unit) ||
                legend.IsCategorical
                    ? string.Empty
                    : $" ({legend.Unit})";

            RuntimeUi.CreateText(
                block,
                legend.VariableDisplayName + unit,
                RuntimeUi.BodySize,
                RuntimeUi.TextColor
            );


            if (legend.IsCategorical)
            {
                BuildCategories(
                    block,
                    legend
                );
            }
            else
            {
                BuildRamp(
                    block,
                    legend
                );

                BuildRange(
                    block,
                    legend
                );
            }


            string source =
                string.IsNullOrWhiteSpace(legend.SourceName)
                    ? legend.DataLayerId
                    : legend.SourceName;

            RuntimeUi.CreateText(
                block,
                $"{source} · {legend.ScaleDescription}",
                RuntimeUi.SmallSize,
                RuntimeUi.MutedColor
            );
        }


        private void BuildRamp(
            RectTransform block,
            VisualizationLegendInfo legend
        )
        {
            RectTransform rampRect =
                RuntimeUi.CreateRect(
                    "Ramp",
                    block
                );

            RawImage image =
                rampRect.gameObject.AddComponent<RawImage>();

            var texture =
                new Texture2D(
                    legend.Ramp.Count,
                    1,
                    TextureFormat.RGBA32,
                    false
                )
                {
                    wrapMode = TextureWrapMode.Clamp,
                    // Stepped scales must keep crisp class edges.
                    filterMode = legend.ScaleType == ScaleType.Quantile
                        ? FilterMode.Point
                        : FilterMode.Bilinear
                };

            var pixels =
                new Color32[legend.Ramp.Count];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] =
                    legend.Ramp[i];
            }

            texture.SetPixels32(
                pixels
            );

            texture.Apply(
                false,
                false
            );

            textures.Add(
                texture
            );

            image.texture =
                texture;

            image.raycastTarget =
                false;

            // A height legend is a wedge-like grey bar: thin at the
            // low end reads as "short", thick at the high end.
            RuntimeUi.Layout(
                rampRect.gameObject,
                -1.0f,
                legend.IsHeight ? 10.0f : 16.0f
            );
        }


        private void BuildRange(
            RectTransform block,
            VisualizationLegendInfo legend
        )
        {
            RectTransform row =
                RuntimeUi.CreateRect(
                    "Range",
                    block
                );

            RuntimeUi.Horizontal(
                row.gameObject,
                0,
                4.0f
            ).childForceExpandWidth =
                true;

            TMP_Text minimum =
                RuntimeUi.CreateText(
                    row,
                    Format(legend.Minimum) + (legend.IsHeight ? "  (low)" : string.Empty),
                    RuntimeUi.SmallSize,
                    RuntimeUi.TextColor
                );

            RuntimeUi.Layout(minimum.gameObject, -1.0f, -1.0f, 1.0f);

            // Rank scales are not linear in value: the middle of the
            // bar is the median, so label it.
            if (legend.Median.HasValue)
            {
                TMP_Text median =
                    RuntimeUi.CreateText(
                        row,
                        "median " + Format(legend.Median.Value),
                        RuntimeUi.SmallSize,
                        RuntimeUi.MutedColor,
                        TextAnchor.MiddleCenter
                    );

                RuntimeUi.Layout(median.gameObject, -1.0f, -1.0f, 1.0f);
            }

            TMP_Text maximum =
                RuntimeUi.CreateText(
                    row,
                    (legend.IsHeight ? "(tall)  " : string.Empty) + Format(legend.Maximum),
                    RuntimeUi.SmallSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleRight
                );

            RuntimeUi.Layout(maximum.gameObject, -1.0f, -1.0f, 1.0f);
        }


        private static void BuildCategories(
            RectTransform block,
            VisualizationLegendInfo legend
        )
        {
            foreach (LegendCategory category in legend.Categories)
            {
                RectTransform row =
                    RuntimeUi.CreateRect(
                        "Category",
                        block
                    );

                RuntimeUi.Horizontal(
                    row.gameObject,
                    0,
                    6.0f
                );

                Image chip =
                    RuntimeUi.CreatePanel(
                        "Chip",
                        row,
                        category.Color
                    );

                RuntimeUi.Layout(
                    chip.gameObject,
                    14.0f,
                    14.0f
                );

                TMP_Text label =
                    RuntimeUi.CreateText(
                        row,
                        category.Label,
                        RuntimeUi.SmallSize,
                        RuntimeUi.TextColor
                    );

                RuntimeUi.Layout(
                    label.gameObject,
                    -1.0f,
                    -1.0f,
                    1.0f
                );
            }
        }


        /// <summary>1 234 · 12.5 · 286 k · 1.2 M (invariant culture).</summary>
        public static string Format(
            double value
        )
        {
            double magnitude =
                System.Math.Abs(value);

            if (magnitude >= 1_000_000.0)
            {
                return (value / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + " M";
            }

            if (magnitude >= 10_000.0)
            {
                return (value / 1_000.0).ToString("0", CultureInfo.InvariantCulture) + " k";
            }

            if (magnitude >= 100.0)
            {
                return value.ToString("0", CultureInfo.InvariantCulture);
            }

            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }


        private void ReleaseTextures()
        {
            foreach (Texture2D texture in textures)
            {
                if (texture != null)
                {
                    Destroy(
                        texture
                    );
                }
            }

            textures.Clear();
        }
    }
}
