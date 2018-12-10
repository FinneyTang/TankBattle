using AI.Base;

namespace AI.FiniteStateMachine
{
    public class State
    {
        public int StateType
        {
            get; protected set;
        }
        public IAgent Agent
        {
            get; set;
        }
        protected StateMachine m_StateMachine;
        public void SetStateMachine(StateMachine m)
        {
            m_StateMachine = m;
        }
        public virtual void Enter() { }
        public virtual State Execute() { return null; }
        public virtual void Exit() { }
    }
}
