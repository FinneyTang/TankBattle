using System.Collections.Generic;
using System.Linq;
using AI.GOAP;
using Main;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GY
{
#if UNITY_EDITOR
    using UnityEditor;
#endif

    #region HealAction

    //回基地回血
    public class HealAction : GOAPAction
    {
        private MyTank m_Tank;

        public HealAction(MyTank tank)
        {
            m_Tank = tank;
            // 添加前提条件：血量不满
            AddPrecondition(WorldStateKey.HasFullHP, false);
            // 添加效果：血量恢复满
            AddEffect(WorldStateKey.HasFullHP, true);
            // 设置成本（回血的成本较高）
            Cost = 5.0f;
        }

        public override void Init()
        {
            m_Tank.Move(Match.instance.GetRebornPos(m_Tank.Team));
        }

        public override bool Update()
        {
            return m_Tank.HP == Match.instance.GlobalSetting.MaxHP;
        }
    }

    #endregion

    #region FindStarAction

    //找星星
    public class MoveToClosetStarAction : GOAPAction
    {
        private MyTank m_Tank;

        public MoveToClosetStarAction(MyTank tank)
        {
            m_Tank = tank;
            AddPrecondition(WorldStateKey.StarAvailable, true);
            AddEffect(WorldStateKey.ScoreIncreased, true);
            Cost = 3.0f;
        }

        public override bool Update()
        {
            return MoveToClosestStar();
        }

        private bool MoveToClosestStar()
        {
            if (m_Tank.ClosetStar != null)
            {
                m_Tank.Move(m_Tank.ClosetStar.Position);
                return true;
            }

            return false;
        }
    }

    public class MoveToSecondClosetStarAction : GOAPAction
    {
        private MyTank m_Tank;

        public MoveToSecondClosetStarAction(MyTank tank)
        {
            m_Tank = tank;
            AddPrecondition(WorldStateKey.StarAvailable, true);
            AddPrecondition(WorldStateKey.OppositeTankCloserToBestStar, true);
            AddPrecondition(WorldStateKey.HasMultipleStar, true);
            AddPrecondition(WorldStateKey.BestTwoStarSimilar, false);
            AddEffect(WorldStateKey.ScoreIncreased, true);
            Cost = 2.0f;
        }

        public override bool Update()
        {
            return MoveToStar();
        }

        private bool MoveToStar()
        {
            if (m_Tank.SecondClosetStar != null)
            {
                m_Tank.Move(m_Tank.SecondClosetStar.Position);
                return true;
            }

            return false;
        }
    }

    #endregion

    #region MoveToCenterAction

    public class MoveToCenterAction : GOAPAction
    {
        private MyTank m_Tank;
        Vector3 targetPos;

        public MoveToCenterAction(MyTank tank)
        {
            m_Tank = tank;
            AddEffect(WorldStateKey.AtCenterPosition, false);
            AddEffect(WorldStateKey.AtCenterPosition, true);
            Cost = 5.0f;
        }

        public override void Init()
        {
            base.Init();
            targetPos = m_Tank.randomMoveToCenterPos;
            m_Tank.Move(targetPos);
        }

        public override bool Update()
        {
            float distanceToCenter = Vector3.Distance(m_Tank.Position, targetPos);
            if (distanceToCenter < Match.instance.FieldSize * 0.3f)
            {
                return true;
            }

            m_Tank.Move(targetPos);
            return false;
        }
    }

    #endregion

    #region EvadeMissile
//规避子弹
public class EvadeMissile : GOAPAction
{
    private MyTank m_Tank;
    private float evadeDistance = 4f; // 固定躲避距离（与SM一致）
    private LayerMask obstacleLayer; // 障碍物层级
    private float maxTurnAngle = 150f; // 最大转向角度限制（不超过150度）

    public EvadeMissile(MyTank tank)
    {
        m_Tank = tank;
        AddPrecondition(WorldStateKey.OppositeMissileCanHit, true);
        AddEffect(WorldStateKey.OppositeMissileCanHit, false);
        Cost = 2.0f;

        // 初始化障碍物层级
        obstacleLayer = PhysicsUtils.LayerMaskScene;
    }

    public override void Init()
    {
        base.Init();
        var missiles = Match.instance.GetOppositeMissiles(m_Tank.Team);

        if (missiles.Count > 0)
        {
            // 找到最近的可命中导弹
            Missile threatMissile = FindMostThreateningMissile(missiles);

            if (threatMissile != null)
            {
                // 计算躲避方向（限制转向角度）
                Vector3 direction = CalculateEvadeDirection(threatMissile);
                
                // 计算躲避目标点（固定4米距离）
                Vector3 evadeTarget = m_Tank.Position + direction * evadeDistance;
                
                // 确保目标点在地图范围内
                evadeTarget = ClampPositionToMap(evadeTarget);
                
                // 执行移动
                m_Tank.Move(evadeTarget);

                //Debug.Log($"[EvadeMissile] 导弹位置: {threatMissile.Position}, 躲避方向: {direction}");
            }
        }
    }

    public override bool Update()
    {
        var missiles = Match.instance.GetOppositeMissiles(m_Tank.Team);
        if (missiles.Count == 0)
            return true;

        Missile threat = FindMostThreateningMissile(missiles);
        if (threat == null)
            return true;

        // 每帧重新计算躲避方向（限制转向角度）
        Vector3 direction = CalculateEvadeDirection(threat);
        
        // 固定4米移动距离
        Vector3 target = m_Tank.Position + direction * evadeDistance;
        m_Tank.Move(target);
        
        // 检查是否到达安全区域或导弹不再构成威胁
        return !m_Tank.m_WorldState.GetState(WorldStateKey.OppositeMissileCanHit);
    }

    /// <summary>
    /// 找到最具威胁的导弹
    /// </summary>
    private Missile FindMostThreateningMissile(Dictionary<int, Missile> missiles)
    {
        Missile mostThreatening = null;
        float minDist = float.MaxValue;

        foreach (var missilePair in missiles)
        {
            Missile missile = missilePair.Value;

            if (!CanHit(missile))
                continue;

            float dist = Vector3.Distance(missile.Position, m_Tank.Position);
            if (dist < minDist)
            {
                mostThreatening = missile;
                minDist = dist;
            }
        }

        return mostThreatening;
    }

    /// <summary>
    /// 计算躲避方向（限制转向角度不超过150度）
    /// </summary>
    private Vector3 CalculateEvadeDirection(Missile missile)
    {
        // 计算与导弹速度垂直的两个方向
        Vector3 perpendicularDir1 = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
        Vector3 perpendicularDir2 = -perpendicularDir1;
        
        // 确定哪个方向使坦克远离导弹
        Vector3 missileToTank = m_Tank.Position - missile.Position;
        float dot1 = Vector3.Dot(perpendicularDir1, missileToTank);
        float dot2 = Vector3.Dot(perpendicularDir2, missileToTank);
        
        // 选择使坦克远离导弹的方向（点积为正表示方向大致相同）
        Vector3 awayFromMissileDir = dot1 > dot2 ? perpendicularDir1 : perpendicularDir2;
        
        // 如果有最近的星星，选择更靠近星星的垂直方向
        if (m_Tank.ClosetStar != null)
        {
            // 计算朝向星星的方向
            Vector3 toStar = (m_Tank.ClosetStar.Position - m_Tank.Position).normalized;
            
            // 计算两个垂直方向与星星方向的夹角
            float angle1 = Vector3.Angle(perpendicularDir1, toStar);
            float angle2 = Vector3.Angle(perpendicularDir2, toStar);
            
            // 选择夹角更小的方向（更靠近星星）
            Vector3 closerToStarDir = angle1 < angle2 ? perpendicularDir1 : perpendicularDir2;
            
            // 确保这个方向也使坦克远离导弹
            float dotCloser = Vector3.Dot(closerToStarDir, missileToTank);
            if (dotCloser > 0)
            {
                // 如果更靠近星星的方向也使坦克远离导弹，就使用它
                awayFromMissileDir = closerToStarDir;
            }
        }
        
        // 限制转向角度不超过150度
        awayFromMissileDir = LimitTurnAngle(awayFromMissileDir);
        
        return awayFromMissileDir;
    }

    /// <summary>
    /// 限制转向角度不超过maxTurnAngle度
    /// </summary>
    private Vector3 LimitTurnAngle(Vector3 desiredDirection)
    {
        // 获取坦克当前朝向
        Vector3 currentForward = m_Tank.Forward;
        
        // 计算期望方向与当前朝向的夹角
        float angleDifference = Vector3.Angle(currentForward, desiredDirection);
        
        // 如果夹角超过最大允许角度，则限制转向
        if (angleDifference > maxTurnAngle)
        {
            // 计算需要旋转的角度（从当前朝向转向期望方向）
            float rotationAngle = angleDifference - maxTurnAngle;
            
            // 确定旋转方向（顺时针还是逆时针）
            Vector3 cross = Vector3.Cross(currentForward, desiredDirection);
            float rotationSign = cross.y >= 0 ? 1 : -1;
            
            // 创建一个新的四元数，表示旋转到最大允许角度的位置
            Quaternion targetRotation = Quaternion.AngleAxis(maxTurnAngle * rotationSign, Vector3.up);
            Vector3 limitedDirection = targetRotation * currentForward;
            
            // 确保新方向仍然使坦克远离导弹（保持原方向的基本特性）
            // 这里我们可以稍微调整，使新方向更接近原期望方向
            limitedDirection = Vector3.Slerp(limitedDirection, desiredDirection, 0.3f);
            
            return limitedDirection.normalized;
        }
        
        return desiredDirection;
    }

    /// <summary>
    /// 判断导弹是否能击中坦克
    /// </summary>
    private bool CanHit(Missile missile)
    {
        // 距离检查
        if (Vector3.SqrMagnitude(missile.Position - m_Tank.Position) > 900.0f)
            return false;
        
        // 直线遮挡检查
        if (Physics.Linecast(missile.Position, m_Tank.Position, PhysicsUtils.LayerMaskScene))
            return false;

        // 球形投射检测
        var hits = Physics.SphereCastAll(missile.Position, 2.0f, missile.Velocity, 60.0f);
        if (hits.Length > 0)
        {
            foreach (var hit in hits)
            {
                var collider = hit.transform.GetComponent<FireCollider>();
                if (collider is not null && collider.Owner != m_Tank)
                    continue;
                else
                    break;
            }
        }
        
        return true;
    }

    /// <summary>
    /// 确保位置在地图范围内
    /// </summary>
    private Vector3 ClampPositionToMap(Vector3 position)
    {
        float mapSize = Match.instance.FieldSize * 0.85f; // 留15%的边距

        position.x = Mathf.Clamp(position.x, -mapSize, mapSize);
        position.z = Mathf.Clamp(position.z, -mapSize, mapSize);
        position.y = 0; // 保持在地面上

        return position;
    }
}
#endregion

    public static class WorldStateKey
    {
        public const string HasEnemy = "HasEnemy";
        public const string HasFullHP = "HasFullHP";
        public const string StarAvailable = "StarAvailable";
        public const string HasMultipleStar = "HasMultipleStar";
        public const string BestTwoStarSimilar = "BestTwoStarSimilar";
        public const string OppositeTankCloserToBestStar = "OppositeTankCloserToBestStar";
        public const string SuperStarAvailable = "SuperStarAvailable";
        public const string EnemyKilled = "EnemyKilled";
        public const string ScoreIncreased = "ScoreIncreased";
        public const string OppositeMissileCanHit = "OppositeMissileCanHit";
        public const string AtCenterPosition = "AtCenterPosition";
    }

    public class MyTank : Tank
    {
        //敌方坦克相关
        public Tank m_TargetTank; //敌方的坦克
        private Vector3 m_TargetTankLastPosition; //上一帧位置（在LateUpdate中记录）
        private Vector3 m_SmoothedVelocity; //平滑后的速度（用于预测）
        private Vector3 m_TargetTankPredictPos; //预测位置
        private float m_StraightDistanceToTargetTank; //到目标坦克间的直线距离
        private Vector3 m_TargetTankRebornPos; //敌方坦克的复活点
        private float m_PreTime = 0.2f; //预测时间

        //场内星星相关
        public Star ClosetStar;
        public Star SecondClosetStar;

        //Other
        private Vector3 rebornPos;
        public Vector3 randomMoveToCenterPos;

        //GOAP
        private Planner m_Planner;
        public WorldState m_WorldState = new WorldState(); //当前世界状态
        private GOAPActionMachine m_ActionMachine;
        private List<GOAPAction> m_CurrentPlan = new List<GOAPAction>();

        private WorldState m_GoalState = new WorldState(); //目标世界状态

        //GOAPInfo
        private float distanceToHome;
        private float distanceToClosestStar;
        private float distanceToSuperStar;
        private float oppositeDistanceToHome;
        private float distanceToOppositeHome;
        private bool canSeeOther;

        public override string GetName()
        {
            return "GY";
        }

        protected override void OnStart()
        {
            base.OnStart();
            m_TargetTank = Match.instance.GetOppositeTank(Team); //获取对手tank
            if (m_TargetTank != null)
            {
                m_TargetTankLastPosition = m_TargetTank.Position;
                m_TargetTankRebornPos = Match.instance.GetRebornPos(m_TargetTank.Team);
            }

            rebornPos = Match.instance.GetRebornPos(Team);
            float halfSize = Match.instance.FieldSize * 0.25f;
            randomMoveToCenterPos =
                new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
            GOAPInit();
        }

        private void GOAPInit()
        {
            m_Planner = new Planner(this);
            m_ActionMachine = new GOAPActionMachine(this);
            m_Planner.AddAction(new HealAction(this))
                .AddAction(new MoveToClosetStarAction(this))
                .AddAction(new EvadeMissile(this))
                .AddAction(new MoveToCenterAction(this))
                .AddAction(new MoveToSecondClosetStarAction(this));
            //注册行为
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            UpdateInfo();
            TurretTargetDecision();
            FireDecision();
            UpdatePlanner();
            if (m_ActionMachine.IsRunning)
            {
                m_ActionMachine.Update();
            }
            //MoveDecision();
        }

        // 在LateUpdate中记录敌方坦克位置，确保每帧更新
        private void LateUpdate()
        {
            if (!m_TargetTank.IsDead)
            {
                m_TargetTankLastPosition = m_TargetTank.Position;
            }
        }

        private void UpdateInfo()
        {
            if (!m_TargetTank.IsDead)
            {
                // 计算瞬时速度
                Vector3 rawVelocity = (m_TargetTank.Position - m_TargetTankLastPosition) / Time.deltaTime;

                m_SmoothedVelocity = Vector3.Lerp(m_SmoothedVelocity, rawVelocity, 0.3f);

                m_TargetTankPredictPos = m_TargetTank.Position + m_SmoothedVelocity * m_PreTime;

                m_StraightDistanceToTargetTank = Vector3.Distance(m_TargetTank.Position, Position);
            }
            else
            {
                m_TargetTank = Match.instance.GetOppositeTank(Team);
                if (!m_TargetTank.IsDead)
                {
                    m_TargetTankLastPosition = m_TargetTank.Position;
                    m_TargetTankRebornPos = Match.instance.GetRebornPos(m_TargetTank.Team);
                }
            }

            GetClosetStar();
            if (ClosetStar != null)
            {
                distanceToClosestStar = Vector3.Distance(ClosetStar.Position, Position);
            }

            distanceToHome = Vector3.Distance(m_TargetTank.Position, Position);
            distanceToHome = Vector3.Distance(rebornPos, Position);
            oppositeDistanceToHome = Vector3.Distance(m_TargetTankRebornPos, m_TargetTank.Position);
            distanceToOppositeHome = Vector3.Distance(m_TargetTankRebornPos,Position);

            var preTimeDetectValue = m_StraightDistanceToTargetTank / Match.instance.FieldSize;

            if (preTimeDetectValue > 0.5f)
            {
                m_PreTime = 0.3f;
            }
            else if (preTimeDetectValue is > 0.25f and <= 0.5f)
            {
                m_PreTime = 0.2f;
            }
            else if (preTimeDetectValue <= 0.25f)
            {
                m_PreTime = 0.1f;
            }

            #region GOAPState

            m_WorldState.SetState(WorldStateKey.HasEnemy, m_TargetTank != null);
            m_WorldState.SetState(WorldStateKey.HasFullHP, HP == Match.instance.GlobalSetting.MaxHP);
            m_WorldState.SetState(WorldStateKey.EnemyKilled, m_TargetTank == null);

            float distanceToCenter = Vector3.Distance(Position, Vector3.zero);
            m_WorldState.SetState(WorldStateKey.AtCenterPosition, distanceToCenter < Match.instance.FieldSize * 0.3f);
            if (ClosetStar != null)
            {
                m_WorldState.SetState(WorldStateKey.OppositeTankCloserToBestStar
                    , Vector3.Distance(m_TargetTank.Position, ClosetStar.Position) < Vector3.Distance(Position, ClosetStar.Position));
            }
            else
            {
                m_WorldState.SetState(WorldStateKey.OppositeTankCloserToBestStar, false);
            }

            var stars = Match.instance.GetStars();
            m_WorldState.SetState(WorldStateKey.StarAvailable, stars.Count > 0);
            m_WorldState.SetState(WorldStateKey.HasMultipleStar, stars.Count > 1);
            var hasSuperStar = false;
            foreach (var pair in stars)
            {
                if (pair.Value.IsSuperStar)
                {
                    hasSuperStar = true;
                    break;
                }
            }

            if (stars.Count > 1)
            {
                m_WorldState.SetState(WorldStateKey.BestTwoStarSimilar,
                    Vector3.Distance(ClosetStar.Position, SecondClosetStar.Position) <
                    Match.instance.FieldSize * 0.25f);
            }
            else
            {
                m_WorldState.SetState(WorldStateKey.BestTwoStarSimilar, false);
            }

            m_WorldState.SetState(WorldStateKey.SuperStarAvailable, hasSuperStar);
            m_WorldState.SetState(WorldStateKey.ScoreIncreased, false);
            m_WorldState.SetState(WorldStateKey.OppositeMissileCanHit, CanMissileHitJudgment());

            #endregion
        }

        public bool CanMissileHitJudgment()
        {
            var missiles = Match.instance.GetOppositeMissiles(Team);
            if (missiles.Count <= 0)
                return false;
            foreach (var missile in missiles)
            {
                if (CanHit(missile.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanHit(Missile missile)
        {
            //距离检查
            if (Vector3.SqrMagnitude(missile.Position - Position) > Match.instance.FieldSize * Match.instance.FieldSize)
                return false;
            //直线遮挡检查
            if (Physics.Linecast(missile.Position, Position, PhysicsUtils.LayerMaskScene))
                return false;
            // 4. 球形投射检测
            var hits = Physics.SphereCastAll(missile.Position, 2f, missile.Velocity, Match.instance.FieldSize * 1.41f);
            foreach (var hit in hits)
            {
                var fireCollider = hit.transform.GetComponent<FireCollider>();
                // 如果击中了其他坦克的碰撞体，则阻挡
                if (fireCollider != null && fireCollider.Owner != this)
                    return false; // 关键：返回 false
                // 如果击中了目标坦克的碰撞体，则通过
                if (fireCollider != null && fireCollider.Owner == this)
                    return true; // 直接命中目标
            }

            return false;
        }

        public Star GetClosetStar()
        {
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            float secondNearestDist = float.MaxValue;
            Star nearestStar = null;
            Star secondNearestStar = null;

            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                float dist = (s.Position - Position).sqrMagnitude;

                // 如果是超级星星，直接设为最近星星（优先级最高）
                if (s.IsSuperStar)
                {
                    // 如果当前最近星星也是超级星星，比较距离
                    if (nearestStar != null && nearestStar.IsSuperStar)
                    {
                        if (dist < nearestDist)
                        {
                            // 将原来的最近星星降级为第二近
                            secondNearestStar = nearestStar;
                            secondNearestDist = nearestDist;
                            nearestStar = s;
                            nearestDist = dist;
                        }
                        else
                        {
                            // 当前超级星星比已记录的最近超级星星远，设为第二近
                            if (secondNearestStar == null || dist < secondNearestDist)
                            {
                                secondNearestStar = s;
                                secondNearestDist = dist;
                            }
                        }
                    }
                    else
                    {
                        // 当前最近星星不是超级星星，将当前最近星星降级为第二近
                        if (nearestStar != null)
                        {
                            secondNearestStar = nearestStar;
                            secondNearestDist = nearestDist;
                        }

                        nearestStar = s;
                        nearestDist = dist;
                    }

                    hasStar = true;
                }
                else
                {
                    // 处理普通星星
                    if (nearestStar == null || dist < nearestDist)
                    {
                        // 将原来的最近星星降级为第二近
                        if (nearestStar != null)
                        {
                            secondNearestStar = nearestStar;
                            secondNearestDist = nearestDist;
                        }

                        nearestStar = s;
                        nearestDist = dist;
                        hasStar = true;
                    }
                    else if (secondNearestStar == null || dist < secondNearestDist)
                    {
                        secondNearestStar = s;
                        secondNearestDist = dist;
                        hasStar = true;
                    }
                }
            }

            // 设置成员变量
            ClosetStar = nearestStar;
            SecondClosetStar = secondNearestStar;

            return nearestStar;
        }

        private void TurretTargetDecision()
        {
            //目标坦克存活则炮口指向预测位置，反之则指向出生点
            if (!m_TargetTank.IsDead)
            {
                /*if (CanSeeOthers(m_TargetTank))
                {
                    TurretTurnTo(m_TargetTank.Position);
                    return;
                }*/

                if (m_StraightDistanceToTargetTank / Match.instance.FieldSize < 0.1f)
                {
                    
                    TurretTurnTo(m_TargetTank.Position);
                    return;
                }

                TurretTurnTo(m_TargetTankPredictPos);
            }
            else
            {
                TurretTurnTo(m_TargetTankRebornPos);
            }
        }

        private void FireDecision()
        {
            if (m_TargetTank.IsDead)
                return;

            Vector3 toTarget = m_TargetTankPredictPos - FirePos;

            bool needFire = false;
            if (Physics.Linecast(FirePos, toTarget, out var hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                {
                    //射线达到碰撞体距离大于坦克直线距离一定程度  即预测位置和坦克间没有墙壁
                    if (Mathf.Abs(m_StraightDistanceToTargetTank - hitInfo.distance) > 3f)
                    {
                        if (Vector3.Dot(TurretAiming, toTarget) > 0.99f)
                        {
                            needFire = true;
                        }
                    }
                }
            }

            canSeeOther = CanSeeOthers(m_TargetTank);
            if (canSeeOther)
            {
                needFire = true;
            }

            if (needFire)
            {
                Fire();
            }
        }

        private void UpdatePlanner()
        {
            m_GoalState.Clear();
            var prioritizedGoals = new SortedDictionary<float, WorldState>(
                Comparer<float>.Create((a, b) => b.CompareTo(a)) // 降序排序
            );

            CalculateGoalPriorities(prioritizedGoals);

            //根据目标的优先级逐个尝试构建计划
            foreach (var kvp in prioritizedGoals)
            {
                float priority = kvp.Key;
                WorldState goalState = kvp.Value;
                m_CurrentPlan = m_Planner.Plan(m_WorldState, goalState, m_CurrentPlan);

                if (m_CurrentPlan.Count > 0)
                {
                    m_ActionMachine.AddActionList(m_CurrentPlan);
                    return; // 找到可行的计划就停止
                }
            }
        }

        /// <summary>
        /// 动态计算目标优先级
        /// </summary>
        /// <param name="prioritizedGoals"></param>
        private Dictionary<string, float> m_DebugGoalPriorities = new Dictionary<string, float>(); //调试使用

        private void CalculateGoalPriorities(SortedDictionary<float, WorldState> prioritizedGoals)
        {
            prioritizedGoals.Clear(); //清空字典

            //规避子弹
            if (Match.instance.GetOppositeMissiles(Team).Count > 0)
            {
                var goal = new WorldState();
                goal.SetState(WorldStateKey.OppositeMissileCanHit, false);
                float priority = 0;

                if (m_WorldState.GetState(WorldStateKey.OppositeMissileCanHit))
                {
                    priority += 0.6f;
                }

                if (m_TargetTank.HP > HP)
                {
                    priority += 0.1f;
                }

                if (Mathf.Lerp(1f, 0f, distanceToHome / Match.instance.FieldSize) > 0.3f)
                {
                    priority += 0.1f;
                }

                if (!m_WorldState.GetState(WorldStateKey.SuperStarAvailable))
                {
                    priority += 0.2f;
                }

                prioritizedGoals[priority] = goal;
                m_DebugGoalPriorities["EvadeMissile"] = priority;
                if (Mathf.Approximately(priority, 1))
                {
                    return;
                }
            }
            else
            {
                m_DebugGoalPriorities["EvadeMissile"] = 0;
            }

            //回血
            if (HP < Match.instance.GlobalSetting.MaxHP)
            {
                var goal = new WorldState();
                goal.SetState(WorldStateKey.HasFullHP, true);

                if (!m_TargetTank.IsDead)
                {
                    // 计算优先级：血量越少，优先级越高（最大100）
                    float healthRatio = ((float)(m_TargetTank.HP - HP) / Match.instance.GlobalSetting.MaxHP) * 0.65f;
                    if (healthRatio < 0)
                    {
                        healthRatio = 0;
                    }

                    float distanceToHomeRatio = Mathf.Lerp(1f, 0f, distanceToHome / Match.instance.FieldSize) * 0.35f;
                    float priority = Mathf.Lerp(0f, 1f, healthRatio + distanceToHomeRatio);
                    m_DebugGoalPriorities["Heal"] = priority;
                    prioritizedGoals[priority] = goal;
                }
                else
                {
                    float healthRatio = ((float)(Match.instance.GlobalSetting.MaxHP - HP) / Match.instance.GlobalSetting.MaxHP) * 0.4f;
                    float distanceToHomeRatio = Mathf.Lerp(1f, 0f, distanceToHome / Match.instance.FieldSize) * 0.6f;
                    float priority = Mathf.Lerp(1f, 0f, healthRatio + distanceToHomeRatio);
                    m_DebugGoalPriorities["Heal"] = priority;
                    prioritizedGoals[priority] = goal;
                }
            }
            else
            {
                m_DebugGoalPriorities["Heal"] = 0;
            }

            //搜寻最近的星星
            if (ClosetStar != null)
            {
                var goal = new WorldState();
                goal.SetState(WorldStateKey.ScoreIncreased, true);

                float priority = Mathf.Lerp(1f, 0f, distanceToClosestStar / Match.instance.FieldSize);
                m_DebugGoalPriorities["CollectStar"] = priority;
                prioritizedGoals[priority] = goal;
            }
            else
            {
                m_DebugGoalPriorities["CollectStar"] = 0;
            }


            //去地图中间
            if (!IsDead)
            {
                if (!m_TargetTank.IsDead && canSeeOther)
                {
                    //对手位于基地且能互相看见优先远离
                    var goal = new WorldState();
                    goal.SetState(WorldStateKey.AtCenterPosition, true);
                    float priority =Mathf.Lerp(1f, 0f, oppositeDistanceToHome / Match.instance.FieldSize) * 0.65f +
                                    Mathf.Lerp(1f, 0f, distanceToOppositeHome / Match.instance.FieldSize) *0.35f;
                    m_DebugGoalPriorities["ToCenter"] = priority;
                    prioritizedGoals[priority] = goal;
                }
                else
                {
                    var goal = new WorldState();
                    goal.SetState(WorldStateKey.AtCenterPosition, true);
                    float priority = 0.2f;
                    m_DebugGoalPriorities["ToCenter"] = priority;
                    prioritizedGoals[priority] = goal;
                }
            }
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            DrawGoalPriorities();
        }

        private void DrawGoalPriorities()
        {
            if (m_DebugGoalPriorities.Count == 0)
                return;

#if UNITY_EDITOR
            // 计算起始位置（坦克上方）
            Vector3 panelPos = transform.position + Vector3.up * 4f;

            // 创建面板样式
            GUIStyle panelStyle = new GUIStyle()
            {
                normal = new GUIStyleState()
                {
                    textColor = Color.white,
                },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                richText = true,
                padding = new RectOffset(10, 10, 10, 10), // 内边距
                border = new RectOffset(2, 2, 2, 2) // 边框
            };

            // 构建优先级文本
            string prioritiesText = "<color=yellow><b>=== GOAP Priorities ===</b></color>\n\n";

            // 按优先级排序
            var sortedPriorities = new List<KeyValuePair<string, float>>(m_DebugGoalPriorities);
            sortedPriorities.Sort((a, b) => b.Value.CompareTo(a.Value)); // 按优先级降序

            for (int i = 0; i < sortedPriorities.Count; i++)
            {
                var kvp = sortedPriorities[i];
                string goalName = kvp.Key;
                float priority = kvp.Value;

                // 根据优先级设置颜色
                Color priorityColor = Color.Lerp(Color.green, Color.red, priority);
                string colorHex = ColorUtility.ToHtmlStringRGB(priorityColor);

                // 添加进度条效果
                int barLength = Mathf.RoundToInt(priority * 4); // 20个字符宽
                string progressBar = new string('█', barLength) + new string('░', 4 - barLength);

                prioritiesText += $"<color=#{colorHex}>[{progressBar}]</color> ";
                prioritiesText += $"<color=white>{goalName}:</color> ";
                prioritiesText += $"<color=#{colorHex}><b>{priority:F2}</b></color>";

                // 如果不是最后一项，添加换行
                if (i < sortedPriorities.Count - 1)
                {
                    prioritiesText += "\n";
                }
            }

            // 添加分隔线和当前状态
            prioritiesText += "\n\n<color=yellow>=== Current Status ===</color>\n";
            prioritiesText += $"<color=white>HP:</color> {HP}/{Match.instance?.GlobalSetting?.MaxHP ?? 100}\n";
            prioritiesText += $"<color=white>Stars:</color> {(ClosetStar != null ? "Available" : "None")}\n";
            prioritiesText += $"<color=white>Position:</color> {transform.position.ToString("F1")}";

            // 绘制面板
            Handles.Label(panelPos, prioritiesText, panelStyle);
            
#endif
        }
        
    }
}