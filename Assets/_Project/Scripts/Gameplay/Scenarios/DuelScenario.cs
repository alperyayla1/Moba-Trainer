using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Duel: you vs one enemy marksman with the same health. It jukes, throws line skillshots
    /// (with a cast time) and the odd circle. First to zero loses. Untimed (2-minute cap).
    /// Score on a win = 1000 + remaining health % × 10 + speed bonus; on a loss = damage dealt % × 5.
    /// </summary>
    public class DuelScenario : Scenario
    {
        public float health = 900f;

        public override string Id => "duel";
        public override string Title => "Duel";
        public override int Hits => record.hits;
        public override int Misses => record.hitsTaken;

        private TargetDummy foe;

        protected override void OnBegin()
        {
            duration = 120f;
            player.Unit.ResetUnit(health);
            foe = Enemy(0, playerSpawn + new Vector3(0f, 0f, 10f), health);
            foe.respawn = false;
            EnemyBrain b = foe.GetComponent<EnemyBrain>();
            b.preferredRange = new Vector2(7f, 10.5f);
            b.moveSpeed = 3.25f;
            b.jukeInterval = new Vector2(0.3f, 0.95f);
            b.castLines = true;
            b.lineInterval = new Vector2(1.5f, 2.4f);
            b.lineWindup = 0.25f;
            b.lineSpeed = 20f;
            b.lineWidth = 1.2f;
            b.lineRange = 12f;
            b.lineDamage = 90f;
            b.leadChance = 0.55f;
            b.castCircles = true;
            b.circleInterval = new Vector2(4f, 6f);
            b.circleRadius = 1.6f;
            b.circleDelay = 0.75f;
            b.circleDamage = 70f;
            b.ResetBrain();
            HideEnemiesFrom(1);
        }

        protected override void OnAbilityCast(int slot, AbilityDefinition ability, Vector3 point)
        {
            if (ability.shape == AbilityShape.LinearSkillshot) record.casts++;
        }

        protected override void OnSkillshotResolved(AbilityDefinition ability, Unit hit)
        {
            if (hit != null && ability != null && ability.shape == AbilityShape.LinearSkillshot) record.hits++;
        }

        protected override void OnMarkDetonated(Unit target, float burst) => record.procs++;
        protected override void OnEnemyDamaged(Unit unit, float amount, Unit source) => record.damageDealt += amount;

        protected override void OnEnemyShot(Unit hit)
        {
            if (hit == player.Unit) record.hitsTaken++;
            else if (hit == null) record.dodged++;
        }

        protected override void OnPlayerDamaged(Unit unit, float amount, Unit source)
        {
            record.damageTaken += amount;
        }

        protected override void OnEnemyDied(Unit unit, Unit killer)
        {
            record.kills++;
            record.won = true;
            Finish();
        }

        protected override void OnTick(float dt)
        {
            if (!player.Unit.IsAlive)
            {
                record.won = false;
                Finish();
            }
        }

        protected override void FillRecord(RunRecord r)
        {
            if (r.won)
            {
                float hpLeft = player.Unit.Health01;
                float speedBonus = Mathf.Max(0f, 60f - r.duration) * 10f;
                r.score = Mathf.RoundToInt(1000f + hpLeft * 1000f + speedBonus);
            }
            else
            {
                r.score = Mathf.RoundToInt(r.damageDealt / Mathf.Max(1f, health) * 500f);
            }
        }
    }
}
