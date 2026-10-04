using MobaTrainer.Core;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Short-lived ground effects (click indicators, blink flashes, impact rings) animated
    /// from a fixed-size array — no allocations, no coroutines.
    /// </summary>
    public class FxSystem : MonoBehaviour
    {
        public static FxSystem Instance { get; private set; }

        [Header("Pooling")]
        public GroundMarker markerPrefab;
        public Material transparentMaterial;
        public int capacity = 48;

        private struct Fx
        {
            public GroundMarker marker;
            public float age;
            public float duration;
            public float scaleFrom;
            public float scaleTo;
            public Color color;
            public Transform follow;
        }

        private ComponentPool<GroundMarker> pool;
        private Fx[] fx;
        private int count;
        private Mesh clickRing;
        private Mesh flashRing;
        private Mesh disc;

        private void Awake()
        {
            Instance = this;
            pool = new ComponentPool<GroundMarker>(markerPrefab, transform, capacity);
            foreach (GroundMarker m in GetComponentsInChildren<GroundMarker>(true)) m.SetMaterial(transparentMaterial);
            fx = new Fx[capacity];
            clickRing = MeshFactory.Ring(0.5f, 0.09f, 0.03f, 48);
            flashRing = MeshFactory.Ring(1f, 0.12f, 0.08f, 64);
            disc = MeshFactory.Disc(1f, 0.55f, 0f, 0.02f, 48);
        }

        /// <summary>Right-click / attack-move acknowledgement: shrinking ring at the ground point.</summary>
        public void ClickIndicator(Vector3 position, Color color)
        {
            Spawn(clickRing, position, color, 0.32f, 1.25f, 0.35f, null);
        }

        /// <summary>Pulse ring attached to a unit (attack order acknowledgement).</summary>
        public void UnitPulse(Transform unit, float radius, Color color)
        {
            Spawn(flashRing, unit.position, color, 0.3f, radius * 1.4f, radius * 0.9f, unit);
        }

        /// <summary>Expanding flash, e.g. blink origin/destination or skillshot impact.</summary>
        public void Flash(Vector3 position, float radius, Color color, float duration = 0.35f)
        {
            Spawn(flashRing, position, color, duration, radius * 0.4f, radius, null);
        }

        public void Burst(Vector3 position, float radius, Color color, float duration = 0.25f)
        {
            Spawn(disc, position, color, duration, radius * 0.6f, radius, null);
        }

        private void Spawn(Mesh mesh, Vector3 position, Color color, float duration, float from, float to, Transform follow)
        {
            if (count >= fx.Length)
            {
                // Recycle the oldest effect.
                Kill(0);
            }
            GroundMarker m = pool.Get();
            m.SetMaterial(transparentMaterial);
            m.Set(mesh, position, Quaternion.identity, Vector3.one * from, color, 0.03f);
            fx[count++] = new Fx { marker = m, age = 0f, duration = duration, scaleFrom = from, scaleTo = to, color = color, follow = follow };
        }

        private void Kill(int index)
        {
            pool.Release(fx[index].marker);
            fx[index] = fx[count - 1];
            fx[count - 1] = default;
            count--;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = count - 1; i >= 0; i--)
            {
                ref Fx f = ref fx[i];
                f.age += dt;
                float t = f.age / f.duration;
                if (t >= 1f)
                {
                    Kill(i);
                    continue;
                }
                float ease = 1f - (1f - t) * (1f - t);
                float s = Mathf.Lerp(f.scaleFrom, f.scaleTo, ease);
                Transform tr = f.marker.transform;
                tr.localScale = new Vector3(s, 1f, s);
                if (f.follow != null)
                {
                    Vector3 p = f.follow.position;
                    p.y = 0.03f;
                    tr.position = p;
                }
                Color c = f.color;
                c.a *= 1f - t;
                f.marker.SetColor(c);
            }
        }

        public void ClearAll()
        {
            while (count > 0) Kill(count - 1);
        }
    }
}
