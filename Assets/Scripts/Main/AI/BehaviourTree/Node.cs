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
        protected Node m_Parent;
        protected List<Node> m_Children = new List<Node>();

        private Condition m_Precondition;
        public ERunningStatus Update(IAgent agent, BlackboardMemory workingMemroy)
        {
            if(m_Precondition != null && m_Precondition.IsTrue(agent) == false)
            {
                return ERunningStatus.Failed;
            }
            return OnUpdate(agent, workingMemroy);
        }
        public void Transition(IAgent agent, BlackboardMemory workingMemroy)
        {
            OnTransition(agent, workingMemroy);
        }
        public Node AddChild(Node c)
        {
            c.m_Parent = this;
            m_Children.Add(c);
            return this;
        }
        public Node SetPrecondition(Condition p)
        {
            m_Precondition = p;
            return this;
        }
        protected virtual ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemroy)
        {
            return ERunningStatus.Finished;
        }
        protected virtual void OnTransition(IAgent agent, BlackboardMemory workingMemroy)
        {
        }
    }
}
