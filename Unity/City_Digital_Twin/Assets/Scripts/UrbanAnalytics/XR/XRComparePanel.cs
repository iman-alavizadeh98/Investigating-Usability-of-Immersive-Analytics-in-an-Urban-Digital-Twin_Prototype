using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// The comparison of two areas (ComparisonManager slots A and B):
    /// a small 3D view of each, then their values side by side with the
    /// difference B − A. Filled with the toolbar's Compare tool (one
    /// click per area; XRClickTools). Stands at the user's left by the
    /// table, opens by itself when something is compared, and can be
    /// carried anywhere by its Grab bar. Unrelated to copies.
    /// </summary>
    public sealed class XRComparePanel :
        XRTablePanel
    {
        private const float ButtonHeight =
            40.0f;


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private ComparisonManager comparisonManager;


        private readonly RawImage[] slotImages =
            new RawImage[2];

        private readonly TMP_Text[] slotTitles =
            new TMP_Text[2];

        private TMP_Text placeholder;

        private GameObject body;

        private RectTransform table;


        public override string PanelName =>
            "Compare";


        public XRComparePanel()
        {
            // 600 × 660 units at 0.8 mm = 0.48 × 0.53 m, to the user's left.
            Configure(
                PanelPlacement.NearSide,
                new Vector2(600.0f, 660.0f),
                0.0008f,
                0.33f,
                -0.95f,
                false,
                true
            );
        }


        protected override void Awake()
        {
            if (comparisonManager == null)
            {
                comparisonManager =
                    FindFirstObjectByType<ComparisonManager>();
            }

            base.Awake();
        }


        private void OnEnable()
        {
            if (comparisonManager != null)
            {
                comparisonManager.Changed +=
                    Refresh;
            }
        }


        private void Start()
        {
            Refresh();
        }


        private void OnDisable()
        {
            if (comparisonManager != null)
            {
                comparisonManager.Changed -=
                    Refresh;
            }
        }


        protected override void BuildContent(
            Transform panel
        )
        {
            CreateHeader(
                panel,
                "Compare",
                true
            );

            placeholder =
                RuntimeUi.CreateText(
                    panel,
                    "Choose  Compare  on the table toolbar,\nthen click two areas on the table.",
                    RuntimeUi.BodySize,
                    RuntimeUi.MutedColor
                );


            RectTransform bodyRect =
                RuntimeUi.CreateRect(
                    "Body",
                    panel
                );

            RuntimeUi.Vertical(
                bodyRect.gameObject,
                0,
                8.0f
            );

            RuntimeUi.Layout(
                bodyRect.gameObject,
                flexibleHeight: 1.0f
            );

            body =
                bodyRect.gameObject;


            RectTransform views =
                RuntimeUi.CreateRect(
                    "Views",
                    bodyRect
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
                    200.0f
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
                    "Remove",
                    () => comparisonManager?.ClearSlot(slot),
                    -1.0f,
                    ButtonHeight
                );
            }


            table =
                CreateScroll(
                    bodyRect,
                    out _
                );
        }


        private void Refresh()
        {
            if (table == null)
            {
                return;
            }


            bool any =
                comparisonManager != null &&
                comparisonManager.Slots != null &&
                comparisonManager.Slots.Count >= 2 &&
                comparisonManager.HasAnyFilled;

            placeholder.gameObject.SetActive(!any);

            body.SetActive(any);

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
                        : RuntimeUi.Colorize("click an area", RuntimeUi.MutedColor));

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
                table
            );


            ComparisonManager.Slot a =
                slots[0];

            ComparisonManager.Slot b =
                slots[1];


            CreateRow(
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

                CreateRow(
                    label,
                    DesktopInteractionUI.CompareCell(rowA, a.IsFilled),
                    DesktopInteractionUI.CompareCell(rowB, b.IsFilled),
                    DesktopInteractionUI.Difference(rowA, rowB),
                    RuntimeUi.TextColor
                );
            }
        }


        /// <summary>
        /// One value row: the label on its own line, then A | B | B − A
        /// in three equal columns.
        /// </summary>
        private void CreateRow(
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
                    table
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
    }
}
