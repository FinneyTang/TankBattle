using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;

namespace ZZH
{
    public enum EStateType { normal, superStar, lowHp};

    public class Normal : State
    {
        public Normal()
        {
            StateType = (int)EStateType.normal;
        }

        public override State Execute()
        {
            Info info = m_StateMachine.info;
            //check star, decide where to move
            if (!info.enemy.IsDead && (info.mytank.HP + info.match.GlobalSetting.DamagePerHit - 1) / info.match.GlobalSetting.DamagePerHit > (info.enemy.HP + info.match.GlobalSetting.DamagePerHit - 1) / info.match.GlobalSetting.DamagePerHit)
            {
                info.mytank.Move(info.mytank.CaculatePath(info.enemy.Position));
            }
            else if (info.closestStar == null)
            {
                if (info.mytank.HP >= 100)
                {
                    info.mytank.Move(info.mytank.CaculatePath((info.match.GetRebornPos(info.myteam) + info.match.GetRebornPos(info.enemy.Team)) / 2));
                }
                else
                {
                    return m_StateMachine.Transition((int)EStateType.lowHp);
                }
            }
            else
            {
                info.mytank.Move(info.mytank.CaculatePath(info.closestStar.Position));
            }
            if ((info.match.RemainingTime < 110 && info.match.RemainingTime > 100))
            {
                return m_StateMachine.Transition((int)EStateType.lowHp);
            }
            if ((info.match.RemainingTime < 100 && info.match.RemainingTime > 90) || info.superStarExist)
            {
                return m_StateMachine.Transition((int)EStateType.superStar);
            }
            if (info.mytank.HP <= 25 || (info.mytank.HP + info.match.GlobalSetting.DamagePerHit - 1) / info.match.GlobalSetting.DamagePerHit < (info.enemy.HP + info.match.GlobalSetting.DamagePerHit - 1) / info.match.GlobalSetting.DamagePerHit)
            {
                return m_StateMachine.Transition((int)EStateType.lowHp);
            }
            return m_StateMachine.Transition((int)EStateType.normal);
        }
    }

    public class SuperStar : State
    {
        public SuperStar()
        {
            StateType = (int)EStateType.superStar;
        }

        public override State Execute()
        {
            Info info = m_StateMachine.info;
            //move to super star
            if (!((info.match.RemainingTime < 100 && info.match.RemainingTime > 90) || info.superStarExist))
            {
                return m_StateMachine.Transition((int)EStateType.normal);
            }
            info.mytank.Move(info.mytank.CaculatePath((info.match.GetRebornPos(info.myteam) + info.match.GetRebornPos(info.enemy.Team)) / 2));
            return m_StateMachine.Transition((int)EStateType.superStar);
        }
    }

    public class LowHp : State
    {
        public LowHp()
        {
            StateType = (int)EStateType.lowHp;
        }

        public override State Execute()
        {
            Info info = m_StateMachine.info;
            if ((info.match.RemainingTime < 100 && info.match.RemainingTime > 90) || info.superStarExist)
            {
                return m_StateMachine.Transition((int)EStateType.superStar);
            }
            //move to home
            info.mytank.Move(info.mytank.CaculatePath(info.match.GetRebornPos(info.myteam)));
            //shoot
            if (info.mytank.HP >= info.match.GlobalSetting.MaxHP - info.match.GlobalSetting.DamagePerHit)
            {
                return m_StateMachine.Transition((int)EStateType.normal);
            }
            return m_StateMachine.Transition((int)EStateType.lowHp);
        }
    }

    public class State
    {
        public int StateType
        {
            get; protected set;
        }
        public Tank Agent
        {
            get; set;
        }
        protected StateMachine m_StateMachine;
        public void SetStateMachine(StateMachine m)
        {
            m_StateMachine = m;
        }
        public virtual void Enter() { }
        public virtual State Execute() { return null; }
        public virtual void Exit() { }
    }

    public class StateMachine
    {
        public Info info;
        private Tank m_Agent;
        private Dictionary<int, State> m_States = new Dictionary<int, State>();
        private State m_CurrentState;
        public StateMachine(Tank agent)
        {
            m_Agent = agent;
            info = new Info(m_Agent);
        }
        public void AddState(State s)
        {
            s.Agent = m_Agent;
            s.SetStateMachine(this);
            m_States[s.StateType] = s;
        }
        public State Transition(int t)
        {
            State s;
            m_States.TryGetValue(t, out s);
            return s;
        }
        public void SetDefaultState(int t)
        {
            if (m_States.TryGetValue(t, out m_CurrentState))
            {
                m_CurrentState.Enter();
            }
        }
        public void Update()
        {
            if (m_CurrentState == null)
            {
                return;
            }
            info.updateInfo();
            State nextState = m_CurrentState.Execute();
            Debug.Log("current state " + (EStateType)nextState.StateType);
            if (nextState != m_CurrentState)
            {
                m_CurrentState.Exit();
                m_CurrentState = nextState;
                if (m_CurrentState != null)
                {
                    m_CurrentState.Enter();
                }
            }
        }
    }
}

