using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Main;

namespace ZZC
{
    // ---------------------------------------------------------------
    // 1. 通用常量 & 帮助函数
    // ---------------------------------------------------------------
    internal static class Consts
    {
        // 地图相关
        public const float MapHalfSize = 50f;            // 地图边界 (±x/±z)
        public const float CornerThreshold = 5f;         // 转角判定阈值
        
        // 导弹相关
        public const float MissileDangerDist = 20f;      // 导弹威胁判定距离
        public const float MissileDangerDot = 0.85f;     // 前向夹角阈值（cos 值）
        public const float EvadeStep = 8f;               // 逃逸位移步长
        public const float MissilePredictTime = 0.5f;    // 导弹预测时间
        
        // 战斗相关
        public const float BaseAimAngle = 20f;           // 远距离基础开火角度阈值
        public const float CombatMinDistance = 15f;      // 战斗最小距离
        public const float CombatMaxDistance = 25f;      // 战斗最大距离
        public const float KillPriorityDistance = 40f;   // 击杀优先距离
        
        // 血量相关
        public const float LowHPThreshold = 0.3f;        // 低血量阈值（百分比）
        public const float RecoveryThreshold = 0.6f;     // 回血阈值（百分比）
        public const float AggressiveTimeThreshold = 0.15f; // 激进时间阈值（百分比）
        
        // 星星相关
        public const float StarCollectMargin = 5f;       // 顺路捡星判定距离
        public const float StarPredictTime = 2f;         // 星星预测时间

        // 导航相关
        public const float NavSampleRadius = 5f;         // 导航采样半径
        public const float NavSampleDistance = 10f;      // 导航采样距离
        public const float PathUpdateInterval = 0.2f;    // 路径更新间隔
        
        // Kalman Filter 相关
        public const float ProcessNoise = 0.1f;          // 过程噪声
        public const float MeasurementNoise = 0.5f;      // 测量噪声
        public const float InitialCovariance = 1f;       // 初始协方差
        
        // 导弹躲避相关
        public const float StrafingRadius = 8f;          // 侧身扫射半径
        public const float StrafingSpeed = 2f;           // 侧身扫射速度
        public const float EvadeMargin = 3f;             // 躲避安全距离
        public const int EvadeSampleCount = 8;           // 躲避采样点数量
    }

    internal static class Helpers
    {
        public static Vector3 ClampToMap(Vector3 v)
        {
            v.x = Mathf.Clamp(v.x, -Consts.MapHalfSize, Consts.MapHalfSize);
            v.z = Mathf.Clamp(v.z, -Consts.MapHalfSize, Consts.MapHalfSize);
            return v;
        }

        public static bool IsPositionInMap(Vector3 position)
        {
            return position.x >= -Consts.MapHalfSize && position.x <= Consts.MapHalfSize &&
                   position.z >= -Consts.MapHalfSize && position.z <= Consts.MapHalfSize;
        }

        public static Vector3 PredictPosition(Vector3 currentPos, Vector3 velocity, Vector3 acceleration, float time)
        {
            Vector3 predictedPos = currentPos + velocity * time + 0.5f * acceleration * time * time;
            return ClampToMap(predictedPos);
        }
    }

    // ---------------------------------------------------------------
    // 2. 数据结构
    // ---------------------------------------------------------------
    internal enum TankState { Patrol, Collect, Fight, Retreat, Evade, CollectSuperStar }

    internal struct UtilityAction
    {
        public string Name;   // 行为标签
        public float Score;   // 实用度得分
        public Vector3 Target;// 目标位置
        public UtilityAction(string name, float score, Vector3 target)
        { Name = name; Score = score; Target = target; }
    }

    // ---------------------------------------------------------------
    // 3. 功能模块
    // ---------------------------------------------------------------
    internal sealed class KalmanFilter
    {
        private Vector3 _position;
        private Vector3 _velocity;
        private Vector3 _acceleration;
        private float _covariance;
        private float _lastUpdateTime;

        public KalmanFilter()
        {
            _covariance = Consts.InitialCovariance;
            _lastUpdateTime = Time.time;
        }

