using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Awareness (60 s): enemies pop up anywhere on the map (often off-screen) with a pulse and
    /// leave after a few seconds. Find them and hit them with anything before they vanish.
    /// Score = 100 per catch − 20 per escape (min 0).
    /// </summary>
    public class AwarenessScenario : Scenario
    {
        public int maxTargets = 2;
        public Vector2 lifetime = new Vector2(3.5f, 2.2f);   // start → end of run
        public Vector2 spawnGap = new Vector2(1.6f, 0.8f);

        public override string Id => "awareness";
        public override string Title => "Awareness";
        public override int Hits => record.hits;
        public override int Misses => record.missed;

        private float[] lifeLeft;
        private bool[] caught;
        private float spawnTimer;

        protected override void OnBegin()
        {
            lifeLeft = new float[maxTargets];
            caught = new bool[maxTargets];
            for (int i = 0; i < maxTargets; i++)
            {
                TargetDummy e = Enemy(i, new Vector3(0f, 0f, 30f), 1f);
                e.respawn = false;
                e.GetComponent<EnemyBrain>().move = false;
                e.gameObject.SetActive(false);
                lifeLeft[i] = 0f;
            }
            HideEnemiesFrom(maxTargets);
            spawnTimer = 0.5f;
        }

        protected override void OnTick(float dt)
        {
            float t = Time01;
            for (int i = 0; i < maxTargets; i++)
            {
                TargetDummy e = enemies[i];
                if (!e.gameObject.activeSelf) continue;
                if (caught[i]) { e.gameObject.SetActive(false); continue; }   // hidden a frame after the hit
                lifeLeft[i] -= dt;
                if (lifeLeft[i] <= 0f)
                {
                    record.missed++;
                    e.gameObject.SetActive(false);
                }
            }

            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            spawnTimer = Mathf.Lerp(spawnGap.x, spawnGap.y, t);
            for (int i = 0; i < maxTargets; i++)
            {
                if (enemies[i].gameObject.activeSelf) continue;
                TargetDummy e = Enemy(i, RandomPointAround(Vector3.zero, 3f, 17f), 1f);
                e.respawn = false;
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.move = false;
                b.Active = false;
                caught[i] = false;
                lifeLeft[i] = Mathf.Lerp(lifetime.x, lifetime.y, t);
                FxSystem.Instance?.Burst(e.Position, 2.2f, new Color(1f, 0.8f, 0.3f, 0.8f), 0.6f);
                break;
            }
        }

        protected override void OnEnemyDamaged(Unit unit, float amount, Unit source)
        {
            for (int i = 0; i < maxTargets; i++)
            {
                if (enemies[i] != unit || caught[i]) continue;
                caught[i] = true;
                record.hits++;
            }
        }

        protected override void FillRecord(RunRecord r)
        {
            r.score = Mathf.Max(0, r.hits * 100 - r.missed * 20);
            r.won = true;
        }
    }
}
