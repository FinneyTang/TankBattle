using Main;
using ZJQ;
using AI.Base;

public abstract class FiniteStateMachine : IAgent
{
    #region
    protected MyTank _obj;
    public abstract void enterState(MyTank obj);
    public abstract void action();
    public abstract void exitState(Tank enemey);
    #endregion
}
