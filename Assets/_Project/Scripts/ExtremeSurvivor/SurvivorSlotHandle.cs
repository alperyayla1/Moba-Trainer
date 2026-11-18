using UnityEngine;
using UnityEngine.EventSystems;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>One item slot on the Extreme Survivor HUD: hover shows the tooltip, left-drag moves the item to another slot.</summary>
    public class SurvivorSlotHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public SurvivorUI ui;
        public int index;

        public void OnPointerEnter(PointerEventData e) => ui?.HoverSlot(index);
        public void OnPointerExit(PointerEventData e) => ui?.HoverSlot(-1);

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            ui?.BeginDrag(index);
            ui?.Drag(e.position);
        }

        public void OnDrag(PointerEventData e) => ui?.Drag(e.position);
        public void OnEndDrag(PointerEventData e) => ui?.EndDrag();
        public void OnDrop(PointerEventData e) => ui?.Drop(index);
    }
}
