using UnityEditor;
using UnityEngine;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// After scripts recompile, rebuilds the generated Main scene if the builder version changed,
    /// so a code update never runs against an out-of-date scene. Also warns before Play if it is stale.
    /// </summary>
    [InitializeOnLoad]
    public static class MobaTrainerAutoBuild
    {
        static MobaTrainerAutoBuild()
        {
            EditorApplication.delayCall += TryRebuild;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private static bool Outdated => EditorPrefs.GetInt(MobaTrainerSetup.SceneVersionKey, 0) != MobaTrainerSetup.SceneVersion;

        private static void TryRebuild()
        {
            if (!Outdated || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Debug.Log("[MOBA Trainer] Code updated - rebuilding the Main scene automatically.");
            MobaTrainerSetup.BuildAll();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && Outdated)
            {
                Debug.LogWarning("[MOBA Trainer] The Main scene is older than the code. Stop Play and run Tools > MOBA Trainer > Build Main Scene.");
            }
        }
    }
}
