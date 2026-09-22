using System.Collections.Generic;
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace Jev
{
    /// <summary>Option ids offered to Jev for the "move_to" question.</summary>
    public static class MoveOption
    {
        public const string Home = "home";
        public const string AwayFromEnemies = "away_from_enemies";
        public const string Hold = "hold";
        public const string Roam = "roam";
        /// <summary>Only offered while a missile is about to hit me.</summary>
        public const string Sidestep = "sidestep";
        public const string StarPrefix = "star_";
        public const string EnemyPrefix = "enemy_";
        public const string TeammatePrefix = "teammate_";
    }

    /// <summary>Option ids offered to Jev for the "aim_at" question (plus the enemy ids).</summary>
    public static class AimOption
    {
        public const string None = "none";
    }

    /// <summary>An enemy missile that will pass close to me within the threat window.</summary>
    public struct MissileThreat
    {
        public Missile Missile;
        /// <summary>Horizontal, normalized travel direction of the missile.</summary>
        public Vector3 Direction;
        /// <summary>Seconds until the missile reaches my position along its path.</summary>
        public float Eta;
        /// <summary>Distance the missile still has to travel to reach my position along its path.</summary>
        public float Distance;
        /// <summary>Closest distance between the missile's path and my centre.</summary>
        public float MissDistance;
        /// <summary>True when a physics cast along the path hits my own collider before anything else.</summary>
        public bool WillHit;
    }

    /// <summary>
    /// Translates the numeric battlefield into the semantic, English-only description that Jev is
    /// good at judging (it is explicitly weak at arithmetic, counting and comparing numbers), and
    /// builds the dynamic option lists for the two questions. It also resolves option ids back to
    /// live game objects so the executor and the fallback AI can share one vocabulary.
    /// </summary>
    public class JevWorldDescriber
    {
        public const string QuestionMoveTo = "move_to";
        public const string QuestionAimAt = "aim_at";

        /// <summary>Sphere radius used to trace a missile's path; generous so grazing hits count.</summary>
        public const float MissileCastRadius = 1f;

        public const string GameRules =
            "Arena tank game. Score by collecting stars (small points) and by killing enemy tanks (more points). " +
            "A super star is worth much more than a normal star. When I die the killer scores and I respawn at home " +
            "after a delay, losing time. Standing inside my home zone slowly recovers HP. The turret aims and fires " +
            "independently from the body, so I can drive to one place while shooting at another. Missiles only hit " +
            "when there is a clear line of sight. A missile hit takes away a big chunk of HP; missiles fly in a straight " +
            "line, so a quick sideways move can dodge one that is about to hit me. The match is won by the team with " +
            "the higher score when time runs out.";

        public const string MoveInstructions =
            "I am the tank described in `self`. Considering `enemies`, `stars`, `teammates`, `threats`, `match` " +
            "and my `current_plan`, which destination should my tank body drive to right now to maximize my team's " +
            "final score while staying alive? Only the movement is decided here; aiming is decided separately.";

        public const string AimInstructions =
            "I am the tank described in `self`. Which enemy should my turret aim at right now? Prefer enemies I can " +
            "see (clear line of fire), that are close, nearly dead, or currently aiming at me. Choose `none` only if " +
            "no enemy is worth tracking.";

        private readonly Tank m_Self;
        private readonly JevConfig m_Config;

        private readonly Dictionary<Tank, string> m_EnemyIds = new Dictionary<Tank, string>();
        private readonly Dictionary<string, Tank> m_EnemiesById = new Dictionary<string, Tank>();
        private readonly Dictionary<Tank, string> m_TeammateIds = new Dictionary<Tank, string>();
        private readonly Dictionary<string, Tank> m_TeammatesById = new Dictionary<string, Tank>();

        private readonly List<Tank> m_AllEnemies = new List<Tank>();
        private readonly List<Tank> m_AliveEnemies = new List<Tank>();
        private readonly List<Tank> m_AliveTeammates = new List<Tank>();
        private readonly List<Star> m_Stars = new List<Star>();
        private readonly Dictionary<int, Missile> m_EnemyMissiles = new Dictionary<int, Missile>();
        private readonly List<MissileThreat> m_Threats = new List<MissileThreat>();

        // travel distances from me, refreshed by Snapshot (path length when UsePathDistances, else straight line)
        private readonly Dictionary<Star, float> m_StarDistances = new Dictionary<Star, float>();
        private readonly Dictionary<Tank, float> m_TankDistances = new Dictionary<Tank, float>();
        private float m_HomeDistance;
        private readonly NavMeshPath m_PathCache = new NavMeshPath();

        public IReadOnlyList<Tank> AliveEnemies => m_AliveEnemies;
        public IReadOnlyList<Tank> AliveTeammates => m_AliveTeammates;
        public IReadOnlyList<Star> Stars => m_Stars;
        /// <summary>Threats from the last <see cref="UpdateThreats"/>, most urgent first.</summary>
        public IReadOnlyList<MissileThreat> Threats => m_Threats;
        public bool HasThreat => m_Threats.Count > 0;
        public MissileThreat MostUrgentThreat => m_Threats[0];

        public JevWorldDescriber(Tank self, JevConfig config)
        {
            m_Self = self;
            m_Config = config;
        }

        // ------------------------------------------------------------------ snapshot

        /// <summary>Refreshes the cached view of the world. Call before building state or criteria.</summary>
        public void Snapshot()
        {
            var match = Match.instance;

            match.GetOppositeTanks(m_Self.Team, m_AllEnemies);
            m_AliveEnemies.Clear();
            foreach (var e in m_AllEnemies)
            {
                if (e == null)
                {
                    continue;
                }
                if (!m_EnemyIds.ContainsKey(e))
                {
                    var id = MoveOption.EnemyPrefix + (m_EnemyIds.Count + 1);
                    m_EnemyIds[e] = id;
                    m_EnemiesById[id] = e;
                }
                if (!e.IsDead)
                {
                    m_AliveEnemies.Add(e);
                }
            }

            m_AliveTeammates.Clear();
            var teammates = match.GetTanks(m_Self.Team);
            if (teammates != null)
            {
                foreach (var t in teammates)
                {
                    if (t == null || t == m_Self)
                    {
                        continue;
                    }
                    if (!m_TeammateIds.ContainsKey(t))
                    {
                        var id = MoveOption.TeammatePrefix + (m_TeammateIds.Count + 1);
                        m_TeammateIds[t] = id;
                        m_TeammatesById[id] = t;
                    }
                    if (!t.IsDead)
                    {
                        m_AliveTeammates.Add(t);
                    }
                }
            }

            m_Stars.Clear();
            foreach (var pair in match.GetStars())
            {
                if (pair.Value != null)
                {
                    m_Stars.Add(pair.Value);
                }
            }

            // Travel distances from me: what actually matters for "is it worth driving there".
            var selfPos = m_Self.Position;
            m_StarDistances.Clear();
            foreach (var s in m_Stars)
            {
                m_StarDistances[s] = TravelDistance(selfPos, s.Position);
            }
            m_TankDistances.Clear();
            foreach (var e in m_AliveEnemies)
            {
                m_TankDistances[e] = TravelDistance(selfPos, e.Position);
            }
            foreach (var t in m_AliveTeammates)
            {
                m_TankDistances[t] = TravelDistance(selfPos, t.Position);
            }
            m_HomeDistance = TravelDistance(selfPos, match.GetRebornPos(m_Self.Team));
            m_Stars.Sort((a, b) => m_StarDistances[a].CompareTo(m_StarDistances[b]));

            UpdateThreats();
        }

        /// <summary>Distance I would actually drive from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public float TravelDistance(Vector3 from, Vector3 to)
        {
            float straight = Vector3.Distance(from, to);
            if (!m_Config.UsePathDistances)
            {
                return straight;
            }
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, m_PathCache) ||
                m_PathCache.status == NavMeshPathStatus.PathInvalid)
            {
                return straight * 2f; // unreachable: make it look far rather than lying about it
            }
            var corners = m_PathCache.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; ++i)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            if (m_PathCache.status == NavMeshPathStatus.PathPartial && corners.Length > 0)
            {
                length += Vector3.Distance(corners[corners.Length - 1], to);
            }
            return Mathf.Max(length, straight);
        }

        /// <summary>Travel distance from me to a star, from the last snapshot.</summary>
        public float DistanceTo(Star s)
        {
            return m_StarDistances.TryGetValue(s, out var d) ? d : Vector3.Distance(m_Self.Position, s.Position);
        }

        /// <summary>Travel distance from me to a tank, from the last snapshot.</summary>
        public float DistanceTo(Tank t)
        {
            return m_TankDistances.TryGetValue(t, out var d) ? d : Vector3.Distance(m_Self.Position, t.Position);
        }

        /// <summary>
        /// Cheap enough to run every frame: finds enemy missiles whose straight path passes close to me
        /// within the threat window. Geometry pre-filters; a sphere cast along the missile's real path then
        /// decides what it hits first, so a missile that ends in a wall or in another tank is not a threat.
        /// Also called by <see cref="Snapshot"/>.
        /// </summary>
        public void UpdateThreats()
        {
            Match.instance.GetOppositeMissilesEx(m_Self.Team, m_EnemyMissiles);
            m_Threats.Clear();
            var selfPos = m_Self.Position;
            foreach (var pair in m_EnemyMissiles)
            {
                var m = pair.Value;
                if (m == null)
                {
                    continue;
                }
                var v = m.Velocity;
                v.y = 0;
                float speed = v.magnitude;
                if (speed < 0.01f)
                {
                    continue;
                }
                var dir = v / speed;
                var rel = selfPos - m.Position;
                rel.y = 0;
                float along = Vector3.Dot(rel, dir);
                if (along <= 0f)
                {
                    continue; // already flew past me
                }
                float eta = along / speed;
                if (eta > m_Config.ThreatEtaSeconds)
                {
                    continue;
                }
                float miss = (rel - dir * along).magnitude;
                if (miss > m_Config.ThreatNearMissRadius)
                {
                    continue;
                }

                bool willHit = false;
                float castDistance = along + m_Config.ThreatNearMissRadius;
                if (Physics.SphereCast(m.Position, MissileCastRadius, m.Velocity.normalized, out var hit,
                        castDistance, PhysicsUtils.LayerMaskCollsion, QueryTriggerInteraction.Ignore))
                {
                    var fireCollider = hit.collider.GetComponent<FireCollider>();
                    if (fireCollider == null || fireCollider.Owner == null)
                    {
                        continue; // scene geometry absorbs it before it gets to me
                    }
                    if (fireCollider.Owner != m_Self)
                    {
                        continue; // another tank is in the way
                    }
                    willHit = true;
                }
                m_Threats.Add(new MissileThreat
                {
                    Missile = m,
                    Direction = dir,
                    Eta = eta,
                    Distance = along,
                    MissDistance = miss,
                    WillHit = willHit,
                });
            }
            m_Threats.Sort((a, b) => a.Eta.CompareTo(b.Eta));
        }

        // ------------------------------------------------------------------ state

        public Dictionary<string, object> BuildState(string currentPlan)
        {
            var match = Match.instance;
            var selfPos = m_Self.Position;

            var enemies = new List<object>();
            foreach (var e in m_AllEnemies)
            {
                if (e == null)
                {
                    continue;
                }
                var id = m_EnemyIds[e];
                if (e.IsDead)
                {
                    enemies.Add(new Dictionary<string, object>
                    {
                        { "id", id },
                        { "status", "dead, will respawn at its home later" },
                    });
                    continue;
                }
                enemies.Add(new Dictionary<string, object>
                {
                    { "id", id },
                    { "status", "alive" },
                    { "line_of_fire", m_Self.CanSeeOthers(e) ? "visible, I have a clear shot" : "not visible, blocked by walls" },
                    { "distance", DistanceBucket(DistanceTo(e)) },
                    { "hp", HpBucket(e.HP) },
                    { "turret", IsAimingAt(e, selfPos) ? "aiming its turret at me" : "not aiming at me" },
                    { "movement", MovementRelativeTo(e, selfPos) },
                    { "location", IsInHomeZone(e) ? "recovering inside its own home zone" : "out in the field" },
                });
            }

            var stars = new List<object>();
            foreach (var s in m_Stars)
            {
                stars.Add(new Dictionary<string, object>
                {
                    { "id", StarId(s) },
                    { "kind", s.IsSuperStar ? "super star, worth a lot of points" : "normal star" },
                    { "distance", DistanceBucket(DistanceTo(s)) },
                    { "competition", StarCompetition(s) },
                });
            }

            var teammates = new List<object>();
            foreach (var t in m_AliveTeammates)
            {
                bool askedForHelp = m_Self.TeamStrategy == (int)ETeamStrategy.Help;
                teammates.Add(new Dictionary<string, object>
                {
                    { "id", m_TeammateIds[t] },
                    { "hp", HpBucket(t.HP) },
                    { "distance", DistanceBucket(DistanceTo(t)) },
                    { "needs_help", askedForHelp && t.HP < match.GlobalSetting.MaxHP * 0.5f
                        ? "asked for help and is hurt" : "does not need help" },
                });
            }

            var self = new Dictionary<string, object>
            {
                { "hp", HpBucket(m_Self.HP) },
                { "location", SelfLocation() },
                { "gun", m_Self.CanFire() ? "ready to fire" : "reloading" },
                { "score", ScoreStatus() },
                { "current_plan", string.IsNullOrEmpty(currentPlan) ? "no plan yet" : currentPlan },
            };

            var matchInfo = new Dictionary<string, object>
            {
                { "time", TimeBucket() },
                { "super_star", SuperStarStatus() },
                { "enemies_alive", CountWords(m_AliveEnemies.Count, "enemy tank", "enemy tanks") },
                { "teammates_alive", m_AliveTeammates.Count == 0 ? "I fight alone" : CountWords(m_AliveTeammates.Count, "teammate", "teammates") },
            };

            return new Dictionary<string, object>
            {
                { "rules", GameRules },
                { "self", self },
                { "enemies", enemies },
                { "stars", stars.Count == 0 ? (object)"no stars on the field right now" : stars },
                { "teammates", teammates.Count == 0 ? (object)"no teammates alive" : teammates },
                { "threats", DescribeThreats() },
                { "match", matchInfo },
            };
        }

        // ------------------------------------------------------------------ criteria

        public Dictionary<string, object> BuildMoveCriteria()
        {
            var criteria = new Dictionary<string, object>();
            foreach (var s in m_Stars)
            {
                var id = StarId(s);
                criteria[id] = $"Drive to {id} (see `stars`) and collect it" +
                               (s.IsSuperStar ? "; it is the super star." : ".");
            }
            foreach (var e in m_AliveEnemies)
            {
                var id = m_EnemyIds[e];
                criteria[id] = $"Drive toward {id} (see `enemies`) to get a clear line of fire and hunt it down.";
            }
            foreach (var t in m_AliveTeammates)
            {
                var id = m_TeammateIds[t];
                criteria[id] = $"Drive to {id} (see `teammates`) to support them or fight together.";
            }
            criteria[MoveOption.AwayFromEnemies] =
                "Move away from all enemies without going home; use when hurt or outgunned but there is no time to reach home.";
            criteria[MoveOption.Home] =
                "Return to my home zone to recover HP; use when HP is low and dying would hand the enemy points.";
            criteria[MoveOption.Hold] =
                "Stay where I am; use when the current spot is good, for example recovering inside the home zone or " +
                "an enemy is driving into my line of fire.";
            criteria[MoveOption.Roam] =
                "Patrol to a random spot on the field looking for new stars; use only when nothing better is available.";
            if (m_Config.DodgeMode == EDodgeMode.Jev && HasThreat)
            {
                criteria[MoveOption.Sidestep] =
                    "Dodge: dart sideways, perpendicular to the incoming missile's path, to avoid being hit. " +
                    "Pick this while `threats` says a missile is on a direct course to hit me; I can return to my " +
                    "previous destination right after.";
            }
            return criteria;
        }

        /// <summary>Returns null when no enemy is alive (a one-option choice is pointless).</summary>
        public Dictionary<string, object> BuildAimCriteria()
        {
            if (m_AliveEnemies.Count == 0)
            {
                return null;
            }
            var criteria = new Dictionary<string, object>();
            foreach (var e in m_AliveEnemies)
            {
                var id = m_EnemyIds[e];
                criteria[id] = $"Track {id} (see `enemies`) with the turret and fire whenever it is lined up.";
            }
            criteria[AimOption.None] = "No enemy is worth tracking right now; keep the turret facing forward.";
            return criteria;
        }

        // ------------------------------------------------------------------ resolving option ids

        public static string StarId(Star s)
        {
            return MoveOption.StarPrefix + s.ID;
        }

        public string EnemyId(Tank t)
        {
            return t != null && m_EnemyIds.TryGetValue(t, out var id) ? id : null;
        }

        public Star ResolveStar(string optionId)
        {
            if (string.IsNullOrEmpty(optionId) || !optionId.StartsWith(MoveOption.StarPrefix))
            {
                return null;
            }
            if (!int.TryParse(optionId.Substring(MoveOption.StarPrefix.Length), out var starId))
            {
                return null;
            }
            return Match.instance.GetStarByID(starId);
        }

        public Tank ResolveEnemy(string optionId)
        {
            if (string.IsNullOrEmpty(optionId))
            {
                return null;
            }
            m_EnemiesById.TryGetValue(optionId, out var t);
            return t;
        }

        public Tank ResolveTeammate(string optionId)
        {
            if (string.IsNullOrEmpty(optionId))
            {
                return null;
            }
            m_TeammatesById.TryGetValue(optionId, out var t);
            return t;
        }

        /// <summary>True when the option still points at something that exists (star not taken, tank alive...).</summary>
        public bool IsMoveOptionValid(string optionId)
        {
            if (string.IsNullOrEmpty(optionId))
            {
                return false;
            }
            switch (optionId)
            {
                case MoveOption.Home:
                case MoveOption.AwayFromEnemies:
                case MoveOption.Hold:
                case MoveOption.Roam:
                    return true;
                case MoveOption.Sidestep:
                    // Expires by itself once the missile has passed or been destroyed.
                    return m_Config.DodgeMode == EDodgeMode.Jev && HasThreat;
            }
            if (optionId.StartsWith(MoveOption.StarPrefix))
            {
                return ResolveStar(optionId) != null;
            }
            if (optionId.StartsWith(MoveOption.EnemyPrefix))
            {
                var e = ResolveEnemy(optionId);
                return e != null && !e.IsDead;
            }
            if (optionId.StartsWith(MoveOption.TeammatePrefix))
            {
                var t = ResolveTeammate(optionId);
                return t != null && !t.IsDead;
            }
            return false;
        }

        public bool IsAimOptionValid(string optionId)
        {
            if (optionId == AimOption.None)
            {
                return true;
            }
            var e = ResolveEnemy(optionId);
            return e != null && !e.IsDead;
        }

        // ------------------------------------------------------------------ fallback helpers

        /// <summary>Star with the shortest travel distance (super star wins), or null. Uses the last snapshot.</summary>
        public string NearestStarId()
        {
            Star best = null;
            foreach (var s in m_Stars)
            {
                if (s.IsSuperStar)
                {
                    return StarId(s);
                }
                best ??= s; // list is sorted by travel distance
            }
            return best != null ? StarId(best) : null;
        }

        /// <summary>Nearest alive enemy with a clear line of fire, or null. Uses the last snapshot.</summary>
        public string NearestVisibleEnemyId()
        {
            Tank best = null;
            float bestDist = float.MaxValue;
            foreach (var e in m_AliveEnemies)
            {
                if (!m_Self.CanSeeOthers(e))
                {
                    continue;
                }
                var d = (e.Position - m_Self.Position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best != null ? m_EnemyIds[best] : null;
        }

        // ------------------------------------------------------------------ semantic buckets

        public string DistanceBucket(float distance)
        {
            var field = Match.instance.FieldSize;
            if (distance < field * m_Config.NearDistanceFraction)
            {
                return "near";
            }
            if (distance < field * m_Config.MediumDistanceFraction)
            {
                return "medium";
            }
            return "far";
        }

        private static string HpBucket(int hp)
        {
            var setting = Match.instance.GlobalSetting;
            float ratio = setting.MaxHP > 0 ? (float)hp / setting.MaxHP : 0f;
            int hitsToDie = setting.DamagePerHit > 0 ? Mathf.CeilToInt((float)hp / setting.DamagePerHit) : 99;
            string level;
            if (ratio >= 0.99f)
            {
                level = "full";
            }
            else if (ratio >= 0.7f)
            {
                level = "high";
            }
            else if (ratio >= 0.4f)
            {
                level = "medium";
            }
            else if (ratio >= 0.2f)
            {
                level = "low";
            }
            else
            {
                level = "critical";
            }
            string hits;
            switch (hitsToDie)
            {
                case 1: hits = "dies to the next hit"; break;
                case 2: hits = "dies in two hits"; break;
                case 3: hits = "dies in three hits"; break;
                default: hits = "can take several hits"; break;
            }
            return $"{level}, {hits}";
        }

        private static bool IsAimingAt(Tank shooter, Vector3 targetPos)
        {
            var to = targetPos - shooter.FirePos;
            to.y = 0;
            if (to.sqrMagnitude < 0.01f)
            {
                return true;
            }
            var aim = shooter.TurretAiming;
            aim.y = 0;
            return Vector3.Dot(aim.normalized, to.normalized) > 0.95f;
        }

        private static string MovementRelativeTo(Tank mover, Vector3 observerPos)
        {
            var v = mover.Velocity;
            v.y = 0;
            if (v.sqrMagnitude < 0.25f)
            {
                return "standing still";
            }
            var to = observerPos - mover.Position;
            to.y = 0;
            float dot = Vector3.Dot(v.normalized, to.normalized);
            if (dot > 0.6f)
            {
                return "driving toward me";
            }
            if (dot < -0.6f)
            {
                return "driving away from me";
            }
            return "driving sideways relative to me";
        }

        private static bool IsInHomeZone(Tank t)
        {
            var home = Match.instance.GetRebornPos(t.Team);
            var radius = Match.instance.GlobalSetting.HomeZoneRadius;
            return (t.Position - home).sqrMagnitude < radius * radius;
        }

        private string SelfLocation()
        {
            var home = Match.instance.GetRebornPos(m_Self.Team);
            if ((m_Self.Position - home).magnitude < Match.instance.GlobalSetting.HomeZoneRadius)
            {
                return "inside my home zone, recovering HP";
            }
            return DistanceBucket(m_HomeDistance) == "near" ? "close to my home zone" : "out in the field, away from home";
        }

        /// <summary>Compares my travel distance to the star with the closest enemy's travel distance.</summary>
        private string StarCompetition(Star s)
        {
            if (m_AliveEnemies.Count == 0)
            {
                return "no enemy alive to compete for it";
            }
            float myDist = DistanceTo(s);
            float enemyDist = float.MaxValue;
            foreach (var e in m_AliveEnemies)
            {
                enemyDist = Mathf.Min(enemyDist, TravelDistance(e.Position, s.Position));
            }
            if (myDist < enemyDist * 0.8f)
            {
                return "I am clearly closer to it than any enemy";
            }
            if (enemyDist < myDist * 0.8f)
            {
                return "an enemy is clearly closer to it than me";
            }
            return "an enemy is about as close to it as I am";
        }

        /// <summary>Up to three threats, most urgent first, or a plain "none" string.</summary>
        private object DescribeThreats()
        {
            if (m_Threats.Count == 0)
            {
                return "no missiles flying toward me";
            }
            var list = new List<object>();
            int count = Mathf.Min(3, m_Threats.Count);
            for (int i = 0; i < count; ++i)
            {
                var t = m_Threats[i];
                string arrival;
                if (t.Eta < 0.6f)
                {
                    arrival = "hits in well under a second";
                }
                else if (t.Eta < 1.2f)
                {
                    arrival = "hits in about a second";
                }
                else
                {
                    arrival = "a couple of seconds away";
                }
                list.Add(new Dictionary<string, object>
                {
                    { "course", t.WillHit ? "on a direct course to hit me" : "will pass close by me but miss" },
                    { "arrival", arrival },
                    { "coming_from", RelativeDirection(-t.Direction) },
                });
            }
            return list;
        }

        /// <summary>Describes a world direction relative to my body orientation.</summary>
        private string RelativeDirection(Vector3 worldDir)
        {
            var forward = m_Self.Forward;
            forward.y = 0;
            worldDir.y = 0;
            if (forward.sqrMagnitude < 1e-6f || worldDir.sqrMagnitude < 1e-6f)
            {
                return "from an unknown direction";
            }
            float angle = Vector3.SignedAngle(forward.normalized, worldDir.normalized, Vector3.up);
            if (Mathf.Abs(angle) <= 45f)
            {
                return "from in front of me";
            }
            if (Mathf.Abs(angle) >= 135f)
            {
                return "from behind me";
            }
            return angle > 0f ? "from my right side" : "from my left side";
        }

        private string ScoreStatus()
        {
            var match = Match.instance;
            int myScore = 0;
            int bestOther = 0;
            for (int i = 0; i < (int)ETeam.NB; ++i)
            {
                var team = (ETeam)i;
                var tanks = match.GetTanks(team);
                if (tanks == null)
                {
                    continue;
                }
                int total = 0;
                foreach (var t in tanks)
                {
                    total += t.Score;
                }
                if (team == m_Self.Team)
                {
                    myScore = total;
                }
                else
                {
                    bestOther = Mathf.Max(bestOther, total);
                }
            }
            int diff = myScore - bestOther;
            int kill = Mathf.Max(1, match.GlobalSetting.ScoreForKill);
            if (diff >= kill * 3)
            {
                return "my team is leading by a lot";
            }
            if (diff > 0)
            {
                return "my team is leading by a little";
            }
            if (diff == 0)
            {
                return "the score is tied";
            }
            if (diff > -kill * 3)
            {
                return "my team is behind by a little";
            }
            return "my team is behind by a lot";
        }

        private static string TimeBucket()
        {
            var match = Match.instance;
            float ratio = match.GlobalSetting.MatchTime > 0 ? match.RemainingTime / match.GlobalSetting.MatchTime : 0f;
            if (ratio > 0.5f)
            {
                return "first half of the match";
            }
            if (ratio > 0.15f)
            {
                return "second half of the match";
            }
            return "final stretch, the match ends very soon";
        }

        private string SuperStarStatus()
        {
            foreach (var s in m_Stars)
            {
                if (s.IsSuperStar)
                {
                    return "the super star is on the field right now";
                }
            }
            var match = Match.instance;
            bool secondHalf = match.RemainingTime < match.GlobalSetting.MatchTime * 0.5f;
            return secondHalf ? "the super star has already been taken" : "the super star will appear at half time";
        }

        private static string CountWords(int n, string singular, string plural)
        {
            switch (n)
            {
                case 0: return "no " + plural;
                case 1: return "one " + singular;
                case 2: return "two " + plural;
                case 3: return "three " + plural;
                default: return "many " + plural;
            }
        }
    }
}
