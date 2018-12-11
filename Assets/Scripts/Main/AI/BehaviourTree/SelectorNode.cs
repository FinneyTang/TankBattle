using AI.Base;
using AI.Blackboard;

namespace AI.BehaviourTree
{
    public class SelectorNode : Node
    {
        private Node m_LastRunningNode;
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            ERunningStatus runningStatus = ERunningStatus.Finished;
            Node previousNode = m_LastRunningNode;
            //select running node
            m_LastRunningNode = null;
            foreach (Node c in m_Children)
            {
                runningStatus = c.Update(agent, workingMemory);
                if(runningStatus != ERunningStatus.Failed)
                {
                    m_LastRunningNode = c;
                    break;
                }
            }
            //clear last running node
            if (previousNode != m_LastRunningNode && previousNode != null)
            {
                previousNode.Reset(agent, workingMemory);
            }
            return runningStatus;
        }
        protected override void OnReset(IAgent agent, BlackboardMemory workingMemory)
        {
            if (m_LastRunningNode != null)
            {
                m_LastRunningNode.Reset(agent, workingMemory);
            }
            m_LastRunningNode = null;
        }
    }
}
