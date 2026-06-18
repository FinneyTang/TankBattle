using System.Collections.Generic;
using Main;
using UnityEngine;

namespace MXF
{
    /// <summary>
    ///     AI攻击模块: 负责坦克的炮塔瞄准和开火决策
    ///     弹道预判 — 通过二次方程求解拦截目标的预判点
    ///     贝叶斯校正 — 收集历史射击偏差样本, 用 μ/σ 修正预判沿敌人移动方向的偏移
    ///     开火控制 — 炮管对准误差&lt;3m 且弹道路径无遮挡才发射
    /// </summary>
    public class AIAttack
    {
        private const int MaxSamples = 20;

        private readonly List<float> _deltaDist = new List<float>();
        private readonly Queue<PendingShot> _pending = new Queue<PendingShot>();
        private AttackData _data;
        private Tank _tank;

        // 贝叶斯估计 (距离偏差, 单位: 米)
        public float Mu { get; private set; }
        public float Sigma { get; private set; }

        // ---- Debug 可视化数据 ----
        public Vector3 DebugFirePos { get; private set; }
        public Vector3 DebugLinearAim { get; private set; }
        public Vector3 DebugCorrectedAim { get; private set; }
        public Vector3 DebugCorrectionVec { get; private set; }
        public bool DebugHasData { get; private set; }

        // 生命周期更新

        public void Update(AttackData data, Tank tank)
        {
            _data = data;
            _tank = tank;
            if (_data == null || _tank == null) return;

            CheckPredictions();
            TryFire();
        }


        /// <summary>
        ///     尝试开火: 计算线性预判点 → 应用贝叶斯校正 → 检查炮管误差和弹道遮挡 → 发射并记录预测
        /// </summary>
        private void TryFire()
        {
            Vector3 firePos = _data.playerPos;
            Vector3 targetPos = new Vector3(_data.target.pos.x, firePos.y, _data.target.pos.y);
            Vector3 targetSpeed = new Vector3(_data.target.dir.x, 0f, _data.target.dir.y)
                                * _data.target.speed;
            float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;

            Vector3 linearAim = PredictedFireForward(firePos, targetPos, targetSpeed, missileSpeed,
                out float predictedTime);
            Vector3 enemyMoveDir = targetSpeed.normalized;
            Vector3 aimPos = ApplyCorrection(firePos, linearAim, enemyMoveDir);
            Vector3 correctionVec = aimPos - linearAim;

            // 修正后瞄准点不能落在敌人后方 (沿移动方向的投影为负)
            if (Vector3.Dot(aimPos - targetPos, enemyMoveDir) < 0f)
                aimPos = targetPos;

            // ---- Debug 可视化 ----
            if (Debugger.EnableAttackDebug)
            {
                DebugFirePos = firePos;
                DebugLinearAim = linearAim;
                DebugCorrectedAim = aimPos;
                DebugCorrectionVec = correctionVec;
                DebugHasData = true;
                Debugger.DrawAttackRuntime(firePos, linearAim, aimPos, targetPos, _tank.TurretAiming);
            }

            _tank.TurretTurnTo(aimPos);

            // 炮管指向与瞄准点的误差 <3m 才发射
            float aimDist = Vector3.Distance(firePos, aimPos);
            Vector3 turretAimPoint = firePos + _tank.TurretAiming.normalized * aimDist;
            float aimError = Vector3.Distance(turretAimPoint, aimPos);
            if (aimError > 3f) return;

            // 弹道路径被墙阻挡 (SphereCast 半径1m = 导弹半径) → 不发射
            Vector3 aimDir = (aimPos - firePos).normalized;
            if (Physics.SphereCast(firePos, 1f, aimDir, out RaycastHit _, aimDist,
                    PhysicsUtils.LayerMaskScene)) return;

            if (_tank.Fire())
            {
                RecordPrediction(firePos, aimPos, enemyMoveDir, predictedTime);
            }
        }

        /// <summary> 开火后记录预测, 用于后续距离偏差计算. </summary>
        private void RecordPrediction(
            Vector3 firePos, Vector3 aimPos,
            Vector3 enemyMoveDir, float predictedTime)
        {
            _pending.Enqueue(new PendingShot
            {
                firePos = firePos,
                predictedAim = aimPos,
                enemyMoveDir = enemyMoveDir,
                matureTime = Time.time + predictedTime
            });
        }

