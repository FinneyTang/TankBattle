using Main;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.Base;
using UnityEngine;
using AI.RuleBased;

namespace BT
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
    class TurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemroy)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                t.TurretTurnTo(oppTank.Position);
            }
            else
            {
                t.TurretTurnTo(t.Position + t.Forward);
            }
            return ERunningStatus.Executing;
        }
    }
    class Fire : ActionNode
    {
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
            if(t.HP <= 30)
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
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = s.Position;
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
            if(hasStar)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, nearestStarPos);
            }
            return hasStar;
        }
    }
    class RandomMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if(workingMemory.HasValue((int)EBBKey.MovingTargetPos))
            {
                Tank t = (Tank)agent;
                Vector3 targetPos = workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos);
                if(Vector3.Distance(targetPos, t.Position) >= 1f)
                {
                    return false;
                }
            }
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, GetNextDestination());
            return true;
        }
        private Vector3 GetNextDestination()
        {
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
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
                            new Fire().SetPrecondition(new ConditionCanSeeEnemy()),
                            new SequenceNode().AddChild(
                                new SelectorNode().AddChild(
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
            return "BTTank";
        }
    }
}
