using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>Practice target: stands still or strafes, respawns after dying.</summary>
    public class TargetDummy : Unit
    {
        public enum Behaviour { Static, Strafe }

        [Header("Dummy")]
        public Behaviour behaviour = Behaviour.Static;
        public float strafeSpeed = 2.5f;
        public float strafeDistance = 3f;
        public float respawnDelay = 1.5f;
        public bool respawn = true;
        /// <summary>Called right before respawning (scenarios move the dummy somewhere new).</summary>
        public System.Action<TargetDummy> BeforeRespawn;
        [Tooltip("Hidden while dead (mesh, shadow, health bar).")]
        public GameObject visualRoot;

        private Vector3 home;
        private Vector3 strafeAxis;
        private float phase;
        private float respawnTimer;

        public void Configure(Vector3 position, Behaviour mode, float hp, float speed)
        {
            home = position;
            transform.position = position;
            behaviour = mode;
            strafeSpeed = speed;
            Vector3 toCenter = -position;
            toCenter.y = 0f;
            strafeAxis = toCenter.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, toCenter.normalized) : Vector3.right;
            phase = Random.value * Mathf.PI * 2f;
            ResetUnit(hp);
            if (visualRoot != null) visualRoot.SetActive(true);
            respawnTimer = 0f;
        }

        protected override void Die(Unit killer)
        {
            base.Die(killer);
            if (visualRoot != null) visualRoot.SetActive(false);
            respawnTimer = respawnDelay;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsAlive)
            {
                if (!respawn) return;
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f)
                {
                    BeforeRespawn?.Invoke(this);
                    ResetUnit();
                    if (visualRoot != null) visualRoot.SetActive(true);
                }
                return;
            }

            if (behaviour == Behaviour.Strafe && strafeDistance > 0f)
            {
                phase += Time.deltaTime * strafeSpeed / strafeDistance;
                transform.position = Arena.ClampStatic(home + strafeAxis * (Mathf.Sin(phase) * strafeDistance), radius);
            }
        }
    }
}
