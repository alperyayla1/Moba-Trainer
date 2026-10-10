using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>Raises events on pointer enter/exit (works on non-interactable buttons too).</summary>
    public class UIHoverNotify : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action Entered;
        public event Action Exited;

        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exited?.Invoke();
    }
}
