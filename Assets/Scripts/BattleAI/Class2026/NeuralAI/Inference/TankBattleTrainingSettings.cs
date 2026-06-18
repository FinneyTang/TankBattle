using System;
using Unity.MLAgents.Actuators;

namespace NeuralAI
{
    public enum FireControlMode
    {
        NeuralNetwork,
        FixedAlgorithm
    }

    [Serializable]
    public class TankBattleTrainingSettings
    {
        public const string BehaviorName = "TankBattlePPO";
        public const int ObservationSize = 128;
        public const int ContinuousActionSize = 4;
        public static readonly int[] DiscreteBranches = { 2 };

        public int DecisionInterval = 3;
        public float MoveStepDistance = 6f;
        public float AimDistance = 100f;
        public float ActionDeadZone = 0.1f;
        public bool RequestDecisionsWhileDead = false;
        public bool FireOnlyOnDecision = true;
        public bool UseHeuristicWhenNoTrainer = true;
        public FireControlMode FireMode = FireControlMode.FixedAlgorithm;

        public void CopyFrom(TankBattleTrainingSettings other)
        {
            if (other == null)
            {
                return;
            }

            DecisionInterval = other.DecisionInterval;
            MoveStepDistance = other.MoveStepDistance;
            AimDistance = other.AimDistance;
            ActionDeadZone = other.ActionDeadZone;
            RequestDecisionsWhileDead = other.RequestDecisionsWhileDead;
            FireOnlyOnDecision = other.FireOnlyOnDecision;
            UseHeuristicWhenNoTrainer = other.UseHeuristicWhenNoTrainer;
            FireMode = other.FireMode;
        }

        public ActionSpec CreateActionSpec()
        {
            return new ActionSpec(ContinuousActionSize, (int[])DiscreteBranches.Clone());
        }
    }
}
