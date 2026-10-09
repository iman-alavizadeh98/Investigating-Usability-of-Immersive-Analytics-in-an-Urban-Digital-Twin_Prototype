using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Base of the VR panels around the table (Legend, Info, Compare,
    /// Help, Tutorial): a world-space canvas in the scaled XR Origin's
    /// space (real metres, so it keeps its size when the table is
    /// resized), operated with the controller ray (trigger = press,
    /// thumbstick = scroll).
    ///
    /// Placement (until the user moves it by its Grab bar, if it has
    /// one; <see cref="ResetPlacement"/> brings it back):
    ///   FarEdge  — standing beyond the table edge opposite the user;
    ///   NearSide — at the user's side of the table, beside them,
    ///              turned toward their head;
    ///   InFront  — in front of the eyes, placed once when shown.
    /// FarEdge / NearSide follow the user around the table, but only once
    /// they are more than <see cref="repositionDegrees"/> away (no
    /// drifting while looking around). <see cref="sideOffsetMeters"/>
    /// shifts the panel to the user's right (+) or left (−).
    ///
    /// A panel is never placed inside the table
    /// (TabletopRig.KeepOutOfTable); XRGrabber does the same while it is
    /// carried.
    ///
    /// Showing / hiding is logged as the study event vr_panel.
    /// </summary>
    public abstract class XRTablePanel :
        MonoBehaviour
    {
        public enum PanelPlacement
        {
            FarEdge,
            NearSide,
            InFront
        }


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Placement (real metres)")]
        [SerializeField]
        protected TabletopRig tabletopRig;

        [SerializeField]
        protected Camera viewCamera;

        [SerializeField]
        private PanelPlacement placement =
            PanelPlacement.NearSide;

        [Tooltip("Distance from the table edge (FarEdge / NearSide).")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float edgeSetbackMeters =
            0.15f;

        [Tooltip("Height of the panel's centre above the table top.")]
        [SerializeField]
        [Range(-0.5f, 1.5f)]
        private float centerAboveTableMeters =
            0.35f;

        [Tooltip("Shift along the edge: + to the user's right, − to the left.")]
        [SerializeField]
        [Range(-2.0f, 2.0f)]
        private float sideOffsetMeters;

        [Tooltip("InFront: distance from the eyes.")]
        [SerializeField]
        [Range(0.3f, 2.0f)]
        private float frontDistanceMeters =
            0.75f;

        [Tooltip("The panel moves when the user is this far around the table.")]
        [SerializeField]
        [Range(10.0f, 120.0f)]
        private float repositionDegrees =
            50.0f;

        [Tooltip("How fast the panel slides to its new place (1/s).")]
        [SerializeField]
        [Range(0.5f, 10.0f)]
        private float moveSharpness =
            3.0f;

        [Tooltip("Panels stay this high above the table top while over it.")]
        [SerializeField]
        [Range(0.0f, 0.6f)]
        private float tableClearanceMeters =
            0.25f;


        [Header("Canvas")]
        [Tooltip("Real metres per canvas unit (0.001 = 1 mm).")]
        [SerializeField]
        [Range(0.0003f, 0.005f)]
        private float metersPerUnit =
            0.0008f;

        [SerializeField]
        private Vector2 sizeUnits =
            new Vector2(520.0f, 600.0f);

        [Tooltip("Draws after the selection highlight (Transparent+10).")]
        [SerializeField]
        private int sortingOrder =
            106;

        [SerializeField]
        private bool startVisible =
            true;

        [Tooltip("Adds a Grab bar so the user can carry the panel.")]
        [SerializeField]
        private bool movable =
            true;


        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        protected StudySession studySession;


        // =========================================================
        // RUNTIME
        // =========================================================

        private GameObject root;

        private XRGrabbable grabbable;

        private bool wantVisible;

        private bool hasBearing;

        private float bearing;

        private float targetBearing;

        private bool placeInFront;


        public bool IsVisible =>
            wantVisible;

        /// <summary>The canvas root (tests, tutorial highlights).</summary>
        public GameObject Root =>
            root;

        /// <summary>Name used in logs and on the wrist menu.</summary>
        public abstract string PanelName
        {
            get;
        }


        public event System.Action<bool> VisibilityChanged;


        // =========================================================
        // CONFIGURATION (scene builder / subclasses)
        // =========================================================

        /// <summary>
        /// Sets the defaults of a panel type. Called from the subclass
        /// constructor, so these act like field initialisers: values
        /// saved in the scene override them.
        /// </summary>
        protected void Configure(
            PanelPlacement where,
            Vector2 size,
            float unitMeters,
            float centerHeight,
            float side,
            bool visibleAtStart,
            bool canMove
        )
        {
            placement = where;
            sizeUnits = size;
            metersPerUnit = unitMeters;
            centerAboveTableMeters = centerHeight;
            sideOffsetMeters = side;
            startVisible = visibleAtStart;
            movable = canMove;
        }


        // =========================================================
        // UNITY
        // =========================================================

        protected virtual void Awake()
        {
            if (tabletopRig == null)
            {
                tabletopRig =
                    FindFirstObjectByType<TabletopRig>();
            }

            if (viewCamera == null)
            {
                viewCamera =
                    Camera.main;
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }


            if (tabletopRig == null ||
                tabletopRig.Origin == null)
            {
                Debug.LogError(
                    $"{GetType().Name}: needs a TabletopRig with an XR Origin.",
                    this
                );

                enabled =
                    false;

                return;
            }


            wantVisible =
                startVisible;

            placeInFront =
                startVisible;

            Build();
        }


        protected virtual void LateUpdate()
        {
            if (root == null)
            {
                return;
            }


            bool show =
                wantVisible &&
                tabletopRig.IsPlaced &&
                viewCamera != null;

            if (root.activeSelf != show)
            {
                root.SetActive(show);
            }

            if (!show ||
                (grabbable != null && grabbable.IsUserPlaced))
            {
                return;
            }


            if (placement == PanelPlacement.InFront)
            {
                if (placeInFront)
                {
                    placeInFront =
                        false;

                    PlaceInFront();
                }

                return;
            }


            PlaceAtTable(
                Time.unscaledDeltaTime
            );
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        public void SetVisible(
            bool visible
        )
        {
            if (wantVisible == visible)
            {
                return;
            }


            wantVisible =
                visible;

            hasBearing =
                false;

            if (visible)
            {
                placeInFront =
                    true;

                // Shown again: back to its own place in front.
                if (placement == PanelPlacement.InFront)
                {
                    grabbable?.ResetPlacement();
                }
            }

            studySession?.LogEvent(
                "vr_panel",
                ("panel", PanelName),
                ("visible", visible)
            );

            VisibilityChanged?.Invoke(
                visible
            );
        }


        /// <summary>Back to automatic placement after the user moved it.</summary>
        public void ResetPlacement()
        {
            grabbable?.ResetPlacement();

            hasBearing =
                false;

            placeInFront =
                true;
        }


        // =========================================================
        // PLACEMENT
        // =========================================================

        private void PlaceAtTable(
            float deltaTime
        )
        {
            Transform space =
                root.transform.parent;

            Vector3 center =
                tabletopRig.TableCenterRig;

            Vector3 head =
                space.InverseTransformPoint(
                    viewCamera.transform.position
                );

            Vector3 toUser =
                head - center;

            toUser.y =
                0.0f;


            if (toUser.sqrMagnitude > 1e-4f)
            {
                float user =
                    Mathf.Atan2(toUser.x, toUser.z) * Mathf.Rad2Deg;

                if (!hasBearing)
                {
                    bearing =
                        user;

                    targetBearing =
                        user;

                    hasBearing =
                        true;
                }
                else if (Mathf.Abs(Mathf.DeltaAngle(targetBearing, user)) >
                         repositionDegrees)
                {
                    targetBearing =
                        user;
                }
            }


            bearing =
                Mathf.LerpAngle(
                    bearing,
                    targetBearing,
                    1.0f - Mathf.Exp(-moveSharpness * deltaTime)
                );


            // Direction from the table centre toward the user.
            var toward =
                new Vector3(
                    Mathf.Sin(bearing * Mathf.Deg2Rad),
                    0.0f,
                    Mathf.Cos(bearing * Mathf.Deg2Rad)
                );

            // The user faces the table (-toward); their right:
            Vector3 right =
                Vector3.Cross(
                    Vector3.up,
                    -toward
                );

            Vector3 outward =
                placement == PanelPlacement.FarEdge
                    ? -toward
                    : toward;


            Vector3 position =
                center +
                outward * (tabletopRig.EdgeDistanceMeters(outward) + edgeSetbackMeters) +
                right * sideOffsetMeters +
                Vector3.up * centerAboveTableMeters;


            Quaternion rotation;

            if (placement == PanelPlacement.FarEdge)
            {
                // Read from its -Z side: forward points away from the user.
                rotation =
                    Quaternion.LookRotation(
                        -toward,
                        Vector3.up
                    );
            }
            else
            {
                Vector3 view =
                    position - head;

                view.y =
                    0.0f;

                rotation =
                    view.sqrMagnitude > 1e-6f
                        ? Quaternion.LookRotation(view.normalized, Vector3.up)
                        : Quaternion.LookRotation(-toward, Vector3.up);
            }


            root.transform.localPosition =
                position;

            root.transform.localRotation =
                rotation;

            KeepOutOfTable();
        }


        private void PlaceInFront()
        {
            Transform space =
                root.transform.parent;

            Vector3 head =
                space.InverseTransformPoint(
                    viewCamera.transform.position
                );

            Vector3 forward =
                space.InverseTransformDirection(
                    viewCamera.transform.forward
                );

            forward.y =
                0.0f;

            if (forward.sqrMagnitude < 1e-6f)
            {
                forward =
                    Vector3.forward;
            }

            forward.Normalize();


            root.transform.localPosition =
                head +
                forward * frontDistanceMeters +
                Vector3.down * 0.1f;

            root.transform.localRotation =
                Quaternion.LookRotation(
                    forward,
                    Vector3.up
                );

            KeepOutOfTable();
        }


        private void KeepOutOfTable()
        {
            Vector2 halfSize =
                0.5f * sizeUnits * metersPerUnit;

            root.transform.position =
                tabletopRig.KeepOutOfTable(
                    root.transform.position,
                    halfSize.x,
                    halfSize.y,
                    tableClearanceMeters
                );
        }


        // =========================================================
        // BUILD
        // =========================================================

        private void Build()
        {
            root =
                new GameObject(
                    GetType().Name + "Canvas",
                    typeof(RectTransform)
                );

            root.SetActive(false);

            root.transform.SetParent(
                tabletopRig.Origin,
                false
            );

            root.transform.localScale =
                Vector3.one * metersPerUnit;


            Canvas canvas =
                root.AddComponent<Canvas>();

            canvas.renderMode =
                RenderMode.WorldSpace;

            canvas.worldCamera =
                viewCamera;

            canvas.sortingOrder =
                sortingOrder;

            ((RectTransform)root.transform).sizeDelta =
                sizeUnits;

            // Rays stop on the panel instead of picking the city.
            root.AddComponent<TrackedDeviceGraphicRaycaster>();


            if (movable)
            {
                grabbable =
                    XRGrabbable.AddPanelHandle(
                        (RectTransform)root.transform
                    );
            }


            // Opaque: in linear colour space even 6 % transparency lets
            // the bright map show clearly through the panel.
            Color panelColor =
                RuntimeUi.PanelColor;

            panelColor.a =
                1.0f;

            Image background =
                RuntimeUi.CreatePanel(
                    "Panel",
                    root.transform,
                    panelColor
                );

            RuntimeUi.Stretch(
                background.rectTransform,
                0.0f
            );

            background.rectTransform.offsetMin =
                Vector2.zero;

            background.rectTransform.offsetMax =
                Vector2.zero;

            RuntimeUi.Vertical(
                background.gameObject,
                14,
                8.0f
            );


            BuildContent(
                background.transform
            );
        }


        /// <summary>Fills the panel (vertical layout, padded).</summary>
        protected abstract void BuildContent(
            Transform panel
        );


        // =========================================================
        // HELPERS FOR SUBCLASSES
        // =========================================================

        /// <summary>Title row with an optional Hide button on the right.</summary>
        protected TMP_Text CreateHeader(
            Transform panel,
            string title,
            bool hideButton
        )
        {
            RectTransform row =
                RuntimeUi.CreateRect(
                    "Header",
                    panel
                );

            RuntimeUi.Horizontal(
                row.gameObject,
                0,
                6.0f
            );

            TMP_Text text =
                RuntimeUi.CreateText(
                    row,
                    title,
                    RuntimeUi.TitleSize,
                    RuntimeUi.AccentColor,
                    TextAnchor.MiddleLeft,
                    FontStyles.Bold
                );

            RuntimeUi.Layout(
                text.gameObject,
                flexibleWidth: 1.0f
            );

            if (hideButton)
            {
                RuntimeUi.CreateButton(
                    row,
                    "Hide",
                    () => SetVisible(false),
                    90.0f,
                    36.0f,
                    RuntimeUi.SmallSize
                );
            }

            return text;
        }


        /// <summary>Scroll area that fills the rest of the panel.</summary>
        protected static RectTransform CreateScroll(
            Transform parent,
            out ScrollRect scroll
        )
        {
            RectTransform content =
                RuntimeUi.CreateScrollView(
                    parent,
                    out scroll
                );

            RuntimeUi.Layout(
                scroll.gameObject,
                flexibleHeight: 1.0f
            );

            // Thumbstick scrolling gives small deltas.
            scroll.scrollSensitivity =
                60.0f;

            return content;
        }
    }
}