        public void Update(Vector3 measurement)
        {
            float dt = Time.time - _lastUpdateTime;
            if (dt <= 0) return;

            // 预测步骤
            Vector3 predictedPosition = _position + _velocity * dt + 0.5f * _acceleration * dt * dt;
            float predictedCovariance = _covariance + Consts.ProcessNoise * dt;

            // 更新步骤
            float kalmanGain = predictedCovariance / (predictedCovariance + Consts.MeasurementNoise);
            _position = predictedPosition + kalmanGain * (measurement - predictedPosition);
            _covariance = (1 - kalmanGain) * predictedCovariance;

            // 更新速度和加速度
            Vector3 positionDelta = _position - predictedPosition;
            _velocity = positionDelta / dt;
            _acceleration = (_velocity - _acceleration) / dt;

            _lastUpdateTime = Time.time;
        }

        public Vector3 GetPredictedPosition(float timeAhead)
        {
            return _position + _velocity * timeAhead + 0.5f * _acceleration * timeAhead * timeAhead;
        }

        public Vector3 GetVelocity() => _velocity;
        public Vector3 GetAcceleration() => _acceleration;
    }

    internal sealed class NavigationSystem
    {
        private readonly Tank _owner;
        private NavMeshPath _currentPath;
        private float _lastPathUpdateTime;
        private Vector3 _currentTarget;
        private int _currentPathIndex;

        public NavigationSystem(Tank owner)
        {
            _owner = owner;
            _currentPath = new NavMeshPath();
        }

        public Vector3 GetNextPosition(Vector3 target)
        {
            if (Time.time - _lastPathUpdateTime >= Consts.PathUpdateInterval || target != _currentTarget)
            {
                UpdatePath(target);
            }

            if (_currentPath.corners.Length == 0) return target;

            // 获取下一个路径点
            Vector3 nextPoint = _currentPath.corners[_currentPathIndex];
            float distanceToNext = Vector3.Distance(_owner.Position, nextPoint);

            // 如果接近当前路径点，移动到下一个
            if (distanceToNext < Consts.NavSampleRadius)
            {
                _currentPathIndex = Mathf.Min(_currentPathIndex + 1, _currentPath.corners.Length - 1);
                nextPoint = _currentPath.corners[_currentPathIndex];
            }

            return nextPoint;
        }

        private void UpdatePath(Vector3 target)
        {
            _currentTarget = target;
            NavMesh.CalculatePath(_owner.Position, target, NavMesh.AllAreas, _currentPath);
            _currentPathIndex = 0;
            _lastPathUpdateTime = Time.time;
        }

        public bool IsPathValid() => _currentPath.status == NavMeshPathStatus.PathComplete;
    }

    internal sealed class ThreatEvaluator
    {
        private readonly Tank _owner;
        private readonly Dictionary<int, Missile> _directMissiles = new();
        private Vector3 _escapePosition;
        private float _lastEvadeTime;

        public ThreatEvaluator(Tank owner)
        {
            _owner = owner;
        }

