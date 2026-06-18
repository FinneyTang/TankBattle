using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Main;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace NeuralAI
{
    public class TrainingMatchController : MonoBehaviour
    {
        [Header("Training Runtime")]
        public bool TrainingMode = true;
        public float TimeScale = 4f;
        public float FixedDeltaTime = 0.02f;
        public int TargetFrameRate = -1;
        public bool DisableLLM = true;
        public bool ReloadSceneOnMatchEnd = true;
        public float ReloadDelaySeconds = 0.25f;

        [Header("Team Setup")]
        public string NeuralTankScript = "NeuralAI.TankBattleAgent";
        public string OpponentTankScript = string.Empty;
        public bool RandomizeOpponent = true;
        public List<string> OpponentPool = new List<string>
        {
            "RuleBasedAI.MyTank",
            "UtilityBasedAI.MyTank"
        };
        [Header("Inference Model")]
        [Tooltip("Optional model assigned to runtime-spawned neural agents for local inference/evaluation. Leave empty when training through mlagents-learn.")]
        public UnityEngine.Object NeuralModel;
        public InferenceDevice NeuralInferenceDevice = InferenceDevice.Default;

        [Tooltip("When recording expert demos, randomize Team A so the recorded expert faces varied opponents.")]
        public bool RandomizeRecordingOpponent = false;
        public List<string> RecordingOpponentPool = new List<string>
        {
            "UtilityBasedAI.MyTank",
            "SM.MyTank"
        };
        public bool AvoidRecordingMirrorMatch = true;
        [Tooltip("Positive value overrides Match.GlobalSetting.MatchTime before each match starts.")]
        public int MatchTimeOverrideSeconds = 0;

        [Header("Imitation Recording")]
        public bool AttachDemoRecorderToOpponent = false;
        public bool RecordMlAgentsDemo = true;
        public bool RecordJsonlDemo = true;
        [Tooltip("Non-empty: saves both .demo and .jsonl files under this directory unless more specific overrides are provided.")]
        public string DemoOutputDirectory = string.Empty;
        public bool AutoNameDemoByExpertType = true;
        public string DemoNamePrefix = "tankbattle_expert";
        [Tooltip("Non-empty: override ExpertDemoRecorder.DemonstrationName when attaching.")]
        public string DemoDemonstrationNameOverride = string.Empty;
        [Tooltip("Non-empty: override ExpertDemoRecorder.DemonstrationDirectory when attaching.")]
        public string DemoDemonstrationDirectoryOverride = string.Empty;
        [Tooltip("Non-empty: override ExpertDemoRecorder.JsonlPath when attaching.")]
        public string DemoJsonlPathOverride = string.Empty;
        [Tooltip("Positive value records this many completed matches, then exits or stops Play Mode.")]
        public int RecordingMatchLimit = 0;
        public bool QuitApplicationWhenRecordingLimitReached = true;

        [Header("Curriculum")]
        public bool UseCurriculum = false;

        [Header("Evaluation")]
        public bool WriteEvaluationCsv = true;
        public string EvaluationCsvPath = "../TrainingData/evaluation.csv";

        private Match m_Match;
        private bool m_ResultLogged;
        private float m_ReloadAtUnscaledTime;
        private int m_LastTeamADeadCount;
        private int m_LastTeamBDeadCount;
        private int m_TeamADeaths;
        private int m_TeamBDeaths;
        private int m_NeuralFireCount;
        private int m_NeuralHitCount;
        private int m_NeuralDamageTakenHits;
        private int m_NeuralDodgeCount;
        private readonly Dictionary<Tank, int> m_LastHpByTank = new Dictionary<Tank, int>();
        private readonly HashSet<int> m_KnownFireMissileIds = new HashSet<int>();
        private readonly Dictionary<int, StarSnapshot> m_LastStarSnapshotsById = new Dictionary<int, StarSnapshot>();
        private readonly List<Tank> m_EvaluationTanks = new List<Tank>(4);
        private readonly Dictionary<int, Missile> m_EvaluationMissiles = new Dictionary<int, Missile>();
        private readonly HashSet<int> m_CurrentNeuralThreatMissiles = new HashSet<int>();
        private readonly HashSet<int> m_TrackedNeuralThreatMissiles = new HashSet<int>();
        private readonly List<int> m_ResolvedNeuralThreatMissiles = new List<int>();
        private readonly List<int> m_ResolvedStarIds = new List<int>();
        private int m_TeamAStarTakenCount;
        private int m_TeamASuperStarTakenCount;
        private int m_TeamBStarTakenCount;
        private int m_TeamBSuperStarTakenCount;
        private static int s_RecordedMatchCount;

        private const float ThreatCrossTrackDistance = 2.5f;
        private const float ThreatTimeWindow = 2f;
        private const string EvaluationCsvHeader =
            "timestamp,team_a_score,team_b_score,team_a_deaths,team_b_deaths," +
            "team_a_stars,team_a_super_stars,team_b_stars,team_b_super_stars," +
            "neural_fire_count,neural_hit_count,neural_hit_rate," +
            "neural_damage_taken_hits,neural_dodge_count,opponent_category";

        private struct StarSnapshot
        {
            public Vector3 Position;
            public bool IsSuperStar;

            public StarSnapshot(Vector3 position, bool isSuperStar)
            {
                Position = position;
                IsSuperStar = isSuperStar;
            }
        }

        private void Awake()
        {
            m_Match = FindObjectOfType<Match>();
            ApplyCommandLineOverrides();
            ApplyTrainingRuntime();
            EnsureCurriculum();
            ApplyTeamSettingsBeforeMatchStart();
        }

        private void EnsureCurriculum()
        {
            if (!UseCurriculum)
            {
                return;
            }

            var curriculum = GetComponent<TankBattleCurriculum>();
            if (curriculum == null)
            {
                curriculum = gameObject.AddComponent<TankBattleCurriculum>();
            }

            // 若 Academy 尚未就绪，强制访问单例触发初始化，使课程参数在 Match.Start 前即可读取
            if (!Academy.IsInitialized)
            {
                try { _ = Academy.Instance; } catch { /* Academy 可能在后续帧就绪 */ }
            }

            curriculum.Apply();
        }

        private IEnumerator Start()
        {
            yield return null;
            ApplyTrainingRuntime();
            InitializeEvaluationSnapshot();
            if (AttachDemoRecorderToOpponent)
            {
                AttachRecordersToRuleOpponents();
            }
        }

        private void Update()
        {
            if (!TrainingMode || m_Match == null)
            {
                return;
            }

            UpdateEvaluationSnapshot();
            if (!m_Match.IsMathEnd())
            {
                return;
            }

            if (!m_ResultLogged)
            {
                LogMatchResult();
                HandleRecordingMatchComplete();
                m_ResultLogged = true;
                m_ReloadAtUnscaledTime = Time.unscaledTime + ReloadDelaySeconds;
            }

            if (ReloadSceneOnMatchEnd && Time.unscaledTime >= m_ReloadAtUnscaledTime)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(activeScene.name);
            }
        }

        private void ApplyTrainingRuntime()
        {
            if (!TrainingMode)
            {
                return;
            }

            Application.runInBackground = true;
            Application.targetFrameRate = TargetFrameRate;
            Time.timeScale = Mathf.Max(0.01f, TimeScale);
            Time.fixedDeltaTime = FixedDeltaTime;

            if (DisableLLM && m_Match != null)
            {
                m_Match.LLMSettingData.EnableLLM = false;
            }
        }

        private void ApplyTeamSettingsBeforeMatchStart()
        {
            if (!TrainingMode || m_Match == null || m_Match.TeamSettings == null ||
                m_Match.TeamSettings.Count < 2)
            {
                return;
            }

            if (MatchTimeOverrideSeconds > 0)
            {
                m_Match.GlobalSetting.MatchTime = MatchTimeOverrideSeconds;
            }

            string expertScript = OpponentTankScript;
            if (string.IsNullOrWhiteSpace(expertScript) && RandomizeOpponent && OpponentPool.Count > 0)
            {
                expertScript = ChooseRandomScript(OpponentPool, string.Empty);
            }

            string teamAScript = NeuralTankScript;
            if (AttachDemoRecorderToOpponent && RandomizeRecordingOpponent && RecordingOpponentPool.Count > 0)
            {
                string excludedScript = AvoidRecordingMirrorMatch ? expertScript : string.Empty;
                string randomRecordingOpponent = ChooseRandomScript(RecordingOpponentPool, excludedScript);
                if (!string.IsNullOrWhiteSpace(randomRecordingOpponent))
                {
                    teamAScript = randomRecordingOpponent;
                }
            }

            m_Match.TeamSettings[0].Team = ETeam.A;
            m_Match.TeamSettings[0].TankScript = teamAScript;

            m_Match.TeamSettings[1].Team = ETeam.B;
            if (!string.IsNullOrWhiteSpace(expertScript))
            {
                m_Match.TeamSettings[1].TankScript = expertScript;
            }
        }

        private void ApplyCommandLineOverrides()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool changed = false;

            for (int i = 0; i < args.Length; ++i)
            {
                if (!TrySplitArgument(args[i], out string key, out string inlineValue))
                {
                    continue;
                }

                switch (key)
                {
                    case "--tankbattle-training-mode":
                        changed |= TryReadBool(args, ref i, inlineValue, value => TrainingMode = value);
                        break;
                    case "--tankbattle-time-scale":
                        changed |= TryReadFloat(args, ref i, inlineValue, value => TimeScale = value);
                        break;
                    case "--tankbattle-fixed-delta-time":
                        changed |= TryReadFloat(args, ref i, inlineValue, value => FixedDeltaTime = value);
                        break;
                    case "--tankbattle-target-frame-rate":
                        changed |= TryReadInt(args, ref i, inlineValue, value => TargetFrameRate = value);
                        break;
                    case "--tankbattle-disable-llm":
                        changed |= TryReadBool(args, ref i, inlineValue, value => DisableLLM = value);
                        break;
                    case "--tankbattle-reload-scene":
                    case "--tankbattle-reload-scene-on-match-end":
                        changed |= TryReadBool(args, ref i, inlineValue, value => ReloadSceneOnMatchEnd = value);
                        break;
                    case "--tankbattle-reload-delay":
                    case "--tankbattle-reload-delay-seconds":
                        changed |= TryReadFloat(args, ref i, inlineValue, value => ReloadDelaySeconds = value);
                        break;
                    case "--tankbattle-neural-script":
                        changed |= TryReadString(args, ref i, inlineValue, value => NeuralTankScript = value);
                        break;
                    case "--tankbattle-opponent":
                    case "--tankbattle-opponent-script":
                        changed |= TryReadString(args, ref i, inlineValue, value =>
                        {
                            OpponentTankScript = value;
                            RandomizeOpponent = false;
                        });
                        break;
                    case "--tankbattle-opponent-pool":
                        changed |= TryReadString(args, ref i, inlineValue, value =>
                        {
                            SetOpponentPool(value);
                            OpponentTankScript = string.Empty;
                            RandomizeOpponent = true;
                        });
                        break;
                    case "--tankbattle-randomize-opponent":
                        changed |= TryReadBool(args, ref i, inlineValue, value => RandomizeOpponent = value);
                        break;
                    case "--tankbattle-recording-opponent":
                    case "--tankbattle-expert-facing-opponent":
                        changed |= TryReadString(args, ref i, inlineValue, value =>
                        {
                            NeuralTankScript = value;
                            RandomizeRecordingOpponent = false;
                        });
                        break;
                    case "--tankbattle-recording-opponent-pool":
                    case "--tankbattle-expert-facing-opponent-pool":
                        changed |= TryReadString(args, ref i, inlineValue, value =>
                        {
                            SetRecordingOpponentPool(value);
                            RandomizeRecordingOpponent = true;
                        });
                        break;
                    case "--tankbattle-randomize-recording-opponent":
                    case "--tankbattle-randomize-expert-facing-opponent":
                        changed |= TryReadBool(args, ref i, inlineValue, value => RandomizeRecordingOpponent = value);
                        break;
                    case "--tankbattle-avoid-recording-mirror-match":
                        changed |= TryReadBool(args, ref i, inlineValue, value => AvoidRecordingMirrorMatch = value);
                        break;
                    case "--tankbattle-match-time":
                    case "--tankbattle-match-time-seconds":
                        changed |= TryReadInt(args, ref i, inlineValue, value => MatchTimeOverrideSeconds = value);
                        break;
                    case "--tankbattle-evaluation-csv":
                        changed |= TryReadString(args, ref i, inlineValue, value => EvaluationCsvPath = value);
                        break;
                    case "--tankbattle-attach-demo-recorder":
                        changed |= TryReadBool(args, ref i, inlineValue, value => AttachDemoRecorderToOpponent = value);
                        break;
                    case "--tankbattle-record-mlagents-demo":
                        changed |= TryReadBool(args, ref i, inlineValue, value => RecordMlAgentsDemo = value);
                        break;
                    case "--tankbattle-record-jsonl-demo":
                        changed |= TryReadBool(args, ref i, inlineValue, value => RecordJsonlDemo = value);
                        break;
                    case "--tankbattle-demo-output-directory":
                    case "--tankbattle-demo-output-dir":
                        changed |= TryReadString(args, ref i, inlineValue, value => DemoOutputDirectory = value);
                        break;
                    case "--tankbattle-auto-name-demo-by-expert":
                    case "--tankbattle-auto-name-demo":
                        changed |= TryReadBool(args, ref i, inlineValue, value => AutoNameDemoByExpertType = value);
                        break;
                    case "--tankbattle-demo-name-prefix":
                        changed |= TryReadString(args, ref i, inlineValue, value => DemoNamePrefix = value);
                        break;
                    case "--tankbattle-demo-name":
                        changed |= TryReadString(args, ref i, inlineValue, value => DemoDemonstrationNameOverride = value);
                        break;
                    case "--tankbattle-demo-directory":
                        changed |= TryReadString(args, ref i, inlineValue, value => DemoDemonstrationDirectoryOverride = value);
                        break;
                    case "--tankbattle-demo-jsonl":
                        changed |= TryReadString(args, ref i, inlineValue, value => DemoJsonlPathOverride = value);
                        break;
                    case "--tankbattle-recording-match-limit":
                    case "--tankbattle-max-recorded-matches":
                    case "--tankbattle-max-episodes":
                        changed |= TryReadInt(args, ref i, inlineValue, value => RecordingMatchLimit = value);
                        break;
                    case "--tankbattle-quit-on-recording-complete":
                        changed |= TryReadBool(args, ref i, inlineValue, value => QuitApplicationWhenRecordingLimitReached = value);
                        break;
                    case "--tankbattle-use-curriculum":
                        changed |= TryReadBool(args, ref i, inlineValue, value => UseCurriculum = value);
                        break;
                }
            }

            if (changed)
            {
                Debug.Log("TrainingMatchController applied command line overrides.");
            }
        }

        private void SetOpponentPool(string value)
        {
            OpponentPool.Clear();
            AddScriptsToPool(OpponentPool, value);
        }

        private void SetRecordingOpponentPool(string value)
        {
            RecordingOpponentPool.Clear();
            AddScriptsToPool(RecordingOpponentPool, value);
        }

        private void AddScriptsToPool(List<string> pool, string value)
        {
            string[] entries = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < entries.Length; ++i)
            {
                string scriptName = entries[i].Trim();
                if (!string.IsNullOrEmpty(scriptName))
                {
                    pool.Add(scriptName);
                }
            }
        }

        private string ChooseRandomScript(List<string> pool, string excludedScript)
        {
            if (pool == null || pool.Count == 0)
            {
                return string.Empty;
            }

            List<string> candidates = new List<string>();
            for (int i = 0; i < pool.Count; ++i)
            {
                string scriptName = pool[i];
                if (string.IsNullOrWhiteSpace(scriptName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(excludedScript) &&
                    string.Equals(scriptName.Trim(), excludedScript.Trim(), StringComparison.Ordinal))
                {
                    continue;
                }

                candidates.Add(scriptName.Trim());
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < pool.Count; ++i)
                {
                    string scriptName = pool[i];
                    if (!string.IsNullOrWhiteSpace(scriptName))
                    {
                        candidates.Add(scriptName.Trim());
                    }
                }
            }

            return candidates.Count == 0 ? string.Empty : candidates[Random.Range(0, candidates.Count)];
        }

        private bool TrySplitArgument(string arg, out string key, out string inlineValue)
        {
            key = null;
            inlineValue = null;

            if (string.IsNullOrEmpty(arg) || !arg.StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }

            int separatorIndex = arg.IndexOf('=');
            if (separatorIndex < 0)
            {
                key = arg;
                return true;
            }

            key = arg.Substring(0, separatorIndex);
            inlineValue = arg.Substring(separatorIndex + 1);
            return true;
        }

        private bool TryReadString(string[] args, ref int index, string inlineValue, Action<string> apply)
        {
            if (!TryGetArgumentValue(args, ref index, inlineValue, false, out string value))
            {
                return false;
            }

            value = value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            apply(value);
            return true;
        }

        private bool TryReadBool(string[] args, ref int index, string inlineValue, Action<bool> apply)
        {
            if (!TryGetArgumentValue(args, ref index, inlineValue, true, out string value))
            {
                return false;
            }

            if (!TryParseBool(value, out bool parsedValue))
            {
                Debug.LogWarning($"TrainingMatchController ignored invalid bool argument value '{value}'.");
                return false;
            }

            apply(parsedValue);
            return true;
        }

        private bool TryReadFloat(string[] args, ref int index, string inlineValue, Action<float> apply)
        {
            if (!TryGetArgumentValue(args, ref index, inlineValue, false, out string value))
            {
                return false;
            }

            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue))
            {
                Debug.LogWarning($"TrainingMatchController ignored invalid float argument value '{value}'.");
                return false;
            }

            apply(parsedValue);
            return true;
        }

        private bool TryReadInt(string[] args, ref int index, string inlineValue, Action<int> apply)
        {
            if (!TryGetArgumentValue(args, ref index, inlineValue, false, out string value))
            {
                return false;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
            {
                Debug.LogWarning($"TrainingMatchController ignored invalid int argument value '{value}'.");
                return false;
            }

            apply(parsedValue);
            return true;
        }

        private bool TryGetArgumentValue(string[] args, ref int index, string inlineValue, bool allowBareBoolean, out string value)
        {
            value = inlineValue;
            if (value != null)
            {
                return true;
            }

            int nextIndex = index + 1;
            if (nextIndex < args.Length && !IsSwitchName(args[nextIndex]))
            {
                value = args[nextIndex];
                index = nextIndex;
                return true;
            }

            if (allowBareBoolean)
            {
                value = "true";
                return true;
            }

            return false;
        }

        private bool IsSwitchName(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("-", StringComparison.Ordinal))
            {
                return false;
            }

            return !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private bool TryParseBool(string value, out bool result)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "y":
                case "on":
                    result = true;
                    return true;
                case "0":
                case "false":
                case "no":
                case "n":
                case "off":
                    result = false;
                    return true;
                default:
                    result = false;
                    return false;
            }
        }

        private void AttachRecordersToRuleOpponents()
        {
            if (m_Match == null)
            {
                return;
            }

            for (int i = 0; i < m_Match.TeamSettings.Count; ++i)
            {
                Match.TeamSetting setting = m_Match.TeamSettings[i];
                // A 队是学习智能体槽位（见 ApplyTeamSettingsBeforeMatchStart），不在此录制；
                // B 队录制专家轨迹。两队脚本相同时也只录 B，避免“同脚本双专家”时全部被跳过。
                if (setting.Team == ETeam.A)
                {
                    continue;
                }

                List<Tank> tanks = m_Match.GetTanks(setting.Team);
                if (tanks == null)
                {
                    continue;
                }

                for (int j = 0; j < tanks.Count; ++j)
                {
                    Tank tank = tanks[j];
                    if (tank == null)
                    {
                        continue;
                    }

                    ExpertDemoRecorder recorder = tank.GetComponent<ExpertDemoRecorder>();
                    if (recorder == null)
                    {
                        recorder = tank.gameObject.AddComponent<ExpertDemoRecorder>();
                    }
                    recorder.TargetTank = tank;
                    recorder.RecordMlAgentsDemo = RecordMlAgentsDemo;
                    recorder.RecordJsonl = RecordJsonlDemo;
                    ApplyDemoOutputSettings(recorder, GetScriptForTeam(setting.Team));
                }
            }
        }

        private void ApplyDemoOutputSettings(ExpertDemoRecorder recorder, string expertScript)
        {
            string expertFileName = SanitizeFileName(string.IsNullOrWhiteSpace(expertScript) ? "unknown_expert" : expertScript);
            string demonstrationName = recorder.DemonstrationName;
            if (!string.IsNullOrWhiteSpace(DemoDemonstrationNameOverride))
            {
                demonstrationName = DemoDemonstrationNameOverride.Trim();
            }
            else if (AutoNameDemoByExpertType)
            {
                string prefix = string.IsNullOrWhiteSpace(DemoNamePrefix) ? string.Empty : DemoNamePrefix.Trim();
                demonstrationName = string.IsNullOrEmpty(prefix) ? expertFileName : $"{prefix}_{expertFileName}";
            }

            string outputDirectory = ResolveDemoDirectory(recorder);
            recorder.DemonstrationName = demonstrationName;
            recorder.DemonstrationDirectory = outputDirectory;

            if (!string.IsNullOrWhiteSpace(DemoJsonlPathOverride))
            {
                recorder.JsonlPath = DemoJsonlPathOverride.Trim();
            }
            else if (AutoNameDemoByExpertType || !string.IsNullOrWhiteSpace(DemoOutputDirectory))
            {
                recorder.JsonlPath = Path.Combine(outputDirectory, $"{demonstrationName}.jsonl");
            }
        }

        private string ResolveDemoDirectory(ExpertDemoRecorder recorder)
        {
            if (!string.IsNullOrWhiteSpace(DemoDemonstrationDirectoryOverride))
            {
                return DemoDemonstrationDirectoryOverride.Trim();
            }

            if (!string.IsNullOrWhiteSpace(DemoOutputDirectory))
            {
                return DemoOutputDirectory.Trim();
            }

            return recorder.DemonstrationDirectory;
        }

        private string SanitizeFileName(string value)
        {
            string trimmedValue = value.Trim();
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; ++i)
            {
                trimmedValue = trimmedValue.Replace(invalidCharacters[i], '_');
            }

            return trimmedValue;
        }

        private void HandleRecordingMatchComplete()
        {
            if (!AttachDemoRecorderToOpponent || RecordingMatchLimit <= 0)
            {
                return;
            }

            s_RecordedMatchCount++;
            Debug.Log($"Expert demo recording completed match {s_RecordedMatchCount}/{RecordingMatchLimit}.");
            if (s_RecordedMatchCount < RecordingMatchLimit)
            {
                return;
            }

            ReloadSceneOnMatchEnd = false;
            Debug.Log("Expert demo recording match limit reached.");
            if (!QuitApplicationWhenRecordingLimitReached)
            {
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void LogMatchResult()
        {
            if (!WriteEvaluationCsv || m_Match == null)
            {
                return;
            }

            string path = ResolvePath(EvaluationCsvPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bool writeHeader = ShouldWriteEvaluationHeader(path);
            using (var writer = new StreamWriter(path, true))
            {
                if (writeHeader)
                {
                    writer.WriteLine(EvaluationCsvHeader);
                }

                int teamAScore = GetTeamScore(ETeam.A);
                int teamBScore = GetTeamScore(ETeam.B);
                float neuralHitRate = m_NeuralFireCount > 0
                    ? (float)m_NeuralHitCount / m_NeuralFireCount
                    : 0f;
                string opponentCategory = GetOpponentCategory(GetScriptForTeam(ETeam.B));
                int teamAStars = m_TeamAStarTakenCount;
                int teamASuperStars = m_TeamASuperStarTakenCount;
                int teamBStars = m_TeamBStarTakenCount;
                int teamBSuperStars = m_TeamBSuperStarTakenCount;
                writer.WriteLine(string.Join(",",
                    DateTime.UtcNow.ToString("o"),
                    teamAScore,
                    teamBScore,
                    m_TeamADeaths,
                    m_TeamBDeaths,
                    teamAStars,
                    teamASuperStars,
                    teamBStars,
                    teamBSuperStars,
                    m_NeuralFireCount,
                    m_NeuralHitCount,
                    neuralHitRate.ToString("0.###", CultureInfo.InvariantCulture),
                    m_NeuralDamageTakenHits,
                    m_NeuralDodgeCount,
                    EscapeCsv(opponentCategory)));
            }
        }

        private void InitializeEvaluationSnapshot()
        {
            if (m_Match == null)
            {
                return;
            }

            m_LastTeamADeadCount = GetDeadCount(ETeam.A);
            m_LastTeamBDeadCount = GetDeadCount(ETeam.B);
            m_NeuralFireCount = 0;
            m_NeuralHitCount = 0;
            m_NeuralDamageTakenHits = 0;
            m_NeuralDodgeCount = 0;
            m_TeamAStarTakenCount = 0;
            m_TeamASuperStarTakenCount = 0;
            m_TeamBStarTakenCount = 0;
            m_TeamBSuperStarTakenCount = 0;
            m_LastHpByTank.Clear();
            m_KnownFireMissileIds.Clear();
            m_LastStarSnapshotsById.Clear();
            m_CurrentNeuralThreatMissiles.Clear();
            m_TrackedNeuralThreatMissiles.Clear();
            m_ResolvedNeuralThreatMissiles.Clear();
            m_ResolvedStarIds.Clear();
            SnapshotTeamForEvaluation(ETeam.A);
            SnapshotOpponentsForEvaluation(ETeam.A);
            SnapshotFireMissilesForEvaluation(ETeam.A);
            SnapshotStarsForEvaluation();
        }

        private void UpdateEvaluationSnapshot()
        {
            UpdateStarPickupEvaluation();
            int neuralDamageTakenHitsThisFrame = UpdateNeuralCombatEvaluation();
            UpdateNeuralDodgeEvaluation(neuralDamageTakenHitsThisFrame);

            int teamADeadCount = GetDeadCount(ETeam.A);
            int teamBDeadCount = GetDeadCount(ETeam.B);
            if (teamADeadCount > m_LastTeamADeadCount)
            {
                m_TeamADeaths += teamADeadCount - m_LastTeamADeadCount;
            }

            if (teamBDeadCount > m_LastTeamBDeadCount)
            {
                m_TeamBDeaths += teamBDeadCount - m_LastTeamBDeadCount;
            }

            m_LastTeamADeadCount = teamADeadCount;
            m_LastTeamBDeadCount = teamBDeadCount;
        }

        private int UpdateNeuralCombatEvaluation()
        {
            m_NeuralFireCount += AccumulateFireCount(ETeam.A);
            m_NeuralHitCount += AccumulateOpponentDamageTakenHits(ETeam.A);

            int damageTakenHits = AccumulateTeamDamageTakenHits(ETeam.A);
            m_NeuralDamageTakenHits += damageTakenHits;
            return damageTakenHits;
        }

        private int AccumulateFireCount(ETeam team)
        {
            int fireCount = 0;
            ETeam excludedTeam = GetMissileSnapshotExcludedTeam(team);
            m_Match.GetOppositeMissilesEx(excludedTeam, m_EvaluationMissiles);

            foreach (var pair in m_EvaluationMissiles)
            {
                Missile missile = pair.Value;
                if (missile == null || missile.Team != team)
                {
                    continue;
                }

                if (m_KnownFireMissileIds.Add(missile.ID))
                {
                    fireCount++;
                }
            }

            return fireCount;
        }

        private int AccumulateOpponentDamageTakenHits(ETeam team)
        {
            m_EvaluationTanks.Clear();
            m_Match.GetOppositeTanks(team, m_EvaluationTanks);
            return AccumulateDamageTakenHits(m_EvaluationTanks);
        }

        private int AccumulateTeamDamageTakenHits(ETeam team)
        {
            List<Tank> tanks = m_Match.GetTanks(team);
            if (tanks == null)
            {
                return 0;
            }

            return AccumulateDamageTakenHits(tanks);
        }

        private int AccumulateDamageTakenHits(List<Tank> tanks)
        {
            int hits = 0;
            int damagePerHit = Mathf.Max(1, m_Match.GlobalSetting.DamagePerHit);
            for (int i = 0; i < tanks.Count; ++i)
            {
                Tank tank = tanks[i];
                if (tank == null)
                {
                    continue;
                }

                int lastHp;
                if (m_LastHpByTank.TryGetValue(tank, out lastHp))
                {
                    int damageTaken = Mathf.Max(0, lastHp - tank.HP);
                    hits += Mathf.RoundToInt((float)damageTaken / damagePerHit);
                }

                m_LastHpByTank[tank] = tank.HP;
            }

            return hits;
        }

        private void UpdateNeuralDodgeEvaluation(int neuralDamageTakenHitsThisFrame)
        {
            m_Match.GetOppositeMissilesEx(ETeam.A, m_EvaluationMissiles);
            m_CurrentNeuralThreatMissiles.Clear();
            foreach (var pair in m_EvaluationMissiles)
            {
                Missile missile = pair.Value;
                if (missile != null && IsMissileThreateningTeam(missile, ETeam.A))
                {
                    m_CurrentNeuralThreatMissiles.Add(pair.Key);
                }
            }

            m_ResolvedNeuralThreatMissiles.Clear();
            foreach (int missileId in m_TrackedNeuralThreatMissiles)
            {
                if (!m_CurrentNeuralThreatMissiles.Contains(missileId))
                {
                    if (neuralDamageTakenHitsThisFrame <= 0)
                    {
                        m_NeuralDodgeCount++;
                    }

                    m_ResolvedNeuralThreatMissiles.Add(missileId);
                }
            }

            for (int i = 0; i < m_ResolvedNeuralThreatMissiles.Count; ++i)
            {
                m_TrackedNeuralThreatMissiles.Remove(m_ResolvedNeuralThreatMissiles[i]);
            }

            foreach (int missileId in m_CurrentNeuralThreatMissiles)
            {
                m_TrackedNeuralThreatMissiles.Add(missileId);
            }
        }

        private bool IsMissileThreateningTeam(Missile missile, ETeam team)
        {
            List<Tank> tanks = m_Match.GetTanks(team);
            if (tanks == null)
            {
                return false;
            }

            for (int i = 0; i < tanks.Count; ++i)
            {
                Tank tank = tanks[i];
                if (tank != null && !tank.IsDead && IsMissileThreateningTank(missile, tank))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsMissileThreateningTank(Missile missile, Tank tank)
        {
            Vector3 velocity = missile.Velocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 toTank = tank.Position - missile.Position;
            float distance = toTank.magnitude;
            if (distance > 30f)
            {
                return false;
            }

            Vector3 direction = velocity.normalized;
            if (Vector3.Dot(direction, toTank.normalized) <= 0f)
            {
                return false;
            }

            if (Physics.SphereCast(missile.Position, 0.5f, direction,
                out RaycastHit hit, distance, PhysicsUtils.LayerMaskTank))
            {
                FireCollider fc = hit.collider.GetComponent<FireCollider>();
                if (fc != null && fc.Owner == tank)
                {
                    return true;
                }
            }

            return false;
        }

        private ETeam GetMissileSnapshotExcludedTeam(ETeam team)
        {
            for (int i = 0; i < (int)ETeam.NB; ++i)
            {
                ETeam candidate = (ETeam)i;
                if (candidate != team)
                {
                    return candidate;
                }
            }

            return team;
        }

        private void SnapshotFireMissilesForEvaluation(ETeam team)
        {
            ETeam excludedTeam = GetMissileSnapshotExcludedTeam(team);
            m_Match.GetOppositeMissilesEx(excludedTeam, m_EvaluationMissiles);

            foreach (var pair in m_EvaluationMissiles)
            {
                Missile missile = pair.Value;
                if (missile != null && missile.Team == team)
                {
                    m_KnownFireMissileIds.Add(missile.ID);
                }
            }
        }

        private void SnapshotStarsForEvaluation()
        {
            Dictionary<int, Star> stars = m_Match.GetStars();
            if (stars == null)
            {
                return;
            }

            foreach (var pair in stars)
            {
                Star star = pair.Value;
                if (star != null)
                {
                    m_LastStarSnapshotsById[pair.Key] = new StarSnapshot(star.Position, star.IsSuperStar);
                }
            }
        }

        private void UpdateStarPickupEvaluation()
        {
            Dictionary<int, Star> stars = m_Match.GetStars();
            if (stars == null)
            {
                return;
            }

            m_ResolvedStarIds.Clear();
            foreach (var pair in m_LastStarSnapshotsById)
            {
                if (!stars.ContainsKey(pair.Key))
                {
                    CountStarPickup(pair.Value);
                    m_ResolvedStarIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < m_ResolvedStarIds.Count; ++i)
            {
                m_LastStarSnapshotsById.Remove(m_ResolvedStarIds[i]);
            }

            foreach (var pair in stars)
            {
                Star star = pair.Value;
                if (star != null)
                {
                    m_LastStarSnapshotsById[pair.Key] = new StarSnapshot(star.Position, star.IsSuperStar);
                }
            }
        }

        private void CountStarPickup(StarSnapshot snapshot)
        {
            ETeam team = FindClosestTankTeam(snapshot.Position);
            switch (team)
            {
                case ETeam.A:
                    if (snapshot.IsSuperStar)
                    {
                        m_TeamASuperStarTakenCount++;
                    }
                    else
                    {
                        m_TeamAStarTakenCount++;
                    }
                    break;
                case ETeam.B:
                    if (snapshot.IsSuperStar)
                    {
                        m_TeamBSuperStarTakenCount++;
                    }
                    else
                    {
                        m_TeamBStarTakenCount++;
                    }
                    break;
            }
        }

        private ETeam FindClosestTankTeam(Vector3 position)
        {
            ETeam closestTeam = ETeam.NB;
            float closestSqrDistance = float.MaxValue;

            for (int teamIndex = 0; teamIndex < (int)ETeam.NB; ++teamIndex)
            {
                List<Tank> tanks = m_Match.GetTanks((ETeam)teamIndex);
                if (tanks == null)
                {
                    continue;
                }

                for (int i = 0; i < tanks.Count; ++i)
                {
                    Tank tank = tanks[i];
                    if (tank == null)
                    {
                        continue;
                    }

                    float sqrDistance = (tank.Position - position).sqrMagnitude;
                    if (sqrDistance < closestSqrDistance)
                    {
                        closestSqrDistance = sqrDistance;
                        closestTeam = tank.Team;
                    }
                }
            }

            return closestTeam;
        }

        private void SnapshotTeamForEvaluation(ETeam team)
        {
            List<Tank> tanks = m_Match.GetTanks(team);
            if (tanks == null)
            {
                return;
            }

            for (int i = 0; i < tanks.Count; ++i)
            {
                Tank tank = tanks[i];
                if (tank == null)
                {
                    continue;
                }

                m_LastHpByTank[tank] = tank.HP;
            }
        }

        private void SnapshotOpponentsForEvaluation(ETeam team)
        {
            m_EvaluationTanks.Clear();
            m_Match.GetOppositeTanks(team, m_EvaluationTanks);
            for (int i = 0; i < m_EvaluationTanks.Count; ++i)
            {
                Tank tank = m_EvaluationTanks[i];
                if (tank != null)
                {
                    m_LastHpByTank[tank] = tank.HP;
                }
            }
        }

        private int GetTeamScore(ETeam team)
        {
            int score = 0;
            List<Tank> tanks = m_Match.GetTanks(team);
            if (tanks == null)
            {
                return score;
            }

            for (int i = 0; i < tanks.Count; ++i)
            {
                if (tanks[i] != null)
                {
                    score += tanks[i].Score;
                }
            }

            return score;
        }

        private int GetDeadCount(ETeam team)
        {
            int deadCount = 0;
            List<Tank> tanks = m_Match.GetTanks(team);
            if (tanks == null)
            {
                return deadCount;
            }

            for (int i = 0; i < tanks.Count; ++i)
            {
                if (tanks[i] != null && tanks[i].IsDead && tanks[i].HP == 0)
                {
                    deadCount++;
                }
            }

            return deadCount;
        }

        private string GetScriptForTeam(ETeam team)
        {
            for (int i = 0; i < m_Match.TeamSettings.Count; ++i)
            {
                Match.TeamSetting setting = m_Match.TeamSettings[i];
                if (setting.Team == team)
                {
                    return setting.TankScript;
                }
            }

            return string.Empty;
        }

        private static string GetOpponentCategory(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return "unknown";
            }

            int dotIndex = scriptName.IndexOf('.');
            if (dotIndex > 0)
            {
                return scriptName.Substring(0, dotIndex);
            }

            return scriptName;
        }

        private bool ShouldWriteEvaluationHeader(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                return true;
            }

            string firstLine;
            using (var reader = new StreamReader(path))
            {
                firstLine = reader.ReadLine();
            }

            if (string.Equals(firstLine, EvaluationCsvHeader, StringComparison.Ordinal))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string legacyPath = Path.Combine(
                string.IsNullOrEmpty(directory) ? "." : directory,
                $"{fileName}_legacy_{timestamp}{extension}");
            File.Move(path, legacyPath);
            return true;
        }

        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, path));
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\""))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
