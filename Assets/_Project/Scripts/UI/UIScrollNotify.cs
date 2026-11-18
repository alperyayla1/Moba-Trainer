using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MobaTrainer.UI
{
    /// <summary>Raises <see cref="Scrolled"/> with +1 (wheel up) / -1 (wheel down) while the pointer is over this rect.</summary>
    public class UIScrollNotify : MonoBehaviour, IScrollHandler
    {
        public event Action<int> Scrolled;

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) < 0.01f) return;
            Scrolled?.Invoke(e.scrollDelta.y > 0f ? 1 : -1);
        }
    }
}
