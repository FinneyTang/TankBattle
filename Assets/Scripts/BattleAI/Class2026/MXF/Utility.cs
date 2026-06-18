using System;
using System.Collections.Generic;
using AI.Base;
using AI.UtilityBased;
using Main;
using UnityEngine;

namespace MXF
{
    /// <summary> Vector3→Vector2 投影到 XZ 平面的扩展方法. </summary>
    public static class VectorExt
    {
        public static Vector2 ToXZ(this Vector3 v) => new Vector2(v.x, v.z);
    }

    /// <summary> 通用辅助方法. </summary>
    internal static class EnemyHelper
    {
        public static Tank GetAliveEnemy(Tank t)
        {
            Tank enemy = Match.instance.GetOppositeTank(t.Team);
            return enemy != null && !enemy.IsDead ? enemy : null;
        }
    }

    /// <summary> 距离效用响应曲线类型: 控制距离→效用的映射形状. </summary>
    public enum ResponseCurve
    {
        Linear,
        Quadratic,
        Cubic
    }

    // ==================== Utility 效用组件 ====================

    /// <summary>
    ///     接近距离效用: 越远分数越高, 100m处75分, 线性上升.
    ///     公式: score = (d / MaxDist) * MaxScore
    /// </summary>
    public class ApproachDistanceScore : Utility
    {
        private const float MaxDist = 100f;
        private const float MaxScore = 75f;

        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank enemy = EnemyHelper.GetAliveEnemy(t);
            if (enemy == null) return 0f;

