using System;
using System.Collections.Generic;
using Main;
using UnityEngine;
using AI.FiniteStateMachine;
using UnityEngine.UIElements;


namespace PYY
{
    enum TankState
    {
        Attack,
        FindStar,
        AwayMissiles,
        BackToHome
    }

    class BackToHomeState : State
    {
        public BackToHomeState()
        {
            StateType = (int)TankState.BackToHome;
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            bool oppTankIsDead = oppTank.IsDead;

            if (oppTankIsDead == true)
            {
                return m_StateMachine.Transition((int)TankState.FindStar);
            }
            else
            {
                t.Move(Match.instance.GetRebornPos(t.Team));
                if (t.HP >= Match.instance.GlobalSetting.MaxHP / 1.5f)
                {
                    return m_StateMachine.Transition((int)TankState.FindStar);
                }
            }


            return this;
        }
    }

    class AttackState : State
    {
        public AttackState()
        {
            StateType = (int)TankState.Attack;
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank myTanks = Match.instance.GetTank(t.Team);
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);

            myTanks.Move(oppTank.Position);
            foreach (var pStar in Match.instance.GetStars())
            {

                if (pStar.Value!=null)
                {
                    return m_StateMachine.Transition((int)TankState.FindStar);
                }
            }
            return this;
        }
    }

    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)TankState.FindStar;
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            Tank MyTank = Match.instance.GetTank(t.Team);
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            bool oppTankIsDead = oppTank.IsDead;
            bool superStarIsNull = true;
            bool findSuperStar = false;
  
            foreach (var pStar in Match.instance.GetStars())
            {
                Star s = pStar.Value;
                if (pStar.Value.IsSuperStar)
                {
                    superStarIsNull = false;
                }
                else
                {
                    superStarIsNull = true;
                }


                float dist = (s.Position - t.Position).magnitude;
                if (dist < nearestDist)
                {
                    hasStar = true;
                    nearestDist = dist;
                    nearestStar = s;
                }
            }
            
            if (Match.instance.RemainingTime <= (Match.instance.GlobalSetting.MatchTime / 2f) + 3f)
            {
                
                findSuperStar = true;
            }
            if (Match.instance.RemainingTime <= Match.instance.GlobalSetting.MatchTime / 2f & superStarIsNull)
            {
                
                findSuperStar = false;
            }


            if (MyTank.HP <= Match.instance.GlobalSetting.MaxHP / 2.1f & oppTankIsDead != true)
            {
                return m_StateMachine.Transition((int)TankState.BackToHome);
            }

            if (hasStar)
            {
                if (findSuperStar==true)
                {
                    t.Move(Vector3.zero);
                }
                else
                {
                    t.Move(nearestStar.Position);
                }
            }
            else
            {
                return m_StateMachine.Transition((int)TankState.Attack);
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
            m_FSM.AddState(new FindStarState());
            m_FSM.AddState(new AttackState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.SetDefaultState((int)TankState.FindStar);
            GameObject gameObject = this.gameObject;
            SphereCollider s = gameObject.AddComponent<SphereCollider>();
            s.radius = 20f;
            s.isTrigger = true;
        }

        protected override void OnUpdate()
        {
            
            Tank t = this;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            Vector3 oppTankPosiTion = oppTank.Position;

            TurretTurnTo(oppTankPosiTion);
            if (CanSeeOthers(oppTank))
            {
                Fire();
            }else
            {
                TurretTurnTo(oppTankPosiTion);
            }

            m_FSM.Update();
        }

        public override string GetName()
        {
            return "PYY";
        }
    }
}