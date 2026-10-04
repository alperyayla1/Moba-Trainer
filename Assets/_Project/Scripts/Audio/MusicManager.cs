using System.Collections;
using System.Collections.Generic;
using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Audio
{
    /// <summary>
    /// Title track, then a shuffled dark-ambient playlist (no immediate repeats).
    /// Port of the Blindfold Chess MusicManager, now with the crossfade the original declared
    /// but never implemented, and volume driven by Settings (master × music).
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [Header("Audio Source")]
        public AudioSource musicSource;

        [Header("Music Clips")]
        [Tooltip("First clip that plays on the title screen.")]
        public AudioClip titleScreenMusic;
        [Tooltip("Background clips that play in random order after the title track.")]
        public AudioClip[] backgroundMusicClips;

        [Header("Settings")]
        public float fadeDuration = 2f;

        private readonly List<AudioClip> shuffled = new List<AudioClip>();
        private int trackIndex;
        private float fadeMultiplier = 1f;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = false;
            musicSource.playOnAwake = false;
            musicSource.ignoreListenerPause = false;
            Shuffle();
        }

        private void OnEnable()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Changed -= OnSettingsChanged;
        }

        private void Start()
        {
            ApplyVolume();
            if (titleScreenMusic != null) PlayClip(titleScreenMusic);
            else PlayNext();
        }

        private void OnSettingsChanged(SettingsData s) => ApplyVolume();

        private void ApplyVolume()
        {
            SettingsData s = SettingsManager.Current;
            musicSource.mute = !s.musicEnabled;
            musicSource.volume = s.masterVolume * s.musicVolume * fadeMultiplier;
        }

        private void Shuffle()
        {
            shuffled.Clear();
            if (backgroundMusicClips == null) return;
            for (int i = 0; i < backgroundMusicClips.Length; i++)
            {
                if (backgroundMusicClips[i] != null) shuffled.Add(backgroundMusicClips[i]);
            }
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            trackIndex = 0;
        }

        private void PlayClip(AudioClip clip)
        {
            musicSource.clip = clip;
            musicSource.time = 0f;
            musicSource.Play();
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(Fade(0f, 1f));
        }

        private void PlayNext()
        {
            if (shuffled.Count == 0) return;
            if (trackIndex >= shuffled.Count) Shuffle();
            AudioClip next = shuffled[trackIndex++];
            if (next == musicSource.clip && shuffled.Count > 1) next = shuffled[trackIndex++ % shuffled.Count];
            PlayClip(next);
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeMultiplier = Mathf.Lerp(from, to, t / fadeDuration);
                ApplyVolume();
                yield return null;
            }
            fadeMultiplier = to;
            ApplyVolume();
            fadeRoutine = null;
        }

        private void Update()
        {
            if (!Application.isFocused || musicSource.clip == null) return;

            // Fade out near the end of the track, then advance.
            float remaining = musicSource.clip.length - musicSource.time;
            if (musicSource.isPlaying && remaining <= fadeDuration && fadeRoutine == null && fadeMultiplier > 0.99f)
            {
                fadeRoutine = StartCoroutine(Fade(1f, 0f));
            }
            if (!musicSource.isPlaying && musicSource.time == 0f)
            {
                PlayNext();
            }
        }
    }
}
