using MobaTrainer.Gameplay;
using MobaTrainer.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>
    /// Extreme Survivor: one long run in the style of a survivors game, built on the Mixed Mode
    /// enemies. Everything specific to this mode lives in this folder; it only *uses* the shared
    /// Scenario / EnemyBrain / EnemySkillSystem / Unit code and never changes their defaults, so the
    /// other drills play exactly as before.
    ///
    /// STEP 1 (this file): run timer, win / lose, restart, debug time skip.
    /// - Survive <see cref="runLength"/> (15:00) to win. Dying ends the run early.
    /// - Restart / quit go through the normal pause menu (Esc).
    /// - Debug (editor and development builds only): F9 = skip 1:00, Shift+F9 = skip 3:00.
    /// Placeholder enemies until step 2 (spawner + tiers): one red orb caster and one melee,
    /// tuned exactly like their Mixed Mode versions, so you can actually die.
    /// Never ends in "endless" mode: the 15:00 run *is* the mode.
    /// </summary>
    public class ExtremeSurvivorScenario : Scenario
    {
        [Header("Run")]
        [Tooltip("Seconds to survive (15:00).")]
        public float runLength = 900f;
        public float playerHealth = 600f;

        [Header("Debug")]
        public bool debugSkip = true;
        public float debugSkipSeconds = 60f;

        public override string Id => "extreme";
        public override string Title => "Extreme Survivor";
        public override int Hits => record.kills;
        public override int Misses => record.hitsTaken;

        /// <summary>Seconds survived so far (counts up, unlike the HUD clock which shows time left).</summary>
        public float Survived => elapsed;

        protected override void OnBegin()
        {
            duration = runLength;
            Endless = false;                 // this mode is never endless (GameFlow sets it again before every drill)
            player.Unit.ResetUnit(playerHealth);

            // ---- placeholder enemies (replaced by the spawner in step 2) ----
            TargetDummy red = Enemy(0, RandomPointAround(playerSpawn, 8f, 11f), 160f);
            red.respawnDelay = 2f;
            red.BeforeRespawn = d => d.transform.position = RandomPointAround(player.Unit.Position, 8.5f, 11.5f);
            EnemyBrain rb = red.GetComponent<EnemyBrain>();
            rb.moveSpeed = 3f;
            rb.jukeInterval = new Vector2(0.35f, 1f);
            rb.showLane = false;
            rb.castLines = true;
            rb.preferredRange = new Vector2(7.5f, 11f);
            rb.lineInterval = new Vector2(2.4f, 3.6f);
            rb.lineRange = 13f;
            rb.lineWidth = 0.9f;
            rb.lineSpeed = 11f;
            rb.lineWindup = 0.5f;
            rb.lineDamage = 80f;
            rb.leadChance = 0.35f;
            rb.ResetBrain();

            TargetDummy melee = Enemy(1, RandomPointAround(playerSpawn, 11f, 13f), 160f, meleePrefab);
            melee.respawnDelay = 2f;
            melee.BeforeRespawn = d => d.transform.position = RandomPointAround(player.Unit.Position, 11f, 13f);
            EnemyBrain mb = melee.GetComponent<EnemyBrain>();
            mb.chase = true;
            mb.castLines = false;
            mb.moveSpeed = 2.7f;
            mb.lunge = true;
            mb.lungeRange = 4.5f;
            mb.lungeDistance = 2.6f;
            mb.lungeSpeed = 16f;
            mb.lungeWindup = 0.25f;
            mb.lungeCooldown = 3f;
            mb.meleeDamage = 90f;
            mb.meleeCooldown = 1.2f;
            mb.ResetBrain();

            HideEnemiesFrom(2);
        }

        protected override void OnTick(float dt)
        {
            if (!player.Unit.IsAlive)
            {
                Finish();                    // lose: died before 15:00
                return;
            }
            DebugSkip();
            // Win: the base class finishes the run when elapsed reaches the duration.
        }

        private void DebugSkip()
        {
            if (!debugSkip || !Debug.isDebugBuild) return;
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.f9Key.wasPressedThisFrame) return;
            float skip = kb.shiftKey.isPressed ? debugSkipSeconds * 3f : debugSkipSeconds;
            elapsed = Mathf.Min(elapsed + skip, runLength - 0.5f);
            Debug.Log($"[Extreme Survivor] Debug skip → {Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00}");
        }

        // ---- stats (same hooks Mixed uses) ----

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
            if (brain != null && brain.chase) record.hitsTaken++;
        }

        protected override void FillRecord(RunRecord r)
        {
            r.won = player.Unit.IsAlive && r.duration >= runLength - 0.01f;
            // 10 points per second survived + 50 per kill, +5000 for surviving the full run.
            r.score = Mathf.Max(0, Mathf.FloorToInt(r.duration) * 10 + r.kills * 50 + (r.won ? 5000 : 0));
        }
    }
}
