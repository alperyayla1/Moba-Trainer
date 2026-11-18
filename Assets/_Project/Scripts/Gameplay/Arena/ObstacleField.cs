using System.Collections.Generic;
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

        /// <summary>Swaps the blockers (another map layout) and forgets the cached path grids.</summary>
        public void SetCircles(Vector4[] newCircles)
        {
            circles = newCircles ?? new Vector4[0];
            grids.Clear();
        }

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

        // ------------------------------------------------------------------ pathfinding

        [Header("Pathfinding")]
        [Tooltip("Grid cell size in units (A* grid, built lazily per unit radius).")]
        public float cellSize = 0.4f;

        private class Grid
        {
            public int size;
            public float origin;
            public bool[] blocked;
        }

        private readonly Dictionary<int, Grid> grids = new Dictionary<int, Grid>();
        private float[] gScore;
        private int[] cameFrom;
        private int[] closedStamp;
        private int[] openStamp;
        private int stamp;
        private readonly List<int> heap = new List<int>(512);
        private float[] fScore;
        private readonly List<Vector3> rawPath = new List<Vector3>(256);

        /// <summary>True if a unit of this radius can walk straight from a to b.</summary>
        public bool LineClear(Vector3 a, Vector3 b, float margin)
        {
            float ax = a.x, az = a.z, dx = b.x - a.x, dz = b.z - a.z;
            float lenSq = dx * dx + dz * dz;
            for (int i = 0; i < circles.Length; i++)
            {
                Vector4 c = circles[i];
                float r = c.w + margin - 0.02f;
                float t = lenSq > 1e-6f ? Mathf.Clamp01(((c.x - ax) * dx + (c.z - az) * dz) / lenSq) : 0f;
                float px = ax + dx * t - c.x, pz = az + dz * t - c.z;
                if (px * px + pz * pz < r * r) return false;
            }
            return true;
        }

        /// <summary>
        /// Shortest walkable route from start to goal for a unit of the given radius, as waypoints
        /// (goal last, start excluded). Straight line when nothing is in the way.
        /// </summary>
        public void FindPath(Vector3 start, Vector3 goal, float margin, List<Vector3> output)
        {
            output.Clear();
            start.y = 0f;
            goal.y = 0f;
            if (circles.Length == 0 || LineClear(start, goal, margin))
            {
                output.Add(goal);
                return;
            }

            Grid g = GetGrid(margin);
            int s = NearestFree(g, start);
            int e = NearestFree(g, goal);
            if (s < 0 || e < 0 || !AStar(g, s, e))
            {
                output.Add(goal);
                return;
            }

            // Cells -> points (goal first, walking back), then reverse.
            rawPath.Clear();
            rawPath.Add(goal);
            for (int c = cameFrom[e]; c >= 0 && c != s; c = cameFrom[c]) rawPath.Add(CellCenter(g, c));
            rawPath.Reverse();

            // String pulling: from the current point, jump to the farthest point still in line of sight.
            Vector3 from = start;
            int i = 0;
            while (i < rawPath.Count)
            {
                int best = i;
                for (int j = rawPath.Count - 1; j > i; j--)
                {
                    if (LineClear(from, rawPath[j], margin)) { best = j; break; }
                }
                output.Add(rawPath[best]);
                from = rawPath[best];
                i = best + 1;
            }
        }

        private Grid GetGrid(float margin)
        {
            int key = Mathf.RoundToInt(margin * 100f);
            if (grids.TryGetValue(key, out Grid g)) return g;

            float extent = (Arena.Instance != null ? Arena.Instance.radius : 22f) + 1f;
            int size = Mathf.CeilToInt(extent * 2f / cellSize);
            g = new Grid { size = size, origin = -extent, blocked = new bool[size * size] };
            float arenaR = extent - 1f - margin;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = g.origin + (x + 0.5f) * cellSize, pz = g.origin + (y + 0.5f) * cellSize;
                bool blocked = px * px + pz * pz > arenaR * arenaR;
                for (int i = 0; !blocked && i < circles.Length; i++)
                {
                    Vector4 c = circles[i];
                    float r = c.w + margin + 0.05f;
                    float ddx = px - c.x, ddz = pz - c.z;
                    if (ddx * ddx + ddz * ddz < r * r) blocked = true;
                }
                g.blocked[y * size + x] = blocked;
            }
            grids[key] = g;

            int n = size * size;
            if (gScore == null || gScore.Length < n)
            {
                gScore = new float[n];
                fScore = new float[n];
                cameFrom = new int[n];
                closedStamp = new int[n];
                openStamp = new int[n];
            }
            return g;
        }

        private Vector3 CellCenter(Grid g, int cell)
        {
            int x = cell % g.size, y = cell / g.size;
            return new Vector3(g.origin + (x + 0.5f) * cellSize, 0f, g.origin + (y + 0.5f) * cellSize);
        }

        private int CellOf(Grid g, Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - g.origin) / cellSize), 0, g.size - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((p.z - g.origin) / cellSize), 0, g.size - 1);
            return y * g.size + x;
        }

        private int NearestFree(Grid g, Vector3 p)
        {
            int c = CellOf(g, p);
            if (!g.blocked[c]) return c;
            int cx = c % g.size, cy = c / g.size;
            for (int r = 1; r < 12; r++)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= g.size || y >= g.size) continue;
                    int n = y * g.size + x;
                    if (g.blocked[n]) continue;
                    float d = (CellCenter(g, n) - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = n; }
                }
                if (best >= 0) return best;
            }
            return -1;
        }

        private bool AStar(Grid g, int start, int goal)
        {
            stamp++;
            heap.Clear();
            int size = g.size;
            int gx = goal % size, gy = goal / size;
            gScore[start] = 0f;
            cameFrom[start] = -1;
            fScore[start] = Octile(start % size, start / size, gx, gy);
            openStamp[start] = stamp;
            HeapPush(start);

            int guard = size * size * 2;
            while (heap.Count > 0 && guard-- > 0)
            {
                int cur = HeapPop();
                if (closedStamp[cur] == stamp) continue;
                if (cur == goal) return true;
                closedStamp[cur] = stamp;
                int x = cur % size, y = cur / size;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                    int n = ny * size + nx;
                    if (g.blocked[n] || closedStamp[n] == stamp) continue;
                    // No corner cutting through blocked cells.
                    if (dx != 0 && dy != 0 && (g.blocked[y * size + nx] || g.blocked[ny * size + x])) continue;
                    float step = (dx != 0 && dy != 0) ? 1.41421f : 1f;
                    float tentative = gScore[cur] + step;
                    if (openStamp[n] == stamp && tentative >= gScore[n]) continue;
                    openStamp[n] = stamp;
                    gScore[n] = tentative;
                    cameFrom[n] = cur;
                    fScore[n] = tentative + Octile(nx, ny, gx, gy);
                    HeapPush(n);
                }
            }
            return false;
        }

        private static float Octile(int x0, int y0, int x1, int y1)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            return (dx + dy) + (1.41421f - 2f) * Mathf.Min(dx, dy);
        }

        private void HeapPush(int n)
        {
            heap.Add(n);
            int i = heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (fScore[heap[p]] <= fScore[heap[i]]) break;
                (heap[p], heap[i]) = (heap[i], heap[p]);
                i = p;
            }
        }

        private int HeapPop()
        {
            int top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < heap.Count && fScore[heap[l]] < fScore[heap[m]]) m = l;
                if (r < heap.Count && fScore[heap[r]] < fScore[heap[m]]) m = r;
                if (m == i) break;
                (heap[m], heap[i]) = (heap[i], heap[m]);
                i = m;
            }
            return top;
        }
    }
}