        public bool Evaluate(out Vector3 escape)
        {
            escape = Vector3.zero;
            _directMissiles.Clear();

            // 检查是否有超级星星在附近
            bool hasNearbySuperStar = false;
            float superStarDist = float.MaxValue;
            foreach (var kv in Match.instance.GetStars())
            {
                if (kv.Value.IsSuperStar)
                {
                    float dist = Vector3.Distance(_owner.Position, kv.Value.Position);
                    if (dist < 15f) // 超级星星在15单位范围内
                    {
                        hasNearbySuperStar = true;
                        superStarDist = dist;
                        break;
                    }
                }
            }

            var missiles = Match.instance.GetOppositeMissiles(_owner.Team);
            if (missiles == null || missiles.Count == 0) return false;

            // 检查所有导弹
            foreach (var kv in missiles)
            {
                Missile missile = kv.Value;
                if (missile == null) continue;
                if (Physics.SphereCast(missile.Position, 0.1f, missile.Velocity, out RaycastHit hit, 40))
                {
                    FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                    if (fireCollider != null && fireCollider.Owner == _owner)
                    {
                        _directMissiles[kv.Key] = missile;
                    }
                }
            }

            if (_directMissiles.Count > 0)
            {
                // 如果有超级星星在附近，且距离很近，则优先吃星星
                if (hasNearbySuperStar && superStarDist < 8f)
                {
                    return false;
                }

                // 如果有超级星星在附近，且导弹威胁不是特别紧急，也优先吃星星
                if (hasNearbySuperStar)
                {
                    bool isUrgentThreat = false;
                    foreach (var pair in _directMissiles)
                    {
                        Missile missile = pair.Value;
                        float distToMissile = Vector3.Distance(_owner.Position, missile.Position);
                        float timeToImpact = distToMissile / missile.Velocity.magnitude;
                        if (timeToImpact < 0.5f) // 导弹在0.5秒内会击中
                        {
                            isUrgentThreat = true;
                            break;
                        }
                    }
                    if (!isUrgentThreat)
                    {
                        return false;
                    }
                }

                foreach (var pair in _directMissiles)
                {
                    Missile missile = pair.Value;
                    Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                    Vector3 onWhichSideInfo = Vector3.Cross(missile.Velocity, _owner.Position - missile.Position);
                    if (onWhichSideInfo.y > 0) cross *= -1;
                    escape = _owner.Position + cross * 4.2f;
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class UtilityEvaluator
    {
        private readonly Tank _owner;
        private readonly float _maxHP;
        private readonly float _lowHPThreshold;
        private readonly float _recoveryThreshold;
        private readonly float _aggressiveTimeThreshold;
        private Dictionary<int, Vector3> _starPositions = new Dictionary<int, Vector3>();

        public UtilityEvaluator(Tank owner)
        {
            _owner = owner;
            _maxHP = Match.instance.GlobalSetting.MaxHP;
            _lowHPThreshold = _maxHP * Consts.LowHPThreshold;
            _recoveryThreshold = _maxHP * Consts.RecoveryThreshold;
            _aggressiveTimeThreshold = Match.instance.GlobalSetting.MatchTime * Consts.AggressiveTimeThreshold;
        }

        public void UpdateStarInfo()
        {
            _starPositions.Clear();
            foreach (var pair in Match.instance.GetStars())
            {
                _starPositions[pair.Key] = pair.Value.Position;
            }
        }

        public UtilityAction Evaluate()
        {
            Tank enemy = Match.instance.GetOppositeTank(_owner.Team);
            Vector3 home = Match.instance.GetRebornPos(_owner.Team);
            float hpRatio = (float)_owner.HP / _maxHP;
            float remainingTime = Match.instance.RemainingTime;
            float totalTime = Match.instance.GlobalSetting.MatchTime;
            bool isSecondHalf = remainingTime < totalTime * 0.5f;

            // --- Collect Super Star ---
            foreach (var kv in Match.instance.GetStars())
            {
                if (kv.Value.IsSuperStar)
                {
                    float distToSuperStar = Vector3.Distance(_owner.Position, kv.Value.Position);
                    float score = 1000f;
                    
                    // 根据距离增加优先级
                    if (distToSuperStar < 15f)
                    {
                        score *= 2f; // 近距离时加倍优先级
                    }
                    
                    // 如果在下半场，进一步增加优先级
                    if (isSecondHalf)
                    {
                        score *= 1.5f;
                    }
                    
                    return new UtilityAction("CollectSuperStar", score, kv.Value.Position);
                }
            }

            // --- Collect Star ---
            if (_starPositions.Count > 0)
            {
                float bestScore = float.MinValue;
                Vector3 bestPos = Vector3.zero;
                foreach (var pair in _starPositions)
                {
                    float myDist = Vector3.Distance(_owner.Position, pair.Value);
                    float enemyDist = enemy != null && !enemy.IsDead ? Vector3.Distance(enemy.Position, pair.Value) : 999f;
                    float score = enemyDist - myDist;
                    if (enemyDist < myDist) score -= 20f;
                    
                    // 如果在下半场，增加地图中央星星的权重
                    if (isSecondHalf)
                    {
                        float distToCenter = Vector3.Distance(pair.Value, Vector3.zero);
                        if (distToCenter < 20f) // 地图中央区域
                        {
                            score *= 1.3f; // 增加30%的权重
                        }
                    }
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = pair.Value;
                    }
                }

                // 路径经过基地且血量危险时顺路回血
                if (_owner.HP < _lowHPThreshold)
                {
                    float distToHome = Vector3.Distance(_owner.Position, home);
                    float distStarToHome = Vector3.Distance(bestPos, home);
                    float distToStar = Vector3.Distance(_owner.Position, bestPos);
                    if (distToHome + distStarToHome - distToStar < 5f)
                    {
                        return new UtilityAction("Retreat", 100f, home);
                    }
                }

                // 如果敌人死亡，增加吃星星的优先级
                if (enemy == null || enemy.IsDead)
                {
                    bestScore *= 1.5f;
                }

                return new UtilityAction("Collect", bestScore, bestPos);
            }

            // --- Fight ---
            if (enemy != null && !enemy.IsDead)
            {
                float dist = Vector3.Distance(_owner.Position, enemy.Position);
                float risk = (hpRatio < Consts.LowHPThreshold) ? 2f : 1f;
                float timeBonus = (remainingTime < _aggressiveTimeThreshold) ? 1.5f : 1f;
                float fightScore = Match.instance.GlobalSetting.ScoreForKill / (dist + 1f) / risk * timeBonus;
                return new UtilityAction("Fight", fightScore, enemy.Position);
            }

            // --- Retreat ---
            if (hpRatio < Consts.LowHPThreshold)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    float dist = Vector3.Distance(_owner.Position, enemy.Position);
                    if (dist < 25f)
                    {
                        return new UtilityAction("Retreat", 100f, home);
                    }
                }
            }

            // --- Patrol ---
            // 如果敌人死亡，扩大巡逻范围
            if (enemy == null || enemy.IsDead)
            {
                Vector3 patrolPos;
                if (isSecondHalf)
                {
                    // 在下半场，有70%的概率在地图中央巡逻
                    if (UnityEngine.Random.value < 0.7f)
                    {
                        patrolPos = new Vector3(
                            Mathf.Sin(Time.time * 0.3f) * 15f,  // 在地图中央区域巡逻
                            0,
                            Mathf.Cos(Time.time * 0.3f) * 15f
                        );
                    }
                    else
                    {
                        patrolPos = home + new Vector3(
                            Mathf.Sin(Time.time * 0.5f) * 20f,
                            0,
                            Mathf.Cos(Time.time * 0.5f) * 20f
                        );
                    }
                }
                else
                {
                    patrolPos = home + new Vector3(
                        Mathf.Sin(Time.time * 0.5f) * 20f,
                        0,
                        Mathf.Cos(Time.time * 0.5f) * 20f
                    );
                }
                return new UtilityAction("Patrol", 0.2f, patrolPos);
            }

            return new UtilityAction("Patrol", 0.1f, home + Vector3.right * 10f);
        }
    }

