using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Auto-attack state machine built for orb-walking:
    /// <list type="bullet">
    /// <item>The attack timer starts when the windup starts (interval = 1 / attacksPerSecond).</item>
    /// <item>Any move order during the windup cancels the attack and refunds the timer.</item>
    /// <item>Once the windup completes the hit is committed (projectile launched) and the
    ///   player is free to move for the rest of the interval — that's the orb-walk window.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(Unit), typeof(PlayerMotor))]
    public class PlayerAttack : MonoBehaviour
    {
        [Header("Attack")]
        [Tooltip("Edge-to-edge range, like most MOBAs.")]
        public float attackRange = 5.5f;
        public float attacksPerSecond = 1.25f; // doubled for intense training
        [Tooltip("Fraction of the attack interval spent winding up (can't move without cancelling).")]
        [Range(0.05f, 0.6f)] public float windupFraction = 0.18839f;
        public float damage = 40f;

        [Header("Projectile")]
        public bool ranged = true;
        public float projectileSpeed = 20f;

        [Header("Targeting")]
        [Tooltip("Extra acquisition radius around the attack-move point / player.")]
        public float acquisitionBonus = 1.5f;

        public enum State { Idle, Windup }

        public State CurrentState { get; private set; }
        public Unit Target { get; private set; }
        public bool AttackMoveActive { get; private set; }
        public bool Holding { get; private set; }
        public float WindupTime => windupFraction / Mathf.Max(0.01f, attacksPerSecond);
        public float Interval => 1f / Mathf.Max(0.01f, attacksPerSecond);
        public float WindupProgress => CurrentState == State.Windup ? 1f - windupRemaining / Mathf.Max(0.0001f, WindupTime) : 0f;
        public float CooldownRemaining => cooldownRemaining;

        // --- metrics (read by scenarios / stats) ---
        public int AttacksStarted { get; private set; }
        public int AttacksCompleted { get; private set; }
        public int AttacksCancelled { get; private set; }
        public float DamageDealt { get; set; }

        public event System.Action<Unit> AttackCommitted;
        public event System.Action AttackCancelled;

        private Unit self;
        private PlayerMotor motor;
        private float cooldownRemaining;
        private float windupRemaining;
        private Vector3 attackMovePoint;

        private void Awake()
        {
            self = GetComponent<Unit>();
            motor = GetComponent<PlayerMotor>();
        }

        public void ResetAttack()
        {
            CurrentState = State.Idle;
            Target = null;
            AttackMoveActive = false;
            Holding = false;
            cooldownRemaining = 0f;
            windupRemaining = 0f;
            AttacksStarted = AttacksCompleted = AttacksCancelled = 0;
            DamageDealt = 0f;
        }

        public bool InRange(Unit target)
        {
            if (target == null) return false;
            Vector3 d = target.Position - self.Position;
            d.y = 0f;
            float reach = attackRange + self.radius + target.radius;
            return d.sqrMagnitude <= reach * reach;
        }

        // ------------------------------------------------------------------ orders

        public void OrderAttack(Unit target)
        {
            Holding = false;
            AttackMoveActive = false;
            if (target == Target) return; // re-clicking the same target never resets a windup
            CancelWindup();
            Target = target;
        }

        public void OrderAttackMove(Vector3 point)
        {
            Holding = false;
            CancelWindup();
            Target = null;
            AttackMoveActive = true;
            attackMovePoint = Arena.ClampStatic(point, self.radius);
            Target = UnitRegistry.NearestHostile(self.team, point, acquisitionBonus + 1f)
                     ?? UnitRegistry.NearestHostile(self.team, self.Position, attackRange + self.radius);
            if (Target == null) motor.MoveTo(point);
        }

        /// <summary>Called for plain move orders.</summary>
        public void ClearOrders()
        {
            Holding = false;
            AttackMoveActive = false;
            Target = null;
            CancelWindup();
        }

        public void OrderStop()
        {
            ClearOrders();
        }

        public void OrderHold()
        {
            ClearOrders();
            Holding = true;
        }

        public void CancelWindup()
        {
            if (CurrentState != State.Windup) return;
            CurrentState = State.Idle;
            cooldownRemaining = 0f; // cancelled attacks refund the timer
            AttacksCancelled++;
            AttackCancelled?.Invoke();
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (cooldownRemaining > 0f) cooldownRemaining -= dt;

            if (Target != null && !Target.IsAlive)
            {
                if (CurrentState == State.Windup)
                {
                    CurrentState = State.Idle;
                    cooldownRemaining = 0f;
                }
                Target = null;
            }

            if (CurrentState == State.Windup)
            {
                motor.Face(Target.Position);
                windupRemaining -= dt;
                if (windupRemaining <= 0f) Commit();
                return;
            }

            if (motor.IsDashing || motor.Paused) return; // no attacks or chasing during a cast time

            if (Target == null) AcquireAutomatically();

            if (Target == null)
            {
                if (AttackMoveActive && !motor.HasDestination)
                {
                    // Resume walking to the attack-move point after a kill; finish when arrived.
                    Vector3 d = attackMovePoint - self.Position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.09f) motor.MoveTo(attackMovePoint);
                    else AttackMoveActive = false;
                }
                return;
            }

            if (InRange(Target))
            {
                motor.Stop();
                motor.Face(Target.Position);
                if (cooldownRemaining <= 0f) StartWindup();
            }
            else if (Holding)
            {
                Target = null;
            }
            else
            {
                motor.MoveTo(Target.Position);
            }
        }

        private void AcquireAutomatically()
        {
            if (AttackMoveActive)
            {
                Target = UnitRegistry.NearestHostile(self.team, self.Position, attackRange + self.radius + acquisitionBonus);
                return;
            }
            bool idle = !motor.HasDestination;
            if (Holding || (idle && SettingsManager.Current.autoAttackWhenIdle))
            {
                Target = UnitRegistry.NearestHostile(self.team, self.Position, attackRange + self.radius);
            }
        }

        private void StartWindup()
        {
            CurrentState = State.Windup;
            windupRemaining = WindupTime;
            cooldownRemaining = Interval;
            AttacksStarted++;
        }

        private void Commit()
        {
            CurrentState = State.Idle;
            AttacksCompleted++;
            Unit t = Target;
            if (ranged && PlayerSkillSystem.Instance != null)
            {
                PlayerSkillSystem.Instance.FireHoming(self, t, projectileSpeed, damage);
            }
            else
            {
                DamageDealt += t.TakeDamage(damage, self);
            }
            AttackCommitted?.Invoke(t);

            if (AttackMoveActive && !t.IsAlive) Target = null;
        }
    }
}
