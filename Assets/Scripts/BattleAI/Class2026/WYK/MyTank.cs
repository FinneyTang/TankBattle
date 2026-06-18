using System;
using System.Collections.Generic;
using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using AI.RuleBased;
using Main;
using UnityEngine;

namespace WYK
{
    //判断是否能看到敌人
    class ConditionCanSeeEnemy:Condition
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
    
    //根据HP判断是否需要回出生点补血
    class ConditionShouldGoHome:Condition
    {
        [SerializeField]
        public float dangerHealth = 30.0f;

        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            if(tank.HP == Match.instance.GlobalSetting.MaxHP)
            {
                return false;
            }
            if(enemy.IsDead)
            {
                return true;
            }
            if (tank.HP <= dangerHealth)
            {
                return true;
            }
            if (tank.HP <= enemy.HP - 2*Match.instance.GlobalSetting.DamagePerHit)
            {
                return true;
            }
            return false;
        }
    }
    
    //有星星
    class ConditionHasStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.GetStars().Count != 0;
        }

    }

    //将炮台转向预瞄点
    class ChangeTurretToPreAimPoint : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            
            if (enemy == null || enemy.IsDead)
            {
                tank.TurretTurnTo(tank.Position + tank.Forward);
                return ERunningStatus.Executing;
            }
            
            Transform turret = tank.transform.GetChild(1);
            
            // 2D坐标计算
            Vector2 enemyPosition = new Vector2(enemy.Position.x, enemy.Position.z);
            Vector2 enemyVelocity = new Vector2(enemy.Velocity.x, enemy.Velocity.z);
            Vector2 turretPosition = new Vector2(tank.FirePos.x, tank.FirePos.z);
            Vector2 delta = enemyPosition - turretPosition;
            
            float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
            
            // 正确的二次方程系数
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
            return ERunningStatus.Executing;
        }

    }

    //射击
    class Fire : ActionNode
    {
        Condition condition = new ConditionCanSeeEnemy();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            if(condition.IsTrue(agent)&&tank.CanFire())
            return true;
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            tank.Fire();
            return ERunningStatus.Executing;
        }
    }

    //回家治疗
    class Treat : ActionNode
    {
        private Condition condition = new ConditionShouldGoHome();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if(condition.IsTrue(agent))
            {
                return true;
            }
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            workingMemory.SetValue((int)Keys.MovingTargetPosation,Match.instance.GetRebornPos(tank.Team));
            return ERunningStatus.Finished;
        }

    }
   
    //吃星星
    class CatchStars : ActionNode
    {
        private Condition condition = new ConditionHasStar();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (condition.IsTrue(agent))
            {
                return true;
            }
            return false;
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Star targetStar = null;
            Dictionary<int,Star> stars = Match.instance.GetStars();
            foreach(var pair in stars)
            {
                Star star = pair.Value;
                if (star.IsSuperStar)
                {
                    targetStar = star;
                    break;
                }
                else
                {
                    float dist = Vector3.SqrMagnitude(star.Position-tank.Position);
                    if(targetStar == null)
                    {
                        targetStar = star;
                    }
                    else if(dist<Vector3.SqrMagnitude(targetStar.Position-tank.Position))
                    {
                        targetStar = star;
                    }
                }
            }
            workingMemory.SetValue((int)Keys.MovingTargetPosation, targetStar.Position);
            return ERunningStatus.Finished;
        }


    }

    //躲避导弹
    class EvadeMissile : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            var missiles = Match.instance.GetOppositeMissiles(tank.Team);
            
            if (missiles == null || missiles.Count == 0)
                return false;

            Missile nearestMissile = null;
            float minDist = float.MaxValue;
            
            foreach (var m in missiles)
            {
                float dist = Vector3.Distance(m.Value.Position, tank.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestMissile = m.Value;
                }
            }

            if (minDist > 20f)
                return false;

            if (nearestMissile != null)
            {
                Vector3 direction = Vector3.Cross(nearestMissile.Velocity, Vector3.up).normalized;
                
                if (Vector3.Cross(nearestMissile.Velocity, tank.Position - nearestMissile.Position).y > 0)
                    direction *= -1f;
                
                workingMemory.SetValue((int)Keys.MovingTargetPosation, tank.Position + direction * 10f);
                return true;
            }

            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            return ERunningStatus.Finished;
        }
    }
    
    class RandomMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            if (workingMemory.TryGetValue((int)Keys.MovingTargetPosation, out Vector3 pos))
            {
                if (Vector3.SqrMagnitude(pos - tank.Position) >= 1f)
                    return false;
            }
            workingMemory.SetValue((int)Keys.MovingTargetPosation, GetNextDestination(tank));
            return true;
        }

        private Vector3 GetNextDestination(Tank tank)
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return new Vector3(UnityEngine.Random.Range(-halfSize, halfSize), 0, UnityEngine.Random.Range(-halfSize, halfSize));
        }
    }

    //移动
    class Move : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return workingMemory.HasValue((int)Keys.MovingTargetPosation);
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            tank.Move(workingMemory.GetValue<Vector3>((int)Keys.MovingTargetPosation));
            return ERunningStatus.Finished;
        }

    }

    enum Keys
    {
        MovingTargetPosation
    }

    class MyTank:Tank
    {
        private BlackboardMemory m_Memory;
        private Node m_Node;

        protected override void OnStart()
        {
            base.OnStart();
            m_Memory = new BlackboardMemory();

            // 构建行为树
            m_Node = new ParallelNode(1).AddChild(
                new ChangeTurretToPreAimPoint(),
                new Fire(),
                new SequenceNode().AddChild(
                    new SelectorNode().AddChild(
                        new EvadeMissile(),
                        new Treat(),
                        new CatchStars(),
                        new RandomMove()
                    ),
                    new Move()
                )
            );
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            BehaviourTreeRunner.Exec(m_Node, this, m_Memory);
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            m_Memory.Clear();
        }

        public override string GetName()
        {
            return "WYK";
        }
    }
}