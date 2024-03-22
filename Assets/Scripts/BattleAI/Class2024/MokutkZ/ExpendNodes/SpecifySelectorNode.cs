using AI.Base;
using AI.Blackboard;
using AI.BehaviourTree;

namespace MokutkZ
{
    abstract class SpecifySelectorNode : Node
    {
        /// <summary>
        /// 指定选择的方法
        /// </summary>
        public abstract void SpecifyMethod(BlackboardMemory workingMemory);
    }
}
