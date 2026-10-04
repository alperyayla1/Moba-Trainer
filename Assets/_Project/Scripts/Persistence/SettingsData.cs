using System;

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

    /// <summary>Everything in settings.json. Add fields with defaults; bump <see cref="version"/> when migrating.</summary>
    [Serializable]
    public class SettingsData
    {
        public const int CurrentVersion = 3;
        public const int CastSlotCount = 5; // Q W E R D (F is always self-cast)

        public int version = CurrentVersion;

        // Audio
        public float masterVolume = 1f;
        public float sfxVolume = 0.8f;
        public float musicVolume = 0.45f;
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
        public float cursorScale = 1f;

        // Camera
        public bool cameraLockedByDefault = true;
        public float edgePanSpeed = 28f;

        // Controls / gameplay
        public CastMode[] castModes = { CastMode.Quick, CastMode.Quick, CastMode.Quick, CastMode.Quick, CastMode.Quick };
        public bool autoAttackWhenIdle = true;
        public bool showRangeOnAttackMove = true;

        public CastMode GetCastMode(int slot)
        {
            if (castModes == null || slot < 0 || slot >= castModes.Length) return CastMode.Quick;
            return castModes[slot];
        }

        public void Sanitize()
        {
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
            otherGameSensitivity = otherGameSensitivity < 0 ? 0 : (otherGameSensitivity > 100 ? 100 : otherGameSensitivity);
            cursorScale = Clamp(cursorScale, 0.5f, 2f);
            edgePanSpeed = Clamp(edgePanSpeed, 5f, 60f);
            masterVolume = Clamp(masterVolume, 0f, 1f);
            sfxVolume = Clamp(sfxVolume, 0f, 1f);
            musicVolume = Clamp(musicVolume, 0f, 1f);
            version = CurrentVersion;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
