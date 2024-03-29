using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI.FiniteStateMachine;
using Main;

namespace ZTX
{
    
    enum EStateType
    {
        FindEnemy, FindStar, BackToHome,Dodge
    }
    class DodgeState : State
    {
        public DodgeState()
        {
            StateType = (int)EStateType.Dodge;
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);

            //Handle Transitioning
            if (oppTank == null || oppTank.IsDead || !t.CanSeeOthers(oppTank))
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }

            //Handle Behavior
            var missiles = Match.instance.GetOppositeMissiles(t.Team);

            //Find Nearest missile
            Missile nearestMissile = null;
            float minDis = Mathf.Infinity;
            foreach (var missile in missiles)
            {
                float Dis = Vector3.Distance(missile.Value.Position, t.Position);
                if (minDis >= Dis)
                {
                    minDis = Dis;
                    nearestMissile = missile.Value;
                }
            }

            Vector3 nextPos = Vector3.zero;
            //Calculate Next Position of the Tank
            if (nearestMissile != null)
            {
                Vector3 dir1 = Quaternion.AngleAxis(90, Vector3.up) * nearestMissile.Velocity.normalized;
                Vector3 dir2 = Quaternion.AngleAxis(-90, Vector3.up) * nearestMissile.Velocity.normalized;

                if (Vector3.Dot(t.Velocity.normalized, dir1) >= 0.0f)
                {
                    nextPos = t.Position + dir1 * 10.0f;
                }
                else if (Vector3.Dot(t.Velocity.normalized, dir2) >= 0.0f)
                {
                    nextPos = t.Position + dir2 * 10.0f;
                }

                t.Move(nextPos);
                return this;
            }
            return this;
        }
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
            if (t.HP <= 25)
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null || oppTank.IsDead)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            if (t.CanSeeOthers(oppTank.Position))
            {
                return m_StateMachine.Transition((int)EStateType.Dodge);
            }
            t.Move(oppTank.NextDestination);
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
            if (t.HP >= 76)//76Ѫ������Ѫ
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
            if (oppTank != null && oppTank.IsDead == false && t.HP > oppTank.HP||t.CanSeeOthers(oppTank))
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
            if (t.HP <= 25 && (hasStar == false || nearestStar.IsSuperStar == false))
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            if (hasStar == true)
            {
                t.Move(nearestStar.Position);
            }
            else
            {
                t.Move(oppTank.Position);
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
            m_FSM.AddState(new DodgeState());
            m_FSM.SetDefaultState((int)EStateType.FindStar);
        }
        protected override void OnUpdate()
        {
            //fire check
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                TurretTurnTo(oppTank.Position);
                if (CanSeeOthers(oppTank))
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            //state update
            m_FSM.Update();
        }
        public override string GetName()
        {
            return "ZTXTank";
        }
    }

}
