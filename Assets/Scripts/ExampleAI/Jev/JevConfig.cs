using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Jev
{
    public enum EDodgeMode
    {
        Off,
        Jev,
        Reflex,
    }

    /// <summary>
    /// Tunables for the Jev tank. Defaults live here; every field can be overridden by an
    /// optional JSON file at ~/.typesafe/tankbattle_jev.json (only the keys you write are applied).
    /// The API key is never stored in the project: it is read from the TYPESAFE_API_KEY
    /// environment variable, or from the file ~/.typesafe/api_key.
    /// </summary>
    [Serializable]
    public class JevConfig
    {
        public const string ConfigDirName = ".typesafe";
        public const string ConfigFileName = "tankbattle_jev.json";
        public const string ApiKeyFileName = "api_key";
        public const string ApiKeyEnvVar = "TYPESAFE_API_KEY";

        // ---- TypeSafe API ----
        public string Endpoint = "https://api.typesafe.ai/v1/systemone";
        public string Model = "jev-latest";

        // ---- decision loop ----
        /// <summary>Seconds between the end of one request and the start of the next.</summary>
        public float DecisionInterval = 0f;
        /// <summary>Seconds before an in-flight request is abandoned.</summary>
        public float RequestTimeout = 3f;
        /// <summary>Consecutive failures before switching to the rule-based fallback.</summary>
        public int FallbackAfterFailures = 3;
        /// <summary>Seconds to stay in fallback before probing Jev again.</summary>
        public float FallbackRetryInterval = 5f;

        // ---- confidence gating (hysteresis) ----
        /// <summary>Changing destination costs travel time, so demand this much confidence to switch.</summary>
        public float MoveSwitchConfidence = 0.35f;
        /// <summary>Re-aiming the turret is cheap, so a low bar is enough.</summary>
        public float AimSwitchConfidence = 0.15f;

        // ---- world description thresholds (fractions of FieldSize) ----
        public float NearDistanceFraction = 0.15f;
        public float MediumDistanceFraction = 0.35f;
        /// <summary>
        /// Measure distances to stars, tanks and home along the NavMesh path instead of as the crow flies,
        /// so a star behind a wall reads as "far". Unreachable targets count as twice the straight line.
        /// </summary>
        public bool UsePathDistances = true;

        // ---- shooting (pure code; Jev only picks the target) ----
        /// <summary>Aim at where the target will be when the missile arrives instead of where it is now.</summary>
        public bool EnableLeadAiming = true;
        /// <summary>Cap on the predicted flight time, so distant fast targets do not produce absurd lead points.</summary>
        public float MaxLeadSeconds = 2f;

        // ---- dodging ----
        /// <summary>
        /// Off: never dodge. Jev: offer a "sidestep" option and ask Jev immediately when a missile is about
        /// to hit me. Reflex: code sidesteps on its own the moment a missile is on a collision course,
        /// overriding the current destination for a moment; Jev is not consulted.
        /// </summary>
        public EDodgeMode DodgeMode = EDodgeMode.Reflex;
        /// <summary>Reflex only: start dodging once the missile is within this distance (world units).</summary>
        public float ReflexDodgeMaxDistance = 30f;
        /// <summary>Reflex only: closer than this it is too late; turning the chassis now only exposes the flank.</summary>
        public float ReflexDodgeMinDistance = 10f;
        /// <summary>Reflex only: do not dodge when the enemy dies to my next shot and I can still take two hits.</summary>
        public bool ReflexDodgeSkipForKill = true;
        /// <summary>Missiles arriving later than this many seconds are not reported as threats.</summary>
        public float ThreatEtaSeconds = 2f;
        /// <summary>
        /// Geometric pre-filter: only missiles whose straight path passes closer than this are examined.
        /// Whether one actually hits me is then decided by a physics sphere cast along its path.
        /// </summary>
        public float ThreatNearMissRadius = 7f;
        /// <summary>How far to dart sideways when sidestepping.</summary>
        public float SidestepDistance = 5f;
        /// <summary>Switching into a sidestep is time critical, so it uses its own lower confidence bar.</summary>
        public float SidestepSwitchConfidence = 0.2f;

        // ---- statistics ----
        /// <summary>Append live request/latency/token counters to the tank's gizmo label in the Scene view.</summary>
        public bool ShowStatsInGizmo = true;
        /// <summary>Used for the cost estimate. TypeSafe lists $0.042 per million input tokens; output is free.</summary>
        public float InputTokenPriceUsdPerMillion = 0.042f;

        // ---- logging ----
        public bool EnableLogging = true;
        /// <summary>Include the full state object in every log line (verbose but useful for analysis).</summary>
        public bool LogState = true;
        /// <summary>Relative to the project folder (parent of Assets). Logs/ is git-ignored.</summary>
        public string LogDirectory = "Logs/Jev";

        public static string ConfigDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ConfigDirName);

        public static JevConfig Load()
        {
            var config = new JevConfig();
            var path = Path.Combine(ConfigDirectory, ConfigFileName);
            if (!File.Exists(path))
            {
                return config;
            }
            try
            {
                // PopulateObject only touches keys present in the file, so partial overrides work.
                JsonConvert.PopulateObject(File.ReadAllText(path), config);
                Debug.Log($"[Jev] Loaded config overrides from {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Jev] Failed to read {path}, using defaults: {e.Message}");
            }
            return config;
        }

        public static string LoadApiKey()
        {
            var key = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }
            var path = Path.Combine(ConfigDirectory, ApiKeyFileName);
            if (File.Exists(path))
            {
                try
                {
                    key = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        return key.Trim();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Jev] Failed to read API key file {path}: {e.Message}");
                }
            }
            Debug.LogWarning(
                $"[Jev] No API key found. Set {ApiKeyEnvVar} or write the key to {path}. " +
                "The tank will run on its rule-based fallback.");
            return string.Empty;
        }
    }
}
