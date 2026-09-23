#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jev.EditorTools
{
    /// <summary>
    /// Builds a standalone player that Tools/run_jev_matches.sh can launch headlessly
    /// (-batchmode -nographics) to play unattended matches through JevMatchRunner.
    /// Menu: Tools / Jev / Build Headless Player. Also callable with
    /// Unity -batchmode -quit -projectPath . -executeMethod Jev.EditorTools.JevBuild.BuildHeadless
    /// </summary>
    public static class JevBuild
    {
        public const string ScenePath = "Assets/Scenes/BattleField.unity";
        public const string OutputDirectory = "Builds/JevHeadless";

        [MenuItem("Tools/Jev/Build Headless Player")]
        public static void BuildHeadlessMenu()
        {
            BuildHeadless();
        }

        /// <summary>
        /// Plays unattended matches inside a batch-mode editor instead of a built player:
        /// Unity -batchmode -nographics -projectPath . -executeMethod Jev.EditorTools.JevBuild.RunMatches
        ///       --jev-matches 3 --jev-team-a Jev.MyTank --jev-team-b TJQ.MyTank
        /// Do not pass -quit: JevMatchRunner exits the process when the last match is done. This avoids
        /// the player build entirely (some student scripts reference UnityEditor and break player builds).
        /// </summary>
        public static void RunMatches()
        {
            if (JevMatchRunner.FindArg(System.Environment.GetCommandLineArgs(), "matches") == null)
            {
                Debug.LogError("[JevBuild] RunMatches needs --jev-matches N on the command line");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(2);
                }
                return;
            }
            try
            {
                File.Delete(JevMatchRunner.DoneMarkerPath); // stale marker from an earlier run
            }
            catch (System.Exception)
            {
                // nothing to clean
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[JevBuild] entering play mode for unattended matches");
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Runs after every domain reload. In a batch run, once JevMatchRunner has left its done marker and
        /// play mode has ended, this is the safe place to end the process (exiting from inside play mode
        /// crashes the editor during teardown). A marker older than this process is ignored.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ExitWhenMatchesDone()
        {
            if (!Application.isBatchMode ||
                JevMatchRunner.FindArg(System.Environment.GetCommandLineArgs(), "matches") == null)
            {
                return;
            }
            EditorApplication.update += CheckDoneMarker;
        }

        private static void CheckDoneMarker()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            string marker = JevMatchRunner.DoneMarkerPath;
            if (!File.Exists(marker))
            {
                return;
            }
            var processStart = System.Diagnostics.Process.GetCurrentProcess().StartTime;
            if (File.GetLastWriteTime(marker) < processStart)
            {
                return; // left over from a previous run; RunMatches deletes it before playing
            }
            EditorApplication.update -= CheckDoneMarker;
            Debug.Log("[JevBuild] matches done, exiting batch editor");
            File.Delete(marker);
            EditorApplication.Exit(0);
        }

        public static void BuildHeadless()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(projectRoot, OutputDirectory);
            Directory.CreateDirectory(outDir);

            var target = EditorUserBuildSettings.activeBuildTarget;
            string location;
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    location = Path.Combine(outDir, "TankBattleJev.app");
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    location = Path.Combine(outDir, "TankBattleJev.exe");
                    break;
                case BuildTarget.StandaloneLinux64:
                    location = Path.Combine(outDir, "TankBattleJev.x86_64");
                    break;
                default:
                    Debug.LogError($"[JevBuild] unsupported build target {target}; switch to a Standalone target first.");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(1);
                    }
                    return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = location,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[JevBuild] built {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB) in {summary.totalTime.TotalSeconds:F0}s");
            }
            else
            {
                Debug.LogError($"[JevBuild] build {summary.result}: {summary.totalErrors} error(s)");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
#endif
