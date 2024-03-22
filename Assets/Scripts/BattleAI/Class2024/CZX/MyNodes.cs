using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using Main;
using UnityEngine;

namespace CZX
{
    public enum StateEnum
    {
        GoToSuperStar,
        AtHome,
        Stop
    }

    public class TurnTurret : ActionNode
    {
        // 走胃会导致甩狙 没时间改了orz
        // 还有一堆其他的BUG 都是逻辑处理上的BUG 也没时间改了 就这样吧orz

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            var t       = (Tank)agent;
            var oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                // 判断坦克之间的距离
                if (Vector3.Distance(t.Position, oppTank.Position) < 15)
                {
                    t.TurretTurnTo((oppTank.Position - t.Position).normalized);
                }
                else
                {
                    // 根据敌方的移动方向、移动速度和己方位置和炮弹的移动速度
                    // 预计算朝哪个方向发射炮弹能命中敌方

                    // 敌方速度和位置
                    var oppTankSpeed    = oppTank.Velocity;
                    var oppTankPosition = oppTank.Position;
                    // 我方位置和炮弹速度
                    var myTankPosition = t.Position;
                    var missleSpeed    = Match.instance.GlobalSetting.MissileSpeed;
                    // 坦克距离
                    var delta = Vector3.Distance(myTankPosition, oppTankPosition);
                    // 坦克相对方向和敌方坦克速度夹角
                    var oppToMy = myTankPosition - oppTankPosition;
                    var theta   = Mathf.Acos(Vector3.Dot(oppTankSpeed.normalized, oppToMy.normalized));
                    // 根据余弦定理解得t和炮弹发射方向
                    var a     = Mathf.Pow(missleSpeed, 2) - Mathf.Pow(oppTankSpeed.magnitude, 2);
                    var b     = 2 * oppTankSpeed.magnitude * delta * Mathf.Cos(theta);
                    var c     = -Mathf.Pow(delta, 2);
                    var del   = Mathf.Pow(b,      2) - 4 * a * c;
                    var time1 = -(b + Mathf.Sqrt(del)) / (2 * a);
                    var time2 = -(b - Mathf.Sqrt(del)) / (2 * a);
                    // 获取大于0且最小的时间
                    var time            = time1 >= time2 ? time2 > 0 ? time2 : time1 : time1 > 0 ? time1 : time2;
                    var hitPosition     = oppTankPosition + oppTankSpeed * time;
                    var turretDirection = (hitPosition - myTankPosition).normalized;
                    t.TurretTurnTo(t.Position + turretDirection);
                    //Debug.Log("瞄准");
                }
            }
            else
            {
                t.TurretTurnTo(t.Position + t.Forward);
            }

