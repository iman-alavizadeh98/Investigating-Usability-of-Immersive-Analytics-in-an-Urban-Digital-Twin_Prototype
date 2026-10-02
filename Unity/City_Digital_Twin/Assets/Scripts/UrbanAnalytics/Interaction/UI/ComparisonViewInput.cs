using UnityEngine;
using UnityEngine.EventSystems;

namespace UrbanAnalytics.Interaction.UI
{
    /// <summary>
    /// Drag on a comparison view rotates both copies; the wheel
    /// zooms both (linked views).
    /// </summary>
    internal sealed class ComparisonViewInput :
        MonoBehaviour,
        IDragHandler,
        IScrollHandler
    {
        public ComparisonManager Comparison;


        public void OnDrag(
            PointerEventData eventData
        )
        {
            Comparison?.Orbit(
                eventData.delta
            );
        }


        public void OnScroll(
            PointerEventData eventData
        )
        {
            if (Comparison == null ||
                Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
            {
                return;
            }


            Comparison.ZoomBy(
                eventData.scrollDelta.y > 0.0f
                    ? 1.15f
                    : 1.0f / 1.15f
            );
        }
    }
}
