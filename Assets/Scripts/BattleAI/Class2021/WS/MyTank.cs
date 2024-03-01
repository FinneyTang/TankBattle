using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using AI.FiniteStateMachine;
using UnityEditor;

namespace WS
{
    class GlobalValue
    {
        private static GlobalValue instance;
        public float MinStarDis = 20f;
        public int RemainTimeWillHalf = 100;
        public int RemainPlus = 5;
        public string state = "FindStar";
        public bool WillSuperStar = true;
        private GlobalValue()
        {
        }
        public static GlobalValue GetInstance()
        {
            if (instance == null)
            {
                instance = new GlobalValue();
            }
            return instance;
        }
    }
    enum TankStateType
    {
        FindStar, FindNearStar, BackHome, ToCenter, FindEnemy
    }
    class FindEnemy : State
    {
        public int RemainTimeWillHalf = GlobalValue.GetInstance().RemainTimeWillHalf;
        public int RemainPlus = GlobalValue.GetInstance().RemainPlus;
        public FindEnemy()
        {
            StateType = (int)TankStateType.FindEnemy;
        }
        public override State Execute()
        {
            float MinStarDis = GlobalValue.GetInstance().MinStarDis;
            Tank t = (Tank)Agent;

            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    //hasStar = true;
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        //hasStar = true;
                        nearestDist = dist;
                        nearestStar = s;
                    }
                }
            }
            if (nearestDist < MinStarDis * MinStarDis)
            {
                GlobalValue.GetInstance().state = "FindNearStar";
                return m_StateMachine.Transition((int)TankStateType.FindNearStar);
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null || oppTank.IsDead || oppTank.HP >= t.HP)
            {

                GlobalValue.GetInstance().state = "FindStar";
                return m_StateMachine.Transition((int)TankStateType.FindStar);
            }
            if (Match.instance.RemainingTime < RemainTimeWillHalf - RemainPlus && GlobalValue.GetInstance().WillSuperStar)
            {
                GlobalValue.GetInstance().state = "ToCenter";
                return m_StateMachine.Transition((int)TankStateType.ToCenter);
            }
            t.Move(oppTank.Position);
            return this;
        }
    }
    class FindStar : State
    {
        public int RemainTimeWillHalf = GlobalValue.GetInstance().RemainTimeWillHalf;
        public int RemainPlus = GlobalValue.GetInstance().RemainPlus;
        public FindStar()
        {
            StateType = (int)TankStateType.FindStar;
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
                t.Move(nearestStar.Position);
            }
            else
            {
                float halfSize = Match.instance.FieldSize * 0.5f;
                Vector3 RandomPos = new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
                t.Move(RandomPos);
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false && t.HP > oppTank.HP)
            {
                GlobalValue.GetInstance().state = "FindEnemy";
                return m_StateMachine.Transition((int)TankStateType.FindEnemy);
            }
            if (t.HP <= 30 || (t.HP <= 60 && Match.instance.RemainingTime < RemainTimeWillHalf))
            {
                GlobalValue.GetInstance().state = "BackHome";
                return m_StateMachine.Transition((int)TankStateType.BackHome);
            }

            if (Match.instance.RemainingTime < RemainTimeWillHalf - RemainPlus && GlobalValue.GetInstance().WillSuperStar)
            {
                GlobalValue.GetInstance().state = "ToCenter";
                return m_StateMachine.Transition((int)TankStateType.ToCenter);
            }
            return this;
        }
    }
    class BackHome : State
    {
        public BackHome()
        {
            StateType = (int)TankStateType.BackHome;
        }
        public override State Execute()
        {
            float MinStarDis = GlobalValue.GetInstance().MinStarDis;
            Tank t = (Tank)Agent;

            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    //hasStar = true;
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        //hasStar = true;
                        nearestDist = dist;
                        nearestStar = s;
                    }
                }
            }

            if (nearestDist < MinStarDis * MinStarDis)
            {
                GlobalValue.GetInstance().state = "FindNearStar";
                return m_StateMachine.Transition((int)TankStateType.FindNearStar);
            }
            if (t.HP >= 70)
            {
                GlobalValue.GetInstance().state = "FindStar";
                return m_StateMachine.Transition((int)TankStateType.FindStar);
            }
            t.Move(Match.instance.GetRebornPos(t.Team));
            return this;
        }
    }
    class FindNearStar : State
    {
        public float MinStarDis = GlobalValue.GetInstance().MinStarDis;
        public FindNearStar()
        {
            StateType = (int)TankStateType.FindNearStar;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;

            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    //hasStar = true;
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        //hasStar = true;
                        nearestDist = dist;
                        nearestStar = s;
                    }
                }
            }
            if (nearestDist < MinStarDis * MinStarDis)
            {
                t.Move(nearestStar.Position);
            }
            if (nearestDist > MinStarDis * MinStarDis)
            {
                if (t.HP < 30)
                {
                    GlobalValue.GetInstance().state = "BackHome";
                    return m_StateMachine.Transition((int)TankStateType.BackHome);
                }
                else
                {
                    GlobalValue.GetInstance().state = "FindEnemy";
                    return m_StateMachine.Transition((int)TankStateType.FindEnemy);
                }
            }
            return this;
        }
    }
    class ToCenter : State
    {
        public ToCenter()
        {
            StateType = (int)TankStateType.ToCenter;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            t.Move(new Vector3(0, 0.5f, 0));
            bool nothasSuperStar = true;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    nothasSuperStar = false;
                    break;
                }
            }
            if (Match.instance.RemainingTime < 90 && nothasSuperStar)
            {
                GlobalValue.GetInstance().state = "FindStar";
                GlobalValue.GetInstance().WillSuperStar = false;
                return m_StateMachine.Transition((int)TankStateType.FindStar);
            }
            return this;
        }
    }
    class MyTank : Tank
    {
        public float MinStarDis = GlobalValue.GetInstance().MinStarDis;
        private StateMachine m_FSM;
        protected override void OnStart()
        {
            base.OnStart();
            m_FSM = new StateMachine(this);
            m_FSM.AddState(new FindStar());
            m_FSM.AddState(new BackHome());
            m_FSM.AddState(new FindNearStar());
            m_FSM.AddState(new ToCenter());
            m_FSM.AddState(new FindEnemy());
            m_FSM.SetDefaultState((int)TankStateType.FindStar);
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
                if (CanSeeOthers(Match.instance.GetRebornPos(oppTank.Team)))
                {
                    TurretTurnTo(Match.instance.GetRebornPos(oppTank.Team));
                    Fire();
                }
            }
            //state update
            m_FSM.Update();
        }

#if UNITY_EDITOR
        private GUIStyle m_State;
#endif
        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            Gizmos.DrawWireSphere(Position, MinStarDis);
#if UNITY_EDITOR
            if (m_State == null)
            {
                m_State = new GUIStyle();
                m_State.normal.textColor = Color.yellow;
                m_State.fontSize = 16;
                m_State.fontStyle = FontStyle.Bold;
            }
            string score = "state:"+GlobalValue.GetInstance().state + "  MinStarDis"+ GlobalValue.GetInstance().MinStarDis;
            Handles.Label(Position + Forward * 5, score, m_State);
#endif
        }
        public override string GetName()
        {
            return "WS";
        }
    }
}
