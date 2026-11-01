using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTools
{
    // Builds the standalone Windows player. run.ps1 runs it in batch mode:
    // Unity -batchmode -quit -executeMethod Unity.Rea.QuickDuel.EditorTools.QuickDuelBuild.BuildWindows
    public static class QuickDuelBuild
    {
        public const string WindowsPath = "Build/QuickDuel/QuickDuel.exe";

        [MenuItem("QuickDuel/Build Windows Player")]
        public static void BuildWindows()
        {
            // The original's icon on the player (UQD-T50).
            PlayerIconBuilder.Ensure();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                scenes = BuildScenes.Order;

            Directory.CreateDirectory(Path.GetDirectoryName(WindowsPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"QuickDuel build: {summary.result}, {summary.totalErrors} errors, {summary.totalSize} bytes -> {WindowsPath}");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
