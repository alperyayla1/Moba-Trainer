using System;
using UnityEngine;

namespace MobaTrainer.Persistence
{
    /// <summary>
    /// Owns <see cref="SettingsData"/>: loads it at startup, applies video settings, and notifies
    /// listeners (audio, cursor, camera, HUD) when something changes. Ported from the Blindfold Chess
    /// SettingsManager and extended; stored as JSON instead of PlayerPrefs.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SettingsManager : MonoBehaviour
    {
        public const string FileName = "settings.json";

        public static SettingsManager Instance { get; private set; }

        public SettingsData Data { get; private set; }

        /// <summary>Raised after any setting changed (and was applied).</summary>
        public event Action<SettingsData> Changed;

        private bool dirty;
        private float saveAt;

        /// <summary>Changes are written to disk this many real seconds after the last edit (slider drags don't spam the disk).</summary>
        private const float AutosaveDelay = 0.35f;

        public static SettingsData Current => Instance != null ? Instance.Data : Fallback;
        private static readonly SettingsData Fallback = new SettingsData();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!SaveSystem.TryLoad(FileName, out SettingsData loaded)) loaded = new SettingsData();
            int loadedVersion = loaded.version;
            loaded.Sanitize();
            Data = loaded;
            if (loadedVersion != SettingsData.CurrentVersion) Save(); // persist migrations right away
            ApplyVideo();
        }

        /// <summary>Mutate settings through this so listeners are notified and the file gets saved.</summary>
        public void Modify(Action<SettingsData> change)
        {
            change?.Invoke(Data);
            Data.Sanitize();
            ApplyVideo();
            dirty = true;
            saveAt = Time.unscaledTime + AutosaveDelay;
            Changed?.Invoke(Data);
        }

        private void Update()
        {
            if (dirty && Time.unscaledTime >= saveAt) Save();
        }

        public void ResetToDefaults()
        {
            Data = new SettingsData();
            ApplyVideo();
            dirty = true;
            Changed?.Invoke(Data);
            Save();
        }

        public void Save()
        {
            SaveSystem.Save(FileName, Data);
            dirty = false;
        }

        public void SaveIfDirty()
        {
            if (dirty) Save();
        }

        private void ApplyVideo()
        {
            QualitySettings.vSyncCount = Data.vSync ? 1 : 0;
            Application.targetFrameRate = Data.vSync ? -1 : (Data.fpsCap <= 0 ? -1 : Data.fpsCap);

            FullScreenMode mode = Data.windowMode switch
            {
                WindowModeSetting.ExclusiveFullscreen => FullScreenMode.ExclusiveFullScreen,
                WindowModeSetting.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow,
            };
#if !UNITY_EDITOR
            if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
#endif
        }

        private void OnApplicationQuit() => SaveIfDirty();
        private void OnDisable() => SaveIfDirty(); // also covers leaving Play mode in the editor

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SaveIfDirty();
        }
    }
}
