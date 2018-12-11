using AI.Base;
using AI.Blackboard;

namespace AI.BehaviourTree
{
    public class SelectorNode : Node
    {
        private Node m_LastRunningNode;
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemroy)
        {
            ERunningStatus runningStatus = ERunningStatus.Failed;
            Node curNode = null;
            foreach (Node c in m_Children)
            {
                runningStatus = c.Update(agent, workingMemroy);
                if(runningStatus == ERunningStatus.Failed)
                {
                    continue;
                }
                else
                {
                    curNode = c;
                    break;
                }
            }
            if (curNode != m_LastRunningNode)
            {
                if(m_LastRunningNode != null)
                {
                    m_LastRunningNode.Transition(agent, workingMemroy);
                }
                m_LastRunningNode = curNode;
            }
            return runningStatus;
        }
        protected override void OnTransition(IAgent agent, BlackboardMemory workingMemroy)
        {
            if (m_LastRunningNode != null)
            {
                m_LastRunningNode.Transition(agent, workingMemroy);
                m_LastRunningNode = null;
            }
        }
    }
}
