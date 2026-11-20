using System;
using MobaTrainer.Audio;
using MobaTrainer.Core;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Q/W/E + Blink ability slots (with MOBA-style cast times) with per-key cast modes:
    /// <list type="bullet">
    /// <item><b>Normal</b>: press shows the indicator, left-click casts, right-click or same key cancels.</item>
    /// <item><b>Quick</b>: casts immediately at the cursor.</item>
    /// <item><b>Quick + indicator</b>: hold shows the indicator, release casts.</item>
    /// </list>
    /// Input is fed by <see cref="PlayerController"/> so there is exactly one input owner per frame.
    /// </summary>
    [RequireComponent(typeof(Unit), typeof(PlayerMotor), typeof(PlayerAttack))]
    public class PlayerAbilities : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public GameAction action = GameAction.AbilityQ;
            public AbilityDefinition ability;
            [NonSerialized] public float cooldown;
        }

        [Header("Slots (Q W E + Blink)")]
        public Slot[] slots =
        {
            new Slot { action = GameAction.AbilityQ },
            new Slot { action = GameAction.AbilityW },
            new Slot { action = GameAction.AbilityE },
            new Slot { action = GameAction.Blink },
        };

        [Header("References")]
        public CastIndicator indicator;
        public GameObject shieldBubble;

        [Header("Tuning")]
        [Tooltip("Global cooldown multiplier (scenarios may shorten cooldowns).")]
        public float cooldownScale = 1f;

        /// <summary>Slot index currently aiming (Normal or Quick+Indicator), -1 if none.</summary>
        public int AimingSlot => targetingSlot >= 0 ? targetingSlot : heldSlot;
        public bool IsAiming => AimingSlot >= 0;

        /// <summary>(slotIndex, ability, aimPoint)</summary>
        public event Action<int, AbilityDefinition, Vector3> AbilityCast;
        public int CastCount { get; private set; }

        private Unit self;
        private PlayerMotor motor;
        private PlayerAttack attack;
        private int targetingSlot = -1;
        private int heldSlot = -1;
        private Vector3 cursor;

        private void Awake()
        {
            self = GetComponent<Unit>();
            motor = GetComponent<PlayerMotor>();
            attack = GetComponent<PlayerAttack>();
        }

        public void ResetAbilities()
        {
            for (int i = 0; i < slots.Length; i++) slots[i].cooldown = 0f;
            targetingSlot = heldSlot = -1;
            castingSlot = -1;
            queuedSlot = -1;
            chargingSlot = -1;
            chargeTimer = 0f;
            lockTimer = 0f;
            motor.SpeedMultiplier = 1f;
            motor.Paused = false;
            CastCount = 0;
            if (indicator != null) indicator.HideAbility();
            if (shieldBubble != null) shieldBubble.SetActive(false);
        }

        /// <summary>Reduces every running ability cooldown (Blink excluded) by <paramref name="seconds"/>.</summary>
        public void ReduceCooldowns(float seconds)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].action == GameAction.Blink || slots[i].cooldown <= 0f) continue;
                slots[i].cooldown = Mathf.Max(0f, slots[i].cooldown - seconds);
            }
        }

        /// <summary>Bolt target after a shift: a marked enemy in range takes priority, else the nearest one.</summary>
        private Unit MarkedOrNearest(float range)
        {
            Unit best = null;
            float bestSq = range * range;
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive || !u.IsMarked || !UnitRegistry.IsHostile(self.team, u.team)) continue;
                Vector3 d = u.Position - self.Position;
                d.y = 0f;
                float reach = range + u.radius;
                if (d.sqrMagnitude <= reach * reach && d.sqrMagnitude < bestSq + 1e-3f) { best = u; bestSq = d.sqrMagnitude; }
            }
            return best != null ? best : UnitRegistry.NearestHostile(self.team, self.Position, range);
        }

        public float CooldownRemaining(int slot) => slot >= 0 && slot < slots.Length ? Mathf.Max(0f, slots[slot].cooldown) : 0f;

        public float CooldownFraction(int slot)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot].ability == null) return 0f;
            float total = slots[slot].ability.cooldown * cooldownScale;
            return total > 0f ? Mathf.Clamp01(slots[slot].cooldown / total) : 0f;
        }

        public void CancelAiming()
        {
            targetingSlot = -1;
            heldSlot = -1;
            if (indicator != null) indicator.HideAbility();
        }

        /// <summary>
        /// Processes ability input for this frame. Returns true if the Select (left-click) press
        /// was consumed by a cast, so the controller must not treat it as an attack-move click.
        /// </summary>
        public bool HandleInput(InputManager input, Vector3 cursorGround, out bool moveCancelledAim)
        {
            cursor = cursorGround;
            moveCancelledAim = false;
            bool selectConsumed = false;
            SettingsData settings = SettingsManager.Current;

            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot.ability == null || !input.Pressed(slot.action)) continue;
                // Beam release lockout: nothing can be cast, Blink included.
                if (IsLocked) continue;
                // While charging a beam only Blink can be used (it keeps charging from the new spot).
                if (IsCharging && slot.action != GameAction.Blink) continue;

                if (slot.cooldown > 0f)
                {
                    if (UIAudio.Instance != null) UIAudio.Instance.PlayError();
                    continue;
                }

                // Charged beam: press starts the charge whatever the cast mode, release fires.
                if (slot.ability.shape == AbilityShape.ChargedBeam)
                {
                    if (!IsCasting) StartCharge(i);
                    continue;
                }

                int castSlot = GameActionInfo.CastSlot(slot.action);
                CastMode mode = slot.ability.IsSelfCast ? CastMode.Quick : settings.GetCastMode(castSlot);
                switch (mode)
                {
                    case CastMode.Quick:
                        heldSlot = -1;
                        targetingSlot = -1;
                        RequestCast(i, cursorGround);
                        break;
                    case CastMode.Normal:
                        heldSlot = -1;
                        targetingSlot = targetingSlot == i ? -1 : i;
                        break;
                    case CastMode.QuickIndicator:
                        targetingSlot = -1;
                        heldSlot = i;
                        break;
                }
            }

            if (IsCharging && !input.Held(slots[chargingSlot].action)) ReleaseCharge(cursorGround);

            if (heldSlot >= 0 && !input.Held(slots[heldSlot].action))
            {
                int s = heldSlot;
                heldSlot = -1;
                if (slots[s].cooldown <= 0f) RequestCast(s, cursorGround);
            }

            if (targetingSlot >= 0)
            {
                if (input.Pressed(GameAction.Select))
                {
                    int s = targetingSlot;
                    targetingSlot = -1;
                    RequestCast(s, cursorGround);
                    selectConsumed = true;
                }
                else if (input.Pressed(GameAction.Move))
                {
                    targetingSlot = -1;
                    moveCancelledAim = true;
                }
            }

            return selectConsumed;
        }

        // ------------------------------------------------------------------ charged beam

        private int chargingSlot = -1;
        private float chargeTimer;

        /// <summary>(slotIndex, ability) when a charge starts (sound / animation).</summary>
        public event Action<int, AbilityDefinition> ChargeStarted;
        /// <summary>(slotIndex) when a charge ends without firing (held too long / reset).</summary>
        public event Action<int> ChargeCancelled;

        public bool IsCharging => chargingSlot >= 0;
        public int ChargingSlot => chargingSlot;

        /// <summary>0..1 progress toward full range.</summary>
        public float Charge01
        {
            get
            {
                if (chargingSlot < 0) return 0f;
                AbilityDefinition a = slots[chargingSlot].ability;
                return a.chargeTime > 0f ? Mathf.Clamp01(chargeTimer / a.chargeTime) : 1f;
            }
        }

        /// <summary>Current beam length while charging.</summary>
        public float ChargeRange
        {
            get
            {
                if (chargingSlot < 0) return 0f;
                AbilityDefinition a = slots[chargingSlot].ability;
                return Mathf.Lerp(Mathf.Min(a.chargeMinRange, a.range), a.range, Charge01);
            }
        }

        private void StartCharge(int slotIndex)
        {
            attack.CancelWindup();
            chargingSlot = slotIndex;
            chargeTimer = 0f;
            targetingSlot = heldSlot = -1;
            ChargeStarted?.Invoke(slotIndex, slots[slotIndex].ability);
        }

        private void ReleaseCharge(Vector3 point)
        {
            int slotIndex = chargingSlot;
            AbilityDefinition a = slots[slotIndex].ability;
            float length = ChargeRange;
            EndCharge();

            Vector3 origin = self.Position;
            Vector3 dir = point - origin;
            dir.y = 0f;
            Vector3 dirN = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            slots[slotIndex].cooldown = a.cooldown * cooldownScale;
            CastCount++;
            motor.Face(origin + dirN);
            AbilityCast?.Invoke(slotIndex, a, origin + dirN * length);
            PlayerSkillSystem.Instance?.CastBeam(self, a, origin, dirN, length, Palette.Accent);

            // Release lockout: the caster stands still until the beam lands (no move, attack, cast, Blink).
            if (a.releaseLock > 0f)
            {
                lockTimer = a.releaseLock;
                motor.Paused = true;
            }
        }

        private float lockTimer;

        /// <summary>True during a beam's release lockout.</summary>
        public bool IsLocked => lockTimer > 0f;

        /// <summary>Drops a charge in progress without firing or starting the cooldown (menus, item offers).</summary>
        public void CancelCharge()
        {
            if (chargingSlot < 0) return;
            int s = chargingSlot;
            EndCharge();
            ChargeCancelled?.Invoke(s);
        }

        private void EndCharge()
        {
            chargingSlot = -1;
            chargeTimer = 0f;
            motor.SpeedMultiplier = 1f;
        }

        private void TickCharge(float dt)
        {
            if (chargingSlot < 0) return;
            AbilityDefinition a = slots[chargingSlot].ability;
            chargeTimer += dt;
            motor.SpeedMultiplier = 1f - Mathf.Clamp01(a.chargeSelfSlow) * Charge01;
            if (chargeTimer >= a.chargeMaxHold)
            {
                // Held too long: the charge fizzles, half the cooldown is refunded.
                int s = chargingSlot;
                EndCharge();
                slots[s].cooldown = a.cooldown * cooldownScale * 0.5f;
                ChargeCancelled?.Invoke(s);
            }
        }

        // ------------------------------------------------------------------ casting

        private int castingSlot = -1;
        private int queuedSlot = -1;
        private Vector3 queuedPoint;
        private float castTimer;
        private Vector3 castDir;
        private float castDist;
        private Vector3 castPoint;

        /// <summary>True while a cast time is running (movement paused, no auto-attacks).</summary>
        public bool IsCasting => castingSlot >= 0;
        public int CastingSlot => castingSlot;

        /// <summary>Starts a cast. Direction/point are locked now; the effect fires after the cast time.</summary>
        /// <summary>Set by the controller: runs a command now or after simulated latency / server tick.</summary>
        public System.Action<System.Action> commandSink;

        private void RequestCast(int slotIndex, Vector3 point)
        {
            if (commandSink == null) { Cast(slotIndex, point); return; }
            commandSink(() =>
            {
                if (slots[slotIndex].ability != null && slots[slotIndex].cooldown <= 0f && self.IsAlive) Cast(slotIndex, point);
            });
        }

        private void Cast(int slotIndex, Vector3 point)
        {
            Slot slot = slots[slotIndex];
            AbilityDefinition a = slot.ability;

            // Only instant abilities (e.g. blink) may overlap another cast. Others are dropped, or
            // queued to fire right after the current cast when "Queue abilities" is on.
            if (IsCasting && a.castTime > 0f)
            {
                if (SettingsManager.Current.queueAbilities && slotIndex != castingSlot)
                {
                    queuedSlot = slotIndex;
                    queuedPoint = point;
                }
                return;
            }

            Vector3 origin = self.Position;
            Vector3 dir = point - origin;
            dir.y = 0f;
            float dist = dir.magnitude;
            Vector3 dirN = dist > 0.001f ? dir / dist : transform.forward;

            attack.CancelWindup();
            slot.cooldown = a.cooldown * cooldownScale;
            CastCount++;
            AbilityCast?.Invoke(slotIndex, a, point);

            if (a.castTime <= 0f)
            {
                Execute(a, dirN, dist, point);
                return;
            }

            castingSlot = slotIndex;
            castTimer = a.castTime;
            castDir = dirN;
            castDist = dist;
            castPoint = point;
            motor.Face(origin + dirN);
            motor.Paused = true;
        }

        private void FinishCast()
        {
            AbilityDefinition a = slots[castingSlot].ability;
            castingSlot = -1;
            motor.Paused = false;

            // The target POINT is locked at key press, not the direction: if you blinked during the
            // cast time, the shot re-aims from your new position toward where you were aiming.
            Vector3 dir = castPoint - self.Position;
            dir.y = 0f;
            float dist = dir.magnitude;
            Vector3 dirN = dist > 0.6f ? dir / dist : castDir;
            Execute(a, dirN, dist, castPoint);

            if (queuedSlot >= 0)
            {
                int q = queuedSlot;
                queuedSlot = -1;
                if (slots[q].ability != null && slots[q].cooldown <= 0f) Cast(q, queuedPoint);
            }
        }

        private void Execute(AbilityDefinition a, Vector3 dirN, float dist, Vector3 point)
        {
            Vector3 origin = self.Position;
            switch (a.shape)
            {
                case AbilityShape.LinearSkillshot:
                    motor.Face(origin + dirN);
                    PlayerSkillSystem.Instance?.FireLinear(self, a, origin, dirN);
                    break;
                case AbilityShape.CircleSkillshot:
                    motor.Face(origin + dirN);
                    PlayerSkillSystem.Instance?.CastCircle(self, a, Arena.ClampStatic(origin + dirN * Mathf.Min(dist, a.range)), Palette.Accent);
                    break;
                case AbilityShape.Dash:
                    motor.Dash(dirN, a.range, a.speed);
                    break;
                case AbilityShape.Blink:
                case AbilityShape.ShiftBolt:
                {
                    Vector3 dest = origin + dirN * Mathf.Min(dist, a.range);
                    FxSystem.Instance?.Flash(origin, 1.0f, Palette.Accent, 0.35f);
                    motor.Blink(dest);
                    // Like the classic MOBA rule: after a blink you stand still (the old move / attack
                    // order is dropped), you don't keep walking to where you clicked before.
                    motor.Stop();
                    attack.ClearOrders();
                    FxSystem.Instance?.Flash(self.Position, 1.2f, Palette.AccentHover, 0.4f);
                    if (a.shape == AbilityShape.ShiftBolt)
                    {
                        Unit target = MarkedOrNearest(a.boltRange);
                        if (target != null) PlayerSkillSystem.Instance?.FireHoming(self, target, a.speed, a.damage, a);
                    }
                    break;
                }
                case AbilityShape.SelfShield:
                    self.AddShield(a.shieldAmount, a.shieldDuration);
                    shieldTimer = a.shieldDuration;
                    if (shieldBubble != null) shieldBubble.SetActive(true);
                    FxSystem.Instance?.Flash(origin, 1.3f, Color.white.WithAlpha(0.8f), 0.3f);
                    break;
            }
        }

        private float shieldTimer;

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].cooldown > 0f) slots[i].cooldown -= dt;
            }

            if (castingSlot >= 0)
            {
                castTimer -= dt;
                if (castTimer <= 0f) FinishCast();
            }
            if (chargingSlot >= 0 && !self.IsAlive) { int s = chargingSlot; EndCharge(); ChargeCancelled?.Invoke(s); }
            TickCharge(dt);
            if (lockTimer > 0f)
            {
                lockTimer -= dt;
                if (lockTimer <= 0f && castingSlot < 0) motor.Paused = false;
            }

            if (shieldTimer > 0f)
            {
                shieldTimer -= dt;
                if (shieldBubble != null && (shieldTimer <= 0f || self.Shield <= 0f))
                {
                    shieldBubble.SetActive(false);
                    shieldTimer = 0f;
                }
            }

            if (indicator == null) return;
            if (chargingSlot >= 0)
            {
                indicator.ShowCharge(slots[chargingSlot].ability, self.Position, cursor, ChargeRange);
                return;
            }
            int aim = AimingSlot;
            if (aim >= 0 && slots[aim].ability != null) indicator.ShowAbility(slots[aim].ability, self.Position, cursor);
            else indicator.HideAbility();
        }
    }
}
