using System;
using MobaTrainer.Core;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Hostile skillshots for the drills: telegraphed line shots (a ground lane fills during the
    /// wind-up, then a glowing bolt flies down it) and delayed circles. Pooled visuals, analytic hits.
    /// </summary>
    public class EnemySkillSystem : MonoBehaviour
    {
        public static EnemySkillSystem Instance { get; private set; }

        [Header("Visuals")]
        public Transform projectilePrefab;
        public GroundMarker markerPrefab;
        public Material transparentMaterial;
        public Material telegraphMaterial;
        public int capacity = 48;

        [Header("Look")]
        public float projectileHeight = 1.2f;
        [ColorUsage(false, true)] public Color boltCore = new Color(2.6f, 0.55f, 0.9f);
        public Color boltTrail = new Color(1f, 0.25f, 0.45f, 0.9f);
        public Color telegraphColor = new Color(1f, 0.25f, 0.3f, 0.32f);
        public Color hitColor = new Color(1f, 0.3f, 0.35f, 0.9f);

        /// <summary>(hitUnit or null). Null = the shot expired without hitting anyone (dodged).</summary>
        public event Action<Unit> ShotResolved;

        private struct Shot
        {
            public bool active;
            public bool isCircle;
            public Unit owner;
            public float windup;        // time left before the bolt launches / circle lands
            public float windupTotal;
            public Vector3 origin;
            public Vector3 direction;
            public Vector3 position;
            public float range;
            public float travelled;
            public float speed;
            public float radius;
            public float damage;
            public GroundMarker telegraph;
            public GroundMarker ring;
            public Transform visual;
        }

        private Shot[] shots;
        private ComponentPool<Transform> projectilePool;
        private ComponentPool<GroundMarker> markerPool;
        private MaterialPropertyBlock mpb;
        private Mesh unitDisc, unitRing;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Instance = this;
            shots = new Shot[capacity];
            projectilePool = new ComponentPool<Transform>(projectilePrefab, transform, capacity / 2);
            markerPool = new ComponentPool<GroundMarker>(markerPrefab, transform, capacity);
            mpb = new MaterialPropertyBlock();
            unitDisc = MeshFactory.Disc(1f, 1f, 1f, 0.03f, 64);
            unitRing = MeshFactory.Ring(1f, 0.06f, 0.03f, 64);
        }

        // ------------------------------------------------------------------ API

        /// <summary>Line skillshot: telegraph for <paramref name="windup"/> s, then a bolt.</summary>
        public void FireLine(Unit owner, Vector3 direction, float range, float width, float speed, float windup, float damage)
        {
            int i = FreeSlot();
            if (i < 0) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) direction = owner.transform.forward;
            direction.Normalize();
            Vector3 origin = owner.Position;

            GroundMarker tele = markerPool.Get();
            tele.SetMaterial(telegraphMaterial != null ? telegraphMaterial : transparentMaterial);
            tele.Set(MeshFactory.Rect(range, width, 0.05f), origin, Quaternion.LookRotation(direction), Vector3.one, telegraphColor, 0.03f);
            tele.SetFill(0f);

            shots[i] = new Shot
            {
                active = true, owner = owner, windup = Mathf.Max(0.01f, windup), windupTotal = Mathf.Max(0.01f, windup),
                origin = origin, direction = direction, position = origin, range = range, speed = speed,
                radius = width * 0.5f, damage = damage, telegraph = tele,
            };
        }

        /// <summary>Circle: fills for <paramref name="delay"/> s, then hits everything inside.</summary>
        public void FireCircle(Unit owner, Vector3 center, float radius, float delay, float damage)
        {
            int i = FreeSlot();
            if (i < 0) return;
            center = Arena.ClampStatic(center);
            GroundMarker fill = markerPool.Get();
            fill.SetMaterial(telegraphMaterial != null ? telegraphMaterial : transparentMaterial);
            fill.Set(unitDisc, center, Quaternion.identity, new Vector3(radius, 1f, radius), telegraphColor, 0.025f);
            fill.SetFill(0f);
            GroundMarker ring = markerPool.Get();
            ring.SetMaterial(transparentMaterial);
            ring.Set(unitRing, center, Quaternion.identity, new Vector3(radius, 1f, radius), telegraphColor.WithAlpha(0.9f), 0.03f);

            shots[i] = new Shot
            {
                active = true, isCircle = true, owner = owner, windup = Mathf.Max(0.05f, delay), windupTotal = Mathf.Max(0.05f, delay),
                position = center, radius = radius, damage = damage, telegraph = fill, ring = ring,
            };
        }

        public void ClearAll()
        {
            for (int i = 0; i < shots.Length; i++) shots[i] = default;
            projectilePool.ReleaseAll();
            markerPool.ReleaseAll();
        }

        // ------------------------------------------------------------------ simulation

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < shots.Length; i++)
            {
                if (!shots[i].active) continue;
                ref Shot s = ref shots[i];

                if (s.windup > 0f)
                {
                    s.windup -= dt;
                    float t = 1f - Mathf.Clamp01(s.windup / s.windupTotal);
                    if (s.telegraph != null) s.telegraph.SetFill(t);
                    if (s.windup > 0f) continue;

                    if (s.isCircle)
                    {
                        ResolveCircle(ref s);
                        Free(i);
                        continue;
                    }

                    // Launch the bolt down the locked lane.
                    s.position = s.origin;
                    s.visual = projectilePool.Get();
                    Style(s.visual, s.origin + Vector3.up * projectileHeight, s.radius * 1.3f);
                }

                if (s.isCircle) continue;

                float step = Mathf.Min(s.speed * dt, s.range - s.travelled);
                Vector3 from = s.position;
                Vector3 to = from + s.direction * step;
                Unit hit = FirstHit(from, to, s.radius, s.owner);
                if (hit != null)
                {
                    hit.TakeDamage(s.damage, s.owner);
                    FxSystem.Instance?.Flash(hit.Position, hit.radius + 0.4f, hitColor, 0.3f);
                    ShotResolved?.Invoke(hit);
                    Free(i);
                    continue;
                }
                s.position = to;
                s.travelled += step;
                if (s.visual != null)
                {
                    s.visual.position = to + Vector3.up * projectileHeight;
                    s.visual.rotation = Quaternion.LookRotation(s.direction);
                }
                if (s.telegraph != null)
                {
                    // The lane fades out behind the bolt.
                    float remain = 1f - s.travelled / Mathf.Max(0.01f, s.range);
                    s.telegraph.SetColor(telegraphColor.WithAlpha(telegraphColor.a * remain));
                }
                if (s.travelled >= s.range - 1e-4f)
                {
                    ShotResolved?.Invoke(null);
                    Free(i);
                }
            }
        }

        private void ResolveCircle(ref Shot s)
        {
            Unit first = null;
            var units = UnitRegistry.All;
            for (int u = 0; u < units.Count; u++)
            {
                Unit unit = units[u];
                if (!unit.IsAlive || s.owner == null || !UnitRegistry.IsHostile(s.owner.team, unit.team)) continue;
                Vector3 d = unit.Position - s.position;
                d.y = 0f;
                float reach = s.radius + unit.radius;
                if (d.sqrMagnitude > reach * reach) continue;
                unit.TakeDamage(s.damage, s.owner);
                if (first == null) first = unit;
            }
            FxSystem.Instance?.Burst(s.position, s.radius, hitColor.WithAlpha(0.6f), 0.25f);
            ShotResolved?.Invoke(first);
        }

        private Unit FirstHit(Vector3 from, Vector3 to, float radius, Unit owner)
        {
            if (owner == null) return null;
            Unit best = null;
            float bestT = float.MaxValue;
            Vector3 seg = to - from;
            seg.y = 0f;
            float segLenSq = seg.sqrMagnitude;
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive || !UnitRegistry.IsHostile(owner.team, u.team)) continue;
                Vector3 rel = u.Position - from;
                rel.y = 0f;
                float t = segLenSq > 0f ? Mathf.Clamp01(Vector3.Dot(rel, seg) / segLenSq) : 0f;
                float reach = radius + u.radius;
                if ((rel - seg * t).sqrMagnitude <= reach * reach && t < bestT)
                {
                    bestT = t;
                    best = u;
                }
            }
            return best;
        }

        private void Style(Transform v, Vector3 position, float scale)
        {
            v.position = position;
            v.localScale = new Vector3(scale, scale, scale * 1.7f);
            var r = v.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, boltCore);
                r.SetPropertyBlock(mpb);
            }
            var trail = v.GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
                trail.widthMultiplier = 0.9f;
                trail.startColor = boltTrail;
                trail.endColor = boltTrail * new Color(1f, 1f, 1f, 0f);
                trail.emitting = true;
            }
        }

        private int FreeSlot()
        {
            for (int i = 0; i < shots.Length; i++) if (!shots[i].active) return i;
            return -1;
        }

        private void Free(int i)
        {
            ref Shot s = ref shots[i];
            if (s.visual != null) projectilePool.Release(s.visual);
            if (s.telegraph != null) markerPool.Release(s.telegraph);
            if (s.ring != null) markerPool.Release(s.ring);
            s = default;
        }
    }
}
