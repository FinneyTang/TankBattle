using System.Collections.Generic;
using AI.Base;

namespace LXK
{
    public class UtilityBase
    {
        float baseScore = 0.0f;
        public float GetBaseScore() { return baseScore; }
        public float CalcU(IAgent agent)
        {
            baseScore = OnCalcU(agent);
            return baseScore;
        }
        protected virtual float OnCalcU(IAgent agent)
        {
            return 0.0f;
        }
    }

    class OneMinusScore : UtilityBase
    {
        protected UtilityBase ub;
        public OneMinusScore(UtilityBase ub)
        {
            this.ub = ub;
        }
        protected override float OnCalcU(IAgent agent)
        {
            float tempScore = ub.CalcU(agent);
            return (1.0f - tempScore) < 0 ? 0.0f : (1.0f - tempScore);
        }
    }

    class CompositeUtility : UtilityBase
    {
        protected List<UtilityBase> utilities = new List<UtilityBase>();
        public virtual CompositeUtility AddUtility(UtilityBase ub)
        {
            utilities.Add(ub);
            return this;
        }
    }

    class AvgComposite : CompositeUtility
    {
        protected override float OnCalcU(IAgent agent)
        {
            if (utilities.Count == 0)
            {
                return 0.0f;
            }
            float totalScore = 0.0f;
            foreach (var item in utilities)
            {
                totalScore += item.CalcU(agent);
            }
            return totalScore/utilities.Count;
        }
    }

    class MulComposite : CompositeUtility
    {
        protected override float OnCalcU(IAgent agent)
        {
            if (utilities.Count == 0)
            {
                return 0.0f;
            }
            float mulResult = 1.0f;
            foreach (var item in utilities)
            {
                mulResult *= item.CalcU(agent);
            }
            return mulResult;
        }
    }
}