            float d = Vector3.Distance(t.Position, enemy.Position);
            return Mathf.Clamp01(d / MaxDist) * MaxScore;
        }
    }

    /// <summary>
    ///     撤退距离效用: 敌人越近分数立方增长.
    ///     50m外可忽略, 25m处100, 0m处200(上限).
    ///     立方曲线: 理论峰值800, 200处截顶.
    /// </summary>
    public class RetreatScore : Utility
    {
        private const float DangerDist = 50f;
        private const float MaxScore = 200f;
        private const float RawPeak = 800f;

        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank enemy = EnemyHelper.GetAliveEnemy(t);
            if (enemy == null) return 0f;

            float d = NavMeshUtils.CalcPathDist(t, enemy.Position);
            if (d > DangerDist) return 0f;
            float tNorm = 1f - d / DangerDist;
            return Mathf.Min(MaxScore, RawPeak * tNorm * tNorm * tNorm);
        }
    }

    /// <summary>
    ///     统一距离效用: 通过 NavMesh 路径距离计算效用值, 越近分数越高.
    ///     curve 参数控制响应曲线形状:
    ///       Linear   — score = MaxScore * (1 - clamp(dist/MaxDist))
    ///       Quadratic— score = MaxScore * (1 - clamp(dist/MaxDist))^2
    ///       Cubic    — score = MaxScore * (1 - clamp(dist/MaxDist))^3
    /// </summary>
    public class DistanceUtility : Utility
    {
        private readonly Func<IAgent, Vector3> _getTargetPos;
        private readonly float _maxDist;
        private readonly float _maxScore;
        private readonly ResponseCurve _curve;

        public DistanceUtility(Vector3 targetPos, float maxScore = 100f, float maxDist = 100f,
            ResponseCurve curve = ResponseCurve.Linear)
            : this(_ => targetPos, maxScore, maxDist, curve) { }

        public DistanceUtility(Func<IAgent, Vector3> getTargetPos, float maxScore = 100f,
            float maxDist = 100f, ResponseCurve curve = ResponseCurve.Linear)
        {
            _getTargetPos = getTargetPos;
            _maxScore = maxScore;
            _maxDist = maxDist;
            _curve = curve;
        }

        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Vector3 target = _getTargetPos(agent);
            float dist = NavMeshUtils.CalcPathDist(t, target);
            float tNorm = 1f - Mathf.Clamp01(dist / _maxDist);

            switch (_curve)
            {
                case ResponseCurve.Quadratic: return _maxScore * tNorm * tNorm;
                case ResponseCurve.Cubic:     return _maxScore * tNorm * tNorm * tNorm;
                default:                       return _maxScore * tNorm;
            }
        }
    }

    /// <summary>
    ///     星星抢夺效用: 我方距星更近→正分(平方放大), 敌方更近→负分.
    ///     公式: score = sign(delta) * delta^2 * weight / 100
    /// </summary>
    public class StarContestScore : Utility
    {
        private readonly Star _star;

        public StarContestScore(Star star) { _star = star; }

        protected override float OnCalcU(IAgent agent)
        {
            if (_star == null) return 0f;
            Tank t = (Tank)agent;

            float myDist = NavMeshUtils.CalcPathDist(t, _star.Position);

            List<Tank> oppTanks = Match.instance.GetOppositeTanks(t.Team);
            if (oppTanks == null || oppTanks.Count == 0) return 500f;

            float enemyMinDist = float.MaxValue;
            foreach (Tank opp in oppTanks)
            {
                if (opp == null || opp.IsDead) continue;
                float d = NavMeshUtils.CalcPathDist(opp, _star.Position);
                if (d < enemyMinDist) enemyMinDist = d;
            }
            if (enemyMinDist >= float.MaxValue * 0.5f) return 0f;

            float delta = enemyMinDist - myDist;
            float sign = Mathf.Sign(delta);
            return sign * delta * delta * 0.5f / 100;
        }
    }

    /// <summary> 星星基本价值: 场上存在至少一颗星星时返回固定分 20, 无星时返回 0. </summary>
    public class BasicStarScore : Utility
    {
        protected override float OnCalcU(IAgent agent)
        {
            Dictionary<int, Star> stars = Match.instance.GetStars();
            return stars != null && stars.Count > 0 ? 20f : 0f;
        }
    }

    /// <summary> 星星密集度 (K近邻距离): 越密集分越高. </summary>
    public class StarDensityScore : Utility
    {
        private readonly int _k = 3;
        private readonly float _maxDist = 75f;
        private readonly Star _star;

        public StarDensityScore(Star star) { _star = star; }

        protected override float OnCalcU(IAgent agent)
        {
            if (_star == null) return 0f;

            Dictionary<int, Star> allStars = Match.instance.GetStars();
            if (allStars == null || allStars.Count <= 1) return 0f;

            Vector2 sp = _star.Position.ToXZ();

            List<float> dists = new List<float>();
            foreach (KeyValuePair<int, Star> pair in allStars)
            {
                if (pair.Value == _star) continue;
                Vector2 op = pair.Value.Position.ToXZ();
                dists.Add(Vector2.Distance(sp, op));
            }

            if (dists.Count == 0) return 0f;
            dists.Sort();

            int count = Mathf.Min(_k, dists.Count);
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += dists[i];
            float avgDist = sum / count;

            return 1f - Mathf.Clamp01(avgDist / _maxDist);
        }
    }

    /// <summary> 安全因子: 敌我HP差距归一化到 [-1, 1]. 正=优势, 负=劣势. </summary>
    public class SafetyFactor : Utility
    {
        private const float HpPerHit = 20f;
        private const float MaxHits = 5f;

        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = EnemyHelper.GetAliveEnemy(t);
            float enemyHits = oppTank == null ? 5f : Mathf.CeilToInt(oppTank.HP / HpPerHit);

            float deltaHit = Mathf.CeilToInt(t.HP / HpPerHit) - enemyHits;
            return Mathf.Clamp(deltaHit / MaxHits, -1f, 1f);
        }
    }

    /// <summary> 生命恢复效用: HP越低分越高 (满血=0, 空血=100), 驱动残血坦克回家. </summary>
    public class RecoverHPScore : Utility
    {
        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            int hit = Mathf.CeilToInt(t.HP / 20f);
            return (5 - hit) * 20f;
        }
    }

    /// <summary> 中心控制效用: 越接近超级星刷新时间, 分数指数上升. </summary>
    public class ControlCenterScore : Utility
    {
        private readonly float _maxScore = 100f;
        private readonly float _timeConstant = 15f;

        protected override float OnCalcU(IAgent agent)
        {
            float halfTime = Match.instance.GlobalSetting.MatchTime * 0.5f;
            float elapsed = Match.instance.GlobalSetting.MatchTime - Match.instance.RemainingTime;
            float timeUntilSpawn = halfTime - elapsed;

            if (timeUntilSpawn <= 0f) return 0f;
            return _maxScore * Mathf.Exp(-timeUntilSpawn / _timeConstant);
        }
    }

    /// <summary> 敌人距星距离效用: 敌人越近分数越高 (0~100), 用于吃星策略惩罚敌方抢夺同一颗星. </summary>
    public class EnemyProximityToStar : Utility
    {
        private readonly float _maxDist;
        private readonly Star _star;
        public EnemyProximityToStar(Star star, float maxDist = 50f)
        {
            _star = star;
            _maxDist = maxDist;
        }
        protected override float OnCalcU(IAgent agent)
        {
            if (_star == null) return 0f;
            Tank enemy = EnemyHelper.GetAliveEnemy((Tank)agent);
            if (enemy == null) return 0f;
            float dist = NavMeshUtils.CalcPathDist(enemy, _star.Position);
            return 100f * (1f - Mathf.Clamp01(dist / _maxDist));
        }
    }

    /// <summary> 子弹轨迹工具: 撞墙时间 + 两阶段直线轨迹命中检测. </summary>
    static class BulletTrajectoryUtils
    {
        public static float GetWallHitTime(Vector2 bulletPos, Vector2 bulletVel, float maxTime)
        {
            float speed = bulletVel.magnitude;
            if (speed < 0.001f) return maxTime;

            Vector3 start = new Vector3(bulletPos.x, 0, bulletPos.y);
            Vector3 dir = new Vector3(bulletVel.x, 0, bulletVel.y).normalized;
            float maxDist = speed * maxTime;

            if (Physics.Linecast(start, start + dir * maxDist, out RaycastHit hit, PhysicsUtils.LayerMaskScene))
                return hit.distance / speed;
            return maxTime;
        }

        public static float GetEarliestHitTime(
            Vector2 tankPos, Vector2 tankDir, float v0,
            float maxSpeed, float acceleration,
            Dictionary<int, Missile> missiles,
            float contactRadius, float maxPredictTime,
            float sampleInterval = 0.05f)
        {
            if (missiles == null || missiles.Count == 0) return float.MaxValue;

            float tAccel = Mathf.Max(0f, (maxSpeed - v0) / acceleration);
            float dAccel = v0 * tAccel + 0.5f * acceleration * tAccel * tAccel;
            Vector2 posAtAccelEnd = tankPos + tankDir * dAccel;
            float contactSq = contactRadius * contactRadius;
            float bestTime = float.MaxValue;

            foreach (KeyValuePair<int, Missile> pair in missiles)
            {
                Missile m = pair.Value;
                Vector2 bPos = m.Position.ToXZ();
                Vector2 bVel = m.Velocity.ToXZ();
                if (bVel.sqrMagnitude < 0.001f) continue;

                float wallHitTime = GetWallHitTime(bPos, bVel, maxPredictTime);

                bool hit = false;
                float hitTime = float.MaxValue;

                for (float t = 0f; t <= tAccel + 0.0001f; t += sampleInterval)
                {
                    if (t > maxPredictTime || t >= wallHitTime) break;
                    float dist = v0 * t + 0.5f * acceleration * t * t;
                    Vector2 tankAt = tankPos + tankDir * dist;
                    if ((bPos + bVel * t - tankAt).sqrMagnitude < contactSq)
                    {
                        hit = true;
                        hitTime = t;
                        break;
                    }
                }

                if (!hit && tAccel < maxPredictTime && tAccel < wallHitTime)
                {
                    Vector2 bAtAccel = bPos + bVel * tAccel;
                    Vector2 relPos = bAtAccel - posAtAccelEnd;
                    Vector2 relVel = bVel - tankDir * maxSpeed;
                    float relSpeedSq = relVel.sqrMagnitude;

                    if (relSpeedSq < 0.001f)
                    {
                        if (relPos.sqrMagnitude < contactSq)
                        {
                            hit = true;
                            hitTime = tAccel;
                        }
                    }
                    else
                    {
                        float tRel = -Vector2.Dot(relPos, relVel) / relSpeedSq;
                        if (tRel >= 0f)
                        {
                            float tAbs = tAccel + tRel;
                            if (tAbs <= maxPredictTime && tAbs < wallHitTime
                                                       && (relPos + relVel * tRel).magnitude < contactRadius)
                            {
                                hit = true;
                                hitTime = tAbs;
                            }
                        }
                    }
                }

                if (hit && hitTime < bestTime)
                    bestTime = hitTime;
            }
            return bestTime;
        }
    }

    /// <summary>
    ///     子弹立即威胁检测: 沿当前轨迹模拟, 命中则按命中时间返回分数, 否则 0.
    ///     公式: score = Score * (1 - hitTime / MaxPredictTime)
    /// </summary>
    public class IsImmediateAttack : Utility
    {
        private const float MaxSpeed = 10f;
        private const float Acceleration = 16f;
        private const float ContactRadius = 5.5f;
        private const float MaxPredictTime = 3f;
        private const float Score = 1145f;

        protected override float OnCalcU(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Vector2 tankPos = tank.Position.ToXZ();
            Vector2 rawVel = tank.Velocity.ToXZ();
            float v0 = rawVel.magnitude;

            Vector2 tankDir = v0 < 0.1f
                ? tank.Forward.ToXZ().normalized
                : rawVel.normalized;

            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissilesEx(tank.Team);
            float bestTime = BulletTrajectoryUtils.GetEarliestHitTime(
                tankPos, tankDir, v0, MaxSpeed, Acceleration,
                missiles, ContactRadius, MaxPredictTime);

            if (bestTime >= MaxPredictTime) return 0f;
            return Score * (1f - bestTime / MaxPredictTime);
        }
    }

    /// <summary>
    ///     子弹追踪器: 维护敌方子弹的排序列表, 支持按距离锁定最近威胁 (ID锁定) 和自定义销毁判定.
    ///     由 MyTank 持有, 供 AvoidAction 跨帧追踪同一颗子弹直到躲避完成.
    ///     内置两种销毁判定: 子弹已越过我方 (HasPassedPlayer) 和子弹正在远离 (IsMovingAway).
    /// </summary>
    public class BulletTracker
    {
        private readonly List<Func<Missile, Tank, bool>> _destroyPredicates = new List<Func<Missile, Tank, bool>>();

        private readonly List<TrackedBullet> _sortedBullets = new List<TrackedBullet>();
        private Dictionary<int, Missile> _missileMap;
        private Tank _owner;

        public BulletTracker()
        {
            AddDestroyPredicate(HasPassedPlayer);
            AddDestroyPredicate(IsMovingAway);
        }

        public int LockedBulletId { get; private set; } = -1;
        public IReadOnlyList<TrackedBullet> SortedBullets => _sortedBullets;
        public int BulletCount => _sortedBullets.Count;

        public void AddDestroyPredicate(Func<Missile, Tank, bool> predicate)
        {
            _destroyPredicates.Add(predicate);
        }

        public void Refresh(Tank owner, Dictionary<int, Missile> missileMap)
        {
            _owner = owner;
            _missileMap = missileMap;
            _sortedBullets.Clear();
            if (missileMap == null) return;

            Vector2 tankPos = owner.Position.ToXZ();

            foreach (KeyValuePair<int, Missile> pair in missileMap)
            {
                Missile m = pair.Value;
                if (m == null) continue;
                Vector2 mPos = m.Position.ToXZ();
                float dist = Vector2.Distance(tankPos, mPos);
                _sortedBullets.Add(new TrackedBullet
                {
                    MissileId = m.ID,
                    Distance = dist,
                    Position = mPos,
                    Velocity = m.Velocity.ToXZ()
                });
            }
            _sortedBullets.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        public bool LockOnNearestThreat()
        {
            LockedBulletId = -1;
            foreach (TrackedBullet tb in _sortedBullets)
            {
                Missile m = GetMissileById(tb.MissileId);
                if (m == null) continue;
                if (IsDestroyed(m)) continue;
                LockedBulletId = tb.MissileId;
                return true;
            }
            return false;
        }

        public bool IsLockedBulletDestroyed()
        {
            if (LockedBulletId < 0) return true;
            Missile m = GetMissileById(LockedBulletId);
            if (m == null) return true;
            return IsDestroyed(m);
        }

        public TrackedBullet? GetLockedBullet()
        {
            if (LockedBulletId < 0) return null;
            foreach (TrackedBullet tb in _sortedBullets)
            {
                if (tb.MissileId == LockedBulletId)
                    return tb;
            }
            return null;
        }

        public Missile GetLockedMissile()
        {
            if (LockedBulletId < 0) return null;
            return GetMissileById(LockedBulletId);
        }

        public void ReleaseLock()
        {
            LockedBulletId = -1;
        }

        public void Reset()
        {
            ReleaseLock();
            _sortedBullets.Clear();
            _missileMap = null;
            _owner = null;
        }

        private bool IsDestroyed(Missile m)
        {
            foreach (Func<Missile, Tank, bool> pred in _destroyPredicates)
            {
                if (pred(m, _owner))
                    return true;
            }
            return false;
        }

        private Missile GetMissileById(int id)
        {
            if (_missileMap != null && _missileMap.TryGetValue(id, out Missile m))
                return m;
            return null;
        }

        private static bool HasPassedPlayer(Missile m, Tank owner)
        {
            Tank enemy = EnemyHelper.GetAliveEnemy(owner);
            if (enemy == null) return false;
            float bulletToEnemy = Vector3.Distance(m.Position, enemy.Position);
            float ownerToEnemy = Vector3.Distance(owner.Position, enemy.Position);
            return bulletToEnemy > ownerToEnemy;
        }

        private static bool IsMovingAway(Missile m, Tank owner)
        {
            Vector2 bPos = m.Position.ToXZ();
            Vector2 bVel = m.Velocity.ToXZ();
            Vector2 toOwner = owner.Position.ToXZ() - bPos;
            return Vector2.Dot(bVel, toOwner) < 0f;
        }

        /// <summary>
        ///     追踪子弹条目: 存储单颗敌方子弹的瞬时状态,
        ///     用于 BulletTracker 按距离排序、锁定威胁和命中预判.
        /// </summary>
        public struct TrackedBullet
        {
            public int MissileId;
            public float Distance;
            public Vector2 Position;
            public Vector2 Velocity;
        }
    }
}
