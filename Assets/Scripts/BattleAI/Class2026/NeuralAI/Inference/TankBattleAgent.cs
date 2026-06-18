using Main;
using UnityEngine;

namespace NeuralAI
{
    public class TankBattleAgent : Tank
    {
        public TankBattleTrainingSettings Settings = new TankBattleTrainingSettings();
        private RewardTracker m_RewardTracker;

        public override string GetName()
        {
            return "NeuralTank";
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            m_RewardTracker = new RewardTracker();
            m_RewardTracker.Bind(this);
        }

        public RewardTracker GetRewardTracker() => m_RewardTracker;

        protected override void OnReborn()
        {
            base.OnReborn();
        }
    }
}
