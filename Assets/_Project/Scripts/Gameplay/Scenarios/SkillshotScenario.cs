using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Skillshot drill (60 s): three enemies juke around you at mid range. Land Q / W, combo marks.
    /// Score = 100 per hit + 150 per mark detonation + 200 per kill.
    /// </summary>
    public class SkillshotScenario : Scenario
    {
        public int enemyCount = 3;
        public float enemyHealth = 320f;

        public override string Id => "skillshot";
        public override string Title => "Skillshot";
        public override int Hits => record.hits;
        public override int Misses => record.casts - record.hits;

        protected override void OnBegin()
        {
            for (int i = 0; i < enemyCount; i++)
            {
                TargetDummy e = Enemy(i, RandomPointAround(playerSpawn, 7f, 10f), enemyHealth);
                e.respawnDelay = 0.6f;
                e.BeforeRespawn = d => d.transform.position = RandomPointAround(player.Unit.Position, 7f, 11f);
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.preferredRange = new Vector2(6.5f, 10.5f);
                b.jukeInterval = new Vector2(0.3f, 0.9f);
            }
            HideEnemiesFrom(enemyCount);
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
        protected override void OnEnemyDied(Unit unit, Unit killer) => record.kills++;

        protected override void FillRecord(RunRecord r)
        {
            r.score = r.hits * 100 + r.procs * 150 + r.kills * 200;
            r.won = true;
        }
    }
}
