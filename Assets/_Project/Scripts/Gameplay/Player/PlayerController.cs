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
        public bool InputEnabled { get; set; } = true;
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
        }

        public void ResetPlayer(Vector3 position)
        {
            Unit.ResetUnit();
            Motor.ResetMotor(position);
            Attack.ResetAttack();
            Abilities.ResetAbilities();
            AttackMoveArmed = false;
            ActionCount = 0;
            UpdateCursorTint();
        }

        private void Update()
        {
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

            // 2) Attack-move arming.
            if (input.Pressed(GameAction.AttackMove))
            {
                AttackMoveArmed = true;
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
                Unit clicked = PickHostile();
                if (clicked != null)
                {
                    Attack.OrderAttack(clicked);
                    FxSystem.Instance?.UnitPulse(clicked.transform, clicked.radius, Palette.Enemy);
                }
                else
                {
                    Attack.OrderAttackMove(CursorGround);
                    FxSystem.Instance?.ClickIndicator(CursorGround, Palette.DangerHover);
                }
                ActionCount++;
            }

            // 5) Stop / Hold.
            if (input.Pressed(GameAction.Stop))
            {
                AttackMoveArmed = false;
                Abilities.CancelAiming();
                Attack.OrderStop();
                Motor.Stop();
                ActionCount++;
            }
            if (input.Pressed(GameAction.Hold))
            {
                AttackMoveArmed = false;
                Abilities.CancelAiming();
                Attack.OrderHold();
                Motor.Stop();
                ActionCount++;
            }

            UpdateCursorTint();
            UpdateRangePreview();
        }

        private void IssueRightClick(bool showFeedback)
        {
            Unit clicked = PickHostile();
            if (clicked != null)
            {
                bool newTarget = clicked != Attack.Target;
                Attack.OrderAttack(clicked);
                if (showFeedback || newTarget) FxSystem.Instance?.UnitPulse(clicked.transform, clicked.radius, Palette.Enemy);
            }
            else
            {
                Attack.ClearOrders();
                Motor.MoveTo(CursorGround);
                if (showFeedback) FxSystem.Instance?.ClickIndicator(Arena.ClampStatic(CursorGround, Unit.radius), Palette.Hex(0x6FD35A, 0.9f));
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
            if (AttackMoveArmed) cursor.SetTint(Palette.DangerHover);
            else if (Abilities != null && Abilities.IsAiming) cursor.SetTint(Palette.AccentHover);
            else cursor.SetTint(Color.white);
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
