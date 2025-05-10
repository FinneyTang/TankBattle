using System.Collections.Generic;
using AI.Blackboard;
using AI.FiniteStateMachine;
using UnityEngine;
using Main;

namespace WWJ
{
    public class BackHomeState : State
    {
        private BlackboardMemory memory;
        
        const float MissileSpeed = 40f; // 导弹速度
        private Queue<Vector3> TankSpeeds = new Queue<Vector3>(); // 记录敌方坦克速度的队列
        private Vector3 lastOppPos; // 敌方坦克上一帧的位置
        //private int maxSteps = 3; // 最大记录速度的步数

        private Vector3 lastPos; // 本地记录的敌方位置
        private float lastTime; // 上一帧的时间戳
        
        public BackHomeState()
        {
            StateType = (int)TankState.BackHome; 
        }
        
        public override void Enter()
        {
            Tank myTank = (Tank)Agent;
            memory = myTank.GetComponent<MyTank>().workingMemory;
        }

        public override State Execute()
        {
            Tank myTank = (Tank)Agent;
            Tank enemy = Match.instance.GetOppositeTank(myTank.Team);

            // 特定时间段设置预寻找超级星标志
            if (Match.instance.RemainingTime is < 95 and > 85)
            {
                return m_StateMachine.Transition((int)TankState.EatStars);;
            }
            // 回够了就出去找星星
            if (memory.GetValue<bool>((int)TankFlag.InHome))
            {
                if (myTank.HP > 66)
                {
                    memory.SetValue((int)TankFlag.InHome, false);
                    return m_StateMachine.Transition((int)TankState.EatStars);
                }
            }

            GoHome(myTank, enemy);
            return this;
        }

        private void GoHome(Tank myTank, Tank enemyTank)
        {
            foreach (var item in Match.instance.GetOppositeMissiles(myTank.Team))
            {
                if (Physics.SphereCast(item.Value.Position, 0.5f, item.Value.Velocity, out RaycastHit hit, 50))
                {
                    if (Tools.JudgeHitIsTank(hit, myTank))
                    {
                        myTank.TurretTurnTo(item.Value.Position);
                        myTank.Fire();
                    }
                }
            }
            // 执行回血
            memory.SetValue((int)TankFlag.InHome, true);
            myTank.Move(Match.instance.GetRebornPos(myTank.Team)); // 移动至重生点
        }

        private void ExecuteDodgeBullets(Tank myTank,Tank enemyTank)
        {
            // 寻找超级星时跳过躲避
            if (memory.GetValue<bool>((int)TankFlag.preSuparStar)) return;
            // 躲避子弹
            foreach (var missile in Match.instance.GetOppositeMissiles(myTank.Team))
            {
                JudgeMissileCanHitCenter(myTank, missile.Value);
                PreJudgeMissileCanHit(myTank, missile.Value);
            }
        }
        
        // 实时检测导弹威胁
        private bool JudgeMissileCanHitCenter(Tank t, Missile missile)
        {
            if (Physics.SphereCast(missile.Position, 0.1f, missile.Velocity, out RaycastHit hit, 50))
            {
                if (Tools.JudgeHitIsTank(hit, t)) // 判断是否命中自己
                {
                    Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized; // 计算躲避方向
                    if (Vector3.Cross(missile.Velocity, t.Position - missile.Position).y > 0)
                        cross = -cross;
                    t.Move(t.Position + cross * 5); // 横向位移躲避
                    Debug.Log("切向躲避");
                    return true;
                }
            }
            return false;
        }
        
        // 预测导弹路径并提前躲避
        private bool PreJudgeMissileCanHit(Tank t, Missile missile)
        {
            // 计算导弹与坦克的平面投影路径
            Vector3 vm = Vector3.ProjectOnPlane(missile.Velocity, Vector3.up);
            Vector3 vt = Vector3.ProjectOnPlane(t.Velocity, Vector3.up);
            // 解直线方程求交点
            Tools.CalculationDualLinearEquation(
                vm.z / vm.x, -1, missile.Position.z - (vm.z / vm.x) * missile.Position.x,
                vt.z / vt.x, -1, t.Position.z - (vt.z / vt.x) * t.Position.x,
                out float x, out float z
            );
            Vector3 predictedPos = new Vector3(x, 0, z);
            // 检测路径是否有障碍物
            if (Physics.SphereCast(missile.Position, 0.2f, missile.Velocity, out RaycastHit hit, (predictedPos - missile.Position).magnitude))
            {
                if (Tools.JudgeHitWall(hit)) return false;
            }
            // 计算碰撞时间并决定躲避方向
            Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
            if (Vector3.Cross(missile.Velocity, t.Position - missile.Position).y > 0)
                cross = -cross;
            t.Move(t.Position + cross * 5); // 横向位移
            // Debug.Log("预测急停规避子弹");
            return true;
        }
    }
}