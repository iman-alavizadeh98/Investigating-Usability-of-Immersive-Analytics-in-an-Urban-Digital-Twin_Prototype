using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// Mouse picking input: a ray through the cursor; a click is a
    /// left press + release that moved less than the drag threshold
    /// and did not start over UI; Alt selects the block.
    ///
    /// This is the desktop behaviour InteractionManager had built in
    /// before the VR pointer existed (unchanged).
    /// </summary>
    public sealed class DesktopMousePointer :
        IInteractionPointer
    {
        private readonly System.Func<Camera> getCamera;

        private readonly System.Func<DesktopCameraController>
            getCameraController;


        private bool leftPressStartedOverUi;

        private Vector2 leftPressPosition;


        public DesktopMousePointer(
            System.Func<Camera> getCamera,
            System.Func<DesktopCameraController> getCameraController
        )
        {
            this.getCamera =
                getCamera;

            this.getCameraController =
                getCameraController;
        }


        public bool TryGetFrame(
            out PointerFrame frame
        )
        {
            frame =
                default;


            Mouse mouse =
                Mouse.current;

            Camera camera =
                getCamera();


            if (mouse == null ||
                camera == null)
            {
                return false;
            }


            Vector2 pointer =
                mouse.position.ReadValue();


            bool overUi =
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject();


            DesktopCameraController controller =
                getCameraController();

            bool dragging =
                controller != null &&
                controller.IsDragging;


            if (mouse.leftButton.wasPressedThisFrame)
            {
                leftPressStartedOverUi =
                    overUi;

                leftPressPosition =
                    pointer;
            }


            bool clicked =
                mouse.leftButton.wasReleasedThisFrame &&
                !leftPressStartedOverUi &&
                (pointer - leftPressPosition).magnitude <=
                    DesktopCameraController
                        .ClickDragThresholdPixels;


            frame =
                new PointerFrame
                {
                    HasRay = true,
                    Ray = camera.ScreenPointToRay(
                        pointer
                    ),
                    MaxDistance = camera.farClipPlane,
                    Blocked = overUi || dragging,
                    Clicked = clicked,
                    SelectBlock =
                        Keyboard.current != null &&
                        Keyboard.current.altKey.isPressed
                };


            return true;
        }
    }
}
