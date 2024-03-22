using System.Collections;
using System.Collections.Generic;
using Main;
using UnityEngine;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.Base;
using AI.RuleBased;

namespace GYS
{
    class DistanceAboveToEnemy : Condition
    {
        private float targetDis;

        public DistanceAboveToEnemy(float dis)
        {
            targetDis = dis;
        }

        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            return Vector3.Distance(myTank.transform.position, oppTank.transform.position) > targetDis;
        }
    }

    class TurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            DistanceAboveToEnemy longDisCondition = new DistanceAboveToEnemy(45);
            DistanceAboveToEnemy midDisCondition = new DistanceAboveToEnemy(15);
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            var targetLongForwardPos = oppTank.Position + 10 * oppTank.Forward.normalized;
            var targetMidForwardPos = oppTank.Position + 3 * oppTank.Forward.normalized;
            var targetCenterPos = oppTank.Position+ oppTank.Forward.normalized;
            if (oppTank == null || oppTank.IsDead )
            {
                return ERunningStatus.Executing;
            }
            
            if (longDisCondition.IsTrue(agent))
            {
                myTank.TurretTurnTo(targetLongForwardPos);
            }
            else if(midDisCondition.IsTrue(agent))
            {
                myTank.TurretTurnTo(targetMidForwardPos);
            }
            else
            {
                myTank.TurretTurnTo(targetCenterPos);
            }

            return ERunningStatus.Executing;
        }
    }

    class CanSeeEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if (oppTank != null)
            {
                return myTank.CanSeeOthers(oppTank);
            }

            return false;
        }
    }

    class Fire : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            var condition = new CanSeeEnemy();
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            float angle = Vector3.Angle(myTank.TurretAiming, oppTank.Position - myTank.Position);
            if (angle > 10f)
            {
                return false;
            }

            return condition.IsTrue(agent);
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            myTank.Fire();
            return ERunningStatus.Executing;
        }
    }

    class BloodGapAboveCondition : Condition
    {
        private int bloodGap;

        public BloodGapAboveCondition(int targetGap)
        {
            bloodGap = targetGap;
        }

        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            int curBloodGap = myTank.HP - oppTank.HP;

            return curBloodGap > bloodGap;
        }
    }


    class DistanceToNearestStarBelowCondition : Condition
    {
        private float distance;

        public DistanceToNearestStarBelowCondition(float targetDistance)
        {
            distance = targetDistance;
        }

        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Vector3 nearestStarPos = GYSTool.CalculateNeareatstStarPos(agent);
            float curDistance = Vector3.Distance(myTank.Position, nearestStarPos);
            return curDistance < distance;
        }
    }

    class GoToNearestStar : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            Vector3 nearestStarPos = GYSTool.CalculateNeareatstStarPos(agent);
            myTank.Move(nearestStarPos);
            return ERunningStatus.Executing;
        }
    }

    class GoToSuperStar : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return Match.instance.RemainingTime < 96 && Match.instance.RemainingTime > 90;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            myTank.Move(Vector3.zero);
            return ERunningStatus.Executing;
        }
    }

    class GoHome : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            //路过星星
            var disToStarCondition = new DistanceToNearestStarBelowCondition(10);
            if (disToStarCondition.IsTrue(agent))
            {
                return false;
            }
            //回满血   
            float disToHome = Vector3.Distance(myTank.Position, Match.instance.GetRebornPos(myTank.Team));
            if (disToHome < 2 && myTank.HP != 100)
            {
                return true;
            }
            return myTank.HP < 45;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            Vector3 homePos = Match.instance.GetRebornPos(myTank.Team);
            myTank.Move(homePos);

            return ERunningStatus.Executing;
        }
    }


    class DoAvoid : ActionNode
    {
        private Missile nearestMissile;
        private float timeCounter;
        
        
        //贴脸不闪避
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            var disToEnemyCondition = new DistanceAboveToEnemy(15);
            if (!disToEnemyCondition.IsTrue(agent))
            {
                return false;
            }
            
            //敌人缩在老家，不对枪，去捡星星
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            var ememyDisToHisHome = Vector3.Distance(oppTank.Position, Match.instance.GetRebornPos(oppTank.Team));
            if (ememyDisToHisHome < 5f)
            {
                return false;
            }
            
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(t.Team);
            foreach (var item in missiles)
            {
                if (Physics.SphereCast(item.Value.Position, 0.1f, item.Value.Velocity, out RaycastHit hit, 50))
                {
                    if (GYSTool.JudgeHitIsTank(hit, t))
                    {
                        nearestMissile = item.Value;
                        return true;
                    }
                }
            }
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            //do avoid
            Tank t = (Tank)agent;
            Vector3 jcross = Vector3.Cross(nearestMissile.Velocity, t.Position - nearestMissile.Position);
            Vector3 cross = Vector3.Cross(nearestMissile.Velocity, Vector3.up).normalized;
            if (jcross.y > 0)
                cross = -cross;
            t.Move(t.Position + cross * 5);
            
            timeCounter += Time.deltaTime;
            if (timeCounter > 0.5f)
            {
                timeCounter = 0;
                return ERunningStatus.Finished;
            }

            return ERunningStatus.Executing;
        }
    }

    class ChaseEnemy : ActionNode
    {
        private float timeCounter;
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            //离星星太近
            var disToStarCondition = new DistanceToNearestStarBelowCondition(10);
            if (disToStarCondition.IsTrue(agent))
            {
                return false;
            }

            //敌人离家太近
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            var ememyDisToHisHome = Vector3.Distance(oppTank.Position, Match.instance.GetRebornPos(oppTank.Team));
            if (ememyDisToHisHome < 10f)
            {
                return false;
            }

            var disToOpp = Vector3.Distance(myTank.Position, oppTank.Position);
            if (disToOpp < 10f)
            {
                return false;
            }

            return true;
        }

        //移动到敌人位置
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            myTank.Move(oppTank.Position);
            
            timeCounter += Time.deltaTime;
            if (timeCounter > 1f)
            {
                timeCounter = 0;
                return ERunningStatus.Finished;
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
            m_BTNode = new ParallelNode(1).AddChild(
                new SelectorNode().AddChild(
                    //超级星星存在时
                    new GoToSuperStar(),
                    //是否回家(包括回家路上有星星
                    new GoHome(),
                    new SelectorNode().AddChild(
                        //Avoid
                        new SequenceNode().AddChild(
                            new DoAvoid(),
                            new ChaseEnemy().SetPrecondition(new BloodGapAboveCondition(25))
                        ),
                        new GoToNearestStar()
                    )
                ),
                new TurnTurret(),
                new Fire()
            );
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }

        public override string GetName()
        {
            return "GYS";
        }
    }


    public static class GYSTool
    {
        public static Vector3 CalculateNeareatstStarPos(IAgent agent)
        {
            Tank t = (Tank)agent;
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = s.Position;
                    break;
                }
                else
                {
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
                return nearestStarPos;
            }

            return Vector3.zero;
        }

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
    }
}