    internal sealed class TurretController
    {
        private readonly Tank _owner;
        private readonly float _missileSpeed;
        private float _lastFireTime = -999f;
        private Vector3 _lastEnemyPosition;
        private Vector3 _lastEnemyVelocity;
        private Vector3 _lastEnemyAcceleration;
        private float _lastEnemyUpdateTime;
        private const float ENEMY_UPDATE_INTERVAL = 0.02f;

        public TurretController(Tank owner, float missileSpeed)
        {
            _owner = owner;
            _missileSpeed = missileSpeed;
        }

        public void Update()
        {
            Tank enemy = Match.instance.GetOppositeTank(_owner.Team);
            if (enemy == null || enemy.IsDead)
            {
                _owner.TurretTurnTo(_owner.Position + _owner.Forward);
                return;
            }

            // 更新敌人信息
            if (Time.time - _lastEnemyUpdateTime >= ENEMY_UPDATE_INTERVAL)
            {
                Vector3 currentPosition = enemy.Position;
                Vector3 currentVelocity = enemy.Velocity;
                
                if (_lastEnemyPosition != Vector3.zero)
                {
                    Vector3 velocityDelta = currentVelocity - _lastEnemyVelocity;
                    _lastEnemyAcceleration = velocityDelta / ENEMY_UPDATE_INTERVAL;
                }
                
                _lastEnemyPosition = currentPosition;
                _lastEnemyVelocity = currentVelocity;
                _lastEnemyUpdateTime = Time.time;
            }

            // 预测目标位置
            Vector3 aim = PredictLead(enemy);
            _owner.TurretTurnTo(aim);
            TryFire(enemy, aim);
        }

