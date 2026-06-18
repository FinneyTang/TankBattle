using System;
using System.Collections.Generic;
using AI.Base;
using AI.UtilityBased;
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace MXF
{
    // ==================== 策略 Action 基类 ====================

    /// <summary>
    ///     策略基类: 持有多个 Utility 组件并可选迟滞加分, 由 MyTank 每帧对候选列表评分后选择最高分执行.
    ///     子类需实现: GetUtility() — 聚合子效用分数; TakeAction() — 执行首次行动.
    /// </summary>
    public abstract class UtilityActions
    {
        protected readonly List<Utility> utilities = new List<Utility>();
        protected Tank owner;
        public virtual float SelfUtilityWhenAction { get; } = 0;

        public void SetOwner(Tank owner)
        {
            this.owner = owner;
        }

        /// <summary> 刷新所有关联 Utility 的分数. </summary>
        protected void RefreshScores()
        {
            foreach (Utility u in utilities)
            {
                u.CalcU(owner);
            }
        }

        public abstract float GetUtility();
        public abstract void TakeAction();
    }

    /// <summary>
    ///     方向性机动策略基类: 持有缓存的移动方向, 支持帧间方向继承和持续移动.
    ///     用于躲避 (AvoidAction) 和撤退 (RetreatFromEnemy) 等需要跨帧维持方向的策略.
    /// </summary>
    public abstract class DirectionalAction : UtilityActions
    {
        protected Vector3? cachedDirection;

        /// <summary> 当前缓存的移动方向 (世界坐标). </summary>
        public Vector3? CachedDirection => cachedDirection;

        /// <summary> 每帧维持当前方向移动. </summary>
        public abstract void MaintainMovement(Tank tank);

        /// <summary>
        ///     继承前一同类型策略的缓存方向.
        ///     返回 false 表示方向不可用, 需要重新 TakeAction.
        /// </summary>
        public virtual bool InheritCachedDirection(DirectionalAction prev)
        {
            cachedDirection = prev.cachedDirection;
            return cachedDirection.HasValue;
        }
    }

    // ==================== 具体策略实现 ====================

    /// <summary>
    ///     吃星策略: 针对单颗星星计算综合采集价值.
    ///     MyTank 为场上每颗活星创建一个 GetOneStar 实例作为候选策略,
    ///     通过多因子加权评分 (距离、争夺、密集度、敌方位置) 选出当前最值得采集的星星.
    ///     执行时: 设置 NavDestination 并调用 Move 前往星星位置.
    /// </summary>
    public class GetOneStar : UtilityActions
    {
        private readonly Star star;

        // ---- 具名 Utility 组件 ----
        private readonly BasicStarScore _basicScore = new BasicStarScore();
        private readonly DistanceUtility _distScore;
        private readonly StarContestScore _contestScore;
        private readonly StarDensityScore _densityScore;
        private readonly DistanceUtility _enemyHomeScore;
        private readonly EnemyProximityToStar _enemyProxScore;

        public GetOneStar(Star star)
        {
            this.star = star;
            _distScore = new DistanceUtility(star.Position, 75f, 100f, ResponseCurve.Quadratic);
            _contestScore = new StarContestScore(star);
            _densityScore = new StarDensityScore(star);
            _enemyHomeScore = new DistanceUtility(
                agent =>
                {
                    Tank enemy = EnemyHelper.GetAliveEnemy((Tank)agent);
                    return enemy != null ? Match.instance.GetRebornPos(enemy.Team) : Vector3.one * 1000f;
                }, 100f, 75f);
            _enemyProxScore = new EnemyProximityToStar(star);

            utilities.Add(_basicScore);
            utilities.Add(_distScore);
            utilities.Add(_contestScore);
            utilities.Add(_densityScore);
            utilities.Add(_enemyHomeScore);
            utilities.Add(_enemyProxScore);
        }
        public int StarID => star.ID;
        public override float SelfUtilityWhenAction { get; } = 20;
        public override void TakeAction()
        {
            ((MyTank)owner).SetNavDestination(star.Position);
            owner.Move(star.Position);
        }
        public override float GetUtility()
        {
            RefreshScores();
            float basicScore = _basicScore.GetLastScore() * TacticWeights.W_Star_BasicStar;
            float distScore = _distScore.GetLastScore() * TacticWeights.W_Star_Distance;
            float contestScore = _contestScore.GetLastScore() * TacticWeights.W_Star_Contest;
            float densityScore = _densityScore.GetLastScore() * TacticWeights.W_Star_Density;
            float enemyHomeScore = _enemyHomeScore.GetLastScore() * TacticWeights.W_Star_EnemyHome;
            float enemyProxScore = _enemyProxScore.GetLastScore() * TacticWeights.W_Star_EnemyProximity;
            float total = basicScore + distScore + contestScore + densityScore + enemyHomeScore + enemyProxScore;

            if (star != null && star.IsSuperStar)
                return total * TacticWeights.W_Star_SuperStar;
            return total;
        }
    }

    /// <summary> 追击策略: 当敌方血量低于我方时激活, 追踪并追杀残血敌人. 敌方离家越近压迫感越低, 避免追击到敌方基地. </summary>
    public class FinishEnemy : UtilityActions
    {
        private readonly SafetyFactor _safetyFactor = new SafetyFactor();
        private readonly DistanceUtility _enemyHomeScore;
        private readonly DistanceUtility _myProximity;

        public FinishEnemy()
        {
            _enemyHomeScore = new DistanceUtility(
                agent =>
                {
                    Tank enemy = EnemyHelper.GetAliveEnemy((Tank)agent);
                    return enemy != null ? Match.instance.GetRebornPos(enemy.Team) : Vector3.one * 1000f;
                }, 100f, 75f);
            _myProximity = new DistanceUtility(
                agent =>
                {
                    Tank enemy = EnemyHelper.GetAliveEnemy((Tank)agent);
                    return enemy?.Position ?? Vector3.one * 1000f;
                }, 100f, 75f);

            utilities.Add(_safetyFactor);
            utilities.Add(_enemyHomeScore);
            utilities.Add(_myProximity);
        }

        public override float SelfUtilityWhenAction { get; } = 50;

        public override void TakeAction()
        {
            Tank enemy = EnemyHelper.GetAliveEnemy(owner);
            if (enemy != null)
            {
                ((MyTank)owner).ClearNavDestination();
                owner.Move(enemy.Position);
            }
        }

        public override float GetUtility()
        {
            Tank enemy = EnemyHelper.GetAliveEnemy(owner);
            if (enemy == null) return -1000f;

            RefreshScores();
            float safetyFactor = _safetyFactor.GetLastScore();

            if (safetyFactor <= 0f) return -1000f;

            float enemyHomeScore = _enemyHomeScore.GetLastScore();
            float myProximity = _myProximity.GetLastScore();

            float finishUrge = safetyFactor * 100f * TacticWeights.W_Finish_Advantage;
            float pursuitBoost = myProximity * TacticWeights.W_Finish_Pursuit;
            float escapePenalty = enemyHomeScore * TacticWeights.W_Finish_EnemyHome;

            return finishUrge + pursuitBoost - escapePenalty;
        }
    }

    /// <summary>
    ///     接近敌人策略: 驱动坦克维持最佳交战距离 (约50m) 并持续追踪敌人位置.
    ///     综合距离效用 (高斯峰)、HP安全因子 (优势时平方放大接近意愿) 和子弹威胁惩罚,
    ///     确保在安全前提下保持火力覆盖. 执行和 MaintainMovement 都是每帧 tank.Move 追踪敌人.
    /// </summary>
    public class ApproachEnemy : UtilityActions
    {
        private readonly ApproachDistanceScore _distScore = new ApproachDistanceScore();
        private readonly SafetyFactor _safetyFactor = new SafetyFactor();
        private readonly IsImmediateAttack _threatDetector = new IsImmediateAttack();

        public ApproachEnemy()
        {
            utilities.Add(_distScore);
            utilities.Add(_safetyFactor);
            utilities.Add(_threatDetector);
        }
        public override float SelfUtilityWhenAction { get; } = 5;

        public override void TakeAction()
        {
            Tank enemy = EnemyHelper.GetAliveEnemy(owner);
            if (enemy != null)
            {
                owner.Move(enemy.Position);
            }
        }

        /// <summary> 每帧更新追踪目标, 保持坦克持续接近敌人. </summary>
        public void MaintainMovement(Tank tank)
        {
            Tank enemy = EnemyHelper.GetAliveEnemy(tank);
            if (enemy != null)
                tank.Move(enemy.Position);
        }

        public override float GetUtility()
        {
            RefreshScores();
            float distScore = _distScore.GetLastScore() * TacticWeights.W_Appr_Distance;
            float safetyFactor = _safetyFactor.GetLastScore();
            float threatPenalty = _threatDetector.GetLastScore() / 11.45f;

            float advantage = Mathf.Max(0f, safetyFactor);
            float boost = 1f + advantage * advantage * TacticWeights.W_Appr_AdvantageBoost;

            return distScore * boost - threatPenalty;
        }
    }

    /// <summary> 撤退策略 (方向性): 计算远离危胁敌人的方向并持续沿该方向移动. 敌人越近撤退意愿越强, HP劣势时撤退意愿指数放大. </summary>
    public class RetreatFromEnemy : DirectionalAction
    {
        private readonly RetreatScore _retreatScore = new RetreatScore();
        private readonly SafetyFactor _safetyFactor = new SafetyFactor();
        private readonly ControlCenterScore _centerScore = new ControlCenterScore();

        public RetreatFromEnemy()
        {
            utilities.Add(_retreatScore);
            utilities.Add(_safetyFactor);
            utilities.Add(_centerScore);
        }

        public override float SelfUtilityWhenAction { get; } = 30;

        public override void TakeAction()
        {
            Tank enemy = EnemyHelper.GetAliveEnemy(owner);
            if (enemy == null)
            {
                cachedDirection = null;
                return;
            }
            Vector3 awayDir = (owner.Position - enemy.Position).normalized;
            Vector3 validDir = FindValidRetreatDirection(awayDir);
            cachedDirection = validDir != Vector3.zero ? validDir : (Vector3?)null;
            ((MyTank)owner).ClearNavDestination();
            if (validDir != Vector3.zero)
                owner.MoveInDirection(validDir);
        }

        private Vector3 FindValidRetreatDirection(Vector3 awayDir)
        {
            const float checkRadius = 10f;
            const float maxAngle = 90f;
            const float angleStep = 10f;

            Vector2 away2D = new Vector2(awayDir.x, awayDir.z);
            Vector3 origin = new Vector3(owner.Position.x, 0.5f, owner.Position.z);

            for (float angle = 0f; angle <= maxAngle; angle += angleStep)
            {
                float[] offsets = angle == 0f
                    ? new[] { 0f }
                    : new[] { angle, -angle };

                foreach (float offset in offsets)
                {
                    float rad = offset * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    Vector2 rotated = new Vector2(
                        away2D.x * cos - away2D.y * sin,
                        away2D.x * sin + away2D.y * cos);

                    Vector3 dir3D = new Vector3(rotated.x, 0f, rotated.y);
                    Vector3 target = owner.Position + dir3D * checkRadius;

                    if (NavMesh.SamplePosition(target, out NavMeshHit _, 1f, NavMesh.AllAreas)
                        && !NavMesh.Raycast(origin, target, out NavMeshHit _, NavMesh.AllAreas))
                    {
                        return dir3D;
                    }
                }
            }
            return Vector3.zero;
        }

        public override void MaintainMovement(Tank tank)
        {
            if (!cachedDirection.HasValue) return;
            tank.MoveInDirection(cachedDirection.Value);
        }

        public override float GetUtility()
        {
            RefreshScores();
            float retreatScore = _retreatScore.GetLastScore() * TacticWeights.W_Retreat_Score;
            float safetyFactor = _safetyFactor.GetLastScore();
            float centerScore = _centerScore.GetLastScore();

            float disadvantage = MathF.Min(Mathf.Max(0f, -safetyFactor), 0.2f);
            float threatMult = TacticWeights.W_Retreat_BaseMul + disadvantage * 5 * TacticWeights.W_Retreat_DisadvBoost;

            float centerPenalty = centerScore * TacticWeights.W_Retreat_CenterScale;

            return retreatScore * threatMult - centerPenalty;
        }
    }

    /// <summary>
    ///     占中策略: 抢占地图中心 (Vector3.zero) 以争夺超级星.
    ///     评分由 ControlCenterScore 驱动, 越接近比赛中场时间分数指数上升,
    ///     在超级星刷新时达到峰值. 执行时: 设置 NavDestination 并 Move 到原点.
    /// </summary>
    public class ControlCenter : UtilityActions
    {
        private readonly ControlCenterScore _centerScore = new ControlCenterScore();

        public ControlCenter()
        {
            utilities.Add(_centerScore);
        }
        public override float SelfUtilityWhenAction { get; } = 40;
        public override void TakeAction()
        {
            ((MyTank)owner).SetNavDestination(Vector3.zero);
            owner.Move(Vector3.zero);
        }
        public override float GetUtility()
        {
            RefreshScores();
            return _centerScore.GetLastScore() * TacticWeights.W_Ctrl_Center;
        }
    }

    /// <summary>
    ///     回家策略: 驱动残血坦克返回出生点回血.
    ///     综合因子: 安全劣势 (HP差距)、恢复需求 (残血程度)、距家距离和超级星压力.
    ///     HP越低、距家越近时紧迫度越高; 超级星临近时劣势方的回家紧迫度额外加算,
    ///     避免因回血错失占中时机. 执行时: 设置 NavDestination 并 Move 到出生点.
    /// </summary>
    public class BackHome : UtilityActions
    {
        private readonly SafetyFactor _safetyFactor = new SafetyFactor();
        private readonly RecoverHPScore _recoverScore = new RecoverHPScore();
        private readonly DistanceUtility _distScore;
        private readonly ControlCenterScore _centerScore = new ControlCenterScore();

        public BackHome()
        {
            _distScore = new DistanceUtility(
                agent => Match.instance.GetRebornPos(((Tank)agent).Team), 100f, 75f);
            utilities.Add(_safetyFactor);
            utilities.Add(_recoverScore);
            utilities.Add(_distScore);
            utilities.Add(_centerScore);
        }
        public override float SelfUtilityWhenAction { get; } = 0;
        public override void TakeAction()
        {
            Vector3 rebornPos = Match.instance.GetRebornPos(owner.Team);
            ((MyTank)owner).SetNavDestination(rebornPos);
            owner.Move(rebornPos);
        }
        public override float GetUtility()
        {
            RefreshScores();
            float safetyFactor = _safetyFactor.GetLastScore();
            float recoverScore = _recoverScore.GetLastScore() * TacticWeights.W_Home_Recover;
            float distScore = _distScore.GetLastScore() * TacticWeights.W_Home_Distance;
            float centerScore = _centerScore.GetLastScore() / 100f;

            float disadvantage = Mathf.Max(0f, -safetyFactor);

            float urgency = disadvantage * TacticWeights.W_Home_DisadvScore + recoverScore;

            float proximity = distScore / TacticWeights.W_Home_DistDiv + TacticWeights.W_Home_DistBase;

            float starPressure = recoverScore * centerScore * TacticWeights.W_Home_CenterScale;

            return urgency * proximity + starPressure;
        }
    }

    /// <summary>
    ///     躲避策略 (方向性): 检测来袭子弹后选择最优躲避方向.
    ///     两级决策: 优先尝试急停 (行进方向与子弹夹角 ∈ [30°,150°] 时停车), 否则枚举 36 方向选择安全路径.
    ///     方向评估分 Tier1 (完全安全) 和 Tier2 (次安全), 综合行进方向一致性和导航目的地偏好.
    ///     支持 Plan A (当前速度) 和 Plan B (先停车再从零加速) 两种机动方案.
    ///     锁定子弹直到 minDist > WarningDist 或急停完成, 释放锁后策略退出.
    /// </summary>
    public class AvoidAction : DirectionalAction
    {
        internal const float TargetRadius = 10f;
        internal const float DesiredSafeDist = 4.5f;
        internal const float MinSafeDist = 3.75f;
        internal const int AngleSteps = 36;
        private const float MaxPredictTime = 10f;
        private const float SampleInterval = 0.05f;
        private const float ThreatScore = 200f;
        private const float DangerDist = 4f;
        private const float WarningDist = 6f;
        private const float StopDodgePerpendicular = 90f;
        private const float StopDodgeHalfAngle = 60f;
        private float _agentAccel;

        private float _agentMaxSpeed;
        private bool _isStopDodge;

        private readonly ControlCenterScore _centerScore;
        private readonly SafetyFactor _safetyFactor;
        private readonly DistanceUtility _enemyProxScore;

#if UNITY_EDITOR
        internal int _debugBestIdx = -1;
        internal float[] _debugMinDists;
        internal Vector3 _debugTankPos;

        internal int DebugBestIdx => _debugBestIdx;
        internal float[] DebugMinDists => _debugMinDists;
        internal Vector3 DebugTankPos => _debugTankPos;
#endif
        private Vector3? _navDest;

        private bool IsStopThenGo { get; set; }

        public AvoidAction()
        {
            _centerScore = new ControlCenterScore();
            _safetyFactor = new SafetyFactor();
            _enemyProxScore = new DistanceUtility(
                agent => EnemyHelper.GetAliveEnemy((Tank)agent)?.Position ?? Vector3.one * 1000f,
                100f, 50f, ResponseCurve.Quadratic);
            utilities.Add(_centerScore);
            utilities.Add(_safetyFactor);
            utilities.Add(_enemyProxScore);
        }

        public override float SelfUtilityWhenAction { get; } = 30;

        private BulletTracker GetTracker()
        {
            return ((MyTank)owner).BulletTracker;
        }

        public override float GetUtility()
        {
            RefreshScores();
            BulletTracker tracker = GetTracker();
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissilesEx(owner.Team);
            tracker.Refresh(owner, missiles);

            if (tracker.LockedBulletId >= 0)
            {
                if (CheckDodgeComplete(tracker))
                {
                    tracker.ReleaseLock();
                    cachedDirection = null;
                    return 0f;
                }
            }
            else if (!HasIncomingThreat())
            {
                return 0f;
            }
            else
            {
                tracker.LockOnNearestThreat();
            }

            float centerPenalty = _centerScore.GetLastScore() * TacticWeights.W_Avoid_CenterScale;

            float safetyFactor = _safetyFactor.GetLastScore();
            float disadvantage = Mathf.Max(0f, -safetyFactor);
            float disadvantageBoost = disadvantage * 250f;

            float enemyProxPenalty = _enemyProxScore.GetLastScore() * TacticWeights.W_Avoid_EnemyProx / 100f;

            return ThreatScore - centerPenalty + disadvantageBoost - enemyProxPenalty;
        }

        public override void TakeAction()
        {
            BulletTracker tracker = GetTracker();
            ReadNavMeshParams();

            if (CanStopDodge(tracker))
            {
                _isStopDodge = true;
                cachedDirection = null;
                owner.Move(owner.Position);
                return;
            }

            _isStopDodge = false;
            ExecuteFreeDodge();
        }

        private bool CheckDodgeComplete(BulletTracker tracker)
        {
            Missile locked = tracker.GetLockedMissile();
            if (locked == null) return true;
            return _isStopDodge ? IsStopDodgeComplete(locked) : IsFreeDodgeComplete(locked);
        }

        private bool IsStopDodgeComplete(Missile locked)
        {
            ReadNavMeshParams();
            Vector2 pos = owner.Position.ToXZ();
            Vector2 startVel = owner.Velocity.ToXZ();
            Vector2 fwd = owner.Forward.ToXZ().normalized;

            Dictionary<int, Missile> solo = new Dictionary<int, Missile> { { locked.ID, locked } };
            bool hit = WouldBeHit(pos, startVel, fwd, solo, out float minDist);
            return !hit && minDist > DangerDist;
        }

        private bool IsFreeDodgeComplete(Missile locked)
        {
            Vector2 bPos = locked.Position.ToXZ();
            Vector2 bVel = locked.Velocity.ToXZ();
            Vector2 myPos = owner.Position.ToXZ();

            float bSpeedSq = bVel.sqrMagnitude;
            if (bSpeedSq < 0.001f) return false;

            float tCa = -Vector2.Dot(bPos - myPos, bVel) / bSpeedSq;
            if (tCa < 0f || tCa > MaxPredictTime) return true;

            float minDist = (bPos - myPos + bVel * tCa).magnitude;
            return minDist > WarningDist;
        }

        private bool HasIncomingThreat()
        {
            Vector2 pos = owner.Position.ToXZ();
            Vector2 rawVel = owner.Velocity.ToXZ();
            float v0 = rawVel.magnitude;

            Vector2 fwd = v0 > 0.1f
                ? rawVel.normalized
                : owner.Forward.ToXZ().normalized;

            ReadNavMeshParams();
            const float maxPredict = 3f;
            const float contactRadius = 5.5f;

            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissilesEx(owner.Team);
            float hitTime = BulletTrajectoryUtils.GetEarliestHitTime(
                pos, fwd, v0, _agentMaxSpeed, _agentAccel,
                missiles, contactRadius, maxPredict);
            return hitTime < maxPredict;
        }

        private bool CanStopDodge(BulletTracker tracker)
        {
            Missile locked = tracker.GetLockedMissile();
            if (locked == null) return false;

            Vector2 tankDir = new Vector2(owner.Velocity.x, owner.Velocity.z).normalized;
            Vector2 bulletDir = new Vector2(locked.Velocity.x, locked.Velocity.z).normalized;
            float angle = Mathf.Abs(Vector2.Angle(tankDir, bulletDir));
            if (angle < StopDodgePerpendicular - StopDodgeHalfAngle || angle > StopDodgePerpendicular + StopDodgeHalfAngle)
                return false;

            Vector2 bPos = locked.Position.ToXZ();
            Vector2 bVel = locked.Velocity.ToXZ();
            Vector2 myPos = owner.Position.ToXZ();

            float bSpeedSq = bVel.sqrMagnitude;
            if (bSpeedSq < 0.001f) return false;

            float t = -Vector2.Dot(bPos - myPos, bVel) / bSpeedSq;
            if (t < 0f || t > 3f) return true;

            float wallHit = BulletTrajectoryUtils.GetWallHitTime(bPos, bVel, 3f);
            if (t > wallHit) return true;

            float minDist = (bPos - myPos + bVel * t).magnitude;
            return minDist > DangerDist;
        }

        /// <summary>
        ///     方向评估结果: 存储自由躲避的 36 方向枚举评分结果.
        ///     Tier1 — minDist >= DesiredSafeDist (完全安全), 按行进方向+目的地偏好角排序
        ///     Tier2 — MinSafeDist &lt;= minDist &lt; DesiredSafeDist (次安全), 按最近距离排序
        ///     PlanB — 从零速开始加速 (先停车再躲避)
        /// </summary>
        public struct DirEvalResult
        {
            public int BestT1Idx;
            public float BestT1Angle;
            public bool BestT1IsB;
            public int BestT2Idx;
            public float BestT2Score;
            public bool BestT2IsB;
        }

        private void ExecuteFreeDodge()
        {
            Vector3 pos3D = owner.Position;
            Vector2 pos = pos3D.ToXZ();
            Vector2 curVel = owner.Velocity.ToXZ();
            float v0 = curVel.magnitude;

            ReadNavMeshParams();
            Vector2 moveDir = v0 > 0.1f ? curVel.normalized : owner.Forward.ToXZ().normalized;

            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissilesEx(owner.Team);

#if UNITY_EDITOR
            _debugMinDists = new float[AngleSteps];
            _debugTankPos = pos3D;
#endif
            Vector3? dest = ((MyTank)owner).NavDestination;
            Tank enemy = EnemyHelper.GetAliveEnemy(owner);
            if (enemy != null && Vector3.Distance(owner.Position, enemy.Position) > 50f)
                _navDest = dest;
            else
                _navDest = null;

            DirEvalResult result = default;
            result.BestT1Idx = -1;
            result.BestT1Angle = float.MaxValue;
            result.BestT2Idx = -1;
            result.BestT2Score = 0f;

            for (int i = 0; i < AngleSteps; i++)
            {
                EvalDir(i, pos, pos3D, curVel, moveDir, missiles, ref result, false);
            }

            Vector2 zeroVel = Vector2.zero;
            for (int i = 0; i < AngleSteps; i++)
            {
                EvalDir(i, pos, pos3D, zeroVel, moveDir, missiles, ref result, true);
            }

            int bestIdx;
            if (result.BestT1Idx >= 0)
            {
                bestIdx = result.BestT1Idx;
                IsStopThenGo = result.BestT1IsB;
            }
            else if (result.BestT2Idx >= 0)
            {
                bestIdx = result.BestT2Idx;
                IsStopThenGo = result.BestT2IsB;
            }
            else
            {
                bestIdx = -1;
                IsStopThenGo = false;
            }

#if UNITY_EDITOR
            _debugBestIdx = bestIdx;
#endif

            if (bestIdx >= 0)
            {
                float rad = bestIdx * (360f / AngleSteps) * Mathf.Deg2Rad;
                cachedDirection = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                if (IsStopThenGo) owner.Move(owner.Position);
            }
            else
            {
                cachedDirection = null;
            }
        }

        private void EvalDir(
            int i, Vector2 pos, Vector3 pos3D, Vector2 startVel,
            Vector2 moveDir, Dictionary<int, Missile> missiles,
            ref DirEvalResult result, bool isPlanB)
        {
            float rad = i * (360f / AngleSteps) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

            Vector3 origin = new Vector3(pos3D.x, 0.5f, pos3D.z);
            Vector3 target3D = new Vector3(pos3D.x + dir.x * TargetRadius, pos3D.y, pos3D.z + dir.y * TargetRadius);

            if (!NavMesh.SamplePosition(target3D, out NavMeshHit _, 1f, NavMesh.AllAreas))
            {
#if UNITY_EDITOR
                _debugMinDists[i] = -1f;
#endif
                return;
            }

            if (NavMesh.Raycast(origin, target3D, out NavMeshHit _, NavMesh.AllAreas))
            {
#if UNITY_EDITOR
                _debugMinDists[i] = -1f;
#endif
                return;
            }

            if (WouldBeHit(pos, startVel, dir, missiles, out float minDist)) return;
#if UNITY_EDITOR
            _debugMinDists[i] = minDist;
#endif

            float absAngle = Mathf.Abs(Vector2.SignedAngle(moveDir, dir));

            float angleToDest = 0f;
            if (_navDest.HasValue)
            {
                Vector2 dest2D = _navDest.Value.ToXZ();
                Vector2 toDest = (dest2D - pos).normalized;
                angleToDest = Mathf.Abs(Vector2.SignedAngle(dir, toDest));
            }

            if (minDist >= DesiredSafeDist)
            {
                float combinedAngle = absAngle * 1.0f + angleToDest * 0.5f;
                if (combinedAngle < result.BestT1Angle)
                {
                    result.BestT1Angle = combinedAngle;
                    result.BestT1Idx = i;
                    result.BestT1IsB = isPlanB;
                }
            }
            else
            {
                float score = minDist - angleToDest * 0.1f;
                if (score > result.BestT2Score)
                {
                    result.BestT2Score = score;
                    result.BestT2Idx = i;
                    result.BestT2IsB = isPlanB;
                }
            }
        }

        private bool WouldBeHit(
            Vector2 pos, Vector2 startVel, Vector2 desiredDir,
            Dictionary<int, Missile> missiles, out float minDist)
        {
            minDist = float.MaxValue;
            if (missiles == null || missiles.Count == 0) return false;

            float minSafeSq = MinSafeDist * MinSafeDist;
            int steps = Mathf.CeilToInt(MaxPredictTime / SampleInterval);
            Vector2[] tankTraj = new Vector2[steps + 1];

            Vector2 tankVel = startVel;
            Vector2 tankPos = pos;
            tankTraj[0] = tankPos;
            Vector2 desiredVel = desiredDir * _agentMaxSpeed;

            for (int s = 1; s <= steps; s++)
            {
                NavMeshUtils.SimulateMovementStep(ref tankVel, desiredVel, _agentAccel, SampleInterval);
                tankPos += tankVel * SampleInterval;
                tankTraj[s] = tankPos;
            }

            bool hit = false;
            foreach (KeyValuePair<int, Missile> pair in missiles)
            {
                Missile m = pair.Value;
                Vector2 bPos = m.Position.ToXZ();
                Vector2 bVel = m.Velocity.ToXZ();
                if (bVel.sqrMagnitude < 0.001f) continue;

                float wallHitTime = BulletTrajectoryUtils.GetWallHitTime(bPos, bVel, MaxPredictTime);
                float minDistSq = float.MaxValue;

                for (int s = 0; s <= steps; s++)
                {
                    float t = s * SampleInterval;
                    if (t > wallHitTime + 0.0001f) break;
                    float dSq = (bPos + bVel * t - tankTraj[s]).sqrMagnitude;
                    if (dSq < minDistSq) minDistSq = dSq;
                }

                float dist = Mathf.Sqrt(minDistSq);
                if (dist < minDist) minDist = dist;
                if (minDistSq < minSafeSq) hit = true;
            }
            return hit;
        }

        public override bool InheritCachedDirection(DirectionalAction prev)
        {
            if (!(prev is AvoidAction prevAvoid)) return false;
            cachedDirection = prevAvoid.cachedDirection;
            IsStopThenGo = prevAvoid.IsStopThenGo;
            _isStopDodge = prevAvoid._isStopDodge;
            if (!cachedDirection.HasValue || _isStopDodge) return false;

            ReadNavMeshParams();
            Vector2 pos = owner.Position.ToXZ();
            Vector2 curVel = owner.Velocity.ToXZ();
            Vector2 cachedDir2D = cachedDirection.Value.ToXZ();
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissilesEx(owner.Team);

            if (WouldBeHit(pos, curVel, cachedDir2D, missiles, out _))
            {
                cachedDirection = null;
                IsStopThenGo = false;
                return false;
            }
            return true;
        }

        public override void MaintainMovement(Tank tank)
        {
            if (_isStopDodge || !cachedDirection.HasValue) return;
            Vector3 dir = cachedDirection.Value;

            if (IsStopThenGo)
            {
                float speed = tank.Velocity.ToXZ().magnitude;
                if (speed > 1f) return;
                IsStopThenGo = false;
            }

            Vector3 origin = new Vector3(tank.Position.x, 0.5f, tank.Position.z);
            Vector3 rawTarget = tank.Position + dir * TargetRadius;
            if (NavMesh.Raycast(origin, rawTarget, out NavMeshHit _, NavMesh.AllAreas))
                return;

            tank.Move(rawTarget);
        }

        private void ReadNavMeshParams()
        {
            NavMeshAgent agent = owner.GetComponent<NavMeshAgent>();
            _agentMaxSpeed = agent != null ? agent.speed : 10f;
            _agentAccel = agent != null ? agent.acceleration : 16f;
        }
    }
}
