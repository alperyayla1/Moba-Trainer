using TMPro;
using UnityEngine;

namespace MobaTrainer.UI
{
    public enum ButtonStyle
    {
        Primary = 0,    // accent fill
        Secondary = 1,  // surface fill + hairline border
        Ghost = 2,      // transparent, surface on hover
        Danger = 3,     // transparent, red text, soft red on hover
    }

    /// <summary>Generated line icons (see MobaTrainerSetup.Icons). Order = index into UITheme.icons.</summary>
    public enum UIIcon
    {
        Play, Dodge, Skillshot, Kiting, LastHit, Blink, Combo, Duel, Radar,
        Gear, Power, Close, Restart, Home, Speaker, Monitor, Mouse, Keyboard, Lock, Unlock,
    }

    public enum TextWeight
    {
        Regular = 0,   // Montserrat Regular (body)
        Medium = 1,    // Montserrat Medium
        Semibold = 2,  // Montserrat SemiBold
        Heading = 3,   // Cinzel SemiBold (menu items, section titles)
        Display = 4,   // Cinzel Bold (big titles)
    }

    /// <summary>Red-on-black theme: Cinzel for titles, Montserrat for body, rounded shapes, generated line icons.</summary>
    [CreateAssetMenu(menuName = "MOBA Trainer/UI Theme", fileName = "UITheme")]
    public class UITheme : ScriptableObject
    {
        [Header("Fonts: Montserrat (body) + Cinzel (display)")]
        public TMP_FontAsset fontRegular;
        public TMP_FontAsset fontMedium;
        public TMP_FontAsset fontSemibold;
        public TMP_FontAsset fontHeading;
        public TMP_FontAsset fontDisplay;
        [Tooltip("Soft left-to-right black gradient for text readability over the live background.")]
        public Sprite gradientLeft;

        [Header("Shapes (generated)")]
        [Tooltip("Rounded rectangle, 9-sliced. Corner radius = 16 px at pixelsPerUnitMultiplier 1.")]
        public Sprite rounded;
        [Tooltip("1.5 px rounded outline matching 'rounded'.")]
        public Sprite roundedOutline;
        public Sprite circle;
        public Sprite white;
        [Tooltip("Soft radial glow (white, alpha falloff) for background accents.")]
        public Sprite glow;
        [Tooltip("Thin dashed ring for animated background decoration.")]
        public Sprite ringDashed;
        [Tooltip("Thin solid ring.")]
        public Sprite ringThin;

        [Header("Icons (indexed by UIIcon)")]
        public Sprite[] icons = new Sprite[0];

        [Header("Radii (px @1080p)")]
        public float radiusPanel = 16f;
        public float radiusButton = 10f;
        public float radiusSmall = 8f;

        [Header("Type Scale (pt @1080p)")]
        public float sizeDisplay = 60f;
        public float sizeTitle = 28f;
        public float sizeHeading = 22f;
        public float sizeBody = 17f;
        public float sizeSmall = 15f;
        public float sizeCaption = 13f;

        [Header("Motion")]
        public float fadeDuration = 0.15f;
        public float hoverDuration = 0.12f;

        public Sprite Icon(UIIcon icon)
        {
            int i = (int)icon;
            return icons != null && i < icons.Length ? icons[i] : null;
        }

        public TMP_FontAsset Font(TextWeight weight)
        {
            switch (weight)
            {
                case TextWeight.Display: return fontDisplay != null ? fontDisplay : (fontHeading != null ? fontHeading : fontRegular);
                case TextWeight.Heading: return fontHeading != null ? fontHeading : fontRegular;
                case TextWeight.Semibold: return fontSemibold != null ? fontSemibold : fontRegular;
                case TextWeight.Medium: return fontMedium != null ? fontMedium : fontRegular;
                default: return fontRegular;
            }
        }

        /// <summary>pixelsPerUnitMultiplier that turns the 16 px source radius into <paramref name="radius"/>.</summary>
        public static float RadiusMultiplier(float radius) => radius > 0.01f ? 16f / radius : 100f;
    }
}
