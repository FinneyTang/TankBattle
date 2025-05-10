using System.Collections.Generic;
using AI.Blackboard;
using AI.FiniteStateMachine;
using UnityEngine;
using Main;

namespace WWJ
{
    public class AttackEnemyState : State
    {
        const float MissileSpeed = 40f; // 导弹速度
        private Queue<Vector3> TankSpeeds = new Queue<Vector3>(); // 记录敌方坦克速度的队列
        private Vector3 lastOppPos; // 敌方坦克上一帧的位置
        private int maxSteps = 3; // 最大记录速度的步数

        private Vector3 lastPos; // 本地记录的敌方位置
        private float lastTime; // 上一帧的时间戳

        private BlackboardMemory memory;
        
        public AttackEnemyState()
        {
            StateType = (int)TankState.AttackEnemy; 
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

            // 状态切换检查
            if ((enemy == null || enemy.IsDead) && myTank.HP <= 30)
                return m_StateMachine.Transition((int)TankState.BackHome);

            ExecuteLogic(myTank, enemy);
            
            if (!IsEnemyVisible(myTank, enemy))
            {
                return m_StateMachine.Transition((int)TankState.EatStars);
            }

            return this;
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
        
        private void ExecuteLogic(Tank myTank, Tank oppTank)
        {
            Transform turret = myTank.transform.GetChild(1).transform; // 获取炮塔Transform
            // 计算敌方速度并记录
            Vector3 v = (oppTank.transform.position - lastPos) / (Time.time - lastTime);
            lastPos = oppTank.transform.position;
            lastTime = Time.time;
            AddToTankSpeedLine(v);
            Vector3 oppSpeed = GetTankAVGSpeed(); // 计算平均速度

            // 预测弹道公式（基于二次方程求解）
            Vector3 firePosition = myTank.FirePos;
            Vector3 d = oppTank.Position - firePosition;
            float vp = MissileSpeed;
            float v0 = oppSpeed.magnitude;
            float cosp0 = Mathf.Cos(Vector3.Angle(-d, oppSpeed) * (Mathf.PI / 180));
            float a = v0 * v0 - vp * vp;
            float b = -2 * v0 * d.magnitude * cosp0;
            float c = d.sqrMagnitude;
            float delta = b * b - 4 * a * c;
            float predictedTime = (-b - Mathf.Sqrt(delta)) / (2 * a);
            Vector3 turnToForward = d + oppSpeed * predictedTime; // 预测目标点方向

            Debug.DrawRay(firePosition, turnToForward, Color.red); // 调试绘制预测弹道
            // Vector3 turnToPosition = firePosition + turnToForward;
            turnToForward = Vector3.ProjectOnPlane(turnToForward, Vector3.up); // 投影到水平面
            // turret.forward = Vector3.Lerp(turret.forward, turnToForward, Time.deltaTime * 180); // 平滑转向
            turret.forward = turnToForward;

            // 根据距离和碰撞检测决定是否开火
            if (Vector3.Distance(myTank.Position, oppTank.Position) < 20)
            {
                // Debug.Log("距离较近，无需判断直接射击");
                myTank.Fire();
            }
            else if (Physics.SphereCast(myTank.FirePos, 0.23f, turnToForward, out RaycastHit hit,
                         turnToForward.magnitude))
            {
                if (!Tools.JudgeHitWall(hit)) // 判断是否击中墙壁
                {
                    if (Vector3.Angle(myTank.TurretAiming, turnToForward) < 15)
                        myTank.Fire();
                }
            }
            else
            {
                if (Vector3.Distance(myTank.Position, oppTank.Position) < 12)
                    myTank.Fire();
                if (Vector3.Angle(myTank.TurretAiming, turnToForward) < 15)
                    myTank.Fire();
            }
            
            ExecuteDodgeBullets(myTank, oppTank);
        }

        // 记录敌方速度队列（限制最大步数）
        private void AddToTankSpeedLine(Vector3 speed)
        {
            if (TankSpeeds.Count >= maxSteps)
                TankSpeeds.Dequeue();
            TankSpeeds.Enqueue(speed);
        }

        // 计算平均速度
        private Vector3 GetTankAVGSpeed()
        {
            Vector3 speed = Vector3.zero;
            foreach (var v in TankSpeeds)
                speed += v;
            return speed / TankSpeeds.Count;
        }
        
        #region 躲子弹
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
        #endregion
    }
}