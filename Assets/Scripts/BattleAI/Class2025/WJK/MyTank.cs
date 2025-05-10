using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using AI.RuleBased;
using Main;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WJK
{
    #region Condition
    class CanSeeEnemyCondition : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if(oppTank != null) {
                if (t.CanSeeOthers(oppTank.Position + oppTank.Forward * 2)) return true;
                return t.CanSeeOthers(oppTank);
            }
            return false;
        }
    }
    class UnhealthyCondition : Condition
    {
        int unHealthHP;
        public UnhealthyCondition(int unHealthHP)
        {
            this.unHealthHP = unHealthHP;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return t.HP<= unHealthHP;
        }
    }

    #endregion
    #region Action
    class TurnTurret : ActionNode {
        protected override void OnEnter(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null) {
                if ((oppTank.Position - t.Position).magnitude <= 10)
                {
                    t.TurretTurnTo(oppTank.Position);
                }
                else t.TurretTurnTo(oppTank.Position+ oppTank.Forward*5);
            }
            if (oppTank.IsDead)
            {
                t.TurretTurnTo(Match.instance.GetRebornPos(oppTank.Team));

            }
            return ERunningStatus.Executing;
        }
    }
    class Fire : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            return t.CanFire();
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)(agent);
            t.Fire();
            return ERunningStatus.Executing;
        }
    }
    class FollowEnemy :ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if((workingMemory.TryGetValue<bool>((int)EBBKey.HasSuperStar,out bool hasSuperStar) && hasSuperStar)||oppTank.IsDead)
            {
                return false;
            } 
            if(oppTank.Position == Match.instance.GetRebornPos(oppTank.Team))
            {
                return false;
            }
            return  t.HP - oppTank.HP >=20;
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            workingMemory.SetValue((int)EBBKey.MoveTarget,oppTank.Position);
            return ERunningStatus.Finished;
        }
    }
    class BackToHome : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (workingMemory.GetValue<bool>((int)EBBKey.HasSuperStar)) return false;
            return !oppTank.IsDead;
        } 
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Debug.Log("GoHome");
            Tank t = (Tank)agent;
            workingMemory.SetValue((int)EBBKey.MoveTarget, Match.instance.GetRebornPos(t.Team));
            return ERunningStatus.Finished;
        }
    }
    class Else : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            Debug.Log("Else");
            workingMemory.SetValue((int)EBBKey.MoveTarget,Vector3.zero);
            return ERunningStatus.Finished;
        }
    }
    class GetStarMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            bool hasStar = false;
            bool hasSuperStar = false;
            float nearestDist = float.MaxValue ;
            float dist = float.MaxValue ;
            Vector3 nearestStarPos = Vector3.zero;

            if (Match.instance.RemainingTime <= 100&& Match.instance.RemainingTime >= 89)
            {
                workingMemory.SetValue((int)EBBKey.MoveTarget, Vector3.zero);
                return true;
            }
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;

                if (s.IsSuperStar)
                {
                    hasStar = true;
                    hasSuperStar = true;
                    workingMemory.SetValue((int)EBBKey.HasSuperStar,true);
                    nearestStarPos = s.Position;
                    break;
                }
                else
                {
                    dist = (s.Position - t.Position).magnitude;
                    if (dist < nearestDist)
                    {
                        workingMemory.SetValue((int)EBBKey.HasSuperStar, false);
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position;
                    }
                }
            }
            if (hasStar)
            {
                workingMemory.SetValue((int)EBBKey.MoveTarget, nearestStarPos);
                if (hasSuperStar) return hasSuperStar;
                if (oppTank.IsDead) {
                    return hasStar;
                }
                if (t.HP <= 40)
                {
                    if (nearestDist <= 20)
                    {
                        if ((oppTank.Position - t.Position).magnitude <= 20) return false;
                        return hasStar;
                    }
                    return false;
                }
            }
            return hasStar;
        }
    }
    class AvoidMissile : ActionNode
    {
        Dictionary<int, Missile> missiles;
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            missiles = Match.instance.GetOppositeMissiles(t.Team);
            if (workingMemory.GetValue<bool>((int)EBBKey.HasSuperStar)) return false;
            return missiles.Count>0;
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            foreach (var pair in missiles)
            {
                if ((pair.Value.Position - t.Position).magnitude >= 18) continue;
                Missile missile = pair.Value;
                Vector3 onWhichSideInfo = Vector3.Cross(missile.Velocity, t.Position - missile.Position);
                Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                if (onWhichSideInfo.y > 0) cross *= -1;
                t.Move(t.Position + cross * 4.2f);
            }
            return ERunningStatus.Finished;
        }
    }
    class MoveToPos : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return workingMemory.HasValue((int)EBBKey.MoveTarget);
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = ( Tank)(agent);
            Vector3 moveTarget = workingMemory.GetValue<Vector3>((int)EBBKey.MoveTarget);
            t.Move(moveTarget);
            if (moveTarget== Match.instance.GetRebornPos(t.Team))
            {
                if (t.HP <= 70) return ERunningStatus.Executing;
            }
            return ERunningStatus.Finished;
        }
        protected override void OnExit(IAgent agent, BlackboardMemory workingMemory)
        {
            workingMemory.Clear();
        }
    }
    #endregion
    enum EBBKey
    {
        MoveTarget,
        HasSuperStar,
    }
    class MyTank : Tank
    {
        private BlackboardMemory m_blackboard;
        private Node m_BTNode;
        protected override void OnAwake()
        {
            base.OnAwake();
            m_blackboard = new BlackboardMemory();
            Node baseNode = new Node();
            m_BTNode = new ParallelNode(1).AddChild(
                            new TurnTurret(),
                            new Fire().SetPrecondition(new CanSeeEnemyCondition()),
                            new SequenceNode().AddChild(
                                new SelectorNode().AddChild(
                                    new FollowEnemy(),
                                    new GetStarMove(),
                                    new BackToHome().SetPrecondition(new UnhealthyCondition(40)),
                                    new Else()
                                ),
                                    new SelectorNode().AddChild(
                                        new AvoidMissile().SetPrecondition(new CanSeeEnemyCondition()),
                                        new MoveToPos())
                                   )
                            );

        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            Debug.Log("´ó"+m_blackboard.GetValue<bool>((int)EBBKey.HasSuperStar));
            BehaviourTreeRunner.Exec(m_BTNode,this, m_blackboard);
        }
        public override string GetName()
        {
            return "WJK";
        }
    }
}
