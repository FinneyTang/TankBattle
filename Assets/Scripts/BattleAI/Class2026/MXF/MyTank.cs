using System.Collections.Generic;
using System.Text;
using Main;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
#endif

namespace MXF
{
    /// <summary>
    ///
    ///     MXF.MyTank
    /// 
    ///     效用驱动的坦克AI主控: 继承自 Tank, 每帧按以下流水线运行:
    ///     1. UpdateAttack — 选择最近敌人进行炮塔瞄准和开火
    ///     2. BuildCandidates — 构建所有候选策略 (星星采集 + 6种固定策略)
    ///     3. 评分 — 每个策略调用 GetUtility() 求出效用分数
    ///     4. 迟滞 — 当前执行中的策略获得迟滞加分, 防止策略振荡
    ///     5. 选择最高分策略 — 切换时调用 TakeAction 初始化行为
    ///     6. MaintainMovement — 方向性策略和追踪策略每帧持续移动
    /// </summary>
    public class MyTank : Tank
    {

        // ==================== 候选构建 ====================

        private readonly Dictionary<int, GetOneStar> _starActions = new Dictionary<int, GetOneStar>();
        /// <summary> 子弹追踪器: 为 AvoidAction 提供跨帧持久的子弹锁定 </summary>
        public readonly BulletTracker BulletTracker = new BulletTracker();

        private UtilityActions _previousAction;
        /// <summary> 当前导航目的地: 定点策略设置, 供躲避方向偏好使用 </summary>
        public Vector3? NavDestination;
        internal string DebugScoresText { get; private set; } = "";
        internal AIAttack AttackForDebug { get; private set; }
        public UtilityActions CurrentAction { get; private set; }

        // ==================== 生命周期 ====================

        protected override void OnStart()
        {
            AttackForDebug = new AIAttack();
        }

        protected override void OnUpdate()
        {
            // 1. AIAttack 始终运行 (炮塔瞄准+开火)
            UpdateAttack();

            // 2. 构建候选策略
            List<UtilityActions> candidates = BuildCandidates();
            if (candidates.Count == 0) return;

            // 3. 评分所有候选 (先设 owner, CalcU 需要 Tank 引用)
            Dictionary<UtilityActions, float> scores = GetCandidateScores(candidates);

            // 4. 当前策略迟滞加分
            ApplyHysteresis(candidates, scores);
            UpdateDebugScoresText(scores);

            // 5. 选最高分策略
            UtilityActions best = GetBestAction(scores);
            if (best == null) return;

            // 6. 策略切换时执行 TakeAction (同类型方向性策略优先继承缓存方向)
            if (best != CurrentAction)
            {
                _previousAction = CurrentAction;
                CurrentAction = best;
                best.SetOwner(this);

                if (best is DirectionalAction da && _previousAction is DirectionalAction prevDa
                                                 && da.GetType() == prevDa.GetType() && prevDa.CachedDirection.HasValue)
                {
                    if (!da.InheritCachedDirection(prevDa))
                        da.TakeAction();
                }
                else
                {
                    best.TakeAction();
                }

            }

            // 7. 方向性策略持续移动 + ApproachEnemy 每帧追踪敌人
            if (CurrentAction is DirectionalAction activeDa && activeDa.CachedDirection.HasValue)
                activeDa.MaintainMovement(this);
            if (CurrentAction is ApproachEnemy appr)
                appr.MaintainMovement(this);
        }
        private static UtilityActions GetBestAction(Dictionary<UtilityActions, float> scores)
        {
            UtilityActions best = null;
            float bestScore = float.MinValue;
            foreach (KeyValuePair<UtilityActions, float> kv in scores)
            {
                if (kv.Value > bestScore)
                {
                    bestScore = kv.Value;
                    best = kv.Key;
                }
            }
            return best;
        }
        private Dictionary<UtilityActions, float> GetCandidateScores(List<UtilityActions> candidates)
        {
            Dictionary<UtilityActions, float> scores = new Dictionary<UtilityActions, float>();
            foreach (UtilityActions c in candidates)
            {
                c.SetOwner(this);
                scores[c] = c.GetUtility();
            }
            return scores;
        }

        protected override void OnReborn()
        {
            CurrentAction = null;
            _previousAction = null;
            _starActions.Clear();
            BulletTracker.Reset();
            NavDestination = null;
        }

        public void SetNavDestination(Vector3 pos)
        {
            NavDestination = pos;
        }
        public void ClearNavDestination()
        {
            NavDestination = null;
        }

        public override string GetName()
        {
            return "MXF";
        }

        // ==================== 攻击更新 ====================

        private void UpdateAttack()
        {
            List<Tank> oppTanks = Match.instance.GetOppositeTanks(Team);
            if (oppTanks == null) return;

            Tank nearest = null;
            float minDist = float.MaxValue;
            foreach (Tank t in oppTanks)
            {
                if (t == null || t.IsDead) continue;
                float d = (t.Position - Position).sqrMagnitude;
                if (d < minDist)
                {
                    minDist = d;
                    nearest = t;
                }
            }

            if (nearest == null) return;

            Vector3 vel = nearest.Velocity;
            AttackData data = new AttackData
            {
                playerPos = FirePos,
                target = new MoveObject
                {
                    pos = new Vector2(nearest.Position.x, nearest.Position.z),
                    dir = new Vector2(vel.x, vel.z).normalized,
                    speed = vel.magnitude
                }
            };
            AttackForDebug.Update(data, this);
        }

