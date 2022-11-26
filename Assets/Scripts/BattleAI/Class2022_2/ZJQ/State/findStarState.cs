using Main;
using ZJQ;
using UnityEngine;
using AI.BehaviourTree;
using AI.Base;
using AI.Blackboard;


public class findStarState : FiniteStateMachine
{
    //行为树成员
    BlackboardMemory shareData;
    Node root;

    public override void enterState(MyTank obj)
    {
        _obj = obj;
        shareData = new BlackboardMemory();
        shareData.SetValue(0, _obj);
        shareData.SetValue(1, _obj.enemy);
        root = new SequenceNode().AddChild(
                new SelectorNode().AddChild(
                    new lessHP(),
                    new findOtherStar(),
                    new avoidMissle()
                    ),
                new movePos(),
                new aimingNode()
            );
    }

    public class findOtherStar : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank mTank = null;
            workingMemory.TryGetValue<Tank>(0, out mTank);
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
                    float dist = (s.Position - mTank.Position).sqrMagnitude;
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
                workingMemory.SetValue(5, nearestStarPos);
            }
            return hasStar;
        }
    }

    public class lessHP : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank mTank = null;
            workingMemory.TryGetValue<Tank>(0, out mTank);
            Tank enemy = null;
            workingMemory.TryGetValue<Tank>(1, out enemy);

            if (mTank != null)
            {
                if (mTank.HP < 50 || enemy.IsDead)
                {
                    workingMemory.SetValue(5, Match.instance.GetRebornPos(mTank.Team));
                    return true;
                }


            }

            return false;
        }
    }

    public class movePos : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Vector3 targetPoint;
            Tank mTank;
            workingMemory.TryGetValue<Tank>(0, out mTank);
            workingMemory.TryGetValue<Vector3>(5, out targetPoint);

            if (mTank != null && targetPoint != null)
                mTank.Move(targetPoint);

            return ERunningStatus.Finished;
        }
    }

    public class aimingNode : ActionNode
    {
        Tank mTank;
        Tank enemy;
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            workingMemory.TryGetValue<Tank>(0, out mTank);
            workingMemory.TryGetValue<Tank>(1, out enemy);

            Vector3 targetPoint = AimAdvanceAmountPosition(enemy);

            if (!enemy.IsDead)
            {
                mTank.TurretTurnTo(targetPoint);
            }
            else
            {
                mTank.TurretTurnTo(mTank.Position + mTank.Forward);
            }

            return ERunningStatus.Finished;

        }

        public Vector3 AimAdvanceAmountPosition(Tank targetPos, float AdvanceWeight = 0.35f)
        {
            Vector3 AimPos;
            float distance = Vector3.Distance(mTank.Position, enemy.Position);
            float missileFlyingTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            //AdvanceWeight调和提前量和坦克位置
            AimPos = targetPos.Position + targetPos.Forward + targetPos.Velocity * missileFlyingTime * AdvanceWeight;
            return AimPos;
        }


    }

    public class avoidMissle : ActionNode
    {
        Tank enemy;
        Tank mTank;

        public Vector3 AimAdvanceAmountPosition(Tank targetPos, float AdvanceWeight = 0.85f)
        {
            Vector3 AimPos;
            float distance = Vector3.Distance(mTank.Position, enemy.Position);
            float missileFlyingTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            //AdvanceWeight调和提前量和坦克位置
            AimPos = targetPos.Position + targetPos.Velocity * missileFlyingTime * AdvanceWeight;
            return AimPos;
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {

            workingMemory.TryGetValue<Tank>(0, out mTank);
            workingMemory.TryGetValue<Tank>(1, out enemy);


            Main.Missile dealingMissile = null;
            Vector3 esaPos = Vector3.zero;
            float missleDistance = float.MaxValue;

            if (!enemy.IsDead)
            {
                foreach (var item in Main.Match.instance.GetOppositeMissiles(mTank.Team).Values)
                {

                    if (!Physics.Raycast(item.Position,
                            item.Position + item.Velocity.normalized * Vector3.Distance(mTank.Position,
                            new Vector3(item.Position.x, mTank.Position.y, item.Position.z)),
                            PhysicsUtils.LayerMaskScene))
                    {
                        continue;
                    }

                    if (Vector3.Distance(item.Position, mTank.Position) < missleDistance)
                        dealingMissile = item;
                }
            }

            if (dealingMissile != null)
            {
                var tmp = Physics.OverlapSphere(mTank.Position + mTank.Forward * 5f + Vector3.up * 3f,
                    2.5f, LayerMask.GetMask("Layer_Entity"));

                if (tmp.Length > 0 && tmp[0].GetComponentInParent<Missile>().Team != mTank.Team)
                {
                    esaPos = mTank.Position;
                }

                if (Vector3.Angle(AimAdvanceAmountPosition(mTank) - dealingMissile.Position,
                    dealingMissile.Velocity) < 30f)
                {
                    esaPos = mTank.Position;
                }

                if (Physics.Raycast(dealingMissile.Position, dealingMissile.Velocity.normalized,
                    Vector3.Distance(mTank.Position + Vector3.up * 3f, dealingMissile.Position) + 3f,
                    PhysicsUtils.LayerMaskTank))
                {
                    Vector3 avoidForward = mTank.Forward * 3f;
                    Vector3 avoidDir = Vector3.Cross(dealingMissile.Velocity.normalized, mTank.transform.up) * 7f;
                    float chooseSide = Vector3.Dot(avoidDir, mTank.Forward);
                    bool canUp = Physics.Raycast(mTank.Position, (avoidDir + avoidForward),
                        (avoidDir + avoidForward).magnitude, PhysicsUtils.LayerMaskScene);
                    bool canDown = Physics.Raycast(mTank.Position, (-avoidDir + avoidForward),
                        (-avoidDir + avoidForward).magnitude, PhysicsUtils.LayerMaskScene);

                    //两边都能跑
                    if (!canUp && !canDown)
                    {
                        //根据地形掩体选择规避方向
                        if (Physics.Raycast(mTank.Position + avoidDir + avoidForward, dealingMissile.Position, PhysicsUtils.LayerMaskScene))
                        {
                            esaPos = mTank.Position + avoidDir + avoidForward;

                        }

                        if (Physics.Raycast(mTank.Position - avoidDir + avoidForward, dealingMissile.Position, PhysicsUtils.LayerMaskScene))
                        {
                            esaPos = mTank.Position - avoidDir + avoidForward;

                        }

                        //根据车头朝向的偏好选择规避方向
                        if (chooseSide >= 0)
                        {
                            esaPos = mTank.Position + avoidDir + avoidForward;

                        }
                        else
                        {
                            esaPos = mTank.Position - avoidDir + avoidForward;

                        }
                    }                        //逃离位置是否存在墙壁？
                    else if (!canUp)
                    {

                        esaPos = mTank.Position + avoidDir + avoidForward;
                    }
                    else if (!canDown)
                    {
                        esaPos = mTank.Position - avoidDir + avoidForward;
                    }
                }
            }


            workingMemory.SetValue(5, esaPos);

            if (Vector3.Distance(mTank.Position, Match.instance.GetRebornPos(mTank.Team)) <=
                Match.instance.GlobalSetting.HomeZoneRadius)
            {
                workingMemory.DelValue(5);
            }

            return ERunningStatus.Finished;
        }

    }



    public override void action()
    {
        BehaviourTreeRunner.Exec(root, _obj, shareData);
    }

    public override void exitState(Tank enemey)
    {
        if (_obj.CanSeeOthers(_obj.enemy))
        {
            Debug.Log("开始攻击");
            shareData.Clear();
            _obj.switchState(_obj.attack);
        }
    }


}
