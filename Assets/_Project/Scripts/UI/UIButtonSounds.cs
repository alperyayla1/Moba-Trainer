using MobaTrainer.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>Click sound for any Selectable, played on press (not release) so it feels instant. No hover sounds.</summary>
    public class UIButtonSounds : MonoBehaviour, IPointerDownHandler
    {
        public bool click = true;

        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!click || eventData.button != PointerEventData.InputButton.Left) return;
            if (selectable != null && !selectable.IsInteractable()) return;
            if (UIAudio.Instance != null) UIAudio.Instance.PlayClick();
        }
    }
}
