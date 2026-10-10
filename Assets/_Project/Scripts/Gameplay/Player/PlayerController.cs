using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// The single owner of gameplay input for the player. Translates rebindable actions into orders:
    /// right-click move/attack (hold to keep issuing), A + left-click attack-move, S stop, H hold,
    /// and forwards ability keys to <see cref="PlayerAbilities"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Unit), typeof(PlayerMotor), typeof(PlayerAttack))]
    [RequireComponent(typeof(PlayerAbilities))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("References")]
        public Camera worldCamera;

        [Header("Orders")]
        [Tooltip("While right mouse is held, a new move order is issued this often (seconds).")]
        public float holdRepeatInterval = 0.09f;
        [Tooltip("Extra pick radius when clicking on units.")]
        public float pickTolerance = 0.25f;

        public Unit Unit { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerAttack Attack { get; private set; }
        public PlayerAbilities Abilities { get; private set; }

        /// <summary>False while paused / counting down / in menus.</summary>
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                inputEnabled = value;
                if (Attack != null) Attack.Suspended = !value;   // no auto-attacks during countdowns / menus
            }
        }
        private bool inputEnabled = true;
        public bool AttackMoveArmed { get; private set; }
        public Vector3 CursorGround { get; private set; }

        /// <summary>Distinct player actions (orders + casts), for APM.</summary>
        public int ActionCount { get; private set; }

        private float holdRepeatTimer;

        private void Awake()
        {
            Instance = this;
            Unit = GetComponent<Unit>();
            Motor = GetComponent<PlayerMotor>();
            Attack = GetComponent<PlayerAttack>();
            Abilities = GetComponent<PlayerAbilities>();
            if (worldCamera == null) worldCamera = Camera.main;
            Abilities.AbilityCast += (_, __, ___) => ActionCount++;
            Abilities.commandSink = Submit;
        }

        public void ResetPlayer(Vector3 position)
        {
            Unit.ResetUnit();
            Motor.ResetMotor(position);
            Attack.ResetAttack();
            Abilities.ResetAbilities();
            AttackMoveArmed = false;
            ActionCount = 0;
            pending.Clear();
            UpdateCursorTint();
        }

        // ------------------------------------------------------------------ simulated latency / server tick

        private struct Command
        {
            public float due;
            public System.Action run;
        }

        private readonly List<Command> pending = new List<Command>(32);
        private const float TickInterval = 1f / 30f;

        /// <summary>
        /// Runs a player command now, or after the simulated ping and/or on the next 30 Hz server tick.
        /// Visual feedback (click markers) stays instant, like in an online client.
        /// </summary>
        public void Submit(System.Action command)
        {
            SettingsData s = SettingsManager.Current;
            if (s.simulatedLatencyMs <= 0 && !s.serverTick)
            {
                command();
                return;
            }
            float due = Time.time + s.simulatedLatencyMs / 1000f;
            if (s.serverTick) due = Mathf.Ceil(due / TickInterval) * TickInterval;
            pending.Add(new Command { due = due, run = command });
        }

        private void FlushCommands()
        {
            if (pending.Count == 0) return;
            float now = Time.time;
            int i = 0;
            while (i < pending.Count)
            {
                if (pending[i].due <= now)
                {
                    System.Action run = pending[i].run;
                    pending.RemoveAt(i);   // keep order: commands run in the order they were issued
                    run();
                }
                else i++;
            }
        }

        private void Update()
        {
            FlushCommands();
            InputManager input = InputManager.Instance;
            CursorService cursor = CursorService.Instance;
            if (input == null || cursor == null || worldCamera == null) return;

            if (cursor.TryGetGroundPoint(worldCamera, out Vector3 ground)) CursorGround = ground;

            if (!InputEnabled || !Unit.IsAlive || input.SuppressShortcuts)
            {
                if (AttackMoveArmed) { AttackMoveArmed = false; UpdateCursorTint(); }
                Abilities.CancelAiming();
                return;
            }

            // 1) Abilities first: they may consume the left-click (Normal cast confirm).
            if (input.Pressed(GameAction.AttackMove)) Abilities.CancelAiming();
            bool selectConsumed = Abilities.HandleInput(input, CursorGround, out _);
            if (Abilities.IsAiming && AttackMoveArmed)
            {
                AttackMoveArmed = false;
            }

            // 2) Attack-move: arm and wait for left-click, or (setting) fire instantly at the cursor.
            if (input.Pressed(GameAction.AttackMove))
            {
                if (SettingsManager.Current.attackMoveOnKeyPress && !Abilities.IsAiming)
                {
                    AttackMoveArmed = false;
                    IssueAttackMove();
                }
                else
                {
                    AttackMoveArmed = true;
                }
            }

            // 3) Right-click: move / attack (and cancels attack-move arming).
            if (input.Pressed(GameAction.Move))
            {
                AttackMoveArmed = false;
                IssueRightClick(true);
                ActionCount++;
                holdRepeatTimer = 0f;
            }
            else if (input.Held(GameAction.Move))
            {
                holdRepeatTimer += Time.deltaTime;
                if (holdRepeatTimer >= holdRepeatInterval)
                {
                    holdRepeatTimer = 0f;
                    IssueRightClick(false);
                }
            }

            // 4) Left-click while armed: attack-move.
            if (AttackMoveArmed && !selectConsumed && input.Pressed(GameAction.Select))
            {
                AttackMoveArmed = false;
                IssueAttackMove();
            }

            // 5) Stop / Hold.
            if (input.Pressed(GameAction.Stop))
            {
                AttackMoveArmed = false;
                Abilities.CancelAiming();
                Submit(() => { Attack.OrderStop(); Motor.Stop(); });
                ActionCount++;
            }
            if (input.Pressed(GameAction.Hold))
            {
                AttackMoveArmed = false;
                Abilities.CancelAiming();
                Submit(() => { Attack.OrderHold(); Motor.Stop(); });
                ActionCount++;
            }

            UpdateCursorTint();
            UpdateRangePreview();
        }

        /// <summary>Attack the hostile under the cursor, else attack-move to the cursor point.</summary>
        private void IssueAttackMove()
        {
            Unit clicked = PickHostile();
            if (clicked != null)
            {
                Submit(() => { if (clicked != null && clicked.IsAlive) Attack.OrderAttack(clicked); });
                FxSystem.Instance?.UnitPulse(clicked.transform, clicked.radius, Palette.Enemy);
            }
            else
            {
                Vector3 point = CursorGround;
                Submit(() => Attack.OrderAttackMove(point));
                FxSystem.Instance?.ClickIndicator(point, Palette.DangerHover);
            }
            ActionCount++;
        }

        private void IssueRightClick(bool showFeedback)
        {
            Unit clicked = PickHostile();
            if (clicked != null)
            {
                bool newTarget = clicked != Attack.Target;
                Submit(() => { if (clicked != null && clicked.IsAlive) Attack.OrderAttack(clicked); });
                if (showFeedback || newTarget) FxSystem.Instance?.UnitPulse(clicked.transform, clicked.radius, Palette.Enemy);
            }
            else
            {
                Vector3 point = CursorGround;
                Submit(() => { Attack.ClearOrders(); Motor.MoveTo(point); });
                if (showFeedback) FxSystem.Instance?.ClickIndicator(Arena.ClampStatic(point, Unit.radius), Palette.Hex(0x6FD35A, 0.9f));
            }
        }

        private Unit PickHostile()
        {
            Unit u = UnitRegistry.Pick(CursorService.Instance.ScreenRay(worldCamera), pickTolerance, Unit);
            return u != null && UnitRegistry.IsHostile(Unit.team, u.team) ? u : null;
        }

        private void UpdateCursorTint()
        {
            CursorService cursor = CursorService.Instance;
            if (cursor == null) return;
            if (AttackMoveArmed) cursor.SetState(CursorState.Attack);
            else if (Abilities != null && Abilities.IsAiming) cursor.SetState(CursorState.Cast);
            else cursor.SetState(CursorState.Normal);
        }

        private void UpdateRangePreview()
        {
            CastIndicator ind = Abilities.indicator;
            if (ind == null) return;
            if (AttackMoveArmed && SettingsManager.Current.showRangeOnAttackMove)
                ind.ShowAttackRange(Unit.Position, Attack.attackRange + Unit.radius);
            else
                ind.HideAttackRange();
        }
    }
}
