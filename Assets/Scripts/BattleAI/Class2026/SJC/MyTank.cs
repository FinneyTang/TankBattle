using AI.FiniteStateMachine;
using Main;
using UnityEngine;
using System.Collections.Generic;

namespace SJC
{
    enum EMoveState
    {
        StarFirst,      // 优先吃星星
        ForSuperStar,   // 去地图中心抢超级星星
        ChaseEnemy,     // 追击敌人
        EvadeMissile,   // 躲避导弹
        GoHome,         // 回家补给
        StayHome,       // 在家回血
    }

    enum EBBKey
    {
        MovingTargetPos,
        IncomingMissile,
        NearestStar,
        IsSuperStar,
        EvadeDirection,
    }

    public class KnowledgePool
    {
        public Tank Self;
        public Tank Enemy;
        public Dictionary<int, Star> Stars;
        public Dictionary<int, Missile> EnemyMissiles;
        public float RemainingTime;
        public Vector3 HomePos;
        public Vector3 EnemyHomePos;
        public Star NearestStar;
        public Star SuperStar;
        public Missile NearestThreatMissile;
        public float NearestStarDist;
        public bool EnemyIsDead;
        public bool EnemyIsAlive;

        public void Update(Tank self)
        {
            Self = self;
            Enemy = Match.instance.GetOppositeTank(self.Team);
            Stars = Match.instance.GetStars();
            EnemyMissiles = Match.instance.GetOppositeMissiles(self.Team);
            RemainingTime = Match.instance.RemainingTime;
            HomePos = Match.instance.GetRebornPos(self.Team);
            EnemyHomePos = Match.instance.GetRebornPos(
                Enemy != null ? Enemy.Team : (self.Team == ETeam.A ? ETeam.B : ETeam.A));
            EnemyIsDead = Enemy == null || Enemy.IsDead;
            EnemyIsAlive = !EnemyIsDead;

            // 找最近的星星和超级星星
            NearestStar = null;
            SuperStar = null;
            NearestStarDist = float.MaxValue;
            if (Stars != null)
            {
                foreach (var pair in Stars)
                {
                    Star s = pair.Value;
                    if (s.IsSuperStar)
                    {
                        SuperStar = s;
                    }
                    float dist = Vector3.SqrMagnitude(s.Position - self.Position);
                    if (dist < NearestStarDist)
                    {
                        NearestStarDist = dist;
                        NearestStar = s;
                    }
                }
            }

            // 找最近的威胁导弹
            NearestThreatMissile = null;
            float minMissileDist = float.MaxValue;
            if (EnemyMissiles != null)
            {
                foreach (var pair in EnemyMissiles)
                {
                    Missile m = pair.Value;
                    if (!CanMissileHitSelf(self, m))
                        continue;
                    float dist = Vector3.SqrMagnitude(m.Position - self.Position);
                    if (dist < minMissileDist)
                    {
                        minMissileDist = dist;
                        NearestThreatMissile = m;
                    }
                }
            }
        }

        private bool CanMissileHitSelf(Tank self, Missile missile)
        {
            // 导弹太远不管
            if (Vector3.SqrMagnitude(missile.Position - self.Position) > 900.0f)
                return false;
            // 导弹朝反方向飞的不管
            if (Vector3.Dot(self.Position - missile.Position, missile.Velocity) < -0.1f)
                return false;
            // 有障碍物挡住的不管
            if (Physics.Linecast(missile.Position, self.Position, PhysicsUtils.LayerMaskScene))
                return false;
            return true;
        }
    }

    public class DecisionLayer
    {
        private KnowledgePool m_Knowledge;
        private Tank m_Self;

        public DecisionLayer(KnowledgePool knowledge, Tank self)
        {
            m_Knowledge = knowledge;
            m_Self = self;
        }

