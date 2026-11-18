using System.Collections.Generic;
using UnityEngine;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>Items in the same group can't be held together and share one cooldown.</summary>
    public enum ItemGroup { None, Spellblade, Annul }

    /// <summary>Active effect used with the item slot key (1-5 by default).</summary>
    public enum ItemActive
    {
        None,
        Stasis,             // become untouchable for activeDuration s: you can't move, attack or cast
    }

    /// <summary>Which passive the item runs. Its numbers live in the item's p1..p4 fields.</summary>
    public enum ItemPassive
    {
        None,
        SpellbladeStrike,   // after a skill, next attack: +p1 × base AD bonus damage
        SpellbladeFrost,    // after a skill, next attack: +p1 × base AD, frost zone (radius p2, slow p3, lasts p4 s)
        SpellbladeHex,      // after a skill, next attack: +p1 × base AD + p2 × AP, then +p3 move speed for p4 s
        ChillingSkills,     // skill hits slow p1 (0.3 = 30%) for p2 s
        Burn,               // skill hits burn p1 of target max HP per second for p2 s
        ApAmplifier,        // total AP × p1
        TankBreaker,        // skill damage +p1 (0.25 = 25%) against enemies with max HP ≥ p2
        SpellShield,        // blocks the next enemy hit; cooldown restarts when you take damage if p1 = 1
        LowHealthShield,    // below p1 HP (0.3 = 30%): shield of p2 + p3 × max HP for p4 s
        SkillLifesteal,     // skill hits heal p1 (0.12 = 12%) of the damage
        ChargedStrike,      // every cooldown: next hit +p1 + p2 × AD damage and +p3 move speed for p4 s
        Thorns,             // when hit: the attacker takes p2 + p3 × your max HP
        CooldownOnHit,      // skill hits refund p1 s of your other skills' cooldowns
        Momentum,           // each skill cast: +1 stack (max p1), each stack +p2 skill damage; stacks fall off p3 s after the last cast
        Bloodthirst,        // attacks heal p1 of their damage, skill hits heal p2 of theirs
        CastRefund,         // every p1-th skill cast refunds p2 s of all skill cooldowns
        KillRefund,         // each kill refunds p1 s of all skill cooldowns
    }

    /// <summary>
    /// One item (data only). Stats and passive numbers are placeholders meant to be tuned in the
    /// asset files under Assets/_Project/Data/ExtremeSurvivor/Items. Names, text and icons are our own.
    /// </summary>
    [CreateAssetMenu(menuName = "MOBA Trainer/Extreme Survivor Item", fileName = "Item")]
    public class SurvivorItem : ScriptableObject
    {
        public string id = "item";
        public string displayName = "Item";
        [TextArea] public string passiveText = "";
        public ItemGroup group = ItemGroup.None;
        [Tooltip("Shared with the rest of the group when the item has one.")]
        public float cooldown = 0f;

        [Header("Icon")]
        public Sprite icon;
        [Tooltip("Shown when there is no icon sprite.")]
        public string glyph = "IT";
        public Color color = new Color(0.8f, 0.6f, 0.3f);

        [Header("Stats")]
        public float ad;
        public float ap;
        public float health;
        [Tooltip("In game units ×100 (10 = +0.1 units/s), like MOBA move speed numbers.")]
        public float moveSpeed;
        [Tooltip("0.10 = +10% attack speed.")]
        public float attackSpeed;
        [Tooltip("0.10 = 10% cooldown reduction (total capped at 40%).")]
        public float cdr;

        [Header("Passive")]
        public ItemPassive passive;
        public float p1, p2, p3, p4;

        [Header("Active (press the slot key)")]
        public ItemActive active;
        public float activeDuration = 2.5f;
        public float activeCooldown = 90f;
        [TextArea] public string activeText = "";

        public bool HasActive => active != ItemActive.None;

        /// <summary>Stat lines for cards and tooltips.</summary>
        public List<string> StatLines(float mult = 1f)
        {
            var l = new List<string>();
            if (ad != 0f) l.Add($"+{Mathf.RoundToInt(ad * mult)} AD");
            if (ap != 0f) l.Add($"+{Mathf.RoundToInt(ap * mult)} AP");
            if (health != 0f) l.Add($"+{Mathf.RoundToInt(health * mult)} HP");
            if (moveSpeed != 0f) l.Add($"+{Mathf.RoundToInt(moveSpeed * mult)} Move speed");
            if (attackSpeed != 0f) l.Add($"+{Mathf.RoundToInt(attackSpeed * mult * 100f)}% Attack speed");
            if (cdr != 0f) l.Add($"+{Mathf.RoundToInt(cdr * mult * 100f)}% Cooldown reduction");
            return l;
        }

        public string GroupLabel => group == ItemGroup.None ? "" : group.ToString().ToUpperInvariant();
    }

    /// <summary>The default item set (used to create the asset files once, and as a fallback).</summary>
    public static class SurvivorItemDefaults
    {
        private static SurvivorItem Make(string id, string name, string glyph, int color, ItemGroup group, float cd,
            float ad, float ap, float hp, float ms, float aspd, float cdr,
            ItemPassive passive, float p1, float p2, float p3, float p4, string text)
        {
            var it = ScriptableObject.CreateInstance<SurvivorItem>();
            it.name = id;
            it.id = id;
            it.displayName = name;
            it.glyph = glyph;
            it.color = MobaTrainer.Core.Palette.Hex(color);
            it.group = group;
            it.cooldown = cd;
            it.ad = ad; it.ap = ap; it.health = hp; it.moveSpeed = ms; it.attackSpeed = aspd; it.cdr = cdr;
            it.passive = passive;
            it.p1 = p1; it.p2 = p2; it.p3 = p3; it.p4 = p4;
            it.passiveText = text;
            return it;
        }

        public const string BrambleText = "When an enemy hits you, it takes 40 + 3% of your max HP as damage.";
        public const string FocusLanceText = "Skill hits refund 0.45 s of your other skills' cooldowns.";
        public const string HourglassText = "ACTIVE (slot key): turn to gold for 2.5 s. You can't be hurt, but you can't move, attack or cast. 45 s cooldown.";

        private static SurvivorItem WithActive(SurvivorItem it, ItemActive active, float duration, float cooldown, string text)
        {
            it.active = active;
            it.activeDuration = duration;
            it.activeCooldown = cooldown;
            it.activeText = text;
            return it;
        }

        public static List<SurvivorItem> Create()
        {
            return new List<SurvivorItem>
            {
                // ---- Spellblade group (one at a time, shared cooldown)
                Make("threefold_edge", "Threefold Edge", "TE", 0xE0A040, ItemGroup.Spellblade, 1.5f,
                    20, 0, 150, 0, 0.10f, 0,
                    ItemPassive.SpellbladeStrike, 2.0f, 0, 0, 0,
                    "After a skill, your next attack deals bonus damage equal to 200% of base AD."),
                Make("rime_gauntlet", "Rime Gauntlet", "RG", 0x7FC8FF, ItemGroup.Spellblade, 1.5f,
                    15, 0, 200, 0, 0, 0,
                    ItemPassive.SpellbladeFrost, 1.0f, 2.2f, 0.3f, 2f,
                    "After a skill, your next attack deals 100% base AD bonus damage and leaves a frost field that slows enemies by 30%."),
                Make("grave_hex", "Grave Hex", "GH", 0x9B6BFF, ItemGroup.Spellblade, 1.5f,
                    0, 40, 0, 10, 0, 0,
                    ItemPassive.SpellbladeHex, 0.75f, 0.5f, 0.2f, 1.5f,
                    "After a skill, your next attack deals 75% base AD + 50% AP bonus damage and gives 20% move speed for 1.5 s."),

                // ---- Annul group (one at a time, shared cooldown)
                Make("ward_veil", "Ward Veil", "WV", 0x5FD3C4, ItemGroup.Annul, 25f,
                    0, 25, 100, 0, 0, 0,
                    ItemPassive.SpellShield, 1f, 0, 0, 0,
                    "Blocks the next enemy hit. Recharges in 25 s; taking damage restarts the recharge."),
                Make("dusk_ward", "Dusk Ward", "DW", 0x4B6EAF, ItemGroup.Annul, 25f,
                    25, 0, 100, 0, 0, 0,
                    ItemPassive.SpellShield, 1f, 0, 0, 0,
                    "Blocks the next enemy hit. Recharges in 25 s; taking damage restarts the recharge."),

                // ---- AP
                Make("frostweave_scepter", "Frostweave Scepter", "FS", 0x9FD8FF, ItemGroup.None, 0f,
                    0, 30, 200, 0, 0, 0,
                    ItemPassive.ChillingSkills, 0.3f, 1f, 0, 0,
                    "Skill hits slow enemies by 30% for 1 s."),
                Make("cinder_veil", "Cinder Veil", "CV", 0xFF7A3A, ItemGroup.None, 0f,
                    0, 35, 100, 0, 0, 0,
                    ItemPassive.Burn, 0.015f, 3f, 0, 0,
                    "Skill hits burn the target for 1.5% of its max HP per second for 3 s."),
                Make("crown_of_ascendance", "Crown of Ascendance", "CA", 0xC77DFF, ItemGroup.None, 0f,
                    0, 50, 0, 0, 0, 0,
                    ItemPassive.ApAmplifier, 1.3f, 0, 0, 0,
                    "Increases your total AP by 30%."),
                Make("hollow_rod", "Hollow Rod", "HR", 0x8A5CF0, ItemGroup.None, 0f,
                    0, 30, 0, 0, 0, 0,
                    ItemPassive.TankBreaker, 0.25f, 250f, 0, 0,
                    "Skills deal 25% more damage to enemies with 250 or more max HP."),

                // ---- AD
                Make("gnawing_fang", "Gnawing Fang", "GF", 0xD94A4A, ItemGroup.None, 0f,
                    25, 0, 0, 0, 0, 0,
                    ItemPassive.SkillLifesteal, 0.12f, 0, 0, 0,
                    "Skill hits heal you for 12% of the damage dealt."),

                // ---- Tank / hybrid
                Make("bramble_plate", "Bramble Plate", "BP", 0x6E9B4A, ItemGroup.None, 0.8f,
                    0, 0, 300, 0, 0, 0,
                    ItemPassive.Thorns, 3f, 40f, 0.03f, 0,
                    SurvivorItemDefaults.BrambleText),

                // ---- More sustain and cooldown builds
                Make("sanguine_blade", "Sanguine Blade", "SB", 0xB0243A, ItemGroup.None, 0f,
                    35, 0, 0, 0, 0, 0,
                    ItemPassive.Bloodthirst, 0.15f, 0.08f, 0, 0,
                    "Attacks heal you for 15% of their damage, skill hits for 8%."),
                WithActive(Make("stillsand_hourglass", "Stillsand Hourglass", "SH", 0xE8C46A, ItemGroup.None, 0f,
                    0, 45, 150, 0, 0, 0,
                    ItemPassive.None, 0, 0, 0, 0,
                    ""),
                    ItemActive.Stasis, 2.5f, 45f, HourglassText),
            };
        }
    }
}
