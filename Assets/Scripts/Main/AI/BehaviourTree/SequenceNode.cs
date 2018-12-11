using AI.Base;
using AI.Blackboard;

namespace AI.BehaviourTree
{
    public class SequenceNode : Node
    {
        private int m_CurrentNodeIndex = -1;
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            if(m_Children.Count == 0)
            {
                return ERunningStatus.Finished;
            }
            if(m_CurrentNodeIndex < 0)
            {
                m_CurrentNodeIndex = 0;
            }
            for(int i = m_CurrentNodeIndex; i < m_Children.Count; ++i)
            {
                ERunningStatus status = m_Children[i].Update(agent, workingMemory);
                if(status != ERunningStatus.Finished)
                {
                    return status;
                }
                m_CurrentNodeIndex++;
            }
            return ERunningStatus.Finished;
        }
        protected override void OnReset(IAgent agent, BlackboardMemory workingMemory)
        {
            if(m_CurrentNodeIndex >= 0 && m_CurrentNodeIndex < m_Children.Count)
            {
                m_Children[m_CurrentNodeIndex].Reset(agent, workingMemory);
            }
            m_CurrentNodeIndex = -1;
        }
    }
}
