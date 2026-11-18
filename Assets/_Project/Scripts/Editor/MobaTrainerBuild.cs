using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// Tools > MOBA Trainer > Build Windows Game: builds into C:\Builds\MobaTrainer (a plain local
    /// folder; builds inside OneDrive get their data files corrupted by syncing) and launches the game.
    /// </summary>
    public static class MobaTrainerBuild
    {
        public const string OutputDir = @"C:\Builds\MobaTrainer";
        public const string ExeName = "MobaTrainer.exe";

        [MenuItem("Tools/MOBA Trainer/Build Windows Game (and run)", priority = 10)]
        public static void BuildAndRun()
        {
            Build(true);
        }

        [MenuItem("Tools/MOBA Trainer/Build Windows Game (no run)", priority = 11)]
        public static void BuildOnly()
        {
            Build(false);
        }

        private static void Build(bool run)
        {
            // Never ship an out-of-date generated scene.
            if (MobaTrainerAutoBuild.Outdated) MobaTrainerSetup.BuildAll();
            Directory.CreateDirectory(OutputDir);
            string exe = Path.Combine(OutputDir, ExeName);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MobaTrainerSetup.ScenePath },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[MOBA Trainer] Build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors). See the Console above.");
                return;
            }
            Debug.Log("[MOBA Trainer] Build OK: " + exe);
            if (run) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = OutputDir, UseShellExecute = true });
            else EditorUtility.RevealInFinder(exe);
        }
    }
}
