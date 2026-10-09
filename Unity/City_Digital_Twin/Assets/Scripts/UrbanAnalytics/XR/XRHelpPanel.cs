using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Help as a one-page brochure: six picture cards with a short title
    /// and one line each (controllers, table toolbar, click tools,
    /// panels, wrist menu, moving the table). Opened from the wrist
    /// menu (Panels → Help) and at the end of the tutorial; it appears
    /// in front of the user and can be carried by its Grab bar.
    ///
    /// The pictures are drawn by Tools/make_vr_help_images.ps1 into
    /// Assets/Textures/UrbanAnalytics/VR/help_*.png and assigned by the
    /// VR scene builder; a card without a picture shows its text only.
    /// </summary>
    public sealed class XRHelpPanel :
        XRTablePanel
    {
        [Serializable]
        public sealed class Card
        {
            public string Title;

            [TextArea]
            public string Caption;

            public Texture2D Image;
        }


        /// <summary>Card titles, captions and picture file names (scene builder).</summary>
        public static readonly (string Title, string Caption, string ImageName)[] DefaultCards =
        {
            ("Controllers", "Trigger clicks. Grip grabs.", "help_controllers"),
            ("Table toolbar", "Every button changes the table.", "help_toolbar"),
            ("Click tools", "Choose what the trigger does: Select, Copy or Compare.", "help_tools"),
            ("Panels", "Info beside you, Legend at the table, Compare on your left.", "help_panels"),
            ("Wrist menu", "Look at your left wrist: Views, Task, Panels.", "help_wrist"),
            ("Move the table", "Hold left grip and move. Right stick: size.", "help_table")
        };


        [Header("Cards")]
        [SerializeField]
        private Card[] cards =
            Array.Empty<Card>();

        [SerializeField]
        [Range(1, 4)]
        private int columns =
            3;


        public override string PanelName =>
            "Help";


        public XRHelpPanel()
        {
            // 1100 × 780 units at 0.8 mm = 0.88 × 0.62 m, in front of the user.
            Configure(
                PanelPlacement.InFront,
                new Vector2(1100.0f, 780.0f),
                0.0008f,
                0.0f,
                0.0f,
                false,
                true
            );
        }


        protected override void BuildContent(
            Transform panel
        )
        {
            CreateHeader(
                panel,
                "Help",
                true
            );


            RectTransform grid =
                RuntimeUi.CreateRect(
                    "Cards",
                    panel
                );

            GridLayoutGroup layout =
                grid.gameObject.AddComponent<GridLayoutGroup>();

            layout.cellSize =
                new Vector2(340.0f, 320.0f);

            layout.spacing =
                new Vector2(14.0f, 14.0f);

            layout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;

            layout.constraintCount =
                columns;

            layout.childAlignment =
                TextAnchor.UpperCenter;

            RuntimeUi.Layout(
                grid.gameObject,
                flexibleHeight: 1.0f
            );


            Card[] shown =
                cards != null && cards.Length > 0
                    ? cards
                    : DefaultCardsWithoutImages();

            foreach (Card card in shown)
            {
                CreateCard(
                    grid,
                    card
                );
            }
        }


        private static Card[] DefaultCardsWithoutImages()
        {
            var result =
                new Card[DefaultCards.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] =
                    new Card
                    {
                        Title = DefaultCards[i].Title,
                        Caption = DefaultCards[i].Caption
                    };
            }

            return result;
        }


        /// <summary>A picture with a bold title and one line below it.</summary>
        internal static void CreateCard(
            Transform parent,
            Card card
        )
        {
            Image frame =
                RuntimeUi.CreatePanel(
                    "Card_" + card.Title,
                    parent,
                    RuntimeUi.ButtonColor
                );

            frame.raycastTarget =
                false;

            RuntimeUi.Vertical(
                frame.gameObject,
                8,
                6.0f
            );


            if (card.Image != null)
            {
                RectTransform imageRect =
                    RuntimeUi.CreateRect(
                        "Picture",
                        frame.transform
                    );

                RawImage image =
                    imageRect.gameObject.AddComponent<RawImage>();

                image.texture =
                    card.Image;

                image.raycastTarget =
                    false;

                // 800 × 500 pictures in a 324-unit-wide card.
                RuntimeUi.Layout(
                    imageRect.gameObject,
                    -1.0f,
                    324.0f * card.Image.height / Mathf.Max(1, card.Image.width)
                );
            }


            RuntimeUi.CreateText(
                frame.transform,
                card.Title,
                RuntimeUi.HeadingSize,
                RuntimeUi.AccentColor,
                TextAnchor.MiddleLeft,
                FontStyles.Bold
            );

            RuntimeUi.CreateText(
                frame.transform,
                card.Caption,
                RuntimeUi.SmallSize,
                RuntimeUi.TextColor
            );
        }
    }
}
