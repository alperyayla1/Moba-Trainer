using UnityEngine;

namespace MobaTrainer.Core
{
    /// <summary>
    /// Design tokens: minimal, modern, red on black. Every UI and placeholder-art colour comes from here.
    /// (Replaces the Blindfold Chess palette — see DESIGN_NOTES.md, "Design direction v2".)
    /// </summary>
    public static class Palette
    {
        // --- Surfaces ------------------------------------------------------------
        public static readonly Color Bg             = Hex(0x060607);
        public static readonly Color BgElevated     = Hex(0x0E0E10);
        public static readonly Color Surface        = Hex(0x131316);
        public static readonly Color SurfaceHover   = Hex(0x1C1618);
        public static readonly Color SurfacePressed = Hex(0x100D0E);
        public static readonly Color Border         = Hex(0x241C1E);
        public static readonly Color BorderStrong   = Hex(0x3A2A2D);
        public static readonly Color Overlay        = Hex(0x000000, 0.72f);

        // --- Text ----------------------------------------------------------------
        public static readonly Color Text      = Hex(0xF2F2F4);
        public static readonly Color TextMuted = Hex(0x8C8C94);
        public static readonly Color TextFaint = Hex(0x55555C);

        // --- Accent / semantic ---------------------------------------------------
        public static readonly Color Accent        = Hex(0xE23A45);
        public static readonly Color AccentHover   = Hex(0xF0515B);
        public static readonly Color AccentPressed = Hex(0xC42D38);
        public static readonly Color AccentSoft    = Hex(0xE23A45, 0.14f);
        public static readonly Color Danger        = Hex(0xFF4D5A);
        public static readonly Color DangerHover   = Hex(0xFF6B76);
        public static readonly Color DangerPressed = Hex(0xD93A47);
        public static readonly Color Success       = Hex(0x3DD68C);
        public static readonly Color Highlight     = Hex(0xF5C451);

        // --- Arena (placeholder art) ---------------------------------------------
        public static readonly Color Floor      = Hex(0x070708);
        public static readonly Color FloorGrid  = Hex(0x1A1214);
        public static readonly Color Player     = Hex(0xE6E6EA);
        public static readonly Color PlayerDark = Hex(0x8A1E27);
        public static readonly Color Enemy      = Hex(0xE23A45);
        public static readonly Color EnemyDark  = Hex(0x7A1820);
        public static readonly Color Neutral    = Hex(0x2A2A30);

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
