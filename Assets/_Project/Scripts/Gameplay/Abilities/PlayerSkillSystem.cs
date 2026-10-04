using System;
using System.Collections.Generic;
using MobaTrainer.Core;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Player-owned projectiles: homing auto-attack bolts, linear skillshots and delayed circles.
    /// Plain struct arrays updated in one loop; visuals are pooled; hit tests are analytic.
    /// (Enemy telegraphs live in the separate ProjectileSystem, milestone 2.)
    /// </summary>
    public class PlayerSkillSystem : MonoBehaviour
    {
        public static PlayerSkillSystem Instance { get; private set; }

        [Header("Visuals")]
        public Transform projectilePrefab;
        public GroundMarker markerPrefab;
        public Material transparentMaterial;
        public Material telegraphMaterial;
        public int projectileCapacity = 64;
        public int circleCapacity = 16;

        [Header("Look")]
        public float projectileHeight = 1.35f;
        [ColorUsage(false, true)] public Color boltCore = new Color(2.4f, 2.1f, 1.4f);
        public Color boltTrail = new Color(1f, 0.86f, 0.45f, 0.9f);
        [ColorUsage(false, true)] public Color waveCore = new Color(2.6f, 1.8f, 0.6f);
        public Color waveTrail = new Color(1f, 0.72f, 0.25f, 0.9f);
        [ColorUsage(false, true)] public Color attackCore = new Color(1.8f, 1.7f, 1.4f);
        public Color attackTrail = new Color(1f, 0.95f, 0.8f, 0.6f);
        public Color markColor = new Color(1f, 0.78f, 0.3f, 0.95f);
        public Color hitColor = new Color(1f, 0.85f, 0.45f, 0.9f);

        /// <summary>Raised when a skillshot resolves: (ability, hitUnit or null).</summary>
        public event Action<AbilityDefinition, Unit> SkillshotResolved;
        /// <summary>Raised when a mark is detonated: (target, burstDamage).</summary>
        public event Action<Unit, float> MarkDetonated;
        /// <summary>Raised when an auto-attack connects: (target, damageDealt).</summary>
        public event Action<Unit, float> AttackLanded;

        private enum Kind : byte { Homing, Linear }

        private struct Projectile
        {
            public Kind kind;
            public Transform visual;
            public Vector3 position;
            public Vector3 direction;
            public float speed;
            public float remaining;
            public float radius;
            public float damage;
            public Unit target;
            public Unit owner;
            public AbilityDefinition ability;
        }

        private struct Circle
        {
            public Vector3 center;
            public float radius;
            public float timer;
            public float delay;
            public float damage;
            public Unit owner;
            public AbilityDefinition ability;
            public GroundMarker fill;
            public GroundMarker ring;
        }

        private ComponentPool<Transform> projectilePool;
        private ComponentPool<GroundMarker> markerPool;
        private Projectile[] projectiles;
        private int projectileCount;
        private Circle[] circles;
        private int circleCount;
        private Mesh unitDisc;
        private Mesh unitRing;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Instance = this;
            projectilePool = new ComponentPool<Transform>(projectilePrefab, transform, projectileCapacity);
            markerPool = new ComponentPool<GroundMarker>(markerPrefab, transform, circleCapacity * 2 + 16);
            projectiles = new Projectile[projectileCapacity];
            circles = new Circle[circleCapacity];
            unitDisc = MeshFactory.Disc(1f, 1f, 1f, 0.03f, 64);
            unitRing = MeshFactory.Ring(1f, 0.06f, 0.03f, 64);
            mpb = new MaterialPropertyBlock();
        }

        // ------------------------------------------------------------------ spawning

        /// <summary>Homing bolt: auto-attacks (ability null) or ability bolts that apply on-hit effects.</summary>
        public void FireHoming(Unit owner, Unit target, float speed, float damage, AbilityDefinition ability = null)
        {
            if (target == null || projectileCount >= projectiles.Length) return;
            Vector3 start = owner.Position + Vector3.up * projectileHeight;
            Transform v = projectilePool.Get();
            if (ability != null) Style(v, start, 0.45f, boltCore, boltTrail, 0.45f);
            else Style(v, start, 0.26f, attackCore, attackTrail, 0.22f);
            projectiles[projectileCount++] = new Projectile
            {
                kind = Kind.Homing, visual = v, position = start, speed = speed, damage = damage, target = target, owner = owner, radius = 0.1f,
                ability = ability,
            };
        }

        public void FireLinear(Unit owner, AbilityDefinition ability, Vector3 origin, Vector3 direction)
        {
            if (projectileCount >= projectiles.Length) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
            direction.Normalize();
            Vector3 start = origin + Vector3.up * projectileHeight;
            Transform v = projectilePool.Get();
            bool wave = ability.marksTarget;
            Style(v, start, wave ? 0.85f : 0.5f, wave ? waveCore : boltCore, wave ? waveTrail : boltTrail, wave ? ability.size * 0.75f : ability.size * 0.55f);
            projectiles[projectileCount++] = new Projectile
            {
                kind = Kind.Linear, visual = v, position = start, direction = direction, speed = ability.speed,
                remaining = ability.range, radius = ability.size * 0.5f, damage = ability.damage, owner = owner, ability = ability,
            };
        }

        public void CastCircle(Unit owner, AbilityDefinition ability, Vector3 center, Color color)
        {
            if (circleCount >= circles.Length) return;
            GroundMarker fill = markerPool.Get();
            fill.SetMaterial(telegraphMaterial != null ? telegraphMaterial : transparentMaterial);
            fill.Set(unitDisc, center, Quaternion.identity, new Vector3(ability.size, 1f, ability.size), color.WithAlpha(0.35f), 0.025f);
            fill.SetFill(0f);
            GroundMarker ring = markerPool.Get();
            ring.SetMaterial(transparentMaterial);
            ring.Set(unitRing, center, Quaternion.identity, new Vector3(ability.size, 1f, ability.size), color.WithAlpha(0.9f), 0.03f);
            circles[circleCount++] = new Circle
            {
                center = center, radius = ability.size, delay = Mathf.Max(0.01f, ability.delay), damage = ability.damage, owner = owner,
                ability = ability, fill = fill, ring = ring,
            };
        }

        public void ClearAll()
        {
            projectilePool.ReleaseAll();
            markerPool.ReleaseAll();
            projectileCount = 0;
            circleCount = 0;
            markVisuals.Clear();
        }

        // ------------------------------------------------------------------ simulation

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            UpdateProjectiles(dt);
            UpdateCircles(dt);
            UpdateMarks();
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i = projectileCount - 1; i >= 0; i--)
            {
                ref Projectile p = ref projectiles[i];
                if (p.kind == Kind.Homing)
                {
                    if (p.target == null || !p.target.IsAlive)
                    {
                        RemoveProjectile(i);
                        continue;
                    }
                    Vector3 aim = p.target.Position + Vector3.up * projectileHeight;
                    Vector3 to = aim - p.position;
                    float step = p.speed * dt;
                    if (to.magnitude <= step + p.target.radius * 0.5f)
                    {
                        if (p.ability != null)
                        {
                            ApplyAbilityHit(p.ability, p.target, p.owner);
                        }
                        else
                        {
                            // Auto-attacks also detonate a mark.
                            float damage = p.damage + Detonate(p.target);
                            float dealt = p.target.TakeDamage(damage, p.owner);
                            AttackLanded?.Invoke(p.target, dealt);
                        }
                        RemoveProjectile(i);
                        continue;
                    }
                    p.position += to.normalized * step;
                    p.visual.position = p.position;
                    p.visual.rotation = Quaternion.LookRotation(to.normalized);
                }
                else
                {
                    float step = Mathf.Min(p.speed * dt, p.remaining);
                    Vector3 from = p.position;
                    Vector3 to = from + p.direction * step;
                    Unit hit = FirstHit(from, to, p.radius, p.owner);
                    if (hit != null)
                    {
                        ApplyAbilityHit(p.ability, hit, p.owner);
                        SkillshotResolved?.Invoke(p.ability, hit);
                        RemoveProjectile(i);
                        continue;
                    }
                    p.position = to;
                    p.remaining -= step;
                    p.visual.position = to;
                    p.visual.rotation = Quaternion.LookRotation(p.direction);
                    if (p.remaining <= 0.0001f || (Arena.Instance != null && !Arena.Instance.Contains(to, -2f)))
                    {
                        SkillshotResolved?.Invoke(p.ability, null);
                        RemoveProjectile(i);
                    }
                }
            }
        }

        /// <summary>
        /// Damage + on-hit rules: a marking shot only marks; detonating shots add the mark's burst;
        /// cooldown refund on hit reduces every ability cooldown except Blink.
        /// </summary>
        private void ApplyAbilityHit(AbilityDefinition ability, Unit hit, Unit owner)
        {
            FxSystem fx = FxSystem.Instance;
            if (ability.marksTarget)
            {
                hit.ApplyMark(ability.markDuration, ability.damage);
                if (fx != null) fx.Flash(hit.Position, hit.radius + 0.5f, markColor, 0.35f);
            }
            else
            {
                float damage = ability.damage;
                if (ability.detonatesMark) damage += Detonate(hit);
                hit.TakeDamage(damage, owner);
                if (fx != null) fx.Flash(hit.Position, hit.radius + 0.4f, hitColor, 0.3f);
            }

            if (ability.cooldownRefundOnHit > 0f && owner != null)
            {
                PlayerAbilities abilities = owner.GetComponent<PlayerAbilities>();
                if (abilities != null) abilities.ReduceCooldowns(ability.cooldownRefundOnHit);
            }
        }

        /// <summary>Consumes the target's mark (if any) and returns its burst damage.</summary>
        private float Detonate(Unit target)
        {
            float burst = target.ConsumeMark();
            if (burst <= 0f) return 0f;
            FxSystem.Instance?.Burst(target.Position, target.radius + 1.1f, markColor, 0.4f);
            MarkDetonated?.Invoke(target, burst);
            return burst;
        }

        // ------------------------------------------------------------------ visuals

        private void Style(Transform v, Vector3 position, float coreScale, Color core, Color trailColor, float trailWidth)
        {
            v.position = position;
            v.localScale = new Vector3(coreScale, coreScale, coreScale * 1.7f); // stretched along flight
            var r = v.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, core);
                r.SetPropertyBlock(mpb);
            }
            var trail = v.GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
                trail.widthMultiplier = trailWidth / Mathf.Max(0.01f, coreScale);
                trail.startColor = trailColor;
                trail.endColor = trailColor * new Color(1f, 1f, 1f, 0f);
                trail.emitting = true;
            }
        }

        private readonly Dictionary<Unit, GroundMarker> markVisuals = new Dictionary<Unit, GroundMarker>();
        private readonly List<Unit> markScratch = new List<Unit>();

        /// <summary>Golden pulsing ring under every marked enemy.</summary>
        private void UpdateMarks()
        {
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                bool marked = u.IsAlive && u.IsMarked;
                markVisuals.TryGetValue(u, out GroundMarker m);
                if (marked)
                {
                    if (m == null)
                    {
                        m = markerPool.Get();
                        m.SetMaterial(transparentMaterial);
                        markVisuals[u] = m;
                    }
                    float pulse = 1f + Mathf.Sin(Time.time * 9f) * 0.06f;
                    float r = (u.radius + 0.3f) * pulse;
                    float fade = Mathf.Clamp01(u.MarkRemaining / 0.6f);
                    m.Set(unitRing, u.Position, Quaternion.identity, new Vector3(r, 1f, r), markColor.WithAlpha(markColor.a * fade), 0.04f);
                }
                else if (m != null)
                {
                    ReleaseMark(m);
                    markVisuals.Remove(u);
                }
            }
            // Units that were removed from the registry.
            markScratch.Clear();
            foreach (var kv in markVisuals) if (kv.Key == null || !kv.Key.isActiveAndEnabled) markScratch.Add(kv.Key);
            foreach (Unit u in markScratch)
            {
                ReleaseMark(markVisuals[u]);
                markVisuals.Remove(u);
            }
        }

        private void ReleaseMark(GroundMarker m) => markerPool.Release(m);

        private void UpdateCircles(float dt)
        {
            for (int i = circleCount - 1; i >= 0; i--)
            {
                ref Circle c = ref circles[i];
                c.timer += dt;
                float t = Mathf.Clamp01(c.timer / c.delay);
                c.fill.SetFill(t);
                if (c.timer < c.delay) continue;

                Unit hit = null;
                var units = UnitRegistry.All;
                for (int u = 0; u < units.Count; u++)
                {
                    Unit unit = units[u];
                    if (!unit.IsAlive || !UnitRegistry.IsHostile(c.owner.team, unit.team)) continue;
                    Vector3 d = unit.Position - c.center;
                    d.y = 0f;
                    float reach = c.radius + unit.radius;
                    if (d.sqrMagnitude <= reach * reach)
                    {
                        unit.TakeDamage(c.damage, c.owner);
                        if (hit == null) hit = unit;
                    }
                }
                if (FxSystem.Instance != null) FxSystem.Instance.Burst(c.center, c.radius, hitColor, 0.25f);
                SkillshotResolved?.Invoke(c.ability, hit);
                RemoveCircle(i);
            }
        }

        private Unit FirstHit(Vector3 from, Vector3 to, float radius, Unit owner)
        {
            Unit best = null;
            float bestT = float.MaxValue;
            Vector3 seg = to - from;
            seg.y = 0f;
            float segLenSq = seg.sqrMagnitude;
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == owner || !u.IsAlive || !UnitRegistry.IsHostile(owner.team, u.team)) continue;
                Vector3 rel = u.Position - from;
                rel.y = 0f;
                float t = segLenSq > 0f ? Mathf.Clamp01(Vector3.Dot(rel, seg) / segLenSq) : 0f;
                Vector3 closest = seg * t;
                float reach = radius + u.radius;
                if ((rel - closest).sqrMagnitude <= reach * reach && t < bestT)
                {
                    bestT = t;
                    best = u;
                }
            }
            return best;
        }

        private void RemoveProjectile(int i)
        {
            projectilePool.Release(projectiles[i].visual);
            projectiles[i] = projectiles[projectileCount - 1];
            projectiles[projectileCount - 1] = default;
            projectileCount--;
        }

        private void RemoveCircle(int i)
        {
            markerPool.Release(circles[i].fill);
            markerPool.Release(circles[i].ring);
            circles[i] = circles[circleCount - 1];
            circles[circleCount - 1] = default;
            circleCount--;
        }
    }
}
