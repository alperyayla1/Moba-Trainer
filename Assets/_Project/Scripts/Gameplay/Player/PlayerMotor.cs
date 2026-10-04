using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Kinematic top-down movement on the arena plane: point-to-point moves, near-instant turning,
    /// dashes and blinks. Deterministic and allocation-free (no NavMesh, no rigidbody).
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Units per second.")]
        public float moveSpeed = 3.25f;
        [Tooltip("Degrees per second. MOBA characters turn almost instantly.")]
        public float turnSpeed = 1800f;
        [Tooltip("Visual child that rotates to face the move/attack direction.")]
        public Transform facingRoot;

        public bool HasDestination { get; private set; }
        public Vector3 Destination { get; private set; }
        public bool IsDashing => dashRemaining > 0f;
        /// <summary>True during an ability cast time: the unit stands still, but move orders are kept and resume afterwards.</summary>
        public bool Paused { get; set; }
        public float DistanceTravelled { get; private set; }

        private Unit unit;
        private Vector3 facingDirection = Vector3.forward;
        private Vector3 dashDirection;
        private float dashSpeed;
        private float dashRemaining;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            if (facingRoot == null) facingRoot = transform;
        }

        public void ResetMotor(Vector3 position)
        {
            transform.position = position;
            HasDestination = false;
            Paused = false;
            dashRemaining = 0f;
            DistanceTravelled = 0f;
            facingDirection = Vector3.forward;
            facingRoot.rotation = Quaternion.LookRotation(facingDirection);
        }

        public void MoveTo(Vector3 point)
        {
            Destination = Arena.ClampStatic(point, unit.radius);
            HasDestination = true;
        }

        public void Stop()
        {
            HasDestination = false;
        }

        public void Face(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) facingDirection = d.normalized;
        }

        public void Blink(Vector3 target)
        {
            Vector3 dest = Arena.ClampStatic(target, unit.radius);
            Face(dest);
            transform.position = dest;
            dashRemaining = 0f;
            if (HasDestination && (Destination - dest).sqrMagnitude < 0.01f) HasDestination = false;
        }

        public void Dash(Vector3 direction, float distance, float speed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = facingDirection;
            dashDirection = direction.normalized;
            dashSpeed = Mathf.Max(1f, speed);
            dashRemaining = distance;
            facingDirection = dashDirection;
            HasDestination = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 pos = transform.position;

            if (dashRemaining > 0f)
            {
                float step = Mathf.Min(dashSpeed * dt, dashRemaining);
                dashRemaining -= step;
                Vector3 next = Arena.ClampStatic(pos + dashDirection * step, unit.radius);
                DistanceTravelled += (next - pos).magnitude;
                transform.position = next;
                if ((next - pos).sqrMagnitude < step * step * 0.25f) dashRemaining = 0f; // hit the wall
            }
            else if (HasDestination && !Paused)
            {
                Vector3 to = Destination - pos;
                to.y = 0f;
                float dist = to.magnitude;
                float step = moveSpeed * dt;
                if (dist <= step)
                {
                    transform.position = Destination;
                    DistanceTravelled += dist;
                    HasDestination = false;
                }
                else
                {
                    Vector3 dir = to / dist;
                    // Slide along walls: step, then push out of any obstacle.
                    Vector3 next = Arena.ClampStatic(pos + dir * step, unit.radius);
                    float moved = (next - pos).magnitude;
                    transform.position = next;
                    DistanceTravelled += moved;
                    facingDirection = dir;
                    if (moved < step * 0.05f) HasDestination = false; // pinned against a wall corner
                }
            }

            Quaternion targetRot = Quaternion.LookRotation(facingDirection, Vector3.up);
            facingRoot.rotation = Quaternion.RotateTowards(facingRoot.rotation, targetRot, turnSpeed * dt);
        }
    }
}
