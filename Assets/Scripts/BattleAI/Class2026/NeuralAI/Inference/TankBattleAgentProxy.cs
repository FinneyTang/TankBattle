using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Main;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace NeuralAI
{
    public class TankBattleAgentProxy : MonoBehaviour
    {
        [Header("Tank Binding")]
        [Tooltip("自动绑定该队伍中的第一个 Neural Tank")]
        public ETeam TargetTeam = ETeam.A;
        public bool AutoFindTank = true;

        [Header("Inference Override")]
        [Tooltip("纯推理时直接拖模型；训练时（mlagents-learn）留空。兼容 ML-Agents 2.x 的 NNModel 和 3.x 的 ModelAsset。")]
        public UnityEngine.Object OverrideModel;
        public InferenceDevice Device = InferenceDevice.Default;

        [Header("Agent Settings Override")]
        [Tooltip("只覆盖 NeuralAI 自身参数，不改 Match 里的选手/对手配置。")]
        public bool OverrideAgentSettings = false;
        public TankBattleTrainingSettings AgentSettings = new TankBattleTrainingSettings();

        private TankBattleMlAgent m_MlAgent;
        private TankBattleAgent m_Tank;
        private int m_Frame;
        private bool m_Bound;

        void Start()
        {
            if (AutoFindTank)
                StartCoroutine(WaitAndBind());
        }

        IEnumerator WaitAndBind()
        {
            while (Match.instance == null)
                yield return null;

            // 给 Match.Start() 一帧时间完成 m_Tanks 初始化
            yield return null;

            List<Tank> tanks = null;
            do
            {
                try
                {
                    tanks = Match.instance.GetTanks(TargetTeam);
                }
                catch
                {
                    tanks = null;
                }

                if (tanks == null || tanks.Count == 0)
                    yield return null;
            } while (tanks == null || tanks.Count == 0);

            foreach (var tank in tanks)
            {
                if (tank is TankBattleAgent agent)
                {
                    Bind(agent);
                    yield break;
                }
            }
        }

        public void Bind(TankBattleAgent tank)
        {
            if (m_Bound || tank == null)
                return;

            m_Tank = tank;
            if (OverrideAgentSettings)
            {
                m_Tank.Settings.CopyFrom(AgentSettings);
            }

            // 按正确顺序动态创建：先配好 BehaviorParameters，再挂 Agent
            // 避免预设 GameObject 上提前挂了 MlAgent 导致 Policy 初始化时参数错误
            var behavior = gameObject.GetComponent<BehaviorParameters>();
            if (behavior == null)
                behavior = gameObject.AddComponent<BehaviorParameters>();

            behavior.TeamId = (int)tank.Team;
            behavior.BrainParameters.VectorObservationSize = TankBattleTrainingSettings.ObservationSize;
            behavior.BrainParameters.NumStackedVectorObservations = 3;
            behavior.BrainParameters.ActionSpec = tank.Settings.CreateActionSpec();
            behavior.BehaviorName = TankBattleTrainingSettings.BehaviorName;
            behavior.BehaviorType = BehaviorType.Default;

            m_MlAgent = gameObject.GetComponent<TankBattleMlAgent>();
            if (m_MlAgent == null)
                m_MlAgent = gameObject.AddComponent<TankBattleMlAgent>();

            m_MlAgent.Bind(tank, tank.GetRewardTracker(), tank.Settings);

            if (OverrideModel != null)
            {
                TryApplyModelOverride();
            }

            m_Bound = true;
        }

        private void TryApplyModelOverride()
        {
            MethodInfo setModel = FindCompatibleSetModel();
            if (setModel == null)
            {
                Debug.LogWarning(
                    $"[TankBattleAgentProxy] OverrideModel type {OverrideModel.GetType().Name} is not compatible " +
                    "with the current ML-Agents Agent.SetModel overload.");
                return;
            }

            setModel.Invoke(m_MlAgent, new object[]
            {
                TankBattleTrainingSettings.BehaviorName,
                OverrideModel,
                Device
            });
        }

        private MethodInfo FindCompatibleSetModel()
        {
            MethodInfo[] methods = typeof(TankBattleMlAgent).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; ++i)
            {
                MethodInfo method = methods[i];
                if (method.Name != nameof(TankBattleMlAgent.SetModel))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 3 ||
                    parameters[0].ParameterType != typeof(string) ||
                    !parameters[1].ParameterType.IsInstanceOfType(OverrideModel) ||
                    parameters[2].ParameterType != typeof(InferenceDevice))
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private void Update()
        {
            if (!m_Bound || m_MlAgent == null || m_Tank == null || Match.instance == null)
                return;

            m_MlAgent.TickReward();
            if (Match.instance.IsMathEnd())
                return;

            if (m_Tank.IsDead && !m_Tank.Settings.RequestDecisionsWhileDead)
                return;

            m_Frame++;
            bool decisionFrame = m_Frame % Mathf.Max(1, m_Tank.Settings.DecisionInterval) == 0;
            if (decisionFrame)
            {
                m_MlAgent.AllowFireForNextAction();
                m_MlAgent.RequestDecision();
            }
            else
            {
                m_MlAgent.RequestAction();
            }
        }
    }
}
