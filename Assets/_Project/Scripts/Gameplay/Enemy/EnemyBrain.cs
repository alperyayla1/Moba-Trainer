using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Simple champion-like AI for the drills: jukes around a zone (random short runs that change
    /// direction like a player dodging), and optionally throws line / circle skillshots at its target
    /// with a stand-still cast time. Driven by a scenario; does nothing while <see cref="Active"/> is false.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class EnemyBrain : MonoBehaviour
    {
        [Header("Movement")]
        public bool move = true;
        public float moveSpeed = 3.25f;
        [Tooltip("Keeps this distance band from the target while juking.")]
        public Vector2 preferredRange = new Vector2(6f, 10f);
        public Vector2 jukeInterval = new Vector2(0.35f, 1.1f);

        [Header("Chase (melee)")]
        [Tooltip("Run straight at the target and hit it on contact instead of juking.")]
        public bool chase;
        public float meleeReach = 0.35f;
        public float meleeCooldown = 1f;
        public float meleeDamage = 1f;

        [Header("Lunge (melee)")]
        [Tooltip("Short dash toward the target when it gets within range (a quick gap-closer).")]
        public bool lunge;
        public float lungeRange = 4.5f;
        public float lungeDistance = 2.6f;
        public float lungeSpeed = 16f;
        public float lungeWindup = 0.22f;
        public float lungeCooldown = 3f;

        public enum ShotStyle { Orb, Fan, Beam }

        [Header("Line skillshot")]
        public ShotStyle shotStyle = ShotStyle.Orb;
        [Tooltip("Fan: number of shards and total spread in degrees.")]
        public int fanCount = 3;
        public float fanSpread = 34f;
        public bool castLines;
        public Vector2 lineInterval = new Vector2(1.6f, 2.6f);
        public float lineRange = 12f;
        public float lineWidth = 1.2f;
        public float lineSpeed = 20f;
        public float lineWindup = 0.25f;
        public float lineDamage = 80f;
        [Range(0f, 1f)] public float leadChance = 0.5f;
        [Tooltip("Draw a ground lane during the windup. Off = a plain skillshot (watch the cast animation).")]
        public bool showLane = false;

        [Header("Circle skillshot")]
        public bool castCircles;
        public Vector2 circleInterval = new Vector2(3f, 5f);
        public float circleRadius = 1.6f;
        public float circleDelay = 0.8f;
        public float circleDamage = 70f;
        [Tooltip("Max cast distance to the circle centre; the caster walks closer if the target is further.")]
        public float circleRange = 9f;
        [Tooltip("Standing cast time before the circle appears (then circleDelay until it erupts).")]
        public float circleCastTime = 0f;
        [Tooltip("0 = red telegraph, 1 = fire pillar.")]
        public byte circleLook = 0;
        [Tooltip("Circle aim: random offset (units) so it doesn't always land dead centre on you. 0 = always on you / your path.")]
        public float circleAimSpread = 0f;

        [Header("Sidestep player skillshots (0 = never, the default)")]
        [Range(0f, 1f)] public float dodgeChance = 0f;
        [Tooltip("Seconds before the sidestep starts (human-like reaction).")]
        public float dodgeReaction = 0.18f;

        public Unit target;
        /// <summary>True while standing still in a cast time (drives the cast animation).</summary>
        public bool IsCasting => castLock > 0f;
        public bool Active { get; set; }

        private Unit self;
        private Vector3 moveDir;
        private float jukeTimer;
        private float lineTimer;
        private float circleTimer;
        private float castLock;
        private bool pendingCircle;
        private Vector3 pendingAt;
        private float meleeTimer;
        private float lungeTimer;
        private float lungeWind;
        private float lungeLeft;
        private Vector3 lungeDir;
        private Vector3 lastTargetPos;
        private float dodgeCheck, dodgeDelay, dodgeBurst;
        private Vector3 dodgeDir;
        // Walking around walls: A* waypoints toward the target when the straight line is blocked.
        private readonly System.Collections.Generic.List<Vector3> path = new System.Collections.Generic.List<Vector3>(16);
        private float repathTimer;
        private Vector3 pathGoal;
        private Vector3 targetVelocity;

        private void Awake()
        {
            self = GetComponent<Unit>();
        }

        /// <summary>Move speed after player slows / stuns (Unit.MoveMultiplier).</summary>
        private float Speed => moveSpeed * self.MoveMultiplier;

        public void ResetBrain()
        {
            lineTimer = Random.Range(0.6f, lineInterval.y);
            circleTimer = Random.Range(circleInterval.x, circleInterval.y);
            jukeTimer = 0f;
            castLock = 0f;
            pendingCircle = false;
            dodgeCheck = dodgeDelay = dodgeBurst = 0f;
            path.Clear();
            repathTimer = 0f;
            meleeTimer = 0f;
            lungeTimer = Random.Range(0.5f, 1.5f);
            lungeWind = 0f;
            lungeLeft = 0f;
            if (target != null) lastTargetPos = target.Position;
        }

        private void Update()
        {
            if (!Active || self == null || !self.IsAlive || target == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 tp = target.Position;
            targetVelocity = Vector3.Lerp(targetVelocity, (tp - lastTargetPos) / dt, 1f - Mathf.Exp(-10f * dt));
            lastTargetPos = tp;

            // Stunned: no moving, attacking or casting; a cast in progress is interrupted.
            if (self.IsStunned)
            {
                castLock = 0f;
                pendingCircle = false;
                lungeLeft = 0f;
                lungeWind = 0f;
                dodgeBurst = 0f;
                return;
            }

            if (dodgeChance > 0f && castLock <= 0f && lungeLeft <= 0f && TickDodge(dt)) return;

            if (castLock > 0f)
            {
                castLock -= dt;          // standing still while casting, like a real cast time
                if (castLock <= 0f && pendingCircle)
                {
                    pendingCircle = false;
                    EnemySkillSystem.Instance?.FireCircle(self, pendingAt, circleRadius, circleDelay, circleDamage, circleLook);
                }
                return;
            }

            if (castLines && target.IsAlive)
            {
                lineTimer -= dt;
                if (lineTimer <= 0f)
                {
                    lineTimer = Random.Range(lineInterval.x, lineInterval.y);
                    CastLine(tp);
                    return;
                }
            }
            if (castCircles && target.IsAlive)
            {
                circleTimer -= dt;
                if (circleTimer <= 0f)
                {
                    Vector3 off = tp - self.Position;
                    off.y = 0f;
                    if (off.magnitude > circleRange + circleRadius)
                    {
                        circleTimer = 0.25f;   // out of range: keep walking, try again shortly
                    }
                    else
                    {
                        circleTimer = Random.Range(circleInterval.x, circleInterval.y);
                        Vector3 at = tp + targetVelocity * ((circleCastTime + circleDelay) * Random.Range(0f, 0.7f));
                        if (circleAimSpread > 0f)
                        {
                            // Vary it: sometimes on your path, sometimes beside you, sometimes between you and the caster.
                            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(circleAimSpread * 0.35f, circleAimSpread);
                            at += new Vector3(r.x, 0f, r.y);
                            if (Random.value < 0.25f) at = Vector3.Lerp(at, self.Position, Random.Range(0.15f, 0.35f));
                        }
                        Vector3 rel = at - self.Position;
                        rel.y = 0f;
                        if (rel.magnitude > circleRange) at = self.Position + rel.normalized * circleRange;
                        if (circleCastTime > 0f)
                        {
                            pendingCircle = true;
                            pendingAt = at;
                            castLock = circleCastTime;
                            return;
                        }
                        EnemySkillSystem.Instance?.FireCircle(self, at, circleRadius, circleDelay, circleDamage, circleLook);
                    }
                }
            }

            if (chase) Chase(tp, dt);
            else if (move) Juke(tp, dt);
        }

        private void CastLine(Vector3 tp)
        {
            Vector3 aim = tp;
            float dist = Vector3.Distance(self.Position, tp);
            if (Random.value < leadChance)
            {
                float flight = lineWindup + dist / Mathf.Max(1f, lineSpeed);
                aim += targetVelocity * flight;   // predict where the target is walking
            }
            Vector3 dir = aim - self.Position;
            dir.y = 0f;
            if (shotStyle == ShotStyle.Beam)
            {
                // Light beam: thin line first (lineWindup s, standing still), then the beam.
                EnemySkillSystem.Instance?.FireBeam(self, dir, lineRange, lineWidth, lineWindup, lineDamage);
            }
            else if (shotStyle == ShotStyle.Fan && fanCount > 1)
            {
                for (int i = 0; i < fanCount; i++)
                {
                    float ang = Mathf.Lerp(-fanSpread * 0.5f, fanSpread * 0.5f, i / (float)(fanCount - 1));
                    EnemySkillSystem.Instance?.FireLine(self, Quaternion.Euler(0f, ang, 0f) * dir, lineRange, lineWidth, lineSpeed, lineWindup, lineDamage, showLane, 1);
                }
            }
            else
            {
                EnemySkillSystem.Instance?.FireLine(self, dir, lineRange, lineWidth, lineSpeed, lineWindup, lineDamage, showLane, 0);
            }
            castLock = lineWindup;
        }

        private void Chase(Vector3 tp, float dt)
        {
            meleeTimer -= dt;
            lungeTimer -= dt;
            Vector3 pos = self.Position;
            Vector3 to = tp - pos;
            to.y = 0f;
            float dist = to.magnitude;
            float contact = self.radius + target.radius + meleeReach;

            // Lunge: brief crouch (wind-up), then a short fast dash along the locked direction.
            if (lungeLeft > 0f)
            {
                if (lungeWind > 0f) { lungeWind -= dt; return; }
                float lstep = Mathf.Min(lungeSpeed * dt, lungeLeft);
                lungeLeft -= lstep;
                Vector3 np = Arena.ClampStatic(pos + lungeDir * lstep, self.radius);
                if ((np - pos).sqrMagnitude < lstep * lstep * 0.2f) lungeLeft = 0f;   // hit a wall
                transform.position = np;
                Vector3 d2 = tp - np; d2.y = 0f;
                if (d2.magnitude <= contact && meleeTimer <= 0f && target.IsAlive)
                {
                    meleeTimer = meleeCooldown;
                    target.TakeDamage(meleeDamage, self);
                    FxSystem.Instance?.Flash(target.Position, target.radius + 0.4f, new Color(1f, 0.55f, 0.2f, 0.9f), 0.25f);
                    lungeLeft = 0f;
                }
                return;
            }
            if (lunge && lungeTimer <= 0f && dist > contact && dist <= lungeRange && Clear(pos, tp))
            {
                lungeTimer = lungeCooldown;
                lungeDir = to / dist;
                lungeLeft = lungeDistance;
                lungeWind = 0f;
                castLock = lungeWindup;   // the wind-up: stands still in the cast pose, then dashes
                return;
            }
            if (dist <= contact)
            {
                if (meleeTimer <= 0f && target.IsAlive)
                {
                    meleeTimer = meleeCooldown;
                    target.TakeDamage(meleeDamage, self);
                    FxSystem.Instance?.Flash(target.Position, target.radius + 0.4f, new Color(1f, 0.3f, 0.35f, 0.9f), 0.25f);
                }
                return;
            }
            Vector3 dir = DirTo(tp, dt);          // straight at the target, or around the wall in between
            Vector3 step = dir * Mathf.Min(Speed * dt, dist - contact * 0.9f);
            transform.position = Arena.ClampStatic(pos + step, self.radius);
        }

        /// <summary>
        /// Sidestep: sees a player skillshot coming, waits a short reaction time, then (with dodgeChance)
        /// steps out of its path for ~0.45 s at normal move speed. Returns true while sidestepping.
        /// </summary>
        private bool TickDodge(float dt)
        {
            if (dodgeBurst > 0f)
            {
                if (dodgeDelay > 0f) { dodgeDelay -= dt; return false; }
                dodgeBurst -= dt;
                Vector3 pos = self.Position;
                transform.position = Arena.ClampStatic(pos + dodgeDir * Speed * dt, self.radius);
                return true;
            }
            dodgeCheck -= dt;
            if (dodgeCheck > 0f) return false;
            dodgeCheck = 0.08f;
            PlayerSkillSystem pss = PlayerSkillSystem.Instance;
            if (pss == null || !pss.IncomingSkillshot(self.Position, self.radius, 0.75f, out Vector3 dir, out Vector3 shot)) return false;
            dodgeCheck = 0.7f;                       // one roll per shot
            if (Random.value >= dodgeChance) return false;
            Vector3 rel = self.Position - shot;
            rel.y = 0f;
            Vector3 perp = Vector3.Cross(Vector3.up, dir);
            float sideSign = Vector3.Dot(rel, perp) >= 0f ? 1f : -1f;   // step away from the line, not across it
            dodgeDir = perp * sideSign;
            dodgeDelay = dodgeReaction;
            dodgeBurst = 0.45f;
            return false;
        }

        private bool Clear(Vector3 a, Vector3 b)
        {
            ObstacleField of = ObstacleField.Instance;
            return of == null || of.LineClear(a, b, self.radius);
        }

        /// <summary>
        /// Direction to walk toward <paramref name="goal"/>: straight when nothing is in the way
        /// (exactly the old behaviour), otherwise along an A* path around the obstacles.
        /// </summary>
        private Vector3 DirTo(Vector3 goal, float dt)
        {
            Vector3 pos = self.Position;
            pos.y = 0f;
            goal.y = 0f;
            ObstacleField of = ObstacleField.Instance;
            if (of == null || of.LineClear(pos, goal, self.radius))
            {
                path.Clear();
                Vector3 d = goal - pos;
                return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
            }
            repathTimer -= dt;
            if (path.Count == 0 || repathTimer <= 0f || (goal - pathGoal).sqrMagnitude > 1f)
            {
                of.FindPath(pos, goal, self.radius, path);
                repathTimer = 0.5f;
                pathGoal = goal;
            }
            while (path.Count > 1 && (path[0] - pos).sqrMagnitude < 0.3f * 0.3f) path.RemoveAt(0);
            Vector3 w = (path.Count > 0 ? path[0] : goal) - pos;
            w.y = 0f;
            return w.sqrMagnitude > 1e-6f ? w.normalized : Vector3.zero;
        }

        private void Juke(Vector3 tp, float dt)
        {
            jukeTimer -= dt;
            Vector3 pos = self.Position;
            Vector3 toTarget = tp - pos;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            // Too far and a wall in between: walk around it instead of juking into it.
            if (dist > preferredRange.y && !Clear(pos, tp))
            {
                Vector3 around = DirTo(tp, dt);
                if (around != Vector3.zero)
                {
                    transform.position = Arena.ClampStatic(pos + around * Speed * dt, self.radius);
                    jukeTimer = 0f;
                    return;
                }
            }
            if (jukeTimer <= 0f)
            {
                jukeTimer = Random.Range(jukeInterval.x, jukeInterval.y);
                Vector3 radial = dist > 0.01f ? toTarget / dist : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, radial) * (Random.value < 0.5f ? -1f : 1f);
                float radialBias = dist > preferredRange.y ? 0.8f : (dist < preferredRange.x ? -0.8f : Random.Range(-0.3f, 0.3f));
                moveDir = (side + radial * radialBias).normalized;
                if (Random.value < 0.12f) moveDir = Vector3.zero; // brief stop, like a fake-out
            }
            if (moveDir == Vector3.zero) return;
            Vector3 next = Arena.ClampStatic(pos + moveDir * Speed * dt, self.radius);
            if ((next - pos).sqrMagnitude < (Speed * dt * 0.3f) * (Speed * dt * 0.3f)) jukeTimer = 0f; // blocked: pick a new way
            transform.position = next;
        }
    }
}
