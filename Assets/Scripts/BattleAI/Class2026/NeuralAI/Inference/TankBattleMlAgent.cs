using Main;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace NeuralAI
{
    [RequireComponent(typeof(BehaviorParameters))]
    public class TankBattleMlAgent : Agent
    {
        private readonly BattleObservationEncoder m_ObservationEncoder = new BattleObservationEncoder();

        private Tank m_Tank;
        private RewardTracker m_RewardTracker;
        private TankBattleTrainingSettings m_Settings;
        private bool m_EpisodeEnded;
        private bool m_AllowFireForNextAction;

        public bool IsReady => m_Tank != null && m_Settings != null;

        public void Bind(Tank tank, RewardTracker rewardTracker, TankBattleTrainingSettings settings)
        {
            m_Tank = tank;
            m_RewardTracker = rewardTracker;
            m_Settings = settings;
            ConfigureBehavior(tank.Team, settings);
            m_RewardTracker?.Reset();
        }

        protected override void OnEnable()
        {
            if (m_Settings != null && m_Tank != null)
            {
                ConfigureBehavior(m_Tank.Team, m_Settings);
            }
            base.OnEnable();
        }

        public void TickReward()
        {
            if (!IsReady || m_RewardTracker == null || Match.instance == null || m_EpisodeEnded)
            {
                return;
            }

            AddReward(m_RewardTracker.CollectStepReward());
            if (Match.instance.IsMathEnd())
            {
                AddReward(m_RewardTracker.CollectTerminalReward());
                m_EpisodeEnded = true;
                EndEpisode();
            }
        }

        public void AllowFireForNextAction()
        {
            m_AllowFireForNextAction = true;
        }

        public override void OnEpisodeBegin()
        {
            m_EpisodeEnded = false;
            m_RewardTracker?.Reset();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (!IsReady)
            {
                for (int i = 0; i < TankBattleTrainingSettings.ObservationSize; ++i)
                {
                    sensor.AddObservation(0f);
                }
                return;
            }

            m_ObservationEncoder.AddObservations(m_Tank, sensor);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!IsReady || Match.instance == null || Match.instance.IsMathEnd())
            {
                return;
            }

            bool allowFire = !m_Settings.FireOnlyOnDecision || m_AllowFireForNextAction;
            TankBattleActionResult result = TankBattleActionMapper.Apply(
                m_Tank,
                actions,
                m_Settings,
                allowFire);
            m_AllowFireForNextAction = false;
            m_RewardTracker?.ReportAction(result);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            var discrete = actionsOut.DiscreteActions;
            for (int i = 0; i < continuous.Length; ++i)
            {
                continuous[i] = 0f;
            }

            for (int i = 0; i < discrete.Length; ++i)
            {
                discrete[i] = 0;
            }

            if (!IsReady || !m_Settings.UseHeuristicWhenNoTrainer)
            {
                return;
            }

            Tank enemy = Match.instance != null ? Match.instance.GetOppositeTank(m_Tank.Team) : null;
            if (enemy != null && !enemy.IsDead)
            {
                Vector3 aim = (enemy.Position - m_Tank.Position).normalized;
                continuous[2] = aim.x;
                continuous[3] = aim.z;
                discrete[0] = m_Tank.CanFire() && m_Tank.CanSeeOthers(enemy) ? 1 : 0;
            }
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
            behavior.BehaviorType = BehaviorType.Default;
        }
    }
}
