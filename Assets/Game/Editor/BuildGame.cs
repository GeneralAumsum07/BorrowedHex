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
    /// The plain entries are release builds: a development build sets Debug.isDebugBuild, which
    /// shows the dev-only endless tools ('Endless (debug)', 'Skip wave') on the main menu, so a
    /// build meant for players must never be one (D85). The '(development)' entries keep them for
    /// testing and write to separate folders so the two can never be mixed up.
    /// </summary>
    public static class BuildGame
    {
        static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [MenuItem("Borrowed Hex/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/BorrowedHex.exe", false);

        // BYOG 2026 jam submission. Only the folder and exe are renamed; productName stays the
        // same, so the window title and the save folder (persistentDataPath) do not move.
        [MenuItem("Borrowed Hex/Build/Windows (BYOG26 jam)")]
        public static void BuildJam() => Build(BuildTarget.StandaloneWindows64, "Builds/BorrowedHex_BYOG26_final/BorrowedHex_BYOG26_final.exe", false);

        [MenuItem("Borrowed Hex/Build/Web")]
        public static void BuildWeb() => Build(BuildTarget.WebGL, "Builds/Web", false);

        [MenuItem("Borrowed Hex/Build/Windows (development)")]
        public static void BuildWindowsDev() => Build(BuildTarget.StandaloneWindows64, "Builds/WindowsDev/BorrowedHex.exe", true);

        [MenuItem("Borrowed Hex/Build/Web (development)")]
        public static void BuildWebDev() => Build(BuildTarget.WebGL, "Builds/WebDev", true);

        static void Build(BuildTarget target, string output, bool development)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                target = target,
                locationPathName = output,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] {target}: {report.summary.result}, {report.summary.totalErrors} errors, " +
                      $"{report.summary.totalSize / (1024 * 1024)} MB -> {output}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
