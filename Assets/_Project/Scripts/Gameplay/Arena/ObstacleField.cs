using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Static blockers (walls, pillars, trees) as circles on the ground plane. Units slide around them;
    /// skillshots fly over them, like in most MOBAs. Filled in by the scene builder.
    /// </summary>
    public class ObstacleField : MonoBehaviour
    {
        public static ObstacleField Instance { get; private set; }

        [Tooltip("x, z = centre, w = radius (y unused).")]
        public Vector4[] circles = new Vector4[0];

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Pushes a unit of the given radius out of every overlapping obstacle.</summary>
        public Vector3 Resolve(Vector3 p, float unitRadius)
        {
            // Two passes handle the seams between neighbouring circles of a wall.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < circles.Length; i++)
                {
                    Vector4 c = circles[i];
                    float dx = p.x - c.x;
                    float dz = p.z - c.z;
                    float min = c.w + unitRadius;
                    float sq = dx * dx + dz * dz;
                    if (sq >= min * min) continue;
                    if (sq < 1e-6f) { dx = 0f; dz = -1f; sq = 1f; }
                    float d = Mathf.Sqrt(sq);
                    p.x = c.x + dx / d * min;
                    p.z = c.z + dz / d * min;
                }
            }
            return p;
        }

        public static Vector3 ResolveStatic(Vector3 p, float unitRadius)
        {
            return Instance != null ? Instance.Resolve(p, unitRadius) : p;
        }
    }
}
