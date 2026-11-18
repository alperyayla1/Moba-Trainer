using System;
using System.Collections.Generic;
using MobaTrainer.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MobaTrainer.Input
{
    /// <summary>
    /// Builds the game's actions in code (one binding per <see cref="GameAction"/>), exposes
    /// polled button state, and handles interactive rebinding with swap-on-conflict.
    /// Overrides are stored in bindings.json via <see cref="SaveSystem"/>.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class InputManager : MonoBehaviour
    {
        public const string BindingsFile = "bindings.json";

        public static InputManager Instance { get; private set; }

        public InputActionAsset Asset { get; private set; }

        /// <summary>Raised whenever any binding changed (rebind, reset, load).</summary>
        public event Action BindingsChanged;

        public bool IsRebinding => rebindOperation != null;

        /// <summary>True while rebinding and for a couple of frames afterwards, so the key that
        /// ended a rebind (e.g. Esc) doesn't also trigger gameplay/menu shortcuts.</summary>
        public bool SuppressShortcuts => IsRebinding || Time.frameCount - lastRebindFrame <= 2;

        private readonly InputAction[] actions = new InputAction[GameActionInfo.Count];
        private InputActionRebindingExtensions.RebindingOperation rebindOperation;
        private int lastRebindFrame = -10;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Build();
            LoadOverrides();
            Asset.Enable();
        }

        private void OnDestroy()
        {
            rebindOperation?.Dispose();
            if (Asset != null)
            {
                Asset.Disable();
                Destroy(Asset);
            }
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "MobaTrainerControls";
            InputActionMap map = Asset.AddActionMap("Gameplay");
            for (int i = 0; i < GameActionInfo.Count; i++)
            {
                var ga = (GameAction)i;
                actions[i] = map.AddAction(ga.ToString(), InputActionType.Button, GameActionInfo.DefaultBinding(ga));
            }
        }

        public InputAction Get(GameAction action) => actions[(int)action];

        public bool Pressed(GameAction action) => actions[(int)action].WasPressedThisFrame();
        public bool Released(GameAction action) => actions[(int)action].WasReleasedThisFrame();
        public bool Held(GameAction action) => actions[(int)action].IsPressed();

        // ------------------------------------------------------------------ display

        public string BindingPath(GameAction action) => actions[(int)action].bindings[0].effectivePath;

        /// <summary>Full display string, e.g. "Q", "Right Button", "F5".</summary>
        public string BindingDisplay(GameAction action)
        {
            string s = actions[(int)action].GetBindingDisplayString(0, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            return string.IsNullOrEmpty(s) ? "-" : s;
        }

        /// <summary>Compact label for the HUD (max ~4 chars), e.g. "Q", "RMB", "M4".</summary>
        public string BindingShort(GameAction action)
        {
            string path = BindingPath(action);
            if (string.IsNullOrEmpty(path)) return "-";
            switch (path)
            {
                case "<Mouse>/leftButton": return "LMB";
                case "<Mouse>/rightButton": return "RMB";
                case "<Mouse>/middleButton": return "MMB";
                case "<Mouse>/forwardButton": return "M5";
                case "<Mouse>/backButton": return "M4";
                case "<Keyboard>/space": return "SPC";
                case "<Keyboard>/leftShift": return "LSH";
                case "<Keyboard>/leftCtrl": return "LCT";
                case "<Keyboard>/leftAlt": return "LAL";
                case "<Keyboard>/tab": return "TAB";
                case "<Keyboard>/escape": return "ESC";
            }
            string full = BindingDisplay(action);
            return full.Length <= 4 ? full.ToUpperInvariant() : full.Substring(0, 4).ToUpperInvariant();
        }

        // ------------------------------------------------------------------ rebinding

        /// <summary>Waits for the next key/button and binds it. Esc cancels. A key already used by
        /// another action is swapped onto that action so no two actions ever share a key.</summary>
        public void StartRebind(GameAction gameAction, Action<bool> onFinished)
        {
            if (!GameActionInfo.IsRebindable(gameAction) || IsRebinding)
            {
                onFinished?.Invoke(false);
                return;
            }

            InputAction action = actions[(int)gameAction];
            string previousPath = action.bindings[0].effectivePath;
            action.Disable();

            rebindOperation = action.PerformInteractiveRebinding(0)
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Mouse>/scroll/x")
                .WithControlsExcluding("<Mouse>/scroll/y")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(op =>
                {
                    string newPath = action.bindings[0].effectivePath;
                    ResolveConflicts(gameAction, newPath, previousPath);
                    EndRebind(action);
                    SaveOverrides();
                    BindingsChanged?.Invoke();
                    onFinished?.Invoke(true);
                })
                .OnCancel(op =>
                {
                    EndRebind(action);
                    onFinished?.Invoke(false);
                })
                .Start();
        }

        /// <summary>Binds an action to a fixed control path (e.g. "&lt;Keyboard&gt;/f"), swapping any conflict.</summary>
        public void ApplyBinding(GameAction gameAction, string path)
        {
            InputAction action = actions[(int)gameAction];
            string previousPath = action.bindings[0].effectivePath;
            if (string.Equals(previousPath, path, StringComparison.OrdinalIgnoreCase)) return;
            action.ApplyBindingOverride(0, path);
            ResolveConflicts(gameAction, path, previousPath);
            SaveOverrides();
            BindingsChanged?.Invoke();
        }

        public void CancelRebind()
        {
            rebindOperation?.Cancel();
        }

        private void EndRebind(InputAction action)
        {
            rebindOperation?.Dispose();
            rebindOperation = null;
            lastRebindFrame = Time.frameCount;
            action.Enable();
        }

        private void ResolveConflicts(GameAction changed, string newPath, string previousPath)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (i == (int)changed) continue;
                InputAction other = actions[i];
                if (string.Equals(other.bindings[0].effectivePath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    other.ApplyBindingOverride(0, previousPath);
                }
            }
        }

        public void ResetAllBindings()
        {
            Asset.RemoveAllBindingOverrides();
            SaveSystem.Delete(BindingsFile);
            BindingsChanged?.Invoke();
        }

        [Serializable]
        private class SavedBinding { public string action; public string path; }

        [Serializable]
        private class SavedBindings { public List<SavedBinding> bindings = new List<SavedBinding>(); }

        /// <summary>
        /// Saves every changed key by action NAME. (Unity's own override JSON is keyed by binding ids,
        /// which are new every launch because the actions are built in code, so it never loaded back.)
        /// </summary>
        private void SaveOverrides()
        {
            var data = new SavedBindings();
            for (int i = 0; i < actions.Length; i++)
            {
                InputBinding b = actions[i].bindings[0];
                if (string.IsNullOrEmpty(b.overridePath)) continue;
                data.bindings.Add(new SavedBinding { action = ((GameAction)i).ToString(), path = b.overridePath });
            }
            SaveSystem.WriteText(BindingsFile, JsonUtility.ToJson(data, true));
        }

        private void LoadOverrides()
        {
            string json = SaveSystem.ReadText(BindingsFile);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                // Also reads the old format ("Gameplay/Blink"): only the action name and path are used.
                SavedBindings data = JsonUtility.FromJson<SavedBindings>(json);
                if (data?.bindings == null) return;
                foreach (SavedBinding s in data.bindings)
                {
                    if (s == null || string.IsNullOrEmpty(s.action) || string.IsNullOrEmpty(s.path)) continue;
                    string name = s.action.Contains("/") ? s.action.Substring(s.action.LastIndexOf('/') + 1) : s.action;
                    if (!Enum.TryParse(name, out GameAction ga) || (int)ga < 0 || (int)ga >= actions.Length) continue;
                    actions[(int)ga].ApplyBindingOverride(0, s.path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputManager] Ignoring invalid bindings file: {e.Message}");
            }
        }
    }
}
