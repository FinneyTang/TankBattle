using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI.Base;
using Main;
namespace HZY
{
    //状态机类，包含状态机的初始化，状态转换，状态更新等方法
    public class StateMachine
    {
        private IAgent m_Agent;//AI主体
        private Dictionary<int, State> m_States = new Dictionary<int, State>();//状态字典，用于匹配状态
        private State m_CurrentState;//当前状态
        public StateMachine(IAgent agent)//构造函数，传入AI主体
        {
            m_Agent = agent;
        }
        public void AddState(State s)//添加状态，用于初始化状态机
        {
            s.Agent = m_Agent;
            s.SetStateMachine(this);
            m_States[s.StateType] = s;
        }
        public State Transition(int t)//状态转换，根据传入的状态值，返回对应的状态
        {
            m_States.TryGetValue(t, out var s);
            return s;
        }
        public void SetDefaultState(int t)//设置默认状态
        {
            if (m_States.TryGetValue(t, out m_CurrentState))
            {
                m_CurrentState.Enter();
            }
        }
        public void Update()//状态机更新
        {
            if (m_CurrentState == null)//如果当前状态为空，返回
            {
                return;
            }
            State nextState = m_CurrentState.Execute();//执行当前状态
            if (nextState != m_CurrentState)//如果下一个状态不等于当前状态，退出当前状态，进入下一个状态
            {
                m_CurrentState.Exit();//退出当前状态
                m_CurrentState = nextState;//当前状态等于下一个状态
                if (m_CurrentState != null)//如果当前状态不为空，进入当前状态
                {
                    m_CurrentState.Enter();//进入当前状态
                }
            }
        }
    }
    //状态父类，包含状态的进入，执行，退出等虚方法
    public class State
    {
        public int StateType//状态类型,
        {
            get; protected set;
        }
        public IAgent Agent //AI主体
        {
            get; set;
        }
        protected StateMachine m_StateMachine;//状态机类型
        public void SetStateMachine(StateMachine sm)//设置状态机
        {
            m_StateMachine = sm;
        }
        public virtual void Enter() { }//进入状态虚方法
        public virtual State Execute() { return null; }//执行状态虚方法，返回下一个应该进入的状态
        public virtual void Exit() { }//退出状态虚方法
    }
    //定义状态类型枚举
    public enum mStateType
    {
        FindEnemy, FindStar, BackToHome
    }
    //找敌人状态
    class FindEnemyState : State
    {
        public FindEnemyState()//构造函数
        {
            StateType = (int)mStateType.FindEnemy;//设置状态类型
        }
        public override State Execute()//重载Execute函数
        {
            Tank t = (Tank)Agent;//获取AI主体
            if (t.HP <= 20)//生命过回家
            {
                return m_StateMachine.Transition((int)mStateType.BackToHome);
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null || oppTank.IsDead)//敌人死亡就找星星
            {
                return m_StateMachine.Transition((int)mStateType.FindStar);
            }
            t.Move(oppTank.Position);
            return this;
        }
    }
    class BackToHomeState : State
    {
        public BackToHomeState()
        {
            StateType = (int)mStateType.BackToHome;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            if (t.HP >= 70)//生命值大于70则吃星星
            {
                return m_StateMachine.Transition((int)mStateType.FindStar);
            }
            t.Move(Match.instance.GetRebornPos(t.Team));//回家
            return this;
        }
    }
    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)mStateType.FindStar;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            // if (oppTank != null && oppTank.IsDead == false && t.HP > oppTank.HP)//如果AI生命值大于对方AI生命值
            // {
            //     return m_StateMachine.Transition((int)mStateType.FindEnemy);//找对方AI
            // }
            bool hasStar = false;//是否有星星
            float nearestDist = float.MaxValue;//最近距离
            Star nearestStar = null;//最近星星
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;//获取星星
                if (s.IsSuperStar)//如果是超级星星
                {
                    hasStar = true;//有星星
                    nearestStar = s;//最近设置为超级星星
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;//计算距离
                    if (dist < nearestDist)//如果距离小于最近距离
                    {
                        hasStar = true;//有星星
                        nearestDist = dist;//最近距离设置为当前距离
                        nearestStar = s;//最近星星设置为当前星星
                    }
                }
            }
            //if low hp and no super star
            if (t.HP <= 30 && (hasStar == false || nearestStar.IsSuperStar == false))//如果生命值小于30并且没有超级星星
            {
                return m_StateMachine.Transition((int)mStateType.BackToHome);//回家
            }
            if (hasStar == true)//如果有星星
            {
                t.Move(nearestStar.Position);//移动到星星
            }
            return this;
        }
    }

    //tank类
    public class MyTank : Tank
    {
        private StateMachine m_FSM;//声明状态机
        protected override void OnStart()
        {
            base.OnStart();
            m_FSM = new StateMachine(this);//初始化状态机
            //添加状态
            m_FSM.AddState(new FindEnemyState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new FindStarState());
            //设置默认状态
            m_FSM.SetDefaultState((int)mStateType.FindStar);
        }
        protected override void OnUpdate()
        {
            //开火逻辑
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                //如果敌人移动速度大于一定值，则应用预瞄准
                if (oppTank.Velocity.magnitude > 10)
                {
                    Vector3 offset = oppTank.Velocity * ((oppTank.Position - Position).magnitude / Match.instance.GlobalSetting.MissileSpeed);
                    TurretTurnTo(oppTank.Position + offset);
                    Fire();
                }
                else
                {
                    TurretTurnTo(oppTank.Position);
                    Fire();
                }
                // if (CanSeeOthers(oppTank))
                // {

                // }
            }
            else
            {
                TurretTurnTo(Match.instance.GetRebornPos(oppTank.Team));
            }

            //状态更新e
            m_FSM.Update();

            //躲闪逻辑，获取场上所有炮弹，计算炮弹飞行时间和自己的位置，进行预判躲避
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(oppTank.Team);
            //如果场上有敌人的子弹
            
            if (missiles.Count > 0)
            {
                foreach (var m in missiles)
                {
                    //如果子弹距离自己的距离小于一定值
                    if ((m.Value.Position - Position).magnitude < 5)
                    {
                        Vector3 toMissile = m.Value.Position - Position;//获取炮弹方向
                        //往炮弹的垂直方向移动
                        Move(Position + Vector3.Cross(toMissile, Vector3.up));
                        break;
                    }

                }
            }


        }
        public override string GetName()
        {
            return "HZY";
        }

    }

}
