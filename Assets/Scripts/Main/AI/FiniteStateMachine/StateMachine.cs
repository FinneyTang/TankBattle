using AI.Base;
using System.Collections.Generic;

namespace AI.FiniteStateMachine
{
    public class StateMachine
    {
        private IAgent m_Agent;
        private Dictionary<int, State> m_States = new Dictionary<int, State>();
        private State m_CurrentState;
        public StateMachine(IAgent agent)
        {
            m_Agent = agent;
        }
        public void AddState(State s)
        {
            s.Agent = m_Agent;
            s.SetStateMachine(this);
            m_States[s.StateType] = s;
        }
        public State Transition(int t)
        {
            m_States.TryGetValue(t, out var s);
            return s;
        }
        public void SetDefaultState(int t)
        {
            if (m_States.TryGetValue(t, out m_CurrentState))
            {
                m_CurrentState.Enter();
            }
        }
        public void Update()
        {
            if (m_CurrentState == null)
            {
                return;
            }
            State nextState = m_CurrentState.Execute();
            if (nextState != m_CurrentState)
            {
                m_CurrentState.Exit();
                m_CurrentState = nextState;
                if (m_CurrentState != null)
                {
                    m_CurrentState.Enter();
                }
            }
        }
    }
}
