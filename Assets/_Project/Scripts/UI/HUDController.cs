using System.Text;
using MobaTrainer.Core;
using MobaTrainer.Gameplay;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>
    /// In-run HUD: ability bar with cooldown sweeps and live key labels, mode title, hints,
    /// camera-lock state, optional FPS counter and a stats block. Text only updates on change.
    /// </summary>
    public class HUDController : UIPanel
    {
        [Header("Theme")]
        public UITheme theme;

        [Header("References")]
        public RectTransform abilityBar;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI hintText;
        public TextMeshProUGUI statsText;
        public TextMeshProUGUI cameraText;
        public Image cameraIcon;
        public TextMeshProUGUI fpsText;

        [Header("Countdown")]
        public TextMeshProUGUI centerText;

        [Header("Counters (above the ability bar)")]
        public RectTransform counterRow;
        public TextMeshProUGUI hitsText;
        public TextMeshProUGUI missesText;
        public TextMeshProUGUI timeText;

        [Header("Layout")]
        public float slotSize = 60f;

        private class SlotView
        {
            public GameAction action;
            public bool summoner;
            public Image frame;
            public Image cooldown;
            public TextMeshProUGUI key;
            public TextMeshProUGUI seconds;
            public int shownTenths = -1;
        }

        private PlayerAbilities abilities;
        private SlotView[] views;
        private MobaCameraController cameraController;
        private bool lastLocked;
        private float fpsTimer;
        private int fpsFrames;
        private float statsTimer;
        private System.Action<StringBuilder> statsProvider;
        private IRunCounters counters;
        private int shownHits = -1, shownMisses = -1, shownSeconds = -1;
        private readonly char[] timeChars = { '0', '0', ':', '0', '0' };
        private readonly StringBuilder sb = new StringBuilder(256);

        public void Bind(PlayerAbilities playerAbilities, MobaCameraController cam)
        {
            abilities = playerAbilities;
            cameraController = cam;
            BuildSlots();
            RefreshKeys();
            lastLocked = !cam.Locked;
        }

        /// <summary>Big centred text (countdown "3 2 1"); empty hides it.</summary>
        public void SetCenterText(string text)
        {
            if (centerText == null) return;
            centerText.text = text;
            centerText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Hits / misses / timer pills above the ability bar (null hides them).</summary>
        public void SetCounters(IRunCounters source)
        {
            counters = source;
            shownHits = shownMisses = shownSeconds = -1;
            if (counterRow != null) counterRow.gameObject.SetActive(source != null);
        }

        public void SetTitle(string title, string hint)
        {
            if (titleText != null)
            {
                titleText.text = title;
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            }
            if (hintText != null)
            {
                hintText.text = hint;
                hintText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            }
        }

        /// <summary>Optional stats block, refreshed 4× per second.</summary>
        public void SetStatsProvider(System.Action<StringBuilder> provider)
        {
            statsProvider = provider;
            if (statsText != null) statsText.gameObject.SetActive(provider != null);
        }

        private void OnEnable()
        {
            if (InputManager.Instance != null) InputManager.Instance.BindingsChanged += RefreshKeys;
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null) InputManager.Instance.BindingsChanged -= RefreshKeys;
        }

        private void BuildSlots()
        {
            if (views != null || abilities == null) return;
            views = new SlotView[abilities.slots.Length];
            for (int i = 0; i < views.Length; i++)
            {
                PlayerAbilities.Slot slot = abilities.slots[i];
                var v = new SlotView { action = slot.action, summoner = slot.action == GameAction.Blink };
                views[i] = v;
                if (slot.ability == null) continue; // empty slots are not shown

                if (v.summoner)
                {
                    RectTransform gap = UIFactory.Rect("Gap", abilityBar);
                    UIFactory.Layout(gap, slotSize, 10f);
                }

                RectTransform root = UIFactory.Rect("Slot_" + slot.action, abilityBar);
                UIFactory.Layout(root, slotSize, slotSize);

                Image bg = UIFactory.Rounded(theme, root, "Background", v.summoner ? Palette.AccentSoft : Palette.Surface, theme.radiusButton);
                UIFactory.Stretch(bg.rectTransform);
                v.frame = UIFactory.Outline(theme, root, IdleFrame(v), theme.radiusButton);

                // The bound key is the only label: Q / W / E / D or F.
                v.key = UIFactory.Text(theme, root, "Key", "", theme.sizeTitle, Palette.Text, TextAlignmentOptions.Center, TextWeight.Heading);
                UIFactory.Stretch(v.key.rectTransform);

                v.cooldown = UIFactory.Image(root, "Cooldown", Palette.Hex(0x000000, 0.7f), theme.rounded);
                UIFactory.Stretch(v.cooldown.rectTransform);
                v.cooldown.type = Image.Type.Filled;
                v.cooldown.fillMethod = Image.FillMethod.Radial360;
                v.cooldown.fillOrigin = (int)Image.Origin360.Top;
                v.cooldown.fillClockwise = false;
                v.cooldown.fillAmount = 0f;

                v.seconds = UIFactory.Text(theme, root, "Seconds", "", theme.sizeHeading, Palette.Text, TextAlignmentOptions.Center, TextWeight.Semibold);
                UIFactory.Stretch(v.seconds.rectTransform);
            }
        }

        private static Color IdleFrame(SlotView v) => v.summoner ? Palette.Accent.WithAlpha(0.45f) : Palette.Border;

        private void RefreshKeys()
        {
            if (views == null || InputManager.Instance == null) return;
            foreach (SlotView v in views)
            {
                if (v.key != null) v.key.text = InputManager.Instance.BindingShort(v.action).ToUpperInvariant();
            }
        }

        private void Update()
        {
            UpdateSlots();
            UpdateCamera();
            UpdateFps();
            UpdateStats();
            UpdateCounters();
        }

        private void UpdateCounters()
        {
            if (counters == null) return;
            if (hitsText != null && counters.Hits != shownHits) { shownHits = counters.Hits; hitsText.SetText("{0}", shownHits); }
            if (missesText != null && counters.Misses != shownMisses) { shownMisses = counters.Misses; missesText.SetText("{0}", shownMisses); }
            int secs = Mathf.FloorToInt(counters.Elapsed);
            if (timeText != null && secs != shownSeconds)
            {
                shownSeconds = secs;
                int m = Mathf.Min(99, secs / 60), sec = secs % 60;
                timeChars[0] = (char)('0' + m / 10); timeChars[1] = (char)('0' + m % 10);
                timeChars[3] = (char)('0' + sec / 10); timeChars[4] = (char)('0' + sec % 10);
                timeText.SetCharArray(timeChars);
            }
        }

        private void UpdateSlots()
        {
            if (views == null || abilities == null) return;
            for (int i = 0; i < views.Length; i++)
            {
                SlotView v = views[i];
                if (v.key == null) continue;
                float frac = abilities.CooldownFraction(i);
                v.cooldown.fillAmount = frac;
                int tenths = frac > 0f ? Mathf.CeilToInt(abilities.CooldownRemaining(i) * 10f) : 0;
                if (tenths != v.shownTenths)
                {
                    v.shownTenths = tenths;
                    v.key.color = tenths > 0 ? Palette.TextFaint : Palette.Text;
                    v.key.alpha = tenths > 0 ? 0f : 1f;
                    if (tenths <= 0) v.seconds.SetText(string.Empty);
                    else if (tenths < 10) v.seconds.SetText("{0:1}", tenths / 10f);
                    else v.seconds.SetText("{0:0}", Mathf.Ceil(tenths / 10f));
                }
                bool aiming = abilities.AimingSlot == i;
                v.frame.color = aiming || (abilities.IsCasting && abilities.CastingSlot == i) ? Palette.AccentHover : IdleFrame(v);
            }
        }

        private void UpdateCamera()
        {
            if (cameraController == null) return;
            if (cameraController.Locked == lastLocked) return;
            lastLocked = cameraController.Locked;
            if (cameraIcon != null && theme != null)
            {
                cameraIcon.sprite = theme.Icon(lastLocked ? UIIcon.Lock : UIIcon.Unlock);
                cameraIcon.color = lastLocked ? Palette.TextFaint : Palette.Accent;
            }
            if (cameraText != null)
            {
                string key = InputManager.Instance != null ? InputManager.Instance.BindingShort(GameAction.CameraLock) : "Y";
                cameraText.text = key;
            }
        }

        private void UpdateFps()
        {
            if (fpsText == null) return;
            bool show = SettingsManager.Current.showFps;
            if (fpsText.gameObject.activeSelf != show) fpsText.gameObject.SetActive(show);
            if (!show) return;
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fpsText.SetText("{0:0} fps", fpsFrames / fpsTimer);
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }

        private void UpdateStats()
        {
            if (statsProvider == null || statsText == null) return;
            statsTimer -= Time.unscaledDeltaTime;
            if (statsTimer > 0f) return;
            statsTimer = 0.25f;
            statsProvider(sb);
            statsText.SetText(sb);
        }
    }
}
