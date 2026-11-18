using UnityEditor;
using UnityEngine;

namespace MobaTrainer.ExtremeSurvivor.EditorTools
{
    /// <summary>Creates the Extreme Survivor data assets once. Never overwrites them, so your tuning survives scene rebuilds.</summary>
    public static class SurvivorAssets
    {
        public const string Folder = "Assets/_Project/Data/ExtremeSurvivor";
        public const string ConfigPath = Folder + "/SurvivorConfig.asset";

        public const string ItemFolder = Folder + "/Items";
        public const string IconFolder = "Assets/_Project/Art/Items";

        public static SurvivorConfig EnsureConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<SurvivorConfig>(ConfigPath);
            if (cfg == null)
            {
                EnsureFolder();
                cfg = ScriptableObject.CreateInstance<SurvivorConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
                Debug.Log("[Extreme Survivor] Created " + ConfigPath + " (edit it to tune the mode).");
            }
            if (cfg.dataVersion < SurvivorConfig.CurrentDataVersion)
            {
                cfg.Upgrade();
                EditorUtility.SetDirty(cfg);
                Debug.Log("[Extreme Survivor] Config updated to the new default values (v" + SurvivorConfig.CurrentDataVersion + ").");
            }
            EnsureItems(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        /// <summary>One asset per item (created once, never overwritten); the config lists them.</summary>
        public static void EnsureItems(SurvivorConfig cfg)
        {
            EnsureFolder();
            if (!AssetDatabase.IsValidFolder(ItemFolder)) AssetDatabase.CreateFolder(Folder, "Items");
            bool changed = false;
            if (cfg.items == null) cfg.items = new System.Collections.Generic.List<SurvivorItem>();
            cfg.items.RemoveAll(i => i == null);
            foreach (SurvivorItem def in SurvivorItemDefaults.Create())
            {
                string path = ItemFolder + "/" + def.id + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<SurvivorItem>(path);
                if (asset == null)
                {
                    AssetDatabase.CreateAsset(def, path);
                    asset = def;
                }
                else
                {
                    Object.DestroyImmediate(def);
                }
                if (!cfg.items.Contains(asset)) { cfg.items.Add(asset); changed = true; }
                if (asset.icon == null)
                {
                    Sprite icon = LoadIcon(asset.id);
                    if (icon != null) { asset.icon = icon; EditorUtility.SetDirty(asset); }
                }
            }
            if (changed) EditorUtility.SetDirty(cfg);
        }

        /// <summary>Item icon PNGs live in Art/Items/&lt;id&gt;.png (imported as UI sprites).</summary>
        private static Sprite LoadIcon(string id)
        {
            string path = IconFolder + "/" + id + ".png";
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data")) AssetDatabase.CreateFolder("Assets/_Project", "Data");
            AssetDatabase.CreateFolder("Assets/_Project/Data", "ExtremeSurvivor");
        }

        [MenuItem("Tools/MOBA Trainer/Extreme Survivor/Select Config")]
        private static void SelectConfig()
        {
            Selection.activeObject = EnsureConfig();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
