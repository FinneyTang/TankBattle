using System.Collections;
using System.Collections.Generic;
using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
//using HZR;
using UnityEngine;
using Main;
using YJY;

namespace YJY
{
    public enum TankFlag
    {
        InHome=1,
        preSuparStar=2,
    }
    public class TurnToEnemy : ActionNode
    {
        private const float MissileRadius = 0.5f;
        const float MissileSpeed = 40f;
        private Queue<Vector3> TankSpeeds = new Queue<Vector3>();//存储对方坦克的方向
        private Vector3 lastOppPos;
        private int maxSteps = 4;//旧：3//优化思路：已经高频获得敌人的方向了减少计算提高命中率

        private Vector3 lastPos;
        private float lastTime;
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)//复写决策层传达决策函数
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false)//这里是敌军坦克没有死亡的时候
            {
                Transform turret = t.transform.GetChild(1).transform;
                Vector3 v = (oppTank.transform.position - lastPos) / (Time.time - lastTime);//敌军坦克方向
                lastPos = oppTank.transform.position;
                lastTime = Time.time;
                AddToTankSpeedLine(v);//传入敌军坦克方向参数计算
                Vector3 oppSpeed = GetTankAVGSpeed();//得到敌军的平均速度用来计算开火方向，预存n三个方向
                Vector3 firePosition = t.FirePos;
                Vector3 d = oppTank.Position - firePosition;
                float vp = MissileSpeed;
                float v0 = oppSpeed.magnitude;
                //优化思路，预测的时候假设对方坦克在指定方向上不是走满单位时间的
                v0 *= 0.92f;
                float cosp0 = Mathf.Cos(Vector3.Angle(-d, oppSpeed) * (Mathf.PI / 180));
                float a = v0 * v0 - vp * vp;
                float b = -2 * v0 * d.magnitude * cosp0;
                float c = d.sqrMagnitude;
                float delta = b * b - 4 * a * c;
                float predictedTime = (-b - Mathf.Sqrt(delta)) / (2 * a);
                Vector3 turnToForward = d + oppSpeed * predictedTime;
                Debug.DrawRay(firePosition, turnToForward, Color.red);
                Vector3 turnToPosition = firePosition + turnToForward;
                turnToForward = Vector3.ProjectOnPlane(turnToForward, Vector3.up);
                turret.forward = Vector3.Lerp(turret.forward, turnToForward, Time.deltaTime * 180);
                if (Vector3.Distance(t.Position, oppTank.Position) < 16)//判断与地方坦克的距离//旧：<14//优化思路：找到性价比最高的极限距离
                {//现有脚本可能会导致距离近的时候不进行走位了，可以考虑更改一下走位逻辑，开火距离可以增大
                    Debug.Log("距离较近，无需判断直接射击Y");
                    t.Fire();
                }else if (Physics.SphereCast(t.FirePos, 0.23f, turnToForward, out RaycastHit hit,
                              turnToForward.magnitude))
                {
                    if (!MyTool.JudgeHitWall(hit))
                    {
                        if (Vector3.Angle(t.TurretAiming, turnToForward) < 15)//
                            t.Fire();
                    }
                }
                else
                {
                    //旧 if (Vector3.Distance(t.Position, oppTank.Position) < 12)
                    if (Vector3.Distance(t.Position, oppTank.Position) < 20)//试试远距离狙击，对有走位功能的脚本很有干扰力
                    {
                        t.Fire();
                    }
                    if (Vector3.Angle(t.TurretAiming, turnToForward) < 25)//旧<15,
                        t.Fire();
                }
            }
            else
            {
                t.TurretTurnTo(t.Position + t.Forward);//如果没有发现地方坦克，坦克会调整炮塔的方向朝向前方。
            }
            return ERunningStatus.Executing;
        }

        private void AddToTankSpeedLine(Vector3 speed)//决策计算用函数
        {
            if (TankSpeeds.Count >= maxSteps)
            {
                TankSpeeds.Dequeue();//弹出旧的元素
            }
            TankSpeeds.Enqueue(speed);//加入新元素
        }
        /// <summary>
        /// 获取坦克平均速度
        /// </summary>
        /// <returns></returns>
        private Vector3 GetTankAVGSpeed()//决策计算用函数
        {
            Vector3 speed = Vector3.zero;
            foreach (var VARIABLE in TankSpeeds)
            {
                speed += VARIABLE;
            }
            return speed / TankSpeeds.Count;
        }
    }

    public class MoveBack : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)//返回是否去基地的正确值//复写决策层传达决策函数
        {
            Tank t = (Tank) agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (Match.instance.RemainingTime < 95 && Match.instance.RemainingTime > 85)
            {
                workingMemory.SetValue((int) TankFlag.preSuparStar, true);//要抢星星了
                return false;
            }
            if (Match.instance.RemainingTime < 105 && Match.instance.RemainingTime > 95&&t.HP<=80)//战斗前专门补给一次
            {
                return true;
            }
            if(Match.instance.RemainingTime < 85)
            {
                workingMemory.SetValue((int)TankFlag.preSuparStar, false);
            }
                //旧   if (oppTank.IsDead && t.HP <= 50)//返回基地的条件
                //新   
                if (oppTank.IsDead && t.HP <= 41)//优化思路：敌人死亡应该增大搜刮星星的机会
            {
                Debug.Log("敌人死亡，回基地Y");
                return true;
            }

            if (workingMemory.HasValue((int) TankFlag.InHome) && workingMemory.GetValue<bool>((int) TankFlag.InHome))
            {
                if (t.HP <= 46)
                {
                    Debug.Log("在基地里恢复到75出去Y");//新：恢复到81就够了
                    return true;
                }
                else
                {
                    Debug.Log("已经满血，出去Y");
                    workingMemory.SetValue((int) TankFlag.InHome, false);
                    return false;
                }
            }

            //旧：if (t.HP <= 41) return true;
            //优化思路，返回应该参考对面AI的血量
            if (t.HP <= 41&&oppTank.HP>=41) return true;
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)//复写决策层传达决策函数
        {
            Tank t = (Tank)agent;
            workingMemory.SetValue((int) TankFlag.InHome, true);
            foreach (var item in Match.instance.GetOppositeMissiles(t.Team))
            {
                if (Physics.SphereCast(item.Value.Position, 0.5f, item.Value.Velocity, out RaycastHit hit, 50))
                {
                    if (MyTool.JudgeHitIsTank(hit, t))
                    {
                        t.TurretTurnTo(item.Value.Position);
                        t.Fire();
                    }
                }
            }
            Debug.Log("回基地Y");
            t.Move(Match.instance.GetRebornPos(t.Team));
            return ERunningStatus.Finished;
        }
    }

    public class AvoidanceUtility : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)//复写决策层传达决策函数
        {
            if(workingMemory.GetValue<bool>((int) TankFlag.preSuparStar))
            {
                return false;
            }
            Tank t = (Tank) agent;
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(t.Team);
            foreach (var item in missiles)
            {
                if (JudgeMissileCanHitCenter(t, item.Value, workingMemory) ||
                    PreJudgeMissileCanHit(t, item.Value, workingMemory))
                {
                    return true;
                }
            }
            return false;
        }
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)//复写决策层传达决策函数
        {
            if (!OnEvaluate(agent, workingMemory))
            {
                return ERunningStatus.Failed;
            }
            return ERunningStatus.Executing;
        }

        private bool JudgeMissileCanHitCenter(Tank t,Missile missile,BlackboardMemory workingMemory)//决策计算用函数
        {
            if (Physics.SphereCast(missile.Position, 0.1f, missile.Velocity, out RaycastHit hit, 50))
            {
                if (MyTool.JudgeHitIsTank(hit, t))
                {
                    Vector3 jcross = Vector3.Cross(missile.Velocity, t.Position - missile.Position);
                    Debug.Log("切向躲避Y");
                    Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                    if (jcross.y > 0)
                        cross = -cross;
                    //旧：
                    // t.Move(t.Position + cross * 5);
                    //优化思路：减少躲避子弹走位的路程和时间，更加精准，切换轨迹
                    //新：
                    Debug.Log("预测急停规避子弹1Y");
                    t.Move(t.Position + cross * 3.0f);
                    return true;
                }
            }

            return false;
        }
        private bool PreJudgeMissileCanHit(Tank t,Missile missile,BlackboardMemory workingMemory)//决策计算用函数
        {
            Vector3 vm = missile.Velocity;
            Vector3 vt = t.Velocity;
            float angle = Vector3.Angle(vm, vt);
            Vector3 jcross = Vector3.Cross(missile.Velocity, t.Position - missile.Position);
            // if (jcross.y < 0)
            // {
            //     Vector3 mcross = Vector3.Cross(missile.Velocity, Vector3.up);
            //     if (Vector3.Angle(mcross, t.Velocity) < 90)
            //     {
            //         return false;
            //     }
            // }
            // else if (jcross.y >= 0)
            // {
            //     Vector3 mcross = -Vector3.Cross(missile.Velocity, Vector3.up);
            //     if (Vector3.Angle(mcross, t.Velocity) < 90)
            //     {
            //         return false;
            //     }
            // }
            if (angle >= 145) return false;
            //投影到地面上
            vm = Vector3.ProjectOnPlane(vm, Vector3.up);
            //计算平面直线方程
            float k1 = vm.z / vm.x;
            Vector3 vmPos = new Vector3(missile.Position.x, 0, missile.Position.z);
            float b1 = vmPos.z - k1 * vmPos.x; 
            vt = Vector3.ProjectOnPlane(vt, Vector3.up);
            float k2 = vt.z / vt.x;
            Vector3 vtPos = new Vector3(t.Position.x, 0, t.Position.z);
            float b2 = vtPos.z - k2 * vtPos.x;
            MyMath.CalculationDualLinearEquation(k1, -1, b1, k2, -1, b2, out float x, out float z);
            Vector3 predictedPos = new Vector3(x, 0, z);
            //真实交点
            // Debug.DrawRay(missile.Position, missile.Velocity, Color.yellow);
            // Debug.DrawRay(t.Position, t.Velocity, Color.yellow);
            //计算交点
            // Debug.DrawLine(vmPos + Vector3.up, predictedPos + Vector3.up, Color.red);
            // Debug.DrawLine(vtPos + Vector3.up, predictedPos + Vector3.up, Color.red);
            if (Physics.SphereCast(missile.Position, 0.2f, missile.Velocity, out RaycastHit hit,
                    (predictedPos - vmPos).magnitude))
            {
                if (MyTool.JudgeHitWall(hit))
                {
                    return false;
                }
            }
            float dm = Vector3.Distance(vmPos, predictedPos);
            float tm = dm / vm.magnitude;
            float dt = Vector3.Distance(vtPos, predictedPos);
            const float collisionLength = 1f + 10f;
            float tmin = (dt - collisionLength) / 10;
            float tmax = (dt + collisionLength) / 10;
            Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
            if (jcross.y > 0)
                cross = -cross;
            //躲避子弹走位
            Debug.DrawRay(t.Position, cross * 20, Color.red);
            if (tm > tmin)
            {
                //旧：
                //Debug.Log("预测急停规避子弹");
                //t.Move(t.Position + cross * 5);
                //return true;
                //优化思路：减少躲避子弹走位的路程和时间，更加精准
                //新：
                Debug.Log("预测急停规避子弹2Y");
                t.Move(t.Position + cross * 0.5f);
                return true;
            }
            return false;
        }
        
    }

    public class MoveToStarPos : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)//复写决策层传达决策函数
        {
            Tank t = (Tank)agent;// 将代理对象转换为坦克对象
            if (workingMemory.GetValue<bool>((int) TankFlag.preSuparStar))
            {
                t.Move(new Vector3(0f, 0f, 0f));// 前往坐标为(0,0,0)的位置
            }
            bool hasStar = false;// 是否发现星星的标志
            float nearestDist = float.MaxValue;// 最近星星的距离
            Vector3 nearestStarPos = Vector3.zero;// 最近星星的位置
            // 遍历所有星星
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;// 获取星星对象
                // 如果当前星星是超级星星
                if (s.IsSuperStar)
                {
                    workingMemory.SetValue((int) TankFlag.preSuparStar, true);// 在黑板中设置前往超级星星的标志
                    hasStar = true;// 标记已发现星星
                    nearestStarPos = s.Position;// 更新最近星星的位置
                    break;// 跳出循环，因为已找到超级星星
                }
                else
                {
                   // workingMemory.SetValue((int) TankFlag.preSuparStar, false);// 在黑板中设置不需要前往超级星星的标志
                    float dist = (s.Position - t.Position).sqrMagnitude;// 计算当前星星与坦克的距离的平方
                    if (dist < nearestDist)
                    {
                        hasStar = true; // 标记已发现星星
                        nearestDist = dist;// 更新最近距离
                        nearestStarPos = s.Position;// 更新最近星星的位置
                    }
                }
            }
            // 如果发现了星星
            if (hasStar)
            {
                t.Move(nearestStarPos);// 坦克前往最近星星的位置
            }
            else
            {
                //旧t.Move(Match.instance.GetRebornPos(t.Team));// 坦克前往出生点
                //新：优化思路，在已经有血量低于一定阈值就返回的情况下应该主动占据极坐标（10，0，10）的场中位置为优势，B队位置为镜像
                if(t.Team==ETeam.A)
                {
                    t.Move(new Vector3(10f, 0f, 10f));
                }else
                {
                    t.Move(new Vector3(-10f, 0f, -10f));
                }
                
            }
            return ERunningStatus.Executing;// 返回执行状态
        }
    }
    
    public class MyTank : Tank
    {
        private BlackboardMemory m_WorkingMemory;
        private Node m_BTNode;
        protected override void OnStart()
        {
            base.OnStart();
            m_WorkingMemory = new BlackboardMemory();
            m_BTNode = new ParallelNode(1).AddChild(new TurnToEnemy(),
                new SelectorNode().AddChild(new MoveBack(),
                    new SelectorNode().AddChild(new AvoidanceUtility(), new MoveToStarPos()))
            );
        }
        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }
        public override string GetName()
        {
            return "YJYTank";
        }
    }
    public class MyMath
    {
        //public static void CalculationDualPlanePoint(float a1, float b1, float c1,float d1, float a2, float b2, float c2,float d2,
        //    out float m, out float n,int choose,float value)
        //{
        //    switch (choose)
        //    {
        //        case 1:
        //            d1 = d1 + value * a1;
        //            d2 = d2 + value * a2;
        //            CalculationDualLinearEquation(b1, c1, d1, b2, c2, d2, out m, out n);
        //            break;
        //        case 2:
        //            d1 = d1 + value * b1;
        //            d2 = d2 + value * b2;
        //            CalculationDualLinearEquation(a1, c1, d1, a2, c2, d2, out m, out n);
        //            break;
        //        case 3:
        //            d1 = d1 + value * c1;
        //            d2 = d2 + value * c2;
        //            CalculationDualLinearEquation(a1, b1, d1, a2, b2, d2, out m, out n);
        //            break;
        //        default:
        //            m = 0;
        //            n = 0;
        //            break;
        //    }
        //}
        public static void CalculationDualLinearEquation(float a1, float b1, float c1, float a2, float b2, float c2,
            out float x, out float y)
        {
            x = (c2 * b1 - c1 * b2) / (a1 * b2 - a2 * b1);
            y = (a1 * c2 - a2 * c1) / (a2 * b1 - a1 * b2);
        }

        //public static void CalculationLineEquation(Vector3 forward, Vector3 position,out float a,out float b,out float c,out float d)
        //{
        //    a = forward.x;
        //    b = forward.y;
        //    c = forward.z;
        //    d = -a * position.x - b * position.y - c * position.z;
        //}

    }

    public class MyTool
    {
        public static bool JudgeHitIsTank(RaycastHit hit, Tank tank)
        {
            var fireCollider = hit.transform.GetComponent<FireCollider>();
            if (fireCollider != null)
            {
                if (fireCollider.Owner == tank)
                    return true;
                else
                {
                    return false;
                }
            }
            return false;
        }

        public static bool JudgeHitWall(RaycastHit hit)
        {
            var fireCollider = hit.transform.GetComponent<FireCollider>();
            if (fireCollider == null)
                return true;
            return false;
        }

        //public static Vector3 PredictedFireForward(Vector3 firePos,Vector3 TargetPos,Vector3 Speed,float MissileSpeed)
        //{
        //    Vector3 targetSpeed = Speed;
        //    Vector3 firePosition = firePos;
        //    Vector3 d = TargetPos - firePosition;
        //    float vp = MissileSpeed;
        //    float v0 = targetSpeed.magnitude;
        //    float cosp0 = Mathf.Cos(Vector3.Angle(-d, targetSpeed) * (Mathf.PI / 180));
        //    float a = v0 * v0 - vp * vp;
        //    float b = -2 * v0 * d.magnitude * cosp0;
        //    float c = d.sqrMagnitude;
        //    float delta = b * b - 4 * a * c;
        //    float predictedTime = (-b - Mathf.Sqrt(delta)) / (2 * a);
        //    Vector3 turnToForward = d + targetSpeed * predictedTime;
        //    return turnToForward;
        //}

    }
}