using AI.Base;

namespace AI.RuleBased
{
    public class Condition
    {
        public virtual bool IsTrue(IAgent agent)
        {
            return false;
        }
    }
    class TrueCondition : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return true;
        }
    }
    class FalseCondition : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return false;
        }
    }
    class AndCondition : Condition
    {
        private Condition m_LHS;
        private Condition m_RHS;
        public AndCondition(Condition lhs, Condition rhs)
        {
            m_LHS = lhs;
            m_RHS = rhs;
        }
        public override bool IsTrue(IAgent agent)
        {
            return m_LHS.IsTrue(agent) && m_RHS.IsTrue(agent);
        }
    }
    class OrCondition : Condition
    {
        private Condition m_LHS;
        private Condition m_RHS;
        public OrCondition(Condition lhs, Condition rhs)
        {
            m_LHS = lhs;
            m_RHS = rhs;
        }
        public override bool IsTrue(IAgent agent)
        {
            return m_LHS.IsTrue(agent) || m_RHS.IsTrue(agent);
        }
    }
    class XorCondition : Condition
    {
        private Condition m_LHS;
        private Condition m_RHS;
        public XorCondition(Condition lhs, Condition rhs)
        {
            m_LHS = lhs;
            m_RHS = rhs;
        }
        public override bool IsTrue(IAgent agent)
        {
            return m_LHS.IsTrue(agent) ^ m_RHS.IsTrue(agent);
        }
    }
    class NotCondition : Condition
    {
        private Condition m_LHS;
        public NotCondition(Condition lhs)
        {
            m_LHS = lhs;
        }
        public override bool IsTrue(IAgent agent)
        {
            return !m_LHS.IsTrue(agent);
        }
    }
}
