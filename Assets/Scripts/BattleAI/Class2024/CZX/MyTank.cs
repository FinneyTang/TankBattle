using AI.BehaviourTree;
using AI.Blackboard;
using Main;

namespace CZX
{
    public class MyTank : Tank
    {
        private BlackboardMemory _memory;
        private Node             _node;

        protected override void OnStart()
        {
            base.OnStart();
            _memory = new BlackboardMemory();
            _node = new ParallelNode(1).AddChild(
                new ParallelNode(1).AddChild(
                    new TurnTurret(),
                    new AttackEnemy()
                ),
                new SelectorNode().AddChild(
                    new GoHome(),
                    new SelectorNode().AddChild(
                        new StopMove(),
                        new MoveTo()
                    )
                )
            );
        }

        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(_node, this, _memory);
        }

        public override string GetName()
        {
            return "CZX";
        }
    }
}