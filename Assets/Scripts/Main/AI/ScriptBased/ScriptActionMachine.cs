using AI.Base;
using System.Collections.Generic;

namespace AI.ScriptBased
{
    public class ScriptActionMachine
    {
        private IAgent m_Agent;
        private Queue<ScriptAction> m_Actions = new Queue<ScriptAction>();
        private ScriptAction m_CurrentAction = null;
        public ScriptActionMachine(IAgent agent)
        {
            m_Agent = agent;
        }
        public void AddAction(ScriptAction action)
        {
            action.Agent = m_Agent;
            m_Actions.Enqueue(action);
        }
        public void Update()
        {
            //check if having script actions
            if (m_Actions.Count <= 0)
            {
                return;
            }
            //retrieve next action
            if (m_CurrentAction == null)
            {
                m_CurrentAction = m_Actions.Dequeue();
                m_CurrentAction.Init();
            }
            if (m_CurrentAction != null)
            {
                //update script
                bool isFinished = m_CurrentAction.Update();
                if (isFinished)
                {
                    //loop
                    m_Actions.Enqueue(m_CurrentAction);
                    m_CurrentAction = null;
                }
            }
        }
    }
}
