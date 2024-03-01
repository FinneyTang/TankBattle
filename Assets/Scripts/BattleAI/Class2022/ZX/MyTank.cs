using Main;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.Base;
using UnityEngine;
using AI.RuleBased;
using UnityEngine.AI;
using EBBKey = BT.EBBKey;

namespace ZX {
    public class MyTank : Tank {
        private BlackboardMemory _memory;
        private Node _btRoot;

        protected override void OnStart() {
            base.OnStart();
            _memory = new BlackboardMemory();
            _btRoot = new SelectorNode().AddChild(
                            new ParallelNode(1).SetPrecondition(new OrCondition(new ConditionSuperStarExists(), new ConditionSuperStarComing())).AddChild(
                                new ActionTurnTurret(),
                                new ActionFire().SetPrecondition(new ConditionCanSeeEnemy()),
                                new SequenceNode().AddChild(
                                    new ActionCaptureCenterArea(),
                                    new ActionMoveToTarget()
                                )
                            ),
                            new SequenceNode().SetPrecondition(new ConditionEnemyDead()).AddChild(
                                new SelectorNode().AddChild(
                                    new ActionBackToHome(),
                                    new ActionGetStar(),
                                    new ActionRandomMove()
                                ),
                                new ActionMoveToTarget()
                            ),
                            new ParallelNode(1).AddChild(
                                new ActionTurnTurret(),
                                new ActionFire().SetPrecondition(new ConditionCanSeeEnemy()),
                                new SequenceNode().AddChild(
                                    new SelectorNode().AddChild( 
                                        new SelectorNode().AddChild(
                                            new ActionDodge(),
                                            new ActionBackToHome(),
                                            new ActionGetStar(),
                                            new ActionRandomMove()
                                        )
                                    ),
                                    //new ActionDodge().SetPrecondition(new ConditionCanSeeEnemy()),
                                    new ActionMoveToTarget()
                                )
                            )
                        );
        }

        protected override void OnUpdate() {
            base.OnUpdate();
            BehaviourTreeRunner.Exec(_btRoot, this, _memory);
        }

        public override string GetName() {
            return "ZX";
        }
    }

    #region Conditions

    public class ConditionCanSeeEnemy : Condition {
        public override bool IsTrue(IAgent agent) {
            Tank t = agent as Tank;
            Tank enemy = Match.instance.GetOppositeTank(t.Team);

            if (enemy != null)
                return t.CanSeeOthers(enemy);
            else
                return false;
        }
    }

    public class ConditionLifeLow : Condition {
        public override bool IsTrue(IAgent agent) {
            Tank t = agent as Tank;
            return t.HP < 50;
        }
    }

    public class ConditionEnemyClose : Condition {
        public override bool IsTrue(IAgent agent) {
            Tank t = agent as Tank;
            Tank enemy = Match.instance.GetOppositeTank(t.Team);

            return Vector3.Distance(t.Position, enemy.Position) <= 15;
        }
    }

    public class ConditionHealCompletly : Condition {
        public override bool IsTrue(IAgent agent) {
            Tank t = agent as Tank;
            return t.HP > 75;
        }
    }

    /// <summary>
    /// 超级星星是否即将生成
    /// 超级星星在90s时生成，提前5s准备
    /// </summary>
    public class ConditionSuperStarComing : Condition {
        public override bool IsTrue(IAgent agent) {
            return Match.instance.RemainingTime <= 95 && Match.instance.RemainingTime >= 90;
        }
    }

    /// <summary>
    /// 寻找超级星星是否已经存在
    /// </summary>
    public class ConditionSuperStarExists : Condition {
        public override bool IsTrue(IAgent agent) {
            foreach (var pair in Match.instance.GetStars()) {
                if (pair.Value.IsSuperStar)
                    return true;
            }

            return false;
        }
    }

    public class ConditionEnemyDead : Condition {
        public override bool IsTrue(IAgent agent) {
            Tank t = agent as Tank;
            return Match.instance.GetOppositeTank(t.Team).IsDead;
        }
    }

    #endregion

    #region ActionNodes

    public class ActionTurnTurret : ActionNode {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory) {
            Tank t = agent as Tank;
            Tank enemy = Match.instance.GetOppositeTank(t.Team);

            Vector3 dir = enemy.Velocity.normalized;
            
