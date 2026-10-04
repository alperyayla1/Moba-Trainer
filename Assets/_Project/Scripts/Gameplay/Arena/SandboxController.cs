using System.Collections.Generic;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Free Practice: a few static and strafing dummies to try movement, orb-walking, cast modes
    /// and blink. Becomes a proper scenario once the scenario framework lands (milestone 3).
    /// </summary>
    public class SandboxController : MonoBehaviour, IRunCounters
    {
        [Header("Dummies")]
        public TargetDummy dummyPrefab;
        public int staticCount = 3;
        public int strafingCount = 3;
        public float dummyHealth = 400f;
        public float strafeSpeed = 3f;
        public float ringRadius = 9f;

        [Header("Player")]
        public PlayerController player;
        public Vector3 playerSpawn = new Vector3(0f, 0f, -6f);

        private readonly List<TargetDummy> dummies = new List<TargetDummy>();
        private float sessionTime;
        private float damageDealt;

        public float SessionTime => sessionTime;
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public float Elapsed => sessionTime;
        public float Dps => sessionTime > 0.5f ? damageDealt / sessionTime : 0f;

        private void Awake()
        {
            int total = staticCount + strafingCount;
            for (int i = 0; i < total; i++)
            {
                TargetDummy d = Instantiate(dummyPrefab, transform);
                d.name = $"Dummy_{i}";
                d.Damaged += OnDummyDamaged;
                dummies.Add(d);
            }
        }

        private PlayerSkillSystem subscribedTo;

        private void Subscribe()
        {
            if (subscribedTo != null || PlayerSkillSystem.Instance == null) return;
            subscribedTo = PlayerSkillSystem.Instance;
            subscribedTo.SkillshotResolved += OnSkillshot;
        }

        private void OnDisable()
        {
            if (subscribedTo != null) subscribedTo.SkillshotResolved -= OnSkillshot;
            subscribedTo = null;
        }

        private void OnSkillshot(AbilityDefinition ability, Unit hit)
        {
            if (hit != null) Hits++;
            else Misses++;
        }

        private void OnDestroy()
        {
            foreach (TargetDummy d in dummies)
            {
                if (d != null) d.Damaged -= OnDummyDamaged;
            }
        }

        private void OnDummyDamaged(Unit unit, float amount, Unit source)
        {
            damageDealt += amount;
        }

        public void Begin()
        {
            gameObject.SetActive(true);
            int total = dummies.Count;
            for (int i = 0; i < total; i++)
            {
                float angle = (i / (float)total) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                bool strafe = i >= staticCount;
                float r = strafe ? ringRadius + 3f : ringRadius;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r + 2f);
                pos = Arena.ClampStatic(pos, 0.8f); // never spawn inside a wall
                dummies[i].Configure(pos, strafe ? TargetDummy.Behaviour.Strafe : TargetDummy.Behaviour.Static, dummyHealth, strafeSpeed);
            }
            if (player != null) player.ResetPlayer(playerSpawn);
            sessionTime = 0f;
            damageDealt = 0f;
            Hits = 0;
            Misses = 0;
        }

        public void End()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            Subscribe();
            sessionTime += Time.deltaTime;
        }

        /// <summary>Fills <paramref name="sb"/> with live stats (call a few times per second, not every frame).</summary>
        public void BuildStatsText(System.Text.StringBuilder sb)
        {
            sb.Clear();
            if (player == null) return;
            PlayerAttack a = player.Attack;
            sb.Append("Attacks  ").Append(a.AttacksCompleted).Append("   cancelled ").Append(a.AttacksCancelled).Append('\n');
            sb.Append("DPS  ").Append(Dps.ToString("0.0")).Append("   APM ").Append(sessionTime > 1f ? (player.ActionCount / (sessionTime / 60f)).ToString("0") : "0").Append('\n');
            sb.Append("Attack speed  ").Append(a.attacksPerSecond.ToString("0.00")).Append("   windup ").Append((a.WindupTime * 1000f).ToString("0")).Append(" ms");
        }
    }
}
