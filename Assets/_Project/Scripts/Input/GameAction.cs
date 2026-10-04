namespace MobaTrainer.Input
{
    /// <summary>Every rebindable action. Order is the display order in Settings ▸ Controls.</summary>
    public enum GameAction
    {
        Move = 0,          // right mouse: move / attack unit
        Select,            // left mouse: confirm cast / attack-move click
        AttackMove,
        Stop,
        Hold,
        AbilityQ,
        AbilityW,
        AbilityE,
        AbilityR,
        Blink,             // utility D
        Utility,           // utility F (heal / shield)
        CameraLock,
        CameraCenter,
        Restart,
        Pause,
    }

    public static class GameActionInfo
    {
        public const int Count = 15;

        public static string DisplayName(GameAction action)
        {
            switch (action)
            {
                case GameAction.Move: return "Move / Attack";
                case GameAction.Select: return "Select / Confirm";
                case GameAction.AttackMove: return "Attack Move";
                case GameAction.Stop: return "Stop";
                case GameAction.Hold: return "Hold Position";
                case GameAction.AbilityQ: return "Ability 1";
                case GameAction.AbilityW: return "Ability 2";
                case GameAction.AbilityE: return "Ability 3";
                case GameAction.AbilityR: return "Ability 4";
                case GameAction.Blink: return "Blink";
                case GameAction.Utility: return "Shield";
                case GameAction.CameraLock: return "Camera Lock";
                case GameAction.CameraCenter: return "Center Camera";
                case GameAction.Restart: return "Instant Restart";
                case GameAction.Pause: return "Pause";
                default: return action.ToString();
            }
        }

        public static string DefaultBinding(GameAction action)
        {
            switch (action)
            {
                case GameAction.Move: return "<Mouse>/rightButton";
                case GameAction.Select: return "<Mouse>/leftButton";
                case GameAction.AttackMove: return "<Keyboard>/a";
                case GameAction.Stop: return "<Keyboard>/s";
                case GameAction.Hold: return "<Keyboard>/h";
                case GameAction.AbilityQ: return "<Keyboard>/q";
                case GameAction.AbilityW: return "<Keyboard>/w";
                case GameAction.AbilityE: return "<Keyboard>/e";
                case GameAction.AbilityR: return "<Keyboard>/r";
                case GameAction.Blink: return "<Keyboard>/d";
                case GameAction.Utility: return "<Keyboard>/f";
                case GameAction.CameraLock: return "<Keyboard>/y";
                case GameAction.CameraCenter: return "<Keyboard>/space";
                case GameAction.Restart: return "<Keyboard>/f5";
                case GameAction.Pause: return "<Keyboard>/escape";
                default: return string.Empty;
            }
        }

        /// <summary>Actions the player may rebind (Pause stays on Esc so you can never lock yourself out).</summary>
        public static bool IsRebindable(GameAction action) => action != GameAction.Pause;

        /// <summary>Cast-mode slot index for ability keys (Q W E R D), or -1.</summary>
        public static int CastSlot(GameAction action)
        {
            switch (action)
            {
                case GameAction.AbilityQ: return 0;
                case GameAction.AbilityW: return 1;
                case GameAction.AbilityE: return 2;
                case GameAction.AbilityR: return 3;
                case GameAction.Blink: return 4;
                default: return -1;
            }
        }
    }
}
