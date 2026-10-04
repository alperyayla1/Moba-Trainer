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

        [Header("Casting")]
        [Tooltip("Self-cast abilities ignore the cast-mode setting and fire on press.")]
        public bool ignoresCastMode = false;

        public bool IsSelfCast => shape == AbilityShape.SelfShield || ignoresCastMode;
    }
}
