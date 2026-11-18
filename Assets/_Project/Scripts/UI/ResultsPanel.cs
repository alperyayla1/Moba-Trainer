using System;
using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Localization;
using MobaTrainer.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>
    /// End-of-run card on a chamfered plate. Top: mode name and the played level as diamonds.
    /// Middle: the big rating (or score), a bar with the 75% unlock mark and one short status line.
    /// Bottom: score / best (or kills / time / accuracy) as label-over-value stats, then three equal
    /// buttons. Built at runtime from the theme's ornate sprites.
    /// </summary>
    public class ResultsPanel : UIPanel
    {
        [Header("Theme")]
        public UITheme theme;

        public event Action RetryRequested;
        public event Action StatsRequested;
        public event Action MenuRequested;

        private const float PanelW = 660f, PanelH = 600f, BarW = 400f;

        private bool built;
        private TextMeshProUGUI modeText, bigText, labelText, statusText;
        private readonly List<Image> levelPips = new List<Image>();
        private RectTransform barRoot;
        private Image barFill, barMark;
        private readonly List<(TextMeshProUGUI label, TextMeshProUGUI value, RectTransform root)> stats =
            new List<(TextMeshProUGUI, TextMeshProUGUI, RectTransform)>();
        private readonly List<Image> statDividers = new List<Image>();

        public void ShowResult(string title, RunRecord r, int previousBest) => ShowResult(title, r, previousBest, false, 0);

        public void ShowResult(string title, RunRecord r, int previousBest, bool endless, int unlockedLevel)
        {
            Build();
            bool leveled = DrillProgress.HasLevels(r.mode);
            bool newBest = r.score > previousBest && !endless;

            string status = r.mode == "duel" || r.mode == "extreme" ? "  ·  " + Loc.T(r.won ? "SURVIVED" : "DOWN") : string.Empty;
            modeText.text = Loc.T(title).ToUpper(Loc.Culture) + status;

            // Played level as diamonds (no "LEVEL 4" text).
            for (int i = 0; i < levelPips.Count; i++)
            {
                int level = i + 1;
                bool show = leveled && r.difficulty > 0;
                levelPips[i].gameObject.SetActive(show);
                if (!show) continue;
                bool unlocked = level <= DrillProgress.Unlocked(r.mode);
                levelPips[i].sprite = unlocked ? Diamond : DiamondRing;
                levelPips[i].color = level == r.difficulty ? Palette.Accent : unlocked ? Palette.Text.WithAlpha(0.7f) : Palette.Text.WithAlpha(0.2f);
                levelPips[i].rectTransform.localScale = Vector3.one * (level == r.difficulty ? 1.35f : 1f);
            }

            var rows = new List<(string, string, Color)>();
            if (leveled)
            {
                float rating = Mathf.Clamp01(r.Rating());
                int pct = Mathf.RoundToInt(rating * 100f);
                Color c = pct >= 100 ? Palette.Highlight : rating >= DrillProgress.UnlockRating ? Palette.Success : Palette.Text;
                bigText.text = pct + "<size=55%>%</size>";
                bigText.color = c;
                labelText.text = Loc.T(RunRecord.RatingLabel(r.mode)).ToUpper(Loc.Culture);

                barRoot.gameObject.SetActive(true);
                barFill.rectTransform.sizeDelta = new Vector2(BarW * rating, 6f);
                barFill.color = c;
                barMark.rectTransform.anchoredPosition = new Vector2(BarW * DrillProgress.UnlockRating - BarW * 0.5f, 0f);

                if (endless) SetStatus(Loc.T("Endless run (no level progress)"), Palette.TextMuted);
                else if (unlockedLevel > 0) SetStatus(Loc.F("LEVEL {0} UNLOCKED", unlockedLevel), Palette.Highlight);
                else if (r.difficulty >= DrillProgress.MaxLevel) SetStatus(Loc.T("Max level"), Palette.TextMuted);
                else if (r.difficulty < DrillProgress.Unlocked(r.mode)) SetStatus(Loc.F("Level {0} is already unlocked", r.difficulty + 1), Palette.TextMuted);
                else SetStatus(Loc.F("Get 75% to unlock level {0}", r.difficulty + 1), Palette.TextMuted);

                rows.Add((Loc.T("Score"), r.score.ToString("N0"), Palette.Text));
            }
            else
            {
                bigText.text = r.score.ToString("N0");
                bigText.color = Palette.Text;
                labelText.text = Loc.T("SCORE").ToUpper(Loc.Culture);
                barRoot.gameObject.SetActive(false);
                SetStatus(string.Empty, Palette.TextMuted);
                if (r.mode == "extreme")
                {
                    int secs = Mathf.FloorToInt(r.duration);
                    rows.Add((Loc.T("Kills"), r.kills.ToString(), Palette.Text));
                    rows.Add((Loc.T("Time"), $"{secs / 60:00}:{secs % 60:00}", Palette.Text));
                    rows.Add((Loc.T("Accuracy"), Mathf.RoundToInt(r.Accuracy * 100f) + "%", Palette.Text));
                }
            }
            if (newBest) rows.Add((Loc.T("Best"), Loc.T("NEW BEST"), Palette.Highlight));
            else rows.Add((Loc.T("Best"), Mathf.Max(previousBest, r.score).ToString("N0"), Palette.Text));
            LayoutStats(rows);
            Show();
        }

        private void SetStatus(string text, Color color)
        {
            statusText.text = text;
            statusText.color = color;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private Sprite Diamond => theme != null && theme.diamond != null ? theme.diamond : theme.white;
        private Sprite DiamondRing => theme != null && theme.diamondRing != null ? theme.diamondRing : theme.white;

        // ------------------------------------------------------------------ layout

        private void LayoutStats(List<(string label, string value, Color color)> rows)
        {
            while (stats.Count < rows.Count) stats.Add(NewStat(stats.Count));
            float cell = 150f;
            float start = -(rows.Count - 1) * 0.5f * cell;
            for (int i = 0; i < stats.Count; i++)
            {
                bool on = i < rows.Count;
                stats[i].root.gameObject.SetActive(on);
                if (!on) continue;
                stats[i].root.anchoredPosition = new Vector2(start + i * cell, -118f);
                stats[i].label.text = rows[i].label.ToUpper(Loc.Culture);
                stats[i].value.text = rows[i].value;
                stats[i].value.color = rows[i].color;
            }
            for (int i = 0; i < statDividers.Count; i++)
            {
                bool on = i < rows.Count - 1;
                statDividers[i].gameObject.SetActive(on);
                if (on) statDividers[i].rectTransform.anchoredPosition = new Vector2(start + (i + 0.5f) * cell, -118f);
            }
            while (statDividers.Count < rows.Count - 1)
            {
                int i = statDividers.Count;
                Image div = UIFactory.Image(plate, "Divider", Palette.Border, theme.white);
                UIFactory.Place(div.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(start + (i + 0.5f) * cell, -118f), new Vector2(1f, 44f));
                statDividers.Add(div);
            }
        }

        private (TextMeshProUGUI, TextMeshProUGUI, RectTransform) NewStat(int index)
        {
            RectTransform root = UIFactory.Rect("Stat" + index, plate);
            UIFactory.Place(root, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(150f, 64f));
            TextMeshProUGUI label = UIFactory.Text(theme, root, "Label", "", 12f, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Heading);
            label.characterSpacing = 8f;
            UIFactory.Place(label.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 18f), new Vector2(150f, 20f));
            TextMeshProUGUI value = UIFactory.Text(theme, root, "Value", "", 30f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Semibold);
            value.enableAutoSizing = true;
            value.fontSizeMax = 30f;
            value.fontSizeMin = 16f;
            MobaTrainer.Localization.Localizer.Exclude(value);
            UIFactory.Place(value.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -10f), new Vector2(146f, 38f));
            return (label, value, root);
        }

        private RectTransform plate;

        private Image Layer(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool sliced)
        {
            Image img = UIFactory.Image(parent, name, color, sprite != null ? sprite : theme.white);
            UIFactory.Place(img.rectTransform, UIFactory.Center, UIFactory.Center, pos, size);
            if (sliced && sprite != null)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
            }
            return img;
        }

        private void Build()
        {
            if (built) return;
            built = true;
            Transform t = transform;

            plate = UIFactory.Rect("Plate", t);
            UIFactory.Place(plate, UIFactory.Center, UIFactory.Center, new Vector2(0f, 10f), new Vector2(PanelW, PanelH));
            Layer(plate, "Fill", theme.plaqueFill, Palette.Hex(0x0E0B0C, 0.94f), Vector2.zero, new Vector2(PanelW, PanelH), true);
            Layer(plate, "Trim", theme.plaqueTrim, Palette.Accent.WithAlpha(0.45f), Vector2.zero, new Vector2(PanelW - 16f, PanelH - 16f), true);

            modeText = UIFactory.Text(theme, plate, "Mode", "", 18f, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Heading);
            modeText.characterSpacing = 12f;
            modeText.enableAutoSizing = true;
            modeText.fontSizeMax = 18f;
            modeText.fontSizeMin = 11f;
            UIFactory.Place(modeText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 236f), new Vector2(PanelW - 80f, 28f));

            int count = DrillProgress.MaxLevel;
            for (int i = 0; i < count; i++)
            {
                Image p = UIFactory.Image(plate, "Level" + (i + 1), Palette.Text, Diamond);
                UIFactory.Place(p.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(-(count - 1) * 0.5f * 20f + i * 20f, 206f), new Vector2(10f, 10f));
                levelPips.Add(p);
            }

            bigText = UIFactory.Text(theme, plate, "Big", "0", 124f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Display);
            bigText.enableAutoSizing = true;
            bigText.fontSizeMax = 124f;
            bigText.fontSizeMin = 48f;
            MobaTrainer.Localization.Localizer.Exclude(bigText);
            UIFactory.Place(bigText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 120f), new Vector2(PanelW - 60f, 140f));

            labelText = UIFactory.Text(theme, plate, "Label", "", 13f, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Heading);
            labelText.characterSpacing = 10f;
            UIFactory.Place(labelText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 44f), new Vector2(PanelW - 80f, 22f));

            // Rating bar with the unlock mark.
            barRoot = UIFactory.Rect("Bar", plate);
            UIFactory.Place(barRoot, UIFactory.Center, UIFactory.Center, new Vector2(0f, 18f), new Vector2(BarW, 20f));
            Image track = UIFactory.Image(barRoot, "Track", Palette.Border, theme.white);
            UIFactory.Place(track.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(BarW, 6f));
            barFill = UIFactory.Image(barRoot, "Fill", Palette.Text, theme.white);
            UIFactory.Place(barFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 6f));
            barMark = UIFactory.Image(barRoot, "Mark", Palette.Highlight, Diamond);
            UIFactory.Place(barMark.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(12f, 12f));

            statusText = UIFactory.Text(theme, plate, "Status", "", 16f, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Medium);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMax = 16f;
            statusText.fontSizeMin = 11f;
            UIFactory.Place(statusText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -22f), new Vector2(PanelW - 80f, 24f));

            Image sep = UIFactory.Image(plate, "Separator", Palette.Border, theme.white);
            UIFactory.Place(sep.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -62f), new Vector2(PanelW - 140f, 1f));

            // Buttons: three equal chamfered plates.
            const float bw = 176f, bh = 50f, gap = 16f;
            float x0 = -(bw + gap);
            MakeButton("Retry", UIIcon.Restart, "RETRY", new Vector2(x0, -226f), new Vector2(bw, bh), true).onClick.AddListener(() => RetryRequested?.Invoke());
            MakeButton("Stats", UIIcon.Radar, "STATS", new Vector2(0f, -226f), new Vector2(bw, bh), false).onClick.AddListener(() => StatsRequested?.Invoke());
            MakeButton("Menu", UIIcon.Home, "MENU", new Vector2(-x0, -226f), new Vector2(bw, bh), false).onClick.AddListener(() => MenuRequested?.Invoke());
        }

        private Button MakeButton(string name, UIIcon icon, string label, Vector2 pos, Vector2 size, bool primary)
        {
            RectTransform root = UIFactory.Rect(name, plate);
            UIFactory.Place(root, UIFactory.Center, UIFactory.Center, pos, size);
            Image fill = Layer(root, "Fill", theme.plaqueFill, Color.white, Vector2.zero, size, true);
            fill.raycastTarget = true;
            Image trim = Layer(root, "Trim", theme.plaqueTrim, primary ? Palette.AccentHover.WithAlpha(0.6f) : Palette.BorderStrong, Vector2.zero, size, true);
            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            ColorBlock cb = b.colors;
            cb.normalColor = primary ? Palette.Accent : Palette.Surface;
            cb.highlightedColor = primary ? Palette.AccentHover : Palette.SurfaceHover;
            cb.pressedColor = primary ? Palette.AccentPressed : Palette.SurfacePressed;
            cb.selectedColor = cb.normalColor;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<UIButtonSounds>();
            root.gameObject.AddComponent<UIHoverScale>().hoverScale = 1.03f;

            Color fg = primary ? Palette.Text : Palette.TextMuted;
            // Icon + label centred as a group (layout measures the real, localized label width).
            RectTransform content = UIFactory.Rect("Content", root);
            UIFactory.Stretch(content);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 10f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            Image ic = UIFactory.Icon(theme, content, "Icon", icon, 16f, fg);
            UIFactory.Layout(ic, 16f, 16f);
            TextMeshProUGUI lt = UIFactory.Text(theme, content, "Label", label, 17f, fg, TextAlignmentOptions.Center, TextWeight.Heading);
            lt.characterSpacing = 8f;
            if (!primary)
            {
                var notify = root.gameObject.AddComponent<UIHoverNotify>();
                notify.Entered += () => { lt.color = Palette.Text; ic.color = Palette.Text; trim.color = Palette.Accent.WithAlpha(0.7f); };
                notify.Exited += () => { lt.color = fg; ic.color = fg; trim.color = Palette.BorderStrong; };
            }
            return b;
        }
    }
}
