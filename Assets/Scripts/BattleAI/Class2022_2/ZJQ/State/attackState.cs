using Main;
using ZJQ;
using UnityEngine;
using AI.BehaviourTree;
using AI.Blackboard;
using AI.RuleBased;
using AI.Base;

public class attackState : FiniteStateMachine
{
    //行为树成员
    //0 => mTank
    //1 => enemy
    //2 => ?
    //3 => ?
    //4 => ?
    //5 => position
    private BlackboardMemory shareData;
    private Node root;
    //行为树修改
    public override void action()
    {
        BehaviourTreeRunner.Exec(root, _obj, shareData);
    }

    public override void enterState(MyTank obj)
    {
        _obj = obj;
        shareData = new BlackboardMemory();
        root = new ParallelNode(3).AddChild(
            new aimingNode(),
            new SequenceNode().AddChild(
                new avoidMissle(),
                new movePos()
                ),
            new attackNode().SetPrecondition(new canAttack())

            );
    }

    public override void exitState(Tank enemey)
    {
        if (_obj.enemy.IsDead || !_obj.CanSeeOthers(_obj.enemy))
        {
            shareData.Clear();
            _obj.switchState(_obj.finding);
        }

    }

    public class avoidMissle : ActionNode {
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
            Main.Missile lastMissle = null;



            foreach (var item in Match.instance.GetStars())
            {
                Star isSuper = item.Value;
                esaPos = isSuper.Position;
                return ERunningStatus.Finished;

            }

            if (!enemy.IsDead) {
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

            if (dealingMissile != null) {

/*                if (lastMissle != null) {
                    if (ReferenceEquals(dealingMissile, lastMissle)) {
                        return ERunningStatus.Finished;
                    }
                }*/
                var tmp = Physics.OverlapSphere(mTank.Position + mTank.Forward * 3f + Vector3.up * 3f,
                    2.5f, LayerMask.GetMask("Layer_Entity"));

                if (tmp.Length > 0 && tmp[0].GetComponentInParent<Missile>().Team != mTank.Team) { 
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
                    if (!canUp && !canDown) {
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
                    else if(!canUp)
                    {

                        esaPos = mTank.Position + avoidDir + avoidForward;
                    }
                    else if (!canDown)
                    {
                        esaPos = mTank.Position - avoidDir + avoidForward;
                    }
                }
            }




            if (Vector3.Distance(mTank.Position, Match.instance.GetRebornPos(mTank.Team)) <= 
                Match.instance.GlobalSetting.HomeZoneRadius) {
                esaPos = mTank.Position;
            }

            if (Vector3.Distance(mTank.Position, enemy.Position) < 10f) {
                esaPos = mTank.Position;


            }

            workingMemory.SetValue(5, esaPos);
            lastMissle = dealingMissile;

            return ERunningStatus.Finished;
        }

    }


    public class movePos : ActionNode {
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


    public class canAttack : Condition {
        public override bool IsTrue(IAgent agent)
        {
            Tank mTank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(mTank.Team);

            float distance = Vector3.Distance(mTank.Position, enemy.Position);
            float missileFlyingTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            Vector3 AimPos = enemy.Position + enemy.Velocity * missileFlyingTime * 0.3f;
            Vector3 direction = (AimPos - mTank.Position).normalized;
            bool enemyInAttackLine = Vector3.Dot(mTank.TurretAiming, direction) > 0.99f ? true : false;







            return enemyInAttackLine && mTank.CanFire();

        }

    }

    
    public class attackNode : ActionNode {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank mTank = null;
            workingMemory.TryGetValue<Tank>(0, out mTank);

            if (mTank != null)
                mTank.Fire();

            return ERunningStatus.Executing;
        }
    }



    public class aimingNode : ActionNode {
        Tank mTank;
        Tank enemy;
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            mTank = (Tank)agent;
            enemy = Match.instance.GetOppositeTank(mTank.Team);



            workingMemory.SetValue(0, mTank);
            workingMemory.SetValue(1, enemy);
            Vector3 targetPoint = AimAdvanceAmountPosition(enemy,0.35f);
            workingMemory.SetValue(3, targetPoint);

            if (!enemy.IsDead)
            {
                mTank.TurretTurnTo(targetPoint);
            }
            else {
                mTank.TurretTurnTo(mTank.Position + mTank.Forward);
            }

            return ERunningStatus.Executing;
            
        }

        public Vector3 AimAdvanceAmountPosition(Tank targetPos, float AdvanceWeight = 0.85f)
        {
            Vector3 AimPos;
            float distance = Vector3.Distance(mTank.Position, enemy.Position);
            float missileFlyingTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            //AdvanceWeight调和提前量和坦克位置
            AimPos = targetPos.Position + targetPos.Velocity * missileFlyingTime * AdvanceWeight;
            return AimPos;
        }


    }

    }

