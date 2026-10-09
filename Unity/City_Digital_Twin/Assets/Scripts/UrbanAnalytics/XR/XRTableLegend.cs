using System.Collections.Generic;
using TMPro;
using UnityEngine;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Visualization;
using UrbanAnalytics.Visualization.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// The legend of what is on the table (view name, then one legend per
    /// encoded variable: colours and heights), standing at the table
    /// edge opposite the user, readable across the table. It stays with
    /// the table (follows the user around it, not grabbable), and is
    /// always shown. The selection's values are on the Info panel.
    /// </summary>
    public sealed class XRTableLegend :
        XRTablePanel
    {
        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private VisualizationManager visualizationManager;


        private TMP_Text viewTitle;

        private TMP_Text placeholder;


        public override string PanelName =>
            "Legend";


        public XRTableLegend()
        {
            // 460 × 420 units at 2 mm = 0.92 × 0.84 m; body text about 3 cm.
            Configure(
                PanelPlacement.FarEdge,
                new Vector2(460.0f, 420.0f),
                0.002f,
                0.10f + 0.42f,
                0.0f,
                true,
                false
            );
        }


        protected override void Awake()
        {
            if (visualizationSwitcher == null)
            {
                visualizationSwitcher =
                    FindFirstObjectByType<VisualizationSwitcher>();
            }

            if (visualizationManager == null)
            {
                visualizationManager =
                    FindFirstObjectByType<VisualizationManager>();
            }

            base.Awake();
        }


        private void OnEnable()
        {
            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed +=
                    Refresh;
            }

            if (visualizationManager != null)
            {
                visualizationManager.LegendsChanged +=
                    HandleLegendsChanged;
            }
        }


        private void Start()
        {
            Refresh();
        }


        private void OnDisable()
        {
            if (visualizationSwitcher != null)
            {
                visualizationSwitcher.Changed -=
                    Refresh;
            }

            if (visualizationManager != null)
            {
                visualizationManager.LegendsChanged -=
                    HandleLegendsChanged;
            }
        }


        protected override void BuildContent(
            Transform panel
        )
        {
            CreateHeader(
                panel,
                "Legend",
                false
            );

            viewTitle =
                RuntimeUi.CreateText(
                    panel,
                    string.Empty,
                    RuntimeUi.HeadingSize,
                    RuntimeUi.TextColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            placeholder =
                RuntimeUi.CreateText(
                    panel,
                    "No data on the table.\nPress  View >  on the table toolbar.",
                    RuntimeUi.BodySize,
                    RuntimeUi.MutedColor
                );


            RectTransform legendContainer =
                RuntimeUi.CreateRect(
                    "Legends",
                    panel
                );

            RuntimeUi.Vertical(
                legendContainer.gameObject,
                0,
                10.0f
            );

            RuntimeUi.Layout(
                legendContainer.gameObject,
                flexibleHeight: 1.0f
            );


            // A legend view of its own; it shares the visualization
            // manager with the desktop legend.
            var legendObject =
                new GameObject("XRTableLegendView");

            legendObject.SetActive(false);

            legendObject.transform.SetParent(
                transform,
                false
            );

            legendObject.AddComponent<LegendStackView>().Container =
                legendContainer;

            legendObject.SetActive(true);
        }


        private void HandleLegendsChanged(
            IReadOnlyList<VisualizationLegendInfo> legends
        )
        {
            Refresh();
        }


        private void Refresh()
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
                shown ?? string.Empty;

            viewTitle.gameObject.SetActive(
                shown != null
            );

            bool anyLegend =
                visualizationManager != null &&
                visualizationManager.ActiveLegends != null &&
                visualizationManager.ActiveLegends.Count > 0;

            placeholder.gameObject.SetActive(
                shown == null && !anyLegend
            );
        }
    }
}
