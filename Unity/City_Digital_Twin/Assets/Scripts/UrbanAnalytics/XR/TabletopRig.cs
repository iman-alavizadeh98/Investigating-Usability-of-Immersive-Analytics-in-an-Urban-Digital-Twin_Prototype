using System;
using UnityEngine;

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
    /// Placement runs once, when the city has loaded. The table never
    /// moves; users walk around it. The scale is logged so sessions
    /// can be reproduced.
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
            "The city's longest side is fitted to this length " +
            "(minus the margins) when no fixed scale is set."
        )]
        [SerializeField]
        [Range(0.5f, 4.0f)]
        private float maxTableSizeMeters =
            1.8f;

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
            if (IsPlaced ||
                !IsCityReady())
            {
                return;
            }


            Place();
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


            float longestSide =
                Mathf.Max(
                    bounds.size.x,
                    bounds.size.z
                );


            float usableMeters =
                Mathf.Max(
                    0.1f,
                    maxTableSizeMeters - 2.0f * tableMarginMeters
                );


            float scale =
                fixedWorldUnitsPerMeter > 0.0f
                    ? fixedWorldUnitsPerMeter
                    : longestSide / usableMeters;


            // The base plane of the city (y = 0 under CityRoot) is
            // the table top. Columns of a signed/diverging height
            // scale go below it and into the table (known limit).
            float tableTopY =
                cityRoot.position.y -
                tableTopGapMeters * scale;


            Vector3 tableCenter =
                new Vector3(
                    bounds.center.x,
                    tableTopY,
                    bounds.center.z
                );


            Vector2 tableSizeWorld =
                new Vector2(
                    bounds.size.x + 2.0f * tableMarginMeters * scale,
                    bounds.size.z + 2.0f * tableMarginMeters * scale
                );


            // ----- XR Origin: floor below the table, user at the
            //       south edge facing north -----

            xrOrigin.localScale =
                Vector3.one * scale;

            xrOrigin.SetPositionAndRotation(
                new Vector3(
                    tableCenter.x,
                    tableTopY - tableHeightMeters * scale,
                    tableCenter.z -
                    0.5f * tableSizeWorld.y -
                    standDistanceMeters * scale
                ),
                Quaternion.identity
            );


            if (xrCamera != null)
            {
                // Clip planes are not affected by the rig's scale.
                xrCamera.nearClipPlane =
                    nearClipMeters * scale;

                xrCamera.farClipPlane =
                    farClipMeters * scale;
            }


            BuildTable(
                tableCenter,
                tableSizeWorld,
                scale
            );


            if (buildFloor)
            {
                BuildFloor(
                    new Vector3(
                        tableCenter.x,
                        tableTopY - tableHeightMeters * scale,
                        tableCenter.z
                    ),
                    scale
                );
            }


            WorldUnitsPerMeter =
                scale;

            CityBounds =
                bounds;

            TableTopWorldY =
                tableTopY;


            Debug.Log(
                $"TabletopRig: city {bounds.size.x:0.00} × " +
                $"{bounds.size.z:0.00} km on a " +
                $"{tableSizeWorld.x / scale:0.00} × " +
                $"{tableSizeWorld.y / scale:0.00} m table, " +
                $"1 m = {scale:0.000} km (scale 1:" +
                $"{scale * 1000.0f:0}), table top " +
                $"{tableHeightMeters:0.00} m.",
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

        private void BuildTable(
            Vector3 topCenter,
            Vector2 sizeWorld,
            float scale
        )
        {
            float thickness =
                tableThicknessMeters * scale;


            GameObject top =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube
                );

            top.name =
                "TableTop";

            top.transform.SetParent(
                transform,
                false
            );

            top.transform.position =
                topCenter - Vector3.up * (0.5f * thickness);

            top.transform.localScale =
                new Vector3(
                    sizeWorld.x,
                    thickness,
                    sizeWorld.y
                );


            // Keeps the collider: rays that miss the city stop on
            // the table instead of hitting things below it.
            ApplyMaterial(
                top,
                ref tableMaterial,
                new Color(0.16f, 0.17f, 0.19f)
            );


            // A pedestal so the table reads as standing on the floor.
            float legHeight =
                tableHeightMeters * scale - thickness;

            GameObject pedestal =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube
                );

            pedestal.name =
                "TablePedestal";

            pedestal.transform.SetParent(
                transform,
                false
            );

            pedestal.transform.position =
                topCenter -
                Vector3.up * (thickness + 0.5f * legHeight);

            pedestal.transform.localScale =
                new Vector3(
                    sizeWorld.x * 0.6f,
                    legHeight,
                    sizeWorld.y * 0.6f
                );

            ApplyMaterial(
                pedestal,
                ref tableMaterial,
                new Color(0.16f, 0.17f, 0.19f)
            );
        }


        private void BuildFloor(
            Vector3 center,
            float scale
        )
        {
            GameObject floor =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad
                );

            floor.name =
                "Floor";

            floor.transform.SetParent(
                transform,
                false
            );

            floor.transform.SetPositionAndRotation(
                center,
                Quaternion.Euler(
                    90.0f,
                    0.0f,
                    0.0f
                )
            );

            floor.transform.localScale =
                Vector3.one * (floorSizeMeters * scale);


            ApplyMaterial(
                floor,
                ref floorMaterial,
                new Color(0.32f, 0.33f, 0.35f)
            );
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
