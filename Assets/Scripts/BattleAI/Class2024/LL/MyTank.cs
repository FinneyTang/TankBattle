using AI.Base;
using AI.FiniteStateMachine; // 导入有限状态机模块
using Main; // 导入主要模块
using System.Linq;
using THL;
using UnityEngine;

namespace LL
{
    // 定义状态类型的枚举
    enum EStateType
    {
        FindEnemy, // 寻找敌人状态
        FindStar, // 寻找星星状态
        BackToHome,// 返回基地状态
        GoToCenter//占领中心状态
    }

    // 寻找敌人状态类
    class FindEnemyState : State
    {
        public FindEnemyState()
        {
            StateType = (int)EStateType.FindEnemy; // 设置状态类型为寻找敌人

        }

        public override State Execute()
        {
            //Debug.Log("李浪正在寻找敌人");
            Tank t = (Tank)Agent; // 获取当前状态对应的坦克实例
            if (t.HP <= 30) // 如果坦克的生命值小于等于30
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome); // 切换到返回基地状态
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team); // 获取敌方坦克实例
            if (oppTank == null || oppTank.IsDead) // 如果敌方坦克为空或已经死亡
            {
                return m_StateMachine.Transition((int)EStateType.FindStar); // 切换到寻找星星状态
            }
            t.Move(oppTank.Position);


            return this;
        }
    }



    // 返回基地状态类
    class BackToHomeState : State
    {
        public BackToHomeState()
        {
            StateType = (int)EStateType.BackToHome; // 设置状态类型为返回基地

        }

        public override State Execute()
        {
            //Debug.Log("李浪正在回家");
            Tank t = (Tank)Agent; // 获取当前状态对应的坦克实例
            if (t.HP >= 80) // 如果坦克的生命值大于等于80
            {
                return m_StateMachine.Transition((int)EStateType.FindStar); // 切换到寻找星星状态
            }
            t.Move(Match.instance.GetRebornPos(t.Team)); // 移动到基地的位置
            Star nearestStar = null; // 最近的星星实例
            foreach (var pair in Match.instance.GetStars()) // 遍历所有星星
            {

                Star s = pair.Value; // 获取星星实例
                if (s.IsSuperStar) // 如果星星是超级星星
                {
                    nearestStar = s; // 更新最近星星实例
                    return m_StateMachine.Transition((int)EStateType.FindStar); // 切换到寻找星星状态
                    
                }
                else
                {

                    float dist = (s.Position - t.Position).sqrMagnitude; // 计算当前星星与坦克之间的距离的平方
                    if ( dist < 600) // 如果距离小于最近距离
                    {
                        t.Move(s.Position);
                    }
                }
            }
            return this;
        }
    }

    // 寻找星星状态类
    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)EStateType.FindStar; // 设置状态类型为寻找星星
            
        }

        public override State Execute()
        {

            //Debug.Log("李浪正在找星星");
            Tank t = (Tank)Agent; // 获取当前状态对应的坦克实例
            Tank oppTank = Match.instance.GetOppositeTank(t.Team); // 获取敌方坦克实例

            if (oppTank != null && oppTank.IsDead == false &&  oppTank.HP<=40) // 如果敌方坦克存在且未死亡，
            {
   
                return m_StateMachine.Transition((int)EStateType.FindEnemy); // 切换到寻找敌人状态
            }
            bool hasStar = false; // 是否有星星
            float nearestDist = float.MaxValue; // 最近星星的距离
            Star nearestStar = null; // 最近的星星实例
            foreach (var pair in Match.instance.GetStars()) // 遍历所有星星
            {
                Star s = pair.Value; // 获取星星实例
                if (s.IsSuperStar) // 如果星星是超级星星
                {
                    hasStar = true; // 标记有星星
                    nearestStar = s; // 更新最近星星实例
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude; // 计算当前星星与坦克之间的距离的平方
                    if (dist < nearestDist) // 如果距离小于最近距离
                    {
                        hasStar = true; // 标记有星星
                        nearestDist = dist; // 更新最近距离
                        nearestStar = s; // 更新最近星星实例
                    }
                }
            }

            // 如果生命值低且没有超级星星
            if (t.HP <= 40 && nearestStar.IsSuperStar == false)
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome); // 切换到返回基地状态
            }
            else if (t.HP>40&&hasStar==false)
            {
                t.Move(new Vector3(0, 0, 0));
            }

            if (hasStar == true) // 如果有星星
            {
                t.Move(nearestStar.Position); // 移动到最近星星的位置
            }
           

            return this;
        }
    }


    // 自定义坦克类
    public class MyTank : Tank
    {
        private StateMachine m_FSM; // 状态机实例

        protected override void OnStart()
        {
            base.OnStart();
            m_FSM = new StateMachine(this); // 创建状态机，传入当前坦克实例
            m_FSM.AddState(new FindEnemyState()); // 添加寻找敌人状态
            m_FSM.AddState(new BackToHomeState()); // 添加返回基地状态
            m_FSM.AddState(new FindStarState()); // 添加寻找星星状态
           // m_FSM.AddState(new GoToCenterState()); // 添加去中间状态
            m_FSM.SetDefaultState((int)EStateType.FindStar); // 设置默认状态为寻找星星
            
        }
        Vector3 predictedPos(Tank tank)
        {
            if (tank.IsDead)
            {
                return Match.instance.GetRebornPos(tank.Team);
            }
            float distance = Vector3.Distance(tank.Position, Match.instance.GetOppositeTank(tank.Team).Position);
            float pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            Vector3 pPos = tank.Position + tank.Velocity * pTime;
            for (int i = 0; i < 2; i++)
            {
                distance = Vector3.Distance(Match.instance.GetOppositeTank(tank.Team).Position, pPos);
                pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
                pPos = tank.Position + tank.Velocity * pTime;
            }
            return pPos;
        }

        protected override void OnUpdate()
        {

            // 开火检查
            Tank oppTank = Match.instance.GetOppositeTank(Team); // 获取敌方坦克实例
            if (oppTank != null && oppTank.IsDead == false) // 如果敌方坦克存在且未死亡
            {
                TurretTurnTo(predictedPos(oppTank));// 炮塔对准开火方向
                if (CanSeeOthers(oppTank))
                {
                    
                    Fire();
                }
              
            }
            else
            {
                TurretTurnTo(Position + Forward); // 炮塔对准前方
            }



            // 状态更新
            m_FSM.Update();

        }

        public override string GetName()
        {
            return "LL";
        }
    }
}