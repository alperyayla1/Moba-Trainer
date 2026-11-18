namespace MobaTrainer.Persistence
{
    /// <summary>
    /// Per-drill levels 1-8. Each run gives a rating (0-100%); 75% or more unlocks the next level of
    /// that drill. Only Skillshot, Dodge, Mixed and Minimap have levels.
    /// </summary>
    public static class DrillProgress
    {
        public const float UnlockRating = 0.75f;
        public const int MaxLevel = 8;

        /// <summary>Index into the settings arrays, or -1 for drills without levels.</summary>
        public static int Slot(string drillId)
        {
            switch (drillId)
            {
                case "skillshot": return 0;
                case "dodge": return 1;
                case "duel": return 2;
                case "minimap": return 3;
                default: return -1;
            }
        }

        public static bool HasLevels(string drillId) => Slot(drillId) >= 0;

        public static int Level(string drillId)
        {
            int s = Slot(drillId);
            return s < 0 ? 0 : SettingsManager.Current.drillLevel[s];
        }

        public static int Unlocked(string drillId)
        {
            int s = Slot(drillId);
            return s < 0 ? 0 : SettingsManager.Current.drillUnlocked[s];
        }

        public static void SetLevel(string drillId, int level)
        {
            int s = Slot(drillId);
            if (s < 0 || SettingsManager.Instance == null) return;
            SettingsManager.Instance.Modify(d => d.drillLevel[s] = level);
        }

        /// <summary>Records a finished run. Returns true when it unlocked a new level (and selects it).</summary>
        public static bool Report(string drillId, int playedLevel, float rating)
        {
            int s = Slot(drillId);
            if (s < 0 || SettingsManager.Instance == null || rating < UnlockRating) return false;
            SettingsData d = SettingsManager.Current;
            if (playedLevel < d.drillUnlocked[s] || d.drillUnlocked[s] >= MaxLevel) return false;
            int next = d.drillUnlocked[s] + 1;
            SettingsManager.Instance.Modify(x => { x.drillUnlocked[s] = next; x.drillLevel[s] = next; });
            SettingsManager.Instance.SaveIfDirty();
            return true;
        }
    }
}
