using System;
using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>
    /// Mode select. Main modes (Mixed, Minimap, Extreme Survivor) hang as tall cloth banners with a
    /// medallion icon; the practice modes (Practice Tool, Skillshot, Dodge) are small chamfered plates
    /// underneath. Levels are a row of diamonds: red = selected, white = unlocked, outline = locked.
    /// Pick a level by clicking a diamond, the ‹ › arrows, or the mouse wheel over the card.
    /// </summary>
    public class DrillsPanel : UIPanel
    {
        private class Drill
        {
            public string name;
            public UIIcon icon;
            public bool available;
            public bool featured;
            public Action onSelect;
            public string levelId;                      // drills with levels (null = none)
            public readonly List<Image> pips = new List<Image>();
            public RectTransform body;                  // moves up on hover
            public Image trim, ring;
            public bool hovered;
            public float t;
        }

        [Header("Theme")]
        public UITheme theme;

        [Header("References")]
        public RectTransform grid;
        public Button backButton;

        [Header("Cards")]
        public Vector2 cardSize = new Vector2(250f, 366f);       // banner
        public Vector2 smallCardSize = new Vector2(236f, 66f);   // plate
        public float gap = 46f;

        public event Action BackRequested;

        private readonly List<Drill> drills = new List<Drill>();
        private bool built;

        private static readonly Color BannerCloth = Palette.Hex(0x1C1517);
        private static readonly Color Rod = Palette.Hex(0x5A4A46);
        private static readonly Color Gold = Palette.Highlight;

        public void AddDrill(string name, UIIcon icon, bool available, Action onSelect, string levelId = null, bool featured = false)
        {
            drills.Add(new Drill { name = name, icon = icon, available = available, onSelect = onSelect, levelId = levelId, featured = featured });
            built = false;
        }

        /// <summary>Updates the level diamonds (after a run unlocked a level).</summary>
        public void RefreshLevels()
        {
            foreach (Drill d in drills) UpdateLevel(d);
        }

        private void UpdateLevel(Drill d)
        {
            if (d.levelId == null || d.pips.Count == 0) return;
            int lv = DrillProgress.Level(d.levelId);
            int open = DrillProgress.Unlocked(d.levelId);
            for (int i = 0; i < d.pips.Count; i++)
            {
                int level = i + 1;
                Image p = d.pips[i];
                bool unlocked = level <= open;
                p.sprite = unlocked ? Diamond : DiamondRing;
                p.color = level == lv ? Palette.Accent : unlocked ? Palette.Text.WithAlpha(0.8f) : Palette.Text.WithAlpha(0.22f);
                p.rectTransform.localScale = Vector3.one * (level == lv ? 1.35f : 1f);
            }
        }

        private Sprite Diamond => theme.diamond != null ? theme.diamond : theme.white;
        private Sprite DiamondRing => theme.diamondRing != null ? theme.diamondRing : theme.white;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        private bool endlessOn;
        private Action<bool> endlessChanged;
        private RectTransform endlessRoot;

        /// <summary>"Endless" switch above the banners: the same drills without a time limit.</summary>
        public void SetEndlessToggle(bool on, Action<bool> changed)
        {
            endlessOn = on;
            endlessChanged = changed;
        }

        protected override void OnShown()
        {
            if (built)
            {
                RefreshLevels();
                return;
            }
            built = true;
            var gl = grid.GetComponent<GridLayoutGroup>();
            if (gl != null) gl.enabled = false;                   // cards are placed by hand
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

            var featured = drills.FindAll(d => d.featured);
            var practice = drills.FindAll(d => !d.featured);
            const float bannerY = 80f;
            float plateY = bannerY - cardSize.y * 0.5f - 70f;
            float fw = featured.Count * cardSize.x + (featured.Count - 1) * gap;
            for (int i = 0; i < featured.Count; i++)
                BuildBanner(featured[i], new Vector2(-fw * 0.5f + cardSize.x * 0.5f + i * (cardSize.x + gap), bannerY));
            float pg = 18f;
            float pw = practice.Count * smallCardSize.x + (practice.Count - 1) * pg;
            for (int i = 0; i < practice.Count; i++)
                BuildPlate(practice[i], new Vector2(-pw * 0.5f + smallCardSize.x * 0.5f + i * (smallCardSize.x + pg), plateY));
            BuildEndlessToggle(fw, bannerY);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (Drill d in drills)
            {
                if (d.body == null) continue;
                float target = d.hovered && d.available ? 1f : 0f;
                if (Mathf.Approximately(d.t, target)) continue;
                d.t = Mathf.MoveTowards(d.t, target, dt / 0.14f);
                float e = d.t * d.t * (3f - 2f * d.t);
                d.body.anchoredPosition = new Vector2(0f, e * (d.featured ? 10f : 3f));
                if (d.trim != null) d.trim.color = d.featured ? Palette.Accent.WithAlpha(Mathf.Lerp(0.55f, 1f, e)) : Color.Lerp(Palette.BorderStrong, Palette.Accent, e);
                if (d.ring != null) d.ring.color = Gold.WithAlpha(Mathf.Lerp(0.7f, 1f, e));
            }
        }

        private void BuildEndlessToggle(float rowWidth, float bannerY)
        {
            if (endlessChanged == null || endlessRoot != null) return;
            endlessRoot = UIFactory.Rect("Endless", grid);
            UIFactory.Place(endlessRoot, new Vector2(0.5f, 0.5f), new Vector2(1f, 0f), new Vector2(rowWidth * 0.5f + 10f, bannerY + cardSize.y * 0.5f + 22f), new Vector2(360f, 28f));
            TextMeshProUGUI label = UIFactory.Text(theme, endlessRoot, "Label", "ENDLESS", theme.sizeCaption, Palette.TextMuted, TextAlignmentOptions.Right, TextWeight.Heading);
            label.characterSpacing = 8f;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = new Vector2(-56f, 0f);
            UISwitch sw = UIFactory.Switch(theme, endlessRoot, "Switch", endlessOn);
            UIFactory.Place((RectTransform)sw.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(44f, 24f));
            sw.Changed += v => { endlessOn = v; endlessChanged?.Invoke(v); };
        }

        // ------------------------------------------------------------------ shared pieces

        private Button MakeHit(Drill d, string name, Vector2 pos, Vector2 size)
        {
            RectTransform root = UIFactory.Rect(name, grid);
            UIFactory.Place(root, UIFactory.Center, UIFactory.Center, pos, size);
            Image hit = UIFactory.Image(root, "Hit", new Color(0f, 0f, 0f, 0f), theme.white, true);
            UIFactory.Stretch(hit.rectTransform);
            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<UIButtonSounds>();
            var notify = root.gameObject.AddComponent<UIHoverNotify>();
            notify.Entered += () => d.hovered = true;
            notify.Exited += () => d.hovered = false;
            Drill captured = d;
            b.onClick.AddListener(() => { if (captured.available) captured.onSelect?.Invoke(); });
            b.interactable = d.available;

            d.body = UIFactory.Rect("Body", root);
            UIFactory.Stretch(d.body);
            if (d.levelId != null && d.available)
                root.gameObject.AddComponent<UIScrollNotify>().Scrolled += dir => StepLevel(d, dir);
            return b;
        }

        private Image Layer(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool sliced = false)
        {
            Image img = UIFactory.Image(parent, name, color, sprite != null ? sprite : theme.white);
            UIFactory.Place(img.rectTransform, UIFactory.Center, UIFactory.Center, pos, size);
            if (sliced && sprite != null)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1.5f;
            }
            return img;
        }

        /// <summary>Row of clickable diamonds, one per level.</summary>
        private void BuildDiamonds(Drill d, Transform parent, Vector2 center, float size, float spacing)
        {
            int count = DrillProgress.MaxLevel;
            for (int i = 0; i < count; i++)
            {
                int level = i + 1;
                RectTransform hit = UIFactory.Rect("Level" + level, parent);
                float x = center.x - (count - 1) * 0.5f * spacing + i * spacing;
                UIFactory.Place(hit, UIFactory.Center, UIFactory.Center, new Vector2(x, center.y), new Vector2(spacing, size + 18f));
                Image hitImg = UIFactory.Image(hit, "Hit", new Color(0f, 0f, 0f, 0f), theme.white, true);
                UIFactory.Stretch(hitImg.rectTransform);
                Image p = UIFactory.Image(hit, "Diamond", Palette.Text, Diamond);
                UIFactory.Place(p.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size, size));
                d.pips.Add(p);
                var btn = hit.gameObject.AddComponent<Button>();
                btn.targetGraphic = hitImg;
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                hit.gameObject.AddComponent<UIButtonSounds>();
                var hover = hit.gameObject.AddComponent<UIHoverNotify>();
                hover.Entered += () => { if (level <= DrillProgress.Unlocked(d.levelId)) p.rectTransform.localScale = Vector3.one * 1.5f; };
                hover.Exited += () => UpdateLevel(d);
                btn.onClick.AddListener(() =>
                {
                    if (level > DrillProgress.Unlocked(d.levelId)) return;
                    DrillProgress.SetLevel(d.levelId, level);
                    UpdateLevel(d);
                });
            }
            UpdateLevel(d);
        }

        /// <summary>Moves the selected level by <paramref name="dir"/> within the unlocked range.</summary>
        private void StepLevel(Drill d, int dir)
        {
            int lv = Mathf.Clamp(DrillProgress.Level(d.levelId) + dir, 1, DrillProgress.Unlocked(d.levelId));
            DrillProgress.SetLevel(d.levelId, lv);
            UpdateLevel(d);
        }

        /// <summary>Small "‹" / "›" arrow that steps the level.</summary>
        private void Arrow(Drill d, Transform parent, string glyph, int dir, Vector2 pos, float size)
        {
            RectTransform r = UIFactory.Rect(dir < 0 ? "Prev" : "Next", parent);
            UIFactory.Place(r, UIFactory.Center, UIFactory.Center, pos, new Vector2(size + 14f, size + 14f));
            Image hit = UIFactory.Image(r, "Hit", new Color(0f, 0f, 0f, 0f), theme.white, true);
            UIFactory.Stretch(hit.rectTransform);
            TextMeshProUGUI t = UIFactory.Text(theme, r, "Glyph", glyph, size, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Heading);
            MobaTrainer.Localization.Localizer.Exclude(t);
            UIFactory.Stretch(t.rectTransform);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            r.gameObject.AddComponent<UIButtonSounds>();
            var n = r.gameObject.AddComponent<UIHoverNotify>();
            n.Entered += () => t.color = Palette.Text;
            n.Exited += () => t.color = Palette.TextMuted;
            b.onClick.AddListener(() => StepLevel(d, dir));
        }

        // ------------------------------------------------------------------ banner (main modes)

        private void BuildBanner(Drill d, Vector2 pos)
        {
            Vector2 size = cardSize;
            MakeHit(d, "Drill_" + d.name, pos, size);
            Transform body = d.body;
            float dim = d.available ? 1f : 0.45f;

            Layer(body, "Rod", theme.bannerRod, Rod.WithAlpha(dim), new Vector2(0f, size.y * 0.5f + 2f), new Vector2(size.x + 44f, 20f));
            Layer(body, "Cloth", theme.bannerFill, BannerCloth.WithAlpha(0.97f), Vector2.zero, size);
            d.trim = Layer(body, "Trim", theme.bannerTrim, Palette.Accent.WithAlpha(0.55f * dim), Vector2.zero, size);

            // Medallion with the mode icon.
            Vector2 med = new Vector2(0f, 62f);
            Layer(body, "MedallionFill", theme.medallionFill, Palette.Hex(0x0C0A0B), med, new Vector2(124f, 124f));
            d.ring = Layer(body, "MedallionRing", theme.medallionRing, Gold.WithAlpha(0.7f * dim), med, new Vector2(124f, 124f));
            Image icon = UIFactory.Icon(theme, body, "Icon", d.icon, 52f, d.available ? Palette.Text : Palette.TextFaint);
            UIFactory.Place(icon.rectTransform, UIFactory.Center, UIFactory.Center, med, new Vector2(52f, 52f));

            TextMeshProUGUI name = UIFactory.Text(theme, body, "Name", d.name.ToUpperInvariant(), 22f, d.available ? Palette.Text : Palette.TextFaint,
                TextAlignmentOptions.Center, TextWeight.Heading);
            name.characterSpacing = 6f;
            name.enableAutoSizing = true;
            name.fontSizeMax = 22f;
            name.fontSizeMin = 12f;
            UIFactory.Place(name.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -30f), new Vector2(size.x - 44f, 34f));

            if (!d.available)
            {
                Image lockIcon = UIFactory.Icon(theme, body, "Lock", UIIcon.Lock, 16f, Palette.TextFaint);
                UIFactory.Place(lockIcon.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -78f), new Vector2(16f, 16f));
            }
            else if (d.levelId != null)
            {
                BuildDiamonds(d, body, new Vector2(0f, -82f), 13f, 22f);
                Arrow(d, body, "‹", -1, new Vector2(-size.x * 0.5f + 22f, -82f), 26f);
                Arrow(d, body, "›", 1, new Vector2(size.x * 0.5f - 22f, -82f), 26f);
            }
        }

        // ------------------------------------------------------------------ plate (practice modes)

        private void BuildPlate(Drill d, Vector2 pos)
        {
            Vector2 size = smallCardSize;
            MakeHit(d, "Drill_" + d.name, pos, size);
            Transform body = d.body;
            Layer(body, "Plate", theme.plaqueFill, Palette.Hex(0x141012, 0.92f), Vector2.zero, size, true);
            d.trim = Layer(body, "Trim", theme.plaqueTrim, Palette.BorderStrong, Vector2.zero, size, true);

            Color text = d.available ? Palette.TextMuted : Palette.TextFaint;
            Image icon = UIFactory.Icon(theme, body, "Icon", d.icon, 20f, text);
            UIFactory.Place(icon.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(-size.x * 0.5f + 34f, 0f), new Vector2(20f, 20f));

            bool levels = d.levelId != null && d.available;
            TextMeshProUGUI name = UIFactory.Text(theme, body, "Name", d.name.ToUpperInvariant(), theme.sizeCaption + 1f, text, TextAlignmentOptions.Left, TextWeight.Heading);
            name.characterSpacing = 5f;
            name.enableAutoSizing = true;
            name.fontSizeMax = theme.sizeCaption + 1f;
            name.fontSizeMin = 10f;
            UIFactory.Place(name.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(22f, levels ? 9f : 0f), new Vector2(size.x - 100f, 22f));
            if (levels) BuildDiamonds(d, body, new Vector2(-size.x * 0.5f + 62f + 3.5f * 13f, -12f), 7f, 13f);
        }
    }
}
