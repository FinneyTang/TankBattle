using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Main;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jev
{
    /// <summary>
    /// Unattended match runner for headless/batch runs. It does nothing unless the process was started
    /// with "--jev-matches N"; then it overrides the Match team scripts before each match starts, plays N
    /// matches back to back by reloading the scene, appends one result line per match to a jsonl file and
    /// quits. No scene changes are needed: it bootstraps itself from a RuntimeInitializeOnLoadMethod.
    ///
    /// Arguments (all optional except --jev-matches):
    ///   --jev-matches N           number of matches to play
    ///   --jev-team-a SCRIPT       tank script for team A, e.g. Jev.MyTank (likewise --jev-team-b/c/d)
    ///   --jev-match-time SECONDS  override Match.GlobalSetting.MatchTime
    ///   --jev-time-scale F        Time.timeScale (keep 1 for Jev: requests are wall-clock, game time is not)
    ///   --jev-results PATH        results file, default Logs/Jev/matches.jsonl
    ///   --jev-no-quit             keep the process alive after the last match
    ///   --jev-config JSON         JevConfig overrides for this run (handled by JevConfig.Load)
    /// </summary>
    public class JevMatchRunner : MonoBehaviour
    {
        private const string ArgPrefix = "--jev-";

        /// <summary>Written when the last match is done; Temp/ is per project and git-ignored.</summary>
        public static string DoneMarkerPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "jev_matches_done"));

        private int m_MatchesToRun;
        private int m_Completed;
        private readonly Dictionary<ETeam, string> m_TeamScripts = new Dictionary<ETeam, string>();
        private int m_MatchTimeOverride;
        private float m_TimeScale = 1f;
        private string m_ResultsPath;
        private bool m_QuitWhenDone = true;
        private bool m_MatchEndHandled;
        private float m_MatchStartRealtime;
        private string m_ConfigOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            if (FindArg(args, "matches") == null)
            {
                return;
            }
            var go = new GameObject("JevMatchRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<JevMatchRunner>();
            runner.Parse(args);
            SceneManager.sceneLoaded += runner.OnSceneLoaded;
            Debug.Log($"[JevRunner] active: {runner.m_MatchesToRun} match(es), teams " +
                      string.Join(", ", runner.m_TeamScripts) + $", timeScale {runner.m_TimeScale}");
        }

        public static string FindArg(string[] args, string name)
        {
            string key = ArgPrefix + name;
            for (int i = 0; i < args.Length; ++i)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1 < args.Length ? args[i + 1] : string.Empty;
                }
                if (args[i].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i].Substring(key.Length + 1);
                }
            }
            return null;
        }

        private void Parse(string[] args)
        {
            int.TryParse(FindArg(args, "matches"), NumberStyles.Integer, CultureInfo.InvariantCulture, out m_MatchesToRun);
            m_MatchesToRun = Mathf.Max(1, m_MatchesToRun);
            foreach (ETeam team in new[] { ETeam.A, ETeam.B, ETeam.C, ETeam.D })
            {
                var script = FindArg(args, "team-" + team.ToString().ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(script))
                {
                    m_TeamScripts[team] = script.Trim();
                }
            }
            int.TryParse(FindArg(args, "match-time") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out m_MatchTimeOverride);
            if (float.TryParse(FindArg(args, "time-scale") ?? "1", NumberStyles.Float, CultureInfo.InvariantCulture, out var ts))
            {
                m_TimeScale = Mathf.Clamp(ts, 0.1f, 20f);
            }
            m_ResultsPath = FindArg(args, "results");
            if (string.IsNullOrWhiteSpace(m_ResultsPath))
            {
                m_ResultsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Jev", "matches.jsonl"));
            }
            m_QuitWhenDone = FindArg(args, "no-quit") == null;
            m_ConfigOverride = FindArg(args, "config");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // sceneLoaded fires after Awake/OnEnable and before Start, so Match.Start sees our overrides.
            var match = Match.instance;
            if (match == null)
            {
                Debug.LogWarning("[JevRunner] no Match in scene " + scene.name);
                return;
            }
            foreach (var pair in m_TeamScripts)
            {
                var setting = match.TeamSettings.Find(s => s.Team == pair.Key);
                if (setting == null)
                {
                    Debug.LogWarning($"[JevRunner] scene has no team {pair.Key}; skipping {pair.Value}");
                    continue;
                }
                setting.TankScript = pair.Value;
            }
            if (m_MatchTimeOverride > 0)
            {
                match.GlobalSetting.MatchTime = m_MatchTimeOverride;
            }
            Time.timeScale = m_TimeScale;
            m_MatchEndHandled = false;
            m_MatchStartRealtime = Time.realtimeSinceStartup;
            Debug.Log($"[JevRunner] match {m_Completed + 1}/{m_MatchesToRun} starting in {scene.name}");
        }

        private void Update()
        {
            var match = Match.instance;
            if (match == null || m_MatchEndHandled || !match.IsMathEnd())
            {
                return;
            }
            m_MatchEndHandled = true;
            StartCoroutine(FinishMatch(match));
        }

        private IEnumerator FinishMatch(Match match)
        {
            // Give the tanks a moment to flush their own summaries.
            yield return new WaitForSecondsRealtime(1f);

            var teams = new List<object>();
            for (int i = 0; i < (int)ETeam.NB; ++i)
            {
                var team = (ETeam)i;
                var tanks = match.GetTanks(team);
                if (tanks == null || tanks.Count == 0)
                {
                    continue;
                }
                int score = 0;
                var names = new List<string>();
                foreach (var t in tanks)
                {
                    score += t.Score;
                    names.Add(t.GetName());
                }
                m_TeamScripts.TryGetValue(team, out var script);
                teams.Add(new
                {
                    team = team.ToString(),
                    script = script ?? match.TeamSettings.Find(s => s.Team == team)?.TankScript,
                    names,
                    score,
                });
            }
            var result = new
            {
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                match_index = m_Completed + 1,
                scene = SceneManager.GetActiveScene().name,
                winner = match.WinnerTeam.ToString(),
                match_time = match.GlobalSetting.MatchTime,
                time_scale = m_TimeScale,
                wall_seconds = Time.realtimeSinceStartup - m_MatchStartRealtime,
                config_override = m_ConfigOverride,
                teams,
            };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(m_ResultsPath));
                File.AppendAllText(m_ResultsPath, JsonConvert.SerializeObject(result) + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JevRunner] could not write results: " + e.Message);
            }
            Debug.Log($"[JevRunner] match {m_Completed + 1}/{m_MatchesToRun} finished, winner {match.WinnerTeam}");

            m_Completed++;
            if (m_Completed >= m_MatchesToRun)
            {
                Debug.Log($"[JevRunner] all {m_MatchesToRun} match(es) done, results in {m_ResultsPath}");
                if (m_QuitWhenDone)
                {
#if UNITY_EDITOR
                    // Calling EditorApplication.Exit from inside play mode crashes the batch editor during
                    // teardown. Leave a marker instead; the editor-side JevBuild exits the process once the
                    // domain has reloaded back into edit mode.
                    try
                    {
                        File.WriteAllText(DoneMarkerPath, DateTime.Now.ToString("o"));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[JevRunner] could not write done marker: " + e.Message);
                    }
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
                yield break;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
