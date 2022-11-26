using System.Collections;
using System.Collections.Generic;
using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using HZR;
using UnityEngine;
using Main;

namespace HZR
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
        private Queue<Vector3> TankSpeeds = new Queue<Vector3>();
        private Vector3 lastOppPos;
        private int maxSteps = 3;

        private Vector3 lastPos;
        private float lastTime;
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                Transform turret = t.transform.GetChild(1).transform;
                Vector3 v = (oppTank.transform.position - lastPos) / (Time.time - lastTime);
                lastPos = oppTank.transform.position;
                lastTime = Time.time;
                AddToTankSpeedLine(v);
                Vector3 oppSpeed = GetTankAVGSpeed();
                Vector3 firePosition = t.FirePos;
                Vector3 d = oppTank.Position - firePosition;
                float vp = MissileSpeed;
                float v0 = oppSpeed.magnitude;
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
                if (Vector3.Distance(t.Position, oppTank.Position) < 14)
                {
                    Debug.Log("距离较近，无需判断直接射击");
                    t.Fire();
                }else if (Physics.SphereCast(t.FirePos, 0.23f, turnToForward, out RaycastHit hit,
                              turnToForward.magnitude))
                {
                    if (!MyTool.JudgeHitWall(hit))
                    {
                        if (Vector3.Angle(t.TurretAiming, turnToForward) < 15)
                            t.Fire();
                    }
                }
                else
                {
                    if (Vector3.Distance(t.Position, oppTank.Position) < 12)
                    {
                        t.Fire();
                    }
                    if (Vector3.Angle(t.TurretAiming, turnToForward) < 15)
                        t.Fire();
                }
            }
            else
            {
                t.TurretTurnTo(t.Position + t.Forward);
            }
            return ERunningStatus.Executing;
        }

        private void AddToTankSpeedLine(Vector3 speed)
        {
            if (TankSpeeds.Count >= maxSteps)
            {
                TankSpeeds.Dequeue();
            }
            TankSpeeds.Enqueue(speed);
        }

        private Vector3 GetTankAVGSpeed()
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
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank) agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (Match.instance.RemainingTime < 95 && Match.instance.RemainingTime > 85)
            {
                workingMemory.SetValue((int) TankFlag.preSuparStar, true);
                return false;
            }
            if (oppTank.IsDead && t.HP <= 50)
            {
                Debug.Log("敌人死亡，回基地");
                return true;
            }

            if (workingMemory.HasValue((int) TankFlag.InHome) && workingMemory.GetValue<bool>((int) TankFlag.InHome))
            {
                if (t.HP <= 66)
                {
                    Debug.Log("在基地里恢复到75出去");
                    return true;
                }
                else
                {
                    Debug.Log("已经满血，出去");
                    workingMemory.SetValue((int) TankFlag.InHome, false);
                    return false;
                }
            }
            if (t.HP <= 50) return true;
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
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
            Debug.Log("回基地");
            t.Move(Match.instance.GetRebornPos(t.Team));
            return ERunningStatus.Finished;
        }
    }

    public class AvoidanceUtility : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
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
        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (!OnEvaluate(agent, workingMemory))
            {
                return ERunningStatus.Failed;
            }
            return ERunningStatus.Executing;
        }

        private bool JudgeMissileCanHitCenter(Tank t,Missile missile,BlackboardMemory workingMemory)
        {
            if (Physics.SphereCast(missile.Position, 0.1f, missile.Velocity, out RaycastHit hit, 50))
            {
                if (MyTool.JudgeHitIsTank(hit, t))
                {
                    Vector3 jcross = Vector3.Cross(missile.Velocity, t.Position - missile.Position);
                    Debug.Log("切向躲避");
                    Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                    if (jcross.y > 0)
                        cross = -cross;
                    t.Move(t.Position + cross * 5);
                    return true;
                }
            }

            return false;
        }
        private bool PreJudgeMissileCanHit(Tank t,Missile missile,BlackboardMemory workingMemory)
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
                Debug.Log("预测急停规避子弹");
                t.Move(t.Position + cross * 5);
                return true;
            }
            return false;
        }
        
    }

    public class MoveToStarPos : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            if(workingMemory.GetValue<bool>((int) TankFlag.preSuparStar))
            {
                t.Move(Vector3.zero);
            }
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    workingMemory.SetValue((int) TankFlag.preSuparStar, true);
                    hasStar = true;
                    nearestStarPos = s.Position;
                    break;
                }
                else
                {
                    workingMemory.SetValue((int) TankFlag.preSuparStar, false);
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position;
                    }
                }
            }

            if (hasStar)
            {
                t.Move(nearestStarPos);
            }
            else
            {
                t.Move(Match.instance.GetRebornPos(t.Team));
            }
            return ERunningStatus.Executing;
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
            return "HZRTank";
        }
    }
}