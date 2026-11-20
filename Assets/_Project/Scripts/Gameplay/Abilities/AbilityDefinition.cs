using UnityEngine;

namespace MobaTrainer.Gameplay
{
    public enum AbilityShape
    {
        LinearSkillshot = 0,  // projectile along a line, stops at first enemy hit
        CircleSkillshot = 1,  // delayed circle at target point
        Dash = 2,             // fast move in cursor direction
        Blink = 3,            // instant reposition up to range
        SelfShield = 4,       // instant shield on self
        ShiftBolt = 5,        // blink toward cursor, then a homing bolt at the nearest enemy
        ChargedBeam = 6,      // hold to charge (range grows, caster slowed), release: delayed piercing beam
    }

    /// <summary>Data for one player ability. Generic names only — no names from existing games.</summary>
    [CreateAssetMenu(menuName = "MOBA Trainer/Ability Definition", fileName = "Ability")]
    public class AbilityDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Ability";
        [Tooltip("Two-letter glyph shown in the HUD slot.")]
        public string glyph = "AB";
        public AbilityShape shape = AbilityShape.LinearSkillshot;

        [Header("Numbers (1 unit = 100 MOBA units)")]
        public float cooldown = 4f;
        [Tooltip("Seconds the caster is locked before the effect fires (movement pauses, direction is locked at key press).")]
        public float castTime = 0f;
        [Tooltip("ShiftBolt: search radius for the homing bolt after the blink.")]
        public float boltRange = 7.5f;
        [Tooltip("Max cast range (distance from caster to the far end / centre).")]
        public float range = 9f;
        [Tooltip("Line WIDTH (linear) or circle radius.")]
        public float size = 0.8f;
        [Tooltip("Projectile speed / dash speed (units per second).")]
        public float speed = 18f;
        [Tooltip("Delay before a circle lands (seconds).")]
        public float delay = 0.5f;
        public float damage = 60f;

        [Header("Mark / proc")]
        [Tooltip("Hit applies a mark instead of damage. The mark's burst damage = this ability's damage.")]
        public bool marksTarget;
        public float markDuration = 4f;
        [Tooltip("Hitting a marked target with this ability detonates the mark (extra damage).")]
        public bool detonatesMark;
        [Tooltip("On hit: every ability cooldown (not Blink) is reduced by this many seconds.")]
        public float cooldownRefundOnHit;

        [Header("Shield")]
        public float shieldAmount = 80f;
        public float shieldDuration = 2.5f;

        [Header("Crowd control (0 = none)")]
        [Tooltip("Slow applied to every enemy hit (0.25 = 25%).")]
        public float slowPct;
        public float slowDuration = 2f;
        [Tooltip("Circle: inner 'bullseye' radius. Enemies inside take centerDamageMult damage and centerSlowPct slow.")]
        public float centerRadius;
        public float centerDamageMult = 1f;
        public float centerSlowPct;
        [Tooltip("Centre slow decays to this over slowDuration (-1 = no decay).")]
        public float centerSlowDecayTo = -1f;
        [Tooltip("Linear: stun on hit, growing from stunMin (point blank) to stunMax (full range).")]
        public float stunMin;
        public float stunMax;

        [Header("Charge (ChargedBeam)")]
        [Tooltip("Beam length when released instantly; grows to 'range' over chargeTime.")]
        public float chargeMinRange = 7.35f;
        [Tooltip("Seconds of holding to reach full range.")]
        public float chargeTime = 1.5f;
        [Tooltip("Holding longer than this cancels the charge (half the cooldown is refunded).")]
        public float chargeMaxHold = 4f;
        [Tooltip("Caster move speed is reduced by up to this while charging (0.5 = 50% at full charge).")]
        public float chargeSelfSlow = 0.4f;
        [Tooltip("After release the caster is locked (no move, attack, cast or Blink) this long; the beam lands at the end of it.")]
        public float releaseLock = 0.528f;

        [Header("Extreme Survivor scaling (0/0 = the mode's default per-slot ratios)")]
        public float survivorADRatio;
        public float survivorAPRatio;

        [Header("Sound")]
        [Tooltip("Resources/SFX clip played on cast (empty = the default sound for this slot).")]
        public string castSound = "";
        [Tooltip("ChargedBeam: clip played when the charge starts.")]
        public string chargeSound = "";

        [Header("Casting")]
        [Tooltip("Self-cast abilities ignore the cast-mode setting and fire on press.")]
        public bool ignoresCastMode = false;

        public bool IsSelfCast => shape == AbilityShape.SelfShield || ignoresCastMode;
    }
}
