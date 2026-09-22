using System.Collections.Generic;
using Main;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;

namespace Jev
{
    /// <summary>
    /// Tank driven by TypeSafe's Jev model. Tank script name: "Jev.MyTank".
    ///
    /// Architecture: code extracts semantic features (JevWorldDescriber), Jev answers two independent
    /// Choice questions per tick ("move_to" for the body, "aim_at" for the turret), and code executes
    /// them (path finding, turret tracking, firing). Movement and aiming are separate channels because
    /// the turret is independent from the body. If Jev is unreachable the tank falls back to a small
    /// rule-based AI that speaks the same option vocabulary, so the executor is shared.
    /// </summary>
    public class MyTank : Tank
    {
        private JevConfig m_Config;
        private TypeSafeClient m_Client;
        private JevWorldDescriber m_World;
        private JevLogger m_Logger;

        // decision loop
        private TypeSafeRequest m_Pending;
        private bool m_DiscardPending;
        private Dictionary<string, object> m_PendingState;
        private float m_NextDecisionTime;
        private int m_ConsecutiveFailures;
        private bool m_InFallback;
        private float m_FallbackProbeTime;
        private float m_RateLimitBackoff = 1f;
        private float m_NextFallbackThinkTime;

        // current decisions (option ids shared by Jev and the fallback)
        private string m_MoveChoice = MoveOption.Roam;
        private float m_MoveConfidence;
        private string m_AimChoice = AimOption.None;
        private float m_AimConfidence;
        private bool m_DecidedByJev;

        // execution
        private Vector3 m_IssuedMovePos;
        private bool m_HasIssuedMove;
        private float m_NextMoveIssueTime;
        private Vector3 m_HoldPos;
        private Vector3 m_RoamTarget;
        private bool m_HasRoamTarget;
        private float m_RoamRetargetTime;
        private Vector3 m_FleeTarget;
        private float m_FleeRetargetTime;
        private Vector3 m_AimPoint;

        // Jev-driven dodging
        private readonly HashSet<int> m_SeenThreatIds = new HashSet<int>();
        private bool m_ThreatArrivedWhilePending;
        private bool m_PendingUrgent;
        private string m_MoveChoiceBeforeSidestep;
        private Vector3 m_SidestepTarget;
        private float m_SidestepRetargetTime;

        // code-reflex dodging (DodgeMode.Reflex): overrides the destination without touching Jev's choice
        private bool m_ReflexDodging;
        private MissileThreat m_ReflexThreat;

        // shooting (after WBQ): tight angle at range, loose up close, trace the missile's real path
        private const float CloseRangeDistance = 15f;
        private const float FireAngleClose = 8f;
        private const float FireAngleFar = 3.5f;
        private const float FireAngleFarDistance = 45f;
        private const float FireCastRadius = 0.24f;

        // stats
        private int m_Ticks;
        private JevStats m_Stats;
        private bool m_SummaryPrinted;
#if UNITY_EDITOR
        private GUIStyle m_GizmoLabelStyle;
#endif

        public override string GetName()
        {
            return "Jev";
        }

        // ------------------------------------------------------------------ lifecycle

        protected override void OnStart()
        {
            base.OnStart();
            m_Config = JevConfig.Load();
            m_Stats = new JevStats(m_Config.InputTokenPriceUsdPerMillion);
            m_World = new JevWorldDescriber(this, m_Config);
            m_Logger = new JevLogger(GetName(), Team.ToString(), m_Config);
            m_Client = new TypeSafeClient(
                JevConfig.LoadApiKey(), m_Config.Endpoint, m_Config.Model, m_Config.RequestTimeout);
            m_InFallback = !m_Client.HasApiKey;
            m_NextDecisionTime = Time.time;
            m_Logger.Log(new
            {
                t = Time.time,
                kind = "start",
                team = Team.ToString(),
                model = m_Config.Model,
                has_api_key = m_Client.HasApiKey,
                decision_interval = m_Config.DecisionInterval,
                move_switch_confidence = m_Config.MoveSwitchConfidence,
                aim_switch_confidence = m_Config.AimSwitchConfidence,
                dodge_mode = m_Config.DodgeMode.ToString(),
                sidestep_switch_confidence = m_Config.SidestepSwitchConfidence,
                lead_aiming = m_Config.EnableLeadAiming,
            });
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (Match.instance.IsMathEnd())
            {
                PrintSummaryOnce();
                return;
            }
            m_World.UpdateThreats();
            UpdateReflexDodge();
            TickDecision();
            ExecuteMove();
            ExecuteAim();
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            // Whatever Jev answered for the pre-death situation is stale now.
            if (m_Pending != null)
            {
                m_DiscardPending = true;
            }
            m_MoveChoice = MoveOption.Roam;
            m_MoveConfidence = 0f;
            m_AimChoice = AimOption.None;
            m_AimConfidence = 0f;
            m_HasIssuedMove = false;
            m_HasRoamTarget = false;
            m_MoveChoiceBeforeSidestep = null;
            m_ThreatArrivedWhilePending = false;
            m_ReflexDodging = false;
            m_NextDecisionTime = Time.time;
            m_NextFallbackThinkTime = Time.time;
        }