        /// <summary>
        /// 是否需要躲避导弹
        /// </summary>
        /// <returns></returns>
        public bool ShouldEvadeMissile()
        {
            if (m_Knowledge.NearestThreatMissile == null)
                return false;

            // 如果敌人一枪就死，且我们血量健康，不躲直接冲
            int dmg = Match.instance.GlobalSetting.DamagePerHit;
            if (m_Knowledge.EnemyIsAlive && m_Knowledge.Enemy.HP <= dmg && m_Self.HP >= 2 * dmg)
                return false;

            // 如果已经在家了，不躲
            if (Vector3.SqrMagnitude(m_Self.Position - m_Knowledge.HomePos) <
                Match.instance.GlobalSetting.HomeZoneRadius * Match.instance.GlobalSetting.HomeZoneRadius)
                return false;

            return true;
        }

        /// <summary>
        /// 是否应该去抢超级星星
        /// </summary>
        /// <returns></returns>
        public bool ShouldGoForSuperStar()
        {
            float remainingTime = m_Knowledge.RemainingTime;
            float halfTime = Match.instance.GlobalSetting.MatchTime * 0.5f;

            // 超级星星在比赛一半时间时生成
            // 提前5秒去中心站位
            if (remainingTime <= halfTime + 5.0f && remainingTime >= halfTime - 10.0f)
            {
                // 如果超级星星已经在场上，直接去
                if (m_Knowledge.SuperStar != null)
                    return true;
                // 或者时间范围内去中心抢占位置
                if (remainingTime > halfTime - 5.0f)
                    return true;
            }

            // 如果场上已经有超级星星了
            if (m_Knowledge.SuperStar != null)
                return true;

            return false;
        }

        /// <summary>
        /// 是否应该回家补给
        /// </summary>
        /// <returns></returns>
        public bool ShouldGoHome()
        {
            int maxHP = Match.instance.GlobalSetting.MaxHP;

            // 血量极低立刻回家
            if (m_Self.HP <= 30)
                return true;

            // 敌人死了且血量不满且没有超级星星可吃
            if (m_Knowledge.EnemyIsDead && m_Self.HP < maxHP && m_Knowledge.SuperStar == null
                && m_Self.GetRebornCD(Time.time) > 3.0f)
            {
                return true;
            }

            // 血量低于敌人两炮的差距
            if (m_Knowledge.EnemyIsAlive)
            {
                int dmg = Match.instance.GlobalSetting.DamagePerHit;
                if (m_Self.HP <= m_Knowledge.Enemy.HP - 2 * dmg)
                    return true;
            }

            // 在家附近且血量不满，顺路回去补一下
            if (m_Self.HP < maxHP * 0.7f &&
                Vector3.SqrMagnitude(m_Self.Position - m_Knowledge.HomePos) < 800.0f)
                return true;

            return false;
        }

        ///<summary>
        /// 是否应该继续待在家里
        ///</summary>
        /// <returns></returns>
        public bool ShouldStayHome()
        {
            int maxHP = Match.instance.GlobalSetting.MaxHP;
            // 血量恢复到80%以上再出门
            if (m_Self.HP >= maxHP * 0.8f)
                return false;

            // 如果超级星星出现了，即使血量少一点也要出去
            if (m_Knowledge.SuperStar != null && m_Self.HP >= 50)
                return false;

            return true;
        }
        ///<summary>
        /// 是否应该追敌人
        ///</summary>
        /// <returns></returns>
        public bool ShouldChaseEnemy()
        {
            if (!m_Knowledge.EnemyIsAlive)
                return false;

            int dmg = Match.instance.GlobalSetting.DamagePerHit;

            // 我们血量优势，追击
            if (m_Self.HP >= m_Knowledge.Enemy.HP + dmg)
                return true;

            // 敌人一炮死，追击
            if (m_Knowledge.Enemy.HP <= dmg)
                return true;

            // 敌人在我方半场，哈气
            float homeDistToEnemy = Vector3.SqrMagnitude(
                m_Knowledge.HomePos - m_Knowledge.Enemy.Position);
            if (homeDistToEnemy < 2500.0f)
                return true;

            return false;
        }

        ///<summary>
        /// 是否应该去吃星星
        ///</summary>
        /// <returns></returns>
        public bool ShouldGoForStar()
        {
            // 有星星就去
            if (m_Knowledge.Stars != null && m_Knowledge.Stars.Count > 0)
                return true;
            return false;
        }

