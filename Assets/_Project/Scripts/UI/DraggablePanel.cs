using UnityEngine;
using UnityEngine.EventSystems;

namespace MobaTrainer.UI
{
    /// <summary>Put on a window's header to drag the whole window (ported from Blindfold Chess).</summary>
    public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [Tooltip("Window to move. Defaults to the parent.")]
        public RectTransform targetPanel;

        private Vector2 pointerStart;
        private Vector2 panelStart;

        private void Awake()
        {
            if (targetPanel == null) targetPanel = transform.parent as RectTransform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (targetPanel == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(targetPanel.parent as RectTransform, eventData.position, eventData.pressEventCamera, out pointerStart);
            panelStart = targetPanel.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (targetPanel == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetPanel.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                targetPanel.anchoredPosition = panelStart + (local - pointerStart);
            }
        }

        public void ResetPosition()
        {
            if (targetPanel != null) targetPanel.anchoredPosition = Vector2.zero;
        }
    }
}
