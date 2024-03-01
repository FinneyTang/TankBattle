using UnityEngine;
using System;
using Main;
using AI.Base;
using AI.RuleBased;
using AI.Blackboard;
using AI.BehaviourTree;

namespace SYW
{
    class ConditionCanSeeEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null)
            {
                return t.CanSeeOthers(oppTank);
            }
            return false;
        }
    }
    class ConditionEnemyIsDead : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null||oppTank .IsDead )
            {
                return true;
            }
            return false;
        }
    }
    class TurnTurret : ActionNode
    {
        Vector3 lastPos;
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemroy)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
   
            //Vector3 targetPos;
            float distance;
            distance = (oppTank.Position - lastPos).sqrMagnitude;
            if (oppTank != null && oppTank.IsDead == false)
            {
                if (distance < 400)
                { 
                    t.TurretTurnTo(oppTank.Position);
                }
                else if (distance < 2000)
                {
                    //targetPos = 5 * oppTank.Position - lastPos;
                    t.TurretTurnTo(oppTank.Position);
                }
                else
                {
                    //targetPos = 10 * oppTank.Position - lastPos;
                    t.TurretTurnTo(oppTank.Position);
                }
                lastPos = oppTank.Position;

            }
            else
            {
                t.TurretTurnTo(Match .instance .GetRebornPos (oppTank .Team));
            }
            return ERunningStatus.Executing;
        }
    }
    class Fire : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            return t.CanFire();
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemroy)
        {
            Tank t = (Tank)agent;
            t.Fire();
            return ERunningStatus.Executing;
        }
    }
    class BackToHome : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            float nearestDist = float.MaxValue;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
              
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                    }  
            }
            if (t.HP <= 30 && nearestDist > 300)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                return true;
            }
            return false;
        }
    }
    class GetStarMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
            
                if(!s.IsSuperStar )
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
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, nearestStarPos);
            }
            return hasStar;
        }
    }
    class GetSuperStarMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            //Tank t = (Tank)agent;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    workingMemory.SetValue((int)EBBKey.MovingTargetPos, Vector3.zero);
                    return true;
                }
            }
            if (Time.time + 7 > 0.5f * Match.instance.GlobalSetting.MatchTime && Time.time - 1 < 0.5f * Match.instance.GlobalSetting.MatchTime)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, Vector3.zero );
                return true;
            }
            
            return false;

        }
    }
    class SideStepMove : ActionNode
    {
        float nextExecuteTime;
        float lastExecuteTime;
        bool CanExecute()
        {
            return Time.time > nextExecuteTime||Time .time <lastExecuteTime ;
        }
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            float SleepTime = 2f;
            float LastTime = 0.2f;

            if (CanExecute() == false)
            {
                return false;
            }
            else
            {
                lastExecuteTime = Time.time + SleepTime;
                nextExecuteTime = Time.time + LastTime;
                Vector3 link = oppTank.Position - t.Position;
                Vector3 result =( Quaternion.AngleAxis(90, Vector3.up) * link).normalized*5;
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, t.Position + result);
                return true;
            }
        }

    }
    class RandomMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Vector3 targetPos;
            if (workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out targetPos))
            {
                Tank t = (Tank)agent;
                if (Vector3.Distance(targetPos, t.Position) >= 1f)
                {
                    return false;
                }
            }
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, GetNextDestination());
            return true;
        }
        private Vector3 GetNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return new Vector3(UnityEngine . Random.Range(-halfSize, halfSize), 0, UnityEngine.Random.Range(-halfSize, halfSize));
        }
    }
    class MoveTo : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return workingMemory.HasValue((int)EBBKey.MovingTargetPos);
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            t.Move(workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos));
            return ERunningStatus.Finished;
        }
    }
    enum EBBKey
    {
        MovingTargetPos
    }
    class MyTank : Tank
    {
        private BlackboardMemory m_WorkingMemory;
        private Node m_BTNode;
        protected override void OnStart()
        {
            base.OnStart();
            m_WorkingMemory = new BlackboardMemory();
            m_BTNode = new ParallelNode(1).AddChild(
                            new TurnTurret(),
                            new Fire().SetPrecondition(new OrCondition(new ConditionCanSeeEnemy() ,new ConditionEnemyIsDead() )),
                            new SequenceNode().AddChild(
                                new SelectorNode().AddChild(
                                    new SideStepMove ().SetPrecondition (new ConditionCanSeeEnemy()),
                                    new GetSuperStarMove(),
                                    new BackToHome(),
                                    new GetStarMove(),
                                    new RandomMove()),
                                new MoveTo()));
        }
        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }
        public override string GetName()
        {
            return "SYW";
        }
    }
}
