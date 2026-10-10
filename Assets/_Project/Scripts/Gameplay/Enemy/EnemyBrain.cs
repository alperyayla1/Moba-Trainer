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

        public enum ShotStyle { Orb, Fan }

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
        private Vector3 targetVelocity;

        private void Awake()
        {
            self = GetComponent<Unit>();
        }

        public void ResetBrain()
        {
            lineTimer = Random.Range(0.6f, lineInterval.y);
            circleTimer = Random.Range(circleInterval.x, circleInterval.y);
            jukeTimer = 0f;
            castLock = 0f;
            pendingCircle = false;
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
            if (shotStyle == ShotStyle.Fan && fanCount > 1)
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
            if (lunge && lungeTimer <= 0f && dist > contact && dist <= lungeRange)
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
            Vector3 step = to / dist * Mathf.Min(moveSpeed * dt, dist - contact * 0.9f);
            transform.position = Arena.ClampStatic(pos + step, self.radius);
        }

        private void Juke(Vector3 tp, float dt)
        {
            jukeTimer -= dt;
            Vector3 pos = self.Position;
            Vector3 toTarget = tp - pos;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
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
            Vector3 next = Arena.ClampStatic(pos + moveDir * moveSpeed * dt, self.radius);
            if ((next - pos).sqrMagnitude < (moveSpeed * dt * 0.3f) * (moveSpeed * dt * 0.3f)) jukeTimer = 0f; // blocked: pick a new way
            transform.position = next;
        }
    }
}
