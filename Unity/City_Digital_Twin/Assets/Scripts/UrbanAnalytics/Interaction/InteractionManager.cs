using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using UrbanAnalytics.Core;
using UrbanAnalytics.Data;
using UrbanAnalytics.Rendering;
using UrbanAnalytics.Spatial;
using UrbanAnalytics.UrbanContext;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// Picking, hover and selection (desktop and VR).
    ///
    /// A pointer ray hits a MeshCollider on a shown chunk;
    /// RaycastHit.triangleIndex is resolved to the entity through
    /// the chunk's triangle → entity map (SpatialMeshChunk for
    /// cells, height columns and glyphs; BuildingMeshChunk for
    /// buildings). No GameObject per entity is needed.
    ///
    /// Input comes from an IInteractionPointer: the VR controller
    /// pointer when one is assigned, otherwise the built-in
    /// DesktopMousePointer. Mouse: hover shows the entity; left click
    /// selects; click on empty space clears; Alt + click selects the
    /// cell (block) of a building. Keys: Esc clears, F focuses the
    /// selection.
    ///
    /// Other features (comparison, UI) use Context, Info and
    /// CollectGeometry, and listen to HoverChanged /
    /// SelectionChanged.
    /// </summary>
    public sealed class InteractionManager :
        MonoBehaviour
    {
        private const string HoverKey =
            "hover";

        private const string SelectionKey =
            "selection";


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Dependencies")]
        [Tooltip(
            "Camera rays are cast from. Defaults to Camera.main."
        )]
        [SerializeField]
        private Camera interactionCamera;

        [SerializeField]
        private DesktopCameraController cameraController;

        [Tooltip(
            "Optional pointer (a component implementing " +
            "IInteractionPointer, e.g. the VR controller pointer). " +
            "Empty = desktop mouse."
        )]
        [SerializeField]
        private MonoBehaviour pointerSource;

        [SerializeField]
        private VisualizationManager visualizationManager;

        [SerializeField]
        private GeometryManager geometryManager;

        [SerializeField]
        private UrbanContextManager urbanContextManager;

        [SerializeField]
        private SpatialLayerManager spatialLayerManager;

        [SerializeField]
        private DataLayerManager dataLayerManager;

        [SerializeField]
        private SpatialReferenceManager spatialReferenceManager;


        [Header("Picking")]
        [Tooltip(
            "Building colliders cost memory (every building " +
            "triangle is cooked). Turn off on constrained devices " +
            "to pick cells only."
        )]
        [SerializeField]
        private bool pickBuildings =
            true;

        [Tooltip(
            "Runtime switch: off = buildings are not hovered or " +
            "selected; rays pass through them to the area below. " +
            "Their colliders stay baked (no rebake)."
        )]
        [SerializeField]
        private bool buildingsSelectable =
            true;


        [Header("Highlight")]
        [Tooltip(
            "Material using UrbanAnalytics/HighlightOverlay."
        )]
        [SerializeField]
        private Material highlightMaterial;

        [SerializeField]
        private Color hoverColor =
            new Color(1.0f, 1.0f, 1.0f, 0.35f);

        [SerializeField]
        private Color selectionColor =
            new Color(1.0f, 0.83f, 0.0f, 0.55f);


        // =========================================================
        // RUNTIME
        // =========================================================

        private PickingColliders pickingColliders;

        private EntityHighlighter highlighter;

        private EntityGeometryCollector geometryCollector;

        private EntityInfoBuilder infoBuilder;


        // Colours of persistent overlays (comparison slots), so
        // they can be re-collected after a visualization change.
        private readonly Dictionary<string, Color>
            persistentColors =
                new Dictionary<string, Color>(
                    StringComparer.Ordinal
                );


        private bool pickingDirty =
            true;

        private bool highlightsDirty;

        private bool hasLoggedFirstBake;


        private IInteractionPointer pointer;


        // =========================================================
        // PUBLIC STATE
        // =========================================================

        public InteractionContext Context
        {
            get;
            private set;
        }


        public EntityInfoBuilder Info =>
            infoBuilder;


        public bool IsReady =>
            Context != null;


        /// <summary>
        /// False while colliders are missing or being rebuilt
        /// after a visualization change.
        /// </summary>
        public bool IsPickingReady
        {
            get;
            private set;
        }


        public string PickingStatus
        {
            get;
            private set;
        } = "Waiting for the city to load…";


        public PickResult Hovered
        {
            get;
            private set;
        }


        public EntityReference Selected
        {
            get;
            private set;
        }


        public bool HasSelection =>
            Selected.IsValid;


        public Camera InteractionCamera =>
            interactionCamera;


        public DesktopCameraController CameraController =>
            cameraController;


        /// <summary>
        /// The input source in use (VR pointer or desktop mouse).
        /// </summary>
        public IInteractionPointer Pointer =>
            pointer;


        /// <summary>
        /// Off: buildings are not hovered or selected; picking rays
        /// pass through them to the area below (VR toolbar toggle).
        /// </summary>
        public bool BuildingsSelectable
        {
            get => buildingsSelectable;
            set
            {
                if (buildingsSelectable == value)
                {
                    return;
                }

                buildingsSelectable =
                    value;

                Debug.Log(
                    $"InteractionManager: buildings selectable = {value}.",
                    this
                );

                BuildingsSelectableChanged?.Invoke(
                    value
                );
            }
        }


        // =========================================================
        // EVENTS
        // =========================================================

        public event Action<bool> BuildingsSelectableChanged;

        public event Action<PickResult> HoverChanged;

        public event Action<EntityReference> SelectionChanged;

        /// <summary>
        /// Raised when shown geometry changed (visualization
        /// applied or cleared) and highlights/copies were
        /// refreshed. Info should be rebuilt by listeners.
        /// </summary>
        public event Action SceneRefreshed;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            ResolveDependencies();
        }


        private void OnEnable()
        {
            if (visualizationManager == null)
            {
                return;
            }


            visualizationManager.VisualizationChanging +=
                HandleVisualizationChanging;

            visualizationManager.VisualizationApplied +=
                HandleVisualizationApplied;

            visualizationManager.VisualizationCleared +=
                HandleVisualizationCleared;
        }


        private void OnDisable()
        {
            if (visualizationManager == null)
            {
                return;
            }


            visualizationManager.VisualizationChanging -=
                HandleVisualizationChanging;

            visualizationManager.VisualizationApplied -=
                HandleVisualizationApplied;

            visualizationManager.VisualizationCleared -=
                HandleVisualizationCleared;
        }


        private void OnDestroy()
        {
            pickingColliders?.Dispose();

            highlighter?.Dispose();
        }


        private void Update()
        {
            if (!TryInitialize())
            {
                return;
            }


            UpdatePicking();

            UpdateHighlights();

            HandlePointer();

            HandleKeyboard();
        }


        // =========================================================
        // INITIALIZATION
        // =========================================================

        private bool TryInitialize()
        {
            if (Context != null)
            {
                return true;
            }


            if (visualizationManager == null ||
                geometryManager == null ||
                spatialLayerManager == null ||
                dataLayerManager == null ||
                spatialReferenceManager == null)
            {
                PickingStatus =
                    "Interaction disabled: a runtime manager " +
                    "is missing.";

                return false;
            }


            if (!geometryManager.IsInitialized ||
                !dataLayerManager.IsInitialized ||
                !spatialLayerManager.IsInitialized)
            {
                return false;
            }


            if (highlightMaterial == null)
            {
                Shader shader =
                    Shader.Find(
                        "UrbanAnalytics/HighlightOverlay"
                    );


                if (shader == null)
                {
                    Debug.LogError(
                        "InteractionManager: assign a Highlight " +
                        "Material (UrbanAnalytics/HighlightOverlay).",
                        this
                    );

                    enabled =
                        false;

                    return false;
                }


                highlightMaterial =
                    new Material(
                        shader
                    );
            }


            Context =
                new InteractionContext(
                    dataLayerManager,
                    spatialLayerManager,
                    spatialReferenceManager,
                    geometryManager,
                    urbanContextManager,
                    visualizationManager
                );


            pickingColliders =
                new PickingColliders();

            geometryCollector =
                new EntityGeometryCollector(
                    Context
                );

            infoBuilder =
                new EntityInfoBuilder(
                    Context
                );

            highlighter =
                new EntityHighlighter(
                    transform,
                    highlightMaterial
                );


            return true;
        }


        // =========================================================
        // PICKING COLLIDERS
        // =========================================================

        private void UpdatePicking()
        {
            if (pickingColliders.IsBaking)
            {
                if (!pickingColliders.TryFinish())
                {
                    return;
                }


                LogBake();
            }


            if (!pickingDirty)
            {
                return;
            }


            // Wait until the scene is stable: buildings loaded
            // (if any) and no visualization being applied.
            bool buildingsPending =
                urbanContextManager != null &&
                urbanContextManager.isActiveAndEnabled &&
                (urbanContextManager.IsInitializing ||
                 (urbanContextManager.IsInitialized &&
                  !urbanContextManager.AreBuildingsLoaded));


            if (visualizationManager.IsBusy ||
                buildingsPending)
            {
                IsPickingReady =
                    false;

                PickingStatus =
                    "Waiting for the visualization…";

                return;
            }


            pickingDirty =
                false;


            Context.InvalidateScene();


            int count =
                pickingColliders.Begin(
                    Context.SpatialChunks,
                    Context.BuildingChunks,
                    pickBuildings
                );


            if (count == 0)
            {
                IsPickingReady =
                    true;

                PickingStatus =
                    null;

                return;
            }


            IsPickingReady =
                false;

            PickingStatus =
                $"Preparing picking ({count} meshes)…";
        }


        private void LogBake()
        {
            IsPickingReady =
                !pickingDirty;

            PickingStatus =
                IsPickingReady
                    ? null
                    : PickingStatus;


            string message =
                $"Picking colliders baked: " +
                $"{pickingColliders.LastBakedMeshCount} meshes, " +
                $"{pickingColliders.LastBakedTriangleCount:N0} " +
                $"triangles in " +
                $"{pickingColliders.LastBakeSeconds:0.00} s " +
                $"(buildings pickable: {pickBuildings}).";


            // The first (whole-city) bake is worth recording for
            // performance notes; later ones are small.
            if (!hasLoggedFirstBake)
            {
                hasLoggedFirstBake =
                    true;

                Debug.Log(
                    message,
                    this
                );
            }
        }


        // =========================================================
        // VISUALIZATION EVENTS
        // =========================================================

        private void HandleVisualizationChanging()
        {
            // Meshes are about to be modified: the bake job must
            // not be reading them.
            pickingColliders?.CompleteNow();


            pickingDirty =
                true;

            IsPickingReady =
                false;


            SetHovered(
                default
            );
        }


        private void HandleVisualizationApplied(
            VisualizationSpec visualization
        )
        {
            MarkSceneChanged();
        }


        private void HandleVisualizationCleared()
        {
            MarkSceneChanged();
        }


        private void MarkSceneChanged()
        {
            pickingDirty =
                true;

            highlightsDirty =
                true;


            Context?.InvalidateScene();

            geometryCollector?.Invalidate();
        }


        // =========================================================
        // HIGHLIGHTS
        // =========================================================

        private void UpdateHighlights()
        {
            if (!highlightsDirty ||
                visualizationManager.IsBusy)
            {
                return;
            }


            highlightsDirty =
                false;


            // Re-collect every overlay from the geometry that is
            // shown now (a column may have replaced a flat cell).
            foreach (string key in highlighter.GetKeys())
            {
                if (highlighter.TryGetEntity(
                        key,
                        out EntityReference entity,
                        out bool includeBuildings
                    ))
                {
                    RefreshOverlay(
                        key,
                        entity,
                        includeBuildings,
                        null
                    );
                }
            }


            SceneRefreshed?.Invoke();
        }


        private void RefreshOverlay(
            string key,
            EntityReference entity,
            bool includeBuildings,
            Color? color
        )
        {
            Color resolved =
                color ??
                (key == HoverKey
                    ? hoverColor
                    : key == SelectionKey
                        ? selectionColor
                        : persistentColors.TryGetValue(
                            key,
                            out Color stored
                        )
                            ? stored
                            : selectionColor);


            highlighter.Show(
                key,
                entity,
                includeBuildings,
                geometryCollector.Collect(
                    entity,
                    includeBuildings
                ),
                resolved
            );
        }


        /// <summary>
        /// Keeps an extra overlay on an entity (e.g. comparison
        /// slot markers) until cleared. It follows visualization
        /// changes like the selection does.
        /// </summary>
        public void SetPersistentHighlight(
            string key,
            EntityReference entity,
            Color color
        )
        {
            if (!IsReady)
            {
                return;
            }


            if (key == HoverKey ||
                key == SelectionKey)
            {
                throw new ArgumentException(
                    $"'{key}' is reserved.",
                    nameof(key)
                );
            }


            if (!entity.IsValid)
            {
                ClearPersistentHighlight(
                    key
                );

                return;
            }


            persistentColors[key] =
                color;


            RefreshOverlay(
                key,
                entity,
                false,
                color
            );
        }


        public void ClearPersistentHighlight(
            string key
        )
        {
            persistentColors.Remove(
                key
            );

            highlighter?.Hide(
                key
            );
        }


        // =========================================================
        // POINTER
        // =========================================================

        private void HandlePointer()
        {
            if (pointer == null ||
                !pointer.TryGetFrame(
                    out PointerFrame frame
                ) ||
                !frame.HasRay)
            {
                SetHovered(
                    default
                );

                return;
            }


            // ----- hover -----

            if (frame.Blocked ||
                !IsPickingReady)
            {
                SetHovered(
                    default
                );
            }
            else
            {
                SetHovered(
                    Pick(
                        frame.Ray,
                        frame.MaxDistance
                    )
                );
            }


            // ----- click -----

            if (!frame.Clicked)
            {
                return;
            }


            // Every click is logged with its outcome, so "nothing
            // happened" can be told apart from "selected, but shown
            // elsewhere" in the Editor log.
            if (frame.Blocked)
            {
                Debug.Log(
                    "InteractionManager: click on a panel (UI), not on the city.",
                    this
                );

                return;
            }

            if (!IsPickingReady)
            {
                Debug.Log(
                    $"InteractionManager: click ignored, picking not ready " +
                    $"({PickingStatus}).",
                    this
                );

                return;
            }


            PickResult pick =
                Pick(
                    frame.Ray,
                    frame.MaxDistance
                );


            if (!pick.IsValid)
            {
                Debug.Log(
                    "InteractionManager: click on empty table, selection cleared.",
                    this
                );

                ClearSelection();

                return;
            }


            Debug.Log(
                $"InteractionManager: click selected {pick.Entity.Kind} " +
                $"'{pick.Entity}'" +
                (frame.SelectBlock ? " (area of the building requested)." : "."),
                this
            );


            if (frame.SelectBlock &&
                pick.Entity.TryGetUnit(
                    out EntityReference block
                ))
            {
                Select(
                    block
                );
            }
            else
            {
                Select(
                    pick.Entity
                );
            }
        }


        /// <summary>
        /// Resolves a screen point to the nearest pickable entity.
        /// </summary>
        public PickResult Pick(
            Vector2 screenPoint
        )
        {
            if (!IsReady ||
                interactionCamera == null)
            {
                return default;
            }


            return Pick(
                interactionCamera.ScreenPointToRay(
                    screenPoint
                ),
                interactionCamera.farClipPlane
            );
        }


        /// <summary>
        /// Resolves a world-space ray (e.g. a VR controller ray) to
        /// the nearest pickable entity.
        /// </summary>
        public PickResult Pick(
            Ray ray,
            float maxDistance
        )
        {
            if (!IsReady)
            {
                return default;
            }


            int count =
                Physics.RaycastNonAlloc(
                    ray,
                    rayHits,
                    maxDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore
                );

            Array.Sort(
                rayHits,
                0,
                count,
                HitDistanceComparer
            );


            // Nearest hit that is a city entity. Skipped: the XR rig's
            // body capsule (CharacterController), and buildings while
            // they are not selectable (the ray goes on to the area
            // below). Any other collider (e.g. the VR table top) stops
            // the ray: nothing is picked.
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit =
                    rayHits[i];

                if (hit.collider is CharacterController)
                {
                    continue;
                }


                PickResult result =
                    Resolve(
                        hit
                    );

                if (!result.IsValid)
                {
                    if (hit.collider.TryGetComponent(out SpatialMeshChunk _) ||
                        hit.collider.TryGetComponent(out BuildingMeshChunk _))
                    {
                        continue;
                    }

                    return default;
                }

                if (!buildingsSelectable &&
                    result.Entity.Kind == EntityKind.Building)
                {
                    continue;
                }

                return result;
            }


            return default;
        }


        private static readonly RaycastHit[] rayHits =
            new RaycastHit[64];


        private static readonly IComparer<RaycastHit> HitDistanceComparer =
            Comparer<RaycastHit>.Create(
                (a, b) => a.distance.CompareTo(b.distance)
            );


        /// <summary>
        /// RaycastHit → entity through the hit chunk's triangle map.
        /// </summary>
        public PickResult Resolve(
            RaycastHit hit
        )
        {
            if (hit.collider == null ||
                hit.triangleIndex < 0)
            {
                return default;
            }


            if (hit.collider.TryGetComponent(
                    out SpatialMeshChunk spatialChunk
                ) &&
                spatialChunk.TryGetUnitIdForTriangle(
                    hit.triangleIndex,
                    out string unitId
                ))
            {
                return new PickResult(
                    EntityReference.ForUnit(
                        spatialChunk.SpatialLayerId,
                        unitId
                    ),
                    hit.point,
                    FindVisualizationLayerId(
                        spatialChunk.transform
                    )
                );
            }


            if (hit.collider.TryGetComponent(
                    out BuildingMeshChunk buildingChunk
                ) &&
                buildingChunk.TryGetBuildingForTriangle(
                    hit.triangleIndex,
                    out BuildingMeshUnitRange range
                ))
            {
                return new PickResult(
                    Context.CreateBuildingReference(
                        range
                    ),
                    hit.point,
                    null
                );
            }


            return default;
        }


        /// <summary>
        /// Visualization roots are named "__Visualization_{id}".
        /// </summary>
        private static string FindVisualizationLayerId(
            Transform transform
        )
        {
            const string prefix =
                "__Visualization_";


            for (
                Transform current = transform;
                current != null;
                current = current.parent
            )
            {
                if (current.name.StartsWith(
                        prefix,
                        StringComparison.Ordinal
                    ))
                {
                    return current.name.Substring(
                        prefix.Length
                    );
                }
            }


            return null;
        }


        private void SetHovered(
            PickResult pick
        )
        {
            if (pick.Entity == Hovered.Entity)
            {
                Hovered =
                    pick;

                return;
            }


            Hovered =
                pick;


            if (highlighter != null)
            {
                if (pick.IsValid &&
                    pick.Entity != Selected)
                {
                    RefreshOverlay(
                        HoverKey,
                        pick.Entity,
                        false,
                        hoverColor
                    );
                }
                else
                {
                    highlighter.Hide(
                        HoverKey
                    );
                }
            }


            HoverChanged?.Invoke(
                pick
            );
        }


        // =========================================================
        // SELECTION
        // =========================================================

        public void Select(
            EntityReference entity
        )
        {
            if (!IsReady)
            {
                return;
            }


            if (!entity.IsValid)
            {
                ClearSelection();

                return;
            }


            if (entity == Selected)
            {
                return;
            }


            Selected =
                entity;


            RefreshOverlay(
                SelectionKey,
                entity,
                false,
                selectionColor
            );


            if (Hovered.Entity == entity)
            {
                highlighter.Hide(
                    HoverKey
                );
            }


            SelectionChanged?.Invoke(
                entity
            );
        }


        public void ClearSelection()
        {
            if (!Selected.IsValid)
            {
                return;
            }


            Selected =
                default;


            highlighter?.Hide(
                SelectionKey
            );


            SelectionChanged?.Invoke(
                default
            );
        }


        /// <summary>
        /// Moves the camera to frame the entity as currently shown
        /// (a unit with its buildings).
        /// </summary>
        public bool Focus(
            EntityReference entity
        )
        {
            if (!IsReady ||
                cameraController == null ||
                !entity.IsValid)
            {
                return false;
            }


            EntityGeometry geometry =
                CollectGeometry(
                    entity,
                    true
                );


            if (geometry.IsEmpty)
            {
                return false;
            }


            cameraController.Focus(
                geometry.Bounds
            );


            return true;
        }


        public bool FocusSelection()
        {
            return Focus(
                Selected
            );
        }


        // =========================================================
        // SERVICES FOR OTHER FEATURES
        // =========================================================

        public EntityGeometry CollectGeometry(
            EntityReference entity,
            bool includeBuildings
        )
        {
            return IsReady
                ? geometryCollector.Collect(
                    entity,
                    includeBuildings
                )
                : new EntityGeometry();
        }


        public EntityGeometry CollectFootprint(
            EntityReference unit
        )
        {
            return IsReady
                ? geometryCollector.CollectFootprint(
                    unit
                )
                : new EntityGeometry();
        }


        public EntityInfo BuildInfo(
            EntityReference entity
        )
        {
            return IsReady
                ? infoBuilder.Build(
                    entity,
                    visualizationManager.ActiveVisualization
                )
                : new EntityInfo();
        }


        // =========================================================
        // KEYBOARD
        // =========================================================

        private void HandleKeyboard()
        {
            Keyboard keyboard =
                Keyboard.current;


            if (keyboard == null)
            {
                return;
            }


            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ClearSelection();
            }


            if (keyboard.fKey.wasPressedThisFrame)
            {
                FocusSelection();
            }
        }


        // =========================================================
        // DEPENDENCIES
        // =========================================================

        private void ResolveDependencies()
        {
            if (interactionCamera == null)
            {
                interactionCamera =
                    Camera.main;
            }


            if (cameraController == null &&
                interactionCamera != null)
            {
                cameraController =
                    interactionCamera
                        .GetComponent<DesktopCameraController>();
            }


            if (pointerSource != null &&
                pointerSource is IInteractionPointer custom)
            {
                pointer =
                    custom;
            }
            else
            {
                if (pointerSource != null)
                {
                    Debug.LogError(
                        $"InteractionManager: '{pointerSource.name}' " +
                        "does not implement IInteractionPointer; " +
                        "using the mouse.",
                        this
                    );
                }


                pointer =
                    new DesktopMousePointer(
                        () => interactionCamera,
                        () => cameraController
                    );
            }


            if (visualizationManager == null)
            {
                visualizationManager =
                    FindFirstObjectByType<VisualizationManager>();
            }


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


            if (spatialLayerManager == null)
            {
                spatialLayerManager =
                    FindFirstObjectByType<SpatialLayerManager>();
            }


            if (dataLayerManager == null)
            {
                dataLayerManager =
                    FindFirstObjectByType<DataLayerManager>();
            }


            if (spatialReferenceManager == null)
            {
                spatialReferenceManager =
                    FindFirstObjectByType<SpatialReferenceManager>();
            }
        }
    }
}
