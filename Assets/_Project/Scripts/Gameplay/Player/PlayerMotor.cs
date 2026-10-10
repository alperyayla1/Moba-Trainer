using System.Collections.Generic;
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
        private readonly List<Vector3> path = new List<Vector3>(16);
        private int pathIndex;
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

        /// <summary>Walks to the point, taking the shortest way around walls (A* + line-of-sight smoothing).</summary>
        public void MoveTo(Vector3 point)
        {
            Destination = Arena.ClampStatic(point, unit.radius);
            HasDestination = true;
            pathIndex = 0;
            if (ObstacleField.Instance != null) ObstacleField.Instance.FindPath(transform.position, Destination, unit.radius, path);
            else { path.Clear(); path.Add(Destination); }
        }

        /// <summary>Next point the unit is walking toward (current waypoint).</summary>
        private Vector3 CurrentTarget => pathIndex < path.Count ? path[pathIndex] : Destination;

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
            else if (HasDestination) MoveTo(Destination);   // re-route from the new position
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
                float step = moveSpeed * dt;
                Vector3 cur = pos;
                // Spend the whole frame's movement, rolling over waypoints so corners don't cost speed.
                while (step > 0f && HasDestination)
                {
                    Vector3 target = CurrentTarget;
                    Vector3 to = target - cur;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist <= step)
                    {
                        cur = new Vector3(target.x, pos.y, target.z);
                        step -= dist;
                        if (pathIndex < path.Count - 1) pathIndex++;
                        else HasDestination = false;
                        if (dist > 1e-4f) facingDirection = to / dist;
                    }
                    else
                    {
                        Vector3 dir = to / dist;
                        cur += dir * step;
                        facingDirection = dir;
                        step = 0f;
                    }
                }
                Vector3 next = Arena.ClampStatic(cur, unit.radius);   // safety net only
                DistanceTravelled += (next - pos).magnitude;
                transform.position = next;
            }

            Quaternion targetRot = Quaternion.LookRotation(facingDirection, Vector3.up);
            facingRoot.rotation = Quaternion.RotateTowards(facingRoot.rotation, targetRot, turnSpeed * dt);
        }
    }
}
