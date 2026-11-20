using System.Collections.Generic;
using MobaTrainer.Gameplay;
using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Audio
{
    /// <summary>
    /// Gameplay sounds (our own synthesized clips in Resources/SFX): your casts, enemy casts,
    /// beams and pillars, getting hit. No death sounds except the Extreme Survivor boss.
    /// Volume follows Settings (master × effects). Created automatically, nothing to place in the scene.
    /// </summary>
    public class GameSfx : MonoBehaviour
    {
        private static GameSfx instance;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private AudioSource[] sources;
        private int next;
        private PlayerController player;
        private bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("GameSfx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameSfx>();
        }

        private void Awake()
        {
            foreach (AudioClip c in Resources.LoadAll<AudioClip>("SFX")) clips[c.name] = c;
            sources = new AudioSource[10];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].ignoreListenerPause = false;
            }
        }

        private void Update()
        {
            if (!hooked) Hook();
        }

        private void Hook()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null || player.Abilities == null) return;
            // Q/W/E cast sounds + Blink whoosh (CC0 Kenney sources, reworked into spell sounds).
            player.Abilities.ChargeStarted += (slot, ability) =>
            {
                if (ability != null && !string.IsNullOrEmpty(ability.chargeSound)) Play(ability.chargeSound, 0.7f);
            };
            player.Abilities.AbilityCast += (slot, ability, point) =>
            {
                // Per-ability sound (e.g. the Arcanist's kit) wins over the slot default.
                if (ability != null && !string.IsNullOrEmpty(ability.castSound)) { Play(ability.castSound, 0.8f); return; }
                switch (slot)
                {
                    case 0: Play("cast_q", 0.8f); break;
                    case 1: Play("cast_q", 0.8f); break;   // W uses the same sound as Q
                    case 2: Play("cast_e", 0.6f); break;
                    case 3: Play("blink", 0.65f); break;
                }
            };
            // No sound for enemies taking damage (by design): only your casts, enemy skills and getting hit.
            // No sound when you take damage (for now).
            EnemySkillSystem.Sound += (name, pos) => Play(name, name == "enemy_cast" ? 0.35f : 0.75f);
            hooked = true;
        }

        /// <summary>Plays a clip by name (rate-limited so many identical hits don't stack up).</summary>
        public static void Play(string name, float volume = 1f, float pitchJitter = 0.05f, float minInterval = 0.03f)
        {
            if (instance == null || !instance.clips.TryGetValue(name, out AudioClip clip)) return;
            SettingsData s = SettingsManager.Current;
            if (!s.sfxEnabled) return;
            float now = Time.unscaledTime;
            if (instance.lastPlayed.TryGetValue(name, out float last) && now - last < minInterval) return;
            instance.lastPlayed[name] = now;
            AudioSource src = instance.sources[instance.next];
            instance.next = (instance.next + 1) % instance.sources.Length;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, volume * s.masterVolume * s.sfxVolume);
        }
    }
}
