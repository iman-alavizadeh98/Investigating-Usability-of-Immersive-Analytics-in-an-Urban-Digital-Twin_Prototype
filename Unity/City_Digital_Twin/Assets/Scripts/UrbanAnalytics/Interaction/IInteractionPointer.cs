using UnityEngine;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// One frame of pointer input for picking, independent of the
    /// device (desktop mouse, VR controller ray).
    /// </summary>
    public struct PointerFrame
    {
        /// <summary>False when the device has no ray this frame
        /// (no mouse, controller not tracked).</summary>
        public bool HasRay;

        /// <summary>World-space ray to pick along.</summary>
        public Ray Ray;

        /// <summary>World units; the ray stops here.</summary>
        public float MaxDistance;

        /// <summary>
        /// True while the pointer must not hover the city: it is over
        /// UI, or the user is dragging the camera.
        /// </summary>
        public bool Blocked;

        /// <summary>
        /// A completed "select" this frame (desktop: click without
        /// drag that did not start over UI; VR: trigger press).
        /// </summary>
        public bool Clicked;

        /// <summary>
        /// Select the cell (block) of a building instead of the
        /// building (desktop: Alt held).
        /// </summary>
        public bool SelectBlock;
    }


    /// <summary>
    /// Source of picking input for InteractionManager. Implemented by
    /// DesktopMousePointer (built in, used when no source is set) and
    /// by the VR controller pointer.
    /// </summary>
    public interface IInteractionPointer
    {
        /// <summary>
        /// Reads this frame's input. Called once per frame by
        /// InteractionManager. Returns false when the source is not
        /// usable at all (then nothing is hovered).
        /// </summary>
        bool TryGetFrame(
            out PointerFrame frame
        );
    }
}
