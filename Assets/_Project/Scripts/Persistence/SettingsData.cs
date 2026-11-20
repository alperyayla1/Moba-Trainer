using System;
using UnityEngine;

namespace MobaTrainer.Persistence
{
    public enum CastMode
    {
        Normal = 0,          // press -> indicator -> left-click to confirm
        Quick = 1,           // cast instantly at cursor on press
        QuickIndicator = 2,  // hold to show indicator, cast on release
    }

    public enum WindowModeSetting
    {
        FullscreenWindow = 0,
        ExclusiveFullscreen = 1,
        Windowed = 2,
    }

    public enum CursorStyle
    {
        Gauntlet = 0,
        Arrow = 1,
    }

    public enum CursorModeSetting
    {
        System = 0,   // hardware cursor, moves like the Windows pointer (MOBA-client feel)
        Raw = 1,      // locked OS cursor + virtual cursor from raw mouse delta
    }

    /// <summary>Everything in settings.json. Add fields with defaults; bump <see cref="version"/> when migrating.</summary>
    [Serializable]
    public class SettingsData
    {
        public const int CurrentVersion = 7;
        public const int CastSlotCount = 5; // Q W E R D (F is always self-cast)

        public int version = CurrentVersion;

        // Audio
        public float masterVolume = 1f;
        public float sfxVolume = 0.8f;
        public float musicVolume = 0.35f;
        public bool sfxEnabled = true;
        public bool musicEnabled = true;

        // Video
        public int fpsCap = 144;              // 0 = unlimited
        public bool vSync = false;
        public WindowModeSetting windowMode = WindowModeSetting.FullscreenWindow;
        public bool showFps = false;

        // Cursor
        public float cursorSensitivity = 0.625f;
        /// <summary>Sensitivity value typed in from another MOBA client (0-100); converted into cursorSensitivity.</summary>
        public int otherGameSensitivity = 35;
        public CursorStyle cursorStyle = CursorStyle.Arrow;
        public CursorModeSetting cursorMode = CursorModeSetting.System;
        /// <summary>Keep the cursor inside the game window.</summary>
        public bool confineCursor = true;
        /// <summary>System cursor: apply the sensitivity as Windows pointer speed while the game is focused.</summary>
        public bool applySensitivityToSystem = true;
        /// <summary>Delay added to every player command (ms), to practise with online-like latency.</summary>
        public int simulatedLatencyMs = 0;
        /// <summary>Resolve player commands on a fixed ~30 Hz server tick, like an online MOBA.</summary>
        public bool serverTick = true;
        /// <summary>Gameplay camera vertical field of view.</summary>
        public float cameraFov = 40f;
        /// <summary>Drills run without a time limit (not recorded in stats).</summary>
        public bool endlessDrills = false;
        /// <summary>UI language (index into Loc.NativeNames). 0 = English (default).</summary>
        public int language = 0;
        /// <summary>Drill difficulty 1-10 (5 = the standard tuning). Not used by Free Practice or Extreme Survivor.</summary>
        public int drillDifficulty = 5;
        /// <summary>Attack-move targets the enemy closest to the cursor instead of the champion (MOBA "Attack move on cursor").</summary>
        public bool attackMoveOnCursor = false;
        /// <summary>Per drill (skillshot, dodge, mixed, minimap): chosen level and highest unlocked level (1-10).</summary>
        public int[] drillLevel = { 1, 1, 1, 1 };
        public int[] drillUnlocked = { 1, 1, 1, 1 };
        public float cursorScale = 1f;

        // Camera
        public bool cameraLockedByDefault = true;
        public float edgePanSpeed = 28f;

        // Controls / gameplay
        public CastMode[] castModes = { CastMode.Quick, CastMode.Quick, CastMode.Quick, CastMode.Quick, CastMode.Quick };
        /// <summary>Attack-move fires as soon as the key is pressed (no left-click needed).</summary>
        public bool attackMoveOnKeyPress = false;
        /// <summary>An ability pressed during another's cast time is cast right after it finishes.</summary>
        public bool queueAbilities = false;
        public bool autoAttackWhenIdle = true;
        public bool showRangeOnAttackMove = true;
        /// <summary>Played character: 0 = Wayfarer anime look, 1 = Wayfarer classic look, 2 = Arcanist (artillery mage). See HeroRoster.</summary>
        public int hero = 0;

        public CastMode GetCastMode(int slot)
        {
            if (castModes == null || slot < 0 || slot >= castModes.Length) return CastMode.Quick;
            return castModes[slot];
        }

        public void Sanitize()
        {
            if (version < 6) language = 0;   // v6: English is the default language (no OS auto-detect)
            if (version < 7)
            {
                // v7: per-drill levels (keep the level you were already playing), LoL-like defaults
                // for the settings that are no longer shown.
                int keep = Mathf.Clamp(drillDifficulty, 1, 10);
                drillLevel = new[] { keep, keep, keep, keep };
                drillUnlocked = new[] { keep, keep, keep, keep };
                cursorMode = CursorModeSetting.System;
                applySensitivityToSystem = true;
                serverTick = true;
                cursorSensitivity = MobaTrainer.Core.SensitivityConverter.FromOtherGame(otherGameSensitivity);
            }
            if (drillLevel == null || drillLevel.Length != 4) drillLevel = new[] { 1, 1, 1, 1 };
            if (drillUnlocked == null || drillUnlocked.Length != 4) drillUnlocked = new[] { 1, 1, 1, 1 };
            for (int i = 0; i < 4; i++)
            {
                drillUnlocked[i] = Mathf.Clamp(drillUnlocked[i], 1, DrillProgress.MaxLevel);
                drillLevel[i] = Mathf.Clamp(drillLevel[i], 1, drillUnlocked[i]);
            }
            if (version < 2) cursorStyle = CursorStyle.Arrow; // v2 UI redesign: modern arrow cursor by default
            if (version < 3 && castModes != null)
            {
                // v3: quick cast is the default for every key; left-click confirm is opt-in.
                for (int i = 0; i < castModes.Length; i++) castModes[i] = CastMode.Quick;
            }
            if (version < 3) cursorSensitivity = MobaTrainer.Core.SensitivityConverter.FromOtherGame(otherGameSensitivity);
            if (castModes == null || castModes.Length != CastSlotCount)
            {
                var fixedModes = new CastMode[CastSlotCount];
                for (int i = 0; i < CastSlotCount; i++)
                {
                    fixedModes[i] = castModes != null && i < castModes.Length ? castModes[i] : CastMode.Quick;
                }
                castModes = fixedModes;
            }
            cursorSensitivity = Clamp(cursorSensitivity, 0.02f, 3.5f);
            simulatedLatencyMs = simulatedLatencyMs < 0 ? 0 : (simulatedLatencyMs > 150 ? 150 : simulatedLatencyMs);
            cameraFov = Clamp(cameraFov, 32f, 50f);
            otherGameSensitivity = otherGameSensitivity < 0 ? 0 : (otherGameSensitivity > 100 ? 100 : otherGameSensitivity);
            cursorScale = Clamp(cursorScale, 0.5f, 2f);
            edgePanSpeed = Clamp(edgePanSpeed, 5f, 60f);
            masterVolume = Clamp(masterVolume, 0f, 1f);
            sfxVolume = Clamp(sfxVolume, 0f, 1f);
            musicVolume = Clamp(musicVolume, 0f, 1f);
            if (drillDifficulty < 1 || drillDifficulty > 10) drillDifficulty = 5;
            if (language < -1 || language >= MobaTrainer.Localization.Loc.NativeNames.Length) language = -1;
            version = CurrentVersion;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
