using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Dodge drill (60 s): four casters around you throw telegraphed lines (half of them aimed where
    /// you are walking) and, later, circles. They get faster over time. Hits cost points, never your life.
    /// Score = 10 per dodged shot − 40 per hit taken (min 0).
    /// </summary>
    public class DodgeScenario : Scenario
    {
        public int casterCount = 4;

        public override string Id => "dodge";
        public override string Title => "Dodge";
        public override int Hits => record.dodged;
        public override int Misses => record.hitsTaken;

        protected override void OnBegin()
        {
            player.Unit.invulnerable = true;
            for (int i = 0; i < casterCount; i++)
            {
                float a = (i / (float)casterCount) * Mathf.PI * 2f + Mathf.PI * 0.25f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10f;
                TargetDummy e = Enemy(i, p, 1000f);
                e.invulnerable = true;
                if (e.healthBar != null) e.healthBar.gameObject.SetActive(false);
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.preferredRange = new Vector2(8f, 12f);
                b.moveSpeed = 2.2f;
                b.castLines = true;
                b.lineWindup = 0.4f;
                b.lineSpeed = 16f;
                b.lineWidth = 1.2f;
                b.lineRange = 16f;
                b.lineDamage = 1f;
                b.leadChance = 0.5f;
                b.circleDamage = 1f;
                b.ResetBrain();
            }
            HideEnemiesFrom(casterCount);
        }

        protected override void OnTick(float dt)
        {
            // Ramp: faster casts and bolts; circles join after 15 s.
            float t = Time01;
            foreach (TargetDummy e in enemies)
            {
                if (!e.gameObject.activeSelf) continue;
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.lineInterval = new Vector2(Mathf.Lerp(2.6f, 1.1f, t), Mathf.Lerp(3.6f, 1.8f, t));
                b.lineSpeed = Mathf.Lerp(16f, 22f, t);
                b.lineWindup = Mathf.Lerp(0.4f, 0.25f, t);
                b.castCircles = elapsed > 15f;
                b.circleInterval = new Vector2(Mathf.Lerp(5f, 2.5f, t), Mathf.Lerp(7f, 4f, t));
            }
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
    }
}