        ///<summary>
        /// 是否有星星在场上
        ///</summary>
        /// <returns></returns>
        public bool HasStarOnField()
        {
            return m_Knowledge.Stars != null && m_Knowledge.Stars.Count > 0;
        }
    }


    // 射击控制器
    class ShootController
    {
        private KnowledgePool m_Knowledge;
        private Tank m_Self;

        public ShootController(KnowledgePool knowledge, Tank self)
        {
            m_Knowledge = knowledge;
            m_Self = self;
        }

        public void Update()
        {
            if (m_Knowledge.EnemyIsAlive)
            {
                Vector3 aimTarget = PredictEnemyPosition();
                m_Self.TurretTurnTo(aimTarget);
                TryFire(aimTarget);
            }
            else
            {
                // 敌人死了，炮管指向敌人复活点
                m_Self.TurretTurnTo(m_Knowledge.EnemyHomePos);
                // 如果能看到敌人老家且能开火就打
                if (m_Self.CanSeeOthers(m_Knowledge.EnemyHomePos) && m_Self.CanFire())
                {
                    m_Self.Fire();
                }
            }
        }

        private Vector3 PredictEnemyPosition()
        {
            Tank enemy = m_Knowledge.Enemy;
            Vector2 enemyPos2D = new Vector2(enemy.Position.x, enemy.Position.z);
            Vector2 enemyVel2D = new Vector2(enemy.Velocity.x, enemy.Velocity.z);
            Vector2 firePos2D = new Vector2(m_Self.FirePos.x, m_Self.FirePos.z);
            Vector2 delta = enemyPos2D - firePos2D;

            float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
            float a = Vector2.Dot(enemyVel2D, enemyVel2D) - missileSpeed * missileSpeed;
            float b = 2 * Vector2.Dot(delta, enemyVel2D);
            float c = delta.sqrMagnitude;

            float discriminant = b * b - 4 * a * c;

            if (discriminant >= 0)
            {
                float sqrtD = Mathf.Sqrt(discriminant);
                float time = (-b - sqrtD) / (2 * a);
                if (time < 0) time = (-b + sqrtD) / (2 * a);
                if (time > 0)
                {
                    Vector2 intercept = enemyPos2D + enemyVel2D * time;
                    return new Vector3(intercept.x, m_Self.FirePos.y, intercept.y);
                }
            }

            return enemy.Position;
        }

        private void TryFire(Vector3 aimTarget)
        {
            Tank enemy = m_Knowledge.Enemy;

            // 敌人在出生点无敌时不打
            if (Vector3.SqrMagnitude(enemy.Position - m_Knowledge.EnemyHomePos) < 200.0f)
                return;

            Vector3 targetDirection = aimTarget - m_Self.FirePos;

            // 近距离直接射击
            if ((m_Self.Position - enemy.Position).magnitude < 15)
            {
                if (m_Self.CanFire())
                    m_Self.Fire();
                return;
            }

            // 远距离需要炮管对准且有视线
            float maxDistance = targetDirection.magnitude + 2;
            if (Physics.SphereCast(m_Self.FirePos, 0.24f, targetDirection, out RaycastHit hit,
                                   maxDistance))
            {
                FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                if (fireCollider != null)
                {
                    if (Vector3.Angle(m_Self.TurretAiming, targetDirection) < 15)
                    {
                        if (m_Self.CanFire())
                            m_Self.Fire();
                    }
                }
            }
            else
            {
                if (Vector3.Angle(m_Self.TurretAiming, targetDirection) < 15)
                {
                    if (m_Self.CanFire())
                        m_Self.Fire();
                }
            }
        }
    }

    // 状态：优先吃星星
    class StarFirstState : State
    {
        public StarFirstState()
        {
            StateType = (int)EMoveState.StarFirst;
        }

