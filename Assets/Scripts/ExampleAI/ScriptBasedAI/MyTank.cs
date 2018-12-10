using Main;
using AI.ScriptBased;
using UnityEngine;

namespace ScriptBasedAI
{
    class MoveTo : ScriptAction
    {
        private Vector3 m_TargetPos;
        public MoveTo(Vector3 targetPos)
        {
            m_TargetPos = targetPos;
        }
        public override bool Update()
        {
            Tank t = (Tank)Agent;
            if (Vector3.Distance(m_TargetPos, t.Position) < 1f)
            {
                return true;
            }
            t.Move(m_TargetPos);
            return false;
        }
    }
    class Fire : ScriptAction
    {
        public override bool Update()
        {
            Tank t = (Tank)Agent;
            t.Fire();
            return true;
        }
    }
    class WaitForSeconds : ScriptAction
    {
        private float m_WaitingDuration;
        private float m_ExpiredTime;
        public WaitForSeconds(float waitingDuration)
        {
            m_WaitingDuration = waitingDuration;
        }
        public override void Init()
        {
            m_ExpiredTime = Time.time + m_WaitingDuration;
        }
        public override bool Update()
        {
            return Time.time >= m_ExpiredTime;
        }
    }
    public class MyTank : Tank
    {
        private ScriptActionMachine m_Machine;
        protected override void OnStart()
        {
            base.OnStart();
            m_Machine = new ScriptActionMachine(this);
            m_Machine.AddAction(new MoveTo(Vector3.zero));
            m_Machine.AddAction(new WaitForSeconds(2f));
            m_Machine.AddAction(new Fire());
            m_Machine.AddAction(new MoveTo(Match.instance.GetRebornPos(Team)));
            m_Machine.AddAction(new WaitForSeconds(3f));
        }
        protected override void OnUpdate()
        {
            m_Machine.Update();
        }
        public override string GetName()
        {
            return "ScriptBasedAITank";
        }
    }
}