        private void OnDestroy()
        {
            m_Pending?.Abort("tank destroyed");
            m_Pending = null;
            m_Logger?.Dispose();
        }

        // ------------------------------------------------------------------ decision loop

        private void TickDecision()
        {
            if (m_Pending != null)
            {
                if (m_Pending.Poll())
                {
                    var finished = m_Pending;
                    m_Pending = null;
                    if (m_DiscardPending)
                    {
                        m_DiscardPending = false;
                        m_NextDecisionTime = Time.time;
                    }
                    else
                    {
                        HandleResponse(finished);
                    }
                }
                else if (m_Pending.ElapsedSeconds > m_Config.RequestTimeout)
                {
                    m_Pending.Abort("timeout");
                    var timedOut = m_Pending;
                    m_Pending = null;
                    m_DiscardPending = false;
                    HandleFailure(timedOut);
                }
            }

            if (m_InFallback || !m_DecidedByJev)
            {
                FallbackDecide();
            }

            // A missile that will hit me is an event worth asking about right now, not at the next tick.
            bool urgent = m_Config.DodgeMode == EDodgeMode.Jev && DetectNewThreat();

            if (!m_Client.HasApiKey)
            {
                return;
            }
            if (m_Pending != null)
            {
                // The in-flight request did not see this missile; re-ask as soon as it returns.
                m_ThreatArrivedWhilePending |= urgent;
                return;
            }
            if (m_InFallback && Time.time < m_FallbackProbeTime)
            {
                return;
            }
            if (Time.time < m_NextDecisionTime && !urgent)
            {
                return;
            }
            SendDecisionRequest(urgent);
        }

        /// <summary>True the first frame a missile on a collision course with me shows up.</summary>
        private bool DetectNewThreat()
        {
            bool newThreat = false;
            foreach (var t in m_World.Threats)
            {
                if (t.WillHit && t.Missile != null && m_SeenThreatIds.Add(t.Missile.ID))
                {
                    newThreat = true;
                }
            }
            if (m_SeenThreatIds.Count > 512 && !m_World.HasThreat)
            {
                m_SeenThreatIds.Clear(); // missile ids only grow; forget the old ones once it is quiet
            }
            return newThreat;
        }

        private void SendDecisionRequest(bool urgent)
        {
            m_World.Snapshot();
            var state = m_World.BuildState(CurrentPlanText());
            var questions = new Dictionary<string, object>
            {
                { JevWorldDescriber.QuestionMoveTo,
                    TypeSafeQuestions.Choice(JevWorldDescriber.MoveInstructions, m_World.BuildMoveCriteria()) },
            };
            var aimCriteria = m_World.BuildAimCriteria();
            if (aimCriteria != null)
            {
                questions[JevWorldDescriber.QuestionAimAt] =
                    TypeSafeQuestions.Choice(JevWorldDescriber.AimInstructions, aimCriteria);
            }
            m_Pending = m_Client.Send(state, questions);
            m_PendingState = state;
            m_PendingUrgent = urgent;
            m_ThreatArrivedWhilePending = false;
            m_Ticks++;
            m_Stats.Requests++;
            if (urgent)
            {
                m_Stats.UrgentRequests++;
            }
        }

