using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>Which Mixed Mode enemy a tier uses (model + AI + skill).</summary>
    public enum SurvivorEnemyKind
    {
        Swarm,        // small melee chaser, no lunge (cheap: no shadows)
        RedOrb,       // red caster: slow orb skillshot
        Melee,        // armoured blade: walks at you and lunges
        BlueFan,      // blue caster: fan of 3 shards
        FireMage,     // fire mage: flame pillar (ground circle)
        Beam,         // light caster: rare long light beam with a 1 s warning line
        Cards,        // card thrower: three slow cards in a wide cone
    }

    [Serializable]
    public class SurvivorEnemyTier
    {
        public string name = "Tier";
        public SurvivorEnemyKind kind = SurvivorEnemyKind.Swarm;
        [Tooltip("Run time (seconds) when this tier starts spawning. 180 = 3:00.")]
        public float unlockTime = 0f;
        public float health = 100f;
        [Tooltip("Damage of its skillshot / pillar / melee hit.")]
        public float damage = 40f;
        [Tooltip("Seconds between spawns of this tier at minute 0 (gets faster each minute, see spawnRampPerMinute).")]
        public float spawnInterval = 4f;
        [Tooltip("How many of this tier may be alive at once.")]
        public int maxAlive = 6;
        public float moveSpeed = 3f;
        [Tooltip("Visual size (1 = normal).")]
        public float scale = 1f;
        [Tooltip("Enemies per spawn at 0:00 (1 = one at a time).")]
        public float groupSize = 1f;
        [Tooltip("Extra enemies per spawn added every minute (0.5 = one more every 2 minutes).")]
        public float groupPerMinute = 0f;
    }

    /// <summary>
    /// Every Extreme Survivor number in one place. Edit the asset at
    /// Assets/_Project/Data/ExtremeSurvivor/SurvivorConfig.asset (scene rebuilds never overwrite it).
    /// All values are starting points for tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "MOBA Trainer/Extreme Survivor Config", fileName = "SurvivorConfig")]
    public class SurvivorConfig : ScriptableObject
    {
        [Tooltip("Internal: lets the editor update an older asset to new default values once.")]
        public int dataVersion;

        [Header("Run")]
        [Tooltip("Reference length used for score and the old timed win (the run now ends when the boss dies).")]
        public float runLength = 900f;
        public float playerHealth = 600f;
        [Tooltip("Player health regenerated per second (there is no other healing until items).")]
        public float playerRegenPerSecond = 3f;

        [Header("Player base stats (items add to these)")]
        [Tooltip("Base attack damage (your auto-attack damage before items).")]
        public float baseAD = 40f;
        public float baseAP = 0f;
        [Tooltip("Auto-attacks deal this share of your AD (skills should carry the damage in this mode).")]
        public float autoAttackFactor = 0.6f;
        [Tooltip("Chance that a caster sidesteps one of your skillshots (blades: 60% of this, swarm: never, boss: half).")]
        [UnityEngine.Range(0f, 1f)] public float enemyDodgeChance = 0.45f;
        [Tooltip("Total cooldown reduction can never go above this (0.4 = 40%).")]
        public float cdrCap = 0.4f;

        [Header("Skill ratios: damage = skill base + AD × ratio + AP × ratio")]
        [Tooltip("Q uses TOTAL AD.")]
        public float qAD = 1.3f;
        public float qAP = 0.4f;
        [Tooltip("W (applied when the mark is detonated) uses BONUS AD.")]
        public float wBonusAD = 1.0f;
        public float wAP = 0.9f;
        [Tooltip("E uses BONUS AD.")]
        public float eBonusAD = 0.6f;
        public float eAP = 0.75f;

        [Header("Items")]
        public int itemSlots = 4;
        [Tooltip("Run times (seconds) when the game pauses and offers items.")]
        public List<float> offerTimes = new List<float> { 60f, 120f, 180f, 240f };
        public int cardsPerOffer = 3;
        [Tooltip("The Random card gives an unowned item with its stats multiplied by this.")]
        public float randomBonus = 1.1f;
        [Tooltip("All items that can be offered. Filled automatically with the default set.")]
        public List<SurvivorItem> items = new List<SurvivorItem>();

        [Header("Boss (appears after the last item pick)")]
        public float bossHealth = 8000f;
        public float bossScale = 2.5f;
        public float bossRadius = 1.6f;
        public float bossPillarDamage = 110f;
        public float bossFanDamage = 70f;
        public float bossLungeDamage = 140f;
        public float bossMoveSpeed = 2.6f;
        [Tooltip("Boss flame pillar radius (units).")]
        public float bossPillarRadius = 2.0f;

        [Header("Flame pillar (fire mage) in this mode: smaller and slower than the Mixed one, so you can walk out")]
        public float pillarRadius = 1.4f;
        [Tooltip("Seconds between the circle appearing and the eruption.")]
        public float pillarDelay = 0.9f;
        [Header("Light beam (light caster)")]
        [Tooltip("Seconds between beams (min, max). Rare on purpose.")]
        public Vector2 beamInterval = new Vector2(11f, 15f);
        [Tooltip("Warning line time before the beam fires (the caster stands still).")]
        public float beamWindup = 1.0f;
        public float beamWidth = 1.8f;
        public float beamRange = 17f;

        [Tooltip("Normal spawns run at this speed while the boss is alive.")]
        public float spawnRateDuringBoss = 0.5f;
        [Tooltip("Seconds after the boss appears until it enrages (attacks more often; movement speed stays the same). Not an instant loss.")]
        public float enrageAfterBoss = 180f;
        public float enrageSpeedUp = 1.5f;
        public int bossKillScore = 10000;

        [Header("Spawning")]
        [Tooltip("Spawn rate grows by this fraction every minute (0.05 = +5% per minute).")]
        public float spawnRampPerMinute = 0.06f;
        [Tooltip("Enemy health grows by this fraction every minute, so they keep up with your items.")]
        public float healthRampPerMinute = 0.04f;
        [Tooltip("Hard cap on enemies alive at once (performance).")]
        public int maxAliveTotal = 12;
        [Tooltip("Enemies appear on a ring this far from you (min, max).")]
        public Vector2 spawnDistance = new Vector2(12f, 16f);
        [Tooltip("Dead enemies disappear after this many seconds.")]
        public float corpseTime = 0.6f;

        [Header("Enemy tiers (a new, stronger one every 3:00)")]
        public List<SurvivorEnemyTier> tiers = DefaultTiers();

        /// <summary>Calm defaults: few enemies, slow spawns, a new type every minute or so.</summary>
        public static List<SurvivorEnemyTier> DefaultTiers() => new List<SurvivorEnemyTier>
        {
            // Mixed-style: few enemies that juke and sidestep, so landing skillshots is what matters.
            new SurvivorEnemyTier { name = "Swarm",        kind = SurvivorEnemyKind.Swarm,    unlockTime = 0f,   health = 60f,  damage = 25f,  spawnInterval = 7f,  maxAlive = 5, moveSpeed = 2.8f, scale = 0.75f, groupSize = 1f, groupPerMinute = 0.25f },
            new SurvivorEnemyTier { name = "Orb Caster",   kind = SurvivorEnemyKind.RedOrb,   unlockTime = 0f,   health = 150f, damage = 60f,  spawnInterval = 8f,  maxAlive = 3, moveSpeed = 3.0f, scale = 1f },
            new SurvivorEnemyTier { name = "Blade",        kind = SurvivorEnemyKind.Melee,    unlockTime = 45f,  health = 240f, damage = 80f,  spawnInterval = 12f, maxAlive = 2, moveSpeed = 2.7f, scale = 1f },
            new SurvivorEnemyTier { name = "Card Thrower", kind = SurvivorEnemyKind.Cards,    unlockTime = 90f,  health = 200f, damage = 55f,  spawnInterval = 12f, maxAlive = 2, moveSpeed = 3.0f, scale = 1f },
            new SurvivorEnemyTier { name = "Light Caster", kind = SurvivorEnemyKind.Beam,     unlockTime = 150f, health = 200f, damage = 130f, spawnInterval = 16f, maxAlive = 2, moveSpeed = 3.0f, scale = 1f },
            new SurvivorEnemyTier { name = "Fire Mage",    kind = SurvivorEnemyKind.FireMage, unlockTime = 180f, health = 280f, damage = 90f,  spawnInterval = 15f, maxAlive = 2, moveSpeed = 2.8f, scale = 1.05f },
        };

        public const int CurrentDataVersion = 7;

        /// <summary>v2: calm spawns, v3: groups and dodgeable pillars, an item every minute, boss after the 4th item, enrage 3:00 after it appears.</summary>
        public void Upgrade()
        {
            if (dataVersion >= CurrentDataVersion) return;
            var fresh = CreateInstance<SurvivorConfig>();
            if (dataVersion < 2)
            {
                offerTimes = new List<float>(fresh.offerTimes);
                enrageAfterBoss = fresh.enrageAfterBoss;
            }
            // v3: swarms come in growing groups, spawns ramp faster, dodgeable pillars, shorter hourglass cooldown.
            tiers = DefaultTiers();
            maxAliveTotal = fresh.maxAliveTotal;
            spawnRampPerMinute = fresh.spawnRampPerMinute;
            pillarRadius = fresh.pillarRadius;
            pillarDelay = fresh.pillarDelay;
            bossPillarRadius = fresh.bossPillarRadius;
            foreach (SurvivorItem it in items)
            {
                if (it == null || it.id != "stillsand_hourglass") continue;
                it.activeCooldown = 45f;
                it.activeText = SurvivorItemDefaults.HourglassText;
            }
            // v5: Mixed-style (fewer enemies that sidestep, card thrower, weaker autos).
            spawnRampPerMinute = fresh.spawnRampPerMinute;
            maxAliveTotal = fresh.maxAliveTotal;
            autoAttackFactor = fresh.autoAttackFactor;
            enemyDodgeChance = fresh.enemyDodgeChance;
            // v4: light caster instead of the fan, smaller pillars, long enemy cooldowns (in code), item buffs.
            beamInterval = fresh.beamInterval;
            beamWindup = fresh.beamWindup;
            beamWidth = fresh.beamWidth;
            beamRange = fresh.beamRange;
            foreach (SurvivorItem it in items)
            {
                if (it == null || it.id != "focus_lance") continue;
                it.cdr = 0.15f;
                it.p1 = 0.45f;
                it.passiveText = SurvivorItemDefaults.FocusLanceText;
            }
            // v6: removed Edge of Momentum, Echo Charm, Swiftcast Prism, Focus Lance and Lastlight Bow;
            // Bramble Plate reflects damage onto the attacker instead of a shockwave.
            // v7: removed Thunder Blade.
            var removed = new HashSet<string> { "edge_of_momentum", "echo_charm", "swiftcast_prism", "focus_lance", "lastlight_bow", "thunder_blade" };
            items.RemoveAll(it => it == null || removed.Contains(it.id));
            foreach (SurvivorItem it in items)
            {
                if (it.id == "bramble_plate") it.passiveText = SurvivorItemDefaults.BrambleText;
            }
            dataVersion = CurrentDataVersion;
            DestroyImmediate(fresh);
        }
    }
}
