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
                   $"cost~${EstimatedCostUsd:F4}, dodges jev={Sidesteps} reflex={ReflexDodges}, mode={mode}";
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
