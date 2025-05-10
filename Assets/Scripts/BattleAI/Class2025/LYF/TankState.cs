namespace LYF
{
    public abstract class TankState
    {
        protected readonly MyTank tank;

        protected TankState(MyTank tank)
        {
            this.tank = tank;
        }

        public virtual void OnEnter() { }
        public virtual void OnUpdate() { }
        public virtual void OnExit() { }
    }
}
