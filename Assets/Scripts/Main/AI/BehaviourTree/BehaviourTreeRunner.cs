using AI.Base;
using AI.Blackboard;

namespace AI.BehaviourTree
{
    public static class BehaviourTreeRunner
    {
        public static void Exec(Node root, IAgent agent, BlackboardMemory workingMemory)
        {
            ERunningStatus runningStatus = root.Update(agent, workingMemory);
            if(runningStatus != ERunningStatus.Executing)
            {
                root.Reset(agent, workingMemory);
            }
        }
    }
}