        private void HandleResponse(TypeSafeRequest request)
        {
            if (!request.Succeeded)
            {
                HandleFailure(request);
                return;
            }

            m_ConsecutiveFailures = 0;
            m_RateLimitBackoff = 1f;
            if (m_InFallback)
            {
                m_InFallback = false;
                Debug.Log($"[Jev] {GetName()} ({Team}) is back on Jev decisions.");
            }
            m_Stats.RecordSuccess(request.LatencyMs, request.Response.usage);

            var move = request.Response.GetAnswer(JevWorldDescriber.QuestionMoveTo);
            var aim = request.Response.GetAnswer(JevWorldDescriber.QuestionAimAt);
            string moveResult = ApplyMoveAnswer(move);
            string aimResult = ApplyAimAnswer(aim);
            m_DecidedByJev = true;
            m_NextDecisionTime = m_ThreatArrivedWhilePending ? Time.time : Time.time + m_Config.DecisionInterval;
            m_ThreatArrivedWhilePending = false;

            m_Logger.Log(new
            {
                t = Time.time,
                tick = m_Ticks,
                kind = "decision",
                team = Team.ToString(),
                urgent = m_PendingUrgent,
                threats = m_World.Threats.Count,
                latency_ms = request.LatencyMs,
                model = request.Response.model,
                usage = request.Response.usage,
                move = new
                {
                    answer = move?.choice,
                    confidence = move?.confidence,
                    probabilities = move?.probabilities,
                    result = moveResult,
                    current = m_MoveChoice,
                },
                aim = new
                {
                    answer = aim?.choice,
                    confidence = aim?.confidence,
                    probabilities = aim?.probabilities,
                    result = aimResult,
                    current = m_AimChoice,
                },
                hp = HP,
                score = Score,
                state = m_Config.LogState ? m_PendingState : null,
            });
            m_PendingState = null;
        }

        private void HandleFailure(TypeSafeRequest request)
        {
            m_Stats.RecordFailure();
            m_ConsecutiveFailures++;
            if (request.IsRateLimited)
            {
                m_NextDecisionTime = Time.time + m_RateLimitBackoff;
                m_RateLimitBackoff = Mathf.Min(8f, m_RateLimitBackoff * 2f);
            }
            else
            {
                m_NextDecisionTime = Time.time + m_Config.DecisionInterval;
            }
            if (m_InFallback)
            {
                m_FallbackProbeTime = Time.time + m_Config.FallbackRetryInterval;
            }
            else if (m_ConsecutiveFailures >= m_Config.FallbackAfterFailures)
            {
                m_InFallback = true;
                m_FallbackProbeTime = Time.time + m_Config.FallbackRetryInterval;
                Debug.LogWarning(
                    $"[Jev] {GetName()} ({Team}) switching to rule-based fallback after " +
                    $"{m_ConsecutiveFailures} failures. Last error: {request.Error}");
            }
            else
            {
                Debug.LogWarning($"[Jev] request failed: {request.Error}");
            }
            m_Logger.Log(new
            {
                t = Time.time,
                tick = m_Ticks,
                kind = "failure",
                team = Team.ToString(),
                latency_ms = request.LatencyMs,
                http = request.HttpStatus,
                error = request.Error,
                in_fallback = m_InFallback,
            });
            m_PendingState = null;
        }

        /// <summary>Applies the move answer with hysteresis; returns a short tag describing what happened.</summary>
        private string ApplyMoveAnswer(TypeSafeAnswer answer)
        {
            if (answer == null || string.IsNullOrEmpty(answer.choice))
            {
                return "missing";
            }
            if (!m_World.IsMoveOptionValid(answer.choice))
            {
                return "invalid";
            }
            float confidence = answer.Confidence;
            if (answer.choice == m_MoveChoice)
            {
                m_MoveConfidence = confidence;
                return "same";
            }
            bool intoSidestep = answer.choice == MoveOption.Sidestep;
            float bar = intoSidestep ? m_Config.SidestepSwitchConfidence : m_Config.MoveSwitchConfidence;
            if (confidence < bar && m_World.IsMoveOptionValid(m_MoveChoice))
            {
                return "kept_low_confidence";
            }
            if (intoSidestep)
            {
                // Remember where I was going so I can resume once the missile has passed.
                m_MoveChoiceBeforeSidestep = m_MoveChoice;
                m_Stats.Sidesteps++;
            }
            else
            {
                m_MoveChoiceBeforeSidestep = null;
            }
            SetMoveChoice(answer.choice, confidence);
            return "switched";
        }

