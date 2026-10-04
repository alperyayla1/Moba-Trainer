using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>Circular play area centred on the origin. Everything that moves is clamped inside it.</summary>
    public class Arena : MonoBehaviour
    {
        public static Arena Instance { get; private set; }

        [Header("Arena")]
        public float radius = 20f;

        public Vector3 Center => Vector3.zero;

        private void Awake()
        {
            Instance = this;
        }

        public Vector3 Clamp(Vector3 point, float margin = 0f)
        {
            point.y = 0f;
            float max = Mathf.Max(0f, radius - margin);
            float sq = point.x * point.x + point.z * point.z;
            if (sq > max * max)
            {
                float k = max / Mathf.Sqrt(sq);
                point.x *= k;
                point.z *= k;
            }
            return point;
        }

        public bool Contains(Vector3 point, float margin = 0f)
        {
            float max = radius - margin;
            return point.x * point.x + point.z * point.z <= max * max;
        }

        /// <summary>Keeps a point inside the arena and outside walls/pillars (margin = unit radius).</summary>
        public static Vector3 ClampStatic(Vector3 point, float margin = 0f)
        {
            Vector3 p = Instance != null ? Instance.Clamp(point, margin) : new Vector3(point.x, 0f, point.z);
            if (ObstacleField.Instance == null) return p;
            p = ObstacleField.Instance.Resolve(p, margin);
            return Instance != null ? Instance.Clamp(p, margin) : p;
        }
    }
}
