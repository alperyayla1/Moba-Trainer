using System.Collections.Generic;
using System.IO;
using MobaTrainer.Core;
using MobaTrainer.Gameplay;
using MobaTrainer.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// One-click project setup: import settings, generated textures, materials, theme, ability data,
    /// prefabs and the Main scene. Safe to re-run (it rebuilds generated assets and the scene).
    /// Menu: Tools ▸ MOBA Trainer ▸ Build Main Scene
    /// </summary>
    public static partial class MobaTrainerSetup
    {
        public const string Root = "Assets/_Project";
        public const string ArtDir = Root + "/Art";
        public const string GenDir = Root + "/Generated";
        public const string DataDir = Root + "/Data";
        public const string AbilityDir = DataDir + "/Abilities";
        public const string MatDir = Root + "/Materials";
        public const string PrefabDir = Root + "/Prefabs";
        public const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/Main.unity";

        public const float ArenaRadius = 20f; // ~4000 game units across

        internal class Materials
        {
            public Material floor, playerBody, playerAccent, dummyBody, transparent, healthBar, telegraph, projectile, particle;
        }

        internal class Abilities
        {
            public AbilityDefinition q, w, e, blink;
        }

        internal class Prefabs
        {
            public GroundMarker marker;
            public Transform projectile;
            public TargetDummy dummy;
        }

        /// <summary>Bump whenever the builder output changes; the editor rebuilds the scene automatically.</summary>
        public const int SceneVersion = 10;
        public const string SceneVersionKey = "MobaTrainer.SceneVersion";

        [MenuItem("Tools/MOBA Trainer/Build Main Scene", priority = 0)]
        public static void BuildAll()
        {
            if (!CheckTextMeshPro()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                // Open the fresh scene FIRST: NewScene unloads unused assets, which would invalidate
                // any material/prefab references created before it.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                EditorUtility.DisplayProgressBar("MOBA Trainer", "Folders & import settings", 0.1f);
                EnsureFolders();
                ConfigureImports();

                EditorUtility.DisplayProgressBar("MOBA Trainer", "Theme & font", 0.25f);
                UITheme theme = BuildTheme();

                EditorUtility.DisplayProgressBar("MOBA Trainer", "Materials", 0.4f);
                Materials mats = BuildMaterials();

                EditorUtility.DisplayProgressBar("MOBA Trainer", "Ability data", 0.5f);
                Abilities abilities = BuildAbilities();

                EditorUtility.DisplayProgressBar("MOBA Trainer", "Prefabs", 0.6f);
                Prefabs prefabs = BuildPrefabs(mats);

                EditorUtility.DisplayProgressBar("MOBA Trainer", "Scene", 0.8f);
                BuildScene(theme, mats, abilities, prefabs);

                ConfigureProject();
                AssetDatabase.SaveAssets();
                EditorPrefs.SetInt(SceneVersionKey, SceneVersion);
                Debug.Log("[MOBA Trainer] Main scene built (v" + SceneVersion + "): " + ScenePath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ checks & folders

        private static bool CheckTextMeshPro()
        {
            bool hasSettings = Resources.Load<TMP_Settings>("TMP Settings") != null;
            bool hasShader = Shader.Find("TextMeshPro/Distance Field") != null;
            if (hasSettings && hasShader) return true;
            EditorUtility.DisplayDialog("MOBA Trainer",
                "TextMeshPro essentials are missing.\n\nRun Window > TextMeshPro > Import TMP Essential Resources, then run Tools > MOBA Trainer > Build Main Scene again.",
                "OK");
            return false;
        }

        private static void EnsureFolders()
        {
            foreach (string dir in new[] { GenDir, DataDir, AbilityDir, MatDir, PrefabDir, SceneDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
                }
            }
        }

        // ------------------------------------------------------------------ import settings

        private static void ConfigureImports()
        {
            string gauntlet = ArtDir + "/Cursors/Gauntlet.png";
            ConfigureCursorTexture(gauntlet);

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio/Music" }))
            {
                ConfigureAudio(AssetDatabase.GUIDToAssetPath(guid), AudioClipLoadType.Streaming);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio/SFX" }))
            {
                ConfigureAudio(AssetDatabase.GUIDToAssetPath(guid), AudioClipLoadType.DecompressOnLoad);
            }

            // Line icons + decorative rings (pre-rendered PNGs, downscaled at runtime -> mipmaps on).
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir + "/UI/Icons" }))
            {
                ConfigureSprite(AssetDatabase.GUIDToAssetPath(guid), Vector4.zero, true);
            }

            GenerateTexture(GenDir + "/White.png", 8, 8, (x, y) => Color.white, true, Vector4.zero);
            GenerateTexture(GenDir + "/Rounded.png", 64, 64, RoundedPixel, true, new Vector4(20, 20, 20, 20));
            GenerateTexture(GenDir + "/RoundedOutline.png", 64, 64, RoundedOutlinePixel, true, new Vector4(20, 20, 20, 20));
            GenerateTexture(GenDir + "/Circle.png", 64, 64, CirclePixel, true, Vector4.zero);
            GenerateTexture(GenDir + "/Glow.png", 256, 256, GlowPixel, true, Vector4.zero);
            GenerateTexture(GenDir + "/GradientLeft.png", 256, 4, (x, y) =>
            {
                float t = Mathf.Clamp01(1f - x / 255f);
                return new Color(0f, 0f, 0f, t * t * (3f - 2f * t));
            }, true, Vector4.zero);
            GenerateTexture(GenDir + "/ArrowCursor.png", 64, 64, ArrowPixel, false, Vector4.zero);
        }

        private static void ConfigureSprite(string path, Vector4 border = default, bool mipmaps = false)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spriteBorder = border;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = mipmaps;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        private static void ConfigureCursorTexture(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
        }

        private static void ConfigureAudio(string path, AudioClipLoadType loadType)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter ai)) return;
            AudioImporterSampleSettings s = ai.defaultSampleSettings;
            s.loadType = loadType;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = loadType == AudioClipLoadType.Streaming;
            ai.SaveAndReimport();
        }

        private static void GenerateTexture(string path, int w, int h, System.Func<int, int, Color> pixel, bool sprite, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (sprite) ConfigureSprite(path, border);
            else ConfigureCursorTexture(path);
        }

        // Signed distance to a 64x64 rounded rect with 16 px corners (negative inside).
        private static float RoundedSdf(float x, float y)
        {
            const float half = 32f, r = 16f;
            float qx = Mathf.Abs(x - half) - (half - r);
            float qy = Mathf.Abs(y - half) - (half - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Color RoundedPixel(int x, int y)
        {
            float d = RoundedSdf(x + 0.5f, y + 0.5f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
        }

        private static Color RoundedOutlinePixel(int x, int y)
        {
            float d = RoundedSdf(x + 0.5f, y + 0.5f);
            const float width = 1.5f;
            float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + width + 0.5f);
            return new Color(1f, 1f, 1f, a);
        }

        private static Color CirclePixel(int x, int y)
        {
            float d = new Vector2(x + 0.5f - 32f, y + 0.5f - 32f).magnitude - 31f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
        }

        private static Color GlowPixel(int x, int y)
        {
            float r = new Vector2(x + 0.5f - 128f, y + 0.5f - 128f).magnitude / 128f;
            float a = Mathf.Clamp01(1f - r);
            a = a * a * (3f - 2f * a);
            return new Color(1f, 1f, 1f, a * a);
        }

        // Modern pointer, 64 px space, origin top-left, tip at (4,3).
        private static readonly Vector2[] ArrowPoly =
        {
            new Vector2(4, 3), new Vector2(4, 44), new Vector2(14.5f, 34.5f), new Vector2(21.5f, 51f),
            new Vector2(28.5f, 48f), new Vector2(21.5f, 31.5f), new Vector2(35f, 31.5f),
        };

        private static Color ArrowPixel(int x, int y)
        {
            // 4x4 supersampling; texture rows go bottom-up, polygon is top-down.
            float fill = 0f, outline = 0f, shadow = 0f;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                var p = new Vector2(x + (sx + 0.5f) / 4f, 64f - (y + (sy + 0.5f) / 4f));
                bool inside = PointInPoly(p, ArrowPoly);
                float d = DistanceToPoly(p, ArrowPoly);
                if (inside && d > 1.6f) fill += 1f / 16f;
                else if (inside || d <= 0.6f) outline += 1f / 16f;
                else
                {
                    Vector2 sp = p - new Vector2(1.5f, 2.5f);
                    bool sIn = PointInPoly(sp, ArrowPoly);
                    float sd = DistanceToPoly(sp, ArrowPoly);
                    shadow += (sIn ? 1f : Mathf.Clamp01(1f - sd / 3f)) / 16f;
                }
            }
            Color white = Palette.Hex(0xF7F7FA);
            Color dark = Palette.Bg;
            float a = Mathf.Clamp01(fill + outline);
            if (a <= 0f) return new Color(0f, 0f, 0f, shadow * 0.35f);
            Color c = Color.Lerp(dark, white, fill / a);
            c.a = Mathf.Clamp01(a + shadow * 0.35f * (1f - a));
            return c;
        }

        private static bool PointInPoly(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        private static float DistanceToPoly(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        // ------------------------------------------------------------------ theme & font

        private static UITheme BuildTheme()
        {
            string path = DataDir + "/UITheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, path);
            }

            theme.white = AssetDatabase.LoadAssetAtPath<Sprite>(GenDir + "/White.png");
            theme.rounded = AssetDatabase.LoadAssetAtPath<Sprite>(GenDir + "/Rounded.png");
            theme.roundedOutline = AssetDatabase.LoadAssetAtPath<Sprite>(GenDir + "/RoundedOutline.png");
            theme.circle = AssetDatabase.LoadAssetAtPath<Sprite>(GenDir + "/Circle.png");
            theme.glow = AssetDatabase.LoadAssetAtPath<Sprite>(GenDir + "/Glow.png");
            theme.ringThin = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/UI/Icons/RingThin.png");
            theme.ringDashed = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/UI/Icons/RingDashed.png");
            string[] iconNames = System.Enum.GetNames(typeof(UIIcon));
            theme.icons = new Sprite[iconNames.Length];
            for (int i = 0; i < iconNames.Length; i++)
            {
                theme.icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/UI/Icons/" + iconNames[i] + ".png");
                if (theme.icons[i] == null) Debug.LogWarning("[MOBA Trainer] Missing icon: " + iconNames[i]);
            }
            theme.fontRegular = BuildFont("Montserrat-Regular");
            theme.fontMedium = BuildFont("Montserrat-Medium");
            theme.fontSemibold = BuildFont("Montserrat-SemiBold");
            theme.fontHeading = BuildFont("Cinzel-SemiBold");
            theme.fontDisplay = BuildFont("Cinzel-Bold");
            theme.gradientLeft = AssetDatabase.LoadAssetAtPath<Sprite>(GenDir + "/GradientLeft.png");
            EditorUtility.SetDirty(theme);
            return theme;
        }

        /// <summary>Creates (once) a dynamic SDF font asset from Art/Fonts/&lt;file&gt;.ttf.</summary>
        private static TMP_FontAsset BuildFont(string file)
        {
            string assetPath = ArtDir + "/Fonts/" + file + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(ArtDir + "/Fonts/" + file + ".ttf");
            if (source == null)
            {
                Debug.LogError("[MOBA Trainer] Missing font file: " + file + ".ttf");
                return TMP_Settings.defaultFontAsset;
            }

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null)
            {
                Debug.LogError("[MOBA Trainer] Could not create font asset for " + file);
                return TMP_Settings.defaultFontAsset;
            }

            fa.name = file + " SDF";
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTexture.name = file + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            fa.material.name = file + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·×‹›–…", out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ------------------------------------------------------------------ materials

        private static Material MakeMaterial(string name, Shader shader)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        private static Shader FindShader(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null) Debug.LogError("[MOBA Trainer] Shader not found: " + name);
            return s;
        }

        private static Materials BuildMaterials()
        {
            Shader lit = FindShader("Universal Render Pipeline/Lit");
            Shader unlit = FindShader("Universal Render Pipeline/Unlit");
            Shader transparent = FindShader("MobaTrainer/UnlitTransparent");
            Shader telegraph = FindShader("MobaTrainer/Telegraph");
            Shader floor = FindShader("MobaTrainer/StylizedGround");

            var m = new Materials
            {
                floor = MakeMaterial("ArenaFloor", floor),
                playerBody = MakeMaterial("PlayerBody", lit),
                playerAccent = MakeMaterial("PlayerAccent", unlit),
                dummyBody = MakeMaterial("DummyBody", lit),
                transparent = MakeMaterial("GroundIndicator", transparent),
                healthBar = MakeMaterial("HealthBar", transparent),
                telegraph = MakeMaterial("Telegraph", telegraph),
                projectile = MakeMaterial("PlayerProjectile", unlit),
                particle = MakeMaterial("ParticleGlow", FindShader("MobaTrainer/ParticleGlow")),
            };

            m.floor.SetFloat("_Radius", ArenaRadius);
            m.floor.SetFloat("_TileSize", 1.15f);
            m.floor.SetFloat("_MossAmount", 0.5f);
            m.floor.SetColor("_Outer", Palette.Hex(0x121715));

            m.playerBody.SetColor("_BaseColor", Palette.Player);
            m.playerBody.SetFloat("_Smoothness", 0.45f);
            m.playerBody.EnableKeyword("_EMISSION");
            m.playerBody.SetColor("_EmissionColor", Palette.PlayerDark * 0.12f);
            m.playerBody.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            m.playerAccent.SetColor("_BaseColor", Palette.Text);

            m.dummyBody.SetColor("_BaseColor", Palette.Hex(0x9C5140)); // clay-red training dummies
            m.dummyBody.SetFloat("_Smoothness", 0.25f);

            m.transparent.SetColor("_Color", Color.white);
            m.healthBar.SetColor("_Color", Color.white);
            m.healthBar.SetFloat("_ZTest", (float)CompareFunction.Always);
            m.healthBar.renderQueue = 3100;

            m.telegraph.SetColor("_Color", Palette.Accent.WithAlpha(0.4f));

            m.projectile.SetColor("_BaseColor", new Color(2.2f, 2.0f, 1.4f)); // HDR white-gold, blooms

            m.particle.SetColor("_Color", Color.white);
            m.particle.SetFloat("_Softness", 1.8f);

            foreach (Material mat in new[] { m.floor, m.playerBody, m.playerAccent, m.dummyBody, m.transparent, m.healthBar, m.telegraph, m.projectile, m.particle })
            {
                EditorUtility.SetDirty(mat);
            }
            return m;
        }

        // ------------------------------------------------------------------ ability data

        /// <summary>Loads or creates the asset and (re)applies the kit numbers every build so the tuning stays canonical.</summary>
        private static AbilityDefinition Ability(string file, System.Action<AbilityDefinition> setup)
        {
            string path = AbilityDir + "/" + file + ".asset";
            var a = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<AbilityDefinition>();
                setup(a);
                AssetDatabase.CreateAsset(a, path);
            }
            else
            {
                setup(a);
                EditorUtility.SetDirty(a);
            }
            return a;
        }

        // Scale: 1 Unity unit = 100 game units of a typical MOBA. Numbers follow a classic
        // ranged marksman kit: two quarter-second skillshots and a short blink that fires a homing bolt.
        private static Abilities BuildAbilities()
        {
            return new Abilities
            {
                q = Ability("Q_Bolt", a =>
                {
                    a.displayName = "Bolt"; a.glyph = ""; a.shape = AbilityShape.LinearSkillshot;
                    a.castTime = 0.25f; a.cooldown = 2.5f; a.range = 12f; a.size = 1.2f; a.speed = 20f; a.damage = 80f;
                    a.ignoresCastMode = false;
                    a.marksTarget = false; a.detonatesMark = true; a.cooldownRefundOnHit = 1.5f;
                }),
                w = Ability("W_Wave", a =>
                {
                    a.displayName = "Wave"; a.glyph = ""; a.shape = AbilityShape.LinearSkillshot;
                    a.castTime = 0.25f; a.cooldown = 3f; a.range = 12f; a.size = 1.6f; a.speed = 17f; a.damage = 100f; // damage = mark burst
                    a.ignoresCastMode = false;
                    a.marksTarget = true; a.markDuration = 4f; a.detonatesMark = false; a.cooldownRefundOnHit = 0f;
                }),
                e = Ability("E_Shift", a =>
                {
                    a.displayName = "Shift"; a.glyph = ""; a.shape = AbilityShape.ShiftBolt;
                    a.castTime = 0.25f; a.cooldown = 5f; a.range = 4.75f; a.boltRange = 7.5f; a.speed = 20f; a.damage = 60f;
                    a.ignoresCastMode = false;
                    a.marksTarget = false; a.detonatesMark = true; a.cooldownRefundOnHit = 0f;
                }),
                blink = Ability("D_Blink", a =>
                {
                    a.displayName = "Blink"; a.glyph = ""; a.shape = AbilityShape.Blink;
                    a.castTime = 0f; a.cooldown = 6f; a.range = 4f; a.damage = 0f;
                    a.ignoresCastMode = false;
                }),
            };
        }

        // ------------------------------------------------------------------ project settings

        private static void ConfigureProject()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Marmotsoft";

            // Lowest audio latency ("Best latency" = 256-sample DSP buffer) so UI clicks feel instant.
            Object[] audioManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (audioManager != null && audioManager.Length > 0)
            {
                var so = new SerializedObject(audioManager[0]);
                SerializedProperty dsp = so.FindProperty("m_DSPBufferSize");
                if (dsp != null) { dsp.intValue = 256; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = false;

            // Crisper edges on the placeholder geometry.
            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (rp != null)
            {
                rp.msaaSampleCount = 4;
                rp.shadowDistance = 60f;
                EditorUtility.SetDirty(rp);
            }
            var qrp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (qrp != null && qrp != rp)
            {
                qrp.msaaSampleCount = 4;
                qrp.shadowDistance = 60f;
                EditorUtility.SetDirty(qrp);
            }
        }
    }
}