        private string ApplyAimAnswer(TypeSafeAnswer answer)
        {
            if (answer == null || string.IsNullOrEmpty(answer.choice))
            {
                // The aim question is skipped when no enemy is alive.
                if (m_AimChoice != AimOption.None)
                {
                    m_AimChoice = AimOption.None;
                    m_AimConfidence = 0f;
                }
                return "missing";
            }
            if (!m_World.IsAimOptionValid(answer.choice))
            {
                return "invalid";
            }
            float confidence = answer.Confidence;
            if (answer.choice == m_AimChoice)
            {
                m_AimConfidence = confidence;
                return "same";
            }
            if (confidence < m_Config.AimSwitchConfidence && m_World.IsAimOptionValid(m_AimChoice))
            {
                return "kept_low_confidence";
            }
            m_AimChoice = answer.choice;
            m_AimConfidence = confidence;
            return "switched";
        }

        private void SetMoveChoice(string choice, float confidence)
        {
            m_MoveChoice = choice;
            m_MoveConfidence = confidence;
            m_HasIssuedMove = false;
            m_HoldPos = Position;
            m_FleeRetargetTime = 0f;
            m_SidestepRetargetTime = 0f;
            m_HasRoamTarget = false;
        }

        private string CurrentPlanText()
        {
            string aim = m_AimChoice == AimOption.None ? "not tracking any enemy" : $"tracking {m_AimChoice}";
            if (m_MoveChoice == MoveOption.Sidestep)
            {
                string resume = string.IsNullOrEmpty(m_MoveChoiceBeforeSidestep) ? "" : $", was heading to {m_MoveChoiceBeforeSidestep}";
                return $"sidestepping to dodge a missile{resume}, {aim}";
            }
            return $"driving to {m_MoveChoice}, {aim}";
        }

        // ------------------------------------------------------------------ fallback (no key / API down)

        private void FallbackDecide()
        {
            if (Time.time < m_NextFallbackThinkTime)
            {
                return;
            }
            m_NextFallbackThinkTime = Time.time + 0.2f;
            m_World.Snapshot();

            string move;
            if (HP < Match.instance.GlobalSetting.MaxHP * 0.3f)
            {
                move = MoveOption.Home;
            }
            else
            {
                move = m_World.NearestStarId() ?? MoveOption.Roam;
            }
            if (move != m_MoveChoice)
            {
                SetMoveChoice(move, 0f);
            }
            m_AimChoice = m_World.NearestVisibleEnemyId() ?? AimOption.None;
            m_AimConfidence = 0f;
            m_DecidedByJev = false;
        }

        // ------------------------------------------------------------------ execution: body

        /// <summary>
        /// Code-only dodge (after WBQ's EvadeMissile): sidestep while a missile that will hit me is inside
        /// the dodge window. Too far away and it is not worth reacting yet; too close and it is too late,
        /// turning the chassis would only expose the flank. Runs for Jev and fallback decisions alike and
        /// leaves Jev's move choice untouched, so the destination resumes the moment the threat is gone.
        /// </summary>
        private void UpdateReflexDodge()
        {
            bool dodge = false;
            if (m_Config.DodgeMode == EDodgeMode.Reflex && m_World.HasThreat && !ReflexDodgeIsBadTrade())
            {
                foreach (var t in m_World.Threats) // most urgent first
                {
                    if (!t.WillHit)
                    {
                        continue;
                    }
                    if (t.Distance > m_Config.ReflexDodgeMaxDistance || t.Distance < m_Config.ReflexDodgeMinDistance)
                    {
                        continue;
                    }
                    m_ReflexThreat = t;
                    dodge = true;
                    break;
                }
            }
            if (dodge && !m_ReflexDodging)
            {
                m_Stats.ReflexDodges++;
            }
            if (!dodge && m_ReflexDodging)
            {
                m_HasIssuedMove = false; // force re-issuing the real destination
            }
            m_ReflexDodging = dodge;
        }

        /// <summary>WBQ's trade rule: if the enemy dies to my next shot and I can take two, stand and shoot.</summary>
        private bool ReflexDodgeIsBadTrade()
        {
            if (!m_Config.ReflexDodgeSkipForKill)
            {
                return false;
            }
            var enemy = m_World.ResolveEnemy(m_AimChoice);
            int damage = Match.instance.GlobalSetting.DamagePerHit;
            return enemy != null && !enemy.IsDead && enemy.HP <= damage && HP >= 2 * damage;
        }