        private Vector3 PredictLead(Tank target)
        {
            Vector3 delta = target.Position - _owner.FirePos;
            Vector3 vel = target.Velocity;
            float a = Vector3.Dot(vel, vel) - _missileSpeed * _missileSpeed;
            float b = 2 * Vector3.Dot(delta, vel);
            float c = Vector3.Dot(delta, delta);
            float t = 0f;
            float det = b * b - 4 * a * c;
            if (det > 0.01f && Math.Abs(a) > 1e-3f)
            {
                t = (-b - Mathf.Sqrt(det)) / (2 * a);
            }
            t = Mathf.Max(0, t);

            // 考虑加速度的预测
            Vector3 predictedPos = target.Position + vel * t + 0.5f * _lastEnemyAcceleration * t * t;
            return Helpers.ClampToMap(predictedPos);
        }

        private void TryFire(Tank enemy, Vector3 aimPos)
        {
            if (!_owner.CanFire() || Time.time - _lastFireTime < Match.instance.GlobalSetting.FireInterval)
                return;

            float angle = Vector3.Angle(_owner.TurretAiming, aimPos - _owner.Position);
            float dist = Vector3.Distance(_owner.Position, enemy.Position);
            float threshold = Mathf.Lerp(Consts.BaseAimAngle, 5f, Mathf.InverseLerp(30f, 5f, dist));

            if (angle < threshold && !Physics.Linecast(_owner.FirePos, aimPos, PhysicsUtils.LayerMaskScene))
            {
                _owner.Fire();
                _lastFireTime = Time.time;
            }
        }
    }

    // ---------------------------------------------------------------
    // 4. ZZCTank 主类
    // ---------------------------------------------------------------
    public class MyTank : Tank
    {
        private ThreatEvaluator _threat;
        private UtilityEvaluator _utility;
        private TurretController _turret;
        private NavigationSystem _nav;
        private TankState _state = TankState.Patrol;
        private Vector3 _target;
        private float _lastStateUpdateTime;
        private const float STATE_UPDATE_INTERVAL = 0.02f;
        private Tank _enemyTank;
        private Vector3 _lastEnemyPosition;
        private Vector3 _lastEnemyVelocity;
        private Vector3 _lastEnemyAcceleration;
        private float _lastEnemyUpdateTime;
        private const float ENEMY_UPDATE_INTERVAL = 0.02f;
        private float _lastInfoUpdateTime = 0f;
        private const float INFO_UPDATE_INTERVAL = 0.02f;

        protected override void OnStart()
        {
            base.OnStart();
            _threat = new ThreatEvaluator(this);
            _utility = new UtilityEvaluator(this);
            _turret = new TurretController(this, Match.instance.GlobalSetting.MissileSpeed);
            _nav = new NavigationSystem(this);
            _enemyTank = Match.instance.GetOppositeTank(Team);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            
            // 更新所有信息
            if (Time.time - _lastInfoUpdateTime >= INFO_UPDATE_INTERVAL)
            {
                UpdateAllInfo();
                _lastInfoUpdateTime = Time.time;
            }
            
            // 更新炮塔控制
            _turret.Update();

            // 更新状态
            if (Time.time - _lastStateUpdateTime >= STATE_UPDATE_INTERVAL)
            {
                UpdateState();
                _lastStateUpdateTime = Time.time;
            }

            Execute();
        }

        private void UpdateAllInfo()
        {
            UpdateEnemyInfo();
            _utility.UpdateStarInfo();
        }

