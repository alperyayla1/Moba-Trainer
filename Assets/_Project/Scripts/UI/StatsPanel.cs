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
    /// Progress page: pick a drill, see runs / best / recent average / key rate, a bar chart of the
    /// last 20 scores (best highlighted) and the latest runs. Reads <see cref="StatsStore"/>.
    /// </summary>
    public class StatsPanel : UIPanel
    {
        [Serializable]
        public class Mode
        {
            public string id;
            public string title;
            public UIIcon icon;
        }

        [Header("Theme")]
        public UITheme theme;

        [Header("References")]
        public RectTransform tabRow;
        public RectTransform tiles;
        public RectTransform chart;
        public RectTransform list;
        public TextMeshProUGUI totalsText;
        public TextMeshProUGUI chartCaption;
        public Button backButton;

        public Mode[] modes =
        {
            new Mode { id = "skillshot", title = "Skillshot", icon = UIIcon.Skillshot },
            new Mode { id = "dodge", title = "Dodge", icon = UIIcon.Dodge },
            new Mode { id = "duel", title = "Mixed", icon = UIIcon.Duel },
            new Mode { id = "minimap", title = "Minimap", icon = UIIcon.Radar },
            new Mode { id = "extreme", title = "Extreme Survivor", icon = UIIcon.Power },
        };

        public event Action BackRequested;

        private int current;
        private readonly List<Button> tabs = new List<Button>();

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        public void ShowMode(string id)
        {
            for (int i = 0; i < modes.Length; i++) if (modes[i].id == id) current = i;
            Show();
        }

        protected override void OnShown()
        {
            if (tabs.Count == 0) BuildTabs();
            Refresh();
        }

        private void BuildTabs()
        {
            for (int i = 0; i < modes.Length; i++)
            {
                int index = i;
                Button b = UIFactory.IconLabelButton(theme, tabRow, "Tab_" + modes[i].id, modes[i].icon, modes[i].title.ToUpperInvariant(), ButtonStyle.Ghost, new Vector2(200f, 44f));
                TextMeshProUGUI lt = UIFactory.Label(b);
                lt.font = theme.Font(TextWeight.Heading);
                lt.characterSpacing = 6f;
                UIFactory.FitText(lt, 11f);
                UIFactory.Layout(b, 44f, 200f);
                b.onClick.AddListener(() => { current = index; Refresh(); });
                tabs.Add(b);
            }
        }

        private void Refresh()
        {
            Mode mode = modes[current];
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == current;
                UIFactory.SetLabelColors(tabs[i], on ? Palette.Accent : Palette.TextMuted, on ? Palette.AccentHover : Palette.Text);
            }

            List<RunRecord> runs = StatsStore.Runs(mode.id);
            Clear(tiles);
            Clear(chart);
            Clear(list);

            int best = 0;
            float avg = 0f;
            int recent = Mathf.Min(10, runs.Count);
            foreach (RunRecord r in runs) best = Mathf.Max(best, r.score);
            for (int i = 0; i < recent; i++) avg += runs[i].score;
            if (recent > 0) avg /= recent;

            Tile("RUNS", runs.Count.ToString());
            Tile("BEST", runs.Count > 0 ? best.ToString("N0") : "-");
            Tile("AVG LAST 10", recent > 0 ? Mathf.RoundToInt(avg).ToString("N0") : "-");
            Tile(RunRecord.KeyRateLabel(mode.id), recent > 0 ? KeyValue(runs, recent) : "-");

            // Chart: last 20, oldest on the left.
            int n = Mathf.Min(20, runs.Count);
            chartCaption.text = n > 0 ? Loc.F("LAST {0} RUNS", n) : "NO RUNS YET  -  PLAY THIS DRILL TO START TRACKING";
            int max = 1;
            for (int i = 0; i < n; i++) max = Mathf.Max(max, runs[i].score);
            float h = chart.rect.height > 10f ? chart.rect.height : 200f;
            for (int i = n - 1; i >= 0; i--)
            {
                RunRecord r = runs[i];
                bool isBest = r.score == best && best > 0;
                Image bar = UIFactory.Rounded(theme, chart, "Bar", isBest ? Palette.Accent : (i == 0 ? Palette.Text.WithAlpha(0.85f) : Palette.TextFaint), 4f, true);
                UIFactory.Layout(bar, Mathf.Max(4f, h * r.score / max), 26f);
                // Hover: the run's score (and when / how) above the bar.
                var hover = bar.gameObject.AddComponent<ChartBarHover>();
                hover.panel = this;
                hover.text = $"<b>{r.score:N0}</b>\n<size=70%><color=#8C8C94>{RunDate(r)}{(r.difficulty > 0 ? "  ·  Lv " + r.difficulty : "")}</color></size>";
            }
            HideBarTip();

            // Latest runs.
            for (int i = 0; i < Mathf.Min(6, runs.Count); i++)
            {
                RunRecord r = runs[i];
                string date = DateTime.TryParse(r.dateUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime d)
                    ? d.ToLocalTime().ToString("dd MMM  HH:mm", Loc.Culture) : "";
                TextMeshProUGUI row = UIFactory.Text(theme, list, "Run", "", theme.sizeSmall, Palette.Text, TextAlignmentOptions.Left);
                row.overflowMode = TextOverflowModes.Ellipsis;
                string lv = r.difficulty > 0 ? $"<color=#F5C451>Lv {r.difficulty}</color>    " : "";
                row.text = $"<color=#8C8C94>{date}</color>    <b>{r.score:N0}</b>    {lv}<color=#8C8C94>{RunSummary(r)}</color>";
                UIFactory.Layout(row, 28f);
            }

            // Totals across every drill.
            float seconds = 0f;
            foreach (RunRecord r in StatsStore.Data.runs) seconds += r.duration;
            totalsText.text = Loc.F("{0} RUNS   ·   {1} MIN TRAINED", StatsStore.Data.runs.Count, Mathf.FloorToInt(seconds / 60f));
        }

        private static string RunDate(RunRecord r) =>
            DateTime.TryParse(r.dateUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime d)
                ? d.ToLocalTime().ToString("dd MMM  HH:mm", Loc.Culture) : "";

        // ------------------------------------------------------------------ chart hover tip

        private Image barTip;
        private TextMeshProUGUI barTipText;

        internal void ShowBarTip(RectTransform bar, string text)
        {
            if (barTip == null)
            {
                // Lives next to the chart (not inside it) so the chart's layout doesn't move it.
                barTip = UIFactory.Rounded(theme, chart.parent, "BarTip", Palette.BgElevated.WithAlpha(0.96f), 8f);
                UIFactory.Outline(theme, barTip.transform, Palette.BorderStrong, 8f);
                barTip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var group = barTip.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                barTip.rectTransform.pivot = new Vector2(0.5f, 0f);
                barTipText = UIFactory.Text(theme, barTip.transform, "Text", "", 20f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Semibold);
                barTipText.richText = true;
                UIFactory.Stretch(barTipText.rectTransform, 10f, 10f, 6f, 6f);
                Localization.Localizer.Exclude(barTipText);
            }
            barTipText.text = text;
            Vector2 pref = barTipText.GetPreferredValues(text);
            barTip.rectTransform.sizeDelta = new Vector2(Mathf.Max(90f, pref.x + 24f), pref.y + 14f);
            Vector3[] c = new Vector3[4];
            bar.GetWorldCorners(c);                                       // 1 = top left, 2 = top right
            barTip.rectTransform.position = (c[1] + c[2]) * 0.5f;
            barTip.rectTransform.anchoredPosition += new Vector2(0f, 8f);
            barTip.transform.SetAsLastSibling();
            barTip.gameObject.SetActive(true);
        }

        internal void HideBarTip()
        {
            if (barTip != null) barTip.gameObject.SetActive(false);
        }

        private void Tile(string label, string value)
        {
            Image card = UIFactory.Rounded(theme, tiles, "Tile", Palette.Surface.WithAlpha(0.8f), theme.radiusButton);
            UIFactory.Layout(card, 100f, 210f);
            TextMeshProUGUI v = UIFactory.Text(theme, card.transform, "Value", value, 34f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Display);
            UIFactory.Stretch(v.rectTransform, 8f, 8f, 10f, 34f);
            UIFactory.FitText(v, 18f);
            TextMeshProUGUI l = UIFactory.Text(theme, card.transform, "Label", label, theme.sizeCaption, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Semibold);
            l.characterSpacing = 6f;
            UIFactory.Stretch(l.rectTransform, 8f, 8f, 62f, 8f);
            UIFactory.FitText(l, 9f);
        }

        private static string KeyValue(List<RunRecord> runs, int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += runs[i].KeyRate();
            return Mathf.RoundToInt(sum / count * 100f) + "%";
        }

        private static string RunSummary(RunRecord r)
        {
            switch (r.mode)
            {
                case "dodge":
                case "blink": return Loc.F("dodged {0} · hit {1}", r.dodged, r.hitsTaken);
                case "kiting": return Loc.F("kills {0} · hit {1}", r.kills, r.hitsTaken);
                case "lasthit": return $"cs {r.kills} · missed {r.missed}";
                case "awareness": return $"caught {r.hits} · escaped {r.missed}";
                case "minimap": return Loc.F("hits {0} · missed {1} · lane hits {2} · {3} ms", r.hits, r.missed, r.hitsTaken, Mathf.RoundToInt(r.reaction));
                case "combo": return Loc.F("acc {0}% · combos {1}", Mathf.RoundToInt(r.Accuracy * 100f), r.procs);
                case "duel": return Loc.F("kills {0} · hit {1}", r.kills, r.hitsTaken) + " · " + Loc.T(r.won ? "survived" : "down");
                default: return Loc.F("acc {0}% · combos {1}", Mathf.RoundToInt(r.Accuracy * 100f), r.procs);
            }
        }

        private static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
        }
    }
}
