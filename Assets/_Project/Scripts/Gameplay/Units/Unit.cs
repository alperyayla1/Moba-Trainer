using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    public enum Team
    {
        Player = 0,
        Enemy = 1,
        Neutral = 2,
    }

    /// <summary>
    /// Anything with health that can be targeted: the player, dummies, creeps, chasers, the duel AI.
    /// Units register themselves in <see cref="UnitRegistry"/> so queries never allocate or use physics.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        [Header("Unit")]
        public Team team = Team.Enemy;
        [Tooltip("Gameplay radius on the ground plane (hit tests, attack range is edge-to-edge).")]
        public float radius = 0.5f;
        public float height = 2f;
        public float maxHealth = 100f;

        [Header("Visuals")]
        [Tooltip("Renderers that flash white when hit.")]
        public Renderer[] flashRenderers;
        public HealthBar healthBar;

        public float Health { get; private set; }
        public float Shield { get; private set; }
        public bool IsAlive => Health > 0f;
        public float Health01 => maxHealth > 0f ? Health / maxHealth : 0f;
        public Vector3 Position => transform.position;

        /// <summary>(unit, damageTaken, source)</summary>
        public event Action<Unit, float, Unit> Damaged;
        public event Action<Unit, Unit> Died;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock mpb;
        private Color[] baseColors;
        private int[] colorProps;
        private float flashTimer;
        private float shieldTimer;
        private const float FlashDuration = 0.12f;

        protected virtual void Awake()
        {
            mpb = new MaterialPropertyBlock();
            CacheBaseColors();
            Health = maxHealth;
        }

        protected virtual void OnEnable()
        {
            UnitRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            UnitRegistry.Unregister(this);
        }

        private void CacheBaseColors()
        {
            int n = flashRenderers != null ? flashRenderers.Length : 0;
            baseColors = new Color[n];
            colorProps = new int[n];
            for (int i = 0; i < n; i++)
            {
                Renderer r = flashRenderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                Material m = r.sharedMaterial;
                colorProps[i] = m.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                baseColors[i] = m.HasProperty(colorProps[i]) ? m.GetColor(colorProps[i]) : Color.white;
            }
        }

        public void ResetUnit(float? newMaxHealth = null)
        {
            if (newMaxHealth.HasValue) maxHealth = newMaxHealth.Value;
            Health = maxHealth;
            Shield = 0f;
            shieldTimer = 0f;
            flashTimer = 0f;
            markTimer = 0f;
            ApplyFlash(0f);
            if (healthBar != null) healthBar.SetValue(1f, 0f);
        }

        public void AddShield(float amount, float duration)
        {
            Shield = Mathf.Max(Shield, amount);
            shieldTimer = duration;
            if (healthBar != null) healthBar.SetValue(Health01, Shield / Mathf.Max(1f, maxHealth));
        }

        /// <summary>Returns damage actually dealt to health.</summary>
        public virtual float TakeDamage(float amount, Unit source)
        {
            if (!IsAlive || amount <= 0f) return 0f;
            if (invulnerable)
            {
                flashTimer = FlashDuration;
                Damaged?.Invoke(this, 0f, source);
                return 0f;
            }

            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
            }

            float dealt = Mathf.Min(Health, amount);
            Health -= dealt;
            flashTimer = FlashDuration;
            if (healthBar != null) healthBar.SetValue(Health01, Shield / Mathf.Max(1f, maxHealth));
            Damaged?.Invoke(this, dealt, source);

            if (Health <= 0f)
            {
                Health = 0f;
                Die(source);
            }
            return dealt;
        }

        protected virtual void Die(Unit killer)
        {
            Died?.Invoke(this, killer);
        }

        // ------------------------------------------------------------------ mark (applied by a marking skillshot)

        /// <summary>Takes hits (flash, events) but never loses health.</summary>
        [System.NonSerialized] public bool invulnerable;

        public bool IsMarked => markTimer > 0f;
        public float MarkRemaining => markTimer;
        private float markTimer;
        private float markDamage;

        public void ApplyMark(float duration, float burstDamage)
        {
            markTimer = duration;
            markDamage = burstDamage;
        }

        /// <summary>Removes the mark and returns its burst damage (0 if not marked).</summary>
        public float ConsumeMark()
        {
            if (markTimer <= 0f) return 0f;
            markTimer = 0f;
            return markDamage;
        }

        protected virtual void Update()
        {
            if (markTimer > 0f) markTimer -= Time.deltaTime;

            if (shieldTimer > 0f)
            {
                shieldTimer -= Time.deltaTime;
                if (shieldTimer <= 0f && Shield > 0f)
                {
                    Shield = 0f;
                    if (healthBar != null) healthBar.SetValue(Health01, 0f);
                }
            }

            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                ApplyFlash(Mathf.Clamp01(flashTimer / FlashDuration));
            }
        }

        private void ApplyFlash(float amount)
        {
            if (flashRenderers == null) return;
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                Renderer r = flashRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(colorProps[i], Color.Lerp(baseColors[i], Color.white, amount * 0.85f));
                r.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>Allocation-free registry of live units for targeting and hit tests.</summary>
    public static class UnitRegistry
    {
        private static readonly List<Unit> units = new List<Unit>(256);

        public static IReadOnlyList<Unit> All => units;

        public static void Register(Unit u)
        {
            if (!units.Contains(u)) units.Add(u);
        }

        public static void Unregister(Unit u)
        {
            units.Remove(u);
        }

        public static bool IsHostile(Team a, Team b) => a != b;

        /// <summary>Nearest living hostile unit whose edge is within <paramref name="maxDistance"/> of <paramref name="point"/>.</summary>
        public static Unit NearestHostile(Team toTeam, Vector3 point, float maxDistance)
        {
            Unit best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive || !IsHostile(toTeam, u.team)) continue;
                Vector3 d = u.Position - point;
                d.y = 0f;
                float reach = maxDistance + u.radius;
                float sq = d.sqrMagnitude;
                if (sq <= reach * reach && sq < bestSq)
                {
                    bestSq = sq;
                    best = u;
                }
            }
            return best;
        }

        /// <summary>
        /// Picks the unit under a screen ray by testing against each unit's vertical capsule
        /// (radius + tolerance). No colliders needed.
        /// </summary>
        public static Unit Pick(Ray ray, float tolerance, Unit ignore = null)
        {
            Unit best = null;
            float bestT = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == ignore || !u.IsAlive) continue;
                Vector3 a = u.Position;
                Vector3 b = a + Vector3.up * u.height;
                float dist = RaySegmentDistance(ray, a, b, out float t);
                if (dist <= u.radius + tolerance && t < bestT)
                {
                    bestT = t;
                    best = u;
                }
            }
            return best;
        }

        private static float RaySegmentDistance(Ray ray, Vector3 a, Vector3 b, out float rayT)
        {
            Vector3 u = ray.direction;
            Vector3 v = b - a;
            Vector3 w = ray.origin - a;
            float uu = Vector3.Dot(u, u), uv = Vector3.Dot(u, v), vv = Vector3.Dot(v, v);
            float uw = Vector3.Dot(u, w), vw = Vector3.Dot(v, w);
            float denom = uu * vv - uv * uv;
            float s, t;
            if (denom < 1e-6f)
            {
                s = 0f;
                t = vv > 0f ? vw / vv : 0f;
            }
            else
            {
                s = (uv * vw - vv * uw) / denom;
                t = (uu * vw - uv * uw) / denom;
            }
            s = Mathf.Max(0f, s);
            t = Mathf.Clamp01(t);
            // Re-project s for the clamped segment point.
            Vector3 segPoint = a + v * t;
            s = Mathf.Max(0f, Vector3.Dot(segPoint - ray.origin, u) / uu);
            rayT = s;
            return Vector3.Distance(ray.origin + u * s, segPoint);
        }
    }
}
