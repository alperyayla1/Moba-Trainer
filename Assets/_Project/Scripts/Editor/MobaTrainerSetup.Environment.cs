using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// Stylized arena dressing: curved stone walls with braziers, grass pockets behind them and a ring
    /// of low-poly pines outside the play area. All meshes are generated (flat-shaded low poly),
    /// all layout is seeded so every rebuild gives the same map. Walls, pillars and pocket trees
    /// register blocking circles in <see cref="ObstacleField"/>.
    /// </summary>
    public static partial class MobaTrainerSetup
    {
        private const string PropDir = GenDir + "/Props";

        private class EnvMaterials
        {
            public Material stone, stoneDark, metal, bark, leaves, leavesDark, grass, flame, floorGlow, cliff, mossTop;
        }

        private class EnvMeshes
        {
            public Mesh[] rocks;
            public Mesh pillar, cup, tree, grass, glowQuad;
        }

        // Wall polylines (x, z). The SE set mirrors the NW set through the centre so both sides play the same.
        private static readonly Vector2[][] WallLines =
        {
            // Long curved outcrops across the middle ring (they make lanes and give cover to dodge behind).
            new[] { new Vector2(-14f, 2.5f), new Vector2(-11f, 6f), new Vector2(-7f, 8.6f), new Vector2(-3.2f, 9.6f) },
            new[] { new Vector2(14f, -2.5f), new Vector2(11f, -6f), new Vector2(7f, -8.6f), new Vector2(3.2f, -9.6f) },
            // Short outcrops toward the open corners.
            new[] { new Vector2(6.5f, 11.5f), new Vector2(10.5f, 8.5f) },
            new[] { new Vector2(-6.5f, -11.5f), new Vector2(-10.5f, -8.5f) },
            // Two small rocks near the middle to juke around.
            new[] { new Vector2(-6.2f, -1.2f), new Vector2(-4.6f, -3.4f) },
            new[] { new Vector2(6.2f, 1.2f), new Vector2(4.6f, 3.4f) },
        };

        private static void BuildEnvironment(Transform arenaRoot, Material particleTemplate)
        {
            if (!AssetDatabase.IsValidFolder(PropDir)) AssetDatabase.CreateFolder(GenDir, "Props");
            EnvMaterials mat = BuildEnvMaterials();
            EnvMeshes mesh = BuildEnvMeshes();

            var root = new GameObject("Environment");
            root.transform.SetParent(arenaRoot, false);
            var obstacles = new List<Vector4>();
            var rng = new System.Random(1337);

            // Walls: chunky rock outcrops with mossy, overgrown tops and braziers at the tips ------------
            var walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            wallCircles.Clear();
            wallMeshIndex = 0;
            int lightCount = 0;
            for (int w = 0; w < WallLines.Length; w++)
            {
                Vector2[] line = WallLines[w];
                bool smallRock = w >= 4;
                float maxHalfWidth = line.Length > 2 ? 1.35f : (smallRock ? 0.95f : 1.05f);
                BuildCliffWall(walls, line, maxHalfWidth, mat, mesh, rng, obstacles, out Vector2 tipA, out Vector2 tipB);
                if (smallRock) continue; // no braziers on the small middle rocks
                foreach (Vector2 tip in new[] { tipA, tipB })
                {
                    bool withLight = lightCount < 8;
                    if (withLight) lightCount++;
                    BuildPillar(walls, mesh, mat, particleTemplate, tip, withLight, rng);
                    obstacles.Add(new Vector4(tip.x, 0f, tip.y, 0.75f));
                }
            }

            // Grass pockets behind the long walls, plus sparse tufts near the arena edge ---------------------
            var grass = new GameObject("Grass").transform;
            grass.SetParent(root.transform, false);
            ScatterPocket(grass, mesh.grass, mat.grass, WallLines[0], rng, 120);
            ScatterPocket(grass, mesh.grass, mat.grass, WallLines[1], rng, 120);
            for (int i = 0; i < 60; i++)
            {
                float ang = Rand(rng, 0f, Mathf.PI * 2f);
                float r = Rand(rng, 16.5f, 21f);
                Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (NearWall(new Vector2(p.x, p.z), 1.0f)) continue;
                PlaceGrass(grass, mesh.grass, mat.grass, p, rng, 0.7f);
            }

            // Trees: a few inside the pockets (blocking), a dense ring outside the arena ---------------------
            var trees = new GameObject("Trees").transform;
            trees.SetParent(root.transform, false);
            var placed = new List<Vector2>();
            foreach (Vector2 p in new[] { new Vector2(-15.5f, 9.5f), new Vector2(-10.5f, 14.5f), new Vector2(15.5f, -9.5f), new Vector2(10.5f, -14.5f) })
            {
                PlaceTree(trees, mesh.tree, mat, p, rng, 1.0f);
                placed.Add(p);
                obstacles.Add(new Vector4(p.x, 0f, p.y, 0.45f));
            }
            int attempts = 0;
            while (placed.Count < 70 && attempts++ < 2000)
            {
                float ang = Rand(rng, 0f, Mathf.PI * 2f);
                float r = Rand(rng, 21.2f, 29f);
                var p = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                bool clear = true;
                foreach (Vector2 q in placed)
                {
                    if ((q - p).sqrMagnitude < 2.2f * 2.2f) { clear = false; break; }
                }
                if (!clear) continue;
                PlaceTree(trees, mesh.tree, mat, p, rng, Rand(rng, 1.0f, 1.7f));
                placed.Add(p);
            }

            var field = root.AddComponent<ObstacleField>();
            field.circles = obstacles.ToArray();
        }

        // ------------------------------------------------------------------ pieces

        private static void BuildPillar(Transform parent, EnvMeshes mesh, EnvMaterials mat, Material particleTemplate, Vector2 at, bool withLight, System.Random rng)
        {
            var pillar = new GameObject("Pillar");
            pillar.transform.SetParent(parent, false);
            pillar.transform.localPosition = new Vector3(at.x, 0f, at.y);
            pillar.transform.localRotation = Quaternion.Euler(0f, Rand(rng, 0f, 90f), 0f);

            var body = NewMeshObject("Stone", pillar.transform, mesh.pillar, mat.stone);
            body.transform.localScale = new Vector3(1.35f, 1.7f, 1.35f);

            var cup = NewMeshObject("Brazier", pillar.transform, mesh.cup, mat.metal);
            cup.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            cup.transform.localScale = new Vector3(0.5f, 0.28f, 0.5f);

            BuildFlame(pillar.transform, mat.flame, new Vector3(0f, 1.95f, 0f));

            var glow = NewMeshObject("FloorGlow", pillar.transform, mesh.glowQuad, mat.floorGlow);
            glow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            glow.transform.localScale = Vector3.one * 5f;
            glow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            if (withLight)
            {
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(pillar.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = Palette.Hex(0xFF9A45);
                l.range = 4.5f;
                l.intensity = 2.2f;
                l.shadows = LightShadows.None;
            }
        }

        private static void BuildFlame(Transform parent, Material mat, Vector3 localPos)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(Palette.Hex(0xFF6A1F), Palette.Hex(0xFFC04A));
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 34f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 8f;
            sh.radius = 0.12f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Palette.Hex(0xFF5A1A), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static void PlaceTree(Transform parent, Mesh treeMesh, EnvMaterials mat, Vector2 p, System.Random rng, float scale)
        {
            var go = NewMeshObject("Pine", parent, treeMesh, null);
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat.bark, rng.NextDouble() < 0.5 ? mat.leaves : mat.leavesDark };
            go.transform.localPosition = new Vector3(p.x, 0f, p.y);
            go.transform.localRotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);
            float s = scale * Rand(rng, 0.9f, 1.1f);
            go.transform.localScale = new Vector3(s, s * Rand(rng, 0.95f, 1.15f), s);
        }

        private static void PlaceGrass(Transform parent, Mesh grassMesh, Material grassMat, Vector3 p, System.Random rng, float scale)
        {
            var go = NewMeshObject("Grass", parent, grassMesh, grassMat);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.transform.localPosition = p;
            go.transform.localRotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);
            float s = scale * Rand(rng, 0.8f, 1.25f);
            go.transform.localScale = new Vector3(s, s * Rand(rng, 0.85f, 1.2f), s);
        }

        /// <summary>Fills the pocket between a curved wall and the arena edge with grass clumps.</summary>
        private static void ScatterPocket(Transform parent, Mesh grassMesh, Material grassMat, Vector2[] line, System.Random rng, int count)
        {
            float a0 = Mathf.Atan2(line[0].y, line[0].x);
            float a1 = Mathf.Atan2(line[line.Length - 1].y, line[line.Length - 1].x);
            if (a1 < a0) { float t = a0; a0 = a1; a1 = t; }
            int placed = 0, attempts = 0;
            while (placed < count && attempts++ < count * 20)
            {
                float ang = Rand(rng, a0, a1);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float wallR = RadiusOfLine(line, dir);
                float r = Rand(rng, wallR + 1.9f, 20.6f);
                if (r <= wallR + 1.9f) continue;
                Vector2 p = dir * r;
                if (NearWall(p, 0.8f)) continue;
                PlaceGrass(parent, grassMesh, grassMat, new Vector3(p.x, 0f, p.y), rng, 1f);
                placed++;
            }
        }

        private static float RadiusOfLine(Vector2[] line, Vector2 dir)
        {
            // Intersection of the ray from the centre with the polyline (largest hit).
            float best = 0f;
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector2 a = line[i], b = line[i + 1], e = b - a;
                float den = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(den) < 1e-5f) continue;
                float t = (a.x * e.y - a.y * e.x) / den;
                float u = (a.x * dir.y - a.y * dir.x) / den;
                if (t > 0f && u >= 0f && u <= 1f) best = Mathf.Max(best, t);
            }
            return best > 0f ? best : 17f;
        }

        /// <summary>Spine circles of every cliff wall (x, z, radius), filled while the walls are built.</summary>
        private static readonly List<Vector3> wallCircles = new List<Vector3>();

        private static bool NearWall(Vector2 p, float clearance)
        {
            foreach (Vector3 c in wallCircles)
            {
                float r = c.z + clearance;
                if ((new Vector2(c.x, c.y) - p).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ cliff walls

        /// <summary>
        /// Builds one rock outcrop along a polyline: smoothed spine, bulging width profile, a three-tier
        /// flat-shaded cliff (wide foot, rough ledge, mossy top) plus small pines and grass on top.
        /// Adds blocking circles and returns the two tip positions for braziers.
        /// </summary>
        private static void BuildCliffWall(Transform parent, Vector2[] line, float maxHalfWidth, EnvMaterials mat, EnvMeshes mesh,
            System.Random rng, List<Vector4> obstacles, out Vector2 tipA, out Vector2 tipB)
        {
            // Spine: Catmull-Rom through the control points, sampled every ~0.45 units.
            var spine = new List<Vector2>();
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector2 p0 = line[Mathf.Max(0, i - 1)], p1 = line[i], p2 = line[i + 1], p3 = line[Mathf.Min(line.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / 0.45f));
                for (int k = 0; k < steps; k++) spine.Add(CatmullRom(p0, p1, p2, p3, k / (float)steps));
            }
            spine.Add(line[line.Length - 1]);
            int n = spine.Count;

            var dirs = new Vector2[n];
            var half = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = spine[Mathf.Min(n - 1, i + 1)] - spine[Mathf.Max(0, i - 1)];
                dirs[i] = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
                float t = i / (float)(n - 1);
                half[i] = maxHalfWidth * (0.55f + 0.45f * Mathf.Sin(t * Mathf.PI)) * Rand(rng, 0.9f, 1.08f);
            }

            // Closed outline: left side forward, end cap, right side back, start cap.
            var outline = new List<Vector2>();
            var outward = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                Vector2 left = new Vector2(-dirs[i].y, dirs[i].x);
                outline.Add(spine[i] + left * half[i]);
                outward.Add(left);
            }
            AddCap(outline, outward, spine[n - 1], dirs[n - 1], half[n - 1], true);
            for (int i = n - 1; i >= 0; i--)
            {
                Vector2 right = new Vector2(dirs[i].y, -dirs[i].x);
                outline.Add(spine[i] + right * half[i]);
                outward.Add(right);
            }
            AddCap(outline, outward, spine[0], -dirs[0], half[0], true);

            // Three rings: foot (y 0, pushed out), ledge (y ~0.7, jagged), top (y ~1.55, pulled in).
            int m = outline.Count;
            var foot = new Vector3[m];
            var ledge = new Vector3[m];
            var top = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                Vector2 o = outline[i], w = outward[i];
                Vector2 f = o + w * Rand(rng, 0.28f, 0.42f);
                Vector2 l = o + w * Rand(rng, -0.05f, 0.18f);
                Vector2 tp = o - w * Rand(rng, 0.12f, 0.25f);
                foot[i] = new Vector3(f.x, 0f, f.y);
                ledge[i] = new Vector3(l.x, Rand(rng, 0.6f, 0.85f), l.y);
                top[i] = new Vector3(tp.x, Rand(rng, 1.45f, 1.7f), tp.y);
            }

            var sides = new FlatBuilder();
            var cap = new FlatBuilder();
            for (int i = 0; i < m; i++)
            {
                int j = (i + 1) % m;
                Vector2 wo = (outward[i] + outward[j]).normalized;
                var out3 = new Vector3(wo.x, 0f, wo.y);
                sides.QuadFacing(foot[i], ledge[i], ledge[j], foot[j], out3);
                sides.QuadFacing(ledge[i], top[i], top[j], ledge[j], out3 + Vector3.up * 0.3f);
            }

            // Top: fan from points raised along the spine (gives a gentle mound instead of a flat lid).
            var spineTop = new Vector3[n];
            for (int i = 0; i < n; i++) spineTop[i] = new Vector3(spine[i].x, Rand(rng, 1.75f, 1.95f), spine[i].y);
            // Outline index layout: left 0..n-1, end cap n..n+3, right (reversed) n+4..2n+3, start cap 2n+4..2n+7.
            const int capPts = 4;
            int Right(int i) => n + capPts + (n - 1 - i);
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 l0 = top[i], l1 = top[i + 1], r0 = top[Right(i)], r1 = top[Right(i + 1)];
                Vector3 s0 = spineTop[i], s1 = spineTop[i + 1];
                cap.TriFacing(l0, l1, s1, Vector3.up);
                cap.TriFacing(l0, s1, s0, Vector3.up);
                cap.TriFacing(s0, s1, r1, Vector3.up);
                cap.TriFacing(s0, r1, r0, Vector3.up);
            }
            // End cap fan: left end -> cap points -> right end.
            Vector3 prev = top[n - 1];
            for (int k = 0; k <= capPts; k++)
            {
                Vector3 next = k < capPts ? top[n + k] : top[Right(n - 1)];
                cap.TriFacing(spineTop[n - 1], prev, next, Vector3.up);
                prev = next;
            }
            // Start cap fan: right start -> cap points -> left start.
            prev = top[Right(0)];
            for (int k = 0; k <= capPts; k++)
            {
                Vector3 next = k < capPts ? top[2 * n + capPts + k] : top[0];
                cap.TriFacing(spineTop[0], prev, next, Vector3.up);
                prev = next;
            }

            Mesh wallMesh = SaveMesh("Cliff_" + wallMeshIndex++, FlatBuilder.Combine(sides, cap));
            var go = NewMeshObject("Cliff", parent, wallMesh, null);
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat.cliff, mat.mossTop };

            // Overgrowth on top: a few small pines and grass tufts.
            int treeCount = Mathf.Max(1, n / 7);
            for (int k = 0; k < treeCount; k++)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((k + 0.5f) / treeCount * (n - 1)), 0, n - 1);
                Vector2 side = new Vector2(-dirs[i].y, dirs[i].x) * Rand(rng, -half[i] * 0.4f, half[i] * 0.4f);
                Vector2 p = spine[i] + side;
                var tree = NewMeshObject("TopPine", go.transform, mesh.tree, null);
                tree.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat.bark, rng.NextDouble() < 0.5 ? mat.leaves : mat.leavesDark };
                tree.transform.localPosition = new Vector3(p.x, 1.65f, p.y);
                tree.transform.localRotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);
                tree.transform.localScale = Vector3.one * Rand(rng, 0.5f, 0.72f);
            }
            for (int k = 0; k < n; k++)
            {
                Vector2 side = new Vector2(-dirs[k].y, dirs[k].x) * Rand(rng, -half[k] * 0.6f, half[k] * 0.6f);
                Vector2 p = spine[k] + side;
                PlaceGrass(go.transform, mesh.grass, mat.grass, new Vector3(p.x, 1.6f, p.y), rng, 0.6f);
            }

            // Collision follows the foot of the cliff.
            for (int i = 0; i < n; i++)
            {
                float r = half[i] + 0.3f;
                obstacles.Add(new Vector4(spine[i].x, 0f, spine[i].y, r));
                wallCircles.Add(new Vector3(spine[i].x, spine[i].y, r));
            }

            tipA = spine[0] - dirs[0] * (half[0] + 0.75f);
            tipB = spine[n - 1] + dirs[n - 1] * (half[n - 1] + 0.75f);
        }

        private static int wallMeshIndex;

        private static void AddCap(List<Vector2> outline, List<Vector2> outward, Vector2 center, Vector2 forward, float radius, bool skipEnds)
        {
            Vector2 left = new Vector2(-forward.y, forward.x);
            const int segs = 5;
            for (int k = 1; k < segs; k++)
            {
                float a = k / (float)segs * Mathf.PI;          // left -> forward -> right
                Vector2 w = left * Mathf.Cos(a) + forward * Mathf.Sin(a);
                outline.Add(center + w * radius);
                outward.Add(w);
            }
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static GameObject NewMeshObject(string name, Transform parent, Mesh m, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ------------------------------------------------------------------ materials

        private static EnvMaterials BuildEnvMaterials()
        {
            Shader lit = FindShader("Universal Render Pipeline/Lit");
            Shader glow = FindShader("MobaTrainer/ParticleGlow");
            var m = new EnvMaterials
            {
                stone = LitMat("EnvStone", lit, 0xA3A7A4, 0.12f),
                stoneDark = LitMat("EnvStoneDark", lit, 0x7C827F, 0.1f),
                metal = LitMat("EnvMetal", lit, 0x2E2A27, 0.35f),
                bark = LitMat("EnvBark", lit, 0x5C4330, 0.05f),
                leaves = LitMat("EnvLeaves", lit, 0x4F8A3C, 0.08f),
                leavesDark = LitMat("EnvLeavesDark", lit, 0x3B7035, 0.08f),
                grass = LitMat("EnvGrass", lit, 0x7DAA45, 0.05f),
                flame = MakeMaterial("EnvFlame", glow),
                floorGlow = MakeMaterial("EnvFloorGlow", glow),
                cliff = LitMat("EnvCliff", lit, 0x6F6B63, 0.08f),
                mossTop = LitMat("EnvMossTop", lit, 0x4C7A34, 0.05f),
            };
            m.grass.SetFloat("_Cull", 0f); // blades are single quads seen from both sides
            m.flame.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            m.flame.SetFloat("_Softness", 1.4f);
            m.floorGlow.SetColor("_Color", Palette.Hex(0xFF8A3A, 0.32f));
            m.floorGlow.SetFloat("_Softness", 1.6f);
            foreach (Material x in new[] { m.stone, m.stoneDark, m.metal, m.bark, m.leaves, m.leavesDark, m.grass, m.flame, m.floorGlow, m.cliff, m.mossTop })
            {
                EditorUtility.SetDirty(x);
            }
            return m;
        }

        private static Material LitMat(string name, Shader lit, int hex, float smoothness)
        {
            Material m = MakeMaterial(name, lit);
            m.SetColor("_BaseColor", Palette.Hex(hex));
            m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;
            return m;
        }

        // ------------------------------------------------------------------ low-poly meshes

        private static EnvMeshes BuildEnvMeshes()
        {
            var rng = new System.Random(7);
            var m = new EnvMeshes { rocks = new Mesh[4] };
            for (int i = 0; i < m.rocks.Length; i++) m.rocks[i] = SaveMesh("Rock" + i, RockMesh(rng, 0.09f));
            m.pillar = SaveMesh("Pillar", PrismMesh(6, 0.5f, 0.42f, 1f, rng, 0.03f));
            m.cup = SaveMesh("Cup", PrismMesh(8, 0.75f, 1f, 1f, rng, 0f));
            m.tree = SaveMesh("Pine", PineMesh(rng));
            m.grass = SaveMesh("GrassClump", GrassMesh(rng));
            m.glowQuad = SaveMesh("GlowQuad", GlowQuadMesh());
            return m;
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = PropDir + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            mesh.name = name;
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Unit box (base at y=0) with jittered corners, flat shaded.</summary>
        private static Mesh RockMesh(System.Random rng, float jitter)
        {
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? -0.5f : 0.5f;
                float y = (i & 2) == 0 ? 0f : 1f;
                float z = (i & 4) == 0 ? -0.5f : 0.5f;
                c[i] = new Vector3(x + Rand(rng, -jitter, jitter), y + (y > 0f ? Rand(rng, -jitter, jitter) : 0f), z + Rand(rng, -jitter, jitter));
                if (y > 0f) { c[i].x *= 0.9f; c[i].z *= 0.9f; }
            }
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, // -z, +z
                new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 }, // -x, +x
                new[] { 2, 6, 7, 3 },                        // top
            };
            var b = new FlatBuilder();
            foreach (int[] f in faces) b.Quad(c[f[0]], c[f[1]], c[f[2]], c[f[3]]);
            return b.ToMesh();
        }

        /// <summary>Tapered prism, base at y=0, height 1.</summary>
        private static Mesh PrismMesh(int sides, float bottomR, float topR, float height, System.Random rng, float jitter)
        {
            var b = new FlatBuilder();
            var bot = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float j = 1f + Rand(rng, -jitter, jitter);
                bot[i] = new Vector3(Mathf.Cos(a) * bottomR * j, 0f, Mathf.Sin(a) * bottomR * j);
                top[i] = new Vector3(Mathf.Cos(a) * topR * j, height, Mathf.Sin(a) * topR * j);
            }
            var topCenter = new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                b.Quad(bot[i], top[i], top[n], bot[n]);
                b.Tri(topCenter, top[n], top[i]);
            }
            return b.ToMesh();
        }

        /// <summary>Low-poly pine: hexagonal trunk + four stacked cones. Submesh 0 = bark, 1 = leaves.</summary>
        private static Mesh PineMesh(System.Random rng)
        {
            var trunk = new FlatBuilder();
            var leaves = new FlatBuilder();

            const int trunkSides = 6;
            for (int i = 0; i < trunkSides; i++)
            {
                float a0 = i / (float)trunkSides * Mathf.PI * 2f, a1 = (i + 1) / (float)trunkSides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.2f, 0f, Mathf.Sin(a0) * 0.2f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.2f, 0f, Mathf.Sin(a1) * 0.2f);
                trunk.Quad(p0, p0 * 0.8f + Vector3.up, p1 * 0.8f + Vector3.up, p1);
            }

            float[] radius = { 1.35f, 1.1f, 0.82f, 0.5f };
            float[] baseY = { 0.7f, 1.45f, 2.15f, 2.8f };
            float[] height = { 1.35f, 1.2f, 1.05f, 0.9f };
            const int sides = 7;
            for (int k = 0; k < radius.Length; k++)
            {
                var apex = new Vector3(Rand(rng, -0.04f, 0.04f), baseY[k] + height[k], Rand(rng, -0.04f, 0.04f));
                var ring = new Vector3[sides];
                float offset = Rand(rng, 0f, 1f);
                for (int i = 0; i < sides; i++)
                {
                    float a = (i + offset) / sides * Mathf.PI * 2f;
                    float r = radius[k] * Rand(rng, 0.88f, 1.08f);
                    ring[i] = new Vector3(Mathf.Cos(a) * r, baseY[k] + Rand(rng, -0.08f, 0.05f), Mathf.Sin(a) * r);
                }
                var under = new Vector3(0f, baseY[k] + 0.18f, 0f);
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    leaves.Tri(ring[i], apex, ring[n]);
                    leaves.Tri(ring[n], under, ring[i]);
                }
            }
            return FlatBuilder.Combine(trunk, leaves);
        }

        /// <summary>A tuft of blades. Normals point up so both sides light like the ground.</summary>
        private static Mesh GrassMesh(System.Random rng)
        {
            var b = new FlatBuilder { upNormals = true };
            for (int i = 0; i < 11; i++)
            {
                float ang = Rand(rng, 0f, Mathf.PI * 2f);
                float r = Rand(rng, 0f, 0.3f);
                var root = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float yaw = Rand(rng, 0f, Mathf.PI * 2f);
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * Rand(rng, 0.05f, 0.08f);
                var lean = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Rand(rng, 0.05f, 0.22f);
                var tip = root + lean + Vector3.up * Rand(rng, 0.4f, 0.75f);
                b.Tri(root - side, tip, root + side);
            }
            return b.ToMesh();
        }

        private static Mesh GlowQuadMesh()
        {
            var mesh = new Mesh();
            mesh.SetVertices(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) });
            mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            return mesh;
        }

        /// <summary>Accumulates unshared triangles so every face gets its own flat normal.</summary>
        private class FlatBuilder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public bool upNormals;

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = upNormals ? Vector3.up : Vector3.Cross(b - a, c - a).normalized;
                v.Add(a); v.Add(b); v.Add(c);
                n.Add(normal); n.Add(normal); n.Add(normal);
            }

            /// <summary>Triangle whose winding is flipped if needed so its normal faces <paramref name="facing"/>.</summary>
            public void TriFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
            {
                Vector3 nrm = Vector3.Cross(b - a, c - a);
                if (nrm.sqrMagnitude < 1e-10f) return;
                if (Vector3.Dot(nrm, facing) < 0f) Tri(a, c, b);
                else Tri(a, b, c);
            }

            public void QuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
            {
                TriFacing(a, b, c, facing);
                TriFacing(a, c, d, facing);
            }

            /// <summary>Quad a-b-c-d in clockwise order seen from outside.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                var tris = new int[v.Count];
                for (int i = 0; i < tris.Length; i++) tris[i] = i;
                mesh.SetTriangles(tris, 0);
                return mesh;
            }

            public static Mesh Combine(FlatBuilder a, FlatBuilder b)
            {
                var mesh = new Mesh { subMeshCount = 2 };
                var verts = new List<Vector3>(a.v); verts.AddRange(b.v);
                var norms = new List<Vector3>(a.n); norms.AddRange(b.n);
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                var t0 = new int[a.v.Count];
                for (int i = 0; i < t0.Length; i++) t0[i] = i;
                var t1 = new int[b.v.Count];
                for (int i = 0; i < t1.Length; i++) t1[i] = a.v.Count + i;
                mesh.SetTriangles(t0, 0);
                mesh.SetTriangles(t1, 1);
                return mesh;
            }
        }
    }
}
