using Main;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.Base;
using UnityEngine;
using AI.RuleBased;

namespace MZF
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

    class DuringTimePeriod:Condition
    {
        private float downLimit;
        private float upLimit;

        public DuringTimePeriod SetStartEndTime(float down,float up)
        {
            downLimit = down;
            upLimit = up;
            return this;
        }
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.RemainingTime < upLimit && Match.instance.RemainingTime > downLimit;
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
                t.TurretTurnTo(oppTank.Position+oppTank.Velocity.normalized*3.5f);
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
            if (t.HP <= 40)
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
    class EvadeEnemyMissile : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if ((oppTank.Position-t.Position).sqrMagnitude<300f) return false;
            bool canSeeMissile = false;
            float nearestDist = float.MaxValue;
            Missile enemyMissile=default(Missile);
            foreach (var pair in Match.instance.GetOppositeMissiles(t.Team))
            {
                Missile s = pair.Value;
                if (t.CanSeeOthers(s.Position) == false) continue;
                float dist = (s.Position - t.Position).sqrMagnitude;
                if (dist < nearestDist)
                {
                    canSeeMissile = true;
                    nearestDist = dist;
                    enemyMissile = s;
                }
            }
            if (canSeeMissile)
            {
                Vector3 vel = enemyMissile.Velocity.normalized;
                Vector3 tmp = t.Position - enemyMissile.Position;
                vel.y = tmp.y = 0;
                float offset = 1;
                if (Vector3.Cross(vel, tmp).normalized != Vector3.up) offset = -1;
                Vector3 targetPosition = t.Position + Quaternion.AngleAxis(offset*75f, Vector3.up) * vel*5f;
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, targetPosition);
            }
            return canSeeMissile;
        }
    }

    class GoToSomePlace : ActionNode
    {
        private Vector3 targetPosition;

        public GoToSomePlace SetTargetPosition(Vector3 _targetPosition)
        {
            targetPosition = _targetPosition;
            return this;
        }

        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, targetPosition);
            return true;
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
                            new ParallelNode(1).AddChild(
                                new TurnTurret(),
                                new Fire().SetPrecondition(new ConditionCanSeeEnemy())),
                            new SequenceNode().AddChild(
                                new SelectorNode().AddChild(
                                    new BackToHome().SetPrecondition(new DuringTimePeriod().SetStartEndTime(97f,103f)),
                                    new GoToSomePlace().SetTargetPosition(new Vector3(0,0.5f,0)).SetPrecondition(new DuringTimePeriod().SetStartEndTime(90f,97f))),
                                new MoveTo()),
                            new SequenceNode().AddChild(
                                new SelectorNode().AddChild(
                                    new EvadeEnemyMissile(),
                                    new BackToHome(),
                                    new GetStarMove(),
                                    new GoToSomePlace().SetTargetPosition(Match.instance.GetRebornPos(this.Team))),
                                new MoveTo()));
        }
        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }
        public override string GetName()
        {
            return "MZF";
        }
    }
}
