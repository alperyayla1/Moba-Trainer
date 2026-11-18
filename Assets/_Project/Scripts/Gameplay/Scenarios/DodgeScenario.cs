using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Dodge drill (60 s): casters around you, each with one skill: slow red orb, flame pillar, card throw
    /// (three slow cards in a wide cone) and light beam (warning line, then a wide beam). Level 1-2 has
    /// two casters, level 5 four, level 9-10 six; the roles always come in this order so every level has
    /// the pillar. Casts come more often as time goes on; shot speeds never change. Hits cost points, never your life.
    /// Score = 10 per dodged shot − 40 per hit taken (min 0).
    /// </summary>
    public class DodgeScenario : Scenario
    {
        private enum Role { Orb, Pillar, Cards, Beam }
        private static readonly Role[] Order = { Role.Orb, Role.Pillar, Role.Cards, Role.Beam, Role.Orb, Role.Pillar };

        public int casterCount = 4;

        public override string Id => "dodge";
        public override string Title => "Dodge";
        public override int Hits => record.dodged;
        public override int Misses => record.hitsTaken;

        private int count;

        protected override void OnBegin()
        {
            player.Unit.invulnerable = true;
            count = Mathf.Clamp(casterCount + ExtraCount, 2, 6);   // difficulty: more casters (same shots)
            for (int i = 0; i < count; i++)
            {
                Role role = Order[i];
                float a = (i / (float)count) * Mathf.PI * 2f + Mathf.PI * 0.25f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10f;
                TargetDummy prefab = role == Role.Pillar ? firePrefab : (role == Role.Orb ? null : (role == Role.Cards && cardsPrefab != null ? cardsPrefab : bluePrefab));
                TargetDummy e = Enemy(i, p, 1000f, prefab);
                e.invulnerable = true;
                if (e.healthBar != null) e.healthBar.gameObject.SetActive(false);
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.preferredRange = new Vector2(8f, 12f);
                b.moveSpeed = 2.2f;
                b.showLane = false;
                switch (role)
                {
                    case Role.Pillar:
                        SetupFirePillar(b, 1f, new Vector2(3.2f, 4.2f));
                        break;
                    case Role.Cards:
                        SetupCards(b, 1f, new Vector2(3.4f, 4.4f));
                        break;
                    case Role.Beam:
                        SetupBeam(b, 1f, new Vector2(8f, 10f));
                        break;
                    default:
                        // Same slow orb as in Mixed.
                        b.castLines = true;
                        b.lineRange = 15f;
                        b.lineDamage = 1f;
                        b.lineWidth = 1.0f;
                        b.lineSpeed = 11f;
                        b.lineWindup = 0.5f;
                        b.leadChance = 0.45f;
                        break;
                }
                b.ResetBrain();
            }
            HideEnemiesFrom(count);
        }

        protected override void OnTick(float dt)
        {
            // Ramp: casts come more often over the run; projectile speeds stay the same.
            float t = Time01;
            for (int i = 0; i < count && i < enemies.Count; i++)
            {
                TargetDummy e = enemies[i];
                if (!e.gameObject.activeSelf) continue;
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                switch (Order[i])
                {
                    case Role.Orb: b.lineInterval = new Vector2(Mathf.Lerp(2.8f, 1.4f, t), Mathf.Lerp(3.8f, 2.2f, t)) * Sooner; break;
                    case Role.Cards: b.lineInterval = new Vector2(Mathf.Lerp(3.4f, 1.8f, t), Mathf.Lerp(4.4f, 2.6f, t)) * Sooner; break;
                    case Role.Beam: b.lineInterval = new Vector2(Mathf.Lerp(8f, 5f, t), Mathf.Lerp(10f, 7f, t)) * Sooner; break;
                    case Role.Pillar: b.circleInterval = new Vector2(Mathf.Lerp(3.2f, 1.8f, t), Mathf.Lerp(4.2f, 2.6f, t)) * Sooner; break;
                }
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
