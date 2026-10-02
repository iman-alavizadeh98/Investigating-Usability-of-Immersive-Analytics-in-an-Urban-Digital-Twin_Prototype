using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UrbanAnalytics.Interaction.UI
{
    /// <summary>
    /// Small factory for uGUI elements built from code, so the
    /// interaction UI needs no prefabs and stays reproducible
    /// from source.
    ///
    /// Text uses TextMeshPro (signed-distance-field fonts), which
    /// stays sharp at any canvas scale; the legacy bitmap Text
    /// blurs whenever the UI is scaled.
    /// </summary>
    internal static class RuntimeUi
    {
        // Font sizes in canvas units (pixels at UI scale 1).
        public const float TitleSize = 21.0f;
        public const float HeadingSize = 18.0f;
        public const float BodySize = 16.0f;
        public const float SmallSize = 14.0f;


        public static readonly Color PanelColor =
            new Color(0.07f, 0.08f, 0.10f, 0.94f);

        public static readonly Color TextColor =
            new Color(0.95f, 0.96f, 0.97f, 1.0f);

        public static readonly Color MutedColor =
            new Color(0.66f, 0.69f, 0.74f, 1.0f);

        public static readonly Color AccentColor =
            new Color(1.0f, 0.83f, 0.0f, 1.0f);

        public static readonly Color ErrorColor =
            new Color(1.0f, 0.48f, 0.42f, 1.0f);

        public static readonly Color ButtonColor =
            new Color(0.21f, 0.23f, 0.27f, 1.0f);

        public static readonly Color ActiveButtonColor =
            new Color(0.58f, 0.47f, 0.05f, 1.0f);


        private static TMP_FontAsset font;


        /// <summary>
        /// TMP default font (LiberationSans SDF from the TMP
        /// Essential Resources in Assets/TextMesh Pro).
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null)
                {
                    font =
                        TMP_Settings.defaultFontAsset != null
                            ? TMP_Settings.defaultFontAsset
                            : Resources.Load<TMP_FontAsset>(
                                "Fonts & Materials/LiberationSans SDF"
                            );


                    if (font == null)
                    {
                        Debug.LogError(
                            "RuntimeUi: no TextMeshPro font. Import " +
                            "Window > TextMeshPro > Import TMP " +
                            "Essential Resources."
                        );
                    }
                }

                return font;
            }
        }


        public static RectTransform CreateRect(
            string name,
            Transform parent
        )
        {
            var gameObject =
                new GameObject(
                    name,
                    typeof(RectTransform)
                );


            gameObject.transform.SetParent(
                parent,
                false
            );


            return (RectTransform)gameObject.transform;
        }


        public static Image CreatePanel(
            string name,
            Transform parent,
            Color color
        )
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent
                );


            Image image =
                rect.gameObject.AddComponent<Image>();


            image.color =
                color;


            return image;
        }


        public static TMP_Text CreateText(
            Transform parent,
            string text,
            float size,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyles style = FontStyles.Normal
        )
        {
            RectTransform rect =
                CreateRect(
                    "Text",
                    parent
                );


            TextMeshProUGUI label =
                rect.gameObject.AddComponent<TextMeshProUGUI>();


            label.font =
                Font;

            label.fontSize =
                size;

            label.color =
                color;

            label.fontStyle =
                style;

            label.richText =
                true;

            label.textWrappingMode =
                TextWrappingModes.Normal;

            label.overflowMode =
                TextOverflowModes.Overflow;

            label.raycastTarget =
                false;

            label.text =
                text;


            SetAlignment(
                label,
                alignment
            );


            return label;
        }


        public static void SetAlignment(
            TMP_Text text,
            TextAnchor alignment
        )
        {
            switch (alignment)
            {
                case TextAnchor.MiddleCenter:
                case TextAnchor.UpperCenter:
                case TextAnchor.LowerCenter:
                    text.alignment =
                        TextAlignmentOptions.Center;
                    break;

                case TextAnchor.MiddleRight:
                case TextAnchor.UpperRight:
                case TextAnchor.LowerRight:
                    text.alignment =
                        TextAlignmentOptions.Right;
                    break;

                default:
                    text.alignment =
                        TextAlignmentOptions.Left;
                    break;
            }
        }


        public static Button CreateButton(
            Transform parent,
            string label,
            UnityAction onClick,
            float width = -1.0f,
            float height = 32.0f,
            float fontSize = BodySize
        )
        {
            Image image =
                CreatePanel(
                    $"Button_{label}",
                    parent,
                    ButtonColor
                );


            Button button =
                image.gameObject.AddComponent<Button>();


            ColorBlock colors =
                button.colors;

            colors.normalColor =
                Color.white;

            colors.highlightedColor =
                new Color(1.3f, 1.3f, 1.3f, 1.0f);

            colors.pressedColor =
                new Color(0.8f, 0.8f, 0.8f, 1.0f);

            colors.colorMultiplier =
                1.5f;

            button.colors =
                colors;


            if (onClick != null)
            {
                button.onClick.AddListener(
                    onClick
                );
            }


            TMP_Text text =
                CreateText(
                    image.transform,
                    label,
                    fontSize,
                    TextColor,
                    TextAnchor.MiddleCenter
                );


            text.textWrappingMode =
                TextWrappingModes.NoWrap;


            Stretch(
                text.rectTransform,
                8.0f
            );


            Layout(
                image.gameObject,
                width,
                height
            );


            return button;
        }


        public static void SetButton(
            Button button,
            string label,
            Color color
        )
        {
            TMP_Text text =
                button.GetComponentInChildren<TMP_Text>();


            if (text != null)
            {
                text.text =
                    label;
            }


            button.GetComponent<Image>().color =
                color;
        }


        public static VerticalLayoutGroup Vertical(
            GameObject target,
            int padding,
            float spacing
        )
        {
            VerticalLayoutGroup group =
                target.AddComponent<VerticalLayoutGroup>();


            group.padding =
                new RectOffset(
                    padding,
                    padding,
                    padding,
                    padding
                );

            group.spacing =
                spacing;

            group.childControlWidth =
                true;

            group.childControlHeight =
                true;

            group.childForceExpandWidth =
                true;

            group.childForceExpandHeight =
                false;


            return group;
        }


        public static HorizontalLayoutGroup Horizontal(
            GameObject target,
            int padding,
            float spacing
        )
        {
            HorizontalLayoutGroup group =
                target.AddComponent<HorizontalLayoutGroup>();


            group.padding =
                new RectOffset(
                    padding,
                    padding,
                    padding,
                    padding
                );

            group.spacing =
                spacing;

            group.childControlWidth =
                true;

            group.childControlHeight =
                true;

            group.childForceExpandWidth =
                false;

            group.childForceExpandHeight =
                false;

            group.childAlignment =
                TextAnchor.MiddleLeft;


            return group;
        }


        public static LayoutElement Layout(
            GameObject target,
            float preferredWidth = -1.0f,
            float preferredHeight = -1.0f,
            float flexibleWidth = -1.0f,
            float flexibleHeight = -1.0f
        )
        {
            LayoutElement element =
                target.GetComponent<LayoutElement>();


            if (element == null)
            {
                element =
                    target.AddComponent<LayoutElement>();
            }


            element.preferredWidth =
                preferredWidth;

            element.preferredHeight =
                preferredHeight;

            element.flexibleWidth =
                flexibleWidth;

            element.flexibleHeight =
                flexibleHeight;


            if (preferredWidth >= 0.0f)
            {
                element.minWidth =
                    preferredWidth;
            }


            if (preferredHeight >= 0.0f)
            {
                element.minHeight =
                    preferredHeight;
            }


            return element;
        }


        /// <summary>
        /// Vertical scroll area; returns the content transform
        /// (vertical layout, grows with its children).
        /// </summary>
        public static RectTransform CreateScrollView(
            Transform parent,
            out ScrollRect scrollRect
        )
        {
            Image background =
                CreatePanel(
                    "Scroll",
                    parent,
                    new Color(0.0f, 0.0f, 0.0f, 0.18f)
                );


            scrollRect =
                background.gameObject.AddComponent<ScrollRect>();


            RectTransform viewport =
                CreateRect(
                    "Viewport",
                    background.transform
                );


            Stretch(
                viewport,
                0.0f
            );


            viewport.gameObject.AddComponent<RectMask2D>();


            // An invisible graphic so the whole viewport receives
            // wheel/drag events.
            Image viewportImage =
                viewport.gameObject.AddComponent<Image>();

            viewportImage.color =
                new Color(0.0f, 0.0f, 0.0f, 0.0f);


            RectTransform content =
                CreateRect(
                    "Content",
                    viewport
                );


            content.anchorMin =
                new Vector2(0.0f, 1.0f);

            content.anchorMax =
                new Vector2(1.0f, 1.0f);

            content.pivot =
                new Vector2(0.5f, 1.0f);

            content.anchoredPosition =
                Vector2.zero;

            content.sizeDelta =
                Vector2.zero;


            Vertical(
                content.gameObject,
                8,
                3.0f
            );


            ContentSizeFitter fitter =
                content.gameObject.AddComponent<ContentSizeFitter>();


            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;


            scrollRect.viewport =
                viewport;

            scrollRect.content =
                content;

            scrollRect.horizontal =
                false;

            scrollRect.vertical =
                true;

            scrollRect.movementType =
                ScrollRect.MovementType.Clamped;

            scrollRect.scrollSensitivity =
                30.0f;


            return content;
        }


        public static void Anchor(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size
        )
        {
            rect.anchorMin =
                anchor;

            rect.anchorMax =
                anchor;

            rect.pivot =
                anchor;

            rect.anchoredPosition =
                anchoredPosition;

            rect.sizeDelta =
                size;
        }


        public static void Stretch(
            RectTransform rect,
            float inset
        )
        {
            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.offsetMin =
                new Vector2(inset, 0.0f);

            rect.offsetMax =
                new Vector2(-inset, 0.0f);
        }


        public static void ClearChildren(
            Transform parent
        )
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child =
                    parent.GetChild(i).gameObject;


                // Detach first so layout ignores it this frame.
                child.transform.SetParent(
                    null,
                    false
                );

                Object.Destroy(
                    child
                );
            }
        }


        public static string Colorize(
            string text,
            Color color
        )
        {
            return
                $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>" +
                $"{text}</color>";
        }
    }
}