            if (enemy != null && enemy.IsDead == false) {
                
                float time_trace = ((enemy.Position - t.FirePos).magnitude /
                                    Match.instance.GlobalSetting.MissileSpeed);
                var targtPos = enemy.Position + enemy.Velocity *
                    (((enemy.Position + enemy.Velocity * (Time.deltaTime * time_trace) - t.FirePos).magnitude /
                      Match.instance.GlobalSetting.MissileSpeed));
                t.TurretTurnTo(targtPos);

                /*var offset = Vector3.Distance(enemy.Position, enemy.NextDestination) < 2 ? 0 : 1.5f;
                t.TurretTurnTo(enemy.Position + dir * offset);*/
            }
            else {
                t.TurretTurnTo(t.transform.position + t.Forward);
            }

            return ERunningStatus.Executing;
        }
    }

    public class ActionFire : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory) {
            Tank t = agent as Tank;
            return t.CanFire();
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory) {
            Tank t = agent as Tank;
            t.Fire();
            return ERunningStatus.Executing;
        }
    }

    public class ActionBackToHome : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = agent as Tank;
            if (t.HP <= 50) {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                return true;
            }
            else if (Match.instance.GetOppositeTank(t.Team).IsDead && t.HP < 76)
                return true;

            return false;   
        }
    }

    public class ActionDodge : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory) {
            Tank t = agent as Tank;
            Tank enemy = Match.instance.GetOppositeTank(t.Team);
            
            var m = Match.instance.GetOppositeMissiles(t.Team);
            //如果敌人已经死亡 则不实行
            if (enemy.IsDead) return false;
            //不需要闪避
            if (m.Count == 0 || t.HP - enemy.HP >= 25) return false;
            foreach (var missile in m) {
                Missile curMissile = missile.Value;
                float timeToHit = Vector3.Distance(t.Position, curMissile.Position) / curMissile.Velocity.magnitude;
                
                var dodgeDistance = curMissile.GetComponentInChildren<SphereCollider>().radius +
                                    t.GetComponent<NavMeshAgent>().radius;
                //求出闪避的方向
                var dir = Vector3.Cross((enemy.Position-t.Position), curMissile.Velocity).y > 0 ? 
                    Quaternion.Euler(0, 90, 0) * curMissile.Velocity . normalized:
                    Quaternion.Euler(0, -90, 0) * curMissile.Velocity . normalized;
                //var dir = Quaternion.Euler(0, 90, 0) * curMissile.Velocity . normalized;
                
                if((dodgeDistance + 1)/t.Velocity.magnitude > timeToHit)
                    continue; //如果来不及躲就不躲 或者躲下一发
                else {
                    workingMemory.SetValue((int)EBBKey.MovingTargetPos, t.Position + (dodgeDistance + 5) * dir);
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 提前占据中心位置
    /// </summary>
    public class ActionCaptureCenterArea : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory) {
            workingMemory.SetValue((int)EBBKey.MovingTargetPos,Vector3.zero);
            return true;
        }
    }

    /// <summary>
    /// 将星星设为目标点
    /// </summary>
    public class ActionGetStar : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory) {
            Tank t = agent as Tank;
            Tank enemy = Match.instance.GetOppositeTank(t.Team);

            Vector3 targetPos = Vector3.zero;
            float nearestDistance = float.PositiveInfinity;
            bool hasStar = false;

            foreach (var star in Match.instance.GetStars()) {
                Star s = star.Value;
                if (Match.instance.GetStars().Count == 1) {
                    hasStar = true;
                    targetPos = s.Position;
                    break;
                }
                if (s.IsSuperStar) {
                    hasStar = true;
                    targetPos = s.Position;
                    break;
                }

                float dis = CaculatePathDistance(t.CaculatePath(s.Position));

                if (!enemy.IsDead && CaculatePathDistance(enemy.CaculatePath(s.Position)) < dis)
                    continue;

                if (nearestDistance > dis) {
                    hasStar = true;
                    targetPos = s.Position;
                    nearestDistance = dis;
                }
            }

            if (hasStar)
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, targetPos);

            return hasStar;
        }

        private float CaculatePathDistance(NavMeshPath path) {
            float sum = 0;
            Vector3 lastPos = Vector3.down * 10;
            foreach (Vector3 corner in path.corners) {
                if (lastPos.y > -2f)
                    sum += Vector3.Distance(corner, lastPos);
                lastPos = corner;
            }

            return sum;
        }
    }

    public class ActionRandomMove : ActionNode {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory) {
            Vector3 targetPos;
            if (workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out targetPos)) {
                Tank t = (Tank)agent;
                if (Vector3.Distance(targetPos, t.Position) >= 1f) {
                    return false;
                }
            }

            workingMemory.SetValue((int)EBBKey.MovingTargetPos, GetNextDestination());
            return true;
        }

        private Vector3 GetNextDestination() {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
        }
    }

    /// <summary>
    /// 移動
    /// </summary>
    class ActionMoveToTarget : ActionNode {
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

    #endregion
}