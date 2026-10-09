using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using UrbanAnalytics.Interaction.UI;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Something the user can pick up with a controller ray + grip and
    /// put somewhere else (XRGrabber does the grabbing): copies and the
    /// VR panels (by their "Grab" bar).
    ///
    /// Only the grab collider is needed, on the "Ignore Raycast" layer
    /// (<see cref="Layer"/>): city picking never hits it, and
    /// XRControllerPointer treats a ray on it like a ray on a panel.
    ///
    /// Panels (<see cref="KeepOutOfTable"/>) cannot be put into the
    /// table: XRGrabber pushes them out (TabletopRig.KeepOutOfTable).
    /// </summary>
    public sealed class XRGrabbable :
        MonoBehaviour
    {
        /// <summary>Unity's "Ignore Raycast" layer.</summary>
        public const int Layer =
            2;

        /// <summary>Label of a panel's grab bar.</summary>
        public const string HandleLabel =
            "Grab";

        /// <summary>Canvas units the grab bar adds below a panel (gap + bar).</summary>
        private const float HandleExtraUnits =
            44.0f;


        [Tooltip("While dragged, turn about the vertical axis to face the user (panels).")]
        [SerializeField]
        private bool faceUser;

        [Tooltip("Pushed out of the table when let go there (panels).")]
        [SerializeField]
        private bool keepOutOfTable;


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


        public bool KeepOutOfTable
        {
            get => keepOutOfTable;
            set => keepOutOfTable = value;
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
        /// Half width and half height in world units of the panel this
        /// sits on (its RectTransform, plus the grab bar below). False
        /// for objects without a RectTransform.
        /// </summary>
        public bool TryGetPanelExtents(
            out float halfWidth,
            out float halfHeight
        )
        {
            halfWidth =
                0.0f;

            halfHeight =
                0.0f;

            if (!(transform is RectTransform rect))
            {
                return false;
            }

            Vector3 scale =
                rect.lossyScale;

            halfWidth =
                0.5f * rect.rect.width * Mathf.Abs(scale.x);

            halfHeight =
                0.5f * (rect.rect.height + 2.0f * HandleExtraUnits) * Mathf.Abs(scale.y);

            return true;
        }


        /// <summary>
        /// Adds a "Grab" bar (with a grab collider) below a world-space
        /// panel canvas and makes the canvas grabbable by it; the panel
        /// is kept out of the table. Sizes are canvas units.
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
                    "GrabHandle",
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
                    HandleLabel,
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

            grabbable.keepOutOfTable =
                true;

            return grabbable;
        }
    }
}
