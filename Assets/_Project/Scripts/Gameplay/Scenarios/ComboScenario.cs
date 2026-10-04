using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Combo (60 s): two tanky enemies juke slowly. Points come mainly from combos: land W to mark,
    /// then detonate it with Q, E or an attack. Score = 250 per detonation + 25 per skillshot hit.
    /// </summary>
    public class ComboScenario : Scenario
    {
        public int enemyCount = 2;

        public override string Id => "combo";
        public override string Title => "Combo";
        public override int Hits => record.procs;
        public override int Misses => record.casts - record.hits;

        protected override void OnBegin()
        {
            for (int i = 0; i < enemyCount; i++)
            {
                TargetDummy e = Enemy(i, RandomPointAround(playerSpawn, 6f, 9f), 2000f);
                e.respawnDelay = 0.6f;
                e.BeforeRespawn = d => d.transform.position = RandomPointAround(player.Unit.Position, 6f, 9f);
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.moveSpeed = 2.4f;
                b.preferredRange = new Vector2(5.5f, 9f);
                b.jukeInterval = new Vector2(0.5f, 1.3f);
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
            r.score = r.procs * 250 + r.hits * 25;
            r.won = true;
        }
    }
}
