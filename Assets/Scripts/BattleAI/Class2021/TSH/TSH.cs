using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.Base;
using AI.RuleBased;

namespace TSH
{
    class ConditionCanSeeEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank mytank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(mytank.Team);
            if(oppTank != null)
            {
                return mytank.CanSeeOthers(oppTank);
            }
            return false;
        }
    }
    class ConditionCanGetNearbyStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            float nearestDist = float.MaxValue;
            float CanGetDist = 40f;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                float dist = (s.Position - myTank.Position).sqrMagnitude;
                if(dist < nearestDist)
                {
                    nearestDist = dist;
                }
            }
            if (nearestDist < CanGetDist)
            {
                return true;
            }
            return false;
        }
    }
    class TurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if(oppTank != null && oppTank.IsDead == false)
            {
                myTank.TurretTurnTo(oppTank.Position);
            }
            else
            {
                myTank.TurretTurnTo(myTank.Position + myTank.Forward);
            }
            return ERunningStatus.Executing;
        }
    }
    class Fire : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            return myTank.CanFire();
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank myTank = (Tank)agent;
            myTank.Fire();
            return ERunningStatus.Executing;
        }
    }
    class GetNearbyStar : ActionNode
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
                float dist = (s.Position - t.Position).sqrMagnitude;
                if (dist < nearestDist)
                {
                    hasStar = true;
                    nearestDist = dist;
                    nearestStarPos = s.Position;
                }
            }
            if (hasStar)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, nearestStarPos);
            }
            return hasStar;
        }
    }
    class GetSuperStar : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            bool HasSuperStar = false;
            Vector3 SuperStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    HasSuperStar = true;
                    SuperStarPos = s.Position;
                    break;
                }
            }
            if (HasSuperStar)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, SuperStarPos);
            }
            return HasSuperStar;
        }
    }
    class BackToHome : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            if (t.HP <= 80)
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
            float farthestDist = float.MinValue;
            Vector3 farthestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;

                float dist = (s.Position - t.Position).sqrMagnitude;
                if (dist > farthestDist)
                {
                    hasStar = true;
                    farthestDist = dist;
                    farthestStarPos = s.Position;
                }                
            }
            if (hasStar)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, farthestStarPos);
            }
            return hasStar;
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
            Tank myTank = (Tank)agent;
            myTank.Move(workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos));
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
                new Fire().SetPrecondition(new ConditionCanSeeEnemy()),
                new TurnTurret(),
                new SequenceNode().AddChild(
                    new SelectorNode().AddChild(
                        new GetNearbyStar().SetPrecondition(new ConditionCanGetNearbyStar()),
                        new GetSuperStar(),
                        new BackToHome(),
                        new GetStarMove()),
                    new MoveTo()));
        }
        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }
        public override string GetName()
        {
            return "TSH";
        }
    }

}


