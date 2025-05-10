using AI.BehaviourTree;
using AI.FiniteStateMachine;
using AI.Blackboard;
using Main;


namespace WWJ
{
    // 定义坦克状态标志位
    public enum TankFlag
    {
        InHome = 1, // 是否在基地
        preSuparStar = 2, // 是否正在寻找超级星
    }

    public enum TankState
    {
        AttackEnemy,
        EatStars,
        BackHome
    }

    public class MyTank : Tank
    {
        public BlackboardMemory workingMemory; // 黑板内存（共享数据）
        private Node m_BTNode;                    // 行为树根节点
        private StateMachine _machine;

        protected override void OnStart()
        {
            base.OnStart();
            workingMemory = new BlackboardMemory();
            workingMemory.SetValue((int)TankFlag.preSuparStar, false);
            workingMemory.SetValue((int)TankFlag.InHome, false);

            _machine = new StateMachine(this);
            _machine.AddState(new AttackEnemyState());
            _machine.AddState(new EatStarState());
            _machine.AddState(new BackHomeState());
            _machine.SetDefaultState((int)TankState.EatStars);
        }

        protected override void OnUpdate()
        {
            _machine.Update();
        }

        public override string GetName() => "ShiMingZi"; // 坦克名称
    }
}