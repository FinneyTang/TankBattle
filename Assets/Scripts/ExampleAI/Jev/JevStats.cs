using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Jev
{
    /// <summary>
    /// Per-tank counters for one match: requests, failures, latency, token usage and an estimated cost.
    /// Rendered live as an on-screen overlay and dumped as the match-end summary.
    /// </summary>
    public class JevStats
    {
        public int Requests;
        public int UrgentRequests;
        public int Failures;
        public int Sidesteps;
        public int ReflexDodges;
        public int InputTokens;
        public int OutputTokens;
        public float TotalLatencyMs;
        public float LastLatencyMs;
        public float MaxLatencyMs;

        private readonly float m_StartTime;
        private readonly float m_InputPriceUsdPerMillion;

        // decision distribution: what Jev answered (raw), how it was applied, and the move answer split by my HP level
        private readonly Dictionary<string, int> m_MoveAnswers = new Dictionary<string, int>();
        private readonly Dictionary<string, int> m_AimAnswers = new Dictionary<string, int>();
        private readonly Dictionary<string, int> m_MoveResults = new Dictionary<string, int>();
        private readonly Dictionary<string, int> m_AimResults = new Dictionary<string, int>();
        private readonly Dictionary<string, Dictionary<string, int>> m_MoveByHp = new Dictionary<string, Dictionary<string, int>>();
        private int m_Decisions;
        private float m_MoveConfidenceSum;
        private int m_MoveConfidenceCount;
        private float m_AimConfidenceSum;
        private int m_AimConfidenceCount;

        public JevStats(float inputPriceUsdPerMillion)
        {
            m_StartTime = Time.time;
            m_InputPriceUsdPerMillion = inputPriceUsdPerMillion;
        }

        public int Successes => Requests - Failures;
        public float ElapsedSeconds => Time.time - m_StartTime;
        public float AverageLatencyMs => Successes > 0 ? TotalLatencyMs / Successes : 0f;
        public int TotalTokens => InputTokens + OutputTokens;
        public float AverageInputTokens => Successes > 0 ? (float)InputTokens / Successes : 0f;
        public float RequestsPerSecond => ElapsedSeconds > 1f ? Requests / ElapsedSeconds : 0f;
        /// <summary>Output tokens are free on TypeSafe's price list, so only input is billed.</summary>
        public float EstimatedCostUsd => InputTokens / 1_000_000f * m_InputPriceUsdPerMillion;

        public void RecordSuccess(float latencyMs, TypeSafeUsage usage)
        {
            TotalLatencyMs += latencyMs;
            LastLatencyMs = latencyMs;
            MaxLatencyMs = Mathf.Max(MaxLatencyMs, latencyMs);
            if (usage != null)
            {
                InputTokens += usage.input_tokens;
                OutputTokens += usage.output_tokens;
            }
        }

        public void RecordFailure()
        {
            Failures++;
        }

        /// <summary>Maps a move option id to a coarse category for distribution counting.</summary>
        public static string MoveCategory(string optionId)
        {
            if (string.IsNullOrEmpty(optionId))
            {
                return "missing";
            }
            if (optionId.StartsWith(MoveOption.StarPrefix))
            {
                return "star";
            }
            if (optionId.StartsWith(MoveOption.EnemyPrefix))
            {
                return "enemy";
            }
            if (optionId.StartsWith(MoveOption.TeammatePrefix))
            {
                return "teammate";
            }
            switch (optionId)
            {
                case MoveOption.AwayFromEnemies: return "away";
                default: return optionId; // home, hold, roam, sidestep
            }
        }

        public static string AimCategory(string optionId)
        {
            if (string.IsNullOrEmpty(optionId))
            {
                return "missing";
            }
            return optionId == AimOption.None ? "none" : "enemy";
        }

        /// <summary>Records one Jev decision. Pass null for an aim answer that was not asked.</summary>
        public void RecordDecision(
            string moveAnswer, string moveResult, double? moveConfidence,
            string aimAnswer, string aimResult, double? aimConfidence,
            string hpLevel)
        {
            m_Decisions++;
            string moveCategory = MoveCategory(moveAnswer);
            Bump(m_MoveAnswers, moveCategory);
            Bump(m_MoveResults, moveResult ?? "missing");
            if (moveConfidence.HasValue)
            {
                m_MoveConfidenceSum += (float)moveConfidence.Value;
                m_MoveConfidenceCount++;
            }
            if (!string.IsNullOrEmpty(hpLevel))
            {
                if (!m_MoveByHp.TryGetValue(hpLevel, out var byCategory))
                {
                    byCategory = new Dictionary<string, int>();
                    m_MoveByHp[hpLevel] = byCategory;
                }
                Bump(byCategory, moveCategory);
            }

            if (aimAnswer != null)
            {
                Bump(m_AimAnswers, AimCategory(aimAnswer));
                Bump(m_AimResults, aimResult ?? "missing");
                if (aimConfidence.HasValue)
                {
                    m_AimConfidenceSum += (float)aimConfidence.Value;
                    m_AimConfidenceCount++;
                }
            }
        }

        private static void Bump(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out var n);
            counts[key] = n + 1;
        }

        private static string Percentages(Dictionary<string, int> counts)
        {
            int total = counts.Values.Sum();
            if (total == 0)
            {
                return "-";
            }
            return string.Join("  ", counts.OrderByDescending(p => p.Value)
                .Select(p => $"{p.Key} {100f * p.Value / total:F0}%"));
        }

        /// <summary>Two or three lines with the decision split, for the gizmo label.</summary>
        public string DistributionText()
        {
            if (m_Decisions == 0)
            {
                return "decisions: none yet";
            }
            float moveConf = m_MoveConfidenceCount > 0 ? m_MoveConfidenceSum / m_MoveConfidenceCount : 0f;
            float aimConf = m_AimConfidenceCount > 0 ? m_AimConfidenceSum / m_AimConfidenceCount : 0f;
            var sb = new StringBuilder();
            sb.Append($"move ({m_Decisions}): ").Append(Percentages(m_MoveAnswers)).Append('\n');
            sb.Append("aim: ").Append(Percentages(m_AimAnswers)).Append('\n');
            sb.Append($"applied: ").Append(Percentages(m_MoveResults))
              .Append($"   conf move {moveConf:F2} aim {aimConf:F2}");
            return sb.ToString();
        }

        /// <summary>Full distribution incl. the HP breakdown, for the jsonl summary.</summary>
        public object DistributionLogObject()
        {
            return new
            {
                decisions = m_Decisions,
                move_answers = m_MoveAnswers,
                move_results = m_MoveResults,
                move_by_hp = m_MoveByHp,
                aim_answers = m_AimAnswers,
                aim_results = m_AimResults,
                avg_move_confidence = m_MoveConfidenceCount > 0 ? m_MoveConfidenceSum / m_MoveConfidenceCount : 0f,
                avg_aim_confidence = m_AimConfidenceCount > 0 ? m_AimConfidenceSum / m_AimConfidenceCount : 0f,
            };
        }

        /// <summary>Human-readable HP breakdown for the console summary, e.g. "medium: star 60% home 30% ...".</summary>
        public string MoveByHpText()
        {
            if (m_MoveByHp.Count == 0)
            {
                return "-";
            }
            string[] order = { "full", "high", "medium", "low", "critical" };
            var parts = new List<string>();
            foreach (var level in order.Concat(m_MoveByHp.Keys.Except(order)))
            {
                if (m_MoveByHp.TryGetValue(level, out var counts))
                {
                    parts.Add($"{level}[{counts.Values.Sum()}]: {Percentages(counts)}");
                }
            }
            return string.Join(" | ", parts);
        }

        /// <summary>Compact multi-line text for the Scene-view gizmo label.</summary>
        public string OverlayText(string title, string mode)
        {
            var sb = new StringBuilder();
            sb.Append(title).Append("  [").Append(mode).Append("]\n");
            sb.Append($"requests {Requests}  (urgent {UrgentRequests}, failed {Failures})  {RequestsPerSecond:F1}/s\n");
            sb.Append($"latency avg {AverageLatencyMs:F0} ms  last {LastLatencyMs:F0}  max {MaxLatencyMs:F0}\n");
            sb.Append($"tokens in {InputTokens:N0}  out {OutputTokens:N0}  avg {AverageInputTokens:F0}/req\n");
            sb.Append($"cost ~${EstimatedCostUsd:F4}   dodges: jev {Sidesteps}  reflex {ReflexDodges}");
            return sb.ToString();
        }

        /// <summary>One-line text for the Unity console at match end.</summary>
        public string SummaryText(string title, string mode, int score)
        {
            return $"[Jev] {title} summary: score={score}, requests={Requests} (urgent={UrgentRequests}, " +
                   $"failed={Failures}, {RequestsPerSecond:F2}/s over {ElapsedSeconds:F0}s), " +
                   $"latency avg={AverageLatencyMs:F0}ms max={MaxLatencyMs:F0}ms, " +
                   $"tokens in={InputTokens:N0} out={OutputTokens:N0} (avg {AverageInputTokens:F0} in/req), " +
                   $"cost~${EstimatedCostUsd:F4}, dodges jev={Sidesteps} reflex={ReflexDodges}, mode={mode}\n" +
                   $"  move answers: {Percentages(m_MoveAnswers)}\n" +
                   $"  aim answers: {Percentages(m_AimAnswers)}   applied: {Percentages(m_MoveResults)}\n" +
                   $"  move by HP: {MoveByHpText()}";
        }

        /// <summary>Structured record for the jsonl log.</summary>
        public object ToLogObject()
        {
            return new
            {
                requests = Requests,
                urgent_requests = UrgentRequests,
                failures = Failures,
                requests_per_second = RequestsPerSecond,
                elapsed_seconds = ElapsedSeconds,
                avg_latency_ms = AverageLatencyMs,
                max_latency_ms = MaxLatencyMs,
                input_tokens = InputTokens,
                output_tokens = OutputTokens,
                avg_input_tokens_per_request = AverageInputTokens,
                estimated_cost_usd = EstimatedCostUsd,
                sidesteps = Sidesteps,
                reflex_dodges = ReflexDodges,
            };
        }
    }
}
