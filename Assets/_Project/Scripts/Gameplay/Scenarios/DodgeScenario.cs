using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Dodge drill (60 s): four casters around you. Red ones throw a slow orb, blue ones a fan of
    /// three shards, the fire mage drops flame pillars on you. Each after a visible wind-up. They cast more often as time goes on. They get faster over time. Hits cost points, never your life.
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
                bool blue = i % 2 == 1;   // alternate red orb casters and blue fan casters
                bool fire = i == 3;       // the fourth one is the fire mage (flame pillars)
                if (fire) blue = false;
                float a = (i / (float)casterCount) * Mathf.PI * 2f + Mathf.PI * 0.25f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10f;
                TargetDummy e = Enemy(i, p, 1000f, fire ? firePrefab : (blue ? bluePrefab : null));
                e.invulnerable = true;
                if (e.healthBar != null) e.healthBar.gameObject.SetActive(false);
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.preferredRange = new Vector2(8f, 12f);
                b.moveSpeed = 2.2f;
                b.castLines = true;
                b.showLane = false;
                b.lineRange = 15f;
                b.lineDamage = 1f;
                b.circleDamage = 1f;
                if (fire)
                {
                    SetupFirePillar(b, 1f, new Vector2(3.2f, 4.2f));
                }
                else if (blue)
                {
                    // Fan of three thin shards, a bit faster than the orb.
                    b.shotStyle = EnemyBrain.ShotStyle.Fan;
                    b.fanCount = 3;
                    b.fanSpread = 34f;
                    b.lineWidth = 0.7f;
                    b.lineSpeed = 13f;
                    b.lineWindup = 0.55f;
                    b.leadChance = 0.2f;
                }
                else
                {
                    // Same slow orb as in Mixed.
                    b.lineWidth = 1.0f;
                    b.lineSpeed = 11f;
                    b.lineWindup = 0.5f;
                    b.leadChance = 0.45f;
                }
                b.ResetBrain();
            }
            HideEnemiesFrom(casterCount);
        }

        protected override void OnTick(float dt)
        {
            // Ramp: casts come more often; projectile speeds stay readable.
            float t = Time01;
            foreach (TargetDummy e in enemies)
            {
                if (!e.gameObject.activeSelf) continue;
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.lineInterval = new Vector2(Mathf.Lerp(2.8f, 1.4f, t), Mathf.Lerp(3.8f, 2.2f, t));
                b.circleInterval = new Vector2(Mathf.Lerp(3.2f, 1.8f, t), Mathf.Lerp(4.2f, 2.6f, t));
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
