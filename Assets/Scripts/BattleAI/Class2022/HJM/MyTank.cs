using AI.FiniteStateMachine;
using Main;
using UnityEngine;

namespace HJM
{
    enum EStateType
    {
        FindEnemy, FindStar, BackToHome
    }
    class FindEnemyState : State
    {
        public FindEnemyState()
        {
            StateType = (int)EStateType.FindEnemy;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            float ophdist = Vector3.Distance(new Vector3(-40, 0, 40), t.Position);
            if (t.HP <= 50 || ophdist < 40)
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            float oppdist = Vector3.Distance(oppTank.Position, t.Position);
            
            if (oppdist > 40 && (oppTank == null || oppTank.IsDead))
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(oppTank.Position);
            return this;
        }
    }
    class BackToHomeState : State
    {
        public BackToHomeState()
        {
            StateType = (int)EStateType.BackToHome;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            if (t.HP >= 70)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(Match.instance.GetRebornPos(t.Team));
            return this;
        }
    }
    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)EStateType.FindStar;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            float oppdist = Vector3.Distance(oppTank.Position, t.Position);
            if (oppTank != null && oppTank.IsDead == false && t.HP > oppTank.HP && oppdist <= 40)
            {
                return m_StateMachine.Transition((int)EStateType.FindEnemy);
            }
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStar = s;
                    }
                }
            }
            //if low hp and no super star
            if (t.HP <= 50 && (hasStar == false || nearestStar.IsSuperStar == false))
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            if (hasStar == true)
            {
                t.Move(nearestStar.Position);
            }
            return this;
        }
    }

    public class MyTank : Tank
    {
        private StateMachine m_HJM;
        protected override void OnStart()
        {
            base.OnStart();
            m_HJM = new StateMachine(this);
            m_HJM.AddState(new FindEnemyState());
            m_HJM.AddState(new BackToHomeState());
            m_HJM.AddState(new FindStarState());
            m_HJM.SetDefaultState((int)EStateType.FindStar);
        }
        protected override void OnUpdate()
        {
            //fire check
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                TurretTurnTo(oppTank.Position);
                Vector3 toTarget = oppTank.Position - FirePos;
                toTarget.y = 0;
                toTarget.Normalize();
                if (Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            //state update
            m_HJM.Update();
        }
        public override string GetName()
        {
            return "HJM";
        }
    }
}