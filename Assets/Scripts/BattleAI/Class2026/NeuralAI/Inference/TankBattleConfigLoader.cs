using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace NeuralAI
{
    /// <summary>
    /// 运行时外部配置加载器。
    /// 查找优先级：
    /// 1. 项目根目录/build/config/training_config.json（构建输出首选）
    /// 2. Assets/NeuralAI/Training/Configs/training_config.json（项目内规范配置）
    /// 3. 项目根目录/config/training_config.json（向后兼容）
    /// 4. StreamingAssets/training_config.json（Editor 内调试）
    /// 修改外部 JSON 后无需重新编译即可生效。
    /// </summary>
    public static class TankBattleConfigLoader
    {
        private static TankBattleConfig s_Config;
        private static bool s_Loaded;

        public static TankBattleConfig Config
        {
            get
            {
                if (!s_Loaded)
                {
                    Load();
                }
                return s_Config;
            }
        }

        public static void Reload()
        {
            s_Loaded = false;
            Load();
        }

        private static void Load()
        {
            s_Loaded = true;
            string path = GetConfigPath();

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    s_Config = JsonConvert.DeserializeObject<TankBattleConfig>(json);
                    Debug.Log($"[TankBattleConfig] Loaded from {path}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[TankBattleConfig] Failed to load config: {e.Message}");
                    s_Config = CreateDefault();
                }
            }
            else
            {
                Debug.LogWarning($"[TankBattleConfig] Config not found at {path}, using defaults.");
                s_Config = CreateDefault();
            }
        }

        private static string GetConfigPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            // 1. 优先使用 build/config/training_config.json
            string buildPath = Path.Combine(projectRoot, "build", "config", "training_config.json");
            if (File.Exists(buildPath))
            {
                return buildPath;
            }

            // 2. 项目内规范配置，便于随 NeuralAI 文件夹分发
            string neuralTrainingConfigPath = Path.Combine(projectRoot, "Assets", "NeuralAI", "Training", "Configs", "training_config.json");
            if (File.Exists(neuralTrainingConfigPath))
            {
                return neuralTrainingConfigPath;
            }

            // 3. 项目根目录下的 config/training_config.json（旧路径兼容）
            string projectConfigPath = Path.Combine(projectRoot, "config", "training_config.json");
            if (File.Exists(projectConfigPath))
            {
                return projectConfigPath;
            }

            // 4. StreamingAssets（Unity 标准路径，Editor 内调试）
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "training_config.json");
            if (File.Exists(streamingPath))
            {
                return streamingPath;
            }

            // 默认返回 build 路径（即使不存在，提示日志也明确告知用户期望位置）
            return buildPath;
        }

        private static TankBattleConfig CreateDefault()
        {
            return new TankBattleConfig
            {
                CurriculumStages = new System.Collections.Generic.List<StageConfigData>
                {
                    new StageConfigData { OpponentPool = new System.Collections.Generic.List<string> { "RuleBasedAI.MyTank", "ScriptBasedAI.MyTank" }, RandomizeOpponent = true },
                    new StageConfigData { OpponentPool = new System.Collections.Generic.List<string> { "RuleBasedAI.MyTank", "FSM.MyTank", "ScriptBasedAI.MyTank", "UtilityBasedAI.MyTank" }, RandomizeOpponent = true },
                    new StageConfigData { OpponentPool = new System.Collections.Generic.List<string> { "FSM.MyTank", "SensorAI.MyTank", "UtilityBasedAI.MyTank", "BT.MyTank", "RuleBasedAI.MyTank" }, RandomizeOpponent = true },
                    new StageConfigData { OpponentPool = new System.Collections.Generic.List<string> { "FSM.MyTank", "UtilityBasedAI.MyTank", "BT.MyTank", "GOAP.MyTank" }, RandomizeOpponent = true },
                    new StageConfigData { OpponentPool = new System.Collections.Generic.List<string> { "FSM.MyTank", "SensorAI.MyTank", "UtilityBasedAI.MyTank", "BT.MyTank", "GOAP.MyTank" }, RandomizeOpponent = true },
                },
                RewardConfig = new RewardConfigData
                {
                    SuperStarScoreReward = 1.5f,
                    OpponentScorePenaltyMultiplier = 0.2f,
                    DamageRewardPerHit = 0.1f,
                    DeathPenalty = 1f,
                    DodgeRewardPerThreat = 0.02f,
                    MaxDodgeRewardPerStep = 0.1f,
                    HealRewardPerHp = 0.02f,
                    ThreatCrossTrackDistance = 2.5f,
                    ThreatTimeWindow = 2f,
                    StageScales = new System.Collections.Generic.List<StageScaleData>
                    {
                        new StageScaleData { StarRewardScale = 1.0f, DamageDealtScale = 1.2f, DeathPenaltyScale = 1.5f, FireAccuracyScale = 1.2f, HealRewardScale = 3.0f },
                        new StageScaleData { StarRewardScale = 1f, DamageDealtScale = 1f, DeathPenaltyScale = 1.5f, FireAccuracyScale = 1f, HealRewardScale = 2.0f },
                        new StageScaleData { StarRewardScale = 1.0f, DamageDealtScale = 1.0f, DeathPenaltyScale = 1.5f, FireAccuracyScale = 1.0f, HealRewardScale = 2.0f },
                        new StageScaleData { StarRewardScale = 1.0f, DamageDealtScale = 1.0f, DeathPenaltyScale = 1.5f, FireAccuracyScale = 1.0f, HealRewardScale = 1.0f },
                        new StageScaleData { StarRewardScale = 1.0f, DamageDealtScale = 1.0f, DeathPenaltyScale = 1.5f, FireAccuracyScale = 1.0f, HealRewardScale = 1.0f },
                    }
                }
            };
        }
    }
}
