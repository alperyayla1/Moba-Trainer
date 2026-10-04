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
        public float damageDealt;
        public float damageTaken;
        public bool won;

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
                case "awareness": return hits + missed > 0 ? hits / (float)(hits + missed) : 0f;
                default: return Accuracy;
            }
        }

        public static string KeyRateLabel(string mode)
        {
            switch (mode)
            {
                case "dodge":
                case "blink": return "DODGE RATE";
                case "duel": return "WIN RATE";
                case "kiting": return "CLEAN KILLS";
                case "lasthit": return "CS RATE";
                case "awareness": return "CATCH RATE";
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