        public override State Execute()
        {
            MyTank tank = (MyTank)Agent;
            DecisionLayer decision = tank.Decision;
            KnowledgePool knowledge = tank.Knowledge;

            // 优先判断：是否需要躲避导弹
            if (decision.ShouldEvadeMissile())
                return m_StateMachine.Transition((int)EMoveState.EvadeMissile);

            // 优先判断：是否需要抢超级星星
            if (decision.ShouldGoForSuperStar())
                return m_StateMachine.Transition((int)EMoveState.ForSuperStar);

            // 优先判断：是否需要回家
            if (decision.ShouldGoHome())
                return m_StateMachine.Transition((int)EMoveState.GoHome);

            // 优先判断：是否应该追击敌人
            if (decision.ShouldChaseEnemy())
                return m_StateMachine.Transition((int)EMoveState.ChaseEnemy);

            // 吃星星逻辑
            if (decision.HasStarOnField())
            {
                // 超级星星优先
                if (knowledge.SuperStar != null)
                {
                    tank.Move(knowledge.SuperStar.Position);
                }
                else
                {
                    // 找最近的星星，但要考虑多个星星聚集的情况
                    tank.Move(tank.FindBestStarPosition());
                }
            }
            else
            {
                // 没星星了，如果有敌人就去追，否则去地图中间转转
                if (knowledge.EnemyIsAlive)
                {
                    return m_StateMachine.Transition((int)EMoveState.ChaseEnemy);
                }
                else
                {
                    tank.Move(tank.GetRandomFieldPosition());
                }
            }

            return this;
        }
    }

    // 状态：抢超级星星
    class ForSuperStarState : State
    {
        public ForSuperStarState()
        {
            StateType = (int)EMoveState.ForSuperStar;
        }

        public override State Execute()
        {
            MyTank tank = (MyTank)Agent;
            DecisionLayer decision = tank.Decision;
            KnowledgePool knowledge = tank.Knowledge;

            // 生命受到威胁时先躲
            if (decision.ShouldEvadeMissile())
                return m_StateMachine.Transition((int)EMoveState.EvadeMissile);

            // 血量太低先保命
            if (tank.HP <= 25)
                return m_StateMachine.Transition((int)EMoveState.GoHome);

            // 超级星星不在了，切换回正常模式
            if (!decision.ShouldGoForSuperStar())
            {
                if (decision.HasStarOnField())
                    return m_StateMachine.Transition((int)EMoveState.StarFirst);
                else if (decision.ShouldChaseEnemy())
                    return m_StateMachine.Transition((int)EMoveState.ChaseEnemy);
                else
                    return m_StateMachine.Transition((int)EMoveState.StarFirst);
            }

            // 去地图中心(超级星星总是在中心生成)
            tank.Move(Vector3.zero);

            return this;
        }
    }

    // 状态：追击敌人
    class ChaseEnemyState : State
    {
        public ChaseEnemyState()
        {
            StateType = (int)EMoveState.ChaseEnemy;
        }

        public override State Execute()
        {
            MyTank tank = (MyTank)Agent;
            DecisionLayer decision = tank.Decision;
            KnowledgePool knowledge = tank.Knowledge;

            // 优先躲避导弹
            if (decision.ShouldEvadeMissile())
                return m_StateMachine.Transition((int)EMoveState.EvadeMissile);

            // 血量太低回家
            if (decision.ShouldGoHome())
                return m_StateMachine.Transition((int)EMoveState.GoHome);

            // 超级星星出现优先抢
            if (decision.ShouldGoForSuperStar())
                return m_StateMachine.Transition((int)EMoveState.ForSuperStar);

            // 敌人死了且星星已经没有了，去吃星星或者回家
            if (knowledge.EnemyIsDead)
            {
                if (decision.HasStarOnField())
                    return m_StateMachine.Transition((int)EMoveState.StarFirst);
                else
                    return m_StateMachine.Transition((int)EMoveState.GoHome);
            }

            // 如果追击条件不再满足，转为吃星星
            if (!decision.ShouldChaseEnemy() && decision.HasStarOnField())
                return m_StateMachine.Transition((int)EMoveState.StarFirst);

            // 追向敌人
            tank.Move(knowledge.Enemy.Position);

            return this;
        }
    }

