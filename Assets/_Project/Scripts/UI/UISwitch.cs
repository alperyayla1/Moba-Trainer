using System;
using MobaTrainer.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>iOS-style on/off switch: pill track + sliding knob, animated on unscaled time.</summary>
    [RequireComponent(typeof(Button))]
    public class UISwitch : MonoBehaviour
    {
        public Image track;
        public RectTransform knob;
        public float travel = 20f;
        public float duration = 0.14f;

        public bool IsOn { get; private set; }
        public event Action<bool> Changed;

        private float t;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => Set(!IsOn, false, true));
        }

        public void Set(bool on, bool instant, bool notify = false)
        {
            IsOn = on;
            if (instant)
            {
                t = on ? 1f : 0f;
                Apply();
            }
            if (notify) Changed?.Invoke(on);
        }

        private void Update()
        {
            float target = IsOn ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            Apply();
        }

        private void Apply()
        {
            float e = t * t * (3f - 2f * t);
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel * 0.5f, travel * 0.5f, e), 0f);
            if (track != null) track.color = Color.Lerp(Palette.BorderStrong, Palette.Accent, e);
        }
    }
}
