using AI.FiniteStateMachine;
using Main;
using UnityEngine;

namespace CR
{
    enum EStateType
    {
        AvoidEnemy,FindStar, BackToHome
    }
    class AvoidEnemyState : State
    {
        public AvoidEnemyState()
        {
            StateType = (int)EStateType.AvoidEnemy;
        }
        public override State Execute()
        {
           
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            float angle = Vector3.Angle(oppTank.TurretAiming, t.Forward);
            Debug.Log(angle);
            if (angle < 30 || angle > 150)
            {

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
            if (hasStar == true)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(new Vector3(0,0,0));
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
            /*if (oppTank != null && oppTank.IsDead == false && t.HP > oppTank.HP)
            {
                return m_StateMachine.Transition((int)EStateType.FindEnemy);
            }*/
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
            //if no star
            if (hasStar == false)
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
            //m_FSM.AddState(new FindEnemyState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new FindStarState());
            m_FSM.SetDefaultState((int)EStateType.FindStar);
        }
        protected override void OnUpdate()
        {
            //fire check
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                TurretTurnTo(Aim(oppTank));
                Fire();
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            //state update
            m_FSM.Update();
        }

        public Vector3 Aim(Tank TargetTank, float AdvanceWeight = 0.85f)
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Vector3 AimPos;
            float distance = Vector3.Distance(Position, oppTank.Position);
            float missileFlyingTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            AimPos = TargetTank.Position + TargetTank.Forward + TargetTank.Velocity * missileFlyingTime * AdvanceWeight;
            return AimPos;
        }
        public override string GetName()
        {
            return "CR";
        }
    }
}
