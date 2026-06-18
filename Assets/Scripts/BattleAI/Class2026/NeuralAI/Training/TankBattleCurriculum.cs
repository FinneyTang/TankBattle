using System.Collections.Generic;
using Main;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuralAI
{
    /// <summary>
    /// 课程学习参数管理器。
    /// 根据 ML-Agents 下发的 lesson_stage 自动调整对局难度，
    /// 并与 TrainingMatchController 协作完成场景重载。
    /// </summary>
    public class TankBattleCurriculum : MonoBehaviour
    {
        public static int CurrentStage { get; private set; } = 3;

        [System.Serializable]
        public class StageConfig
        {
            [Tooltip("固定对手脚本（非空时优先使用）")]
            public string OpponentScript = string.Empty;
            [Tooltip("随机对手池（OpponentScript 为空且 RandomizeOpponent=true 时使用）")]
            public List<string> OpponentPool = new List<string>();
            public bool RandomizeOpponent;
            public int MatchTimeOverrideSeconds;
        }

        [Header("Curriculum Stages")]
        public List<StageConfig> StageConfigs = new List<StageConfig>
        {
            new StageConfig { OpponentPool = new List<string> { "RuleBasedAI.MyTank", "ScriptBasedAI.MyTank"}, RandomizeOpponent = true },
            new StageConfig { OpponentPool = new List<string> { "RuleBasedAI.MyTank", "FSM.MyTank", "ScriptBasedAI.MyTank", "UtilityBasedAI.MyTank" }, RandomizeOpponent = true },
            new StageConfig { OpponentPool = new List<string> { "FSM.MyTank", "SensorAI.MyTank", "UtilityBasedAI.MyTank", "BT.MyTank", "RuleBasedAI.MyTank" }, RandomizeOpponent = true },
            new StageConfig { OpponentPool = new List<string> { "FSM.MyTank", "UtilityBasedAI.MyTank", "BT.MyTank", "GOAP.MyTank" }, RandomizeOpponent = true },
            new StageConfig { OpponentPool = new List<string> { "FSM.MyTank", "SensorAI.MyTank", "UtilityBasedAI.MyTank", "BT.MyTank", "GOAP.MyTank" }, RandomizeOpponent = true },
        };

        private TrainingMatchController m_Controller;
        private Match m_Match;
        private float m_LastLessonStage = -1f;
        private bool m_Applied;

        private void Awake()
        {
            LoadConfig();
            EnsureRefs();
            if (Academy.IsInitialized)
            {
                m_LastLessonStage = Academy.Instance.EnvironmentParameters.GetWithDefault("lesson_stage", 3.0f);
            }
        }

        private void LoadConfig()
        {
            var config = TankBattleConfigLoader.Config;
            if (config == null || config.CurriculumStages == null || config.CurriculumStages.Count == 0)
            {
                return;
            }

            StageConfigs.Clear();
            foreach (var data in config.CurriculumStages)
            {
                StageConfigs.Add(new StageConfig
                {
                    OpponentScript = data.OpponentScript ?? string.Empty,
                    OpponentPool = data.OpponentPool != null ? new List<string>(data.OpponentPool) : new List<string>(),
                    RandomizeOpponent = data.RandomizeOpponent,
                    MatchTimeOverrideSeconds = data.MatchTimeOverrideSeconds
                });
            }
        }

        private void EnsureRefs()
        {
            if (m_Controller == null)
                m_Controller = GetComponent<TrainingMatchController>();
            if (m_Match == null)
                m_Match = FindObjectOfType<Match>();
        }

        private void Update()
        {
            EnsureRefs();
            if (m_Controller == null || !m_Controller.TrainingMode || m_Match == null)
            {
                return;
            }

            if (!Academy.IsInitialized)
            {
                return;
            }

            float currentStage = Academy.Instance.EnvironmentParameters.GetWithDefault("lesson_stage", m_LastLessonStage);

            // 首次初始化或 Academy 就绪后补 Apply
            if (!m_Applied)
            {
                Apply();
                return;
            }

            if (Mathf.Abs(currentStage - m_LastLessonStage) > 0.01f)
            {
                Debug.Log($"[Curriculum] Lesson stage changed: {m_LastLessonStage} -> {currentStage}. Reloading scene.");
                m_LastLessonStage = currentStage;
                CurrentStage = Mathf.RoundToInt(currentStage);
                if (m_Controller.ReloadSceneOnMatchEnd)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }
            }
        }

        public void Apply()
        {
            EnsureRefs();
            if (m_Match == null || m_Controller == null || !m_Controller.TrainingMode)
            {
                return;
            }

            if (!Academy.IsInitialized)
            {
                return;
            }

            float stage = Academy.Instance.EnvironmentParameters.GetWithDefault("lesson_stage", 0.0f);
            int stageInt = Mathf.RoundToInt(stage);
            CurrentStage = stageInt;
            m_LastLessonStage = stage;
            m_Applied = true;

            StageConfig config = null;
            if (stageInt >= 0 && stageInt < StageConfigs.Count)
            {
                config = StageConfigs[stageInt];
            }

            if (config != null)
            {
                m_Controller.MatchTimeOverrideSeconds = config.MatchTimeOverrideSeconds;
                m_Controller.RandomizeOpponent = config.RandomizeOpponent;
                m_Controller.OpponentTankScript = config.OpponentScript ?? string.Empty;

                // 像命令行 SetOpponentPool 一样覆盖列表，而不是直接替换对象引用
                m_Controller.OpponentPool.Clear();
                if (config.OpponentPool != null)
                {
                    for (int i = 0; i < config.OpponentPool.Count; ++i)
                    {
                        string script = config.OpponentPool[i];
                        if (!string.IsNullOrWhiteSpace(script))
                        {
                            m_Controller.OpponentPool.Add(script.Trim());
                        }
                    }
                }

                string opponentDesc = !string.IsNullOrEmpty(config.OpponentScript)
                    ? config.OpponentScript
                    : (config.OpponentPool.Count > 0 ? string.Join(" | ", config.OpponentPool) : "none");
                Debug.Log($"[Curriculum] Stage {stageInt} - Opponent: {opponentDesc}, Randomize={config.RandomizeOpponent}");
            }
            else
            {
                m_Controller.MatchTimeOverrideSeconds = 0;
                m_Controller.OpponentTankScript = "UtilityBasedAI.MyTank";
                m_Controller.RandomizeOpponent = false;
                m_Controller.OpponentPool.Clear();
                m_Controller.OpponentPool.Add("UtilityBasedAI.MyTank");
                Debug.Log("[Curriculum] Stage default - Full Competition vs UtilityBasedAI");
            }
        }
    }
}