    // 状态：躲避导弹
    class EvadeMissileState : State
    {
        private float m_EvadeStartTime;

        public EvadeMissileState()
        {
            StateType = (int)EMoveState.EvadeMissile;
        }

        public override void Enter()
        {
            m_EvadeStartTime = Time.time;
        }

        public override State Execute()
        {
            MyTank tank = (MyTank)Agent;
            DecisionLayer decision = tank.Decision;
            KnowledgePool knowledge = tank.Knowledge;

            // 躲避完成，返回正常逻辑
            if (!decision.ShouldEvadeMissile())
            {
                if (decision.ShouldGoForSuperStar())
                    return m_StateMachine.Transition((int)EMoveState.ForSuperStar);
                if (decision.ShouldGoHome())
                    return m_StateMachine.Transition((int)EMoveState.GoHome);
                if (decision.ShouldChaseEnemy())
                    return m_StateMachine.Transition((int)EMoveState.ChaseEnemy);
                return m_StateMachine.Transition((int)EMoveState.StarFirst);
            }

            // 执行躲避
            Missile missile = knowledge.NearestThreatMissile;
            if (missile != null)
            {
                // 计算垂直于导弹飞行方向的躲避方向
                Vector3 missileVelXZ = new Vector3(missile.Velocity.x, 0, missile.Velocity.z);
                Vector3 evadeDir = Vector3.Cross(missileVelXZ, Vector3.up).normalized;

                // 判断坦克在导弹的哪一侧，向远离导弹的方向躲
                Vector3 toTank = tank.Position - missile.Position;
                Vector3 cross = Vector3.Cross(missileVelXZ, toTank);
                if (cross.y > 0)
                    evadeDir = -evadeDir;

                // 同时考虑远离敌人的方向
                if (knowledge.EnemyIsAlive)
                {
                    Vector3 awayFromEnemy = (tank.Position - knowledge.Enemy.Position).normalized;
                    awayFromEnemy.y = 0;
                    if (awayFromEnemy.sqrMagnitude > 0.01f)
                    {
                        // 综合躲避方向和远离敌人方向
                        evadeDir = (evadeDir + awayFromEnemy * 0.5f).normalized;
                    }
                }

                Vector3 evadeTarget = tank.Position + evadeDir * 8.0f;
                tank.Move(evadeTarget);
            }

            return this;
        }
    }

    // 状态：回家
    class GoHomeState : State
    {
        public GoHomeState()
        {
            StateType = (int)EMoveState.GoHome;
        }

        public override State Execute()
        {
            MyTank tank = (MyTank)Agent;
            DecisionLayer decision = tank.Decision;
            KnowledgePool knowledge = tank.Knowledge;

            // 超级星星出现时，如果血量不是特别低就去抢
            if (decision.ShouldGoForSuperStar() && tank.HP >= 40)
                return m_StateMachine.Transition((int)EMoveState.ForSuperStar);

            // 检查是否到家了
            float homeDist = Vector3.SqrMagnitude(tank.Position - knowledge.HomePos);
            float homeZoneRadius = Match.instance.GlobalSetting.HomeZoneRadius;

            if (homeDist < homeZoneRadius * homeZoneRadius)
            {
                // 切换到停留状态
                return m_StateMachine.Transition((int)EMoveState.StayHome);
            }

            // 移动回家
            tank.Move(knowledge.HomePos);
            return this;
        }
    }

    // 状态：在家回血
    class StayHomeState : State
    {
        public StayHomeState()
        {
            StateType = (int)EMoveState.StayHome;
        }

        public override State Execute()
        {
            MyTank tank = (MyTank)Agent;
            DecisionLayer decision = tank.Decision;
            KnowledgePool knowledge = tank.Knowledge;

            // 超级星星优先
            if (decision.ShouldGoForSuperStar() && tank.HP >= 50)
                return m_StateMachine.Transition((int)EMoveState.ForSuperStar);

            // 血量恢复好了，出门
            if (!decision.ShouldStayHome())
            {
                if (decision.HasStarOnField())
                    return m_StateMachine.Transition((int)EMoveState.StarFirst);
                else if (decision.ShouldChaseEnemy())
                    return m_StateMachine.Transition((int)EMoveState.ChaseEnemy);
                else
                    return m_StateMachine.Transition((int)EMoveState.StarFirst);
            }

            // 待在家里
            tank.Move(knowledge.HomePos);
            return this;
        }
    }

