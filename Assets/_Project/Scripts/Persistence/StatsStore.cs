using System;
using System.Collections.Generic;

namespace MobaTrainer.Persistence
{
    /// <summary>One finished drill run.</summary>
    [Serializable]
    public class RunRecord
    {
        public string mode;          // "skillshot", "dodge", "duel"
        public string dateUtc;       // ISO 8601
        public int score;
        public float duration;
        public int casts;            // skillshots thrown
        public int hits;             // skillshots landed
        public int procs;            // marks detonated
        public int kills;
        public int dodged;           // enemy shots avoided
        public int hitsTaken;
        public int missed;           // last hits lost / targets that escaped
        public float reaction;       // average reaction time in ms (minimap drill)
        public float damageDealt;
        public float damageTaken;
        public bool won;
        /// <summary>Drill difficulty 1-10 (0 = not a difficulty drill).</summary>
        public int difficulty;

        public float Accuracy => casts > 0 ? hits / (float)casts : 0f;
        public float DodgeRate => dodged + hitsTaken > 0 ? dodged / (float)(dodged + hitsTaken) : 0f;

        /// <summary>The one percentage that matters most for this drill (shown on the Stats page).</summary>
        public float KeyRate()
        {
            switch (mode)
            {
                case "dodge":
                case "blink": return DodgeRate;
                case "duel": return won ? 1f : 0f;
                case "kiting": return kills + hitsTaken > 0 ? kills / (float)(kills + hitsTaken) : 0f;
                case "lasthit": return kills + missed > 0 ? kills / (float)(kills + missed) : 0f;
                case "awareness":
                case "minimap": return hits + missed > 0 ? hits / (float)(hits + missed) : 0f;
                default: return Accuracy;
            }
        }

        /// <summary>
        /// The one number a run is judged by (0-1, 1 = perfect). 75% unlocks the next level.
        /// Skillshot: accuracy. Dodge: dodge rate. Mixed: half accuracy, half dodge rate (max 74% if you die).
        /// Minimap: 70% prompts hit, 30% minimap dots avoided.
        /// </summary>
        public float Rating()
        {
            switch (mode)
            {
                case "skillshot": return Accuracy;
                case "dodge": return DodgeRate;
                case "duel":
                {
                    float acc = casts > 0 ? Accuracy : 0f;
                    float dodge = dodged + hitsTaken > 0 ? DodgeRate : 1f;
                    float r = acc * 0.5f + dodge * 0.5f;
                    return won ? r : System.Math.Min(r, 0.74f);
                }
                case "minimap":
                {
                    float prompts = hits + missed + casts > 0 ? hits / (float)(hits + missed + casts) : 0f;
                    float lanes = dodged + hitsTaken > 0 ? dodged / (float)(dodged + hitsTaken) : 1f;
                    return prompts * 0.7f + lanes * 0.3f;
                }
                default: return KeyRate();
            }
        }

        public static string RatingLabel(string mode)
        {
            switch (mode)
            {
                case "skillshot": return "ACCURACY";
                case "dodge": return "DODGE RATE";
                case "duel": return "PERFORMANCE";
                case "minimap": return "HIT RATE";
                default: return KeyRateLabel(mode);
            }
        }

        public static string KeyRateLabel(string mode)
        {
            switch (mode)
            {
                case "dodge":
                case "blink": return "DODGE RATE";
                case "duel": return "SURVIVED";
                case "kiting": return "CLEAN KILLS";
                case "lasthit": return "CS RATE";
                case "awareness": return "CATCH RATE";
                case "minimap": return "HIT RATE";
                default: return "ACCURACY";
            }
        }
    }

    [Serializable]
    public class StatsData
    {
        public int version = 1;
        public List<RunRecord> runs = new List<RunRecord>();
    }

    /// <summary>All finished runs, stored locally in <c>Save/stats.json</c> (newest last, capped).</summary>
    public static class StatsStore
    {
        public const string FileName = "stats.json";
        public const int MaxRuns = 1000;

        private static StatsData data;

        public static StatsData Data
        {
            get
            {
                if (data == null)
                {
                    if (!SaveSystem.TryLoad(FileName, out data) || data == null) data = new StatsData();
                    if (data.runs == null) data.runs = new List<RunRecord>();
                }
                return data;
            }
        }

        /// <summary>Adds a run, saves immediately, and returns the previous best score for that mode (-1 if none).</summary>
        public static int Add(RunRecord run)
        {
            int previousBest = Best(run.mode);
            run.dateUtc = DateTime.UtcNow.ToString("o");
            Data.runs.Add(run);
            if (Data.runs.Count > MaxRuns) Data.runs.RemoveRange(0, Data.runs.Count - MaxRuns);
            SaveSystem.Save(FileName, Data, false);
            return previousBest;
        }

        public static int Best(string mode)
        {
            int best = -1;
            foreach (RunRecord r in Data.runs) if (r.mode == mode && r.score > best) best = r.score;
            return best;
        }

        /// <summary>Newest-first runs of one mode (allocates; call from UI only).</summary>
        public static List<RunRecord> Runs(string mode, int max = int.MaxValue)
        {
            var list = new List<RunRecord>();
            for (int i = Data.runs.Count - 1; i >= 0 && list.Count < max; i--)
            {
                if (Data.runs[i].mode == mode) list.Add(Data.runs[i]);
            }
            return list;
        }
    }
}
