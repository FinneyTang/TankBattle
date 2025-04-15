using AI.Base;
using System.Collections.Generic;

namespace AI.ScriptBased
{
    public class ScriptActionMachine
    {
        protected IAgent m_Agent;
        protected readonly Queue<ScriptAction> m_Actions = new Queue<ScriptAction>();
        protected ScriptAction m_CurrentAction = null;

        protected bool IsLoop
        {
            get; set;
        }
        public ScriptActionMachine(IAgent agent)
        {
            m_Agent = agent;
        }
        public void AddAction(ScriptAction action)
        {
            action.Agent = m_Agent;
            m_Actions.Enqueue(action);
        }
        public void Clear()
        {
            m_Actions.Clear();
            m_CurrentAction = null;
        }
        public void Update()
        {
            //check if having script actions
            if (m_CurrentAction == null && m_Actions.Count <= 0)
            {
                return;
            }
            //retrieve next action
            if (m_CurrentAction == null)
            {
                m_CurrentAction = m_Actions.Dequeue();
                m_CurrentAction.Init();
            }
            //update script
            bool isFinished = m_CurrentAction.Update();
            if (isFinished)
            {
                //loop
                if (IsLoop)
                {
                    m_Actions.Enqueue(m_CurrentAction);
                }
                m_CurrentAction = null;
            }
        }
        public override string ToString()
        {
            if (m_CurrentAction != null)
            {
                return m_CurrentAction.GetType().Name;
            }
            return string.Empty;
        }
    }
}
