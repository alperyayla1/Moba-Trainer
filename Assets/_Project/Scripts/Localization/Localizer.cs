using MobaTrainer.Persistence;
using TMPro;
using UnityEngine;

namespace MobaTrainer.Localization
{
    /// <summary>
    /// Boots the language from settings (first launch: the OS language), keeps it in sync with the
    /// Settings window and attaches <see cref="LocalizedText"/> to every TMP label in the scene,
    /// including ones created later at runtime.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class Localizer : MonoBehaviour
    {
        private static Localizer instance;
        private float scanTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("Localizer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Localizer>();
        }

        private void Start()
        {
            SettingsManager sm = SettingsManager.Instance;
            if (sm != null)
            {
                if (sm.Data.language < 0)
                {
                    Language guess = Loc.FromSystem(Application.systemLanguage);
                    sm.Modify(d => d.language = (int)guess);
                }
                sm.Changed += OnSettings;
            }
            Loc.SetLanguage((Language)Mathf.Max(0, SettingsManager.Current.language));
            Scan();
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Changed -= OnSettings;
        }

        private static void OnSettings(SettingsData d) => Loc.SetLanguage((Language)Mathf.Max(0, d.language));

        private void Update()
        {
            // Safety net for labels created without UIFactory (they are attached instantly otherwise).
            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer > 0f) return;
            scanTimer = 1f;
            Scan();
        }

        private static void Scan()
        {
            TMP_Text[] all = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text t in all) Attach(t);
        }

        public static void Attach(TMP_Text t)
        {
            if (t == null || !Application.isPlaying) return;
            if (t.GetComponent<LocalizedText>() == null) t.gameObject.AddComponent<LocalizedText>();
        }
    }
}
