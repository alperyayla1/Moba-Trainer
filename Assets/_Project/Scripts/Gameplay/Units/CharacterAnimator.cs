using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Procedural animation for the low-poly hero rig: run cycle driven by real ground speed,
    /// idle breathing, ranged attack (arm raise during windup + recoil), ability cast thrust,
    /// blink squash and hit flinch. No Animator/clips needed; every pose is a few joint angles.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("Rig")]
        public Transform rigRoot;
        public Transform hips, spine, neck;
        public Transform legL, kneeL, legR, kneeR;
        public Transform armL, elbowL, armR, elbowR;

        [Header("Staff (Arcanist)")]
        [Tooltip("Optional staff pivot in the right hand. When set, the right arm holds the staff and the staff poses (channel / cast) are used.")]
        public Transform staffGrip;

        [Header("Behaviour")]
        [Tooltip("Rotate the model toward its movement / the nearest enemy (for units without a motor).")]
        public bool autoFace;
        public float strideLength = 2.1f;
        public float runSpeedForFullCycle = 2.2f;

        private Unit unit;
        private PlayerAttack attack;
        private PlayerAbilities abilities;
        private EnemyBrain brain;
        private bool brainChecked;

        private Vector3 lastPos;
        private float speed;
        private float phase;
        private float runWeight;
        private float attackWeight;
        private float recoil;
        private float castWeight;
        private float channelWeight;
        private float squash;
        private float flinch;
        private bool wasWindup;

        private void Awake()
        {
            unit = GetComponentInParent<Unit>();
            attack = GetComponentInParent<PlayerAttack>();
            abilities = GetComponentInParent<PlayerAbilities>();
            brain = GetComponentInParent<EnemyBrain>();
        }

        private void OnEnable()
        {
            lastPos = transform.position;
            if (unit != null) unit.Damaged += OnDamaged;
            if (abilities != null) abilities.AbilityCast += OnAbilityCast;
        }

        private void OnDisable()
        {
            if (unit != null) unit.Damaged -= OnDamaged;
            if (abilities != null) abilities.AbilityCast -= OnAbilityCast;
        }

        private void OnDamaged(Unit u, float amount, Unit source) => flinch = 1f;

        private void OnAbilityCast(int slot, AbilityDefinition ability, Vector3 point)
        {
            if (ability.castTime <= 0f) squash = 1f; // blink pops
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Ground speed from actual movement (works for motor, strafing dummies and dashes).
            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPos;
            delta.y = 0f;
            lastPos = pos;
            float jump = delta.magnitude;
            if (jump > 1.2f) { squash = 1f; jump = 0f; }      // teleport: blink pop, no run burst
            float instSpeed = jump / dt;
            speed = Mathf.Lerp(speed, instSpeed, 1f - Mathf.Exp(-20f * dt));

            if (autoFace) Face(delta, dt);

            runWeight = Mathf.MoveTowards(runWeight, Mathf.Clamp01(speed / runSpeedForFullCycle), dt * 8f);
            phase += Mathf.PI * 2f * speed / strideLength * dt;
            if (phase > Mathf.PI * 200f) phase -= Mathf.PI * 200f;

            bool windup = attack != null && attack.CurrentState == PlayerAttack.State.Windup;
            if (wasWindup && !windup) recoil = 1f;
            wasWindup = windup;
            float attackTarget = windup ? Mathf.Lerp(0.6f, 1f, attack.WindupProgress) : 0f;
            attackWeight = Mathf.MoveTowards(attackWeight, Mathf.Max(attackTarget, recoil), dt * 9f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 5.5f);

            if (!brainChecked) { brain = GetComponentInParent<EnemyBrain>(); brainChecked = true; }   // added at runtime by drills
            bool charging = abilities != null && abilities.IsCharging;
            // Staff holders get their own channel pose while charging; everyone else uses the cast thrust.
            bool casting = (abilities != null && (abilities.IsCasting || abilities.IsLocked || (charging && staffGrip == null))) || (brain != null && brain.IsCasting);
            castWeight = Mathf.MoveTowards(castWeight, casting ? 1f : 0f, dt * (casting ? 14f : 5f));
            channelWeight = Mathf.MoveTowards(channelWeight, charging && staffGrip != null ? 1f : 0f, dt * (charging ? 10f : 6f));
            squash = Mathf.MoveTowards(squash, 0f, dt * 6f);
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 6f);

            Pose(Time.time);
        }

        private void Face(Vector3 delta, float dt)
        {
            Vector3 look = Vector3.zero;
            if (delta.sqrMagnitude > 1e-6f) look = delta;
            else if (unit != null)
            {
                Unit enemy = UnitRegistry.NearestHostile(unit.team, unit.Position, 30f);
                if (enemy != null) look = enemy.Position - unit.Position;
            }
            look.y = 0f;
            if (look.sqrMagnitude < 1e-6f) return;
            Quaternion target = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 720f * dt);
        }

        private void Pose(float t)
        {
            float w = runWeight;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float idle = 1f - w;
            float breathe = Mathf.Sin(t * 2.2f);

            // Legs: forward swing = negative X. Knees bend during the swing phase.
            Set(legL, -s * 36f * w, 0f, 0f);
            Set(legR, s * 36f * w, 0f, 0f);
            Set(kneeL, (Mathf.Max(0f, c) * 58f + 6f) * w, 0f, 0f);
            Set(kneeR, (Mathf.Max(0f, -c) * 58f + 6f) * w, 0f, 0f);

            // Torso: lean into the run, counter-twist, breathe when idle, flinch back on hits.
            float lean = 9f * w + breathe * 1.2f * idle - flinch * 12f + castWeight * 6f;
            float twist = s * 9f * w - attackWeight * 22f - castWeight * 14f;
            Set(spine, lean, twist, 0f);
            Set(neck, -lean * 0.6f + flinch * 6f, -twist * 0.7f, 0f);

            // Arms swing opposite to legs; elbows bent while running.
            float armSwing = 32f * w;
            float lx = s * armSwing, rx = -s * armSwing;
            float lz = -7f - breathe * 1.5f * idle, rz = 7f + breathe * 1.5f * idle;
            float le = -(14f + 40f * w), re = -(14f + 40f * w);

            // Right arm: ranged attack points forward; cast thrusts both arms.
            rx = Mathf.Lerp(rx, -88f, Mathf.Max(attackWeight, castWeight));
            rz = Mathf.Lerp(rz, 4f, attackWeight);
            re = Mathf.Lerp(re, -6f, Mathf.Max(attackWeight, castWeight));
            lx = Mathf.Lerp(lx, -45f, castWeight);
            le = Mathf.Lerp(le, -55f, castWeight);

            Set(armL, lx, 0f, lz);
            Set(armR, rx, 0f, rz);
            Set(elbowL, le, 0f, 0f);
            Set(elbowR, re, 0f, 0f);

            if (staffGrip != null) StaffPose(w);

            // Root: bob twice per stride, squash on blink.
            if (rigRoot != null)
            {
                float bob = Mathf.Abs(c) * 0.06f * w - 0.03f * w + breathe * 0.006f * idle;
                rigRoot.localPosition = new Vector3(0f, bob, 0f);
                float sq = squash * squash;
                rigRoot.localScale = new Vector3(1f + sq * 0.25f, 1f - sq * 0.35f, 1f + sq * 0.25f);
            }
        }

        // Staff pose snapshots (local Euler degrees), designed on the Arcanist rig.
        private static readonly Vector3 StaffIdleArmR = new Vector3(-10f, 0f, 7f), StaffIdleElbowR = new Vector3(-20f, 0f, 0f);
        private static readonly Vector3 GripIdle = new Vector3(30f, 0f, -4f), GripWalk = new Vector3(52f, 0f, -4f);
        // Channel: staff held open in both hands above the shoulders, torso leaning back 10 degrees.
        private static readonly Vector3 ChSpine = new Vector3(-10f, -5f, 0f), ChNeck = new Vector3(6f, 3.5f, 0f);
        private static readonly Vector3 ChArmL = new Vector3(-43.986635f, 19.590863f, -8.530011f), ChElbowL = new Vector3(-89.467451f, 0f, 0f);
        private static readonly Vector3 ChArmR = new Vector3(-51.407405f, -7.007689f, 12.097582f), ChElbowR = new Vector3(-90.757374f, 0f, 0f);
        private static readonly Vector3 ChGrip = new Vector3(26.928698f, -178.996582f, 92.168613f);
        private static readonly Vector3 CaSpine = new Vector3(8f, -16f, 0f), CaNeck = new Vector3(-4.8f, 11.2f, 0f);
        private static readonly Vector3 CaArmL = new Vector3(-60f, -15f, -22f), CaElbowL = new Vector3(-35f, 0f, 0f);
        private static readonly Vector3 CaArmR = new Vector3(-82f, 24f, 9f), CaElbowR = new Vector3(-8f, 0f, 0f);
        private static readonly Vector3 CaGrip = new Vector3(-11.061f, 6.625f, 167.258f);

        /// <summary>
        /// Staff holder layer on top of the base pose: the right arm keeps the staff (no swing), charging
        /// raises it in both hands (channel), casts and attacks swing it forward.
        /// </summary>
        private void StaffPose(float run)
        {
            float cast = Mathf.Max(castWeight, attackWeight);
            float hold = 1f - Mathf.Max(cast, channelWeight);
            // Right arm holds the staff instead of swinging (only partly while running).
            Blend(armR, StaffIdleArmR + new Vector3(-12f * run, 0f, 0f), hold);
            Blend(elbowR, StaffIdleElbowR + new Vector3(-10f * run, 0f, 0f), hold);

            // Legs keep the base walk/idle pose: the Arcanist can walk (slowed) while charging.
            Blend(spine, ChSpine, channelWeight);
            Blend(neck, ChNeck, channelWeight);
            Blend(armL, ChArmL, channelWeight);
            Blend(elbowL, ChElbowL, channelWeight);
            Blend(armR, ChArmR, channelWeight);
            Blend(elbowR, ChElbowR, channelWeight);

            Blend(spine, CaSpine, cast * 0.7f);
            Blend(neck, CaNeck, cast * 0.7f);
            Blend(armL, CaArmL, castWeight);           // the free hand only joins real casts
            Blend(elbowL, CaElbowL, castWeight);
            Blend(armR, CaArmR, cast);
            Blend(elbowR, CaElbowR, cast);

            Quaternion grip = Quaternion.Slerp(Quaternion.Euler(GripIdle), Quaternion.Euler(GripWalk), run);
            grip = Quaternion.Slerp(grip, Quaternion.Euler(ChGrip), channelWeight);
            grip = Quaternion.Slerp(grip, Quaternion.Euler(CaGrip), cast);
            staffGrip.localRotation = grip;
        }

        private static void Blend(Transform tr, Vector3 euler, float weight)
        {
            if (tr == null || weight <= 0f) return;
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.Euler(euler), Mathf.Clamp01(weight));
        }

        private static void Set(Transform tr, float x, float y, float z)
        {
            if (tr != null) tr.localRotation = Quaternion.Euler(x, y, z);
        }
    }
}
