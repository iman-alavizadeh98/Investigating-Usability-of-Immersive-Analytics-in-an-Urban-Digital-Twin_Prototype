using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.UrbanContext;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.XR
{
    /// <summary>
    /// Shows the city as a fixed "hologram table" in VR.
    ///
    /// The city is NOT moved or scaled (1 Unity unit = 1 km stays
    /// true, so picking, highlights and comparison copies are
    /// unchanged). Instead the XR Origin is scaled up: with scale S,
    /// one real metre in the headset covers S world units (S km).
    /// The rig is placed so the city's base plane is a table top at
    /// <see cref="tableHeightMeters"/> above the real floor.
    ///
    /// Placement runs once, when the city has loaded; users walk around
    /// the table. The table can be resized (<see cref="SetTableLength"/>,
    /// right thumbstick via XRControllerShortcuts): the physical table
    /// centre stays where it is in the room and the table grows or
    /// shrinks around it, i.e. only the rig scale S changes. The table
    /// can also be moved and turned in the room (<see cref="MoveTable"/>,
    /// <see cref="BringTableTo"/>; XRTableMover): again only the rig is
    /// placed differently, the city never moves. Scale and moves are
    /// logged so sessions can be reproduced.
    ///
    /// N / E / S / W letters on the table margin show the compass
    /// directions (north = the city's +Z = CRS northing).
    ///
    /// Also sets the XR eye-texture resolution scale (supersampling)
    /// once the headset display is active.
    /// </summary>
    public sealed class TabletopRig :
        MonoBehaviour
    {
        private const string ComparisonStageLayerName =
            "ComparisonStage";


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Rig")]
        [Tooltip("Root of the XR rig (XR Origin). It is scaled and placed.")]
        [SerializeField]
        private Transform xrOrigin;

        [Tooltip("The XR camera. Clip planes are set in table metres.")]
        [SerializeField]
        private Camera xrCamera;

        [Tooltip("Parent of the runtime city meshes (CityRoot).")]
        [SerializeField]
        private Transform cityRoot;


        [Header("Managers (found automatically when empty)")]
        [SerializeField]
        private GeometryManager geometryManager;

        [SerializeField]
        private UrbanContextManager urbanContextManager;

        [SerializeField]
        private VisualizationManager visualizationManager;


        [Header("Table (real metres)")]
        [Tooltip("Height of the table top above the floor.")]
        [SerializeField]
        [Range(0.5f, 1.3f)]
        private float tableHeightMeters =
            0.9f;

        [Tooltip(
            "Start length of the table: the city's longest side is " +
            "fitted to this length (minus the margins) when no fixed " +
            "scale is set."
        )]
        [SerializeField]
        [Range(0.5f, 4.0f)]
        private float maxTableSizeMeters =
            2.6f;

        [Tooltip("Smallest table length reachable by resizing.")]
        [SerializeField]
        [Range(0.5f, 4.0f)]
        private float minTableLengthMeters =
            1.2f;

        [Tooltip("Largest table length reachable by resizing.")]
        [SerializeField]
        [Range(0.5f, 6.0f)]
        private float maxTableLengthMeters =
            4.0f;

        [Tooltip("Free border between the city and the table edge.")]
        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float tableMarginMeters =
            0.08f;

        [SerializeField]
        [Range(0.005f, 0.2f)]
        private float tableThicknessMeters =
            0.04f;

        [Tooltip(
            "World units (km) per table metre. 0 = fit the city " +
            "to Max Table Size. Set a value to keep the scale fixed " +
            "across datasets."
        )]
        [SerializeField]
        [Min(0.0f)]
        private float fixedWorldUnitsPerMeter =
            0.0f;

        [Tooltip(
            "Table top sits this far below the city base plane, " +
            "so the flat base layer does not z-fight with it."
        )]
        [SerializeField]
        [Range(0.0f, 0.01f)]
        private float tableTopGapMeters =
            0.002f;


        [Header("Start position (real metres)")]
        [Tooltip(
            "The user starts this far from the table's south " +
            "edge, facing north (the city's +Z)."
        )]
        [SerializeField]
        [Range(0.1f, 2.0f)]
        private float standDistanceMeters =
            0.45f;


        [Header("Camera (real metres)")]
        [SerializeField]
        [Range(0.005f, 0.2f)]
        private float nearClipMeters =
            0.01f;

        [SerializeField]
        [Range(5.0f, 500.0f)]
        private float farClipMeters =
            60.0f;

        [Tooltip(
            "XR eye-texture resolution scale (supersampling). Quest " +
            "Link recommends about 1824 × 1968 per eye for a Quest 3 " +
            "on this PC; 1.2 renders close to the panel's native " +
            "resolution. Lower it if the frame rate drops."
        )]
        [SerializeField]
        [Range(0.5f, 2.0f)]
        private float eyeResolutionScale =
            1.2f;


        [Header("Room")]
        [SerializeField]
        private bool buildFloor =
            true;

        [SerializeField]
        [Range(2.0f, 30.0f)]
        private float floorSizeMeters =
            10.0f;

        [Tooltip("Optional; a plain lit material is created when empty.")]
        [SerializeField]
        private Material tableMaterial;

        [Tooltip("Optional; a plain lit material is created when empty.")]
        [SerializeField]
        private Material floorMaterial;


        [Header("Compass (real metres)")]
        [Tooltip(
            "N / E / S / W letters on the table margin at each edge " +
            "(north = the city's +Z, from the CRS northing)."
        )]
        [SerializeField]
        private bool showCompass =
            true;

        [SerializeField]
        [Range(0.02f, 0.15f)]
        private float compassLetterMeters =
            0.06f;


        // =========================================================
        // PUBLIC STATE
        // =========================================================

        /// <summary>
        /// World units (km) per real metre: the XR Origin scale.
        /// 0 until placed.
        /// </summary>
        public float WorldUnitsPerMeter
        {
            get;
            private set;
        }


        /// <summary>World-space bounds of the city when placed.</summary>
        public Bounds CityBounds
        {
            get;
            private set;
        }


        /// <summary>World-space height of the table top.</summary>
        public float TableTopWorldY
        {
            get;
            private set;
        }


        public bool IsPlaced =>
            WorldUnitsPerMeter > 0.0f;


        /// <summary>Raised once, after the rig was placed.</summary>
        public event Action Placed;


        /// <summary>
        /// Current table length (along the city's longest side) in
        /// real metres. 0 until placed.
        /// </summary>
        public float TableLengthMeters
        {
            get;
            private set;
        }


        /// <summary>The scaled XR Origin (its local units are real metres).</summary>
        public Transform Origin =>
            xrOrigin;


        /// <summary>Table-top centre in XR Origin space (real metres).</summary>
        public Vector3 TableCenterRig =>
            tableCenterRig;


        /// <summary>Table width (x) and depth (z) in real metres.</summary>
        public Vector2 TableSizeMeters =>
            IsPlaced
                ? new Vector2(
                    CityBounds.size.x / WorldUnitsPerMeter + 2.0f * tableMarginMeters,
                    CityBounds.size.z / WorldUnitsPerMeter + 2.0f * tableMarginMeters
                )
                : Vector2.zero;


        public float MinTableLengthMeters =>
            minTableLengthMeters;


        public float MaxTableLengthMeters =>
            maxTableLengthMeters;


        /// <summary>Raised after every resize (not on placement).</summary>
        public event Action Resized;


        /// <summary>
        /// Table rotation in the room (degrees about the vertical, XR
        /// Origin space); 0 = the city's north points along the rig's +Z.
        /// </summary>
        public float TableYawRig =>
            tableYawRig;


        /// <summary>Raised after every move or turn of the table.</summary>
        public event Action Moved;


        // =========================================================
        // RUNTIME
        // =========================================================

        // Table-top centre in XR Origin space (real metres). Kept when
        // resizing; changed only by MoveTable / BringTableTo.
        private Vector3 tableCenterRig;

        private float tableYawRig;

        private Transform[] compassLabels =
            Array.Empty<Transform>();

        private Transform tableTop;

        private Transform tablePedestal;

        private Transform floor;

        private Vector2Int loggedEyeTexture;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (geometryManager == null)
            {
                geometryManager =
                    FindFirstObjectByType<GeometryManager>();
            }


            if (urbanContextManager == null)
            {
                urbanContextManager =
                    FindFirstObjectByType<UrbanContextManager>();
            }


            if (visualizationManager == null)
            {
                visualizationManager =
                    FindFirstObjectByType<VisualizationManager>();
            }


            if (xrOrigin == null ||
                cityRoot == null)
            {
                Debug.LogError(
                    "TabletopRig: assign the XR Origin and CityRoot.",
                    this
                );

                enabled =
                    false;
            }
        }


        private void Update()
        {
            ApplyEyeResolution();


            if (IsPlaced ||
                !IsCityReady())
            {
                return;
            }


            Place();
        }


        // =========================================================
        // RESIZE
        // =========================================================

        /// <summary>
        /// Resizes the table to the given length (clamped to the min/max
        /// lengths) around its fixed centre. Returns the new length.
        /// </summary>
        public float SetTableLength(
            float meters
        )
        {
            if (!IsPlaced)
            {
                return 0.0f;
            }


            float length =
                Mathf.Clamp(
                    meters,
                    minTableLengthMeters,
                    Mathf.Max(minTableLengthMeters, maxTableLengthMeters)
                );


            if (Mathf.Abs(length - TableLengthMeters) < 1e-4f)
            {
                return TableLengthMeters;
            }


            ApplyScale(
                ScaleForLength(length)
            );


            Resized?.Invoke();

            return TableLengthMeters;
        }


        // =========================================================
        // MOVE
        // =========================================================

        /// <summary>
        /// Moves and turns the table in the room (XR Origin space, real
        /// metres; only the horizontal part of the centre is used, the
        /// height stays). The city does not move: the rig is placed so
        /// the table appears there.
        /// </summary>
        public void MoveTable(
            Vector3 centerRig,
            float yawDegrees
        )
        {
            if (!IsPlaced)
            {
                return;
            }


            tableCenterRig =
                new Vector3(
                    centerRig.x,
                    tableHeightMeters,
                    centerRig.z
                );

            tableYawRig =
                Mathf.Repeat(
                    yawDegrees,
                    360.0f
                );


            ApplyScale(
                WorldUnitsPerMeter
            );


            Moved?.Invoke();
        }


        /// <summary>
        /// Brings the table in front of a viewer (head position and
        /// view direction in XR Origin space): its near edge ends up
        /// <see cref="standDistanceMeters"/> ahead, its rotation is kept.
        /// </summary>
        public void BringTableTo(
            Vector3 headRig,
            Vector3 forwardRig
        )
        {
            forwardRig.y =
                0.0f;

            if (!IsPlaced ||
                forwardRig.sqrMagnitude < 1e-6f)
            {
                return;
            }


            forwardRig.Normalize();


            MoveTable(
                headRig +
                forwardRig *
                (EdgeDistanceMeters(-forwardRig) + standDistanceMeters),
                tableYawRig
            );
        }


        /// <summary>
        /// Distance from the table centre to its edge along a horizontal
        /// direction in XR Origin space (the table is a turned rectangle).
        /// </summary>
        public float EdgeDistanceMeters(
            Vector3 directionRig
        )
        {
            directionRig.y =
                0.0f;

            if (directionRig.sqrMagnitude < 1e-8f)
            {
                return 0.0f;
            }


            Vector3 local =
                Quaternion.Euler(0.0f, -tableYawRig, 0.0f) *
                directionRig.normalized;

            Vector2 size =
                TableSizeMeters;


            return Mathf.Min(
                Mathf.Abs(local.x) > 1e-4f
                    ? 0.5f * size.x / Mathf.Abs(local.x)
                    : float.MaxValue,
                Mathf.Abs(local.z) > 1e-4f
                    ? 0.5f * size.y / Mathf.Abs(local.z)
                    : float.MaxValue
            );
        }


        private float ScaleForLength(
            float lengthMeters
        )
        {
            float longestSide =
                Mathf.Max(
                    CityBounds.size.x,
                    CityBounds.size.z
                );

            return longestSide /
                   Mathf.Max(
                       0.1f,
                       lengthMeters - 2.0f * tableMarginMeters
                   );
        }


        /// <summary>
        /// Scales and places the XR Origin for scale S so that the table
        /// centre stays at <see cref="tableCenterRig"/> in the room, and
        /// updates the clip planes and the table visuals.
        /// </summary>
        private void ApplyScale(
            float scale
        )
        {
            var tableCenterWorld =
                new Vector3(
                    CityBounds.center.x,
                    TableTopWorldY,
                    CityBounds.center.z
                );


            // Rig → world: x_world = position + rotation · (S · x_rig).
            // The table (fixed in the world) appears turned by +yaw in
            // the room when the rig is turned by −yaw.
            Quaternion rotation =
                Quaternion.Euler(
                    0.0f,
                    -tableYawRig,
                    0.0f
                );

            xrOrigin.localScale =
                Vector3.one * scale;

            xrOrigin.SetPositionAndRotation(
                tableCenterWorld - rotation * (scale * tableCenterRig),
                rotation
            );


            if (xrCamera != null)
            {
                // Clip planes are not affected by the rig's scale.
                xrCamera.nearClipPlane =
                    nearClipMeters * scale;

                xrCamera.farClipPlane =
                    farClipMeters * scale;
            }


            WorldUnitsPerMeter =
                scale;

            TableLengthMeters =
                Mathf.Max(
                    CityBounds.size.x,
                    CityBounds.size.z
                ) / scale +
                2.0f * tableMarginMeters;


            UpdateVisuals(
                scale
            );
        }


        private void ApplyEyeResolution()
        {
            if (!XRSettings.isDeviceActive)
            {
                return;
            }


            if (!Mathf.Approximately(
                    XRSettings.eyeTextureResolutionScale,
                    eyeResolutionScale
                ))
            {
                XRSettings.eyeTextureResolutionScale =
                    eyeResolutionScale;
            }


            var size =
                new Vector2Int(
                    XRSettings.eyeTextureWidth,
                    XRSettings.eyeTextureHeight
                );


            if (size != loggedEyeTexture &&
                size.x > 0)
            {
                loggedEyeTexture =
                    size;

                Debug.Log(
                    $"TabletopRig: XR eye texture {size.x} × {size.y} " +
                    $"(resolution scale {eyeResolutionScale:0.00}).",
                    this
                );
            }
        }


        // =========================================================
        // PLACEMENT
        // =========================================================

        private bool IsCityReady()
        {
            if (geometryManager == null ||
                !geometryManager.IsInitialized)
            {
                return false;
            }


            bool buildingsPending =
                urbanContextManager != null &&
                urbanContextManager.isActiveAndEnabled &&
                (urbanContextManager.IsInitializing ||
                 (urbanContextManager.IsInitialized &&
                  !urbanContextManager.AreBuildingsLoaded));


            bool visualizationBusy =
                visualizationManager != null &&
                visualizationManager.IsBusy;


            return !buildingsPending &&
                   !visualizationBusy;
        }


        private void Place()
        {
            if (!TryGetCityBounds(
                    out Bounds bounds
                ))
            {
                Debug.LogError(
                    "TabletopRig: the city has no visible " +
                    "geometry; the table was not placed.",
                    this
                );

                enabled =
                    false;

                return;
            }


            CityBounds =
                bounds;


            float scale =
                fixedWorldUnitsPerMeter > 0.0f
                    ? fixedWorldUnitsPerMeter
                    : ScaleForLength(maxTableSizeMeters);


            // The base plane of the city (y = 0 under CityRoot) is
            // the table top. Columns of a signed/diverging height
            // scale go below it and into the table (known limit).
            // Fixed at placement; the gap is a fraction of a millimetre
            // after resizing.
            TableTopWorldY =
                cityRoot.position.y -
                tableTopGapMeters * scale;


            // ----- table centre in the room: the user starts at the
            //       south edge facing north (XR Origin space, metres;
            //       the origin's floor is y = 0) -----

            float tableDepthMeters =
                bounds.size.z / scale +
                2.0f * tableMarginMeters;

            tableCenterRig =
                new Vector3(
                    0.0f,
                    tableHeightMeters,
                    0.5f * tableDepthMeters + standDistanceMeters
                );


            ApplyScale(
                scale
            );


            float tableWidthMeters =
                bounds.size.x / scale +
                2.0f * tableMarginMeters;

            Debug.Log(
                $"TabletopRig: city {bounds.size.x:0.00} × " +
                $"{bounds.size.z:0.00} km on a " +
                $"{tableWidthMeters:0.00} × " +
                $"{tableDepthMeters:0.00} m table, " +
                $"1 m = {scale:0.000} km (scale 1:" +
                $"{scale * 1000.0f:0}), table top " +
                $"{tableHeightMeters:0.00} m; resizable " +
                $"{minTableLengthMeters:0.0}–{maxTableLengthMeters:0.0} m.",
                this
            );


            Placed?.Invoke();
        }


        private bool TryGetCityBounds(
            out Bounds bounds
        )
        {
            bounds =
                default;


            int stageLayer =
                LayerMask.NameToLayer(
                    ComparisonStageLayerName
                );


            bool found =
                false;


            foreach (Renderer renderer in
                     cityRoot.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    renderer.gameObject.layer == stageLayer)
                {
                    continue;
                }


                if (!found)
                {
                    bounds =
                        renderer.bounds;

                    found =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds
                    );
                }
            }


            return found &&
                   bounds.size.x > 0.0f &&
                   bounds.size.z > 0.0f;
        }


        // =========================================================
        // TABLE AND FLOOR VISUALS
        // =========================================================

        /// <summary>
        /// Creates the table top, pedestal and floor on first use and
        /// sizes them for scale S (world units per metre).
        /// </summary>
        private void UpdateVisuals(
            float scale
        )
        {
            var topCenter =
                new Vector3(
                    CityBounds.center.x,
                    TableTopWorldY,
                    CityBounds.center.z
                );

            var sizeWorld =
                new Vector2(
                    CityBounds.size.x + 2.0f * tableMarginMeters * scale,
                    CityBounds.size.z + 2.0f * tableMarginMeters * scale
                );

            float thickness =
                tableThicknessMeters * scale;


            // Keeps the collider: rays that miss the city stop on
            // the table instead of hitting things below it.
            if (tableTop == null)
            {
                tableTop =
                    CreateBlock(
                        "TableTop",
                        PrimitiveType.Cube,
                        ref tableMaterial,
                        new Color(0.16f, 0.17f, 0.19f)
                    );
            }

            tableTop.position =
                topCenter - Vector3.up * (0.5f * thickness);

            tableTop.localScale =
                new Vector3(
                    sizeWorld.x,
                    thickness,
                    sizeWorld.y
                );


            // A pedestal so the table reads as standing on the floor.
            if (tablePedestal == null)
            {
                tablePedestal =
                    CreateBlock(
                        "TablePedestal",
                        PrimitiveType.Cube,
                        ref tableMaterial,
                        new Color(0.16f, 0.17f, 0.19f)
                    );
            }

            float legHeight =
                tableHeightMeters * scale - thickness;

            tablePedestal.position =
                topCenter -
                Vector3.up * (thickness + 0.5f * legHeight);

            tablePedestal.localScale =
                new Vector3(
                    sizeWorld.x * 0.6f,
                    legHeight,
                    sizeWorld.y * 0.6f
                );


            UpdateCompass(
                topCenter,
                scale
            );


            if (!buildFloor)
            {
                return;
            }


            if (floor == null)
            {
                floor =
                    CreateBlock(
                        "Floor",
                        PrimitiveType.Quad,
                        ref floorMaterial,
                        new Color(0.32f, 0.33f, 0.35f)
                    );

                floor.rotation =
                    Quaternion.Euler(
                        90.0f,
                        0.0f,
                        0.0f
                    );
            }

            floor.position =
                new Vector3(
                    topCenter.x,
                    TableTopWorldY - tableHeightMeters * scale,
                    topCenter.z
                );

            floor.localScale =
                Vector3.one * (floorSizeMeters * scale);
        }


        /// <summary>
        /// N / E / S / W on the table margin, centred on each edge. They
        /// are fixed to the city (north = +Z) and turned toward the
        /// viewer every frame (<see cref="LateUpdate"/>).
        /// </summary>
        private void UpdateCompass(
            Vector3 topCenter,
            float scale
        )
        {
            if (!showCompass)
            {
                return;
            }


            if (compassLabels.Length == 0)
            {
                compassLabels =
                    new[]
                    {
                        CreateCompassLabel("N", RuntimeUi.AccentColor),
                        CreateCompassLabel("E", RuntimeUi.TextColor),
                        CreateCompassLabel("S", RuntimeUi.TextColor),
                        CreateCompassLabel("W", RuntimeUi.TextColor)
                    };
            }


            // Middle of the margin strip, just above the table top.
            float halfX =
                0.5f * CityBounds.size.x + 0.5f * tableMarginMeters * scale;

            float halfZ =
                0.5f * CityBounds.size.z + 0.5f * tableMarginMeters * scale;

            Vector3 lift =
                Vector3.up * (0.002f * scale);

            Vector3[] offsets =
            {
                new Vector3(0.0f, 0.0f, halfZ),
                new Vector3(halfX, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, -halfZ),
                new Vector3(-halfX, 0.0f, 0.0f)
            };


            for (int i = 0; i < compassLabels.Length; i++)
            {
                compassLabels[i].position =
                    topCenter + offsets[i] + lift;

                // 100 canvas units = one letter box.
                compassLabels[i].localScale =
                    Vector3.one * (compassLetterMeters * scale / 100.0f);
            }
        }


        private Transform CreateCompassLabel(
            string letter,
            Color color
        )
        {
            var label =
                new GameObject(
                    "Compass_" + letter,
                    typeof(RectTransform)
                );

            label.transform.SetParent(
                transform,
                false
            );


            Canvas canvas =
                label.AddComponent<Canvas>();

            canvas.renderMode =
                RenderMode.WorldSpace;

            canvas.worldCamera =
                xrCamera;

            // Below the panels (100+), above the city.
            canvas.sortingOrder =
                90;

            ((RectTransform)label.transform).sizeDelta =
                new Vector2(100.0f, 100.0f);


            TMP_Text text =
                RuntimeUi.CreateText(
                    label.transform,
                    letter,
                    90.0f,
                    color,
                    TextAnchor.MiddleCenter,
                    FontStyles.Bold
                );

            RuntimeUi.Stretch(
                text.rectTransform,
                0.0f
            );

            text.rectTransform.offsetMin =
                Vector2.zero;

            text.rectTransform.offsetMax =
                Vector2.zero;


            return label.transform;
        }


        private void LateUpdate()
        {
            if (compassLabels.Length == 0 ||
                xrCamera == null)
            {
                return;
            }


            Vector3 eye =
                xrCamera.transform.position;


            foreach (Transform label in compassLabels)
            {
                // Lying flat (read from above), its top pointing away
                // from the viewer so the letter reads upright.
                Vector3 away =
                    label.position - eye;

                away.y =
                    0.0f;

                if (away.sqrMagnitude < 1e-8f)
                {
                    continue;
                }

                label.rotation =
                    Quaternion.LookRotation(
                        Vector3.down,
                        away.normalized
                    );
            }
        }


        private Transform CreateBlock(
            string name,
            PrimitiveType primitive,
            ref Material material,
            Color fallbackColor
        )
        {
            GameObject block =
                GameObject.CreatePrimitive(
                    primitive
                );

            block.name =
                name;

            block.transform.SetParent(
                transform,
                false
            );

            ApplyMaterial(
                block,
                ref material,
                fallbackColor
            );

            return block.transform;
        }


        private static void ApplyMaterial(
            GameObject target,
            ref Material material,
            Color fallbackColor
        )
        {
            if (material == null)
            {
                Shader shader =
                    Shader.Find(
                        "Universal Render Pipeline/Lit"
                    );


                if (shader != null)
                {
                    material =
                        new Material(
                            shader
                        )
                        {
                            color = fallbackColor
                        };
                }
            }


            if (material != null)
            {
                target.GetComponent<Renderer>().sharedMaterial =
                    material;
            }
        }
    }
}
