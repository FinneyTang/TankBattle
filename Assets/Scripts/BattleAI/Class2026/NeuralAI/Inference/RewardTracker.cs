using System.Collections.Generic;
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace NeuralAI
{
    public class RewardTracker
    {
        // ============================================================
        // Internal state
        // ============================================================
        private readonly List<Tank> m_Opponents = new List<Tank>(4);
        private readonly List<Tank> m_ScoreOpponents = new List<Tank>(4);
        private readonly Dictionary<int, Missile> m_OpponentMissiles = new Dictionary<int, Missile>();
        private readonly HashSet<int> m_CurrentThreatMissiles = new HashSet<int>();
        private readonly HashSet<int> m_TrackedThreatMissiles = new HashSet<int>();
        private readonly List<int> m_ResolvedThreatMissiles = new List<int>();
        private readonly Dictionary<int, float> m_LastStarDistances = new Dictionary<int, float>();

        private Tank m_Tank;
        private Tank m_LastEnemy;
        private int m_LastTeamScore;
        private int m_LastHp;
        private int m_LastOpponentScore;
        private int m_LastEnemyHp;
        private bool m_WasDead;
        private float m_LastCenterDistance;
        private float m_LastHomeDistance;
        private Vector3 m_LastPosition;
        private int m_InvalidMoves;
        private int m_WastedShots;
        private int m_SuccessfulShots;
        private bool m_TerminalRewardGiven;

        // 课程学习奖励权重（由 RefreshRewardScales 更新）
        public float StarRewardScale = 1f;
        public float DamageDealtScale = 1f;
        public float DeathPenaltyScale = 1f;
        public float FireAccuracyScale = 1f;
        public float HealRewardScale = 1f;

        // ============================================================
        // External parameters (loaded from JSON)
        // ============================================================
        private struct RewardParameters
        {
            public float SuperStarScoreReward;
            public float OpponentScorePenaltyMultiplier;
            public float DamageRewardPerHit;
            public float DeathPenalty;
            public float DodgeRewardPerThreat;
            public float MaxDodgeRewardPerStep;
            public float HealRewardPerHp;

            public float TimePenaltyThresholdHigh;
            public float TimePenaltyThresholdMedium;
            public float TimePenaltyThresholdLow;
            public float TimePenaltyHealthy;
            public float TimePenaltyMedium;
            public float TimePenaltyLow;
            public float TimePenaltyCritical;

            public float InvalidMovePenalty;
            public float WastedShotPenalty;

            public float SuperStarWeight;
            public float NormalStarWeight;
            public float HomeWeightMultiplier;
            public float HomeWeightMax;
            public float StarProgressClampMin;
            public float StarProgressClampMax;
            public float MinMoveSqrMagnitude;
            public float MinToTargetSqrMagnitude;
            public float AntiFarmingAlignmentThreshold;
            public float MissileThreatDiscount;
            public float StarProgressNormalizationOffset;

            public float SuperStarPrepWindowStartOffset;
            public float SuperStarPrepWindowEndOffset;
            public float SuperStarPrepClampMin;
            public float SuperStarPrepClampMax;

            public float MovementDeathPenalty;
            public float MovementIdleThreshold;
            public float MovementVelocityThresholdSqr;
            public float MovementIdlePenalty;

            public float FireAccuracyDotThreshold;
            public float FireAccuracyPenaltyMultiplier;
            public float FireAccuracyBlockedPenalty;
            public float FireAccuracyMinSqrMagnitude;

            public float AimingDotThreshold;
            public float AimingReward;
            public float AimingMinSqrMagnitude;

            public float MissileMinVelocitySqr;
            public float MissileMaxDetectionDistance;
            public float MissileSphereCastRadius;

            public float TerminalScoreNormalizerMultiplier;
            public float TerminalWinnerBase;
            public float TerminalLoserBase;
        }

        private RewardParameters m_Params;

        // ============================================================
        // Lifecycle
        // ============================================================
        public void Bind(Tank tank)
        {
            m_Tank = tank;
            ApplyConfig();
            Reset();
        }

        private void ApplyConfig()
        {
            var cfg = TankBattleConfigLoader.Config?.RewardConfig;
            if (cfg == null)
            {
                m_Params = new RewardParameters(); // fallback: all defaults from struct init (0)
                // Re-apply hardcoded defaults manually
                m_Params.SuperStarScoreReward = 1.5f;
                m_Params.OpponentScorePenaltyMultiplier = 0.2f;
                m_Params.DamageRewardPerHit = 0.1f;
                m_Params.DeathPenalty = 1f;
                m_Params.DodgeRewardPerThreat = 0.02f;
                m_Params.MaxDodgeRewardPerStep = 0.1f;
                m_Params.HealRewardPerHp = 0.02f;
                m_Params.TimePenaltyThresholdHigh = 0.75f;
                m_Params.TimePenaltyThresholdMedium = 0.5f;
                m_Params.TimePenaltyThresholdLow = 0.25f;
                m_Params.TimePenaltyHealthy = -0.0001f;
                m_Params.TimePenaltyMedium = -0.0002f;
                m_Params.TimePenaltyLow = -0.0003f;
                m_Params.TimePenaltyCritical = -0.0004f;
                m_Params.InvalidMovePenalty = 0.01f;
                m_Params.WastedShotPenalty = 0.005f;
                m_Params.SuperStarWeight = 6f;
                m_Params.NormalStarWeight = 1f;
                m_Params.HomeWeightMultiplier = 7f;
                m_Params.HomeWeightMax = 5.5f;
                m_Params.StarProgressClampMin = -0.01f;
                m_Params.StarProgressClampMax = 0.01f;
                m_Params.MinMoveSqrMagnitude = 0.0001f;
                m_Params.MinToTargetSqrMagnitude = 0.0001f;
                m_Params.AntiFarmingAlignmentThreshold = -0.3f;
                m_Params.MissileThreatDiscount = 0.2f;
            m_Params.StarProgressNormalizationOffset = 1f;
                m_Params.SuperStarPrepWindowStartOffset = -5f;
                m_Params.SuperStarPrepWindowEndOffset = 10f;
                m_Params.SuperStarPrepClampMin = -0.01f;
                m_Params.SuperStarPrepClampMax = 0.02f;
                m_Params.MovementDeathPenalty = -0.00001f;
                m_Params.MovementIdleThreshold = 0.01f;
                m_Params.MovementVelocityThresholdSqr = 0.01f;
                m_Params.MovementIdlePenalty = -0.001f;
                m_Params.FireAccuracyDotThreshold = 0.98f;
                m_Params.FireAccuracyPenaltyMultiplier = 0.01f;
                m_Params.FireAccuracyBlockedPenalty = 0.01f;
                m_Params.FireAccuracyMinSqrMagnitude = 0.0001f;
                m_Params.AimingDotThreshold = 0.95f;
                m_Params.AimingReward = 0.002f;
                m_Params.AimingMinSqrMagnitude = 0.0001f;
                m_Params.MissileMinVelocitySqr = 0.0001f;
                m_Params.MissileMaxDetectionDistance = 60f;
                m_Params.MissileSphereCastRadius = 1.0f;
                m_Params.TerminalScoreNormalizerMultiplier = 2f;
                m_Params.TerminalWinnerBase = 1f;
                m_Params.TerminalLoserBase = -1f;
                return;
            }

            m_Params.SuperStarScoreReward = cfg.SuperStarScoreReward;
            m_Params.OpponentScorePenaltyMultiplier = cfg.OpponentScorePenaltyMultiplier;
            m_Params.DamageRewardPerHit = cfg.DamageRewardPerHit;
            m_Params.DeathPenalty = cfg.DeathPenalty;
            m_Params.DodgeRewardPerThreat = cfg.DodgeRewardPerThreat;
            m_Params.MaxDodgeRewardPerStep = cfg.MaxDodgeRewardPerStep;
            m_Params.HealRewardPerHp = cfg.HealRewardPerHp;

            m_Params.TimePenaltyThresholdHigh = cfg.TimePenaltyThresholdHigh;
            m_Params.TimePenaltyThresholdMedium = cfg.TimePenaltyThresholdMedium;
            m_Params.TimePenaltyThresholdLow = cfg.TimePenaltyThresholdLow;
            m_Params.TimePenaltyHealthy = cfg.TimePenaltyHealthy;
            m_Params.TimePenaltyMedium = cfg.TimePenaltyMedium;
            m_Params.TimePenaltyLow = cfg.TimePenaltyLow;
            m_Params.TimePenaltyCritical = cfg.TimePenaltyCritical;

            m_Params.InvalidMovePenalty = cfg.InvalidMovePenalty;
            m_Params.WastedShotPenalty = cfg.WastedShotPenalty;

            m_Params.SuperStarWeight = cfg.SuperStarWeight;
            m_Params.NormalStarWeight = cfg.NormalStarWeight;
            m_Params.HomeWeightMultiplier = cfg.HomeWeightMultiplier;
            m_Params.HomeWeightMax = cfg.HomeWeightMax;
            m_Params.StarProgressClampMin = cfg.StarProgressClampMin;
            m_Params.StarProgressClampMax = cfg.StarProgressClampMax;
            m_Params.MinMoveSqrMagnitude = cfg.MinMoveSqrMagnitude;
            m_Params.MinToTargetSqrMagnitude = cfg.MinToTargetSqrMagnitude;
            m_Params.AntiFarmingAlignmentThreshold = cfg.AntiFarmingAlignmentThreshold;
            m_Params.MissileThreatDiscount = cfg.MissileThreatDiscount;
            m_Params.StarProgressNormalizationOffset = cfg.StarProgressNormalizationOffset;

            m_Params.SuperStarPrepWindowStartOffset = cfg.SuperStarPrepWindowStartOffset;
            m_Params.SuperStarPrepWindowEndOffset = cfg.SuperStarPrepWindowEndOffset;
            m_Params.SuperStarPrepClampMin = cfg.SuperStarPrepClampMin;
            m_Params.SuperStarPrepClampMax = cfg.SuperStarPrepClampMax;

            m_Params.MovementDeathPenalty = cfg.MovementDeathPenalty;
            m_Params.MovementIdleThreshold = cfg.MovementIdleThreshold;
            m_Params.MovementVelocityThresholdSqr = cfg.MovementVelocityThresholdSqr;
            m_Params.MovementIdlePenalty = cfg.MovementIdlePenalty;

            m_Params.FireAccuracyDotThreshold = cfg.FireAccuracyDotThreshold;
            m_Params.FireAccuracyPenaltyMultiplier = cfg.FireAccuracyPenaltyMultiplier;
            m_Params.FireAccuracyBlockedPenalty = cfg.FireAccuracyBlockedPenalty;
            m_Params.FireAccuracyMinSqrMagnitude = cfg.FireAccuracyMinSqrMagnitude;

            m_Params.AimingDotThreshold = cfg.AimingDotThreshold;
            m_Params.AimingReward = cfg.AimingReward;
            m_Params.AimingMinSqrMagnitude = cfg.AimingMinSqrMagnitude;

            m_Params.MissileMinVelocitySqr = cfg.MissileMinVelocitySqr;
            m_Params.MissileMaxDetectionDistance = cfg.MissileMaxDetectionDistance;
            m_Params.MissileSphereCastRadius = cfg.MissileSphereCastRadius;

            m_Params.TerminalScoreNormalizerMultiplier = cfg.TerminalScoreNormalizerMultiplier;
            m_Params.TerminalWinnerBase = cfg.TerminalWinnerBase;
            m_Params.TerminalLoserBase = cfg.TerminalLoserBase;
        }

        public void Reset()
        {
            RefreshRewardScales();

            if (m_Tank == null || Match.instance == null)
            {
                return;
            }

            Tank enemy = FindNearestEnemy();
            m_LastEnemy = enemy;
            m_LastTeamScore = GetTeamScore(m_Tank.Team);
            m_LastHp = m_Tank.HP;
            m_LastOpponentScore = GetOpposingTeamsScore(m_Tank.Team);
            m_LastEnemyHp = enemy != null ? enemy.HP : 0;
            m_WasDead = m_Tank.IsDead;
            m_LastStarDistances.Clear();
            m_LastCenterDistance = Vector3.Distance(m_Tank.Position, Vector3.zero);
            m_LastHomeDistance = GetPathDistance(Match.instance.GetRebornPos(m_Tank.Team));
            m_LastPosition = m_Tank.Position;
            m_InvalidMoves = 0;
            m_WastedShots = 0;
            m_SuccessfulShots = 0;
            m_CurrentThreatMissiles.Clear();
            m_TrackedThreatMissiles.Clear();
            m_ResolvedThreatMissiles.Clear();
            m_TerminalRewardGiven = false;
        }

        public void ReportAction(TankBattleActionResult result)
        {
            if (result.MoveRequested && !result.MoveSucceeded)
            {
                m_InvalidMoves++;
            }

            if (result.FireRequested && !result.FireSucceeded)
            {
                m_WastedShots++;
            }

            if (result.FireRequested && result.FireSucceeded)
            {
                m_SuccessfulShots++;
            }
        }

        // ============================================================
        // Reward Collection
        // ============================================================
        public float CollectStepReward()
        {
            if (m_Tank == null || Match.instance == null)
            {
                return 0f;
            }

            Match match = Match.instance;
            Tank enemy = FindNearestEnemy();
            float reward = GetTimeBasedStepPenalty(match);

            int teamScore = GetTeamScore(m_Tank.Team);
            int teamScoreDelta = teamScore - m_LastTeamScore;
            if (teamScoreDelta > 0)
            {
                reward += ScoreDeltaReward(teamScoreDelta, match);
            }

            int opponentScore = GetOpposingTeamsScore(m_Tank.Team);
            int opponentScoreDelta = opponentScore - m_LastOpponentScore;
            if (opponentScoreDelta > 0)
            {
                reward -= ScoreDeltaReward(opponentScoreDelta, match) * m_Params.OpponentScorePenaltyMultiplier;
            }

            if (enemy != m_LastEnemy)
            {
                m_LastEnemy = enemy;
                m_LastEnemyHp = enemy != null ? enemy.HP : 0;
            }

            if (enemy != null)
            {
                int damageDealt = Mathf.Max(0, m_LastEnemyHp - enemy.HP);
                reward += DamageReward(damageDealt, match) * DamageDealtScale;
                m_LastEnemyHp = enemy.HP;
            }

            // 先处理复活状态转换，避免将复活视为治疗
            if (m_WasDead && !m_Tank.IsDead)
            {
                m_LastHp = m_Tank.HP;
            }

            int damageTaken = Mathf.Max(0, m_LastHp - m_Tank.HP);
            reward -= DamageReward(damageTaken, match);
            reward += EvasionReward(damageTaken);

            int healAmount = Mathf.Max(0, m_Tank.HP - m_LastHp);
            reward += m_Params.HealRewardPerHp * healAmount * HealRewardScale;

            if (!m_WasDead && m_Tank.IsDead)
            {
                reward -= m_Params.DeathPenalty * DeathPenaltyScale;
            }

            bool hasMissileThreat = m_CurrentThreatMissiles.Count > 0;
            reward += StarProgressReward(hasMissileThreat) * StarRewardScale;
            reward += SuperStarPreparationReward(match) * StarRewardScale;
            reward += MovementReward(match);
            reward += FireAccuracyReward() * FireAccuracyScale;
            reward += AimingReward();
            reward -= m_Params.InvalidMovePenalty * m_InvalidMoves;
            reward -= m_Params.WastedShotPenalty * m_WastedShots;

            m_LastTeamScore = teamScore;
            m_LastOpponentScore = opponentScore;
            m_LastHp = m_Tank.HP;
            m_WasDead = m_Tank.IsDead;
            m_LastPosition = m_Tank.Position;
            m_InvalidMoves = 0;
            m_WastedShots = 0;
            m_SuccessfulShots = 0;

            return reward;
        }

        public float CollectTerminalReward()
        {
            if (m_TerminalRewardGiven || m_Tank == null || Match.instance == null ||
                !Match.instance.IsMathEnd())
            {
                return 0f;
            }

            m_TerminalRewardGiven = true;
            Match match = Match.instance;
            int teamScore = GetTeamScore(m_Tank.Team);
            int opponentScore = GetOpposingTeamsScore(m_Tank.Team);
            float scoreBonus = Mathf.Clamp(
                (teamScore - opponentScore) /
                Mathf.Max(1f, match.GlobalSetting.ScoreForSuperStar * m_Params.TerminalScoreNormalizerMultiplier),
                -1f,
                1f);
            return match.WinnerTeam == m_Tank.Team ? m_Params.TerminalWinnerBase + scoreBonus : m_Params.TerminalLoserBase + scoreBonus;
        }

        // ============================================================
        // Time Penalty
        // ============================================================
        private float GetTimeBasedStepPenalty(Match match)
        {
            if (m_Tank == null)
            {
                return m_Params.TimePenaltyHealthy;
            }

            float hpRatio = (float)m_Tank.HP / Mathf.Max(1, match.GlobalSetting.MaxHP);
            if (hpRatio > m_Params.TimePenaltyThresholdHigh)
            {
                return m_Params.TimePenaltyHealthy;
            }

            if (hpRatio > m_Params.TimePenaltyThresholdMedium)
            {
                return m_Params.TimePenaltyMedium;
            }

            if (hpRatio > m_Params.TimePenaltyThresholdLow)
            {
                return m_Params.TimePenaltyLow;
            }

            return m_Params.TimePenaltyCritical;
        }

        // ============================================================
        // Score & Damage
        // ============================================================
        private float ScoreDeltaReward(int scoreDelta, Match match)
        {
            return Mathf.Max(0, scoreDelta) * m_Params.SuperStarScoreReward /
                   Mathf.Max(1, match.GlobalSetting.ScoreForSuperStar);
        }

        private float DamageReward(int damage, Match match)
        {
            return m_Params.DamageRewardPerHit * damage / Mathf.Max(1, match.GlobalSetting.DamagePerHit);
        }

        // ============================================================
        // Star Progress
        // ============================================================
        private float StarProgressReward(bool hasMissileThreat)
        {
            if (m_Tank == null || Match.instance == null)
            {
                m_LastStarDistances.Clear();
                return 0f;
            }

            float fieldSize = Mathf.Max(1f, Match.instance.FieldSize);
            float weightedProgress = 0f;
            Star dominantStar = null;
            float maxWeightedProgress = float.MinValue;

            foreach (var pair in Match.instance.GetStars())
            {
                Star star = pair.Value;
                if (star == null) continue;

                float currentDist = GetPathDistance(star.Position);
                if (!m_LastStarDistances.TryGetValue(star.ID, out float lastDist) || lastDist <= 0f)
                {
                    lastDist = currentDist;
                }

                float progress = (lastDist - currentDist) / fieldSize;
                float weight = star.IsSuperStar ? m_Params.SuperStarWeight : m_Params.NormalStarWeight;
                float wProgress = weight * progress;
                weightedProgress += wProgress;

                if (wProgress > maxWeightedProgress)
                {
                    maxWeightedProgress = wProgress;
                    dominantStar = star;
                }

                m_LastStarDistances[star.ID] = currentDist;
            }

            // Clean up records for stars that no longer exist
            var idsToRemove = new List<int>();
            foreach (var id in m_LastStarDistances.Keys)
            {
                if (!Match.instance.GetStars().ContainsKey(id))
                {
                    idsToRemove.Add(id);
                }
            }
            foreach (var id in idsToRemove)
            {
                m_LastStarDistances.Remove(id);
            }

            // Home as a virtual target
            float currentHomeDist = m_Tank.IsDead ? 0f : GetPathDistance(Match.instance.GetRebornPos(m_Tank.Team));
            float homeProgress = (m_LastHomeDistance - currentHomeDist) / fieldSize;
            float hpRatio = (float)m_Tank.HP / Mathf.Max(1, Match.instance.GlobalSetting.MaxHP);
            float homeWeight = Mathf.Clamp(m_Params.HomeWeightMultiplier * (1f - hpRatio), 0f, m_Params.HomeWeightMax);
            float homeWProgress = homeWeight * homeProgress;
            weightedProgress += homeWProgress;
            m_LastHomeDistance = currentHomeDist;

            if (homeWProgress > maxWeightedProgress)
            {
                maxWeightedProgress = homeWProgress;
                dominantStar = null;
            }

            weightedProgress /= Match.instance.GlobalSetting.MaxStarCount + m_Params.StarProgressNormalizationOffset;

            float reward = Mathf.Clamp(weightedProgress, m_Params.StarProgressClampMin, m_Params.StarProgressClampMax);

            // Anti-farming
            if (reward > 0f && dominantStar != null)
            {
                Vector3 toStar = dominantStar.Position - m_Tank.Position;
                toStar.y = 0f;
                Vector3 displacement = m_Tank.Position - m_LastPosition;
                displacement.y = 0f;

                if (displacement.sqrMagnitude > m_Params.MinMoveSqrMagnitude && toStar.sqrMagnitude > m_Params.MinToTargetSqrMagnitude)
                {
                    float alignment = Vector3.Dot(displacement.normalized, toStar.normalized);
                    if (alignment < m_Params.AntiFarmingAlignmentThreshold)
                    {
                        reward = 0f;
                    }
                }
            }

            // Safety discount under missile threat
            if (reward > 0f && hasMissileThreat)
            {
                reward *= m_Params.MissileThreatDiscount;
            }

            return reward;
        }

        // ============================================================
        // Super Star Preparation
        // ============================================================
        private float SuperStarPreparationReward(Match match)
        {
            float halfTime = match.GlobalSetting.MatchTime * 0.5f;
            bool inPreparationWindow = match.RemainingTime > halfTime + m_Params.SuperStarPrepWindowStartOffset &&
                                       match.RemainingTime < halfTime + m_Params.SuperStarPrepWindowEndOffset;
            float currentCenterDistance = Vector3.Distance(m_Tank.Position, Vector3.zero);
            float reward = 0f;

            bool superStarShouldHaveSpawned = match.RemainingTime < halfTime;
            if (superStarShouldHaveSpawned && !HasSuperStarOnField())
            {
                m_LastCenterDistance = currentCenterDistance;
                return 0f;
            }

            if (inPreparationWindow)
            {
                reward = Mathf.Clamp((m_LastCenterDistance - currentCenterDistance) /
                                     Mathf.Max(1f, match.FieldSize),
                                     m_Params.SuperStarPrepClampMin,
                                     m_Params.SuperStarPrepClampMax);
            }

            m_LastCenterDistance = currentCenterDistance;
            return reward;
        }

        private bool HasSuperStarOnField()
        {
            if (Match.instance == null)
            {
                return false;
            }

            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value != null && pair.Value.IsSuperStar)
                {
                    return true;
                }
            }

            return false;
        }

        // ============================================================
        // Movement
        // ============================================================
        private float MovementReward(Match match)
        {
            if (m_Tank.IsDead)
            {
                return m_Params.MovementDeathPenalty;
            }

            float movedDistance = Vector3.Distance(m_Tank.Position, m_LastPosition);
            if (movedDistance < m_Params.MovementIdleThreshold && m_Tank.Velocity.sqrMagnitude < m_Params.MovementVelocityThresholdSqr)
            {
                bool inHomeZone = Vector3.Distance(m_Tank.Position, match.GetRebornPos(m_Tank.Team))
                                  < match.GlobalSetting.HomeZoneRadius;
                bool needsHeal = m_Tank.HP < match.GlobalSetting.MaxHP;
                if (inHomeZone && needsHeal)
                {
                    return 0f;
                }

                return m_Params.MovementIdlePenalty;
            }

            return 0f;
        }

        // ============================================================
        // Fire Accuracy
        // ============================================================
        private float FireAccuracyReward()
        {
            if (m_SuccessfulShots <= 0)
            {
                return 0f;
            }

            float penalty = 0f;
            Tank enemy = FindNearestEnemy();

            // 计算瞄准偏差（基于最近活着的敌人）
            if (enemy != null && !enemy.IsDead)
            {
                Vector3 predictedTarget = TankBattleActionMapper.GetPredictedAimTarget(m_Tank, enemy);
                Vector3 toPredicted = predictedTarget - m_Tank.FirePos;
                toPredicted.y = 0f;
                Vector3 aim = m_Tank.TurretAiming;
                aim.y = 0f;

                if (toPredicted.sqrMagnitude >= m_Params.FireAccuracyMinSqrMagnitude && aim.sqrMagnitude >= m_Params.FireAccuracyMinSqrMagnitude)
                {
                    float dot = Vector3.Dot(aim.normalized, toPredicted.normalized);
                    float maxDist = toPredicted.magnitude;

                    if (dot < m_Params.FireAccuracyDotThreshold)
                    {
                        penalty = Mathf.Clamp01((m_Params.FireAccuracyDotThreshold - dot) / m_Params.FireAccuracyDotThreshold) * m_Params.FireAccuracyPenaltyMultiplier;
                    }

                    if (!TankBattleActionMapper.WillHitEnemyAlongAim(m_Tank, maxDist))
                    {
                        penalty = Mathf.Max(penalty, m_Params.FireAccuracyBlockedPenalty);
                    }
                }
            }
            else
            {
                // 没有活着的敌人：检查是否向墙开火
                Vector3 aim = m_Tank.TurretAiming;
                aim.y = 0f;
                if (aim.sqrMagnitude >= m_Params.FireAccuracyMinSqrMagnitude)
                {
                    if (Physics.SphereCast(m_Tank.FirePos, 0.24f, aim.normalized, out RaycastHit hit,
                        60f, PhysicsUtils.LayerMaskScene))
                    {
                        penalty = Mathf.Max(penalty, m_Params.FireAccuracyBlockedPenalty);
                    }
                }
            }

            return -penalty * m_SuccessfulShots;
        }

        // ============================================================
        // Aiming
        // ============================================================
        private float AimingReward()
        {
            Tank enemy = FindNearestEnemy();
            if (enemy == null || enemy.IsDead)
            {
                return 0f;
            }

            Vector3 predictedTarget = TankBattleActionMapper.GetPredictedAimTarget(m_Tank, enemy);
            Vector3 toPredicted = predictedTarget - m_Tank.FirePos;
            toPredicted.y = 0f;
            Vector3 aim = m_Tank.TurretAiming;
            aim.y = 0f;

            if (toPredicted.sqrMagnitude < m_Params.AimingMinSqrMagnitude || aim.sqrMagnitude < m_Params.AimingMinSqrMagnitude)
            {
                return 0f;
            }

            float dot = Vector3.Dot(aim.normalized, toPredicted.normalized);
            // 连续奖励：完全对准得 +AimingReward，背对得 -AimingReward
            return dot * m_Params.AimingReward;
        }

        // ============================================================
        // Evasion
        // ============================================================
        private float EvasionReward(int damageTaken)
        {
            if (m_Tank.IsDead || Match.instance == null)
            {
                m_CurrentThreatMissiles.Clear();
                m_TrackedThreatMissiles.Clear();
                m_ResolvedThreatMissiles.Clear();
                return 0f;
            }

            Match.instance.GetOppositeMissilesEx(m_Tank.Team, m_OpponentMissiles);
            m_CurrentThreatMissiles.Clear();
            foreach (var pair in m_OpponentMissiles)
            {
                Missile missile = pair.Value;
                if (missile != null && IsMissileThreateningTank(missile, m_Tank))
                {
                    m_CurrentThreatMissiles.Add(pair.Key);
                }
            }

            float reward = 0f;
            int resolvedCount = 0;
            m_ResolvedThreatMissiles.Clear();
            foreach (int missileId in m_TrackedThreatMissiles)
            {
                if (!m_CurrentThreatMissiles.Contains(missileId))
                {
                    m_ResolvedThreatMissiles.Add(missileId);
                    resolvedCount++;
                }
            }

            int hitCount = damageTaken > 0 ? Mathf.Max(1, damageTaken / Mathf.Max(1, Match.instance.GlobalSetting.DamagePerHit)) : 0;
            int dodgeCount = Mathf.Max(0, resolvedCount - hitCount);
            reward += dodgeCount * m_Params.DodgeRewardPerThreat;

            for (int i = 0; i < m_ResolvedThreatMissiles.Count; ++i)
            {
                m_TrackedThreatMissiles.Remove(m_ResolvedThreatMissiles[i]);
            }

            foreach (int missileId in m_CurrentThreatMissiles)
            {
                m_TrackedThreatMissiles.Add(missileId);
            }

            return Mathf.Min(reward, m_Params.MaxDodgeRewardPerStep);
        }

        private bool IsMissileThreateningTank(Missile missile, Tank tank)
        {
            Vector3 velocity = missile.Velocity;
            if (velocity.sqrMagnitude < m_Params.MissileMinVelocitySqr)
            {
                return false;
            }

            Vector3 toTank = tank.Position - missile.Position;
            float distance = toTank.magnitude;
            if (distance > m_Params.MissileMaxDetectionDistance)
            {
                return false;
            }

            Vector3 direction = velocity.normalized;
            if (Vector3.Dot(direction, toTank.normalized) <= 0f)
            {
                return false;
            }

            if (Physics.SphereCast(missile.Position, m_Params.MissileSphereCastRadius, direction,
                out RaycastHit hit, distance, PhysicsUtils.LayerMaskTank))
            {
                FireCollider fc = hit.collider.GetComponent<FireCollider>();
                if (fc != null && fc.Owner == tank)
                {
                    return true;
                }
            }

            return false;
        }

        // ============================================================
        // Helpers
        // ============================================================
        private float GetPathDistance(Vector3 targetPos)
        {
            if (m_Tank == null)
            {
                return float.PositiveInfinity;
            }

            if (m_Tank.IsDead)
            {
                return Vector3.Distance(m_Tank.Position, targetPos);
            }

            NavMeshPath path = m_Tank.CaculatePath(targetPos);
            if (path == null || path.corners.Length == 0)
            {
                return Vector3.Distance(m_Tank.Position, targetPos);
            }

            float distance = 0f;
            Vector3 from = m_Tank.Position;
            for (int i = 0; i < path.corners.Length; ++i)
            {
                distance += Vector3.Distance(from, path.corners[i]);
                from = path.corners[i];
            }
            return distance;
        }

        private float GetNearestValuableStarDistance()
        {
            if (m_Tank == null || Match.instance == null || Match.instance.GetStars().Count == 0)
            {
                return float.PositiveInfinity;
            }

            float nearest = float.PositiveInfinity;
            foreach (var pair in Match.instance.GetStars())
            {
                Star star = pair.Value;
                if (star == null)
                {
                    continue;
                }

                float dist = GetPathDistance(star.Position);
                if (dist < nearest)
                {
                    nearest = dist;
                }
            }

            return nearest;
        }

        private Star FindNearestStar()
        {
            if (m_Tank == null || Match.instance == null || Match.instance.GetStars().Count == 0)
            {
                return null;
            }

            float nearest = float.PositiveInfinity;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star star = pair.Value;
                if (star == null)
                {
                    continue;
                }

                float dist = GetPathDistance(star.Position);
                if (dist < nearest)
                {
                    nearest = dist;
                    nearestStar = star;
                }
            }

            return nearestStar;
        }

        private bool IsNearestStarSuperStar()
        {
            Star nearestStar = FindNearestStar();
            return nearestStar != null && nearestStar.IsSuperStar;
        }

        private int GetTeamScore(ETeam team)
        {
            if (Match.instance == null)
            {
                return 0;
            }

            int score = 0;
            List<Tank> tanks = Match.instance.GetTanks(team);
            if (tanks == null)
            {
                return score;
            }

            for (int i = 0; i < tanks.Count; ++i)
            {
                if (tanks[i] != null)
                {
                    score += tanks[i].Score;
                }
            }

            return score;
        }

        private int GetOpposingTeamsScore(ETeam team)
        {
            if (Match.instance == null)
            {
                return 0;
            }

            int score = 0;
            Match.instance.GetOppositeTanks(team, m_ScoreOpponents);
            for (int i = 0; i < m_ScoreOpponents.Count; ++i)
            {
                if (m_ScoreOpponents[i] != null)
                {
                    score += m_ScoreOpponents[i].Score;
                }
            }

            return score;
        }

        private void RefreshRewardScales()
        {
            int stage = TankBattleCurriculum.CurrentStage;
            var scales = TankBattleConfigLoader.Config?.RewardConfig?.StageScales;

            if (scales != null && stage >= 0 && stage < scales.Count)
            {
                var s = scales[stage];
                StarRewardScale = s.StarRewardScale;
                DamageDealtScale = s.DamageDealtScale;
                DeathPenaltyScale = s.DeathPenaltyScale;
                FireAccuracyScale = s.FireAccuracyScale;
                HealRewardScale = s.HealRewardScale;
            }
            else
            {
                StarRewardScale = 1f;
                DamageDealtScale = 1f;
                DeathPenaltyScale = 1.5f;
                FireAccuracyScale = 1f;
                HealRewardScale = 1f;
            }
        }

        private Tank FindNearestEnemy()
        {
            if (m_Tank == null || Match.instance == null)
            {
                return null;
            }

            m_Opponents.Clear();
            Match.instance.GetOppositeTanks(m_Tank.Team, m_Opponents);
            Tank nearest = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < m_Opponents.Count; ++i)
            {
                Tank candidate = m_Opponents[i];
                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                float sqrDistance = (candidate.Position - m_Tank.Position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
