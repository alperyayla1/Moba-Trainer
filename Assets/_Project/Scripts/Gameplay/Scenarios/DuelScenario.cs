using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Mixed (60 s): dodge and hit at the same time. Three enemies, each dies to two skillshots and comes back
    /// elsewhere: a red caster (slow orb), a blue caster (fan of shards), a fire mage (flame pillar) and a melee that walks at you
    /// and lunges when close. You have health: if it runs out the run ends early.
    /// Score = 150 per kill + 25 per skillshot hit − 60 per orb taken (+300 if you survive).
    /// </summary>
    public class DuelScenario : Scenario
    {
        public int enemyCount = 4;
        public float enemyHealth = 160f;     // two bolts
        public float playerHealth = 600f;

        public override string Id => "duel";
        public override string Title => "Mixed";
        public override int Hits => record.kills;
        public override int Misses => record.hitsTaken;

        protected override void OnBegin()
        {
            duration = 60f;
            player.Unit.ResetUnit(playerHealth);
            for (int i = 0; i < enemyCount; i++)
            {
                // 0 = red orb caster, 1 = blue fan caster, 2 = melee lunger, 3 = fire mage (flame pillar).
                TargetDummy prefab = i == 1 ? bluePrefab : (i == 2 ? meleePrefab : (i == 3 ? firePrefab : null));
                TargetDummy e = Enemy(i, RandomPointAround(playerSpawn, i == 2 ? 11f : 8f, i == 2 ? 13f : 11f), enemyHealth, prefab);
                e.respawnDelay = 1.5f;
                int kind = i;
                e.BeforeRespawn = d => d.transform.position = RandomPointAround(player.Unit.Position, kind == 2 ? 11f : 8.5f, kind == 2 ? 13f : 11.5f);
                EnemyBrain b = e.GetComponent<EnemyBrain>();
                b.moveSpeed = 3.0f;
                b.jukeInterval = new Vector2(0.35f, 1.0f);
                b.showLane = false;
                b.castCircles = false;
                if (i == 2)
                {
                    // Melee: walks straight at you and lunges when close. Kill it before it arrives.
                    b.chase = true;
                    b.castLines = false;
                    b.moveSpeed = 2.7f;
                    b.lunge = true;
                    b.lungeRange = 4.5f;
                    b.lungeDistance = 2.6f;
                    b.lungeSpeed = 16f;
                    b.lungeWindup = 0.25f;
                    b.lungeCooldown = 3f;
                    b.meleeDamage = 90f;
                    b.meleeCooldown = 1.2f;
                }
                else if (i == 3)
                {
                    SetupFirePillar(b, 80f, new Vector2(3.2f, 4.6f));
                }
                else
                {
                    b.castLines = true;
                    b.preferredRange = new Vector2(7.5f, 11f);
                    b.lineInterval = new Vector2(2.4f + i * 0.4f, 3.6f + i * 0.4f);
                    b.lineRange = 13f;
                    b.lineDamage = 80f;
                    if (i == 1)
                    {
                        b.shotStyle = EnemyBrain.ShotStyle.Fan;
                        b.fanCount = 3;
                        b.fanSpread = 30f;
                        b.lineWidth = 0.6f;
                        b.lineSpeed = 13f;
                        b.lineWindup = 0.55f;
                        b.lineDamage = 50f;
                        b.leadChance = 0.2f;
                    }
                    else
                    {
                        b.lineWidth = 0.9f;     // thinner orb
                        b.lineSpeed = 11f;
                        b.lineWindup = 0.5f;
                        b.leadChance = 0.35f;
                    }
                }
                b.ResetBrain();
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

        protected override void OnEnemyShot(Unit hit)
        {
            if (hit == player.Unit) record.hitsTaken++;
            else if (hit == null) record.dodged++;
        }

        protected override void OnPlayerDamaged(Unit unit, float amount, Unit source)
        {
            record.damageTaken += amount;
            if (source == null) return;
            EnemyBrain brain = source.GetComponent<EnemyBrain>();
            if (brain != null && brain.chase) record.hitsTaken++;   // melee hits (projectile hits are counted in OnEnemyShot)
        }

        protected override void OnTick(float dt)
        {
            if (!player.Unit.IsAlive) Finish();   // ran out of health
        }

        protected override void FillRecord(RunRecord r)
        {
            r.won = player.Unit.IsAlive;
            r.score = Mathf.Max(0, r.kills * 150 + r.hits * 25 - r.hitsTaken * 60 + (r.won ? 300 : 0));
        }
    }
}
