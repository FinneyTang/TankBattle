using AI.Base;
using AI.Blackboard;

namespace AI.BehaviourTree
{
    public class ActionNode : Node
    {
        private const int ACTION_Ready      = 1;
        private const int ACTION_Running    = 2;

        private int m_ActionStatus = ACTION_Ready;
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            if(OnEvaluate(agent, workingMemory) == false)
            {
                return ERunningStatus.Failed;
            }
            ERunningStatus runningStatus = ERunningStatus.Finished;
            if(m_ActionStatus == ACTION_Ready)
            {
                OnEnter(agent, workingMemory);
                m_ActionStatus = ACTION_Running;
            }
            if(m_ActionStatus == ACTION_Running)
            {
                runningStatus = OnExecute(agent, workingMemory);
            }
            return runningStatus;
        }
        protected override void OnReset(IAgent agent, BlackboardMemory workingMemory)
        {
            if(m_ActionStatus == ACTION_Running)
            {
                OnExit(agent, workingMemory);
            }
            m_ActionStatus = ACTION_Ready;
        }

        protected virtual bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return true;
        }
        protected virtual void OnEnter(IAgent agent, BlackboardMemory workingMemory)
        {}
        protected virtual ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            return ERunningStatus.Finished;
        }
        protected virtual void OnExit(IAgent agent, BlackboardMemory workingMemory)
        { }
    }
}
