using System;
using UnityEngine;
using UnityEngine.UI;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Displays the active color visualization as a simple
    /// runtime legend.
    ///
    /// The view listens to VisualizationManager and does not
    /// query analytical data directly.
    /// </summary>
    public sealed class VisualizationLegendView : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private VisualizationManager visualizationManager;


        [Header("UI")]
        [SerializeField]
        private GameObject contentRoot;

        [SerializeField]
        private Text titleText;

        [SerializeField]
        private Text unitText;

        [SerializeField]
        private Text minimumText;

        [SerializeField]
        private Text maximumText;

        [SerializeField]
        private RawImage gradientImage;


        [Header("Formatting")]
        [SerializeField]
        [Min(16)]
        private int gradientTextureWidth = 256;

        [SerializeField]
        [Range(0, 4)]
        private int decimalPlaces = 0;


        private Texture2D gradientTexture;


        // =========================================================
        // UNITY LIFECYCLE
        // =========================================================

        private void Awake()
        {
            ResolveDependencies();


            if (visualizationManager == null)
            {
                Debug.LogError(
                    "VisualizationLegendView could not find " +
                    "VisualizationManager.",
                    this
                );

                enabled = false;
                return;
            }


            SetVisible(
                false
            );
        }


        private void OnEnable()
        {
            if (visualizationManager == null)
            {
                return;
            }


            visualizationManager.VisualizationChanged +=
                HandleVisualizationChanged;

            visualizationManager.VisualizationCleared +=
                HandleVisualizationCleared;


            if (visualizationManager.ActiveLegend != null)
            {
                Refresh(
                    visualizationManager.ActiveLegend
                );
            }
        }


        private void OnDisable()
        {
            if (visualizationManager == null)
            {
                return;
            }


            visualizationManager.VisualizationChanged -=
                HandleVisualizationChanged;

            visualizationManager.VisualizationCleared -=
                HandleVisualizationCleared;
        }


        private void OnDestroy()
        {
            if (gradientTexture != null)
            {
                Destroy(
                    gradientTexture
                );

                gradientTexture =
                    null;
            }
        }


        // =========================================================
        // EVENTS
        // =========================================================

        private void HandleVisualizationChanged(
            VisualizationLegendInfo info
        )
        {
            Refresh(
                info
            );
        }


        private void HandleVisualizationCleared()
        {
            SetVisible(
                false
            );
        }


        // =========================================================
        // REFRESH
        // =========================================================

        private void Refresh(
            VisualizationLegendInfo info
        )
        {
            if (info == null)
            {
                SetVisible(
                    false
                );

                return;
            }


            if (titleText != null)
            {
                titleText.text =
                    info.VariableDisplayName;
            }


            if (unitText != null)
            {
                unitText.text =
                    info.Unit;
            }


            if (minimumText != null)
            {
                minimumText.text =
                    FormatNumber(
                        info.Minimum
                    );
            }


            if (maximumText != null)
            {
                maximumText.text =
                    FormatNumber(
                        info.Maximum
                    );
            }


            UpdateGradientTexture(
                info
            );


            SetVisible(
                true
            );
        }


        // =========================================================
        // GRADIENT
        // =========================================================

        private void UpdateGradientTexture(
            VisualizationLegendInfo info
        )
        {
            if (gradientImage == null)
            {
                return;
            }


            int width =
                Mathf.Max(
                    16,
                    gradientTextureWidth
                );


            if (gradientTexture == null ||
                gradientTexture.width != width)
            {
                if (gradientTexture != null)
                {
                    Destroy(
                        gradientTexture
                    );
                }


                gradientTexture =
                    new Texture2D(
                        width,
                        1,
                        TextureFormat.RGBA32,
                        false,
                        false
                    );


                gradientTexture.name =
                    "RuntimeVisualizationLegendGradient";

                gradientTexture.wrapMode =
                    TextureWrapMode.Clamp;

                gradientTexture.filterMode =
                    FilterMode.Bilinear;


                gradientImage.texture =
                    gradientTexture;
            }


            for (
                int x = 0;
                x < width;
                x++
            )
            {
                float t =
                    width == 1
                        ? 0.0f
                        : x /
                          (float)(width - 1);


                if (info.ReverseGradient)
                {
                    t =
                        1.0f - t;
                }


                Color color =
                    info.Gradient.Evaluate(
                        t
                    );


                gradientTexture.SetPixel(
                    x,
                    0,
                    color
                );
            }


            gradientTexture.Apply(
                false,
                false
            );
        }


        // =========================================================
        // FORMATTING
        // =========================================================

        private string FormatNumber(
            double value
        )
        {
            if (Math.Abs(value) >= 1000000.0)
            {
                return (
                    value / 1000000.0
                ).ToString(
                    $"F{decimalPlaces}"
                ) + "M";
            }


            if (Math.Abs(value) >= 1000.0)
            {
                return (
                    value / 1000.0
                ).ToString(
                    $"F{decimalPlaces}"
                ) + "k";
            }


            return value.ToString(
                $"F{decimalPlaces}"
            );
        }


        // =========================================================
        // VISIBILITY
        // =========================================================

        private void SetVisible(
            bool visible
        )
        {
            if (contentRoot != null)
            {
                contentRoot.SetActive(
                    visible
                );
            }
        }


        // =========================================================
        // DEPENDENCIES
        // =========================================================

        private void ResolveDependencies()
        {
            if (visualizationManager == null)
            {
                visualizationManager =
                    FindFirstObjectByType<VisualizationManager>();
            }
        }
    }
}