using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Pop-out copies of the selection (VR): "Copy" makes a complete 3D
    /// copy of everything drawn for the selected entity (building, or
    /// area with its columns/surface), lifted above the original with a
    /// thin line back to where it came from and a label with its name
    /// and values. The copy can be grabbed (controller grip or hand
    /// pinch with the ray on it) and put anywhere, e.g. next to another
    /// area to compare them. Copies are snapshots: they keep the view
    /// they were made in (named on the label). The label's "Remove"
    /// button removes one copy; <see cref="RemoveAll"/> removes all.
    ///
    /// The copy lives in world space next to the city (1 unit = 1 km), so
    /// it scales with the table. Its only collider is a grab box on the
    /// "Ignore Raycast" layer (an XRGrabbable, moved by XRGrabber): city
    /// picking never hits it, and XRControllerPointer treats a ray on it
    /// like a ray on a panel (no city selection through a copy).
    ///
    /// Logged as the study event vr_copy (create / move / remove).
    /// </summary>
    public sealed class XRSelectionCopies :
        MonoBehaviour
    {

        private sealed class Copy
        {
            public int Number;

            public GameObject Root;

            public Vector3 Anchor;

            public float Height;

            public LineRenderer Stem;

            public RectTransform Label;

            public readonly List<Mesh> Meshes =
                new List<Mesh>();

            public string EntityId;
        }


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Systems (found automatically when empty)")]
        [SerializeField]
        private InteractionManager interactionManager;

        [SerializeField]
        private XRControllerPointer pointer;

        [SerializeField]
        private Camera viewCamera;

        [SerializeField]
        private VisualizationSwitcher visualizationSwitcher;

        [SerializeField]
        private StudySession studySession;


        [Header("Look (real metres)")]
        [Tooltip("The copy's base starts this far above the original's top.")]
        [SerializeField]
        [Range(0.02f, 0.5f)]
        private float liftMeters =
            0.12f;

        [Tooltip("Grab box is at least this big, so small copies are easy to grab.")]
        [SerializeField]
        [Range(0.01f, 0.2f)]
        private float minGrabSizeMeters =
            0.05f;

        [SerializeField]
        [Range(0.0005f, 0.01f)]
        private float stemWidthMeters =
            0.002f;

        [SerializeField]
        [Range(0.0002f, 0.002f)]
        private float labelMetersPerUnit =
            0.0006f;

        [Tooltip("Line material using vertex colours (UrbanAnalytics/VertexColorUnlit).")]
        [SerializeField]
        private Material lineMaterial;

        [SerializeField]
        [Range(1, 20)]
        private int maxCopies =
            8;

        [SerializeField]
        private Color[] colors =
        {
            new Color(1.00f, 0.83f, 0.00f),
            new Color(0.25f, 0.80f, 1.00f),
            new Color(1.00f, 0.45f, 0.80f),
            new Color(0.55f, 0.95f, 0.45f)
        };


        // =========================================================
        // RUNTIME
        // =========================================================

        private readonly List<Copy> copies =
            new List<Copy>();

        private Transform copiesRoot;

        private int nextNumber =
            1;


        public int Count =>
            copies.Count;


        public event Action Changed;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (interactionManager == null)
            {
                interactionManager =
                    FindFirstObjectByType<InteractionManager>();
            }

            if (pointer == null)
            {
                pointer =
                    FindFirstObjectByType<XRControllerPointer>();
            }

            if (visualizationSwitcher == null)
            {
                visualizationSwitcher =
                    FindFirstObjectByType<VisualizationSwitcher>();
            }

            if (studySession == null)
            {
                studySession =
                    FindFirstObjectByType<StudySession>();
            }

            if (viewCamera == null)
            {
                viewCamera =
                    Camera.main;
            }


            copiesRoot =
                new GameObject("VRCopies").transform;
        }



        private void OnDestroy()
        {
            RemoveAll();

            if (copiesRoot != null)
            {
                Destroy(copiesRoot.gameObject);
            }
        }



        private void LateUpdate()
        {
            if (viewCamera == null)
            {
                return;
            }


            float worldScale =
                Mathf.Max(
                    1e-4f,
                    viewCamera.transform.lossyScale.x
                );

            Vector3 eye =
                viewCamera.transform.position;


            foreach (Copy copy in copies)
            {
                Vector3 bottom =
                    copy.Root.transform.position;

                copy.Stem.SetPosition(0, bottom);

                copy.Stem.SetPosition(1, copy.Anchor);

                copy.Stem.widthMultiplier =
                    stemWidthMeters * worldScale;


                Vector3 labelPosition =
                    bottom +
                    Vector3.up * (copy.Height + 0.02f * worldScale);

                Vector3 toLabel =
                    labelPosition - eye;

                toLabel.y =
                    0.0f;

                copy.Label.SetPositionAndRotation(
                    labelPosition,
                    toLabel.sqrMagnitude > 1e-8f
                        ? Quaternion.LookRotation(toLabel.normalized, Vector3.up)
                        : copy.Label.rotation
                );

                copy.Label.localScale =
                    Vector3.one * (labelMetersPerUnit * worldScale);
            }
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        /// <summary>
        /// Copies the current selection. Returns false when nothing is
        /// selected or nothing is drawn for it.
        /// </summary>
        public bool CopySelected()
        {
            if (interactionManager == null ||
                !interactionManager.HasSelection)
            {
                Debug.Log(
                    "XRSelectionCopies: nothing selected to copy.",
                    this
                );

                return false;
            }


            EntityReference entity =
                interactionManager.Selected;

            EntityGeometry geometry =
                interactionManager.CollectGeometry(
                    entity,
                    true
                );

            if (geometry.IsEmpty)
            {
                geometry =
                    interactionManager.CollectFootprint(
                        entity
                    );
            }

            if (geometry.IsEmpty)
            {
                Debug.Log(
                    $"XRSelectionCopies: nothing drawn for '{entity}', no copy.",
                    this
                );

                return false;
            }


            while (copies.Count >= maxCopies)
            {
                Remove(copies[0], "limit");
            }


            float worldScale =
                viewCamera != null
                    ? Mathf.Max(1e-4f, viewCamera.transform.lossyScale.x)
                    : 1.0f;

            Bounds bounds =
                geometry.Bounds;

            var basePoint =
                new Vector3(
                    bounds.center.x,
                    bounds.min.y,
                    bounds.center.z
                );

            var anchor =
                new Vector3(
                    bounds.center.x,
                    bounds.max.y,
                    bounds.center.z
                );


            var copy =
                new Copy
                {
                    Number = nextNumber++,
                    Anchor = anchor,
                    Height = bounds.size.y,
                    EntityId = entity.ToString()
                };

            Color color =
                colors.Length > 0
                    ? colors[(copy.Number - 1) % colors.Length]
                    : Color.white;


            copy.Root =
                new GameObject($"Copy{copy.Number}_{EntityReference.ShortId(entity.Id)}");

            copy.Root.transform.SetParent(
                copiesRoot,
                false
            );

            copy.Root.transform.position =
                anchor +
                Vector3.up * (liftMeters * worldScale);


            // ----- the geometry, relative to the copy's base -----

            foreach (EntityGeometry.Part part in geometry.Parts)
            {
                Mesh mesh =
                    EntityGeometry.BuildMesh(
                        part,
                        basePoint,
                        copy.Root.name
                    );

                copy.Meshes.Add(mesh);

                var partObject =
                    new GameObject(
                        part.Material != null
                            ? part.Material.name
                            : "part"
                    );

                partObject.transform.SetParent(
                    copy.Root.transform,
                    false
                );

                partObject.AddComponent<MeshFilter>().sharedMesh =
                    mesh;

                MeshRenderer renderer =
                    partObject.AddComponent<MeshRenderer>();

                renderer.sharedMaterial =
                    part.Material;

                renderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                renderer.receiveShadows =
                    false;
            }


            // ----- grab box (Ignore Raycast layer) -----

            float minSize =
                minGrabSizeMeters * worldScale;

            var grabObject =
                new GameObject("GrabBox")
                {
                    layer = XRGrabbable.Layer
                };

            // XRGrabber moves the copy (grip / pinch with the ray on it).
            XRGrabbable grabbable =
                copy.Root.AddComponent<XRGrabbable>();

            Copy moved =
                copy;

            grabbable.Released +=
                () => Log("move", moved);

            grabObject.transform.SetParent(
                copy.Root.transform,
                false
            );

            BoxCollider box =
                grabObject.AddComponent<BoxCollider>();

            box.center =
                bounds.center - basePoint;

            box.size =
                Vector3.Max(
                    bounds.size,
                    Vector3.one * minSize
                );


            // ----- stem line back to the original -----

            copy.Stem =
                new GameObject("Stem").AddComponent<LineRenderer>();

            copy.Stem.transform.SetParent(
                copy.Root.transform,
                false
            );

            copy.Stem.useWorldSpace =
                true;

            copy.Stem.positionCount =
                2;

            copy.Stem.sharedMaterial =
                lineMaterial;

            copy.Stem.startColor =
                color;

            copy.Stem.endColor =
                color;

            copy.Stem.shadowCastingMode =
                ShadowCastingMode.Off;


            // ----- label -----

            copy.Label =
                BuildLabel(
                    copy,
                    entity,
                    color
                );


            copies.Add(copy);


            Debug.Log(
                $"XRSelectionCopies: copy {copy.Number} of '{entity}' " +
                $"({geometry.TriangleCount:N0} triangles).",
                this
            );

            Log("create", copy);

            Changed?.Invoke();

            return true;
        }


        public void RemoveAll()
        {
            for (int i = copies.Count - 1; i >= 0; i--)
            {
                Remove(copies[i], "remove");
            }

            Changed?.Invoke();
        }



        // =========================================================
        // HELPERS
        // =========================================================

        private RectTransform BuildLabel(
            Copy copy,
            EntityReference entity,
            Color color
        )
        {
            var labelObject =
                new GameObject(
                    "Label",
                    typeof(RectTransform)
                );

            labelObject.transform.SetParent(
                copy.Root.transform,
                false
            );


            Canvas canvas =
                labelObject.AddComponent<Canvas>();

            canvas.renderMode =
                RenderMode.WorldSpace;

            canvas.worldCamera =
                viewCamera;

            canvas.sortingOrder =
                100;

            // Same structure as XRHoverLabel: pivot at the bottom centre,
            // a background that grows with the text.
            var rect =
                (RectTransform)labelObject.transform;

            rect.pivot =
                new Vector2(0.5f, 0.0f);

            rect.sizeDelta =
                new Vector2(360.0f, 0.0f);


            Color panel =
                RuntimeUi.PanelColor;

            panel.a =
                1.0f;

            Image background =
                RuntimeUi.CreatePanel(
                    "Background",
                    labelObject.transform,
                    panel
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
                8,
                0.0f
            );

            ContentSizeFitter fitter =
                background.gameObject.AddComponent<ContentSizeFitter>();

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            backgroundRect.sizeDelta =
                new Vector2(360.0f, 0.0f);


            EntityInfo info =
                interactionManager.BuildInfo(
                    entity
                );

            string view =
                visualizationSwitcher != null &&
                visualizationSwitcher.ActiveIndex >= 0 &&
                visualizationSwitcher.ActiveIndex < visualizationSwitcher.Options.Count
                    ? visualizationSwitcher.Options[visualizationSwitcher.ActiveIndex].DisplayName
                    : "no view";

            var lines =
                new List<string>
                {
                    RuntimeUi.Colorize($"Copy {copy.Number} · {view}", color),
                    $"<b>{info.Title}</b>"
                };

            int shown =
                0;

            foreach (EntityInfoRow row in info.Highlights)
            {
                if (shown++ >= 3)
                {
                    break;
                }

                lines.Add(
                    $"{row.Label}: <b>{DesktopInteractionUI.ValueWithUnit(row)}</b>"
                );
            }


            TMP_Text text =
                RuntimeUi.CreateText(
                    background.transform,
                    string.Join("\n", lines),
                    RuntimeUi.SmallSize,
                    RuntimeUi.TextColor
                );

            text.raycastTarget =
                false;


            // Remove just this copy (ray + trigger / pinch, or poke).
            labelObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

            Copy target =
                copy;

            RuntimeUi.CreateButton(
                background.transform,
                "Remove",
                () =>
                {
                    Remove(target, "remove");

                    Changed?.Invoke();
                },
                -1.0f,
                40.0f,
                RuntimeUi.SmallSize
            );

            return rect;
        }


        private void Remove(
            Copy copy,
            string how
        )
        {
            Log(how, copy);

            foreach (Mesh mesh in copy.Meshes)
            {
                Destroy(mesh);
            }

            if (copy.Root != null)
            {
                Destroy(copy.Root);
            }

            copies.Remove(copy);
        }


        private void Log(
            string how,
            Copy copy
        )
        {
            Vector3 position =
                copy.Root != null
                    ? copy.Root.transform.position
                    : Vector3.zero;

            studySession?.LogEvent(
                "vr_copy",
                ("how", how),
                ("copy", copy.Number),
                ("entity", copy.EntityId),
                ("position", position)
            );
        }
    }
}
