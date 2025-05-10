using AI.Blackboard;
using AI.FiniteStateMachine;
using UnityEngine;
using Main;

namespace WWJ
{
    public class EatStarState : State
    {
        private BlackboardMemory memory;

        public EatStarState()
        {
            StateType = (int)TankState.EatStars; 
        }
        
        public override void Enter()
        {
            Tank myTank = (Tank)Agent;
            memory = myTank.GetComponent<MyTank>().workingMemory;
        }

        public override State Execute()
        {
            Tank myTank = (Tank)Agent;
            BlackboardMemory memory = myTank.GetComponent<MyTank>().workingMemory;
            Tank enemy = Match.instance.GetOppositeTank(myTank.Team);

            // 切换状态
            if (myTank.HP <= 50) // 没血了就回家
                return m_StateMachine.Transition((int)TankState.BackHome);
            if (IsEnemyVisible(myTank, enemy))
                return m_StateMachine.Transition((int)TankState.AttackEnemy);

            // 执行逻辑
            ExecuteLogic(myTank);
            return this;
        }

        private void ExecuteLogic(Tank myTank)
        {
            // 特定时间段设置预寻找超级星标志
            if (Match.instance.RemainingTime is < 95 and > 85)
            {
                memory.SetValue((int)TankFlag.preSuparStar, true);
            }
            else
            {
                memory.SetValue((int)TankFlag.preSuparStar, false);
            }

            if (memory.GetValue<bool>((int)TankFlag.preSuparStar))
                myTank.Move(Vector3.zero); // 预寻找超级星时逻辑

            // 寻找最近的星星（优先超级星）
            Vector3 nearestStarPos = Vector3.zero;
            float nearestDist = float.MaxValue;
            bool hasStar = false;
            foreach (var star in Match.instance.GetStars())
            {
                if (star.Value.IsSuperStar)
                {
                    memory.SetValue((int)TankFlag.preSuparStar, true);
                    nearestStarPos = star.Value.Position;
                    hasStar = true;
                    break;
                }
                else
                {
                    float dist = (star.Value.Position - myTank.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestStarPos = star.Value.Position;
                        hasStar = true;
                    }
                }
            }

            myTank.Move(hasStar ? nearestStarPos : Match.instance.GetRebornPos(myTank.Team)); // 移动至目标
        }

        // 具体检测逻辑
        private bool IsEnemyVisible(Tank myTank, Tank enemy)
        {
            if (enemy == null || enemy.IsDead)
                return false;

            #region 开火许可
            // 根据距离和碰撞检测决定是否开火
            if (Vector3.Distance(myTank.Position, enemy.Position) < 14)
            {
                return true;
            }
            if (Physics.SphereCast(myTank.FirePos, 0.23f, myTank.FirePos, out RaycastHit hit1, myTank.FirePos.magnitude))
            {
                if (!Tools.JudgeHitWall(hit1)) // 判断是否击中墙壁
                {
                    if (Vector3.Angle(myTank.TurretAiming, myTank.FirePos) < 15)
                        return true;
                }
            }
            else
            {
                if (Vector3.Distance(myTank.Position, enemy.Position) < 12)
                    return true;
                if (Vector3.Angle(myTank.TurretAiming, myTank.FirePos) < 15)
                    return true;
            }
            #endregion
            
            foreach (var missile in Match.instance.GetOppositeMissiles(myTank.Team))
            {
                if (Physics.SphereCast(missile.Value.Position, 0.5f, missile.Value.Velocity, out RaycastHit hit, 50))
                {
                    if (Tools.JudgeHitIsTank(hit, myTank)) // 判断导弹是否威胁自身
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}