            return ERunningStatus.Executing;
        }
    }

    public class AttackEnemy : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (workingMemory.HasValue((int)StateEnum.Stop) &&
                workingMemory.GetValue<bool>((int)StateEnum.Stop)) return false;
            var t       = (Tank)agent;
            var oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false)
                return t.CanSeeOthers(oppTank.Position);
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            var t       = (Tank)agent;
            var oppTank = Match.instance.GetOppositeTank(t.Team);
            //Debug.Log("尝试攻击");
            if (oppTank != null && oppTank.IsDead == false)
                t.Fire();
            else
                return ERunningStatus.Failed;

            return ERunningStatus.Executing;
        }
    }

    public class GoHome : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            var t = (Tank)agent;
            if (Match.instance.RemainingTime >= 89 && Match.instance.RemainingTime <= 95)
            {
                workingMemory.SetValue((int)StateEnum.GoToSuperStar, true);
                return false;
            }

            workingMemory.SetValue((int)StateEnum.GoToSuperStar, false);

            if (t.HP <= 40) return true;

            if (workingMemory.HasValue((int)StateEnum.AtHome) && workingMemory.GetValue<bool>((int)StateEnum.AtHome))
            {
                if (t.HP <= 50) return true;

                workingMemory.SetValue((int)StateEnum.AtHome, false);
                return false;
            }

            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            workingMemory.SetValue((int)StateEnum.AtHome, true);
            var t = (Tank)agent;
            t.Move(Match.instance.GetRebornPos(t.Team));
            return ERunningStatus.Finished;
        }
    }

    public class StopMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (workingMemory.HasValue((int)StateEnum.GoToSuperStar) &&
                workingMemory.GetValue<bool>((int)StateEnum.GoToSuperStar))
                return false;

            var t        = (Tank)agent;
            var oppTank  = Match.instance.GetOppositeTank(t.Team);
            var distance = Vector3.Distance(t.Position, oppTank.Position);

            var missiles = Match.instance.GetOppositeMissiles(t.Team);
            foreach (var missile in missiles)
                if (CheckBeHit(t, missile.Value, workingMemory))
                    return true;

            return false;
        }

        private bool CheckBeHit(Tank t, Missile missile, BlackboardMemory workingMemory)
        {
            var missilePosition = missile.Position;
            var tankPosition    = t.Position;
            var delta           = (missilePosition - tankPosition).magnitude;
            var missileSpeed    = missile.Velocity;
            var tankSpeed       = t.Velocity;

            if (Vector3.Angle(missileSpeed, tankSpeed) <= 15 || Vector3.Angle(missileSpeed, tankSpeed) >= 165)
            {
                //Debug.Log("角度：" + Vector3.Angle(missileSpeed, tankSpeed));
                RaycastHit hit;
                if (Physics.SphereCast(tankPosition, 0.5f, tankSpeed.normalized, out hit))
                    if (hit.collider != null && hit.collider.gameObject.tag == "Missile")
                    {
                        var normal    = Vector3.Cross(tankSpeed,    missileSpeed).normalized;
                        var direction = Vector3.Cross(missileSpeed, normal).normalized;
                        t.Move(t.Position + direction * tankSpeed.magnitude * 0.5f);
                        workingMemory.SetValue((int)StateEnum.Stop, true);
                        return true;
                    }

                workingMemory.SetValue((int)StateEnum.Stop, false);
                return false;
            }

            if (delta > 15)
            {
                var deltaPosition = missilePosition - tankPosition;
                var t1 =
                    Vector3.Dot(Vector3.Cross(deltaPosition, missileSpeed), Vector3.Cross(tankSpeed, missileSpeed)) /
                    Vector3.Dot(Vector3.Cross(tankSpeed, missileSpeed), Vector3.Cross(tankSpeed, missileSpeed));
                var t2 = Vector3.Dot(Vector3.Cross(deltaPosition, tankSpeed), Vector3.Cross(tankSpeed, missileSpeed)) /
                         Vector3.Dot(Vector3.Cross(tankSpeed, missileSpeed), Vector3.Cross(tankSpeed, missileSpeed));
                if (Mathf.Abs(t2 - t1) < 0.35f)
                {
                    //Debug.Log("Be Hit!");
                    var direction = -t.Velocity;
                    t.Move(t.Position + direction * 0.4f);
                    workingMemory.SetValue((int)StateEnum.Stop, true);
                    return true;
                }
            }

            workingMemory.SetValue((int)StateEnum.Stop, false);
            return false;
        }

        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (OnEvaluate(agent, workingMemory) == false) return ERunningStatus.Failed;

            return ERunningStatus.Executing;
        }
    }

    public class MoveTo : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            var t = (Tank)agent;
            if (workingMemory.HasValue((int)StateEnum.GoToSuperStar) &&
                workingMemory.GetValue<bool>((int)StateEnum.GoToSuperStar))
            {
                t.Move(Vector3.zero);
                return ERunningStatus.Executing;
            }

            var minDistance  = float.MaxValue;
            var nextPosition = Vector3.zero;
            var goToStar     = false;
            var stars        = Match.instance.GetStars();
            foreach (var star in stars.Values)
                if (star.IsSuperStar)
                {
                    workingMemory.GetValue((int)StateEnum.GoToSuperStar, true);
                    nextPosition = star.Position;
                    goToStar     = true;
                    break;
                }
                else
                {
                    workingMemory.GetValue((int)StateEnum.GoToSuperStar, false);
                    var distance = (star.Position - t.Position).magnitude;
                    if (distance <= minDistance)
                    {
                        minDistance  = distance;
                        nextPosition = star.Position;
                        goToStar     = true;
                    }
                }

            if (goToStar)
            {
                t.Move(nextPosition);
            }
            else
            {
                if (t.HP <= 35) t.Move(Match.instance.GetRebornPos(t.Team));
            }

            return ERunningStatus.Executing;
        }
    }
}