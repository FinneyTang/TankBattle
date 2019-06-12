using AI.Base;
using AI.Blackboard;
using AI.BehaviourTree;

namespace lhx
{
	// TODO Support more than 2 children
	public class WeightedSelectorNode : Node
    {
		private EBBKey rateType;
        private Node m_LastRunningNode;
		public WeightedSelectorNode(EBBKey type)
		{
			rateType = type;
		}
		protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
			float rate = workingMemory.GetValue<float>((int)rateType);
			// TODO Test the certainty rate
			Node c = rate > 50 || true ? m_Children[0] : m_Children[1];
			ERunningStatus runningStatus = c.Update(agent, workingMemory);
			Node previousNode = m_LastRunningNode;
			m_LastRunningNode = null;
			if (runningStatus != ERunningStatus.Failed)
				m_LastRunningNode = c;
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