        /// <summary> 检查到期的预测, 计算沿移动方向的距离偏差 Δd 并更新贝叶斯估计. </summary>
        private void CheckPredictions()
        {
            Vector3 enemyPos = new Vector3(_data.target.pos.x, 0f, _data.target.pos.y);

            while (_pending.Count > 0 && _pending.Peek().matureTime <= Time.time)
            {
                PendingShot shot = _pending.Dequeue();

                // Δd = dot(实际位置 - 预测位置, 发射时敌人移动方向)
                // 正值 = 敌人比预测更远 (加速), 负值 = 敌人比预测更近 (减速/转向)
                float deltaDist = Vector3.Dot(enemyPos - shot.predictedAim, shot.enemyMoveDir);

                _deltaDist.Add(deltaDist);
                if (_deltaDist.Count > MaxSamples)
                    _deltaDist.RemoveAt(0);
            }

            if (_pending.Count > MaxSamples * 2)
            {
                while (_pending.Count > MaxSamples)
                    _pending.Dequeue();
            }

            UpdateBayesian();
        }

        /// <summary>
        ///     更新贝叶斯估计: 计算 Δd 样本的均值 μ 和标准差 σ.
        /// </summary>
        private void UpdateBayesian()
        {
            int n = _deltaDist.Count;
            if (n == 0)
            {
                Mu = -3f;
                Sigma = 0f;
                return;
            }

            float sum = 0f;
            foreach (float d in _deltaDist)
            {
                sum += d;
            }
            Mu = sum / n;

            float sumSq = 0f;
            foreach (float d in _deltaDist)
            {
                sumSq += (d - Mu) * (d - Mu);
            }
            Sigma = n > 1 ? Mathf.Sqrt(sumSq / (n - 1)) : 0f;
        }

        /// <summary> 沿敌人移动方向偏移线性预测 μ 米. </summary>
        private Vector3 ApplyCorrection(Vector3 firePos, Vector3 linearAim, Vector3 enemyMoveDir)
        {
            if (_deltaDist.Count < 3) return linearAim;
            return linearAim + enemyMoveDir * Mu;
        }

        // 二次方程弹道预判
        // |d + v_target*t| = v_missile*t → a*t² + b*t + c = 0

        public static Vector3 PredictedFireForward(
            Vector3 firePos, Vector3 targetPos, Vector3 targetSpeed, float missileSpeed,
            out float predictedTime)
        {
            Vector3 d = targetPos - firePos;
            float vp = missileSpeed;
            float v0 = targetSpeed.magnitude;
            float fallbackTime = d.magnitude / Mathf.Max(vp, 1f);

            if (v0 < 0.001f)
            {
                predictedTime = fallbackTime;
                return targetPos;
            }

            float cosTheta = Vector3.Dot(-d.normalized, targetSpeed.normalized);
            float a = v0 * v0 - vp * vp;
            float b = -2f * v0 * d.magnitude * cosTheta;
            float c = d.sqrMagnitude;
            float delta = b * b - 4f * a * c;

            if (delta < 0f || Mathf.Abs(a) < 0.001f)
            {
                predictedTime = fallbackTime;
                return targetPos;
            }

            float t = (-b - Mathf.Sqrt(delta)) / (2f * a);
            if (t < 0f) t = (-b + Mathf.Sqrt(delta)) / (2f * a);
            if (t < 0f)
            {
                predictedTime = fallbackTime;
                return targetPos;
            }

            predictedTime = t;
            return firePos + d + targetSpeed * t;
        }

        /// <summary>
        ///     待验证射击记录: 记录开火时的预测瞄准点、敌人移动方向和预期到达时间,
        ///     到期后与实际位置对比计算 Δd, 用于更新贝叶斯估计的 μ/σ.
        /// </summary>
        private struct PendingShot
        {
            public Vector3 firePos;
            public Vector3 predictedAim;
            public Vector3 enemyMoveDir;
            public float matureTime;
        }
    }

    /// <summary>
    ///     攻击数据容器: 持有当前帧用于攻击计算的玩家开火位置和目标的瞬时运动状态.
    ///     每次 UpdateAttack 时由 MyTank 填充最新数据后传给 AIAttack.
    /// </summary>
    public class AttackData
    {
        public Vector3 playerPos;
        public MoveObject target;
    }
}
