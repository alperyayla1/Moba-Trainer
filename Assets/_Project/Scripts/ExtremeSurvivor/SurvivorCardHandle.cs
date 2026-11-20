using UnityEngine;
using UnityEngine.EventSystems;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>One card on the Extreme Survivor item offer screen: hover shows the full item tooltip next to the card.</summary>
    public class SurvivorCardHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public SurvivorUI ui;
        public SurvivorItem item;

        public void OnPointerEnter(PointerEventData e) => ui?.HoverCard(item, (RectTransform)transform);
        public void OnPointerExit(PointerEventData e) => ui?.HoverCard(null, null);
    }
}
