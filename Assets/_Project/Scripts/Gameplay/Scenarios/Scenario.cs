using System;
using System.Collections.Generic;
using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Base for timed drills. GameFlow calls <see cref="Begin"/> (set the stage), then
    /// <see cref="StartRunning"/> after the countdown; the scenario raises <see cref="Completed"/>
    /// with a filled <see cref="RunRecord"/> when time runs out or a win/lose condition is met.
    /// Enemies are pooled <see cref="TargetDummy"/> instances with an <see cref="EnemyBrain"/>.
    /// </summary>
    public abstract class Scenario : MonoBehaviour, IRunCounters
    {
        [Header("Wiring")]
        public TargetDummy dummyPrefab;
        public PlayerController player;
        [Tooltip("Blue caster variant (fan of shards).")]
        public TargetDummy bluePrefab;
        [Tooltip("Melee variant (lunges at you).")]
        public TargetDummy meleePrefab;
        [Tooltip("Fire mage: drops delayed flame pillars.")]
        public TargetDummy firePrefab;
        [Tooltip("Card thrower: throws three slow cards in a wide cone.")]
        public TargetDummy cardsPrefab;

        /// <summary>Set from the Drills screen: runs have no time limit (not recorded in stats).</summary>
        public static bool Endless;
        /// <summary>Chosen on the Drills screen (1-10, 5 = standard). Set before every run.</summary>
        public static int Difficulty = 5;
        /// <summary>False for drills that ignore the difficulty (Extreme Survivor).</summary>
        public virtual bool UsesDifficulty => true;
        // Difficulty never changes speeds (skill speed, wind-ups, movement): those stay true to a real
        // MOBA. It only changes HOW MUCH happens: more enemies, more frequent casts, more juking.
        /// <summary>Multiplier for gaps between casts / spawns: 1.47 at level 1, 1.0 at 5, 0.71 at 10.</summary>
        // Levels 1-8 spread over the old 1-10 difficulty scale (level 1 = 1, level 8 = 10).
        protected static float Scale => 1f + (Mathf.Clamp(Difficulty, 1, Persistence.DrillProgress.MaxLevel) - 1) * 9f / (Persistence.DrillProgress.MaxLevel - 1);
        protected float Sooner => UsesDifficulty ? 1f / (1f + (Scale - 5f) * 0.08f) : 1f;
        /// <summary>Extra enemies / prompts: -2 at level 1-2, -1 at 3-4, 0 at 5-6, +1 at 7-8, +2 at 9-10.</summary>
        protected int ExtraCount => UsesDifficulty ? Mathf.FloorToInt((Scale - 5f) / 2f) : 0;
        public bool IsEndless { get; private set; }

        protected float runDuration;

        [Header("Run")]
        [Tooltip("Seconds; 0 = untimed (ends on a win/lose condition).")]
        public float duration = 60f;
        public Vector3 playerSpawn = new Vector3(0f, 0f, -4f);

        public abstract string Id { get; }
        public abstract string Title { get; }
        /// <summary>False for drills that only use mouse/keys on UI (the hero stays idle, OS cursor visible).</summary>
        public virtual bool UsesPlayerControl => true;

        public bool Running { get; private set; }
        /// <summary>0→1 over the run (endless: over the first 60 s), used for difficulty ramps.</summary>
        public float Time01 => Mathf.Clamp01(elapsed / (runDuration > 0f ? runDuration : 60f));

        public event Action<Scenario, RunRecord> Completed;

        // IRunCounters (HUD pills)
        public abstract int Hits { get; }
        public abstract int Misses { get; }
        public float Elapsed => runDuration > 0f ? Mathf.Max(0f, runDuration - elapsed) : elapsed;

        protected float elapsed;
        protected readonly List<TargetDummy> enemies = new List<TargetDummy>();
        protected RunRecord record;
        private bool subscribed;
        private static float playerBaseHealth = -1f;

        // ------------------------------------------------------------------ lifecycle

        public void Begin()
        {
            gameObject.SetActive(true);
            elapsed = 0f;
            Running = false;
            record = new RunRecord { mode = Id, difficulty = UsesDifficulty ? Mathf.Clamp(Difficulty, 1, 10) : 0 };
            EnemySkillSystem.Instance?.ClearAll();
            Subscribe(true);
            // The chosen hero (Settings > Champion) sets the base stats for this run, health included.
            HeroRoster roster = player.GetComponent<HeroRoster>();
            if (roster != null)
            {
                roster.ApplyWanted();
                roster.Locked = true;
                if (roster.CurrentHero != null) playerBaseHealth = roster.CurrentHero.maxHealth;
            }
            if (playerBaseHealth < 0f) playerBaseHealth = player.Unit.maxHealth;
            player.Unit.maxHealth = playerBaseHealth;
            player.Unit.invulnerable = false;
            player.ResetPlayer(playerSpawn);
            runDuration = duration;
            OnBegin();
            IsEndless = Endless;
            runDuration = IsEndless ? 0f : duration;   // read after OnBegin (some drills set their duration there)
            SetBrains(false);
        }

        public void StartRunning()
        {
            Running = true;
            SetBrains(true);
            OnStart();
        }

        public void End()
        {
            Running = false;
            HeroRoster roster = player != null ? player.GetComponent<HeroRoster>() : null;
            if (roster != null) roster.Locked = false;
            SetBrains(false);
            Subscribe(false);
            EnemySkillSystem.Instance?.ClearAll();
            foreach (TargetDummy d in enemies) if (d != null) d.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        protected void Finish()
        {
            if (!Running) return;
            Running = false;
            SetBrains(false);
            EnemySkillSystem.Instance?.ClearAll();
            record.duration = elapsed;
            FillRecord(record);
            Completed?.Invoke(this, record);
        }

        private void Update()
        {
            if (!Running) return;
            elapsed += Time.deltaTime;
            OnTick(Time.deltaTime);
            if (Running && runDuration > 0f && elapsed >= runDuration) Finish();
        }

        protected virtual void OnDisable()
        {
            Subscribe(false);
            if (player != null)
            {
                player.Unit.invulnerable = false;
                if (playerBaseHealth > 0f) player.Unit.maxHealth = playerBaseHealth;
            }
        }

        // ------------------------------------------------------------------ hooks

        protected abstract void OnBegin();
        protected virtual void OnStart() { }
        protected virtual void OnTick(float dt) { }
        /// <summary>Fill score and mode-specific numbers.</summary>
        protected abstract void FillRecord(RunRecord r);

        protected virtual void OnAbilityCast(int slot, AbilityDefinition ability, Vector3 point) { }
        protected virtual void OnSkillshotResolved(AbilityDefinition ability, Unit hit) { }
        protected virtual void OnMarkDetonated(Unit target, float burst) { }
        protected virtual void OnEnemyShot(Unit hit) { }
        protected virtual void OnPlayerDamaged(Unit unit, float amount, Unit source) { }
        protected virtual void OnEnemyDamaged(Unit unit, float amount, Unit source) { }
        protected virtual void OnEnemyDied(Unit unit, Unit killer) { }

        // ------------------------------------------------------------------ helpers

        /// <summary>Gets (or instantiates) enemy #index, active and reset.</summary>
        /// <summary>
        /// Fire mage's flame pillar, tuned to the classic MOBA ground-target numbers (1 unit = 100 game units):
        /// 900 cast range, 0.25 s cast time, then 0.627 s until it erupts (radius trimmed to 200 and the aim
        /// varied so it is dodgeable without Blink).
        /// </summary>
        /// <summary>
        /// Card throw (like a classic three-card toss): three thin, slow cards in a wide cone. Slower and
        /// more spread out than a normal shot, so you dodge it by reading the gaps.
        /// </summary>
        protected static void SetupCards(EnemyBrain b, float damage, Vector2 interval)
        {
            b.castLines = true;
            b.shotStyle = EnemyBrain.ShotStyle.Fan;
            b.fanCount = 3;
            b.fanSpread = 52f;
            b.lineWidth = 0.55f;
            b.lineSpeed = 9f;
            b.lineWindup = 0.45f;
            b.lineRange = 14f;
            b.lineDamage = damage;
            b.leadChance = 0.1f;
            b.lineInterval = interval;
        }

        /// <summary>
        /// Light beam (like a long-range finale laser): a thin light line shows the path for 1 s while the
        /// caster stands still, then a wide beam hits the whole line at once.
        /// </summary>
        protected static void SetupBeam(EnemyBrain b, float damage, Vector2 interval, float width = 1.8f, float windup = 1f, float range = 17f)
        {
            b.castLines = true;
            b.shotStyle = EnemyBrain.ShotStyle.Beam;
            b.lineWidth = width;
            b.lineWindup = windup;
            b.lineRange = range;
            b.lineDamage = damage;
            b.leadChance = 0.15f;
            b.lineInterval = interval;
            b.preferredRange = new Vector2(9f, 13f);
        }

        protected static void SetupFirePillar(EnemyBrain b, float damage, Vector2 interval)
        {
            b.castLines = false;
            b.castCircles = true;
            b.circleLook = 1;
            b.circleRange = 9f;
            b.circleRadius = 2.0f;          // smaller than the classic 260 so it can be walked out of
            b.circleCastTime = 0.25f;
            b.circleDelay = 0.627f;
            b.circleDamage = damage;
            b.circleInterval = interval;
            b.circleAimSpread = 1.8f;       // not always dead centre: on your path, beside you, or short
            b.preferredRange = new Vector2(6.5f, 8.5f);   // stays inside its cast range
        }

        protected TargetDummy Enemy(int index, Vector3 position, float hp, TargetDummy prefab = null)
        {
            while (enemies.Count <= index)
            {
                TargetDummy d = Instantiate(prefab != null ? prefab : dummyPrefab, transform);
                d.name = $"{Id}_Enemy_{enemies.Count}";
                d.gameObject.AddComponent<EnemyBrain>();
                d.Damaged += (u, amount, src) => { if (Running) OnEnemyDamaged(u, amount, src); };
                d.Died += (u, killer) => { if (Running) OnEnemyDied(u, killer); };
                enemies.Add(d);
            }
            TargetDummy e = enemies[index];
            e.gameObject.SetActive(true);
            e.respawn = true;
            e.respawnDelay = 0.8f;
            e.invulnerable = false;
            e.BeforeRespawn = null;
            e.Configure(Arena.ClampStatic(position, e.radius + 0.2f), TargetDummy.Behaviour.Static, hp, 0f);
            if (e.healthBar != null) e.healthBar.gameObject.SetActive(true);
            EnemyBrain brain = e.GetComponent<EnemyBrain>();
            brain.target = player.Unit;
            brain.move = true;
            brain.castLines = false;
            brain.castCircles = false;
            brain.chase = false;
            brain.lunge = false;
            brain.shotStyle = EnemyBrain.ShotStyle.Orb;
            brain.circleAimSpread = 0f;
            brain.dodgeChance = 0f;
            brain.circleLook = 0;
            brain.circleCastTime = 0f;
            brain.ResetBrain();
            return e;
        }

        protected void HideEnemiesFrom(int index)
        {
            for (int i = index; i < enemies.Count; i++) enemies[i].gameObject.SetActive(false);
        }

        protected Vector3 RandomPointAround(Vector3 center, float minR, float maxR)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float r = UnityEngine.Random.Range(minR, maxR);
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 c = Arena.ClampStatic(p, 0.9f);
                if ((c - p).sqrMagnitude < 0.01f) return c;
            }
            return Arena.ClampStatic(center + Vector3.forward * minR, 0.9f);
        }

        private void SetBrains(bool on)
        {
            foreach (TargetDummy d in enemies)
            {
                if (d == null) continue;
                EnemyBrain b = d.GetComponent<EnemyBrain>();
                if (b != null) b.Active = on && d.gameObject.activeSelf;
            }
        }

        private void Subscribe(bool on)
        {
            if (on == subscribed) return;
            PlayerSkillSystem pss = PlayerSkillSystem.Instance;
            EnemySkillSystem ess = EnemySkillSystem.Instance;
            if (on)
            {
                if (pss != null) { pss.SkillshotResolved += SkillshotHandler; pss.MarkDetonated += MarkHandler; }
                if (ess != null) ess.ShotResolved += EnemyShotHandler;
                if (player != null)
                {
                    player.Abilities.AbilityCast += CastHandler;
                    player.Unit.Damaged += PlayerDamagedHandler;
                }
            }
            else
            {
                if (pss != null) { pss.SkillshotResolved -= SkillshotHandler; pss.MarkDetonated -= MarkHandler; }
                if (ess != null) ess.ShotResolved -= EnemyShotHandler;
                if (player != null)
                {
                    player.Abilities.AbilityCast -= CastHandler;
                    player.Unit.Damaged -= PlayerDamagedHandler;
                }
            }
            subscribed = on;
        }

        private void SkillshotHandler(AbilityDefinition a, Unit hit) { if (Running) OnSkillshotResolved(a, hit); }
        private void MarkHandler(Unit t, float burst) { if (Running) OnMarkDetonated(t, burst); }
        private void EnemyShotHandler(Unit hit) { if (Running) OnEnemyShot(hit); }
        private void CastHandler(int slot, AbilityDefinition a, Vector3 p) { if (Running) OnAbilityCast(slot, a, p); }
        private void PlayerDamagedHandler(Unit u, float amount, Unit src) { if (Running) OnPlayerDamaged(u, amount, src); }
    }
}
