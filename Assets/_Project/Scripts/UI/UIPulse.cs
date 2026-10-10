using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
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
