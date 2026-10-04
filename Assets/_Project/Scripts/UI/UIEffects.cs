using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>Smoothly scales the element up while hovered (unscaled time).</summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.05f;
        public float pressedScale = 0.97f;
        public float sharpness = 18f;

        private bool hovered;
        private bool pressed;
        private Selectable selectable;

        private void Awake() => selectable = GetComponent<Selectable>();

        private void OnDisable()
        {
            hovered = pressed = false;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData e) => hovered = true;
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) => pressed = true;
        public void OnPointerUp(PointerEventData e) => pressed = false;

        private void Update()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            float target = !interactable ? 1f : pressed ? pressedScale : hovered ? hoverScale : 1f;
            float s = transform.localScale.x;
            if (Mathf.Abs(s - target) < 0.0005f) return;
            s = Mathf.Lerp(s, target, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Raises events on pointer enter/exit (works on non-interactable buttons too).</summary>
    public class UIHoverNotify : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action Entered;
        public event Action Exited;

        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exited?.Invoke();
    }

    /// <summary>Constant slow rotation for background decoration.</summary>
    public class UISpin : MonoBehaviour
    {
        public float degreesPerSecond = 4f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }

    /// <summary>Gentle alpha "breathing" for glows.</summary>
    [RequireComponent(typeof(Graphic))]
    public class UIPulse : MonoBehaviour
    {
        public float minAlpha = 0.10f;
        public float maxAlpha = 0.20f;
        public float period = 6f;

        private Graphic graphic;
        private float phase;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            phase = UnityEngine.Random.value * period;
        }

        private void Update()
        {
            float t = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime + phase) / period * Mathf.PI * 2f);
            Color c = graphic.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            graphic.color = c;
        }
    }
}
