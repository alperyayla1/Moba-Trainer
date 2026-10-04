using System;
using System.Text;
using MobaTrainer.Core;
using MobaTrainer.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>End-of-run card: mode, big score, best / new best, key numbers, Retry · Stats · Menu.</summary>
    public class ResultsPanel : UIPanel
    {
        [Header("References")]
        public TextMeshProUGUI modeText;
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI bestText;
        public TextMeshProUGUI detailText;
        public Button retryButton;
        public Button statsButton;
        public Button menuButton;

        public event Action RetryRequested;
        public event Action StatsRequested;
        public event Action MenuRequested;

        private readonly StringBuilder sb = new StringBuilder(256);

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
            if (statsButton != null) statsButton.onClick.AddListener(() => StatsRequested?.Invoke());
            if (menuButton != null) menuButton.onClick.AddListener(() => MenuRequested?.Invoke());
        }

        public void ShowResult(string title, RunRecord r, int previousBest)
        {
            modeText.text = title.ToUpperInvariant() + (r.mode == "duel" ? (r.won ? "  ·  VICTORY" : "  ·  DEFEAT") : string.Empty);
            scoreText.text = r.score.ToString("N0");
            bool newBest = r.score > previousBest;
            bestText.text = newBest ? "NEW BEST" : "BEST  " + previousBest.ToString("N0");
            bestText.color = newBest ? Palette.Highlight : Palette.TextMuted;
            detailText.text = Details(r);
            Show();
        }

        private string Details(RunRecord r)
        {
            sb.Clear();
            void Line(string label, string value) => sb.Append("<color=#8C8C94>").Append(label).Append("</color>   ").Append(value).Append('\n');
            switch (r.mode)
            {
                case "skillshot":
                    Line("Accuracy", Percent(r.Accuracy) + $"  ({r.hits}/{r.casts})");
                    Line("Combos", r.procs.ToString());
                    Line("Kills", r.kills.ToString());
                    break;
                case "kiting":
                    Line("Kills", r.kills.ToString());
                    Line("Hit", r.hitsTaken.ToString());
                    Line("Clean kills", Percent(r.KeyRate()));
                    break;
                case "lasthit":
                    Line("Last hits", r.kills.ToString());
                    Line("Missed", r.missed.ToString());
                    Line("CS rate", Percent(r.KeyRate()));
                    break;
                case "awareness":
                    Line("Caught", r.hits.ToString());
                    Line("Escaped", r.missed.ToString());
                    Line("Catch rate", Percent(r.KeyRate()));
                    break;
                case "combo":
                    Line("Combos", r.procs.ToString());
                    Line("Accuracy", Percent(r.Accuracy) + $"  ({r.hits}/{r.casts})");
                    break;
                case "dodge":
                case "blink":
                    Line("Dodged", r.dodged.ToString());
                    Line("Hit", r.hitsTaken.ToString());
                    Line("Dodge rate", Percent(r.DodgeRate));
                    break;
                default:
                    Line("Time", r.duration.ToString("0.0") + " s");
                    Line("Accuracy", Percent(r.Accuracy) + $"  ({r.hits}/{r.casts})");
                    Line("Combos", r.procs.ToString());
                    Line("Dodged", $"{r.dodged}   hit {r.hitsTaken}");
                    break;
            }
            return sb.ToString();
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }
}