        /// <summary> 同步星星策略: 新星→创建, 消失→移除. </summary>
        private void SyncStarActions()
        {
            Dictionary<int, Star> stars = Match.instance.GetStars();
            HashSet<int> alive = new HashSet<int>();

            if (stars != null)
            {
                foreach (KeyValuePair<int, Star> pair in stars)
                {
                    if (pair.Value == null) continue;
                    int id = pair.Value.ID;
                    alive.Add(id);

                    if (!_starActions.ContainsKey(id))
                        _starActions[id] = new GetOneStar(pair.Value);
                }
            }

            // 移除已消失的星星策略
            List<int> dead = new List<int>();
            foreach (int id in _starActions.Keys)
            {
                if (!alive.Contains(id)) dead.Add(id);
            }
            foreach (int id in dead)
            {
                _starActions.Remove(id);
            }
        }

        private List<UtilityActions> BuildCandidates()
        {
            SyncStarActions();

            List<UtilityActions> list = new List<UtilityActions>();
            foreach (GetOneStar sa in _starActions.Values)
            {
                list.Add(sa);
            }

            // 固定策略
            list.Add(new ApproachEnemy());
            list.Add(new FinishEnemy());
            list.Add(new RetreatFromEnemy());
            list.Add(new ControlCenter());
            list.Add(new BackHome());
            list.Add(new AvoidAction());

            return list;
        }

        // ==================== 迟滞 ====================

        /// <summary>
        ///     当前策略加分 (SelfUtilityWhenAction > 0 → 加到自己;
        ///     =0 → 加到上一个策略). 防止策略振荡.
        /// </summary>
        private void ApplyHysteresis(
            List<UtilityActions> candidates,
            Dictionary<UtilityActions, float> scores)
        {
            if (CurrentAction == null) return;

            float bonus = 0;
            UtilityActions target = null;

            if (CurrentAction.SelfUtilityWhenAction > 0)
            {
                bonus = CurrentAction.SelfUtilityWhenAction;
                target = CurrentAction;
            }
            else if (_previousAction != null)
            {
                bonus = _previousAction.SelfUtilityWhenAction;
                target = _previousAction;
            }

            if (bonus <= 0 || target == null) return;

            foreach (UtilityActions c in candidates)
            {
                if (SameKind(c, target))
                {
                    scores[c] += bonus;
                    break;
                }
            }
        }

        private static bool SameKind(UtilityActions a, UtilityActions b)
        {
            return a.GetType() == b.GetType();
        }

        // ==================== 调试输出 ====================

        /// <summary>
        ///     构建策略评分文本并存储, 后续由 OnOnDrawGizmos 在场景中绘制.
        ///     关闭: EnableDebugScores = false.
        /// </summary>
        private void UpdateDebugScoresText(Dictionary<UtilityActions, float> scores)
        {
            DebugScoresText = "";
            if (!Debugger.EnableScoreDebug) return;

            List<(string name, float score)> sorted = new List<(string name, float score)>();
            foreach (KeyValuePair<UtilityActions, float> kv in scores)
            {
                sorted.Add((Debugger.GetDebugActionName(kv.Key), kv.Value));
            }
            sorted.Sort((a, b) => b.score.CompareTo(a.score));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=#00CCFF>═══ 策略评分 ═══</color>");

            for (int i = 0; i < sorted.Count; i++)
            {
                (string name, float score) = sorted[i];
                string color = score >= 100f ? "#00FF00" :
                    score >= 20f ? "#FFFF00" :
                    score >= 0f ? "#AAAAAA" :
                    "#FF4444";
                string rank = i == 0 ? "> " : "  ";
                sb.AppendLine($"<color={color}>{rank}{name}: {score:F1}</color>");
            }

            DebugScoresText = sb.ToString();
        }

#if UNITY_EDITOR
        protected override void OnOnDrawGizmos()
        {
            Debugger.DrawMyTankGizmos(this);
        }
#endif
    }

    /// <summary>
    ///     NavMesh 工具集:
    ///     1. CalcPathDist — 通过 NavMesh.CalculatePath 计算坦克到目标的可达路径总长度
    ///     2. SimulateMovementStep — 单步速度逼近模拟 (MoveTowards 模型), 用于弹道命中预测
    /// </summary>
    static class NavMeshUtils
    {
        public static float CalcPathDist(Tank tank, Vector3 targetPos)
        {
            NavMeshPath path = tank.CaculatePath(targetPos);
            if (path == null || path.corners.Length < 2)
                return Vector3.Distance(tank.Position, targetPos); // fallback

            float dist = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                dist += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            return dist;
        }

        /// <summary>
        ///     单步模拟: 将 vel 以 acceleration 的加速度向 desiredVel 逼近 (MoveTowards 模型).
        ///     速度上限 = |desiredVel|.
        /// </summary>
        public static void SimulateMovementStep(
            ref Vector2 vel, Vector2 desiredVel,
            float acceleration, float dt)
        {
            Vector2 velDiff = desiredVel - vel;
            float diffMag = velDiff.magnitude;
            if (diffMag > 0.001f)
            {
                Vector2 accelVec = velDiff / diffMag * acceleration;
                vel += accelVec * dt;
                float maxSpeed = desiredVel.magnitude;
                if (vel.sqrMagnitude > maxSpeed * maxSpeed)
                    vel = vel.normalized * maxSpeed;
            }
        }
    }

}
