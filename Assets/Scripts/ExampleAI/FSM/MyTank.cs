using AI.FiniteStateMachine;
using Main;
using UnityEngine;

namespace FSM
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
            if(t.HP <= 30)
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if(oppTank == null || oppTank.IsDead)
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
            if (oppTank != null && oppTank.IsDead == false && t.HP > oppTank.HP)
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
            if (t.HP <= 30 && (hasStar == false || nearestStar.IsSuperStar == false))
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
        private StateMachine m_FSM;
        protected override void OnStart()
        {
            base.OnStart();
            m_FSM = new StateMachine(this);
            m_FSM.AddState(new FindEnemyState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new FindStarState());
            m_FSM.SetDefaultState((int)EStateType.FindStar);
        }
        protected override void OnUpdate()
        {
            //fire check
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null)
            {
                TurretTurnTo(oppTank.Position);
                if(CanSeeOthers(oppTank))
                {
                    Fire();
                }
            }
            //state update
            m_FSM.Update();
        }
        public override string GetName()
        {
            return "FSMTank";
        }
    }
}
