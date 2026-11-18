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
        [ColorUsage(false, true)] public Color boltCore = new Color(1.9f, 0.6f, 2.6f);
        public Color boltTrail = new Color(0.75f, 0.35f, 1f, 0.8f);
        [ColorUsage(false, true)] public Color shardCore = new Color(0.5f, 1.8f, 2.6f);
        public Color shardTrail = new Color(0.35f, 0.8f, 1f, 0.8f);
        public Color telegraphColor = new Color(1f, 0.25f, 0.3f, 0.32f);
        public Color hitColor = new Color(1f, 0.3f, 0.35f, 0.9f);
        [Tooltip("Light beam: the thin line that shows where it will fire, then the beam itself.")]
        public Color beamCharge = new Color(1f, 0.95f, 0.75f, 0.55f);
        public Color beamColor = new Color(1f, 0.93f, 0.6f, 0.9f);
        public Color fireTelegraph = new Color(1f, 0.42f, 0.08f, 0.34f);
        [ColorUsage(false, true)] public Color fireCore = new Color(3.2f, 1.2f, 0.25f);
        public Color fireHit = new Color(1f, 0.55f, 0.12f, 0.95f);

        /// <summary>(hitUnit or null). Null = the shot expired without hitting anyone (dodged).</summary>
        public event Action<Unit> ShotResolved;
        /// <summary>Sound cues: ("enemy_cast" | "beam_charge" | "beam_fire" | "pillar", position).</summary>
        public static event Action<string, Vector3> Sound;

        private struct Shot
        {
            public bool active;
            public bool isCircle;
            public bool isBeam;
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
            public byte look;           // line: 0 = magenta orb, 1 = cyan shard · circle: 0 = red, 1 = fire pillar
        }

        private struct Pillar
        {
            public Transform visual;
            public float age, life, radius;
        }
        private readonly System.Collections.Generic.List<Pillar> pillars = new System.Collections.Generic.List<Pillar>();

        private Shot[] shots;

        private struct Fade { public GroundMarker marker; public float life, total; public Color color; }
        private readonly System.Collections.Generic.List<Fade> fades = new System.Collections.Generic.List<Fade>();
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
        /// <param name="showLane">Draw the filling ground lane during the windup (off = plain skillshot, read the cast animation).</param>
        public void FireLine(Unit owner, Vector3 direction, float range, float width, float speed, float windup, float damage, bool showLane = true, byte look = 0)
        {
            int i = FreeSlot();
            if (i < 0) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) direction = owner.transform.forward;
            direction.Normalize();
            Vector3 origin = owner.Position;

            GroundMarker tele = null;
            if (showLane)
            {
                tele = markerPool.Get();
                tele.SetMaterial(telegraphMaterial != null ? telegraphMaterial : transparentMaterial);
                tele.Set(MeshFactory.Rect(range, width, 0.05f), origin, Quaternion.LookRotation(direction), Vector3.one, telegraphColor, 0.03f);
                tele.SetFill(0f);
            }

            shots[i] = new Shot
            {
                active = true, owner = owner, windup = Mathf.Max(0.01f, windup), windupTotal = Mathf.Max(0.01f, windup),
                origin = origin, direction = direction, position = origin, range = range, speed = speed,
                radius = width * 0.5f, damage = damage, telegraph = tele, look = look,
            };
        }

        /// <summary>Circle: fills for <paramref name="delay"/> s, then hits everything inside.</summary>
        public void FireCircle(Unit owner, Vector3 center, float radius, float delay, float damage, byte look = 0)
        {
            Color tc = look == 1 ? fireTelegraph : telegraphColor;
            int i = FreeSlot();
            if (i < 0) return;
            center = Arena.ClampStatic(center);
            GroundMarker fill = markerPool.Get();
            fill.SetMaterial(telegraphMaterial != null ? telegraphMaterial : transparentMaterial);
            fill.Set(unitDisc, center, Quaternion.identity, new Vector3(radius, 1f, radius), tc, 0.025f);
            fill.SetFill(0f);
            GroundMarker ring = markerPool.Get();
            ring.SetMaterial(transparentMaterial);
            ring.Set(unitRing, center, Quaternion.identity, new Vector3(radius, 1f, radius), tc.WithAlpha(0.9f), 0.03f);

            shots[i] = new Shot
            {
                active = true, isCircle = true, owner = owner, windup = Mathf.Max(0.05f, delay), windupTotal = Mathf.Max(0.05f, delay),
                position = center, radius = radius, damage = damage, telegraph = fill, ring = ring, look = look,
            };
        }

        /// <summary>
        /// Light beam: a thin line shows the path for <paramref name="windup"/> s (the caster stands still),
        /// then a wide beam hits everything on the line at once.
        /// </summary>
        public void FireBeam(Unit owner, Vector3 direction, float range, float width, float windup, float damage)
        {
            int i = FreeSlot();
            if (i < 0) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) direction = owner.transform.forward;
            direction.Normalize();
            Vector3 origin = owner.Position;
            GroundMarker tele = markerPool.Get();
            tele.SetMaterial(telegraphMaterial != null ? telegraphMaterial : transparentMaterial);
            tele.Set(MeshFactory.Rect(range, 0.16f, 0.04f), origin, Quaternion.LookRotation(direction), Vector3.one, beamCharge, 0.035f);
            tele.SetFill(0f);
            Sound?.Invoke("beam_charge", origin);
            shots[i] = new Shot
            {
                active = true, isBeam = true, owner = owner, windup = Mathf.Max(0.05f, windup), windupTotal = Mathf.Max(0.05f, windup),
                origin = origin, direction = direction, position = origin, range = range, radius = width * 0.5f, damage = damage, telegraph = tele,
            };
        }

        public void ClearAll()
        {
            foreach (Fade f in fades) if (f.marker != null) markerPool.Release(f.marker);
            fades.Clear();
            for (int i = 0; i < shots.Length; i++) shots[i] = default;
            pillars.Clear();
            projectilePool.ReleaseAll();
            markerPool.ReleaseAll();
        }

        // ------------------------------------------------------------------ simulation

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || shots == null) return;   // not initialised (e.g. scripts reloaded during Play)
            TickPillars(dt);
            TickFades(dt);
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
                    if (s.isBeam)
                    {
                        ResolveBeam(ref s);
                        Free(i);
                        continue;
                    }

                    // Launch the bolt down the locked lane.
                    Sound?.Invoke("enemy_cast", s.origin);
                    s.position = s.origin;
                    s.visual = projectilePool.Get();
                    Style(s.visual, s.origin + Vector3.up * projectileHeight, s.radius * (s.look == 1 ? 1.1f : 1.4f), s.look);
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
            if (s.look == 1)
            {
                Sound?.Invoke("pillar", s.position);
                FxSystem.Instance?.Burst(s.position, s.radius, fireHit.WithAlpha(0.75f), 0.35f);
                FxSystem.Instance?.Flash(s.position, s.radius * 1.1f, fireHit, 0.3f);
                SpawnPillar(s.position, s.radius);
            }
            else
            {
                FxSystem.Instance?.Burst(s.position, s.radius, hitColor.WithAlpha(0.6f), 0.25f);
            }
            ShotResolved?.Invoke(first);
        }

        private void ResolveBeam(ref Shot s)
        {
            Unit first = null;
            var units = UnitRegistry.All;
            for (int u = 0; u < units.Count; u++)
            {
                Unit unit = units[u];
                if (!unit.IsAlive || s.owner == null || !UnitRegistry.IsHostile(s.owner.team, unit.team)) continue;
                Vector3 rel = unit.Position - s.origin;
                rel.y = 0f;
                float along = Vector3.Dot(rel, s.direction);
                if (along < -unit.radius || along > s.range + unit.radius) continue;
                float side = (rel - s.direction * along).magnitude;
                if (side > s.radius + unit.radius) continue;
                unit.TakeDamage(s.damage, s.owner);
                FxSystem.Instance?.Flash(unit.Position, unit.radius + 0.5f, beamColor, 0.3f);
                if (first == null) first = unit;
            }
            // The beam itself: a bright strip that fades out quickly.
            GroundMarker beam = markerPool.Get();
            beam.SetMaterial(transparentMaterial);
            beam.Set(MeshFactory.Rect(s.range, s.radius * 2f, 0.12f), s.origin, Quaternion.LookRotation(s.direction), Vector3.one, beamColor, 0.05f);
            fades.Add(new Fade { marker = beam, life = 0.4f, total = 0.4f, color = beamColor });
            FxSystem.Instance?.Flash(s.origin, 1.2f, beamColor, 0.3f);
            Sound?.Invoke("beam_fire", s.origin);
            ShotResolved?.Invoke(first);
        }

        private void TickFades(float dt)
        {
            for (int i = fades.Count - 1; i >= 0; i--)
            {
                Fade f = fades[i];
                f.life -= dt;
                if (f.life <= 0f || f.marker == null)
                {
                    if (f.marker != null) markerPool.Release(f.marker);
                    fades.RemoveAt(i);
                    continue;
                }
                f.marker.SetColor(f.color.WithAlpha(f.color.a * (f.life / f.total)));
                fades[i] = f;
            }
        }

        /// <summary>Short-lived column of fire where a pillar erupts.</summary>
        private void SpawnPillar(Vector3 center, float radius)
        {
            Transform v = projectilePool.Get();
            if (v == null) return;
            v.position = center + Vector3.up * 1.4f;
            v.rotation = Quaternion.identity;
            var r = v.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, fireCore);
                r.SetPropertyBlock(mpb);
            }
            var trail = v.GetComponentInChildren<TrailRenderer>();
            if (trail != null) { trail.Clear(); trail.emitting = false; }
            pillars.Add(new Pillar { visual = v, life = 0.35f, radius = radius });
            TickPillarVisual(pillars[pillars.Count - 1]);
        }

        private void TickPillars(float dt)
        {
            for (int i = pillars.Count - 1; i >= 0; i--)
            {
                Pillar p = pillars[i];
                p.age += dt;
                if (p.age >= p.life || p.visual == null)
                {
                    if (p.visual != null) projectilePool.Release(p.visual);
                    pillars.RemoveAt(i);
                    continue;
                }
                pillars[i] = p;
                TickPillarVisual(p);
            }
        }

        private static void TickPillarVisual(Pillar p)
        {
            float k = Mathf.Clamp01(p.age / p.life);
            float w = p.radius * 1.3f * (1f - k * 0.8f);       // thins out as it fades
            p.visual.localScale = new Vector3(w, 2.8f * (0.6f + 0.4f * Mathf.Sin(Mathf.Min(1f, k * 3f) * Mathf.PI * 0.5f)), w);
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

        private void Style(Transform v, Vector3 position, float scale, byte look)
        {
            v.position = position;
            v.localScale = new Vector3(scale, scale, scale * (look == 1 ? 2.6f : 1.3f));
            var r = v.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, look == 1 ? shardCore : boltCore);
                r.SetPropertyBlock(mpb);
            }
            var trail = v.GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
                trail.time = 0.07f;              // short tail: reads as a ball, not a beam
                trail.widthMultiplier = 0.5f;
                Color tc = look == 1 ? shardTrail : boltTrail;
                trail.startColor = tc;
                trail.endColor = tc * new Color(1f, 1f, 1f, 0f);
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
