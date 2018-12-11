using AI.Base;
using AI.Blackboard;
using AI.RuleBased;
using System.Collections.Generic;

namespace AI.BehaviourTree
{
    public enum ERunningStatus
    {
        Failed, Executing, Finished
    }
    public class Node
    {
        //Tree Structure
        protected Node m_Parent;
        protected List<Node> m_Children = new List<Node>();

        //Precondition
        private Condition m_Precondition;
        public ERunningStatus Update(IAgent agent, BlackboardMemory workingMemory)
        {
            if(m_Precondition != null && m_Precondition.IsTrue(agent) == false)
            {
                return ERunningStatus.Failed;
            }
            return OnUpdate(agent, workingMemory);
        }
        public void Reset(IAgent agent, BlackboardMemory workingMemory)
        {
            OnReset(agent, workingMemory);
        }
        public Node AddChild(params Node[] children)
        {
            foreach(Node c in children)
            {
                c.m_Parent = this;
                m_Children.Add(c);
            }
            return this;
        }
        public Node SetPrecondition(Condition p)
        {
            m_Precondition = p;
            return this;
        }
        //implemented by inherited class
        protected virtual ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            return ERunningStatus.Finished;
        }
        protected virtual void OnReset(IAgent agent, BlackboardMemory workingMemory)
        {
        }
    }
}
