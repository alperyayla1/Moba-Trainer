using System.Collections.Generic;
using UnityEngine;

namespace MobaTrainer.Core
{
    /// <summary>
    /// Procedural flat meshes on the XZ plane (y = 0) for ground indicators, telegraphs,
    /// blob shadows and the arena floor. Vertex alpha gives soft, anti-aliased edges;
    /// the tint comes from the material / MaterialPropertyBlock.
    /// Meshes are cached, so call these at setup time, not per frame.
    /// </summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        private static readonly List<Vector3> V = new List<Vector3>(1024);
        private static readonly List<Color> C = new List<Color>(1024);
        private static readonly List<Vector2> U = new List<Vector2>(1024);
        private static readonly List<int> T = new List<int>(4096);

        private static void Begin() { V.Clear(); C.Clear(); U.Clear(); T.Clear(); }

        private static Mesh End(string name)
        {
            // DontSave: generated meshes are rebuilt at runtime and never serialised into scenes.
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(V);
            mesh.SetColors(C);
            mesh.SetUVs(0, U);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Polar(float radius, float angle) => new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

        /// <summary>Adds quads between two concentric loops (loop A inside loop B).</summary>
        private static void Bridge(int loopA, int loopB, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                int a0 = loopA + i, a1 = loopA + i + 1, b0 = loopB + i, b1 = loopB + i + 1;
                T.Add(a0); T.Add(b1); T.Add(b0);
                T.Add(a0); T.Add(a1); T.Add(b1);
            }
        }

        private static int AddLoop(float radius, float alpha, int segments, float uvV)
        {
            int start = V.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                V.Add(Polar(radius, t * Mathf.PI * 2f));
                C.Add(new Color(1f, 1f, 1f, alpha));
                U.Add(new Vector2(t, uvV));
            }
            return start;
        }

        /// <summary>Thin ring with feathered edges.</summary>
        public static Mesh Ring(float radius, float thickness, float feather = 0.04f, int segments = 96)
        {
            string key = $"ring_{radius:F3}_{thickness:F3}_{feather:F3}_{segments}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Begin();
            float inner = Mathf.Max(0f, radius - thickness * 0.5f);
            float outer = radius + thickness * 0.5f;
            int l0 = AddLoop(Mathf.Max(0f, inner - feather), 0f, segments, 0f);
            int l1 = AddLoop(inner, 1f, segments, 0.33f);
            int l2 = AddLoop(outer, 1f, segments, 0.66f);
            int l3 = AddLoop(outer + feather, 0f, segments, 1f);
            Bridge(l0, l1, segments);
            Bridge(l1, l2, segments);
            Bridge(l2, l3, segments);
            return Cache[key] = End(key);
        }

        /// <summary>Filled disc; alpha goes from <paramref name="centerAlpha"/> to <paramref name="edgeAlpha"/>, with a feathered rim.</summary>
        public static Mesh Disc(float radius, float centerAlpha = 1f, float edgeAlpha = 1f, float feather = 0.04f, int segments = 96)
        {
            string key = $"disc_{radius:F3}_{centerAlpha:F2}_{edgeAlpha:F2}_{feather:F3}_{segments}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Begin();
            int center = V.Count;
            V.Add(Vector3.zero); C.Add(new Color(1f, 1f, 1f, centerAlpha)); U.Add(new Vector2(0.5f, 0f));
            int edge = AddLoop(radius, edgeAlpha, segments, 1f);
            int rim = AddLoop(radius + feather, 0f, segments, 1f);
            for (int i = 0; i < segments; i++)
            {
                T.Add(center); T.Add(edge + i + 1); T.Add(edge + i);
            }
            Bridge(edge, rim, segments);
            return Cache[key] = End(key);
        }

        /// <summary>
        /// Rectangle starting at the origin and extending along +Z by <paramref name="length"/>,
        /// <paramref name="width"/> wide, with feathered sides and ends. UV.y runs 0..1 along the length.
        /// </summary>
        public static Mesh Rect(float length, float width, float feather = 0.05f)
        {
            string key = $"rect_{length:F3}_{width:F3}_{feather:F3}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Begin();
            float hw = width * 0.5f;
            float[] xs = { -hw - feather, -hw, hw, hw + feather };
            float[] xa = { 0f, 1f, 1f, 0f };
            float[] zs = { -feather, 0f, length, length + feather };
            float[] za = { 0f, 1f, 1f, 0f };
            for (int zi = 0; zi < 4; zi++)
            {
                for (int xi = 0; xi < 4; xi++)
                {
                    V.Add(new Vector3(xs[xi], 0f, zs[zi]));
                    C.Add(new Color(1f, 1f, 1f, xa[xi] * za[zi]));
                    U.Add(new Vector2((xs[xi] + hw) / Mathf.Max(0.0001f, width), Mathf.Clamp01(zs[zi] / Mathf.Max(0.0001f, length))));
                }
            }
            for (int zi = 0; zi < 3; zi++)
            {
                for (int xi = 0; xi < 3; xi++)
                {
                    int a = zi * 4 + xi, b = a + 1, c = a + 4, d = c + 1;
                    T.Add(a); T.Add(c); T.Add(d);
                    T.Add(a); T.Add(d); T.Add(b);
                }
            }
            return Cache[key] = End(key);
        }

        /// <summary>
        /// Cone / sector pointing along +Z with the given total angle (degrees). Feathered outer edge.
        /// UV.y is the normalised distance from the apex (used for fill-progress telegraphs).
        /// </summary>
        public static Mesh Sector(float radius, float angleDegrees, float feather = 0.05f, int segments = 48)
        {
            string key = $"sector_{radius:F3}_{angleDegrees:F1}_{feather:F3}_{segments}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Begin();
            float half = angleDegrees * 0.5f * Mathf.Deg2Rad;
            float start = Mathf.PI * 0.5f - half; // +Z is 90 degrees in XZ polar
            int apex = V.Count;
            V.Add(Vector3.zero); C.Add(Color.white); U.Add(new Vector2(0.5f, 0f));
            int edge = V.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = start + (2f * half) * i / segments;
                V.Add(Polar(radius, a)); C.Add(Color.white); U.Add(new Vector2((float)i / segments, 1f));
            }
            int rim = V.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = start + (2f * half) * i / segments;
                V.Add(Polar(radius + feather, a)); C.Add(new Color(1f, 1f, 1f, 0f)); U.Add(new Vector2((float)i / segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                T.Add(apex); T.Add(edge + i + 1); T.Add(edge + i);
            }
            Bridge(edge, rim, segments);
            return Cache[key] = End(key);
        }

        /// <summary>Opaque disc with UVs = world XZ / radius; used for the arena floor.</summary>
        public static Mesh FloorDisc(float radius, int segments = 128)
        {
            Begin();
            V.Add(Vector3.zero); C.Add(Color.white); U.Add(Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments * Mathf.PI * 2f;
                Vector3 p = Polar(radius, t);
                V.Add(p); C.Add(Color.white); U.Add(new Vector2(p.x, p.z) / radius);
            }
            for (int i = 0; i < segments; i++)
            {
                T.Add(0); T.Add(i + 2); T.Add(i + 1);
            }
            return End("ArenaFloor");
        }
    }
}
