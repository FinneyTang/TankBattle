using AI.Base;

namespace AI.ScriptBased
{
    public class ScriptAction
    {
        public IAgent Agent
        {
            get; set;
        }
        public ScriptAction()
        {
        }
        public virtual void Init()
        {
        }
        public virtual bool Update()
        {
            return true;
        }
    }
}