using AI.Base;
using AI.Blackboard;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI.BehaviourTree
{
    public class ParallelNode : Node
    {
        private int m_RequestFinishedCount;
        private List<ERunningStatus> m_ChildrenRunning = new List<ERunningStatus>();
        public ParallelNode(int threshold)
        {
            m_RequestFinishedCount = threshold;
        }
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            if(m_Children.Count == 0)
            {
                return ERunningStatus.Finished;
            }
            m_RequestFinishedCount = Mathf.Clamp(m_RequestFinishedCount, 1, m_Children.Count);
            if (m_ChildrenRunning.Count != m_Children.Count)
            {
                m_ChildrenRunning.AddRange(Enumerable.Repeat(ERunningStatus.Executing, m_Children.Count));
            }
            int failedCount   = 0;
            int finishedCount = 0;
            for(int i = 0; i < m_Children.Count; ++i)
            {
                ERunningStatus status = m_ChildrenRunning[i];
                if (status == ERunningStatus.Executing)
                {
                    status = m_Children[i].Update(agent, workingMemory);
                }
                if (status == ERunningStatus.Finished)
                {
                    finishedCount++;
                    m_ChildrenRunning[i] = status;
                    if(finishedCount == m_RequestFinishedCount)
                    {
                        return ERunningStatus.Finished;
                    }
                }
                else if(status == ERunningStatus.Failed)
                {
                    failedCount++;
                    if(failedCount > m_Children.Count - m_RequestFinishedCount)
                    {
                        return ERunningStatus.Failed;
                    }
                }
            }
            return ERunningStatus.Executing;
        }
        protected override void OnReset(IAgent agent, BlackboardMemory workingMemory)
        {
            for (int i = 0; i < m_Children.Count; ++i)
            {
                m_Children[i].Reset(agent, workingMemory);
            }
            m_ChildrenRunning.Clear();
        }
    }
}
