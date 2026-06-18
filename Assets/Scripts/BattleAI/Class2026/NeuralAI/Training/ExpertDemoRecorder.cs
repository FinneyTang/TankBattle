using System;
using System.IO;
using Main;
using Newtonsoft.Json;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace NeuralAI
{
    public class ExpertDemoRecorder : MonoBehaviour
    {
        public Tank TargetTank;
        public TankBattleTrainingSettings Settings = new TankBattleTrainingSettings();
        public bool RecordMlAgentsDemo = true;
        public bool RecordJsonl = true;
        public string DemonstrationName = "tankbattle_expert";
        public string DemonstrationDirectory = "../TrainingData/Demonstrations";
        public string JsonlPath = "../TrainingData/tankbattle_expert.jsonl";

        private void Start()
        {
            TargetTank ??= GetComponent<Tank>();
            if (TargetTank == null)
            {
                Debug.LogWarning("ExpertDemoRecorder requires a Tank target.");
                enabled = false;
                return;
            }

            ExpertDemoAgent demoAgent = GetComponent<ExpertDemoAgent>();
            if (demoAgent == null)
            {
                demoAgent = gameObject.AddComponent<ExpertDemoAgent>();
            }
            demoAgent.Bind(TargetTank, Settings, RecordJsonl, ResolvePath(JsonlPath));

            if (RecordMlAgentsDemo)
            {
                DemonstrationRecorder recorder = GetComponent<DemonstrationRecorder>();
                if (recorder == null)
                {
                    recorder = gameObject.AddComponent<DemonstrationRecorder>();
                }

                string demonstrationDirectory = ResolvePath(DemonstrationDirectory);
                Directory.CreateDirectory(demonstrationDirectory);
                recorder.Record = true;
                recorder.DemonstrationName = DemonstrationName;
                recorder.DemonstrationDirectory = demonstrationDirectory;
            }
        }

        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, path));
        }
    }

    [RequireComponent(typeof(BehaviorParameters))]
    public class ExpertDemoAgent : Agent
    {
        [Serializable]
        private class JsonDemoFrame
        {
            public float time;
            public string tankName;
            public float[] observations;
            public float[] continuousActions;
            public int[] discreteActions;
        }

        private readonly BattleObservationEncoder m_ObservationEncoder = new BattleObservationEncoder();
        private readonly ExpertActionInferenceState m_InferenceState = new ExpertActionInferenceState();
        private readonly float[] m_ContinuousActions = new float[TankBattleTrainingSettings.ContinuousActionSize];
        private readonly int[] m_DiscreteActions = new int[TankBattleTrainingSettings.DiscreteBranches.Length];

        private Tank m_Tank;
        private TankBattleTrainingSettings m_Settings;
        private StreamWriter m_Writer;
        private int m_Frame;

        protected override void OnEnable()
        {
            ExpertDemoRecorder recorder = GetComponent<ExpertDemoRecorder>();
            Tank tank = m_Tank ?? recorder?.TargetTank ?? GetComponent<Tank>();
            TankBattleTrainingSettings settings =
                m_Settings ?? recorder?.Settings ?? new TankBattleTrainingSettings();
            ETeam team = tank != null ? tank.Team : ETeam.A;
            ConfigureBehavior(team, settings);
            base.OnEnable();
        }

        public void Bind(Tank tank, TankBattleTrainingSettings settings, bool writeJsonl, string jsonlPath)
        {
            m_Tank = tank;
            m_Settings = settings;
            ConfigureBehavior(tank.Team, settings);

            if (writeJsonl && !string.IsNullOrEmpty(jsonlPath))
            {
                string directory = Path.GetDirectoryName(jsonlPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                m_Writer = new StreamWriter(jsonlPath, true);
            }
        }

        private void Update()
        {
            if (m_Tank == null || m_Settings == null || Match.instance == null ||
                Match.instance.IsMathEnd())
            {
                return;
            }

            m_Frame++;
            if (m_Frame % Mathf.Max(1, m_Settings.DecisionInterval) == 0)
            {
                RequestDecision();
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (m_Tank == null)
            {
                for (int i = 0; i < TankBattleTrainingSettings.ObservationSize; ++i)
                {
                    sensor.AddObservation(0f);
                }
                return;
            }

            m_ObservationEncoder.AddObservations(m_Tank, sensor);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            TankBattleActionMapper.InferExpertAction(
                m_Tank, m_Settings, m_InferenceState, m_ContinuousActions, m_DiscreteActions);

            var continuous = actionsOut.ContinuousActions;
            var discrete = actionsOut.DiscreteActions;
            for (int i = 0; i < continuous.Length && i < m_ContinuousActions.Length; ++i)
            {
                continuous[i] = m_ContinuousActions[i];
            }

            for (int i = 0; i < discrete.Length && i < m_DiscreteActions.Length; ++i)
            {
                discrete[i] = m_DiscreteActions[i];
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (m_Writer == null || m_Tank == null)
            {
                return;
            }

            var frame = new JsonDemoFrame
            {
                time = Time.time,
                tankName = m_Tank.GetName(),
                observations = m_ObservationEncoder.Encode(m_Tank),
                continuousActions = Copy(actions.ContinuousActions),
                discreteActions = Copy(actions.DiscreteActions)
            };
            m_Writer.WriteLine(JsonConvert.SerializeObject(frame));
            m_Writer.Flush();
        }

        private void OnDestroy()
        {
            m_Writer?.Dispose();
            m_Writer = null;
        }

        private void ConfigureBehavior(ETeam team, TankBattleTrainingSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            m_Settings = settings;
            BehaviorParameters behavior = GetComponent<BehaviorParameters>();
            behavior.TeamId = (int)team;
            behavior.BrainParameters.VectorObservationSize = TankBattleTrainingSettings.ObservationSize;
            behavior.BrainParameters.NumStackedVectorObservations = 3;
            behavior.BrainParameters.ActionSpec = settings.CreateActionSpec();
            behavior.BehaviorName = TankBattleTrainingSettings.BehaviorName;
            behavior.BehaviorType = BehaviorType.HeuristicOnly;
        }

        private float[] Copy(ActionSegment<float> source)
        {
            float[] result = new float[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                result[i] = source[i];
            }
            return result;
        }

        private int[] Copy(ActionSegment<int> source)
        {
            int[] result = new int[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                result[i] = source[i];
            }
            return result;
        }
    }
}
