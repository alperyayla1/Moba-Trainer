using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Blink reaction (60 s): a danger circle appears under you and lands after a short delay —
    /// too big to walk out of, so Blink or Shift out. Cooldowns are shortened for this drill.
    /// The delay shrinks over time. Score = 10 per escape − 40 per hit (min 0). You cannot die.
    /// </summary>
    public class BlinkScenario : Scenario
    {
        public float circleRadius = 2.3f;
        public float cooldownScale = 0.3f;

        public override string Id => "blink";
        public override string Title => "Blink";
        public override int Hits => record.dodged;
        public override int Misses => record.hitsTaken;

        private TargetDummy caster;
        private float timer;

        protected override void OnBegin()
        {
            player.Unit.invulnerable = true;
            player.Abilities.cooldownScale = cooldownScale;
            caster = Enemy(0, Vector3.zero, 1000f);
            caster.invulnerable = true;
            caster.GetComponent<EnemyBrain>().move = false;
            if (caster.visualRoot != null) caster.visualRoot.SetActive(false);
            caster.transform.position = new Vector3(0f, 0f, 80f);   // off the map: only its circles matter
            HideEnemiesFrom(1);
            timer = 1f;
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            float t = Time01;
            float delay = Mathf.Lerp(0.75f, 0.45f, t);
            timer = delay + Mathf.Lerp(1.3f, 0.7f, t);
            EnemySkillSystem.Instance?.FireCircle(caster, player.Unit.Position, circleRadius, delay, 1f);
        }

        protected override void OnEnemyShot(Unit hit)
        {
            if (hit == player.Unit) record.hitsTaken++;
            else if (hit == null) record.dodged++;
        }

        protected override void FillRecord(RunRecord r)
        {
            r.score = Mathf.Max(0, r.dodged * 10 - r.hitsTaken * 40);
            r.won = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (player != null) player.Abilities.cooldownScale = 1f;
            if (caster != null && caster.visualRoot != null) caster.visualRoot.SetActive(true);
        }
    }
}