        private void ExecuteMove()
        {
            if (m_ReflexDodging)
            {
                IssueMove(ComputeSidestepTarget(m_ReflexThreat));
                return;
            }

            if (!m_World.IsMoveOptionValid(m_MoveChoice))
            {
                // Target vanished (star taken, tank died, missile passed): pick something sensible
                // and ask Jev again asap. After a sidestep, resume the interrupted destination.
                m_World.Snapshot();
                string next = null;
                if (m_MoveChoice == MoveOption.Sidestep && m_World.IsMoveOptionValid(m_MoveChoiceBeforeSidestep))
                {
                    next = m_MoveChoiceBeforeSidestep;
                }
                m_MoveChoiceBeforeSidestep = null;
                SetMoveChoice(next ?? m_World.NearestStarId() ?? MoveOption.Roam, 0f);
                m_NextDecisionTime = Time.time;
            }

            switch (m_MoveChoice)
            {
                case MoveOption.Sidestep:
                    if (Time.time >= m_SidestepRetargetTime && m_World.HasThreat)
                    {
                        m_SidestepTarget = ComputeSidestepTarget(m_World.MostUrgentThreat);
                        m_SidestepRetargetTime = Time.time + 0.3f;
                    }
                    IssueMove(m_SidestepTarget);
                    return;
                case MoveOption.Home:
                    IssueMove(Match.instance.GetRebornPos(Team));
                    return;
                case MoveOption.Hold:
                    IssueMove(m_HoldPos);
                    return;
                case MoveOption.AwayFromEnemies:
                    if (Time.time >= m_FleeRetargetTime)
                    {
                        m_FleeTarget = ComputeFleeTarget();
                        m_FleeRetargetTime = Time.time + 1f;
                    }
                    IssueMove(m_FleeTarget);
                    return;
                case MoveOption.Roam:
                    if (!m_HasRoamTarget || Time.time >= m_RoamRetargetTime ||
                        (m_RoamTarget - Position).sqrMagnitude < 9f)
                    {
                        m_RoamTarget = RandomFieldPoint();
                        m_HasRoamTarget = true;
                        m_RoamRetargetTime = Time.time + 6f;
                    }
                    IssueMove(m_RoamTarget);
                    return;
            }

            var star = m_World.ResolveStar(m_MoveChoice);
            if (star != null)
            {
                IssueMove(star.Position);
                return;
            }
            var enemy = m_World.ResolveEnemy(m_MoveChoice);
            if (enemy != null)
            {
                IssueMove(enemy.Position);
                return;
            }
            var teammate = m_World.ResolveTeammate(m_MoveChoice);
            if (teammate != null)
            {
                IssueMove(teammate.Position);
            }
        }

        /// <summary>Re-plans only when the destination moved noticeably or on a slow timer.</summary>
        private void IssueMove(Vector3 target)
        {
            if (m_HasIssuedMove &&
                (target - m_IssuedMovePos).sqrMagnitude < 1f &&
                Time.time < m_NextMoveIssueTime)
            {
                return;
            }
            Move(target);
            m_IssuedMovePos = target;
            m_HasIssuedMove = true;
            m_NextMoveIssueTime = Time.time + 0.5f;
        }

