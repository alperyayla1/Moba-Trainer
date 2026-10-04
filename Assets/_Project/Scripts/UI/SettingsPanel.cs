using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>
    /// Settings modal: text tabs with a sliding underline, rows generated at runtime.
    /// Every change applies live and is saved to settings.json when the window closes.
    /// </summary>
    public class SettingsPanel : UIPanel
    {
        private enum Tab { Audio, Video, Controls, Keybinds }

        [Header("Theme")]
        public UITheme theme;

        [Header("References")]
        public Button closeButton;
        public RectTransform tabBar;
        public RectTransform tabIndicator;
        public TextMeshProUGUI tabTitle;
        public RectTransform content;
        public ScrollRect scrollRect;
        public DraggablePanel dragHandle;

        private readonly List<Button> tabButtons = new List<Button>();
        private readonly Dictionary<GameAction, TextMeshProUGUI> bindingLabels = new Dictionary<GameAction, TextMeshProUGUI>();
        private Tab currentTab = Tab.Audio;
        private bool tabsBuilt;

        public event System.Action Closed;

        private static readonly int[] FpsValues = { 60, 120, 144, 165, 240, 360, 0 };
        private static readonly string[] FpsLabels = { "60", "120", "144", "165", "240", "360", "Unlimited" };
        private static readonly string[] WindowLabels = { "Borderless", "Fullscreen", "Windowed" };
        private static readonly string[] CastModeLabels = { "Normal", "Quick", "Quick + Indicator" };
        private static readonly string[] BlinkKeyLabels = { "D", "F" };
        private static readonly string[] CursorLabels = { "Gauntlet", "Arrow" };
        private static readonly string[] LockLabels = { "Unlocked", "Locked" };

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (InputManager.Instance != null) InputManager.Instance.BindingsChanged += RefreshBindingLabels;
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null) InputManager.Instance.BindingsChanged -= RefreshBindingLabels;
        }

        protected override void OnShown()
        {
            if (!tabsBuilt) BuildTabs();
            if (dragHandle != null) dragHandle.ResetPosition();
            ShowTab(currentTab);
        }

        protected override void OnHidden()
        {
            if (InputManager.Instance != null && InputManager.Instance.IsRebinding) InputManager.Instance.CancelRebind();
            if (SettingsManager.Instance != null) SettingsManager.Instance.SaveIfDirty();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Hide();
            Closed?.Invoke();
        }

        // ------------------------------------------------------------------ tabs

        private static readonly string[] TabNames = { "Audio", "Video", "Controls", "Keybinds" };
        private static readonly UIIcon[] TabIcons = { UIIcon.Speaker, UIIcon.Monitor, UIIcon.Mouse, UIIcon.Keyboard };
        private readonly List<Image> tabIcons = new List<Image>();

        private void BuildTabs()
        {
            tabsBuilt = true;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                Button b = UIFactory.IconButton(theme, tabBar, "Tab_" + TabNames[i], TabIcons[i], 48f, 24f, Palette.TextMuted, Palette.Text);
                UIFactory.Layout(b, 48f, 48f);
                b.onClick.AddListener(() => ShowTab((Tab)index));
                tabButtons.Add(b);
                tabIcons.Add(b.transform.Find("Icon").GetComponent<Image>());
            }
        }

        private void ShowTab(Tab tab)
        {
            currentTab = tab;
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool selected = i == (int)tab;
                var tint = tabButtons[i].GetComponent<UIHoverTint>();
                foreach (UIHoverTint.Target t in tint.targets)
                {
                    if (t.graphic != tabIcons[i]) continue;
                    t.normal = selected ? Palette.Accent : Palette.TextMuted;
                    t.hover = selected ? Palette.AccentHover : Palette.Text;
                    t.pressed = t.hover;
                    tabIcons[i].color = t.normal;
                }
            }
            if (tabTitle != null) tabTitle.text = TabNames[(int)tab];

            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            bindingLabels.Clear();

            switch (tab)
            {
                case Tab.Audio: BuildAudio(); break;
                case Tab.Video: BuildVideo(); break;
                case Tab.Controls: BuildControls(); break;
                case Tab.Keybinds: BuildKeybinds(); break;
            }

            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        private void Update()
        {
            // Slide the underline under the active tab.
            if (tabIndicator == null || tabButtons.Count == 0) return;
            var target = (RectTransform)tabButtons[(int)currentTab].transform;
            Vector3 local = tabIndicator.parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            float goalX = local.x;
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            Vector2 pos = tabIndicator.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, goalX, k);
            tabIndicator.anchoredPosition = pos;
            Vector2 size = tabIndicator.sizeDelta;
            size.x = Mathf.Lerp(size.x, 24f, k);
            tabIndicator.sizeDelta = size;
        }

        private static SettingsManager S => SettingsManager.Instance;
        private static SettingsData D => SettingsManager.Current;

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private void BuildAudio()
        {
            UIFactory.Header(theme, content, "Volume");
            UIFactory.SliderRow(theme, content, "Master", 0f, 1f, D.masterVolume, Percent, v => S.Modify(d => d.masterVolume = v));
            UIFactory.SliderRow(theme, content, "Effects", 0f, 1f, D.sfxVolume, Percent, v => S.Modify(d => d.sfxVolume = v));
            UIFactory.Header(theme, content, "Output");
            UIFactory.SwitchRow(theme, content, "Sound effects", D.sfxEnabled, on => S.Modify(d => d.sfxEnabled = on));
        }

        private void BuildVideo()
        {
            UIFactory.Header(theme, content, "Display");
            int fpsIndex = System.Array.IndexOf(FpsValues, D.fpsCap);
            if (fpsIndex < 0) fpsIndex = 2;
            UIFactory.CycleRow(theme, content, "Frame rate limit", FpsLabels, fpsIndex, i => S.Modify(d => d.fpsCap = FpsValues[i]));
            UIFactory.SwitchRow(theme, content, "V-Sync", D.vSync, on => S.Modify(d => d.vSync = on));
            UIFactory.CycleRow(theme, content, "Window mode", WindowLabels, (int)D.windowMode, i => S.Modify(d => d.windowMode = (WindowModeSetting)i));
            UIFactory.SwitchRow(theme, content, "Show FPS", D.showFps, on => S.Modify(d => d.showFps = on));
        }

        private void BuildControls()
        {
            UIFactory.Header(theme, content, "Cursor");
            // Converter: type in the 0-100 value from another MOBA client; the fine-tune slider follows it.
            Slider fine = null;
            Slider other = UIFactory.SliderRow(theme, content, "Sensitivity from other MOBA", 0f, 100f, D.otherGameSensitivity, v => v.ToString("0"), v =>
            {
                float converted = SensitivityConverter.FromOtherGame(v);
                S.Modify(d => { d.otherGameSensitivity = Mathf.RoundToInt(v); d.cursorSensitivity = converted; });
                if (fine != null) fine.value = converted;
            });
            other.wholeNumbers = true;
            fine = UIFactory.SliderRow(theme, content, "Fine-tune", 0.02f, 3.5f, D.cursorSensitivity, v => v.ToString("0.00") + "x", v => S.Modify(d => d.cursorSensitivity = v));
            UIFactory.CycleRow(theme, content, "Cursor style", CursorLabels, (int)D.cursorStyle, i => S.Modify(d => d.cursorStyle = (CursorStyle)i));
            UIFactory.SliderRow(theme, content, "Cursor size", 0.5f, 2f, D.cursorScale, v => v.ToString("0.00") + "x", v => S.Modify(d => d.cursorScale = v));

            UIFactory.Header(theme, content, "Camera");
            UIFactory.CycleRow(theme, content, "Camera on start", LockLabels, D.cameraLockedByDefault ? 1 : 0, i => S.Modify(d => d.cameraLockedByDefault = i == 1));
            UIFactory.SliderRow(theme, content, "Edge pan speed", 5f, 60f, D.edgePanSpeed, v => v.ToString("0"), v => S.Modify(d => d.edgePanSpeed = v));

            UIFactory.Header(theme, content, "Combat");
            if (InputManager.Instance != null)
            {
                bool onF = string.Equals(InputManager.Instance.BindingPath(GameAction.Blink), "<Keyboard>/f", System.StringComparison.OrdinalIgnoreCase);
                UIFactory.CycleRow(theme, content, "Blink key", BlinkKeyLabels, onF ? 1 : 0,
                    i => InputManager.Instance.ApplyBinding(GameAction.Blink, i == 1 ? "<Keyboard>/f" : "<Keyboard>/d"));
            }
            UIFactory.SwitchRow(theme, content, "Auto-attack when idle", D.autoAttackWhenIdle, on => S.Modify(d => d.autoAttackWhenIdle = on));
            UIFactory.SwitchRow(theme, content, "Range ring on attack-move", D.showRangeOnAttackMove, on => S.Modify(d => d.showRangeOnAttackMove = on));

            UIFactory.Header(theme, content, "Cast mode per key");
            GameAction[] castKeys = { GameAction.AbilityQ, GameAction.AbilityW, GameAction.AbilityE, GameAction.Blink };
            foreach (GameAction ga in castKeys)
            {
                int slot = GameActionInfo.CastSlot(ga);
                string key = InputManager.Instance != null ? InputManager.Instance.BindingShort(ga) : ga.ToString();
                UIFactory.CycleRow(theme, content, $"{GameActionInfo.DisplayName(ga)}  <color=#5E5E6B>{key}</color>", CastModeLabels, (int)D.GetCastMode(slot),
                    i => S.Modify(d => d.castModes[slot] = (CastMode)i));
            }
        }

        private void BuildKeybinds()
        {
            UIFactory.Header(theme, content, "Click a key, then press the new one  -  Esc cancels");
            InputManager input = InputManager.Instance;
            if (input == null) return;

            for (int i = 0; i < GameActionInfo.Count; i++)
            {
                var ga = (GameAction)i;
                if (ga == GameAction.AbilityR || ga == GameAction.Utility) continue; // not part of the current kit
                RectTransform row = UIFactory.Row(theme, content, GameActionInfo.DisplayName(ga), out _, 200f);
                Button b = UIFactory.Button(theme, row, "Bind", input.BindingDisplay(ga), ButtonStyle.Secondary, new Vector2(200f, 40f), theme.sizeSmall);
                UIFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(200f, 40f));
                TextMeshProUGUI label = UIFactory.Label(b);
                bindingLabels[ga] = label;

                if (!GameActionInfo.IsRebindable(ga))
                {
                    b.interactable = false;
                    continue;
                }

                b.onClick.AddListener(() =>
                {
                    if (input.IsRebinding) return;
                    UIFactory.SetLabelColors(b, Palette.Accent, Palette.Accent);
                    label.text = "Press a key";
                    input.StartRebind(ga, ok => RefreshBindingLabels());
                });
            }

            RectTransform resetRow = UIFactory.Rect("ResetRow", content);
            UIFactory.Layout(resetRow, 72f);
            Button reset = UIFactory.Button(theme, resetRow, "ResetBindings", "Reset to defaults", ButtonStyle.Danger, new Vector2(200f, 40f), theme.sizeSmall);
            UIFactory.Place((RectTransform)reset.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(200f, 40f));
            reset.onClick.AddListener(() => input.ResetAllBindings());
        }

        private void RefreshBindingLabels()
        {
            InputManager input = InputManager.Instance;
            if (input == null) return;
            foreach (KeyValuePair<GameAction, TextMeshProUGUI> kv in bindingLabels)
            {
                if (kv.Value == null) continue;
                kv.Value.text = input.BindingDisplay(kv.Key);
                Button b = kv.Value.GetComponentInParent<Button>();
                if (b != null) UIFactory.SetLabelColors(b, Palette.Text, Palette.Text);
            }
        }
    }
}
