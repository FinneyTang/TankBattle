namespace LYF
{
    public class RetreatState : TankState
    {
        public RetreatState(MyTank tank) : base(tank) { }

        private float hpOnEnter;

        public override void OnEnter()
        {
            hpOnEnter = tank.HP;
            tank.Move(tank.RebornPos);
        }

        public override void OnUpdate()
        {
            if (hpOnEnter > 50)
            {
                if (tank.HP > hpOnEnter || tank.HP == 100)
                    tank.ChangeState(new CollectStarsState(tank));
            }
            else
            {
                if (tank.HP > 50 || tank.HP == 100)
                    tank.ChangeState(new CollectStarsState(tank));
            }
        }
    }
}