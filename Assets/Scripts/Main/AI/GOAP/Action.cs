using System.Collections.Generic;
using AI.Base;
using AI.ScriptBased;

namespace AI.GOAP
{
    public abstract class GOAPAction : ScriptAction
    {
        private readonly WorldState m_Preconditions = new WorldState();
        public WorldState Preconditions => m_Preconditions;
        
        private readonly WorldState m_Effects = new WorldState();
        public WorldState Effects => m_Effects;

        public float Cost
        {
            get;
            set;
        } = 1;
        
        //helper methods for preconditions and effects
        protected void AddPrecondition(string key, bool value)
        {
            m_Preconditions.SetState(key, value);
        }
        protected void AddEffect(string key, bool value)
        {
            m_Effects.SetState(key, value);
        }
    }

    public class GOAPActionMachine : ScriptActionMachine
    {
        public bool IsRunning => m_Actions.Count > 0 || m_CurrentAction != null;
        
        public GOAPActionMachine(IAgent agent) : base(agent)
        {
            IsLoop = false;
        }
        
        public void AddActionList(List<GOAPAction> actionList)
        {
            Clear();
            foreach (var action in actionList)
            {
                AddAction(action);
            }
        }
    }
}