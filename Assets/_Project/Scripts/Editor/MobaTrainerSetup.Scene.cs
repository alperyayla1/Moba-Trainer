using MobaTrainer.Audio;
using MobaTrainer.Core;
using MobaTrainer.Flow;
using MobaTrainer.Gameplay;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using MobaTrainer.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobaTrainer.EditorTools
{
    public static partial class MobaTrainerSetup
    {
        // ------------------------------------------------------------------ prefabs

        private static GameObject NewPrimitive(PrimitiveType type, string name, Transform parent, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static ProceduralFlatMesh NewFlat(string name, Transform parent, Material mat, ProceduralFlatMesh.Shape shape,
            float radius, Color color, float thickness = 0.06f, float centerAlpha = 1f, float edgeAlpha = 0f, float y = 0.015f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var flat = go.AddComponent<ProceduralFlatMesh>();
            flat.shape = shape;
            flat.radius = radius;
            flat.thickness = thickness;
            flat.centerAlpha = centerAlpha;
            flat.edgeAlpha = edgeAlpha;
            flat.color = color;
            return flat;
        }

        private static HealthBar NewHealthBar(Transform parent, Material mat, Color fill, float height)
        {
            var root = new GameObject("HealthBar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, height, 0f);
            var bar = root.AddComponent<HealthBar>();
            bar.background = NewQuad("Background", root.transform, mat);
            bar.fill = NewQuad("Fill", root.transform, mat);
            bar.shield = NewQuad("Shield", root.transform, mat);
            bar.fillColor = fill;
            return bar;
        }

        private static MeshRenderer NewQuad(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return mr;
        }

        private static T SavePrefab<T>(GameObject temp, string file) where T : Component
        {
            string path = PrefabDir + "/" + file + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return asset.GetComponent<T>();
        }

        private static Prefabs BuildPrefabs(Materials m)
        {
            var p = new Prefabs();

            // Ground marker
            var marker = new GameObject("GroundMarker", typeof(MeshFilter), typeof(MeshRenderer));
            marker.GetComponent<MeshRenderer>().sharedMaterial = m.transparent;
            marker.AddComponent<GroundMarker>();
            p.marker = SavePrefab<GroundMarker>(marker, "GroundMarker");

            // Player projectile
            // Glowing white-gold core (HDR colour set per shot -> bloom) with a tapered energy trail.
            GameObject proj = NewPrimitive(PrimitiveType.Sphere, "PlayerProjectile", null, m.projectile);
            proj.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            proj.transform.localScale = Vector3.one * 0.35f;
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(proj.transform, false);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.sharedMaterial = LoadOrMakeTrailMaterial();
            trail.time = 0.16f;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.7f), new Keyframe(1f, 0f));
            trail.widthMultiplier = 1f;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 4;

            p.projectile = SavePrefab<Transform>(proj, "PlayerProjectile");

            // Target dummy
            var dummy = new GameObject("TargetDummy");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(dummy.transform, false);
            CharacterBuild enemyChar = BuildCharacter(visual.transform, EnemyLook, true);
            NewFlat("Shadow", visual.transform, m.transparent, ProceduralFlatMesh.Shape.Disc, 0.6f, new Color(0f, 0f, 0f, 0.35f));
            NewFlat("Ring", visual.transform, m.transparent, ProceduralFlatMesh.Shape.Ring, 0.7f, Palette.Hex(0xE0443C, 0.8f), 0.06f, 1f, 0f, 0.02f);
            HealthBar dummyBar = NewHealthBar(visual.transform, m.healthBar, Palette.Hex(0xE0443C), 2.95f);
            var td = dummy.AddComponent<TargetDummy>();
            td.team = Team.Enemy;
            td.radius = 0.65f; // 65-unit gameplay radius, like a typical ranged champion
            td.height = 2.3f;
            td.maxHealth = 400f;
            td.visualRoot = visual;
            td.healthBar = dummyBar;
            td.flashRenderers = enemyChar.flashRenderers;
            p.dummy = SavePrefab<TargetDummy>(dummy, "TargetDummy");

            return p;
        }

        // ------------------------------------------------------------------ scene

        private static void BuildScene(UITheme theme, Materials mats, Abilities abilities, Prefabs prefabs)
        {
            Scene scene = SceneManager.GetActiveScene();

            // Re-resolve prefab references from disk in case an asset refresh unloaded them.
            prefabs.marker = AssetDatabase.LoadAssetAtPath<GroundMarker>(PrefabDir + "/GroundMarker.prefab");
            prefabs.projectile = AssetDatabase.LoadAssetAtPath<Transform>(PrefabDir + "/PlayerProjectile.prefab");
            prefabs.dummy = AssetDatabase.LoadAssetAtPath<TargetDummy>(PrefabDir + "/TargetDummy.prefab");

            // Lighting & environment ------------------------------------------------
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.Hex(0x9DB0BF);
            RenderSettings.ambientEquatorColor = Palette.Hex(0x6E7A6E);
            RenderSettings.ambientGroundColor = Palette.Hex(0x3A3A31);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Palette.Hex(0x1A201D);
            RenderSettings.fogDensity = 0.011f;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Palette.Hex(0xFFF1DC);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            lightGo.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            VolumeProfile profile = BuildVolumeProfile();
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // Camera ----------------------------------------------------------------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Hex(0x131816);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            var camCtrl = camGo.AddComponent<MobaCameraController>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 20f, -19f), Quaternion.Euler(56f, 0f, 0f));

            // Systems ---------------------------------------------------------------
            var systems = new GameObject("Systems");
            systems.AddComponent<SettingsManager>();
            systems.AddComponent<InputManager>();
            CursorService cursorService = systems.AddComponent<CursorService>();
            var uiAudio = systems.AddComponent<UIAudio>();
            uiAudio.audioSource = systems.AddComponent<AudioSource>();
            uiAudio.audioSource.playOnAwake = false;
            uiAudio.clickClip = LoadClip("SFX/Click.wav");

            // No music for now (design direction v3). MusicManager stays in the codebase for later.

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            // Left without an actions asset on purpose: the module falls back to its built-in
            // default UI actions at runtime (AssignDefaultActions() throws when called from edit code).
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Arena -----------------------------------------------------------------
            var arenaGo = new GameObject("Arena");
            var arena = arenaGo.AddComponent<Arena>();
            arena.radius = ArenaRadius;

            var floorGo = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer));
            floorGo.transform.SetParent(arenaGo.transform, false);
            floorGo.GetComponent<MeshFilter>().sharedMesh = SaveFloorMesh();
            var floorRenderer = floorGo.GetComponent<MeshRenderer>();
            floorRenderer.sharedMaterial = mats.floor;
            floorRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var fxGo = new GameObject("FxSystem");
            fxGo.transform.SetParent(arenaGo.transform, false);
            var fx = fxGo.AddComponent<FxSystem>();
            fx.markerPrefab = prefabs.marker;
            fx.transparentMaterial = mats.transparent;

            var skillGo = new GameObject("PlayerSkillSystem");
            skillGo.transform.SetParent(arenaGo.transform, false);
            var skills = skillGo.AddComponent<PlayerSkillSystem>();
            skills.projectilePrefab = prefabs.projectile;
            skills.markerPrefab = prefabs.marker;
            skills.transparentMaterial = mats.transparent;
            skills.telegraphMaterial = mats.telegraph;

            var indicatorGo = new GameObject("CastIndicator");
            indicatorGo.transform.SetParent(arenaGo.transform, false);
            var indicator = indicatorGo.AddComponent<CastIndicator>();
            indicator.rangeRing = InstantiateMarker(prefabs.marker, indicatorGo.transform, "RangeRing");
            indicator.shape = InstantiateMarker(prefabs.marker, indicatorGo.transform, "Shape");
            indicator.destination = InstantiateMarker(prefabs.marker, indicatorGo.transform, "Destination");
            indicator.attackRangeRing = InstantiateMarker(prefabs.marker, indicatorGo.transform, "AttackRange");

            PlayerController player = BuildPlayer(arenaGo.transform, mats, abilities, indicator, cam);

            BuildEnvironment(arenaGo.transform, mats.particle);
            BuildAtmosphere(arenaGo.transform, mats.particle);

            var enemySkillGo = new GameObject("EnemySkillSystem");
            enemySkillGo.transform.SetParent(arenaGo.transform, false);
            var enemySkills = enemySkillGo.AddComponent<EnemySkillSystem>();
            enemySkills.projectilePrefab = prefabs.projectile;
            enemySkills.markerPrefab = prefabs.marker;
            enemySkills.transparentMaterial = mats.transparent;
            enemySkills.telegraphMaterial = mats.telegraph;

            var sandboxGo = new GameObject("Sandbox");
            sandboxGo.transform.SetParent(arenaGo.transform, false);
            var sandbox = sandboxGo.AddComponent<SandboxController>();
            sandbox.dummyPrefab = prefabs.dummy;
            sandbox.player = player;

            var drillsGo = new GameObject("Drills");
            drillsGo.transform.SetParent(arenaGo.transform, false);
            T Drill<T>(string name) where T : Scenario
            {
                var go = new GameObject(name);
                go.transform.SetParent(drillsGo.transform, false);
                var sc = go.AddComponent<T>();
                sc.dummyPrefab = prefabs.dummy;
                sc.player = player;
                return sc;
            }
            var skillshotDrill = Drill<SkillshotScenario>("SkillshotDrill");
            var dodgeDrill = Drill<DodgeScenario>("DodgeDrill");
            var duelDrill = Drill<DuelScenario>("DuelDrill");
            var kitingDrill = Drill<KitingScenario>("KitingDrill");
            var lastHitDrill = Drill<LastHitScenario>("LastHitDrill");
            var blinkDrill = Drill<BlinkScenario>("BlinkDrill");
            var comboDrill = Drill<ComboScenario>("ComboDrill");
            var awarenessDrill = Drill<AwarenessScenario>("AwarenessDrill");

            camCtrl.target = player.transform;

            // UI ----------------------------------------------------------------------
            Canvas canvas = NewCanvas("UI", 0, true);
            MainMenuController menu = BuildMainMenu(theme, canvas.transform);
            DrillsPanel drills = BuildDrills(theme, canvas.transform);
            HUDController hud = BuildHud(theme, canvas.transform);
            PausePanel pause = BuildPause(theme, canvas.transform);
            SettingsPanel settings = BuildSettings(theme, canvas.transform);
            ResultsPanel results = BuildResults(theme, canvas.transform);
            StatsPanel stats = BuildStats(theme, canvas.transform);

            Canvas cursorCanvas = NewCanvas("CursorCanvas", 32000, false);
            var cursorImg = UIFactory.Rect("Cursor", cursorCanvas.transform).gameObject.AddComponent<RawImage>();
            cursorImg.raycastTarget = false;
            cursorService.cursorRect = cursorImg.rectTransform;
            cursorService.cursorImage = cursorImg;
            cursorService.gauntletTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Cursors/Gauntlet.png");
            cursorService.arrowTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/ArrowCursor.png");
            cursorImg.texture = cursorService.arrowTexture;
            cursorImg.rectTransform.sizeDelta = cursorService.arrowSize;

            // Flow --------------------------------------------------------------------
            var flowGo = new GameObject("GameFlow");
            var flow = flowGo.AddComponent<GameFlowController>();
            flow.mainMenu = menu;
            flow.drillsPanel = drills;
            flow.hud = hud;
            flow.pausePanel = pause;
            flow.settingsPanel = settings;
            flow.arenaRoot = arenaGo;
            flow.sandbox = sandbox;
            flow.player = player;
            flow.cameraController = camCtrl;
            flow.resultsPanel = results;
            flow.statsPanel = stats;
            flow.skillshot = skillshotDrill;
            flow.dodge = dodgeDrill;
            flow.duel = duelDrill;
            flow.kiting = kitingDrill;
            flow.lastHit = lastHitDrill;
            flow.blink = blinkDrill;
            flow.combo = comboDrill;
            flow.awareness = awarenessDrill;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Material LoadOrMakeTrailMaterial()
        {
            Material m = MakeMaterial("ProjectileTrail", FindShader("MobaTrainer/AdditiveTrail"));
            m.SetFloat("_Intensity", 1.8f);
            m.SetFloat("_CoreWidth", 0.22f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static AudioClip LoadClip(string relative)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + relative);
            if (clip == null) Debug.LogWarning("[MOBA Trainer] Missing audio: " + relative);
            return clip;
        }

        private static GroundMarker InstantiateMarker(GroundMarker prefab, Transform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            go.name = name;
            go.SetActive(false);
            return go.GetComponent<GroundMarker>();
        }

        private static Mesh SaveFloorMesh()
        {
            string path = GenDir + "/ArenaFloor.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            Mesh mesh = MeshFactory.FloorDisc(ArenaRadius + 16f);
            mesh.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static VolumeProfile BuildVolumeProfile()
        {
            string path = GenDir + "/ArenaVolume.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.6f);
            AssetDatabase.AddObjectToAsset(bloom, profile);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.36f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);
            AssetDatabase.AddObjectToAsset(vignette, profile);

            var grade = profile.Add<ColorAdjustments>(true);
            grade.saturation.Override(12f);
            grade.contrast.Override(8f);
            AssetDatabase.AddObjectToAsset(grade, profile);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.Neutral);
            AssetDatabase.AddObjectToAsset(tonemap, profile);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static PlayerController BuildPlayer(Transform parent, Materials m, Abilities a, CastIndicator indicator, Camera cam)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, -6f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            CharacterBuild hero = BuildCharacter(visual.transform, PlayerLook, false);

            NewFlat("Shadow", go.transform, m.transparent, ProceduralFlatMesh.Shape.Disc, 0.6f, new Color(0f, 0f, 0f, 0.35f));
            NewFlat("SelectionRing", go.transform, m.transparent, ProceduralFlatMesh.Shape.Ring, 0.7f, Palette.Hex(0x5BD14E, 0.85f), 0.06f, 1f, 0f, 0.02f);

            var shield = new GameObject("ShieldBubble");
            shield.transform.SetParent(go.transform, false);
            NewFlat("Fill", shield.transform, m.transparent, ProceduralFlatMesh.Shape.Disc, 1.05f, new Color(1f, 1f, 1f, 0.12f), 0.06f, 0.4f, 1f, 0.025f);
            NewFlat("Edge", shield.transform, m.transparent, ProceduralFlatMesh.Shape.Ring, 1.05f, new Color(1f, 1f, 1f, 0.8f), 0.08f, 1f, 0f, 0.03f);
            shield.SetActive(false);

            HealthBar bar = NewHealthBar(go.transform, m.healthBar, Palette.Hex(0x5BD14E), 2.95f);

            var unit = go.AddComponent<Unit>();
            unit.team = Team.Player;
            unit.radius = 0.65f;
            unit.height = 2.3f;
            unit.maxHealth = 600f;
            unit.healthBar = bar;
            unit.flashRenderers = hero.flashRenderers;

            var motor = go.AddComponent<PlayerMotor>();
            motor.facingRoot = visual.transform;
            go.AddComponent<PlayerAttack>();
            var abilities = go.AddComponent<PlayerAbilities>();
            abilities.indicator = indicator;
            abilities.shieldBubble = shield;
            abilities.slots = new[]
            {
                new PlayerAbilities.Slot { action = GameAction.AbilityQ, ability = a.q },
                new PlayerAbilities.Slot { action = GameAction.AbilityW, ability = a.w },
                new PlayerAbilities.Slot { action = GameAction.AbilityE, ability = a.e },
                new PlayerAbilities.Slot { action = GameAction.Blink, ability = a.blink },
            };

            var controller = go.AddComponent<PlayerController>();
            controller.worldCamera = cam;
            return controller;
        }

        // ------------------------------------------------------------------ UI builders

        private static Canvas NewCanvas(string name, int sortingOrder, bool raycaster)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (raycaster) go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static T NewPanel<T>(Transform canvas, string name, UITheme theme) where T : UIPanel
        {
            RectTransform rt = UIFactory.Stretch(UIFactory.Rect(name, canvas));
            rt.gameObject.AddComponent<CanvasGroup>();
            var panel = rt.gameObject.AddComponent<T>();
            panel.fadeDuration = theme.fadeDuration;
            return panel;
        }

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Image Decor(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, float size)
        {
            Image g = UIFactory.Image(parent, name, color, sprite);
            UIFactory.Place(g.rectTransform, anchor, Center, pos, new Vector2(size, size));
            return g;
        }

        /// <summary>Shared animated backdrop: black, breathing red glow, slowly counter-rotating rings.</summary>
        private static void Backdrop(UITheme theme, Transform t, bool rings)
        {
            UIFactory.Stretch(UIFactory.Image(t, "Background", Palette.Bg, theme.white, true).rectTransform);

            Image glow = Decor(t, "GlowBottom", theme.glow, Palette.Accent.WithAlpha(0.16f), UIFactory.BottomCenter, new Vector2(0f, -120f), 1900f);
            var pulse = glow.gameObject.AddComponent<UIPulse>();
            pulse.minAlpha = 0.10f; pulse.maxAlpha = 0.20f; pulse.period = 7f;
            Decor(t, "GlowTop", theme.glow, Palette.PlayerDark.WithAlpha(0.08f), UIFactory.TopLeft, new Vector2(120f, -80f), 1200f);

            if (!rings) return;
            Image r1 = Decor(t, "RingDashed", theme.ringDashed, Palette.Accent.WithAlpha(0.10f), Center, new Vector2(0f, 10f), 640f);
            r1.gameObject.AddComponent<UISpin>().degreesPerSecond = 3f;
            Image r2 = Decor(t, "RingThin", theme.ringThin, Palette.Text.WithAlpha(0.05f), Center, new Vector2(0f, 10f), 820f);
            Image r3 = Decor(t, "RingDashedOuter", theme.ringDashed, Palette.Text.WithAlpha(0.035f), Center, new Vector2(0f, 10f), 1040f);
            r3.gameObject.AddComponent<UISpin>().degreesPerSecond = -1.5f;
        }

        private static MainMenuController BuildMainMenu(UITheme theme, Transform canvas)
        {
            var menu = NewPanel<MainMenuController>(canvas, "MainMenu", theme);
            menu.theme = theme;
            Transform t = menu.transform;
            const float left = 140f;

            // The live arena shows through; a soft gradient keeps the left side readable.
            Image grad = UIFactory.Image(t, "Gradient", Color.white, theme.gradientLeft);
            grad.rectTransform.anchorMin = new Vector2(0f, 0f);
            grad.rectTransform.anchorMax = new Vector2(0f, 1f);
            grad.rectTransform.pivot = new Vector2(0f, 0.5f);
            grad.rectTransform.sizeDelta = new Vector2(1150f, 0f);
            grad.rectTransform.anchoredPosition = Vector2.zero;
            Image glow = Decor(t, "Glow", theme.glow, Palette.Accent.WithAlpha(0.08f), UIFactory.BottomLeft, new Vector2(260f, 60f), 1300f);
            var pulse = glow.gameObject.AddComponent<UIPulse>();
            pulse.minAlpha = 0.05f; pulse.maxAlpha = 0.11f; pulse.period = 8f;

            TextMeshProUGUI t1 = UIFactory.Text(theme, t, "Title1", "MOBA", 96f, Palette.Text, TextAlignmentOptions.TopLeft, TextWeight.Display);
            t1.characterSpacing = 14f;
            UIFactory.Place(t1.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(left, -236f), new Vector2(900f, 110f));
            TextMeshProUGUI t2 = UIFactory.Text(theme, t, "Title2", "TRAINER", 96f, Palette.Accent, TextAlignmentOptions.TopLeft, TextWeight.Display);
            t2.characterSpacing = 14f;
            UIFactory.Place(t2.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(left, -338f), new Vector2(900f, 110f));
            Image rule = UIFactory.Image(t, "Rule", Palette.Accent, theme.white);
            UIFactory.Place(rule.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(left + 2f, -470f), new Vector2(80f, 2f));

            RectTransform list = UIFactory.Rect("Items", t);
            UIFactory.Place(list, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(left - 4f, -530f), new Vector2(560f, 300f));
            VerticalLayoutGroup vl = UIFactory.VerticalGroup(list, 4f);
            vl.childForceExpandWidth = false;
            menu.itemList = list;
            return menu;
        }

        private static DrillsPanel BuildDrills(UITheme theme, Transform canvas)
        {
            var drills = NewPanel<DrillsPanel>(canvas, "Drills", theme);
            drills.theme = theme;
            Transform t = drills.transform;

            UIFactory.Stretch(UIFactory.Image(t, "Shade", Palette.Bg.WithAlpha(0.6f), theme.white, true).rectTransform);
            Image glow = Decor(t, "Glow", theme.glow, Palette.Accent.WithAlpha(0.1f), UIFactory.BottomCenter, new Vector2(0f, -200f), 1800f);
            var pulse = glow.gameObject.AddComponent<UIPulse>();
            pulse.minAlpha = 0.07f; pulse.maxAlpha = 0.14f; pulse.period = 7f;

            TextMeshProUGUI title = UIFactory.Text(theme, t, "Title", "DRILLS", 56f, Palette.Text, TextAlignmentOptions.TopLeft, TextWeight.Display);
            title.characterSpacing = 14f;
            UIFactory.Place(title.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(140f, -96f), new Vector2(900f, 70f));
            Image rule = UIFactory.Image(t, "Rule", Palette.Accent, theme.white);
            UIFactory.Place(rule.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(142f, -176f), new Vector2(60f, 2f));

            Button back = UIFactory.IconButton(theme, t, "Back", UIIcon.Close, 48f, 18f, Palette.TextMuted, Palette.Text);
            UIFactory.Place((RectTransform)back.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-72f, -92f), new Vector2(48f, 48f));
            drills.backButton = back;

            const float gap = 20f;
            Vector2 card = drills.cardSize;
            RectTransform grid = UIFactory.Rect("Grid", t);
            UIFactory.Place(grid, Center, Center, new Vector2(0f, -50f), new Vector2(card.x * 3f + gap * 2f, card.y * 3f + gap * 2f));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = card;
            gl.spacing = new Vector2(gap, gap);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.MiddleCenter;
            drills.grid = grid;
            return drills;
        }

        private static void BuildAtmosphere(Transform parent, Material mat)
        {
            // Soft sunlit motes drifting over the plaza (fire sparks live on the braziers).
            MakeParticles(parent, "Motes", mat, 10f, 150, new Vector2(10f, 16f), new Vector2(0.06f, 0.16f),
                new Color(1f, 0.95f, 0.8f, 0.35f), new Color(1f, 1f, 0.9f, 0.15f), new Vector2(0.02f, 0.12f), 0.25f);
        }

        private static void MakeParticles(Transform parent, string name, Material mat, float rate, int max, Vector2 life, Vector2 size,
            Color colorA, Color colorB, Vector2 rise, float noiseStrength)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = rate;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(60f, 1f, 60f);

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.y = new ParticleSystem.MinMaxCurve(rise.x, rise.y);
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static HUDController BuildHud(UITheme theme, Transform canvas)
        {
            var hud = NewPanel<HUDController>(canvas, "HUD", theme);
            hud.theme = theme;
            hud.GetComponent<CanvasGroup>().blocksRaycasts = false;
            Transform t = hud.transform;

            hud.titleText = UIFactory.Text(theme, t, "Title", "FREE PRACTICE", theme.sizeSmall, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Heading);
            hud.titleText.characterSpacing = 12f;
            UIFactory.Place(hud.titleText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -28f), new Vector2(1000f, 24f));
            hud.hintText = UIFactory.Text(theme, t, "Hint", "", theme.sizeCaption, Palette.TextFaint, TextAlignmentOptions.Center);
            UIFactory.Place(hud.hintText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -52f), new Vector2(1400f, 22f));

            Image plate = UIFactory.Rounded(theme, t, "AbilityBar", Palette.BgElevated.WithAlpha(0.9f), theme.radiusPanel);
            RectTransform bar = plate.rectTransform;
            UIFactory.Place(bar, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 28f), new Vector2(460f, 80f));
            Image barOutline = UIFactory.Outline(theme, bar, Palette.Border, theme.radiusPanel);
            barOutline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HorizontalLayoutGroup hl = UIFactory.HorizontalGroup(bar, 8f, TextAnchor.MiddleCenter);
            hl.padding = new RectOffset(10, 10, 10, 10);
            var fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.abilityBar = bar;

            // Counters: hits / misses / time, small pills right above the ability bar.
            RectTransform counters = UIFactory.Rect("Counters", t);
            UIFactory.Place(counters, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 118f), new Vector2(400f, 30f));
            UIFactory.HorizontalGroup(counters, 8f, TextAnchor.MiddleCenter);
            hud.counterRow = counters;
            hud.hitsText = CounterPill(theme, counters, "Hits", UIIcon.Skillshot, Palette.Text);
            hud.missesText = CounterPill(theme, counters, "Misses", UIIcon.Close, Palette.Accent);
            hud.timeText = CounterPill(theme, counters, "Time", null, Palette.TextMuted);
            hud.timeText.text = "00:00";

            hud.statsText = UIFactory.Text(theme, t, "Stats", "", theme.sizeCaption, Palette.TextMuted, TextAlignmentOptions.TopLeft, TextWeight.Medium);
            hud.statsText.lineSpacing = 14f;
            UIFactory.Place(hud.statsText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(32f, -28f), new Vector2(700f, 120f));

            hud.cameraIcon = UIFactory.Icon(theme, t, "CameraIcon", UIIcon.Lock, 18f, Palette.TextFaint);
            UIFactory.Place(hud.cameraIcon.rectTransform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-60f, 34f), new Vector2(18f, 18f));
            hud.cameraText = UIFactory.Text(theme, t, "CameraKey", "Y", theme.sizeCaption, Palette.TextFaint, TextAlignmentOptions.Left, TextWeight.Medium);
            UIFactory.Place(hud.cameraText.rectTransform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-24f, 32f), new Vector2(30f, 22f));

            hud.centerText = UIFactory.Text(theme, t, "Countdown", "", 120f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Display);
            UIFactory.Place(hud.centerText.rectTransform, Center, Center, new Vector2(0f, 120f), new Vector2(600f, 160f));
            hud.centerText.gameObject.SetActive(false);

            hud.fpsText = UIFactory.Text(theme, t, "Fps", "", theme.sizeCaption, Palette.TextFaint, TextAlignmentOptions.TopRight, TextWeight.Medium);
            UIFactory.Place(hud.fpsText.rectTransform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-32f, -28f), new Vector2(300f, 24f));
            return hud;
        }

        private static TextMeshProUGUI CounterPill(UITheme theme, Transform parent, string name, UIIcon? icon, Color iconColor)
        {
            Image pill = UIFactory.Rounded(theme, parent, name, Palette.BgElevated.WithAlpha(0.85f), theme.radiusSmall);
            RectTransform rt = pill.rectTransform;
            UIFactory.Layout(pill, 30f, icon.HasValue ? 64f : 70f);
            HorizontalLayoutGroup hl = UIFactory.HorizontalGroup(rt, 6f, TextAnchor.MiddleCenter);
            hl.padding = new RectOffset(10, 10, 0, 0);
            if (icon.HasValue)
            {
                Image ic = UIFactory.Icon(theme, rt, "Icon", icon.Value, 14f, iconColor);
                UIFactory.Layout(ic, 14f, 14f);
            }
            TextMeshProUGUI txt = UIFactory.Text(theme, rt, "Value", "0", theme.sizeCaption, Palette.Text, TextAlignmentOptions.Center, TextWeight.Semibold);
            UIFactory.Layout(txt, 30f, icon.HasValue ? 26f : 50f);
            return txt;
        }

        private static ResultsPanel BuildResults(UITheme theme, Transform canvas)
        {
            var results = NewPanel<ResultsPanel>(canvas, "Results", theme);
            Transform t = results.transform;
            UIFactory.Stretch(UIFactory.Image(t, "Overlay", Palette.Overlay, theme.white, true).rectTransform);
            Image glow = Decor(t, "Glow", theme.glow, Palette.Accent.WithAlpha(0.12f), Center, new Vector2(0f, 80f), 1200f);
            glow.gameObject.AddComponent<UIPulse>().maxAlpha = 0.16f;

            results.modeText = UIFactory.Text(theme, t, "Mode", "SKILLSHOT", theme.sizeHeading, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Heading);
            results.modeText.characterSpacing = 14f;
            UIFactory.Place(results.modeText.rectTransform, Center, Center, new Vector2(0f, 250f), new Vector2(900f, 40f));

            results.scoreText = UIFactory.Text(theme, t, "Score", "0", 120f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Display);
            UIFactory.Place(results.scoreText.rectTransform, Center, Center, new Vector2(0f, 150f), new Vector2(900f, 150f));

            results.bestText = UIFactory.Text(theme, t, "Best", "", theme.sizeSmall, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Semibold);
            results.bestText.characterSpacing = 10f;
            UIFactory.Place(results.bestText.rectTransform, Center, Center, new Vector2(0f, 62f), new Vector2(600f, 28f));

            Image rule = UIFactory.Image(t, "Rule", Palette.Accent, theme.white);
            UIFactory.Place(rule.rectTransform, Center, Center, new Vector2(0f, 36f), new Vector2(60f, 2f));

            results.detailText = UIFactory.Text(theme, t, "Details", "", theme.sizeBody, Palette.Text, TextAlignmentOptions.Top, TextWeight.Medium);
            results.detailText.lineSpacing = 18f;
            UIFactory.Place(results.detailText.rectTransform, Center, new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(700f, 150f));

            RectTransform row = UIFactory.Rect("Buttons", t);
            UIFactory.Place(row, Center, Center, new Vector2(0f, -200f), new Vector2(640f, 48f));
            UIFactory.HorizontalGroup(row, 12f, TextAnchor.MiddleCenter);
            Button Make(string name, UIIcon ic, string label, ButtonStyle style)
            {
                Button b = UIFactory.IconLabelButton(theme, row, name, ic, label, style, new Vector2(200f, 48f));
                TextMeshProUGUI lt = UIFactory.Label(b);
                lt.font = theme.Font(TextWeight.Heading);
                lt.characterSpacing = 8f;
                UIFactory.Layout(b, 48f, 200f);
                return b;
            }
            results.retryButton = Make("Retry", UIIcon.Restart, "RETRY", ButtonStyle.Primary);
            results.statsButton = Make("Stats", UIIcon.Radar, "STATS", ButtonStyle.Ghost);
            results.menuButton = Make("Menu", UIIcon.Home, "MENU", ButtonStyle.Ghost);
            return results;
        }

        private static StatsPanel BuildStats(UITheme theme, Transform canvas)
        {
            var stats = NewPanel<StatsPanel>(canvas, "Stats", theme);
            stats.theme = theme;
            Transform t = stats.transform;
            UIFactory.Stretch(UIFactory.Image(t, "Shade", Palette.Bg.WithAlpha(0.72f), theme.white, true).rectTransform);

            TextMeshProUGUI title = UIFactory.Text(theme, t, "Title", "STATS", 56f, Palette.Text, TextAlignmentOptions.TopLeft, TextWeight.Display);
            title.characterSpacing = 14f;
            UIFactory.Place(title.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(140f, -96f), new Vector2(900f, 70f));
            Image rule = UIFactory.Image(t, "Rule", Palette.Accent, theme.white);
            UIFactory.Place(rule.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(142f, -176f), new Vector2(60f, 2f));

            stats.totalsText = UIFactory.Text(theme, t, "Totals", "", theme.sizeCaption, Palette.TextMuted, TextAlignmentOptions.TopLeft, TextWeight.Semibold);
            stats.totalsText.characterSpacing = 6f;
            UIFactory.Place(stats.totalsText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(142f, -192f), new Vector2(900f, 24f));

            Button back = UIFactory.IconButton(theme, t, "Back", UIIcon.Close, 48f, 18f, Palette.TextMuted, Palette.Text);
            UIFactory.Place((RectTransform)back.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-72f, -92f), new Vector2(48f, 48f));
            stats.backButton = back;

            stats.tabRow = UIFactory.Rect("Tabs", t);
            UIFactory.Place(stats.tabRow, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(132f, -240f), new Vector2(1500f, 44f));
            UIFactory.HorizontalGroup(stats.tabRow, 8f, TextAnchor.MiddleLeft);

            stats.tiles = UIFactory.Rect("Tiles", t);
            UIFactory.Place(stats.tiles, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(140f, -306f), new Vector2(860f, 96f));
            UIFactory.HorizontalGroup(stats.tiles, 14f, TextAnchor.MiddleLeft);

            stats.chartCaption = UIFactory.Text(theme, t, "ChartCaption", "", theme.sizeCaption, Palette.TextMuted, TextAlignmentOptions.TopLeft, TextWeight.Semibold);
            stats.chartCaption.characterSpacing = 6f;
            UIFactory.Place(stats.chartCaption.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(142f, -430f), new Vector2(860f, 22f));

            Image chartBg = UIFactory.Rounded(theme, t, "ChartBg", Palette.Surface.WithAlpha(0.45f), theme.radiusButton);
            UIFactory.Place(chartBg.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(140f, -460f), new Vector2(860f, 240f));
            stats.chart = UIFactory.Rect("Chart", chartBg.transform);
            UIFactory.Stretch(stats.chart, 20f, 20f, 20f, 20f);
            UIFactory.HorizontalGroup(stats.chart, 12f, TextAnchor.LowerLeft);

            TextMeshProUGUI listCaption = UIFactory.Text(theme, t, "ListCaption", "LATEST", theme.sizeCaption, Palette.TextMuted, TextAlignmentOptions.TopLeft, TextWeight.Semibold);
            listCaption.characterSpacing = 6f;
            UIFactory.Place(listCaption.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(1060f, -430f), new Vector2(600f, 22f));
            stats.list = UIFactory.Rect("List", t);
            UIFactory.Place(stats.list, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(1060f, -460f), new Vector2(700f, 240f));
            UIFactory.VerticalGroup(stats.list, 6f);
            return stats;
        }

        /// <summary>Centered modal card over a dark overlay.</summary>
        private static RectTransform Modal(UITheme theme, Transform panel, Vector2 size)
        {
            UIFactory.Stretch(UIFactory.Image(panel, "Overlay", Palette.Overlay, theme.white, true).rectTransform);
            Image window = UIFactory.Rounded(theme, panel, "Window", Palette.BgElevated, theme.radiusPanel, true);
            UIFactory.Place(window.rectTransform, Center, Center, Vector2.zero, size);
            UIFactory.Outline(theme, window.transform, Palette.Border, theme.radiusPanel);
            Image accent = UIFactory.Image(window.transform, "AccentLine", Palette.Accent, theme.white);
            accent.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            accent.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.sizeDelta = new Vector2(48f, 2f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            return window.rectTransform;
        }

        private static PausePanel BuildPause(UITheme theme, Transform canvas)
        {
            var pause = NewPanel<PausePanel>(canvas, "Pause", theme);
            Transform t = pause.transform;
            UIFactory.Stretch(UIFactory.Image(t, "Overlay", Palette.Overlay, theme.white, true).rectTransform);
            Image glow = Decor(t, "Glow", theme.glow, Palette.Accent.WithAlpha(0.10f), Center, Vector2.zero, 1100f);
            glow.gameObject.AddComponent<UIPulse>().maxAlpha = 0.14f;

            Image bar1 = UIFactory.Rounded(theme, t, "Bar1", Palette.Accent, 3f);
            UIFactory.Place(bar1.rectTransform, Center, Center, new Vector2(-9f, 150f), new Vector2(8f, 28f));
            Image bar2 = UIFactory.Rounded(theme, t, "Bar2", Palette.Accent, 3f);
            UIFactory.Place(bar2.rectTransform, Center, Center, new Vector2(9f, 150f), new Vector2(8f, 28f));

            RectTransform col = UIFactory.Rect("Buttons", t);
            UIFactory.Place(col, Center, new Vector2(0.5f, 1f), new Vector2(0f, 100f), new Vector2(280f, 240f));
            UIFactory.VerticalGroup(col, 8f);

            Button Make(string name, UIIcon ic, string label, ButtonStyle style)
            {
                Button b = UIFactory.IconLabelButton(theme, col, name, ic, label.ToUpperInvariant(), style, new Vector2(280f, 48f));
                TextMeshProUGUI lt = UIFactory.Label(b);
                lt.font = theme.Font(TextWeight.Heading);
                lt.characterSpacing = 8f;
                UIFactory.Layout(b, 48f);
                return b;
            }

            pause.resumeButton = Make("Resume", UIIcon.Play, "Resume", ButtonStyle.Primary);
            pause.restartButton = Make("Restart", UIIcon.Restart, "Restart", ButtonStyle.Ghost);
            pause.settingsButton = Make("Settings", UIIcon.Gear, "Settings", ButtonStyle.Ghost);
            pause.menuButton = Make("Menu", UIIcon.Home, "Menu", ButtonStyle.Danger);
            return pause;
        }

        private static SettingsPanel BuildSettings(UITheme theme, Transform canvas)
        {
            var settings = NewPanel<SettingsPanel>(canvas, "Settings", theme);
            settings.theme = theme;
            RectTransform w = Modal(theme, settings.transform, new Vector2(880f, 680f));

            Image header = UIFactory.Image(w, "Header", new Color(0f, 0f, 0f, 0f), theme.white, true);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.sizeDelta = new Vector2(0f, 84f);
            header.rectTransform.anchoredPosition = Vector2.zero;
            var drag = header.gameObject.AddComponent<DraggablePanel>();
            drag.targetPanel = w;
            settings.dragHandle = drag;

            // Icon tabs (left) + active tab name (right of tabs) + close (far right).
            RectTransform tabs = UIFactory.Rect("Tabs", header.transform);
            UIFactory.Place(tabs, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -18f), new Vector2(220f, 48f));
            UIFactory.HorizontalGroup(tabs, 4f, TextAnchor.MiddleLeft);
            settings.tabBar = tabs;

            TextMeshProUGUI title = UIFactory.Text(theme, header.transform, "TabTitle", "Audio", theme.sizeHeading, Palette.Text, TextAlignmentOptions.Right, TextWeight.Heading);
            UIFactory.Place(title.rectTransform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-84f, -26f), new Vector2(400f, 32f));
            settings.tabTitle = title;

            Button close = UIFactory.IconButton(theme, header.transform, "Close", UIIcon.Close, 40f, 16f, Palette.TextMuted, Palette.Text);
            UIFactory.Place((RectTransform)close.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-24f, -22f), new Vector2(40f, 40f));
            settings.closeButton = close;

            Image rule = UIFactory.Image(w, "TabRule", Palette.Border, theme.white);
            rule.rectTransform.anchorMin = new Vector2(0f, 1f);
            rule.rectTransform.anchorMax = new Vector2(1f, 1f);
            rule.rectTransform.pivot = new Vector2(0.5f, 1f);
            rule.rectTransform.sizeDelta = new Vector2(-48f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -84f);

            Image indicator = UIFactory.Rounded(theme, w, "TabIndicator", Palette.Accent, 1f);
            UIFactory.Place(indicator.rectTransform, UIFactory.TopCenter, Center, new Vector2(-392f, -83f), new Vector2(24f, 2f));
            settings.tabIndicator = indicator.rectTransform;

            RectTransform scrollRoot = UIFactory.Rect("Scroll", w);
            UIFactory.Stretch(scrollRoot, 32f, 32f, 100f, 24f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = UIFactory.Stretch(UIFactory.Rect("Viewport", scrollRoot));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image vpHit = viewport.gameObject.AddComponent<Image>();
            vpHit.color = new Color(0f, 0f, 0f, 0f);
            vpHit.raycastTarget = true;

            RectTransform content = UIFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UIFactory.VerticalGroup(content, 0f, new RectOffset(0, 8, 0, 16));
            var cfit = content.gameObject.AddComponent<ContentSizeFitter>();
            cfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            settings.content = content;
            settings.scrollRect = scroll;
            return settings;
        }
    }
}
