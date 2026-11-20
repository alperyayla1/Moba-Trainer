using System.Collections.Generic;
using MobaTrainer.Gameplay;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>
    /// Extreme Survivor: one long run in the style of a survivors game, built on the Mixed Mode
    /// enemies. Everything specific to this mode lives in this folder; it only *uses* the shared
    /// Scenario / EnemyBrain / EnemySkillSystem / Unit code and never changes their defaults, so the
    /// other drills play exactly as before.
    ///
    /// - Run timer counts up. Dying ends the run (loss). Killing the boss ends it (win).
    /// - Config-driven spawner (<see cref="SurvivorConfig"/>): enemy tiers unlock over time, spawn rate
    ///   ramps up every minute, pooled enemies, hard cap on enemies alive.
    /// - Item offers every minute by default (3 cards + Random), 4 slots, stats and passives
    ///   (<see cref="SurvivorItemSystem"/>), overlay UI (<see cref="SurvivorUI"/>).
    /// - After the last pick: the boss (a giant fire mage) with three phases; enrages 3:00 after it appears.
    ///
    /// Debug (editor and development builds only): F9 = skip 1:00, Shift+F9 = skip 3:00.
    /// </summary>
    public class ExtremeSurvivorScenario : Scenario
    {
        [Tooltip("All numbers for this mode. If empty, built-in defaults are used.")]
        public SurvivorConfig config;

        [Header("Debug")]
        public bool debugSkip = true;
        public float debugSkipSeconds = 60f;

        public override string Id => "extreme";
        public override string Title => "Extreme Survivor";
        public override int Hits => record.kills;
        public override int Misses => record.hitsTaken;
        public override bool UsesDifficulty => false;   // this mode has its own pacing

        [Tooltip("Small horned swarm enemy (its own look, so it is not confused with the Blade).")]
        public TargetDummy swarmPrefab;
        [Tooltip("This mode's own map (Ember Wastes). Shown only while the mode runs.")]
        public MapVariant map;

        public float Minutes => elapsed / 60f;
        public int AliveCount { get; private set; }

        private SurvivorConfig cfg;
        private SurvivorItemSystem items;
        private SurvivorUI ui;
        private List<SurvivorItem> catalog;
        private static List<SurvivorItem> defaultItems;
        private int nextOffer;
        private bool subscribedExtra;

        // One pool block per tier: enemies[first .. first + maxAlive).
        private class TierState
        {
            public SurvivorEnemyTier tier;
            public int first;
            public int count;
            public float timer;
            public bool announced;
        }
        private readonly List<TierState> tierStates = new List<TierState>();
        private readonly List<(TargetDummy dummy, float at)> corpses = new List<(TargetDummy, float)>();

        // Boss
        private int bossIndex = -1;
        private TargetDummy boss;
        private bool bossActive, bossKilled, enraged;
        private float bossStart;
        private int bossPhase;
        private float winAt = -1f;

        // ------------------------------------------------------------------ lifecycle

        protected override void OnBegin()
        {
            cfg = config != null ? config : ScriptableObject.CreateInstance<SurvivorConfig>();
            if (cfg.dataVersion < SurvivorConfig.CurrentDataVersion) cfg.Upgrade();
            catalog = cfg.items != null && cfg.items.Count > 0 ? cfg.items : (defaultItems ??= SurvivorItemDefaults.Create());
            duration = cfg.runLength;
            Endless = false;                 // this mode is never endless (GameFlow sets it again before every drill)
            player.Unit.ResetUnit(cfg.playerHealth);

            nextOffer = 0;
            bossActive = bossKilled = enraged = false;
            bossPhase = 0;
            winAt = -1f;
            boss = null;

            if (items != null) items.Restore();
            items = new SurvivorItemSystem(cfg, player);
            if (ui != null) Destroy(ui.gameObject);
            ui = SurvivorUI.Create(player, cfg.itemSlots);
            if (map != null) map.Apply(true);
            BuildPool();
            SubscribeExtra(true);
        }

        protected override void OnStart()
        {
            runDuration = 0f;                // no time limit: the run ends on death or when the boss dies
        }

        protected override void OnDisable()
        {
            SubscribeExtra(false);
            if (ui != null) { ui.CloseOffer(); Destroy(ui.gameObject); ui = null; }
            if (items != null) { items.Restore(); items = null; }
            if (map != null) map.Apply(false);
            base.OnDisable();
        }

        private void SubscribeExtra(bool on)
        {
            if (on == subscribedExtra) return;
            PlayerSkillSystem pss = PlayerSkillSystem.Instance;
            if (pss == null) return;
            if (on) { pss.AbilityHit += AbilityHitHandler; pss.AttackLanded += AttackLandedHandler; }
            else { pss.AbilityHit -= AbilityHitHandler; pss.AttackLanded -= AttackLandedHandler; }
            subscribedExtra = on;
        }

        protected override void OnTick(float dt)
        {
            if (!player.Unit.IsAlive)
            {
                Finish();                    // lose
                return;
            }
            if (winAt > 0f && elapsed >= winAt)
            {
                Finish();                    // win: the boss is down
                return;
            }
            DebugSkip();
            if (cfg.playerRegenPerSecond > 0f) player.Unit.Heal(cfg.playerRegenPerSecond * dt);
            items.OnTick(dt);
            TickItemKeys();
            TickCorpses();
            TickOffers();
            TickSpawns(dt);
            TickBoss();
            ui.Refresh(items);
        }

        // ------------------------------------------------------------------ item actives (slot keys)

        private bool wasInStasis;

        private void TickItemKeys()
        {
            // Stasis: no moving, attacking or casting until it ends.
            if (items.InStasis)
            {
                if (player.InputEnabled) player.InputEnabled = false;
                wasInStasis = true;
                return;
            }
            if (wasInStasis)
            {
                wasInStasis = false;
                if (!ui.OfferOpen) player.InputEnabled = true;
            }
            InputManager input = InputManager.Instance;
            if (input == null || input.SuppressShortcuts || !player.InputEnabled || ui.OfferOpen) return;
            int count = Mathf.Min(5, cfg.itemSlots);
            for (int i = 0; i < count; i++)
            {
                if (input.Pressed(GameAction.Item1 + i)) items.Activate(i);
            }
        }

        // ------------------------------------------------------------------ item offers

        private void TickOffers()
        {
            if (ui.OfferOpen || bossActive || bossKilled) return;
            bool slotsLeft = items.owned.Count < cfg.itemSlots;
            if (nextOffer < cfg.offerTimes.Count && slotsLeft)
            {
                float at = cfg.offerTimes[nextOffer];
                ui.SetNext("NEXT ITEM  " + Clock(Mathf.Max(0f, at - elapsed)));
                if (elapsed >= at) OpenOffer();
            }
            else
            {
                StartBoss();
            }
        }

        private List<SurvivorItem> Eligible()
        {
            var list = new List<SurvivorItem>();
            foreach (SurvivorItem it in catalog) if (items.CanTake(it)) list.Add(it);
            return list;
        }

        private void OpenOffer()
        {
            List<SurvivorItem> pool = Eligible();
            if (pool.Count == 0) { nextOffer++; return; }
            var picks = new List<SurvivorItem>();
            var bag = new List<SurvivorItem>(pool);
            while (picks.Count < cfg.cardsPerOffer && bag.Count > 0)
            {
                int i = Random.Range(0, bag.Count);
                picks.Add(bag[i]);
                bag.RemoveAt(i);
            }
            var cards = new List<SurvivorUI.Card>();
            foreach (SurvivorItem it in picks) cards.Add(new SurvivorUI.Card { item = it });
            cards.Add(new SurvivorUI.Card { item = null });   // Random
            ui.ShowOffer(cards, cfg.randomBonus, index =>
            {
                SurvivorItem chosen = cards[index].item;
                bool random = chosen == null;
                if (random)
                {
                    List<SurvivorItem> any = Eligible();
                    chosen = any.Count > 0 ? any[Random.Range(0, any.Count)] : null;
                }
                if (chosen != null)
                {
                    items.Add(chosen, random ? cfg.randomBonus : 1f, random);
                    MobaTrainer.Audio.GameSfx.Play("item_pick", 0.8f, 0f);
                    ui.Announce(random ? $"RANDOM: {chosen.displayName.ToUpperInvariant()}  (+{Mathf.RoundToInt((cfg.randomBonus - 1f) * 100f)}%)" : chosen.displayName.ToUpperInvariant());
                }
                nextOffer++;
            });
        }

        // ------------------------------------------------------------------ pool

        private void BuildPool()
        {
            tierStates.Clear();
            corpses.Clear();
            int index = 0;
            foreach (SurvivorEnemyTier t in cfg.tiers)
            {
                if (t == null || t.maxAlive <= 0) continue;
                var st = new TierState { tier = t, first = index, count = t.maxAlive, timer = 0.8f };
                TargetDummy prefab = PrefabFor(t.kind);
                for (int i = 0; i < st.count; i++)
                {
                    // Pre-create (first run only) in index order so each block keeps its own prefab.
                    TargetDummy d = Enemy(index + i, playerSpawn + Vector3.forward * 40f, t.health, prefab);
                    d.respawn = false;
                    d.gameObject.SetActive(false);
                }
                index += st.count;
                tierStates.Add(st);
            }
            bossIndex = index;
            TargetDummy b = Enemy(bossIndex, playerSpawn + Vector3.forward * 40f, cfg.bossHealth, firePrefab);
            b.respawn = false;
            b.gameObject.SetActive(false);
            HideEnemiesFrom(bossIndex + 1);
            AliveCount = 0;
        }

        private TargetDummy PrefabFor(SurvivorEnemyKind kind)
        {
            switch (kind)
            {
                case SurvivorEnemyKind.Swarm: return swarmPrefab != null ? swarmPrefab : meleePrefab;
                case SurvivorEnemyKind.Melee: return meleePrefab;
                case SurvivorEnemyKind.Cards: return cardsPrefab != null ? cardsPrefab : bluePrefab;
                case SurvivorEnemyKind.BlueFan:
                case SurvivorEnemyKind.Beam: return bluePrefab;
                case SurvivorEnemyKind.FireMage: return firePrefab;
                default: return null;        // red caster = default dummy
            }
        }

        // ------------------------------------------------------------------ spawning

        private void TickSpawns(float dt)
        {
            float rate = 1f + cfg.spawnRampPerMinute * Minutes;   // +5% per minute by default
            if (bossActive) rate *= cfg.spawnRateDuringBoss;
            foreach (TierState st in tierStates)
            {
                if (elapsed < st.tier.unlockTime) continue;
                if (!st.announced)
                {
                    st.announced = true;
                    if (st.tier.unlockTime > 0f) ui.Announce("NEW ENEMY: " + st.tier.name.ToUpperInvariant());
                }
                st.timer -= dt * rate;
                if (st.timer > 0f) continue;
                if (AliveCount >= cfg.maxAliveTotal || Alive(st) >= st.count)
                {
                    st.timer = 0.25f;        // full: try again shortly
                    continue;
                }
                st.timer = Mathf.Max(0.1f, st.tier.spawnInterval);
                // Groups grow over time (e.g. swarms of 2, then 3, 4 …), capped by the alive limits.
                int group = Mathf.Max(1, Mathf.FloorToInt(st.tier.groupSize + st.tier.groupPerMinute * Minutes));
                for (int g = 0; g < group; g++)
                {
                    if (AliveCount >= cfg.maxAliveTotal || Alive(st) >= st.count) break;
                    Spawn(st);
                }
            }
        }

        private int Alive(TierState st)
        {
            int n = 0;
            for (int i = st.first; i < st.first + st.count && i < enemies.Count; i++)
                if (enemies[i].gameObject.activeSelf) n++;
            return n;
        }

        private void Spawn(TierState st)
        {
            int slot = -1;
            for (int i = st.first; i < st.first + st.count; i++)
            {
                if (!enemies[i].gameObject.activeSelf) { slot = i; break; }
            }
            if (slot < 0) return;

            SurvivorEnemyTier t = st.tier;
            float hp = t.health * (1f + cfg.healthRampPerMinute * Minutes);
            Vector3 pos = RandomPointAround(player.Unit.Position, cfg.spawnDistance.x, cfg.spawnDistance.y);
            TargetDummy d = Enemy(slot, pos, hp, PrefabFor(t.kind));
            d.respawn = false;
            d.transform.localScale = Vector3.one * t.scale;
            if (t.kind == SurvivorEnemyKind.Swarm) MakeCheap(d);

            EnemyBrain b = d.GetComponent<EnemyBrain>();
            items.ForgetSlow(b);
            Configure(b, t);
            // Mixed-style: enemies sidestep your skillshots, so aiming matters (swarm never does).
            b.dodgeChance = t.kind == SurvivorEnemyKind.Swarm ? 0f : (t.kind == SurvivorEnemyKind.Melee ? cfg.enemyDodgeChance * 0.6f : cfg.enemyDodgeChance);
            b.ResetBrain();
            b.Active = Running;
            AliveCount++;
        }

        /// <summary>Same skill set-ups as Mixed Mode, with this tier's damage and speed.</summary>
        private void Configure(EnemyBrain b, SurvivorEnemyTier t)
        {
            b.moveSpeed = t.moveSpeed;
            b.jukeInterval = new Vector2(0.35f, 1f);
            b.showLane = false;
            b.castCircles = false;
            switch (t.kind)
            {
                case SurvivorEnemyKind.Swarm:
                    b.chase = true;
                    b.castLines = false;
                    b.lunge = false;
                    b.meleeDamage = t.damage;
                    b.meleeCooldown = 1f;
                    break;
                case SurvivorEnemyKind.Melee:
                    b.chase = true;
                    b.castLines = false;
                    b.lunge = true;
                    b.lungeRange = 4.5f;
                    b.lungeDistance = 2.6f;
                    b.lungeSpeed = 16f;
                    b.lungeWindup = 0.25f;
                    b.lungeCooldown = 6f;     // long cooldowns: one clear lunge, then a window to punish
                    b.meleeDamage = t.damage;
                    b.meleeCooldown = 1.2f;
                    break;
                case SurvivorEnemyKind.RedOrb:
                    b.castLines = true;
                    b.preferredRange = new Vector2(7.5f, 11f);
                    b.lineInterval = new Vector2(5f, 7f);
                    b.lineRange = 13f;
                    b.lineWidth = 0.9f;
                    b.lineSpeed = 11f;
                    b.lineWindup = 0.5f;
                    b.lineDamage = t.damage;
                    b.leadChance = 0.35f;
                    break;
                case SurvivorEnemyKind.BlueFan:
                    b.castLines = true;
                    b.preferredRange = new Vector2(7.5f, 11f);
                    b.lineInterval = new Vector2(7f, 9f);
                    b.lineRange = 13f;
                    b.shotStyle = EnemyBrain.ShotStyle.Fan;
                    b.fanCount = 3;
                    b.fanSpread = 30f;
                    b.lineWidth = 0.6f;
                    b.lineSpeed = 13f;
                    b.lineWindup = 0.55f;
                    b.lineDamage = t.damage;
                    b.leadChance = 0.2f;
                    break;
                case SurvivorEnemyKind.Cards:
                    SetupCards(b, t.damage, new Vector2(7f, 9f));
                    b.preferredRange = new Vector2(7.5f, 11f);
                    break;
                case SurvivorEnemyKind.Beam:
                    // Light beam: rare. A thin light line shows the path for 1 s, then the wide beam fires.
                    b.castLines = true;
                    b.shotStyle = EnemyBrain.ShotStyle.Beam;
                    b.preferredRange = new Vector2(9f, 13f);
                    b.lineInterval = new Vector2(cfg.beamInterval.x, cfg.beamInterval.y);
                    b.lineRange = cfg.beamRange;
                    b.lineWidth = cfg.beamWidth;
                    b.lineWindup = cfg.beamWindup;
                    b.lineDamage = t.damage;
                    b.leadChance = 0.15f;
                    break;
                case SurvivorEnemyKind.FireMage:
                    SetupFirePillar(b, t.damage, new Vector2(7f, 9f));
                    DodgeablePillar(b, cfg.pillarRadius, cfg.pillarDelay);
                    break;
            }
        }

        /// <summary>
        /// Extreme Survivor's flame pillar: a bit smaller and slower to erupt than the Mixed / Dodge one,
        /// so you can walk out of it without Blink.
        /// </summary>
        private static void DodgeablePillar(EnemyBrain b, float radius, float delay)
        {
            b.circleRadius = radius;
            b.circleDelay = delay;
        }

        /// <summary>Swarm enemies: no shadows (they come in numbers).</summary>
        private static void MakeCheap(TargetDummy d)
        {
            foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void TickCorpses()
        {
            for (int i = corpses.Count - 1; i >= 0; i--)
            {
                if (elapsed < corpses[i].at) continue;
                if (corpses[i].dummy != null) corpses[i].dummy.gameObject.SetActive(false);
                corpses.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ boss

        private void StartBoss()
        {
            if (bossActive || bossKilled || bossIndex < 0) return;
            bossActive = true;
            bossStart = elapsed;
            Vector3 pos = RandomPointAround(player.Unit.Position, 11f, 13f);
            boss = Enemy(bossIndex, pos, cfg.bossHealth, firePrefab);
            boss.respawn = false;
            boss.transform.localScale = Vector3.one * cfg.bossScale;
            boss.radius = cfg.bossRadius;
            EnemyBrain b = boss.GetComponent<EnemyBrain>();
            items.ForgetSlow(b);
            bossPhase = 0;
            SetBossPhase(1);
            b.Active = Running;
            ui.Announce("THE EMBER COLOSSUS AWAKENS", 3.5f);
            ui.SetNext("");
        }

        private void TickBoss()
        {
            if (!bossActive || boss == null) { ui.SetBoss(false, 0f, ""); return; }
            float h = boss.Health01;
            int phase = h > 0.66f ? 1 : (h > 0.33f ? 2 : 3);
            if (!enraged && elapsed >= bossStart + cfg.enrageAfterBoss)
            {
                enraged = true;
                ui.Announce("ENRAGED", 2.5f);
                SetBossPhase(phase, true);
            }
            else if (phase != bossPhase)
            {
                SetBossPhase(phase);
            }
            string label = "EMBER COLOSSUS" + (enraged ? "  ·  ENRAGED" : "") + $"   {Mathf.CeilToInt(boss.Health)} / {Mathf.RoundToInt(boss.maxHealth)}";
            ui.SetBoss(boss.IsAlive, h, label);
            if (!enraged)
                ui.SetNext("ENRAGE IN  " + Clock(bossStart + cfg.enrageAfterBoss - elapsed));
            else
                ui.SetNext("");
        }

        /// <summary>Phase 1: flame pillars. Phase 2: wide fans + slower pillars. Phase 3: dash-lunges + fans.</summary>
        private void SetBossPhase(int phase, bool force = false)
        {
            if (boss == null || (phase == bossPhase && !force)) return;
            bossPhase = phase;
            EnemyBrain b = boss.GetComponent<EnemyBrain>();
            float k = enraged ? 1f / Mathf.Max(1f, cfg.enrageSpeedUp) : 1f;   // interval multiplier
            float speed = cfg.bossMoveSpeed;   // enrage = attacks more often, never faster movement

            b.chase = false;
            b.lunge = false;
            b.castLines = false;
            b.castCircles = false;
            b.showLane = false;
            b.jukeInterval = new Vector2(0.6f, 1.4f);

            if (phase == 1)
            {
                SetupFirePillar(b, cfg.bossPillarDamage, new Vector2(3.5f, 4.5f) * k);
                DodgeablePillar(b, cfg.bossPillarRadius, cfg.pillarDelay);
                b.circleRange = 11f;
                b.preferredRange = new Vector2(7f, 10f);
            }
            else
            {
                // Light beams (phase 2 also keeps slower pillars).
                b.castLines = true;
                b.shotStyle = EnemyBrain.ShotStyle.Beam;
                b.lineWidth = cfg.beamWidth * 1.2f;
                b.lineWindup = cfg.beamWindup;
                b.lineRange = cfg.beamRange + 2f;
                b.lineDamage = cfg.bossFanDamage;
                b.leadChance = 0.25f;
                b.lineInterval = (phase == 2 ? new Vector2(5f, 7f) : new Vector2(6f, 8f)) * k;
                b.preferredRange = new Vector2(6f, 9f);
                if (phase == 2)
                {
                    b.castCircles = true;
                    b.circleLook = 1;
                    b.circleRange = 11f;
                    b.circleRadius = cfg.bossPillarRadius;
                    b.circleAimSpread = 1.8f;
                    b.circleCastTime = 0.25f;
                    b.circleDelay = cfg.pillarDelay;
                    b.circleDamage = cfg.bossPillarDamage;
                    b.circleInterval = new Vector2(6f, 8f) * k;
                }
                else
                {
                    b.chase = true;
                    b.lunge = true;
                    b.lungeRange = 7f;
                    b.lungeDistance = 5f;
                    b.lungeSpeed = 20f;
                    b.lungeWindup = 0.4f;
                    b.lungeCooldown = 5f * k;
                    b.meleeDamage = cfg.bossLungeDamage;
                    b.meleeCooldown = 1f;
                }
            }
            items.SetBaseSpeed(b, speed);
            b.dodgeChance = cfg.enemyDodgeChance * 0.5f;
            b.ResetBrain();
            if (phase > 1) ui.Announce(phase == 2 ? "THE COLOSSUS SPLITS THE SKY" : "THE COLOSSUS CHARGES", 2f);
        }

        // ------------------------------------------------------------------ debug

        private void DebugSkip()
        {
            if (!debugSkip || !Debug.isDebugBuild) return;
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.f9Key.wasPressedThisFrame) return;
            float skip = kb.shiftKey.isPressed ? debugSkipSeconds * 3f : debugSkipSeconds;
            elapsed += skip;
            Debug.Log($"[Extreme Survivor] Debug skip → {Clock(elapsed)}");
        }

        private static string Clock(float t) => $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";

        // ------------------------------------------------------------------ hooks → items + stats

        private int SlotOf(AbilityDefinition ability)
        {
            var slots = player.Abilities.slots;
            for (int i = 0; i < slots.Length && i < 3; i++) if (slots[i].ability == ability) return i;
            return -1;
        }

        private void AbilityHitHandler(AbilityDefinition ability, Unit hit, float baseDamage)
        {
            if (!Running || items == null || hit == null || hit.team == player.Unit.team) return;
            int slot = SlotOf(ability);
            if (slot < 0) return;
            // The W marking shot deals its damage later (when the mark bursts), so no ratio here.
            items.OnSkillHit(ability.marksTarget ? -1 : slot, hit, baseDamage, ability);
        }

        private void AttackLandedHandler(Unit target, float dealt)
        {
            if (!Running || items == null) return;
            items.OnAttackHit(target, dealt);
        }

        protected override void OnAbilityCast(int slot, AbilityDefinition ability, Vector3 point)
        {
            if (IsSkillshot(ability)) record.casts++;
            items?.OnSkillCast(slot);
        }

        protected override void OnSkillshotResolved(AbilityDefinition ability, Unit hit)
        {
            if (hit != null && IsSkillshot(ability)) record.hits++;
        }

        /// <summary>Aimed abilities that count toward accuracy (Wayfarer Q/W, Arcanist Q/W/E).</summary>
        private static bool IsSkillshot(AbilityDefinition a) =>
            a != null && (a.shape == AbilityShape.LinearSkillshot || a.shape == AbilityShape.ChargedBeam || a.shape == AbilityShape.CircleSkillshot);

        protected override void OnMarkDetonated(Unit target, float burst)
        {
            record.procs++;
            items?.OnMarkBurst(target, burst);
        }

        protected override void OnEnemyDamaged(Unit unit, float amount, Unit source) => record.damageDealt += amount;

        protected override void OnEnemyDied(Unit unit, Unit killer)
        {
            record.kills++;
            if (boss != null && unit == boss)
            {
                bossKilled = true;
                bossActive = false;
                MobaTrainer.Audio.GameSfx.Play("boss_death", 1f, 0f, 0f);
                winAt = elapsed + 1.5f;
                ui.Announce("VICTORY", 3f);
                ui.SetBoss(false, 0f, "");
                return;
            }
            AliveCount = Mathf.Max(0, AliveCount - 1);
            items?.OnKill(unit);
            if (unit is TargetDummy d) corpses.Add((d, elapsed + cfg.corpseTime));
        }

        protected override void OnEnemyShot(Unit hit)
        {
            if (hit == player.Unit) record.hitsTaken++;
            else if (hit == null) record.dodged++;
        }

        protected override void OnPlayerDamaged(Unit unit, float amount, Unit source)
        {
            record.damageTaken += amount;
            items?.OnTakeDamage(amount, source);
            if (source == null) return;
            EnemyBrain brain = source.GetComponent<EnemyBrain>();
            if (brain != null && brain.chase) record.hitsTaken++;
        }

        protected override void FillRecord(RunRecord r)
        {
            if (ui != null) { ui.CloseOffer(); ui.SetBoss(false, 0f, ""); }
            r.won = bossKilled && player.Unit.IsAlive;
            // 10 points per second survived + 50 per kill, + boss bonus.
            r.score = Mathf.Max(0, Mathf.FloorToInt(r.duration) * 10 + r.kills * 50 + (r.won ? cfg.bossKillScore : 0));
        }
    }
}