        private Vector3 ComputeFleeTarget()
        {
            var away = Vector3.zero;
            foreach (var e in m_World.AliveEnemies)
            {
                var d = Position - e.Position;
                d.y = 0;
                float dist = d.magnitude;
                if (dist > 0.1f)
                {
                    away += d / (dist * dist); // closer enemies push harder
                }
            }
            if (away.sqrMagnitude < 1e-6f)
            {
                away = -Forward;
            }
            away.y = 0;
            away.Normalize();

            float fleeDistance = Match.instance.FieldSize * 0.25f;
            float[] angles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
            foreach (var angle in angles)
            {
                var dir = Quaternion.Euler(0f, angle, 0f) * away;
                var candidate = ClampToField(Position + dir * fleeDistance);
                if (NavMesh.SamplePosition(candidate, out var hit, 8f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            return Match.instance.GetRebornPos(Team);
        }

        /// <summary>
        /// A point perpendicular to the most urgent missile's path, on the side I am already offset to,
        /// so the dodge moves me further away from the path rather than across it.
        /// </summary>
        private Vector3 ComputeSidestepTarget(MissileThreat threat)
        {
            if (threat.Missile == null)
            {
                return Position;
            }
            var dir = threat.Direction;
            var perpendicular = Vector3.Cross(Vector3.up, dir).normalized;
            var rel = Position - threat.Missile.Position;
            rel.y = 0;
            var offset = rel - dir * Vector3.Dot(rel, dir);
            float side = Vector3.Dot(offset, perpendicular) >= 0f ? 1f : -1f;

            float[] sides = { side, -side };
            foreach (var s in sides)
            {
                var candidate = ClampToField(Position + perpendicular * (s * m_Config.SidestepDistance));
                if (NavMesh.SamplePosition(candidate, out var hit, 4f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            // No walkable point either way; still try the raw point rather than standing in the path.
            return ClampToField(Position + perpendicular * (side * m_Config.SidestepDistance));
        }

        private Vector3 RandomFieldPoint()
        {
            float half = Match.instance.FieldSize * 0.5f - 5f;
            for (int i = 0; i < 8; ++i)
            {
                var candidate = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
                if (NavMesh.SamplePosition(candidate, out var hit, 8f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            return Position;
        }

        private static Vector3 ClampToField(Vector3 p)
        {
            float half = Match.instance.FieldSize * 0.5f - 3f;
            p.x = Mathf.Clamp(p.x, -half, half);
            p.z = Mathf.Clamp(p.z, -half, half);
            return p;
        }

        // ------------------------------------------------------------------ execution: turret

        private void ExecuteAim()
        {
            var enemy = m_World.ResolveEnemy(m_AimChoice);
            if (enemy == null || enemy.IsDead)
            {
                if (m_AimChoice != AimOption.None)
                {
                    m_AimChoice = AimOption.None;
                    m_AimConfidence = 0f;
                }
                TurretTurnTo(Position + Forward);
                return;
            }
            var aimPoint = m_Config.EnableLeadAiming ? PredictInterceptPoint(enemy) : enemy.Position;
            m_AimPoint = aimPoint;
            TurretTurnTo(aimPoint);
            if (ShouldFire(aimPoint))
            {
                Fire();
            }
        }

        /// <summary>
        /// Closed-form intercept (after WBQ): solve |delta + v t| = s t for the earliest positive t,
        /// where delta is the enemy offset, v its velocity and s the missile speed.
        /// </summary>
        private Vector3 PredictInterceptPoint(Tank enemy)
        {
            float s = Match.instance.GlobalSetting.MissileSpeed;
            var v = enemy.Velocity;
            v.y = 0;
            var delta = enemy.Position - FirePos;
            delta.y = 0;
            if (s <= 0.01f || v.sqrMagnitude < 0.01f)
            {
                return enemy.Position;
            }

            float a = Vector3.Dot(v, v) - s * s;
            float b = 2f * Vector3.Dot(delta, v);
            float c = delta.sqrMagnitude;
            float t;
            if (Mathf.Abs(a) < 1e-4f)
            {
                // Target as fast as the missile: the equation degenerates to linear.
                if (Mathf.Abs(b) < 1e-4f)
                {
                    return enemy.Position;
                }
                t = -c / b;
            }
            else
            {
                float discriminant = b * b - 4f * a * c;
                if (discriminant < 0f)
                {
                    return enemy.Position; // cannot catch it, aim at where it is
                }
                float root = Mathf.Sqrt(discriminant);
                float t1 = (-b - root) / (2f * a);
                float t2 = (-b + root) / (2f * a);
                t = Mathf.Min(t1, t2);
                if (t <= 0f)
                {
                    t = Mathf.Max(t1, t2);
                }
            }
            if (t <= 0f)
            {
                return enemy.Position;
            }
            t = Mathf.Min(t, m_Config.MaxLeadSeconds);
            return enemy.Position + v * t;
        }

        /// <summary>
        /// Fire gate after WBQ: at point blank any ready shot goes; further out the allowed aiming error
        /// tightens with distance, and a sphere cast along the actual muzzle direction must not end in a
        /// wall or a teammate before reaching the target.
        /// </summary>
        private bool ShouldFire(Vector3 aimPoint)
        {
            if (!CanFire())
            {
                return false;
            }
            var toAim = aimPoint - FirePos;
            toAim.y = 0;
            float distance = toAim.magnitude;
            if (distance < CloseRangeDistance)
            {
                return true;
            }

            var aiming = TurretAiming;
            aiming.y = 0;
            float maxAngle = Mathf.Lerp(FireAngleClose, FireAngleFar,
                Mathf.InverseLerp(CloseRangeDistance, FireAngleFarDistance, distance));
            if (Vector3.Angle(aiming, toAim) > maxAngle)
            {
                return false;
            }

            float castDistance = Mathf.Max(0.1f, distance - 2f);
            if (Physics.SphereCast(FirePos, FireCastRadius, TurretAiming, out var hit, castDistance,
                    PhysicsUtils.LayerMaskCollsion, QueryTriggerInteraction.Ignore))
            {
                var fireCollider = hit.collider.GetComponent<FireCollider>();
                if (fireCollider == null || fireCollider.Owner == null)
                {
                    return false; // scene geometry in the way
                }
                // My own collider is not an obstacle; an enemy body in the path is exactly what we want.
                return fireCollider.Owner == this || fireCollider.Owner.Team != Team;
            }
            return true;
        }

        // ------------------------------------------------------------------ diagnostics

        private void PrintSummaryOnce()
        {
            if (m_SummaryPrinted)
            {
                return;
            }
            m_SummaryPrinted = true;
            Debug.Log(m_Stats.SummaryText($"{GetName()} ({Team})", ModeText(), Score));
            m_Logger.Log(new
            {
                t = Time.time,
                kind = "summary",
                team = Team.ToString(),
                score = Score,
                winner = Match.instance.WinnerTeam.ToString(),
                dodge_mode = m_Config.DodgeMode.ToString(),
                in_fallback = m_InFallback,
                stats = m_Stats.ToLogObject(),
            });
            m_Logger.Dispose();
        }

        private string ModeText()
        {
            string source = !m_Client.HasApiKey ? "NO KEY" : m_InFallback ? "FALLBACK" : "JEV";
            return $"{source}, dodge {m_Config.DodgeMode}";
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            if (m_World == null)
            {
                return;
            }
            if (m_HasIssuedMove)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(m_IssuedMovePos, 1.5f);
                Gizmos.DrawLine(Position, m_IssuedMovePos);
            }
            var enemy = m_World.ResolveEnemy(m_AimChoice);
            if (enemy != null && !enemy.IsDead)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(FirePos, m_AimPoint);
                Gizmos.DrawWireSphere(m_AimPoint, 0.8f);
                if ((m_AimPoint - enemy.Position).sqrMagnitude > 0.25f)
                {
                    Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.6f);
                    Gizmos.DrawLine(enemy.Position, m_AimPoint);
                }
            }
            foreach (var threat in m_World.Threats)
            {
                if (threat.Missile == null)
                {
                    continue;
                }
                Gizmos.color = threat.WillHit ? Color.magenta : new Color(1f, 0.5f, 1f, 0.5f);
                float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
                Gizmos.DrawLine(threat.Missile.Position, threat.Missile.Position + threat.Direction * (threat.Eta * missileSpeed));
            }
#if UNITY_EDITOR
            string source = m_InFallback || !m_DecidedByJev ? "FALLBACK" : "JEV";
            if (m_ReflexDodging)
            {
                source += " +REFLEX DODGE";
            }
            string pending = m_Pending != null ? " ..." : "";
            if (m_GizmoLabelStyle == null)
            {
                m_GizmoLabelStyle = new GUIStyle
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                };
                m_GizmoLabelStyle.normal.textColor = new Color(1f, 0.92f, 0f);
            }
            string label = $"{source}{pending}\nmove: {m_MoveChoice} ({m_MoveConfidence:F2})\naim: {m_AimChoice} ({m_AimConfidence:F2})";
            if (m_Stats != null && m_Config.ShowStatsInGizmo)
            {
                label += "\n" + m_Stats.OverlayText($"{GetName()} ({Team})", ModeText());
            }
            Handles.Label(Position + Vector3.up * 4f, label, m_GizmoLabelStyle);
#endif
        }
    }
}
