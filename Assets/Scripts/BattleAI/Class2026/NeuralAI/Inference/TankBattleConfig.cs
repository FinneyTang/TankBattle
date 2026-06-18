using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeuralAI
{
    // ============================================================
    // Curriculum
    // ============================================================
    [System.Serializable]
    public class StageConfigData
    {
        [JsonProperty("opponentScript")]
        public string OpponentScript = "";

        [JsonProperty("opponentPool")]
        public List<string> OpponentPool = new List<string>();

        [JsonProperty("randomizeOpponent")]
        public bool RandomizeOpponent = false;

        [JsonProperty("matchTimeOverrideSeconds")]
        public int MatchTimeOverrideSeconds = 0;
    }

    [System.Serializable]
    public class StageScaleData
    {
        [JsonProperty("starRewardScale")]
        public float StarRewardScale = 1f;

        [JsonProperty("damageDealtScale")]
        public float DamageDealtScale = 1f;

        [JsonProperty("deathPenaltyScale")]
        public float DeathPenaltyScale = 1f;

        [JsonProperty("fireAccuracyScale")]
        public float FireAccuracyScale = 1f;

        [JsonProperty("healRewardScale")]
        public float HealRewardScale = 1f;
    }

    // ============================================================
    // Reward Parameters
    // ============================================================
    [System.Serializable]
    public class RewardConfigData
    {
        // --- Core ---
        [JsonProperty("superStarScoreReward")]
        public float SuperStarScoreReward = 1.5f;

        [JsonProperty("opponentScorePenaltyMultiplier")]
        public float OpponentScorePenaltyMultiplier = 0.2f;

        [JsonProperty("damageRewardPerHit")]
        public float DamageRewardPerHit = 0.1f;

        [JsonProperty("deathPenalty")]
        public float DeathPenalty = 1f;

        [JsonProperty("dodgeRewardPerThreat")]
        public float DodgeRewardPerThreat = 0.02f;

        [JsonProperty("maxDodgeRewardPerStep")]
        public float MaxDodgeRewardPerStep = 0.1f;

        [JsonProperty("healRewardPerHp")]
        public float HealRewardPerHp = 0.02f;

        [JsonProperty("threatCrossTrackDistance")]
        public float ThreatCrossTrackDistance = 2.5f;

        [JsonProperty("threatTimeWindow")]
        public float ThreatTimeWindow = 2f;

        [JsonProperty("stageScales")]
        public List<StageScaleData> StageScales = new List<StageScaleData>();

        // --- Time-based Step Penalty ---
        [JsonProperty("timePenaltyThresholdHigh")]
        public float TimePenaltyThresholdHigh = 0.75f;

        [JsonProperty("timePenaltyThresholdMedium")]
        public float TimePenaltyThresholdMedium = 0.5f;

        [JsonProperty("timePenaltyThresholdLow")]
        public float TimePenaltyThresholdLow = 0.25f;

        [JsonProperty("timePenaltyHealthy")]
        public float TimePenaltyHealthy = -0.0001f;

        [JsonProperty("timePenaltyMedium")]
        public float TimePenaltyMedium = -0.0002f;

        [JsonProperty("timePenaltyLow")]
        public float TimePenaltyLow = -0.0003f;

        [JsonProperty("timePenaltyCritical")]
        public float TimePenaltyCritical = -0.0004f;

        // --- Action Penalty ---
        [JsonProperty("invalidMovePenalty")]
        public float InvalidMovePenalty = 0.01f;

        [JsonProperty("wastedShotPenalty")]
        public float WastedShotPenalty = 0.005f;

        // --- Star Progress ---
        [JsonProperty("superStarWeight")]
        public float SuperStarWeight = 6f;

        [JsonProperty("normalStarWeight")]
        public float NormalStarWeight = 1f;

        [JsonProperty("homeWeightMultiplier")]
        public float HomeWeightMultiplier = 7f;

        [JsonProperty("homeWeightMax")]
        public float HomeWeightMax = 5.5f;

        [JsonProperty("starProgressClampMin")]
        public float StarProgressClampMin = -0.01f;

        [JsonProperty("starProgressClampMax")]
        public float StarProgressClampMax = 0.01f;

        [JsonProperty("minMoveSqrMagnitude")]
        public float MinMoveSqrMagnitude = 0.0001f;

        [JsonProperty("minToTargetSqrMagnitude")]
        public float MinToTargetSqrMagnitude = 0.0001f;

        [JsonProperty("antiFarmingAlignmentThreshold")]
        public float AntiFarmingAlignmentThreshold = -0.3f;

        [JsonProperty("missileThreatDiscount")]
        public float MissileThreatDiscount = 0.2f;

        [JsonProperty("starProgressNormalizationOffset")]
        public float StarProgressNormalizationOffset = 1f;

        // --- Super Star Preparation ---
        [JsonProperty("superStarPrepWindowStartOffset")]
        public float SuperStarPrepWindowStartOffset = -5f;

        [JsonProperty("superStarPrepWindowEndOffset")]
        public float SuperStarPrepWindowEndOffset = 10f;

        [JsonProperty("superStarPrepClampMin")]
        public float SuperStarPrepClampMin = -0.01f;

        [JsonProperty("superStarPrepClampMax")]
        public float SuperStarPrepClampMax = 0.02f;

        // --- Movement ---
        [JsonProperty("movementDeathPenalty")]
        public float MovementDeathPenalty = -0.00001f;

        [JsonProperty("movementIdleThreshold")]
        public float MovementIdleThreshold = 0.01f;

        [JsonProperty("movementVelocityThresholdSqr")]
        public float MovementVelocityThresholdSqr = 0.01f;

        [JsonProperty("movementIdlePenalty")]
        public float MovementIdlePenalty = -0.001f;

        // --- Fire Accuracy ---
        [JsonProperty("fireAccuracyDotThreshold")]
        public float FireAccuracyDotThreshold = 0.98f;

        [JsonProperty("fireAccuracyPenaltyMultiplier")]
        public float FireAccuracyPenaltyMultiplier = 0.01f;

        [JsonProperty("fireAccuracyBlockedPenalty")]
        public float FireAccuracyBlockedPenalty = 0.01f;

        [JsonProperty("fireAccuracyMinSqrMagnitude")]
        public float FireAccuracyMinSqrMagnitude = 0.0001f;

        // --- Aiming ---
        [JsonProperty("aimingDotThreshold")]
        public float AimingDotThreshold = 0.95f;

        [JsonProperty("aimingReward")]
        public float AimingReward = 0.002f;

        [JsonProperty("aimingMinSqrMagnitude")]
        public float AimingMinSqrMagnitude = 0.0001f;

        // --- Missile Threat ---
        [JsonProperty("missileMinVelocitySqr")]
        public float MissileMinVelocitySqr = 0.0001f;

        [JsonProperty("missileMaxDetectionDistance")]
        public float MissileMaxDetectionDistance = 60f;

        [JsonProperty("missileSphereCastRadius")]
        public float MissileSphereCastRadius = 1.0f;

        // --- Terminal ---
        [JsonProperty("terminalScoreNormalizerMultiplier")]
        public float TerminalScoreNormalizerMultiplier = 2f;

        [JsonProperty("terminalWinnerBase")]
        public float TerminalWinnerBase = 1f;

        [JsonProperty("terminalLoserBase")]
        public float TerminalLoserBase = -1f;
    }

    // ============================================================
    // Root Config
    // ============================================================
    [System.Serializable]
    public class TankBattleConfig
    {
        [JsonProperty("curriculumStages")]
        public List<StageConfigData> CurriculumStages = new List<StageConfigData>();

        [JsonProperty("rewardConfig")]
        public RewardConfigData RewardConfig = new RewardConfigData();
    }
}
