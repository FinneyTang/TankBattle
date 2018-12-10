using AI.Base;

namespace AI.RuleBased
{
    public class Condition
    {
        public virtual bool IsTrue(IAgent owner)
        {
            return false;
        }
    }
    class TrueCondition : Condition
    {
        public override bool IsTrue(IAgent owner)
        {
            return true;
        }
    }
    class FalseCondition : Condition
    {
        public override bool IsTrue(IAgent owner)
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
        public override bool IsTrue(IAgent owner)
        {
            return m_LHS.IsTrue(owner) && m_RHS.IsTrue(owner);
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
        public override bool IsTrue(IAgent owner)
        {
            return m_LHS.IsTrue(owner) || m_RHS.IsTrue(owner);
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
        public override bool IsTrue(IAgent owner)
        {
            return m_LHS.IsTrue(owner) ^ m_RHS.IsTrue(owner);
        }
    }
    class NotCondition : Condition
    {
        private Condition m_LHS;
        public NotCondition(Condition lhs)
        {
            m_LHS = lhs;
        }
        public override bool IsTrue(IAgent owner)
        {
            return !m_LHS.IsTrue(owner);
        }
    }
}
