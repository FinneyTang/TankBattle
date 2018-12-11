using AI.Base;
using AI.Blackboard;
using UnityEngine;

namespace AI.BehaviourTree
{
    public class RepeatNode : DecoratorNode
    {
        private int m_RepeatCount;
        private int m_RepeatIndex;
        public RepeatNode(Node child, int count = 1) : base(child)
        {
            m_RepeatCount = Mathf.Max(1, count);
            m_RepeatIndex = 0;
        }
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            while(true)
            {
                ERunningStatus status = Child.Update(agent, workingMemory);
                if (status == ERunningStatus.Failed)
                {
                    return ERunningStatus.Failed;
                }
                else if(status == ERunningStatus.Executing)
                {
                    break;
                }
                else
                {
                    if(++m_RepeatIndex == m_RepeatCount)
                    {
                        return ERunningStatus.Finished;
                    }
                    Child.Reset(agent, workingMemory);
                }
            }
            return ERunningStatus.Executing;
        }
        protected override void OnReset(IAgent agent, BlackboardMemory workingMemory)
        {
            Child.Reset(agent, workingMemory);
            m_RepeatIndex = 0;
        }
    }
}
