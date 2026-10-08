using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Something the user can pick up with a controller ray + grip or a
    /// hand ray + pinch and put somewhere else (XRGrabber does the
    /// grabbing): pop-out copies and the VR panels (via their "Move"
    /// handle).
    ///
    /// Only the grab collider is needed, on the "Ignore Raycast" layer
    /// (<see cref="Layer"/>): city picking never hits it, and
    /// XRControllerPointer treats a ray on it like a ray on a panel.
    /// </summary>
    public sealed class XRGrabbable :
        MonoBehaviour
    {
        /// <summary>Unity's "Ignore Raycast" layer.</summary>
        public const int Layer =
            2;


        [Tooltip("While dragged, turn about the vertical axis to face the user (panels).")]
        [SerializeField]
        private bool faceUser;


        /// <summary>
        /// True once the user has moved it; panels then stop following
        /// their automatic placement until <see cref="ResetPlacement"/>.
        /// </summary>
        public bool IsUserPlaced
        {
            get;
            private set;
        }


        public bool FaceUser
        {
            get => faceUser;
            set => faceUser = value;
        }


        public event Action Grabbed;

        public event Action Released;


        internal void NotifyGrabbed()
        {
            IsUserPlaced =
                true;

            Grabbed?.Invoke();
        }


        internal void NotifyReleased()
        {
            Released?.Invoke();
        }


        /// <summary>Back to automatic placement (panels).</summary>
        public void ResetPlacement()
        {
            IsUserPlaced =
                false;
        }


        /// <summary>
        /// Adds a "Move" handle (a bar with a grab collider) below a
        /// world-space panel canvas and makes the canvas grabbable by it.
        /// Sizes are canvas units.
        /// </summary>
        public static XRGrabbable AddPanelHandle(
            RectTransform canvasRoot,
            float widthUnits = 220.0f,
            float heightUnits = 34.0f,
            float gapUnits = 10.0f
        )
        {
            RectTransform handle =
                RuntimeUi.CreateRect(
                    "MoveHandle",
                    canvasRoot
                );

            handle.anchorMin =
                new Vector2(0.5f, 0.0f);

            handle.anchorMax =
                new Vector2(0.5f, 0.0f);

            handle.pivot =
                new Vector2(0.5f, 1.0f);

            handle.anchoredPosition =
                new Vector2(0.0f, -gapUnits);

            handle.sizeDelta =
                new Vector2(widthUnits, heightUnits);


            Image bar =
                handle.gameObject.AddComponent<Image>();

            bar.color =
                RuntimeUi.ButtonColor;

            bar.raycastTarget =
                false;


            TMP_Text label =
                RuntimeUi.CreateText(
                    handle,
                    "Move  (grip / pinch)",
                    RuntimeUi.SmallSize,
                    RuntimeUi.MutedColor,
                    TextAnchor.MiddleCenter
                );

            RuntimeUi.Stretch(label.rectTransform, 0.0f);

            label.rectTransform.offsetMin =
                Vector2.zero;

            label.rectTransform.offsetMax =
                Vector2.zero;

            label.raycastTarget =
                false;


            handle.gameObject.layer =
                Layer;

            BoxCollider box =
                handle.gameObject.AddComponent<BoxCollider>();

            // The pivot is the top centre: the box hangs below it.
            box.center =
                new Vector3(0.0f, -0.5f * heightUnits, 0.0f);

            box.size =
                new Vector3(widthUnits, heightUnits, 20.0f);


            if (!canvasRoot.TryGetComponent(out XRGrabbable grabbable))
            {
                grabbable =
                    canvasRoot.gameObject.AddComponent<XRGrabbable>();
            }

            grabbable.faceUser =
                true;

            return grabbable;
        }
    }
}
