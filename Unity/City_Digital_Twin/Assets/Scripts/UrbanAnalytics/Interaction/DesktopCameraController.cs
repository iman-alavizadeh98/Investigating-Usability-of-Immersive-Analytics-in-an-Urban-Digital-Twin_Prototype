using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using UrbanAnalytics.Rendering;
using UrbanAnalytics.UrbanContext;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// Map-style desktop camera: the camera orbits a pivot on the
    /// ground at a distance, yaw and pitch.
    ///
    /// Mouse:
    /// - left drag / middle drag: pan (the ground point under the
    ///   cursor stays under the cursor);
    /// - right drag: orbit;
    /// - wheel: zoom toward the point under the cursor.
    /// A left click without dragging is left to InteractionManager
    /// (selection).
    ///
    /// Keyboard: WASD / arrows pan, Q / E rotate, + / - and
    /// PageUp / PageDown zoom, Shift = faster, Home = home view.
    ///
    /// Input over UI is ignored. Units: 1 Unity unit = 1 km, so
    /// clip planes follow the distance (a fixed 0.3 near plane
    /// would cut away everything closer than 300 m).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DesktopCameraController :
        MonoBehaviour
    {
        /// <summary>
        /// Pointer movement (pixels) after which a press becomes
        /// a drag instead of a click. Shared with selection.
        /// </summary>
        public const float ClickDragThresholdPixels =
            6.0f;


        [Header("Dependencies")]
        [Tooltip(
            "Used to frame the home view once the spatial layers " +
            "are rendered. Found automatically when empty."
        )]
        [SerializeField]
        private GeometryManager geometryManager;


        // Found automatically; frames the buildings when a scene
        // has no spatial layers.
        private UrbanContextManager urbanContextManager;


        [Header("Home View")]
        [SerializeField]
        [Range(5.0f, 89.0f)]
        private float homePitch =
            55.0f;

        [SerializeField]
        private float homeYaw =
            0.0f;

        [Tooltip(
            "1 = the rendered spatial layers just fill the view."
        )]
        [SerializeField]
        [Min(0.1f)]
        private float homeFitMargin =
            0.9f;


        [Header("Limits")]
        [SerializeField]
        [Range(1.0f, 89.0f)]
        private float minimumPitch =
            8.0f;

        [SerializeField]
        [Range(1.0f, 89.9f)]
        private float maximumPitch =
            89.0f;

        [Tooltip(
            "Unity units (1 = 1 km)."
        )]
        [SerializeField]
        [Min(0.001f)]
        private float minimumDistance =
            0.03f;

        [SerializeField]
        [Min(0.01f)]
        private float maximumDistance =
            150.0f;


        [Header("Speeds")]
        [SerializeField]
        [Min(0.01f)]
        private float orbitDegreesPerPixel =
            0.25f;

        [Tooltip(
            "Distance change per wheel notch (0.15 = 15%)."
        )]
        [SerializeField]
        [Range(0.01f, 0.5f)]
        private float zoomStepPerNotch =
            0.15f;

        [Tooltip(
            "Keyboard pan speed in distances per second."
        )]
        [SerializeField]
        [Min(0.01f)]
        private float keyboardPanSpeed =
            0.8f;

        [SerializeField]
        [Min(1.0f)]
        private float keyboardOrbitDegreesPerSecond =
            90.0f;

        [Tooltip(
            "Keyboard zoom: distance factor per second."
        )]
        [SerializeField]
        [Min(1.01f)]
        private float keyboardZoomPerSecond =
            2.5f;

        [SerializeField]
        [Min(1.0f)]
        private float shiftMultiplier =
            3.0f;


        [Header("Smoothing")]
        [Tooltip(
            "Higher = snappier zoom, focus and keyboard motion."
        )]
        [SerializeField]
        [Min(1.0f)]
        private float sharpness =
            12.0f;


        [Header("Clip Planes")]
        [SerializeField]
        [Min(0.00001f)]
        private float nearClipPerDistance =
            0.002f;

        [SerializeField]
        [Min(0.00001f)]
        private float minimumNearClip =
            0.0002f;

        [SerializeField]
        [Min(1.0f)]
        private float farClipPerDistance =
            40.0f;

        [SerializeField]
        [Min(1.0f)]
        private float minimumFarClip =
            80.0f;


        private Camera controlledCamera;


        // Current (rendered) and target (smoothed toward) state.
        private Vector3 pivot;
        private Vector3 targetPivot;
        private float yaw;
        private float targetYaw;
        private float pitch;
        private float targetPitch;
        private float distance;
        private float targetDistance;


        private bool homeResolved;

        private Vector3 homePivot;

        private float homeDistance;


        private bool leftHeld;
        private bool leftDragging;
        private bool middleHeld;
        private bool rightHeld;
        private Vector2 leftPressPosition;
        private Vector2 previousPointer;


        public Camera Camera =>
            controlledCamera;


        /// <summary>
        /// True while the left button is held and has moved past
        /// the click threshold (a pan, not a click).
        /// </summary>
        public bool IsLeftDragging =>
            leftDragging;


        public bool IsDragging =>
            leftDragging ||
            middleHeld ||
            rightHeld;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            controlledCamera =
                GetComponent<Camera>();


            if (geometryManager == null)
            {
                geometryManager =
                    FindFirstObjectByType<GeometryManager>();
            }


            urbanContextManager =
                FindFirstObjectByType<UrbanContextManager>();


            InitializeFromTransform();
        }


        private void Update()
        {
            TryResolveHome();


            Mouse mouse =
                Mouse.current;

            Keyboard keyboard =
                Keyboard.current;


            if (mouse != null)
            {
                HandleMouse(
                    mouse
                );
            }


            if (keyboard != null)
            {
                HandleKeyboard(
                    keyboard
                );
            }


            Smooth();

            ApplyToCamera();
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        /// <summary>
        /// Moves the view to frame world-space bounds, keeping the
        /// current direction.
        /// </summary>
        public void Focus(
            Bounds bounds
        )
        {
            float radius =
                Mathf.Max(
                    bounds.extents.magnitude,
                    minimumDistance * 0.5f
                );


            targetPivot =
                bounds.center;


            targetDistance =
                Mathf.Clamp(
                    FitDistance(radius) * 1.4f,
                    minimumDistance,
                    maximumDistance
                );
        }


        public void ResetToHome()
        {
            if (!homeResolved)
            {
                return;
            }


            targetPivot =
                homePivot;

            targetDistance =
                homeDistance;

            targetYaw =
                homeYaw;

            targetPitch =
                Mathf.Clamp(
                    homePitch,
                    minimumPitch,
                    maximumPitch
                );
        }


        // =========================================================
        // MOUSE
        // =========================================================

        private void HandleMouse(
            Mouse mouse
        )
        {
            Vector2 pointer =
                mouse.position.ReadValue();


            bool overUi =
                IsPointerOverUi();


            // ----- presses (ignored when they start over UI) -----

            if (mouse.leftButton.wasPressedThisFrame &&
                !overUi)
            {
                leftHeld =
                    true;

                leftDragging =
                    false;

                leftPressPosition =
                    pointer;
            }


            if (mouse.middleButton.wasPressedThisFrame &&
                !overUi)
            {
                middleHeld =
                    true;
            }


            if (mouse.rightButton.wasPressedThisFrame &&
                !overUi)
            {
                rightHeld =
                    true;
            }


            if (!mouse.leftButton.isPressed)
            {
                leftHeld =
                    false;

                leftDragging =
                    false;
            }


            if (!mouse.middleButton.isPressed)
            {
                middleHeld =
                    false;
            }


            if (!mouse.rightButton.isPressed)
            {
                rightHeld =
                    false;
            }


            if (leftHeld &&
                !leftDragging &&
                (pointer - leftPressPosition).magnitude >
                    ClickDragThresholdPixels)
            {
                leftDragging =
                    true;

                // Pan from the press point, so the drag does not
                // jump by the threshold.
                previousPointer =
                    leftPressPosition;
            }


            // ----- drags -----

            if (rightHeld)
            {
                Vector2 delta =
                    pointer - previousPointer;


                yaw +=
                    delta.x * orbitDegreesPerPixel;

                pitch =
                    Mathf.Clamp(
                        pitch - delta.y * orbitDegreesPerPixel,
                        minimumPitch,
                        maximumPitch
                    );


                targetYaw =
                    yaw;

                targetPitch =
                    pitch;
            }
            else if (leftDragging ||
                     middleHeld)
            {
                PanBetween(
                    previousPointer,
                    pointer
                );
            }


            // ----- wheel -----

            float scroll =
                mouse.scroll.ReadValue().y;


            if (!overUi &&
                Mathf.Abs(scroll) > 0.01f)
            {
                // Wheel deltas differ by platform (120 per notch
                // on Windows); use the sign per event.
                ZoomAt(
                    pointer,
                    scroll > 0.0f
                        ? 1.0f - zoomStepPerNotch
                        : 1.0f / (1.0f - zoomStepPerNotch)
                );
            }


            previousPointer =
                pointer;
        }


        /// <summary>
        /// Grab-the-map pan on the horizontal plane through the
        /// pivot: the plane point under fromScreen moves to
        /// toScreen. Applied directly (not smoothed) so the map
        /// sticks to the cursor.
        /// </summary>
        private void PanBetween(
            Vector2 fromScreen,
            Vector2 toScreen
        )
        {
            var plane =
                new Plane(
                    Vector3.up,
                    new Vector3(0.0f, pivot.y, 0.0f)
                );


            if (!TryIntersect(plane, fromScreen, out Vector3 from) ||
                !TryIntersect(plane, toScreen, out Vector3 to))
            {
                return;
            }


            Vector3 delta =
                from - to;


            delta.y =
                0.0f;


            pivot +=
                delta;

            targetPivot +=
                delta;
        }


        private void ZoomAt(
            Vector2 screen,
            float factor
        )
        {
            float newDistance =
                Mathf.Clamp(
                    targetDistance * factor,
                    minimumDistance,
                    maximumDistance
                );


            float applied =
                newDistance / targetDistance;


            // Move the pivot toward the point under the cursor in
            // proportion, horizontally, so zooming "into" a place
            // keeps it roughly under the cursor.
            Ray ray =
                controlledCamera.ScreenPointToRay(
                    screen
                );


            Vector3 point;


            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    controlledCamera.farClipPlane
                ))
            {
                point =
                    hit.point;
            }
            else if (!TryIntersect(
                         new Plane(
                             Vector3.up,
                             new Vector3(0.0f, targetPivot.y, 0.0f)
                         ),
                         screen,
                         out point
                     ))
            {
                point =
                    targetPivot;
            }


            Vector3 shift =
                (point - targetPivot) *
                (1.0f - applied);


            shift.y =
                0.0f;


            targetPivot +=
                shift;

            targetDistance =
                newDistance;
        }


        // =========================================================
        // KEYBOARD
        // =========================================================

        private void HandleKeyboard(
            Keyboard keyboard
        )
        {
            float dt =
                Time.unscaledDeltaTime;


            float speed =
                keyboard.shiftKey.isPressed
                    ? shiftMultiplier
                    : 1.0f;


            Vector2 move =
                Vector2.zero;


            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                move.y += 1.0f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                move.y -= 1.0f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                move.x += 1.0f;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                move.x -= 1.0f;
            }


            if (move != Vector2.zero)
            {
                Quaternion heading =
                    Quaternion.Euler(
                        0.0f,
                        targetYaw,
                        0.0f
                    );


                Vector3 direction =
                    heading *
                    new Vector3(
                        move.x,
                        0.0f,
                        move.y
                    ).normalized;


                targetPivot +=
                    direction *
                    (keyboardPanSpeed * targetDistance * speed * dt);
            }


            if (keyboard.qKey.isPressed)
            {
                targetYaw -=
                    keyboardOrbitDegreesPerSecond * speed * dt;
            }

            if (keyboard.eKey.isPressed)
            {
                targetYaw +=
                    keyboardOrbitDegreesPerSecond * speed * dt;
            }


            bool zoomIn =
                keyboard.equalsKey.isPressed ||
                keyboard.numpadPlusKey.isPressed ||
                keyboard.pageUpKey.isPressed;

            bool zoomOut =
                keyboard.minusKey.isPressed ||
                keyboard.numpadMinusKey.isPressed ||
                keyboard.pageDownKey.isPressed;


            if (zoomIn != zoomOut)
            {
                float factor =
                    Mathf.Pow(
                        keyboardZoomPerSecond,
                        dt * speed
                    );


                targetDistance =
                    Mathf.Clamp(
                        zoomIn
                            ? targetDistance / factor
                            : targetDistance * factor,
                        minimumDistance,
                        maximumDistance
                    );
            }


            if (keyboard.homeKey.wasPressedThisFrame)
            {
                ResetToHome();
            }
        }


        // =========================================================
        // STATE → CAMERA
        // =========================================================

        private void Smooth()
        {
            float t =
                1.0f -
                Mathf.Exp(
                    -sharpness * Time.unscaledDeltaTime
                );


            pivot =
                Vector3.Lerp(
                    pivot,
                    targetPivot,
                    t
                );

            yaw =
                Mathf.LerpAngle(
                    yaw,
                    targetYaw,
                    t
                );

            pitch =
                Mathf.Lerp(
                    pitch,
                    targetPitch,
                    t
                );

            // Log-space, so zooming feels even at all scales.
            distance =
                Mathf.Exp(
                    Mathf.Lerp(
                        Mathf.Log(distance),
                        Mathf.Log(targetDistance),
                        t
                    )
                );
        }


        private void ApplyToCamera()
        {
            Quaternion rotation =
                Quaternion.Euler(
                    pitch,
                    yaw,
                    0.0f
                );


            transform.SetPositionAndRotation(
                pivot -
                rotation * Vector3.forward * distance,
                rotation
            );


            controlledCamera.nearClipPlane =
                Mathf.Max(
                    minimumNearClip,
                    distance * nearClipPerDistance
                );

            controlledCamera.farClipPlane =
                Mathf.Max(
                    minimumFarClip,
                    distance * farClipPerDistance
                );
        }


        // =========================================================
        // INITIAL / HOME
        // =========================================================

        private void InitializeFromTransform()
        {
            Vector3 euler =
                transform.rotation.eulerAngles;


            pitch =
                Mathf.Clamp(
                    euler.x > 180.0f
                        ? euler.x - 360.0f
                        : euler.x,
                    minimumPitch,
                    maximumPitch
                );

            yaw =
                euler.y;


            distance =
                Mathf.Clamp(
                    transform.position.y /
                    Mathf.Max(
                        0.05f,
                        Mathf.Sin(pitch * Mathf.Deg2Rad)
                    ),
                    minimumDistance,
                    maximumDistance
                );


            pivot =
                transform.position +
                Quaternion.Euler(pitch, yaw, 0.0f) *
                Vector3.forward *
                distance;


            pivot.y =
                0.0f;


            targetPivot =
                pivot;

            targetYaw =
                yaw;

            targetPitch =
                pitch;

            targetDistance =
                distance;
        }


        /// <summary>
        /// Once the spatial layers are rendered, frame them and
        /// jump there (the scene camera position is arbitrary).
        /// </summary>
        private void TryResolveHome()
        {
            if (homeResolved ||
                geometryManager == null ||
                !geometryManager.IsInitialized)
            {
                return;
            }


            // Buildings may be the only content (a city package
            // without spatial layers yet): wait until they are in.
            if (urbanContextManager != null &&
                urbanContextManager.isActiveAndEnabled &&
                urbanContextManager.IsInitializing)
            {
                return;
            }


            bool hasBounds =
                false;

            var bounds =
                new Bounds();


            foreach (
                SpatialMeshChunk chunk
                in FindObjectsByType<SpatialMeshChunk>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                )
            )
            {
                if (chunk.MeshRenderer == null)
                {
                    continue;
                }


                Bounds chunkBounds =
                    chunk.MeshRenderer.bounds;


                if (!hasBounds)
                {
                    bounds =
                        chunkBounds;

                    hasBounds =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        chunkBounds
                    );
                }
            }


            // No spatial layers: frame the buildings instead.
            if (!hasBounds &&
                urbanContextManager != null)
            {
                foreach (
                    BuildingMeshChunk chunk
                    in urbanContextManager.BuildingChunks
                )
                {
                    if (chunk == null ||
                        chunk.MeshRenderer == null)
                    {
                        continue;
                    }


                    if (!hasBounds)
                    {
                        bounds =
                            chunk.MeshRenderer.bounds;

                        hasBounds =
                            true;
                    }
                    else
                    {
                        bounds.Encapsulate(
                            chunk.MeshRenderer.bounds
                        );
                    }
                }
            }


            homeResolved =
                true;


            if (!hasBounds)
            {
                return;
            }


            homePivot =
                new Vector3(
                    bounds.center.x,
                    bounds.min.y,
                    bounds.center.z
                );


            float radius =
                new Vector2(
                    bounds.extents.x,
                    bounds.extents.z
                ).magnitude;


            homeDistance =
                Mathf.Clamp(
                    FitDistance(radius) * homeFitMargin,
                    minimumDistance,
                    maximumDistance
                );


            ResetToHome();


            // Jump, do not glide across the city on startup.
            pivot =
                targetPivot;

            yaw =
                targetYaw;

            pitch =
                targetPitch;

            distance =
                targetDistance;
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private float FitDistance(
            float radius
        )
        {
            float halfFov =
                0.5f *
                Mathf.Min(
                    controlledCamera.fieldOfView,
                    Camera.VerticalToHorizontalFieldOfView(
                        controlledCamera.fieldOfView,
                        controlledCamera.aspect
                    )
                ) *
                Mathf.Deg2Rad;


            return
                radius /
                Mathf.Max(
                    0.05f,
                    Mathf.Sin(halfFov)
                );
        }


        private bool TryIntersect(
            Plane plane,
            Vector2 screen,
            out Vector3 point
        )
        {
            Ray ray =
                controlledCamera.ScreenPointToRay(
                    screen
                );


            if (plane.Raycast(
                    ray,
                    out float enter
                ) &&
                enter > 0.0f)
            {
                point =
                    ray.GetPoint(
                        enter
                    );

                return true;
            }


            point =
                default;

            return false;
        }


        private static bool IsPointerOverUi()
        {
            return
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject();
        }
    }
}
