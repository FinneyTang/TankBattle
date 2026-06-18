using System;
using UnityEngine;
using Main;
using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using AI.RuleBased;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace WBQ
{
    //BlackBoard
    enum E_BBkey
    {
        TurretTargetDir,
        MovingTargetPos,
        NearestStar,
        IsSuperStar,
        IncomingMissile,
    }

    #region Condition

    class ConditionHasStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.GetStars().Count > 0;
        }
    }

    class ConditionShouldHeal : Condition
    {
        private Condition hasStarCondition = new ConditionHasStar();
        
        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);

            bool hasStar = hasStarCondition.IsTrue(agent);

            if (enemy.IsDead && enemy.GetRebornCD(Time.time) > 3f && hasStar) 
                return false;

            if (tank.HP <= 30) 
                return true;
                
            if (tank.HP < 70 && !hasStar && enemy.IsDead) 
                return true;
                
            if (tank.HP < 70 && Vector3.SqrMagnitude(tank.Position - Match.instance.GetRebornPos(tank.Team)) < 800f) 
                return true;
                
            if (tank.HP <= enemy.HP - 2 * Match.instance.GlobalSetting.DamagePerHit) 
                return true;
                
            if (enemy.IsDead && tank.HP < 81f) 
                return true;
                
            return false;
        }                           
    }

    #endregion
    
    #region ActionNode

    class TurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy != null && !enemy.IsDead)
            {
                Transform turret = tank.transform.GetChild(1);
                Vector2 enemyPos =  new Vector2(enemy.Position.x, enemy.Position.z);
                Vector2 enemyVelocity = new Vector2(enemy.Velocity.x, enemy.Velocity.z);
                Vector2 turretPos = new Vector2(tank.FirePos.x, tank.FirePos.z);
                Vector2 delta = enemyPos  - turretPos;

                float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
                float a = Vector2.Dot(enemyVelocity,enemyVelocity) - missileSpeed * missileSpeed;
                float b = 2.0f * Vector2.Dot(delta,enemyVelocity);
                float c = delta.sqrMagnitude;
                float discriminant = b * b - 4 * a * c;
                Vector3 targetDirection;

                if (discriminant >= 0)  //有根
                {
                    float sqrtD = Mathf.Sqrt(discriminant);
                    float time = (-b -  sqrtD) / (2 * a);
                    if (time < 0) time = (-b + sqrtD) / (2 * a);
                    if (time > 0)
                    {
                        Vector2 predictPos = enemyPos +  enemyVelocity * time;
                        Vector3 aimPoint = new Vector3(predictPos.x, tank.FirePos.y,predictPos.y);
                        targetDirection = (aimPoint - turret.position).normalized;
                    }
                    else //根为0
                    {
                        targetDirection = (enemy.Position - turret.position).normalized;
                    }
                }
                else //无根
                {
                    targetDirection = (enemy.Position - turret.position).normalized;
                }
                turret.forward = Vector3.Lerp(turret.forward, targetDirection, Time.deltaTime * 720f);
                workingMemory.SetValue((int)E_BBkey.TurretTargetDir, targetDirection);
            }
            else //无敌人
            {
                tank.TurretTurnTo(tank.Position + tank.Forward);       
            }
            return ERunningStatus.Executing;
        }
    }

    class Fire : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy =  Match.instance.GetOppositeTank(tank.Team);
            Vector3 targetDir = workingMemory.GetValue<Vector3>((int)E_BBkey.TurretTargetDir);
            float realDis = Vector3.Distance(tank.Position, enemy.Position);
            
            //敌人离家太近
            if (Vector3.SqrMagnitude(enemy.Position - Match.instance.GetRebornPos(enemy.Team)) < 200f)
            {
                return false;
            }
            
            //离敌人太近,肉搏
            if (realDis < 15f)
            {
                return tank.CanFire();
            }
            
            // 动态开火角，远距离精准，近距离放宽
            float maxFireAngle = Mathf.Lerp(8f,3.5f,(realDis - 15f) / 30f);
            maxFireAngle = Mathf.Clamp(maxFireAngle, 3.5f, 8f);
            
            //射出一个球体，检测炮塔瞄准方向是否被墙体遮挡
            float castDis = realDis - 2f;
            if (castDis <= 0) castDis = 0.1f;
            if (Physics.SphereCast(tank.Position,0.24f,targetDir,out  RaycastHit hit, castDis))
            {
                //有遮挡
                FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                if (fireCollider != null)   //有遮挡，但是是坦克
                {
                    if (Vector3.Angle(tank.TurretAiming,targetDir) < maxFireAngle)
                    {
                        return tank.CanFire();
                    }

                    return false;
                }
            }
            else
            {
                //无遮挡
                if (Vector3.Angle(tank.TurretAiming,targetDir) < maxFireAngle)
                {
                    return tank.CanFire();
                }
            }
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            tank.Fire();
            return ERunningStatus.Executing;
        }
    }

    class Move : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return workingMemory.HasValue((int)E_BBkey.MovingTargetPos);
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            // 回归最原始的移动，底盘绝对稳定，射击精度最大化
            tank.Move(workingMemory.GetValue<Vector3>((int)E_BBkey.MovingTargetPos));
            return ERunningStatus.Finished;
        }
    }

    class Patrol : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            
            if (workingMemory.TryGetValue((int)E_BBkey.MovingTargetPos,out Vector3 targetPos))
            {
                if (Vector3.SqrMagnitude(tank.Position - targetPos) >= 1f)
                {
                    //有目标，并且没有到达目标
                    return true;
                }
            }
            
            //没有目标 或 有目标但是到达目标
            workingMemory.SetValue((int)E_BBkey.MovingTargetPos,GetTargetPos(tank));
            return true;
        }

        private Vector3 GetTargetPos(Tank tank)
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            
            //随机采样5次，取离家最近的，保证可以即使回家补血
            float  minDis = float.MaxValue;
            Vector3 home =  Match.instance.GetRebornPos(tank.Team);
            Vector3 bestPos = home;
            for (int i = 0; i < 5; i++)
            {
                Vector3 randomPos = new Vector3(Random.Range(-halfSize, halfSize),0,Random.Range(-halfSize, halfSize));
                //网格吸附，随机生成的点不会卡在墙里，让坦克卡住
                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    float dis = Vector3.SqrMagnitude(hit.position - home);
                    if (dis < minDis)
                    {
                        bestPos = hit.position;
                        minDis = dis;
                    }
                }
                
            }
            return bestPos;
        }
    }

    class FindStar : ActionNode
    {
        private Condition hasStar = new ConditionHasStar();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            //场上没有星星
            if (!hasStar.IsTrue(tank)) return false;

            float highestScore = -float.MaxValue;
            Star bestStar = null;
            bool isSuperStar = false;

            foreach (var star in Match.instance.GetStars())
            {
                Star s = star.Value;
                //优先抢超级星星，有超级星星直接跳出
                if (s.IsSuperStar)
                {
                    bestStar = s;
                    isSuperStar = true;
                    break;
                }
                
                float mySqrDis = (s.Position - tank.Position).sqrMagnitude;
                //规避敌人截胡
                if (enemy != null && !enemy.IsDead)
                {
                    float enemySqrDis = (s.Position - enemy.Position).sqrMagnitude;
                    if (mySqrDis > enemySqrDis) continue;
                }
                
                //星星密度计算
                int density = 0;
                foreach (var other in Match.instance.GetStars().Values)
                {
                    if (Vector3.SqrMagnitude(other.Position - s.Position) < 400f)
                        density++;
                }
                
                //效用综合打分
                float score = (1000f / Mathf.Max(1f,Mathf.Sqrt(mySqrDis))) + (density * 20f);
                if (score > highestScore)
                {
                    highestScore = score;
                    bestStar = s;
                    isSuperStar = false; // 修正了之前的一处小笔误，这里应当为false
                }
            }
            
            //保底：全部星星都在敌人附近
            if (bestStar == null)
            {
                //找个距离最近的碰运气
                float minDis = float.MaxValue;
                foreach (var star in Match.instance.GetStars().Values)
                {
                    float d = (star.Position - tank.Position).sqrMagnitude;
                    if (d < minDis)
                    {
                        minDis = d;
                        bestStar = star;
                    }
                }
            }

            if (bestStar != null)
            {
                workingMemory.SetValue((int)E_BBkey.MovingTargetPos, bestStar.Position);
                workingMemory.SetValue((int)E_BBkey.NearestStar, bestStar);
                workingMemory.SetValue((int)E_BBkey.IsSuperStar, isSuperStar);
                return true;
            }

            return false;
        }
    }

    class Chase : ActionNode
    {
        private Tank enemy;

        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            enemy = Match.instance.GetOppositeTank(tank.Team);
            var enemyHome = Match.instance.GetRebornPos(enemy.Team);

            if (enemy && !enemy.IsDead && (enemy.HP < tank.HP - Match.instance.GlobalSetting.DamagePerHit || enemy.HP <= Match.instance.GlobalSetting.DamagePerHit) 
                && Vector3.SqrMagnitude(enemy.Position - enemyHome) > 200f)  
            {
                return true;
            }
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            if (enemy)
            {
                workingMemory.SetValue((int)E_BBkey.MovingTargetPos,enemy.Position);
                return ERunningStatus.Finished;
            }
            
            return ERunningStatus.Failed;
        }
    }

    class Heal : ActionNode
    {
        private Condition shouldHeal = new ConditionShouldHeal();

        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;

            if (workingMemory.TryGetValue((int)E_BBkey.NearestStar,out Star star))
            {
                if (star != null && Vector3.SqrMagnitude(star.Position - tank.Position) < 300f)
                {
                    return false;
                }
            }

            if (workingMemory.TryGetValue((int)E_BBkey.IsSuperStar,out bool isSuperStar))
            {
                if (isSuperStar) return false;
            }

            if (shouldHeal.IsTrue(agent))
            {
                workingMemory.SetValue((int)E_BBkey.MovingTargetPos,Match.instance.GetRebornPos(tank.Team));
                return true;
            }
            
            return false;
        }
    }

    class GoToSuperStar : ActionNode
    {
        //制胜之术，快到时间，为抢星星做准备
        private float superStarSpawnTime = Match.instance.GlobalSetting.MatchTime * 0.5f;

        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            float remainingTime = Match.instance.RemainingTime;
            float diff =  remainingTime - superStarSpawnTime;
            if (diff > 0 && diff < 7f)
            {
                return true;
            }
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            workingMemory.SetValue((int)E_BBkey.MovingTargetPos,Vector3.zero);
            return ERunningStatus.Finished;
        }
    }

    class EvadeMissile : ActionNode
    {
        override protected bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy =  Match.instance.GetOppositeTank(tank.Team);
            float missileDamage = Match.instance.GlobalSetting.DamagePerHit;

            if (enemy.HP <= missileDamage && tank.HP >= 2 * missileDamage)
            {
                return false;
            }
            
            var missiles = Match.instance.GetOppositeMissiles(tank.Team);
            Missile missile = null;
            float minDis = float.MaxValue;
            foreach (var m in missiles)
            {
                if (!CanHit(m.Value,tank)) continue;
                float dis = Vector3.Distance(m.Value.Position, tank.Position);
                if (missile is null || dis < minDis)
                {
                    missile = m.Value;
                    minDis = dis;
                }
            }

            if (minDis < 10f) return false;

            if (missile != null)
            {
                workingMemory.SetValue((int)E_BBkey.IncomingMissile,missile);
                return true;
            }
            
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            if (workingMemory.TryGetValue((int)E_BBkey.IncomingMissile, out Missile missile))
            {
                Vector3 dir = Vector3.Cross(missile.Velocity,Vector3.up).normalized;
                var tank = (Tank)agent;
                //判断应该向左躲还是向右躲
                if (Vector3.Cross(missile.Velocity,tank.Position - missile.Position).y > 0)
                {
                    dir *= -1.0f;
                }
                workingMemory.SetValue((int)E_BBkey.MovingTargetPos,tank.Position + dir * 4f);
            }
            return ERunningStatus.Finished;
        }

        private bool CanHit(Missile missile, Tank tank)
        {
            //距离太远
            if (Vector3.SqrMagnitude(missile.Position - tank.Position) > 900f)
                return false;
            //方向大于90度，打不着
            if (Vector3.Dot(tank.Position - missile.Position,missile.Velocity) < -0.1f)
                return false;
            //先打线，粗筛一边导弹路径是否有墙
            if (Physics.Linecast(missile.Position,tank.Position,PhysicsUtils.LayerMaskScene))
            {
                return false;
            }
            //打球体，拿到路径上所有东西
            var hits = Physics.SphereCastAll(missile.Position,1.0f,missile.Velocity,60f);
            if (hits.Length > 0)
            {
                //找到距离导弹最近的碰撞体
                float closestDis = float.MaxValue;
                FireCollider closestCollider = null;
                
                foreach (var hit in hits)
                {
                    if (hit.collider.isTrigger) continue;   //星星
                
                    float dis = Vector3.Distance(missile.Position,hit.point);
                    if (dis < closestDis)
                    {
                        closestDis = dis;
                        closestCollider = hit.transform.gameObject.GetComponent<FireCollider>();
                    }
                }
                //找到了最近的碰撞体
                if (closestCollider != null)
                {
                    //有别的东西挡在自己前面
                    if (closestCollider.Owner != tank) 
                        return false;
                    if (closestCollider.Owner ==  tank) 
                        return true;
                    
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
    }

    #endregion
    
    public class MyTank : Tank
    {
        private BlackboardMemory m_Blackboard;
        private Node m_Node;

        protected override void OnStart()
        {
            base.OnStart();
            m_Blackboard = new BlackboardMemory();
            m_Node = new ParallelNode(1).AddChild(
                        new TurnTurret(),
                        new Fire(),
                        new SequenceNode().AddChild(
                            new SelectorNode().AddChild(
                                new EvadeMissile(),
                                new GoToSuperStar(),
                                new Heal(),
                                new Chase(),
                                new FindStar(),
                                new Patrol()
                                ),
                            new Move()));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            BehaviourTreeRunner.Exec(m_Node,this, m_Blackboard);
        }

        public override string GetName()
        {
            return "WBQ";
        }
    }
}