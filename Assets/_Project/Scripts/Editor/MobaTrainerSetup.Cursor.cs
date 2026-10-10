using System.IO;
using MobaTrainer.Core;
using MobaTrainer.Input;
using UnityEditor;
using UnityEngine;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// Generates the game cursor: an original faceted metal shard with a dark gem core, dark rim,
    /// bevel highlight and soft drop shadow, in three states (normal dark core, attack red, cast blue) and
    /// three sizes (32/48/64). Imported as Cursor textures for Cursor.SetCursor.
    /// </summary>
    public static partial class MobaTrainerSetup
    {
        private const string CursorDir = GenDir + "/Cursor";
        private static readonly int[] CursorSizes = { 32, 48, 64 };

        // Faceted metal shard in a 64×64 design space, top-left origin. Tip (7, 3) is the hotspot.
        private static readonly Vector2[] ShardOuter = { new Vector2(7f, 3f), new Vector2(38f, 33f), new Vector2(22f, 60f), new Vector2(9f, 45f) };
        private static readonly Vector2[] ShardInner = { new Vector2(13f, 15f), new Vector2(30f, 34f), new Vector2(19f, 49f) };

        private struct CursorLook
        {
            public Color core, coreHi;
        }

        private static void BuildCursors(CursorService service)
        {
            if (!AssetDatabase.IsValidFolder(CursorDir)) AssetDatabase.CreateFolder(GenDir, "Cursor");
            var normal = new CursorLook { core = Palette.Hex(0x15131A), coreHi = Palette.Hex(0x3A3442) };
            var attack = new CursorLook { core = Palette.Hex(0x6E0E18), coreHi = Palette.Hex(0xE0414E) };
            var cast = new CursorLook { core = Palette.Hex(0x0E3550), coreHi = Palette.Hex(0x4CB5EC) };

            service.normalTextures = new Texture2D[CursorSizes.Length];
            service.attackTextures = new Texture2D[CursorSizes.Length];
            service.castTextures = new Texture2D[CursorSizes.Length];
            for (int i = 0; i < CursorSizes.Length; i++)
            {
                int n = CursorSizes[i];
                service.normalTextures[i] = WriteCursor($"Cursor_Normal_{n}.png", n, normal);
                service.attackTextures[i] = WriteCursor($"Cursor_Attack_{n}.png", n, attack);
                service.castTextures[i] = WriteCursor($"Cursor_Cast_{n}.png", n, cast);
            }
            service.hotspot = new Vector2(7f / 64f, 3f / 64f);
        }

        private static Texture2D WriteCursor(string file, int size, CursorLook look)
        {
            Color metalHi = Palette.Hex(0xF4EEDC), metalLo = Palette.Hex(0x8C7756), rimC = Palette.Hex(0x14110D);
            string path = CursorDir + "/" + file;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float k = 64f / size;
            const int ss = 4;
            const float w = 1f / (ss * ss);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color acc = Color.clear;
                float a = 0f, shadow = 0f;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    // Texture rows go bottom-up; design space is top-down.
                    var p = new Vector2((x + (sx + 0.5f) / ss) * k, 64f - (y + (sy + 0.5f) / ss) * k);
                    bool inside = PointInPoly(p, ShardOuter);
                    float d = DistanceToPoly(p, ShardOuter);
                    Color c;
                    if (inside && d > 1.1f)
                    {
                        bool inCore = PointInPoly(p, ShardInner);
                        float dc = DistanceToPoly(p, ShardInner);
                        if (inCore && dc > 0.9f)
                        {
                            c = Color.Lerp(look.coreHi, look.core, Mathf.Clamp01((p.y - 15f) / 34f));
                        }
                        else if (inCore || dc <= 0.9f)
                        {
                            c = rimC;
                        }
                        else
                        {
                            // Metal frame: two facets split by the tip→bottom line, vertical gradient, bevel glint.
                            float t = Mathf.Clamp01((p.y - 3f) / 57f);
                            Vector2 a0 = ShardOuter[0], a2 = ShardOuter[2];
                            bool left = (a2.x - a0.x) * (p.y - a0.y) - (a2.y - a0.y) * (p.x - a0.x) > 0f;
                            c = Color.Lerp(metalHi, metalLo, t * (left ? 0.6f : 1f) + (left ? 0f : 0.25f));
                            if (left && d < 3f) c = Color.Lerp(c, Color.white, 0.35f * (1f - d / 3f));
                        }
                    }
                    else if (inside || d <= 1.1f)
                    {
                        c = rimC;
                    }
                    else
                    {
                        Vector2 sp = p - new Vector2(2f, 3f);
                        shadow += w * (PointInPoly(sp, ShardOuter) ? 1f : Mathf.Clamp01(1f - DistanceToPoly(sp, ShardOuter) / 3.5f));
                        continue;
                    }
                    acc += c * w;
                    a += w;
                }

                if (a <= 0f)
                {
                    px[y * size + x] = new Color(0f, 0f, 0f, shadow * 0.45f);
                    continue;
                }
                Color col = acc / a;
                col.a = Mathf.Clamp01(a + shadow * 0.45f * (1f - a));
                px[y * size + x] = col;
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Cursor;
                ti.isReadable = true;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
