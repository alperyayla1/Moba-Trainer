using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Kiting (60 s): melee chasers slightly slower than you run straight at you. Orb-walk
    /// (attack, move, attack) and kill them without letting them touch you. More join over time.
    /// Score = 100 per kill − 50 per hit taken (min 0). You cannot die.
    /// </summary>
    public class KitingScenario : Scenario
    {
        public int maxChasers = 4;
        public float chaserHealth = 160f;

        public override string Id => "kiting";
        public override string Title => "Kiting";
        public override int Hits => record.kills;
        public override int Misses => record.hitsTaken;

        private int active;

        protected override void OnBegin()
        {
            player.Unit.invulnerable = true;
            active = 0;
            Spawn();
            HideEnemiesFrom(1);
        }

        protected override void OnTick(float dt)
        {
            int wanted = Mathf.Min(maxChasers, 1 + Mathf.FloorToInt(elapsed / 12f));
            while (active < wanted) Spawn();
        }

        private void Spawn()
        {
            TargetDummy e = Enemy(active, RandomPointAround(player.Unit.Position, 9f, 12f), chaserHealth);
            e.respawnDelay = 1.2f;
            e.BeforeRespawn = d => d.transform.position = RandomPointAround(player.Unit.Position, 10f, 13f);
            EnemyBrain b = e.GetComponent<EnemyBrain>();
            b.chase = true;
            b.moveSpeed = 2.85f;
            b.meleeCooldown = 1f;
            b.meleeDamage = 1f;
            b.ResetBrain();
            b.Active = Running;
            active++;
        }

        protected override void OnPlayerDamaged(Unit unit, float amount, Unit source)
        {
            if (source != null && source != player.Unit) record.hitsTaken++;
        }

        protected override void OnEnemyDamaged(Unit unit, float amount, Unit source) => record.damageDealt += amount;
        protected override void OnEnemyDied(Unit unit, Unit killer) => record.kills++;

        protected override void FillRecord(RunRecord r)
        {
            r.score = Mathf.Max(0, r.kills * 100 - r.hitsTaken * 50);
            r.won = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (TargetDummy d in enemies)
            {
                if (d == null) continue;
                EnemyBrain b = d.GetComponent<EnemyBrain>();
                if (b != null) b.chase = false;
            }
        }
    }
}