    public class MyTank : Tank
    {
        // 状态机
        private StateMachine m_MoveFSM;

        // 信息层
        public KnowledgePool Knowledge { get; private set; }

        // 决策层
        public DecisionLayer Decision { get; private set; }

        // 射击控制器
        private ShootController m_ShootController;

        public override string GetName()
        {
            return "SJC";
        }

        protected override void OnStart()
        {
            base.OnStart();

            // 初始化信息层
            Knowledge = new KnowledgePool();

            // 初始化决策层
            Decision = new DecisionLayer(Knowledge, this);

            // 初始化射击控制器
            m_ShootController = new ShootController(Knowledge, this);

            // 初始化状态机
            m_MoveFSM = new StateMachine(this);
            m_MoveFSM.AddState(new StarFirstState());
            m_MoveFSM.AddState(new ForSuperStarState());
            m_MoveFSM.AddState(new ChaseEnemyState());
            m_MoveFSM.AddState(new EvadeMissileState());
            m_MoveFSM.AddState(new GoHomeState());
            m_MoveFSM.AddState(new StayHomeState());
            m_MoveFSM.SetDefaultState((int)EMoveState.StarFirst);
        }

        protected override void OnUpdate()
        {
            // 收集战场信息
            Knowledge.Update(this);

            // 炮塔控制与射击
            m_ShootController.Update();

            // FSM控制移动
            m_MoveFSM.Update();
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            // 重生后重新设置默认状态
            m_MoveFSM.SetDefaultState((int)EMoveState.StarFirst);
        }

        /// <summary>
        /// 找到最优的星星位置
        /// </summary>
        public Vector3 FindBestStarPosition()
        {
            var stars = Knowledge.Stars;
            if (stars == null || stars.Count == 0)
                return Knowledge.HomePos;

            float nearestDist = float.MaxValue;
            int mostNearbyStars = 0;
            Star bestStar = null;

            foreach (var pair in stars)
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    return s.Position; // 超级星星直接去
                }

                float dist = Vector3.SqrMagnitude(s.Position - Position);

                // 计算这颗星周围有多少颗其他星星
                int nearbyCount = 0;
                foreach (var inner in stars)
                {
                    if (Vector3.SqrMagnitude(inner.Value.Position - s.Position) < 400.0f)
                        ++nearbyCount;
                }

                // 优先去星星密集的区域
                if (nearbyCount > mostNearbyStars && dist < nearestDist * 1.5f)
                {
                    bestStar = s;
                    nearestDist = dist;
                    mostNearbyStars = nearbyCount;
                }
                else if (nearbyCount - mostNearbyStars > 2)
                {
                    bestStar = s;
                    mostNearbyStars = nearbyCount;
                }
                else if (dist < nearestDist)
                {
                    nearestDist = dist;
                    bestStar = s;
                }
            }

            return bestStar != null ? bestStar.Position : Knowledge.HomePos;
        }

        /// <summary>
        /// 获取一个随机的战场位置
        /// </summary>
        public Vector3 GetRandomFieldPosition()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            Vector3 best = Vector3.zero;
            float minimalDist = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(
                    Random.Range(-halfSize, halfSize),
                    0,
                    Random.Range(-halfSize, halfSize));
                float dist = Vector3.SqrMagnitude(pos - Knowledge.HomePos);
                if (dist < minimalDist)
                {
                    best = pos;
                    minimalDist = dist;
                }
            }

            return best;
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();

            // 可视化最近星星
            if (Knowledge != null && Knowledge.NearestStar != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(Knowledge.NearestStar.Position, 2.0f);
            }

            // 可视化威胁导弹
            if (Knowledge != null && Knowledge.NearestThreatMissile != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(Position, Knowledge.NearestThreatMissile.Position);
            }
        }
    }
}
