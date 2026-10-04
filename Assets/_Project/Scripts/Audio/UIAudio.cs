using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Audio
{
    /// <summary>
    /// Central one-shot SFX player. UI is deliberately quiet: a single click sound, no hover sounds.
    /// Volume follows Settings (master × sfx).
    /// </summary>
    [DefaultExecutionOrder(-700)]
    public class UIAudio : MonoBehaviour
    {
        public static UIAudio Instance { get; private set; }

        [Header("Audio Source")]
        public AudioSource audioSource;

        [Header("UI")]
        public AudioClip clickClip;
        [Range(0f, 1f)] public float clickVolume = 0.7f;

        [Header("Gameplay (optional)")]
        [Tooltip("Played when an ability is pressed while on cooldown. Leave empty for silence.")]
        public AudioClip errorClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true;
            Application.runInBackground = true;
        }

        private void OnEnable()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Changed += OnSettingsChanged;
            OnSettingsChanged(SettingsManager.Current);
        }

        private void OnDisable()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Changed -= OnSettingsChanged;
        }

        private void OnSettingsChanged(SettingsData s)
        {
            if (audioSource == null) return;
            audioSource.mute = !s.sfxEnabled;
            audioSource.volume = s.masterVolume * s.sfxVolume;
        }

        private void OnApplicationFocus(bool hasFocus) { AudioListener.pause = !hasFocus; }

        public void PlayClick()
        {
            if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip, clickVolume);
        }

        public void PlayError()
        {
            if (audioSource != null && errorClip != null) audioSource.PlayOneShot(errorClip, 0.6f);
        }
    }
}