        private void UpdateEnemyInfo()
        {
            if (Time.time - _lastEnemyUpdateTime >= ENEMY_UPDATE_INTERVAL)
            {
                _enemyTank = Match.instance.GetOppositeTank(Team);
                if (_enemyTank != null && !_enemyTank.IsDead)
                {
                    Vector3 currentPosition = _enemyTank.Position;
                    Vector3 currentVelocity = _enemyTank.Velocity;
                    
                    if (_lastEnemyPosition != Vector3.zero)
                    {
                        Vector3 velocityDelta = currentVelocity - _lastEnemyVelocity;
                        _lastEnemyAcceleration = velocityDelta / ENEMY_UPDATE_INTERVAL;
                    }
                    
                    _lastEnemyPosition = currentPosition;
                    _lastEnemyVelocity = currentVelocity;
                }
                _lastEnemyUpdateTime = Time.time;
            }
        }

        private void UpdateState()
        {
            // 优先处理导弹威胁
            if (_threat.Evaluate(out Vector3 evadePos))
            {
                _state = TankState.Evade;
                _target = evadePos;
                return;
            }

            // 评估其他行为
            UtilityAction act = _utility.Evaluate();
            _state = act.Name switch
            {
                "CollectSuperStar" => TankState.CollectSuperStar,
                "Collect" => TankState.Collect,
                "Fight" => TankState.Fight,
                "Retreat" => TankState.Retreat,
                _ => TankState.Patrol
            };
            _target = act.Target;

            // 如果敌人死亡，确保不会停留在原地
            if (_enemyTank != null && _enemyTank.IsDead)
            {
                _lastStateUpdateTime = Time.time - STATE_UPDATE_INTERVAL * 0.5f; // 提前更新状态
            }
        }

        private void Execute()
        {
            switch (_state)
            {
                case TankState.Evade:
                case TankState.Collect:
                case TankState.Fight:
                case TankState.Retreat:
                case TankState.CollectSuperStar:
                    Move(_target);
                    break;
                case TankState.Patrol:
                    PatrolAroundHome();
                    break;
            }
        }

        private void PatrolAroundHome()
        {
            Vector3 home = Match.instance.GetRebornPos(Team);
            Tank enemy = Match.instance.GetOppositeTank(Team);
            float remainingTime = Match.instance.RemainingTime;
            float totalTime = Match.instance.GlobalSetting.MatchTime;
            bool isSecondHalf = remainingTime < totalTime * 0.5f;
            
            // 如果敌人死亡，扩大巡逻范围并增加移动速度
            float patrolRadius = (enemy == null || enemy.IsDead) ? 20f : 10f;
            float patrolSpeed = (enemy == null || enemy.IsDead) ? 1.5f : 1f;  // 增加移动速度
            
            Vector3 patrol;
            if (isSecondHalf && (enemy == null || enemy.IsDead))
            {
                // 在下半场，有70%的概率在地图中央巡逻
                if (UnityEngine.Random.value < 0.7f)
                {
                    patrol = new Vector3(
                        Mathf.Sin(Time.time * patrolSpeed) * 15f,  // 增加移动速度
                        0,
                        Mathf.Cos(Time.time * patrolSpeed) * 15f
                    );
                }
                else
                {
                    patrol = home + new Vector3(
                        Mathf.Sin(Time.time * patrolSpeed) * patrolRadius,
                        0,
                        Mathf.Cos(Time.time * patrolSpeed) * patrolRadius
                    );
                }
            }
            else
            {
                patrol = home + new Vector3(
                    Mathf.Sin(Time.time * patrolSpeed) * patrolRadius,
                    0,
                    Mathf.Cos(Time.time * patrolSpeed) * patrolRadius
                );
            }
            Move(patrol);
        }

        private bool WillHitPredictionMissile()
        {
            var missiles = Match.instance.GetOppositeMissiles(Team);
            foreach (var pair in missiles)
            {
                Missile missile = pair.Value;
                if (missile == null) continue;
                Collider[] colliders = Physics.OverlapSphere(missile.Position, 5f);
                foreach (var collider in colliders)
                {
                    if (collider != null && collider.gameObject == this.gameObject)
                    {
                        // 如果预测到会被击中，立即向侧面移动
                        Vector3 escapeDir = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                        Vector3 escapePos = Position + escapeDir * 4.2f;
                        Move(escapePos);
                        return true;
                    }
                }
            }
            return false;
        }

        public override string GetName() => "ZZC_Tank";
    }
}
