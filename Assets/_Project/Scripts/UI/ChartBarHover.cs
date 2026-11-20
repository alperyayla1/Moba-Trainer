using UnityEngine;
using UnityEngine.EventSystems;

namespace MobaTrainer.UI
{
    /// <summary>One bar of the Stats chart: hovering shows that run's score above it.</summary>
    public class ChartBarHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public StatsPanel panel;
        public string text;

        public void OnPointerEnter(PointerEventData e) => panel?.ShowBarTip((RectTransform)transform, text);
        public void OnPointerExit(PointerEventData e) => panel?.HideBarTip();
        private void OnDisable() => panel?.HideBarTip();
    }
}
