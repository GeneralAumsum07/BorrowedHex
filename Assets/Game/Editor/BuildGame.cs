using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Build entry points shared by the CLI (`unity build --execute-method ...`) and the menu.
    /// Both targets build the same scene list so Windows and Web never diverge.
    /// </summary>
    public static class BuildGame
    {
        static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [MenuItem("Borrowed Hex/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/BorrowedHex.exe");

        [MenuItem("Borrowed Hex/Build/Web")]
        public static void BuildWeb() => Build(BuildTarget.WebGL, "Builds/Web");

        static void Build(BuildTarget target, string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                target = target,
                locationPathName = output,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] {target}: {report.summary.result}, {report.summary.totalErrors} errors, " +
                      $"{report.summary.totalSize / (1024 * 1024)} MB -> {output}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
