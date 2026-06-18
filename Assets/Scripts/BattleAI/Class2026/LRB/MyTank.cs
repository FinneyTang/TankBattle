using System.Collections;
using System.Collections.Generic;
using Main;
using AI.RuleBased;
using UnityEngine;
using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using System;

namespace LRB
{
    #region 规则Condition
    class ConditionCanSeeEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);//MAKRER Opponent对手的/对手 Opposite相反的/对立面
            if (enemy != null)
            {
                return tank.CanSeeOthers(enemy);
            }
            return false;//
        }
    }
    class ConditionHasStarOnMatch : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.GetStars().Count != 0;
        }
    }
    class ConditionCloseCombatMode : Condition
    {
        public const float EnterDistance = 30f;
        public const float ExitDistance = 35f;
        private const float SuperStarCriticalWindow = 15f;
        private const bool EnableDebugLog = true;

        private bool m_inCloseCombat = false;
        private bool m_lastOutput = false;
        private string m_lastBlockReason = string.Empty;

        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null || enemy.IsDead)
            {
                m_inCloseCombat = false;
                LogModeChange(false, "EnemyUnavailable");
                return false;
            }

            bool prevCloseCombat = m_inCloseCombat;
            float distance = Vector3.Distance(tank.Position, enemy.Position);
            if (!m_inCloseCombat && distance <= EnterDistance)
            {
                m_inCloseCombat = true;
            }
            else if (m_inCloseCombat && distance >= ExitDistance)
            {
                m_inCloseCombat = false;
            }

            bool hpGate = IsHPNotDisadvantaged(tank, enemy);
            bool fireCdGate = IsEnemyInFireCooldown(enemy);
            bool notSuperStarWindowGate = !IsInSuperStarCriticalWindow();

            bool result = m_inCloseCombat && hpGate && fireCdGate;

            if (EnableDebugLog && m_inCloseCombat && !result)
            {
                string reason = hpGate ? (fireCdGate ? "SuperStarCriticalWindow" : "EnemyCanFire") : "HPDisadvantage";
                if (reason != m_lastBlockReason)
                {
                    m_lastBlockReason = reason;
                    Debug.Log($"[CloseCombatMode] blocked by {reason}");
                }
            }
            else
            {
                m_lastBlockReason = string.Empty;
            }

            if (prevCloseCombat != m_inCloseCombat)
            {
                LogModeChange(result, m_inCloseCombat ? "EnterDistance" : "ExitDistance");
            }
            else if (m_lastOutput != result)
            {
                LogModeChange(result, "GateChanged");
            }

            m_lastOutput = result;
            return result;
        }

        private static bool IsHPNotDisadvantaged(Tank tank, Tank enemy)
        {
            return tank.HP - enemy.HP > 1 * Match.instance.GlobalSetting.DamagePerHit;
        }

        private static bool IsEnemyInFireCooldown(Tank enemy)
        {
            return !enemy.CanFire();
        }

        private static bool IsInSuperStarCriticalWindow()
        {
            float remainingTime = Match.instance.RemainingTime;
            float superStarSpawnTime = Match.instance.GlobalSetting.MatchTime * 0.5f;
            float delta = remainingTime - superStarSpawnTime;
            return delta > 0f && delta <= SuperStarCriticalWindow;
        }

        private static void LogModeChange(bool output, string reason)
        {
            Debug.Log($"[CloseCombatMode] output={output}, reason={reason}");
        }
    }
    class ConditionShouldHeal : Condition//CORE 当下回血回到满再进攻
    {
        private Condition hasStarCondition = new ConditionHasStarOnMatch();
        private const int EnterHealHP = 40;
        private const int ExitHealHP  = 80;
        private bool m_isHealing = false;
        
        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            bool hasStar = hasStarCondition.IsTrue(agent);
            // 已在回血模式：没到退出线就继续回血
            if (m_isHealing)
            {
                if (tank.HP < ExitHealHP)
                {
                    return true;
                } 
                m_isHealing = false;
                Debug.LogWarning("<color=black>退出回血模式</color>");
            }

            //MARKER 必须回血的阈值
            if (!m_isHealing && tank.HP <= EnterHealHP)
            {
                Debug.LogWarning("<color=red>必须回血了</color>");
                m_isHealing = true;
                return true;
            }
            
            if (enemy.IsDead && enemy.GetRebornCD(Time.time) > 6.0f && hasStar)
            {
                Debug.LogWarning("<color=red>敌人还有6秒复活有星星也先回家补血</color>");
                m_isHealing = false;
                return false;
            }         
            if (tank.HP <= 60 && !hasStar && enemy.IsDead)
            {
                m_isHealing = true;
                return true;
            } 
            if (tank.HP <= 60 &&
                Vector3.SqrMagnitude(tank.Position - Match.instance.GetRebornPos(tank.Team)) < 900.0f)
            {
                Debug.LogWarning("<color=red>离家近顺便回血</color>");
                m_isHealing = true;
                return true;
            }
            if (tank.HP <= enemy.HP - 2 * Match.instance.GlobalSetting.DamagePerHit)
            {
                Debug.LogWarning("<color=red>敌方血量有优势</color>");
                m_isHealing = true;
                return true;
            }
            if (enemy.IsDead && tank.HP < 90.0f)
            {
                m_isHealing = true;
                return true;
            }
            
            return false;
        }
    }
    #endregion
    #region 行为节点
    class TurnTurret : ActionNode//TODO 优化炮管瞄准逻辑
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            Vector3 opponentHomePos = Match.instance.GetRebornPos(enemy.Team);
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
            else if (opponentHomePos != null)
            {
                tank.TurretTurnTo(opponentHomePos);
            }
            else
            {
                tank.TurretTurnTo(tank.Position + tank.Forward);
            }
            return ERunningStatus.Executing;
        }
    }
    class Fire : ActionNode//TODO 添加前置条件，看到敌人再开火等等
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
    class EvadeMissile : ActionNode//TODO 优化躲避逻辑
    {
        private const float DangerRadius = 15f;
        private const float AlertRadius = 30f;
        private const float MaxThreatLookaheadTime = 1.2f;
        private const float WallCheckMargin = 0.2f;

        private enum EThreatLevel
        {
            None = 0,
            Alert = 1,
            Danger = 2
        }

        private struct MissileThreatInfo
        {
            public Missile Missile;
            public EThreatLevel ThreatLevel;
            public float ClosestDist;
            public float TimeToClosest;
        }
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            float missileDamage = Match.instance.GlobalSetting.DamagePerHit;

            if (enemy.HP <= missileDamage && tank.HP >= 2 * missileDamage)
                return false;
            
            var missiles = Match.instance.GetOppositeMissiles(tank.Team);
            bool hasThreat = false;
            MissileThreatInfo bestThreat = default;

            foreach (var m in missiles)
            {
                MissileThreatInfo threat;
                if (!TryGetThreatInfo(m.Value, tank, out threat))
                    continue;

                if (!hasThreat || IsBetterThreat(threat, bestThreat))
                {
                    bestThreat = threat;
                    hasThreat = true;
                }
            }
            if (hasThreat)
            {
                workingMemory.SetValue((int)EBBKey.IncomingMissile, bestThreat.Missile);
                return true;
            }

            return false;
            /* Missile missile = null;
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
            
            return false; */
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            if (workingMemory.TryGetValue((int)EBBKey.IncomingMissile, out Missile missile))
            {
                
                var direction = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                var tank = (Tank)agent;
                if (Vector3.Cross(missile.Velocity, tank.Position - missile.Position).y > 0)
                    direction *= -1.0f;
                
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, tank.Position + direction * 3.5f);
                
            }
            return ERunningStatus.Finished;
        }

        private bool TryGetThreatInfo(Missile missile, Tank tank, out MissileThreatInfo info)
        {
            info = default;
            if (missile == null || tank == null)
                return false;

            if (Vector3.SqrMagnitude(missile.Position - tank.Position) > 900.0f)
                return false;

            Vector3 velocity = missile.Velocity;
            float speedSqr = velocity.sqrMagnitude;
            if (speedSqr < 0.0001f)
                return false;

            Vector3 toTank = tank.Position - missile.Position;
            float rawTClosest = Vector3.Dot(toTank, velocity) / speedSqr;
            if (rawTClosest <= 0f)
                return false;

            float tClosest = Mathf.Min(rawTClosest, MaxThreatLookaheadTime);
            Vector3 closestPoint = missile.Position + velocity * tClosest;
            float closestDist = Vector3.Distance(closestPoint, tank.Position);
            if (closestDist > AlertRadius)
                return false;

            float travelDist = velocity.magnitude * tClosest;
            if (travelDist > 0f &&
                Physics.SphereCast(missile.Position, 0.12f, velocity.normalized, out RaycastHit hit,
                    travelDist + WallCheckMargin, PhysicsUtils.LayerMaskScene))
            {
                return false;
            }

            info.Missile = missile;
            info.ClosestDist = closestDist;
            info.TimeToClosest = tClosest;
            info.ThreatLevel = closestDist <= DangerRadius ? EThreatLevel.Danger : EThreatLevel.Alert;
            return true;
        }

        private bool IsBetterThreat(MissileThreatInfo candidate, MissileThreatInfo current)
        {
            if (candidate.ThreatLevel != current.ThreatLevel)
                return candidate.ThreatLevel > current.ThreatLevel;

            const float timeEpsilon = 0.05f;
            float timeDelta = candidate.TimeToClosest - current.TimeToClosest;
            if (Mathf.Abs(timeDelta) > timeEpsilon)
                return candidate.TimeToClosest < current.TimeToClosest;

            return candidate.ClosestDist < current.ClosestDist;
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
    class Heal : ActionNode
    {
        private Condition c = new ConditionShouldHeal();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;

            if (workingMemory.TryGetValue((int)EBBKey.NearestStar, out Star star))
            {
                if (star != null && Vector3.SqrMagnitude(star.Position - tank.Position) < 250.0f)
                    return false;
            }
            
            if (workingMemory.TryGetValue((int)EBBKey.IsSuperStar, out bool isSuperStar))//MARKER 有超级星星不补血
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
            Tank enemy = Match.instance.GetOppositeTank(tank.Team);
            bool hasValidStar = false;
            if (!hasStarCondition.IsTrue(agent))//STEP 1 没有星星就不找
            {
                return false;
            }
            float nearestDist = float.MaxValue;
            int maxQuantityStarGroupStarCount = 0;
            Star bestStar = null;
            bool isSuperStar = false;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s1 = pair.Value;
                if (s1.IsSuperStar)//STEP 2 判断是否为超级星星
                {
                    bestStar = s1;
                    isSuperStar = true;
                    hasValidStar = true;
                    break;
                }
                else if (Vector3.SqrMagnitude(s1.Position - Match.instance.GetRebornPos(enemy.Team)) < 500f)//MARKER 如果星星在距离敌人出生点2个单位左右不去捡
                {
                    hasValidStar = false;
                    continue;
                }
                else
                {
                    hasValidStar = true;
                    float dist = (s1.Position - tank.Position).sqrMagnitude;
                    int currentNearbyStars = 0;
                    foreach (var newPair in Match.instance.GetStars())//STEP 3 计算普通星星附近的星星数
                    {
                        Star s2 = newPair.Value;
                        if (Vector3.SqrMagnitude(s2.Position - s1.Position) < 400.0f)//MARKER 小于2个地图单位
                        {
                            currentNearbyStars++;
                        }
                    }
                    //STEP 4 筛选计算最大密度星群
                    if (currentNearbyStars > maxQuantityStarGroupStarCount && dist < nearestDist)//STEP 5 密度大同时距离近
                    {
                        bestStar = s1;
                        nearestDist = dist;
                        maxQuantityStarGroupStarCount = currentNearbyStars;
                        isSuperStar = false;
                    }
                    else if (currentNearbyStars - maxQuantityStarGroupStarCount > 2)//STEP 6 密度大
                    {
                        bestStar = s1;
                        maxQuantityStarGroupStarCount = currentNearbyStars;
                        isSuperStar = false;
                    }
                    else if (dist < nearestDist)//STEP 7 距离近
                    {
                        nearestDist = dist;
                        bestStar = s1;
                        isSuperStar = false;
                    }
                }
            }
            if (hasValidStar)
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, bestStar.Position);
                workingMemory.SetValue((int)EBBKey.NearestStar, bestStar);
                workingMemory.SetValue((int)EBBKey.IsSuperStar, isSuperStar);
            }
            else
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, Match.instance.GetRebornPos(tank.Team));
                workingMemory.SetValue((int)EBBKey.NearestStar, null);
                workingMemory.SetValue((int)EBBKey.IsSuperStar, false);
            }
            return true;
        }
    }
    class GoToSuperStarPosition : ActionNode//已实现另外一个，这个作为参考
    {
        private float superStarSpawnTime = Match.instance.GlobalSetting.MatchTime * 0.5f;
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            var remainingTime = Match.instance.RemainingTime;
            var difference = remainingTime - superStarSpawnTime;
            if (difference > 0 && difference < 15.0f)
                return true;
            return false;
        }
        
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, Vector3.zero);
            return ERunningStatus.Finished;
        }
    }
    class MoveToSuperStarPosition : ActionNode
    {
        /* private enum EPreSpawnPhase
        {
            None,       
            Far,        
            Mid,        
            Near,       
            LockCenter  
        } */
        private const float moveToCenterWindow = 7f;
        private const float canActionWindow = 15f;
        private const float arrivalThreshold = 1f;   // 到达阈值（米）
        private const float maxRoamRadius = 50f;   // 刷新前最早阶段的巡游半径
        private const float minRoamRadius = 30f;    // 临近刷新时的最小巡游半径
        private float m_superStarSpawnTime = Match.instance.GlobalSetting.MatchTime * 0.5f;
        private bool m_isFirstEnter = true;
        private Vector3 m_superStarCenter = Vector3.zero;
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)//CORE 只是在这个方法里写入黑板要移动的位置，不执行OnExecute
        {
            //TODO 该行动节点可以设置前置节点，判断血量等等
            //STEP 1 判断是否在可以行动的窗口内
            float remainingTime = Match.instance.RemainingTime;
            float delta = remainingTime - m_superStarSpawnTime;
            if (delta > 0 && delta < canActionWindow)
            {
                if (m_isFirstEnter)
                {
                    m_isFirstEnter = false;
                    Debug.LogWarning("初次进入超级星星移动节点");
                    Vector3 newTarget = SampleRoamPointAroundCenterByTime();
                    workingMemory.SetValue((int)EBBKey.MovingTargetPos, newTarget);
                    return true;
                }
                if (delta > 0 && delta <= moveToCenterWindow)//STEP 2 判断是否在前往中心点的窗口内
                {
                    workingMemory.SetValue((int)EBBKey.MovingTargetPos, m_superStarCenter);
                    return true;
                }
                if (HasReachedTarget(agent, workingMemory))//STEP 3 判断是否到达了目标点，到了才能设置新的目标点
                {
                    Vector3 newTarget = SampleRoamPointAroundCenterByTime();
                    workingMemory.SetValue((int)EBBKey.MovingTargetPos, newTarget);
                    return true;
                }                
            }
            return false;
        }
        private bool HasReachedTarget(IAgent agent, BlackboardMemory workingMemory)
        {
            Vector3 targetPos;
            if (workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out targetPos))
            {
                Tank tank = (Tank)agent;
                if (Vector3.Distance(targetPos, tank.Position) >= arrivalThreshold)
                {
                    return false;
                }
            }
            return true;
        }
        private Vector3 SampleRoamPointAroundCenterByTime()
        {
            float radius = GetCurrentRoamRadiusByTime();
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
            return new Vector3(
                m_superStarCenter.x + offset.x,
                m_superStarCenter.y,
                m_superStarCenter.z + offset.y
            );
        }
        private float GetCurrentRoamRadiusByTime()//CORE
        {
            float remainingTime = Match.instance.RemainingTime;
            float delta = remainingTime - m_superStarSpawnTime;
            if (delta <= 0f)
            {
                return minRoamRadius;
            }
            // 用你定义的“预热总窗口”做归一化（例如 midWindow=20f）
            // delta 越大（离刷新越远）=> 半径越接近 max
            // delta 越小（离刷新越近）=> 半径越接近 min
            float progress = 1f - Mathf.Clamp01(delta / canActionWindow); // 0(远) -> 1(近)
            return Mathf.Lerp(maxRoamRadius, minRoamRadius, progress);
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
            //MARKER 对手没死且自己血量有压制，且敌人靠近家，发动追击
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
    class CloseCombatChaseDirect : ActionNode
    {
        private Condition m_closeCombatCondition = new ConditionCloseCombatMode();
        private Tank m_enemy;

        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (!m_closeCombatCondition.IsTrue(agent))
            {
                return false;
            }

            Tank tank = (Tank)agent;
            m_enemy = Match.instance.GetOppositeTank(tank.Team);
            return m_enemy != null && !m_enemy.IsDead;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            if (m_enemy == null || m_enemy.IsDead)
            {
                return ERunningStatus.Failed;
            }
            Debug.LogError("<color=cyan>进入猛攻状态</color>");
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, m_enemy.Position);
            return ERunningStatus.Finished;
        }
    }
    class RandomMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Vector3 targetPos;
            Tank tank = (Tank)agent;
            if(workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out targetPos))
            {
                if(Vector3.Distance(targetPos, tank.Position) >= 1f)//MARKER 还没到目标点则不更换目标点
                {
                    return false;
                }
            }
            Debug.LogWarning("进入随机移动节点");
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, GetNextDestination(tank));
            return true;
        }
        private Vector3 GetNextDestination(Tank tank)
        {
            Vector3 centerPoint = Vector3.zero;
            Vector3 home = Match.instance.GetRebornPos(tank.Team);
            Vector3 midPoint = (centerPoint + home) * 0.5f;
            const float radius = 25f;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float r = radius * Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f));
            float x = midPoint.x + Mathf.Cos(angle) * r;
            float z = midPoint.z + Mathf.Sin(angle) * r;
            return new Vector3(x, 0f, z);
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
            Tank tank = (Tank)agent;
            tank.Move(workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos));
            return ERunningStatus.Finished;
        }
    }
    #endregion
    enum EBBKey
    {
        MovingTargetPos,
        IncomingMissile,
        NearestStar,
        IsSuperStar
    }
    public class MyTank : Tank
    {
        private BlackboardMemory m_workingMemory;
        private Node m_BTNode;
        protected override void OnStart()
        {
            base.OnStart();
            m_workingMemory = new BlackboardMemory();
            Condition closeCombatCondition = new ConditionCloseCombatMode();
            Condition notCloseCombatCondition = new NotCondition(closeCombatCondition);
            m_BTNode = new ParallelNode(1).AddChild(
                new TurnTurret(),
                new Fire(),
                new SequenceNode().AddChild(
                    new SelectorNode().AddChild(
                        new CloseCombatChaseDirect(),
                        new EvadeMissile().SetPrecondition(notCloseCombatCondition),
                        new MoveToSuperStarPosition().SetPrecondition(notCloseCombatCondition),
                        new Heal().SetPrecondition(notCloseCombatCondition),
                        new Chase(),
                        new FindStar(),
                        new RandomMove()),
                    new MoveTo()));
        }
        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_workingMemory);
        }

        public override string GetName()
        {
            return "LRB";
        }
    }
}
