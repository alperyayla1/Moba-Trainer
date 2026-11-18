using UnityEditor;
using UnityEngine;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// After scripts recompile, rebuilds the generated Main scene if the builder version changed,
    /// so a code update never runs against an out-of-date scene. If the editor is busy (compiling,
    /// importing, in Play mode) it keeps waiting and rebuilds as soon as it can. Pressing Play with a
    /// stale scene cancels Play, rebuilds, and asks to press Play again.
    /// </summary>
    [InitializeOnLoad]
    public static class MobaTrainerAutoBuild
    {
        static MobaTrainerAutoBuild()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static bool Outdated => EditorPrefs.GetInt(MobaTrainerSetup.SceneVersionKey, 0) != MobaTrainerSetup.SceneVersion;

        private static void Poll()
        {
            if (!Outdated) { EditorApplication.update -= Poll; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Poll;
            Debug.Log("[MOBA Trainer] Code updated - rebuilding the Main scene automatically.");
            MobaTrainerSetup.BuildAll();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && Outdated)
            {
                EditorApplication.isPlaying = false;
                Debug.LogWarning("[MOBA Trainer] The Main scene was older than the code: rebuilding it now. Press Play again when it's done.");
                EditorApplication.update -= Poll;
                EditorApplication.update += Poll;
            }
            else if (change == PlayModeStateChange.EnteredEditMode && Outdated)
            {
                EditorApplication.update -= Poll;
                EditorApplication.update += Poll;
            }
        }
    }
}
