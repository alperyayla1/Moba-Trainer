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
            new Mode { id = "duel", title = "Duel", icon = UIIcon.Duel },
            new Mode { id = "kiting", title = "Kiting", icon = UIIcon.Kiting },
            new Mode { id = "lasthit", title = "Last Hit", icon = UIIcon.LastHit },
            new Mode { id = "blink", title = "Blink", icon = UIIcon.Blink },
            new Mode { id = "combo", title = "Combo", icon = UIIcon.Combo },
            new Mode { id = "awareness", title = "Awareness", icon = UIIcon.Radar },
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
                Button b = UIFactory.IconLabelButton(theme, tabRow, "Tab_" + modes[i].id, modes[i].icon, modes[i].title.ToUpperInvariant(), ButtonStyle.Ghost, new Vector2(156f, 40f));
                TextMeshProUGUI lt = UIFactory.Label(b);
                lt.font = theme.Font(TextWeight.Heading);
                lt.characterSpacing = 6f;
                UIFactory.Layout(b, 40f, 156f);
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
            chartCaption.text = n > 0 ? $"LAST {n} RUNS" : "NO RUNS YET  -  PLAY THIS DRILL TO START TRACKING";
            int max = 1;
            for (int i = 0; i < n; i++) max = Mathf.Max(max, runs[i].score);
            float h = chart.rect.height > 10f ? chart.rect.height : 200f;
            for (int i = n - 1; i >= 0; i--)
            {
                RunRecord r = runs[i];
                bool isBest = r.score == best && best > 0;
                Image bar = UIFactory.Rounded(theme, chart, "Bar", isBest ? Palette.Accent : (i == 0 ? Palette.Text.WithAlpha(0.85f) : Palette.TextFaint), 4f);
                UIFactory.Layout(bar, Mathf.Max(4f, h * r.score / max), 26f);
            }

            // Latest runs.
            for (int i = 0; i < Mathf.Min(6, runs.Count); i++)
            {
                RunRecord r = runs[i];
                string date = DateTime.TryParse(r.dateUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime d)
                    ? d.ToLocalTime().ToString("dd MMM  HH:mm") : "";
                TextMeshProUGUI row = UIFactory.Text(theme, list, "Run", "", theme.sizeSmall, Palette.Text, TextAlignmentOptions.Left);
                row.text = $"<color=#8C8C94>{date}</color>    <b>{r.score:N0}</b>    <color=#8C8C94>{RunSummary(r)}</color>";
                UIFactory.Layout(row, 28f);
            }

            // Totals across every drill.
            float seconds = 0f;
            foreach (RunRecord r in StatsStore.Data.runs) seconds += r.duration;
            totalsText.text = $"{StatsStore.Data.runs.Count} RUNS   ·   {Mathf.FloorToInt(seconds / 60f)} MIN TRAINED";
        }

        private void Tile(string label, string value)
        {
            Image card = UIFactory.Rounded(theme, tiles, "Tile", Palette.Surface.WithAlpha(0.8f), theme.radiusButton);
            UIFactory.Layout(card, 96f, 196f);
            TextMeshProUGUI v = UIFactory.Text(theme, card.transform, "Value", value, 34f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Display);
            UIFactory.Stretch(v.rectTransform, 8f, 8f, 10f, 34f);
            TextMeshProUGUI l = UIFactory.Text(theme, card.transform, "Label", label, theme.sizeCaption, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Semibold);
            l.characterSpacing = 6f;
            UIFactory.Stretch(l.rectTransform, 8f, 8f, 62f, 8f);
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
                case "blink": return $"dodged {r.dodged} · hit {r.hitsTaken}";
                case "kiting": return $"kills {r.kills} · hit {r.hitsTaken}";
                case "lasthit": return $"cs {r.kills} · missed {r.missed}";
                case "awareness": return $"caught {r.hits} · escaped {r.missed}";
                case "combo": return $"combos {r.procs} · acc {Mathf.RoundToInt(r.Accuracy * 100f)}%";
                case "duel": return (r.won ? "won" : "lost") + $" · {r.duration:0}s · acc {Mathf.RoundToInt(r.Accuracy * 100f)}%";
                default: return $"acc {Mathf.RoundToInt(r.Accuracy * 100f)}% · combos {r.procs}";
            }
        }

        private static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
        }
    }
}
