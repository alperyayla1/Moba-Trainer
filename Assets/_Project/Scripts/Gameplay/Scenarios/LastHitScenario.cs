using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Last Hit (60 s): small minions lose health on their own (an invisible fight). Only the
    /// killing blow counts: kill them with an attack or ability at low health. A minion that
    /// dies to the fight is a missed last hit. Score = 100 per last hit − 30 per miss (min 0).
    /// </summary>
    public class LastHitScenario : Scenario
    {
        public int minionCount = 6;
        public float minionHealth = 420f;
        public Vector2 tickInterval = new Vector2(0.55f, 1.0f);
        public Vector2 tickDamage = new Vector2(20f, 55f);
        public Vector3 laneCenter = new Vector3(0f, 0f, 4f);

        public override string Id => "lasthit";
        public override string Title => "Last Hit";
        public override int Hits => record.kills;
        public override int Misses => record.missed;

        private float[] timers;

        protected override void OnBegin()
        {
            timers = new float[minionCount];
            for (int i = 0; i < minionCount; i++)
            {
                TargetDummy e = Enemy(i, RandomPointAround(laneCenter, 0.5f, 3.5f), minionHealth);
                e.transform.localScale = Vector3.one * 0.72f;   // minion-sized
                e.respawnDelay = 1.6f;
                e.BeforeRespawn = d => d.transform.position = RandomPointAround(laneCenter, 0.5f, 3.5f);
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.move = false;
                timers[i] = Random.Range(0.2f, 1.2f);
            }
            HideEnemiesFrom(minionCount);
        }

        protected override void OnTick(float dt)
        {
            for (int i = 0; i < minionCount && i < enemies.Count; i++)
            {
                TargetDummy e = enemies[i];
                if (!e.IsAlive) continue;
                timers[i] -= dt;
                if (timers[i] > 0f) continue;
                timers[i] = Random.Range(tickInterval.x, tickInterval.y);
                e.TakeDamage(Random.Range(tickDamage.x, tickDamage.y), null);   // source null = not you
            }
        }

        protected override void OnEnemyDied(Unit unit, Unit killer)
        {
            if (killer == player.Unit) record.kills++;
            else record.missed++;
        }

        protected override void OnEnemyDamaged(Unit unit, float amount, Unit source)
        {
            if (source == player.Unit) record.damageDealt += amount;
        }

        protected override void FillRecord(RunRecord r)
        {
            r.score = Mathf.Max(0, r.kills * 100 - r.missed * 30);
            r.won = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (TargetDummy d in enemies) if (d != null) d.transform.localScale = Vector3.one;
        }
    }
}
