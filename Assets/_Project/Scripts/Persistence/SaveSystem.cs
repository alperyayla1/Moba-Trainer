using System;
using System.IO;
using UnityEngine;

namespace MobaTrainer.Persistence
{
    /// <summary>
    /// Local JSON persistence under <c>persistentDataPath/Save</c>.
    /// Writes are atomic (temp file + replace) so a crash never leaves a half-written save.
    /// All files are plain JSON so they can later be mirrored to Steam Cloud unchanged.
    /// </summary>
    public static class SaveSystem
    {
        public static string Root
        {
            get
            {
                string root = Path.Combine(Application.persistentDataPath, "Save");
                if (!Directory.Exists(root)) Directory.CreateDirectory(root);
                return root;
            }
        }

        public static string PathFor(string relativePath) => Path.Combine(Root, relativePath);

        public static bool TryLoad<T>(string relativePath, out T data) where T : class
        {
            data = null;
            string text = ReadText(relativePath);
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                data = JsonUtility.FromJson<T>(text);
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not parse '{relativePath}': {e.Message}");
                return false;
            }
        }

        public static void Save<T>(string relativePath, T data, bool pretty = true)
        {
            WriteText(relativePath, JsonUtility.ToJson(data, pretty));
        }

        public static string ReadText(string relativePath)
        {
            try
            {
                string path = PathFor(relativePath);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Read failed '{relativePath}': {e.Message}");
                return null;
            }
        }

        public static void WriteText(string relativePath, string text)
        {
            try
            {
                string path = PathFor(relativePath);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, text);
                if (File.Exists(path))
                {
                    File.Replace(tmp, path, null);
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Write failed '{relativePath}': {e.Message}");
            }
        }

        public static void Delete(string relativePath)
        {
            string path = PathFor(relativePath);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
