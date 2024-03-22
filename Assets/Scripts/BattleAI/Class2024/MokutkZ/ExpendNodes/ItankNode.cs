using AI.BehaviourTree;

namespace MokutkZ
{
    interface ItankNode
    {
        public TankActionType TankActionType { get; set; }

        public abstract void Init();
    }

}