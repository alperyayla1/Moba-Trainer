using MobaTrainer.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// Anime-style Wayfarer (experimental): rigged FBX with Idle / Walk / Cast takes, one painted atlas
    /// material (BaseColor, Normal, MetallicSmoothness, Emission for the arcane gauntlet). Driven by
    /// <see cref="HeroAnimDriver"/>. The primitive Wayfarer stays in the roster as the classic look.
    /// Returns null when the files are missing (the classic model is used instead).
    /// </summary>
    public static partial class MobaTrainerSetup
    {
        private const string AnimeHeroDir = ArtDir + "/Characters/AnimeHero";
        /// <summary>Rest height of the source model and its native run speed (units/s) at that height.</summary>
        private const float AnimeSourceHeight = 2.160407f, AnimeNativeRunSpeed = 3.25f;

        private static CharacterBuild BuildAnimeHero(Transform parent)
        {
            string fbxPath = AnimeHeroDir + "/AnimeHero.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return null;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; dirty = true; }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (!importer.bakeAxisConversion) { importer.bakeAxisConversion = true; dirty = true; }
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation c in clips)
            {
                string n = c.takeName.ToLowerInvariant();
                string want = n.Contains("walk") ? "Walk" : n.Contains("idle") ? "Idle" : n.Contains("cast") ? "Cast" : c.name;
                bool loop = want == "Walk" || want == "Idle";
                if (c.name != want || c.loopTime != loop) { c.name = want; c.loopTime = loop; dirty = true; }
            }
            if (dirty)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelAsset == null) return null;
            AnimationClip idle = null, walk = null, cast = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (!(o is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                if (clip.name == "Idle") idle = clip;
                else if (clip.name == "Walk") walk = clip;
                else if (clip.name == "Cast") cast = clip;
            }
            if (walk == null || cast == null)
            {
                Debug.LogWarning("[MOBA Trainer] AnimeHero.fbx has no Walk/Cast clips: using the classic Wayfarer.");
                return null;
            }

            string ctrlPath = GenDir + "/AnimeHero.controller";
            if (AssetDatabase.LoadAssetAtPath<Object>(ctrlPath) != null) AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle != null ? idle : walk;
            if (idle == null) sIdle.speed = 0f;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            var sCast = sm.AddState("Cast"); sCast.motion = cast;
            sm.defaultState = sIdle;
            EditorUtility.SetDirty(ctrl);

            Texture2D Tex(string file, bool normal, bool linear)
            {
                string path = AnimeHeroDir + "/" + file;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null)
                {
                    bool d = false;
                    if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; d = true; }
                    if (!normal && linear && imp.sRGBTexture) { imp.sRGBTexture = false; d = true; }
                    if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; d = true; }
                    if (d) imp.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            Shader lit = FindShader("Universal Render Pipeline/Lit");
            Material mat = MakeMaterial("CharAnimeHero", lit);
            mat.SetTexture("_BaseMap", Tex("AnimeHero_BaseColor.png", false, false));
            mat.SetColor("_BaseColor", Color.white);
            Texture2D nrm = Tex("AnimeHero_Normal.png", true, true);
            if (nrm != null) { mat.SetTexture("_BumpMap", nrm); mat.SetFloat("_BumpScale", 0.5f); mat.EnableKeyword("_NORMALMAP"); }
            Texture2D msk = Tex("AnimeHero_MetallicSmoothness.png", false, true);
            if (msk != null) { mat.SetTexture("_MetallicGlossMap", msk); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness", 1f); }
            Texture2D emi = Tex("AnimeHero_Emission.png", false, false);
            if (emi != null)
            {
                mat.SetTexture("_EmissionMap", emi);
                mat.SetColor("_EmissionColor", Color.white * 2.4f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);

            var model = new GameObject("AnimeModel").transform;
            model.SetParent(parent, false);
            var inst = (GameObject)Object.Instantiate(modelAsset);
            inst.name = "AnimeHero";
            inst.transform.SetParent(model, false);
            // The FBX imports facing -Z (seen in game: it walked backwards), so turn it to face +Z.
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var renderers = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) { Object.DestroyImmediate(model.gameObject); return null; }
            float h = 0f;
            foreach (SkinnedMeshRenderer r in renderers)
            {
                r.sharedMaterial = mat;
                r.updateWhenOffscreen = true;
                r.shadowCastingMode = ShadowCastingMode.On;
                h = Mathf.Max(h, r.bounds.size.y / Mathf.Max(1e-4f, model.lossyScale.y));
            }
            if (h > 0.01f) inst.transform.localScale *= HeroHeight / h;

            var animator = inst.GetComponent<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var driver = model.gameObject.AddComponent<HeroAnimDriver>();
            driver.animator = animator;
            driver.walkClipSpeed = AnimeNativeRunSpeed * (HeroHeight / AnimeSourceHeight);
            driver.castClipLength = cast.length;

            var flash = new Renderer[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) flash[i] = renderers[i];
            return new CharacterBuild { model = model, animator = null, flashRenderers = flash };
        }
    }
}
