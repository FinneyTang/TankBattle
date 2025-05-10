using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using AI.RuleBased;
using Main;
using UnityEngine;

namespace SM
{
    
    class ConditionCanSeeEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy != null)
            {
                return tank.CanSeeOthers(enemy);
            }
            return false;
        }
    }

    class ConditionShouldHeal : Condition
    {
        private Condition hasStarCondition = new ConditionHasStarOnMatch();
        
        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);

            bool hasStar = hasStarCondition.IsTrue(agent);
            
            if (enemy.IsDead && enemy.GetRebornCD(Time.time) > 3.0f && hasStar)
                return false;
            
            if (tank.HP <= 30)
                return true; 

            if (tank.HP < 70 && !hasStar && enemy.IsDead)
                return true;

            if (tank.HP < 70 &&
                Vector3.SqrMagnitude(tank.Position - Match.instance.GetRebornPos(tank.Team)) < 800.0f)
                return true;

            if (tank.HP <= enemy.HP - 2 * Match.instance.GlobalSetting.DamagePerHit)
                return true;

            if (enemy.IsDead && tank.HP < 81.0f)
                return true;
            
            return false;
        }
    }

    class ConditionHasStarOnMatch : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.GetStars().Count != 0;
        }
    }
    
    class TurnTurret : ActionNode
    {

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemroy)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy != null && enemy.IsDead == false)
            {
                Transform turret = tank.transform.GetChild(1);
                Vector2 enemyPosition = new Vector2(enemy.Position.x, enemy.Position.z);
                Vector2 enemyVelocity = new Vector2(enemy.Velocity.x, enemy.Velocity.z);
                Vector2 turretPosition = new Vector2(tank.FirePos.x, tank.FirePos.z);
                Vector2 delta = enemyPosition - turretPosition;
                
                float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
                float a = Vector2.Dot(enemyVelocity, enemyVelocity) - missileSpeed * missileSpeed;
                float b = 2 * Vector2.Dot(delta, enemyVelocity);
                float c = delta.sqrMagnitude;

                float discriminant = b * b - 4 * a * c;
                Vector3 targetDirection;

                if (discriminant >= 0)
                {
                    float sqrtD = Mathf.Sqrt(discriminant);
                    float time = (-b - sqrtD) / (2 * a);
                    if (time < 0) time = (-b + sqrtD) / (2 * a);
                    if (time > 0)
                    {
                        Vector2 intercept = enemyPosition + enemyVelocity * time;
                        Vector3 aimPoint = new Vector3(intercept.x, tank.FirePos.y, intercept.y);
                        targetDirection = (aimPoint - turret.position).normalized;
                    }
                    else
                    {
                        targetDirection = (enemy.Position - turret.position).normalized;
                    }
                }
                else
                {
                    targetDirection = (enemy.Position - turret.position).normalized;
                }

                turret.forward = Vector3.Lerp(turret.forward, targetDirection, Time.deltaTime * 720);
                //tank.TurretTurnTo(targetDirection);
            }
            else
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
            var enemy = Match.instance.GetOppositeTank(tank.Team);
            var targetDirection = enemy.Position - tank.Position;
            if (Vector3.SqrMagnitude(enemy.Position - Match.instance.GetRebornPos(enemy.Team)) < 200.0)
                return false;

            if ((tank.Position - enemy.Position).magnitude < 15)
            {
                return tank.CanFire();
            }
            else if (Physics.SphereCast(tank.Position, 0.24f, targetDirection, out RaycastHit hit,
                         (targetDirection - tank.Position).magnitude - 2))
            {
                FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                if (fireCollider != null)
                {
                    if (Vector3.Angle(tank.TurretAiming, targetDirection) < 20)
                        return tank.CanFire();
                }
            }
            else
            {
                if (Vector3.Angle(tank.TurretAiming, targetDirection) < 20)
                    return tank.CanFire();
            }
            
            return false;
        }
        
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemroy)
        {
            Tank tank = (Tank)agent;
            tank.Fire();
            return ERunningStatus.Executing;
        }
    }
    
    class Heal : ActionNode
    {
        private Condition c = new ConditionShouldHeal();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;

            if (workingMemory.TryGetValue((int)EBBKey.NearestStar, out Star star))
            {
                if (star != null && Vector3.SqrMagnitude(star.Position - tank.Position) < 300.0f)
                    return false;
            }
            
            if (workingMemory.TryGetValue((int)EBBKey.IsSuperStar, out bool isSuperStar))
            {
                if(isSuperStar)
                    return false;
            }
            
            if(c.IsTrue(agent))
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, Match.instance.GetRebornPos(tank.Team));
                return true;
            }
            
            return false;
        }
    }
    
    class FindStar : ActionNode
    {
        private Condition hasStarCondition = new ConditionHasStarOnMatch();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;

            if (!hasStarCondition.IsTrue(agent))
            {
                return false;
            }
            
            float nearestDist = float.MaxValue;
            int mostNearbyStars = 0;
            Star bestStar = null;
            bool isSuperStar = false;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    bestStar = s;
                    isSuperStar = true;
                    break;
                }
                else
                {
                    float dist = (s.Position - tank.Position).sqrMagnitude;
                    
                    int currentNearbyStars = 0;
                    foreach (var newPair in Match.instance.GetStars())
                    {
                        if (Vector3.SqrMagnitude(newPair.Value.Position - s.Position) < 400.0f)
                            ++currentNearbyStars;
                    }

                    if (currentNearbyStars > mostNearbyStars && dist < nearestDist)
                    {
                        bestStar = s;
                        nearestDist = dist;
                        mostNearbyStars = currentNearbyStars;
                        isSuperStar = false;
                    }
                    else if (currentNearbyStars - mostNearbyStars > 2)
                    {
                        bestStar = s;
                        mostNearbyStars = currentNearbyStars;
                        isSuperStar = false;
                    }
                    else if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        bestStar = s;
                        isSuperStar = false;
                    }
                }
            }
      
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, bestStar.Position);
            workingMemory.SetValue((int)EBBKey.NearestStar, bestStar);
            workingMemory.SetValue((int)EBBKey.IsSuperStar, isSuperStar);
            return true;
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
      
            if (enemy && !enemy.IsDead && (enemy.HP <= tank.HP - Match.instance.GlobalSetting.DamagePerHit ||
                                  enemy.HP <= Match.instance.GlobalSetting.DamagePerHit)
                && Vector3.SqrMagnitude(enemy.Position - enemyHome) < 200.0f)
            {
                return true;
            }

            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            if (enemy)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, enemy.Position);
                return ERunningStatus.Finished;
            }

            return ERunningStatus.Failed;
        }
    }
    
    class GetRandomPosition : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            
            if(workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out Vector3 position))
            {
                if(Vector3.SqrMagnitude(position - tank.Position) >= 1f)
                {
                    return false;
                }
            }
            
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, GetNextDestination(tank));
            return true;
        }
        private Vector3 GetNextDestination(Tank tank)
        {
            float halfSize = Match.instance.FieldSize * 0.5f;

            Vector3 best = Vector3.zero;
            float minimalDist = float.MaxValue;
            Vector3 home = Match.instance.GetRebornPos(tank.Team);
            for (int i = 0; i < 3; i++)
            {
                var position = new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
                float dist = Vector3.SqrMagnitude(position - home);
                if (dist < minimalDist)
                {
                    best = position;
                    minimalDist = dist;
                }
            }
            
            return best;
        }
    }
    
    class Move : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return workingMemory.HasValue((int)EBBKey.MovingTargetPos);
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            
            Debug.Log(workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos));
            tank.Move(workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos));
            return ERunningStatus.Finished;
        }
    }

    class GoToSuperStarPosition : ActionNode
    {
        private float superStarSpawnTime = Match.instance.GlobalSetting.MatchTime * 0.5f;
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            var remainingTime = Match.instance.RemainingTime;
            var difference = remainingTime - superStarSpawnTime;
            if (difference > 0 && difference < 7.0f)
                return true;
            return false;
        }
        
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, Vector3.zero);
            return ERunningStatus.Finished;
        }
    }
    
    class EvadeMissile : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            float missileDamage = Match.instance.GlobalSetting.DamagePerHit;

            if (enemy.HP <= missileDamage && tank.HP >= 2 * missileDamage)
                return false;
            
            var missiles = Match.instance.GetOppositeMissiles(tank.Team);
            Missile missile = null;
            float minDist = float.MaxValue;
            foreach (var m in missiles)
            {
                if(!CanHit(m.Value, tank))
                    continue;
                float dist = Vector3.Distance(m.Value.Position, tank.Position);
                if (missile is null || dist < minDist)
                {
                    missile = m.Value;
                    minDist = dist;
                }
            }

            if (minDist < 10.0f)
                return false;
            
            if (missile is not null)
            {
                workingMemory.SetValue((int)EBBKey.IncomingMissile, missile);
                return true;
            }
            
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            if (workingMemory.TryGetValue((int)EBBKey.IncomingMissile, out Missile missile))
            {
                
                var direction = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                var tank = (Tank)agent;
                if (Vector3.Cross(missile.Velocity, tank.Position - missile.Position).y > 0)
                    direction *= -1.0f;
                
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, tank.Position + direction * 4f);
                
            }
            return ERunningStatus.Finished;
        }

        private bool CanHit(Missile missile, Tank tank)
        {
            if (Vector3.SqrMagnitude(missile.Position - tank.Position) > 900.0f)
                return false;
            if (Vector3.Dot(tank.Position - missile.Position, missile.Velocity) < -0.1f)
                return false;
            if (Physics.Linecast(missile.Position, tank.Position, PhysicsUtils.LayerMaskScene))
            {
                return false;
            }

            var hits = Physics.SphereCastAll(missile.Position, 1.0f, missile.Velocity, 60.0f);
            if(hits.Length > 0)
            {
                foreach (var hit in hits)
                {
                    var collider = hit.transform.GetComponent<FireCollider>();
                    
                    if (collider is not null && collider.Owner != tank)
                        continue;
                    else
                        break;
                }
            }
            
            return true;
        }
    }

    enum EBBKey
    {
        MovingTargetPos,
        IncomingMissile,
        NearestStar,
        IsSuperStar
    }
    
    public class MyTank : Tank
    {
        
        private BlackboardMemory memory;
        private Node node;
        public override string GetName()
        {
            return "SM";
        }

        protected override void OnStart()
        {
            base.OnStart();
            memory = new BlackboardMemory();

            node = new ParallelNode(1).AddChild(
                new TurnTurret(),
                new Fire(),
                new SequenceNode().AddChild(
                    new SelectorNode().AddChild(
                        new EvadeMissile(),
                        new GoToSuperStarPosition(),
                        new Heal(),
                        new Chase(),
                        new FindStar(),
                        new GetRandomPosition()),
                    new Move()));
        }

        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(node, this, memory);
        }
    }
}