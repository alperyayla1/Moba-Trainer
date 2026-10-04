using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>
    /// Smoothly tints extra graphics (borders, labels, icons) on hover/press, complementing the
    /// Selectable's own colour transition. Runs on unscaled time so it works while paused.
    /// </summary>
    public class UIHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Serializable]
        public class Target
        {
            public Graphic graphic;
            public Color normal = Color.white;
            public Color hover = Color.white;
            public Color pressed = Color.white;
            public Color disabled = new Color(1f, 1f, 1f, 0.35f);
        }

        public Target[] targets = new Target[0];
        public float duration = 0.12f;

        private Selectable selectable;
        private bool hovered;
        private bool pressed;
        private float t;          // 0 = normal, 1 = hover
        private float p;          // 0 = not pressed, 1 = pressed
        private bool lastInteractable = true;

        public void Add(Graphic graphic, Color normal, Color hover, Color pressedColor)
        {
            var list = new System.Collections.Generic.List<Target>(targets)
            {
                new Target { graphic = graphic, normal = normal, hover = hover, pressed = pressedColor, disabled = normal.WithAlphaMul(0.4f) },
            };
            targets = list.ToArray();
            graphic.color = normal;
        }

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            t = p = 0f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData e) => hovered = true;
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData e) => pressed = false;

        private void Update()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            float speed = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
            float tt = interactable && hovered ? 1f : 0f;
            float pt = interactable && pressed ? 1f : 0f;
            bool changed = !Mathf.Approximately(t, tt) || !Mathf.Approximately(p, pt) || interactable != lastInteractable;
            if (!changed) return;
            t = Mathf.MoveTowards(t, tt, speed);
            p = Mathf.MoveTowards(p, pt, speed * 1.5f);
            lastInteractable = interactable;
            Apply();
        }

        private void Apply()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            for (int i = 0; i < targets.Length; i++)
            {
                Target g = targets[i];
                if (g.graphic == null) continue;
                if (!interactable)
                {
                    g.graphic.color = g.disabled;
                    continue;
                }
                Color c = Color.Lerp(g.normal, g.hover, t);
                g.graphic.color = Color.Lerp(c, g.pressed, p);
            }
        }
    }

    internal static class ColorExt
    {
        public static Color WithAlphaMul(this Color c, float k)
        {
            c.a *= k;
            return c;
        }
    }
}
