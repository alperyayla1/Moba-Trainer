namespace MobaTrainer.Core
{
    /// <summary>
    /// Converts the 0-100 mouse sensitivity slider used by typical MOBA clients into this game's
    /// cursor multiplier (1.0 = Windows default speed, raw input, no pointer acceleration).
    /// Those clients map the 0-100 slider onto the 20 Windows pointer-speed notches (value / 5),
    /// so the Windows notch multipliers are interpolated. Result is an estimate: the fine-tune
    /// slider in Controls is there to correct the last few percent.
    /// </summary>
    public static class SensitivityConverter
    {
        // Windows pointer-speed multipliers for notches 1..20 (notch 10 = 1.0).
        private static readonly float[] Notch =
        {
            1f / 32f, 1f / 16f, 1f / 8f, 1f / 4f, 3f / 8f, 1f / 2f, 5f / 8f, 3f / 4f, 7f / 8f, 1f,
            1.25f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 2.75f, 3f, 3.25f, 3.5f,
        };

        public static float FromOtherGame(float value)
        {
            float n = value / 5f;
            if (n < 1f) n = 1f;
            if (n > 20f) n = 20f;
            int lo = (int)n;
            if (lo >= 20) return Notch[19];
            float t = n - lo;
            return Notch[lo - 1] + (Notch[lo] - Notch[lo - 1]) * t;
        }
    }
}
