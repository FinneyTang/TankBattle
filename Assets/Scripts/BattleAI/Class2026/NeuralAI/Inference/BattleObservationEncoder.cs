using System.Collections.Generic;
using Main;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

namespace NeuralAI
{
    public class BattleObservationEncoder
    {
        private const int MaxMissileObservations = 3;
        private const int MaxEnemyObservations = 3;
        private const int LidarRayCount = 8;
        private const float ThreatCrossTrackDistance = 2.5f;
        private const float ThreatTimeWindow = 2f;
        private const float LidarMaxDistance = 60f;

        private readonly List<Tank> m_Tanks = new List<Tank>(4);
        private readonly List<Tank> m_Enemies = new List<Tank>(4);
        private readonly Dictionary<int, Missile> m_Missiles = new Dictionary<int, Missile>();
        private readonly List<Missile> m_NearestMissiles = new List<Missile>(MaxMissileObservations);

        public void AddObservations(Tank tank, VectorSensor sensor)
        {
            var values = new List<float>(TankBattleTrainingSettings.ObservationSize);
            WriteObservations(tank, values);
            for (int i = 0; i < values.Count; ++i)
            {
                sensor.AddObservation(values[i]);
            }
        }

        public float[] Encode(Tank tank)
        {
            var values = new List<float>(TankBattleTrainingSettings.ObservationSize);
            WriteObservations(tank, values);
            return values.ToArray();
        }

        private void WriteObservations(Tank tank, List<float> values)
        {
            Match match = Match.instance;
            if (tank == null || match == null)
            {
                Pad(values);
                return;
            }

            float fieldScale = Mathf.Max(1f, match.FieldSize);
            float speedScale = Mathf.Max(1f, match.GlobalSetting.MissileSpeed);
            float scoreScale = Mathf.Max(1f, match.GlobalSetting.ScoreForSuperStar + match.GlobalSetting.ScoreForKill);

            FindNearestEnemies(tank, m_Enemies);
            Tank nearestEnemy = m_Enemies.Count > 0 ? m_Enemies[0] : null;

            // Self state: 14 dims
            AddRelativeXZ(values, match.GetRebornPos(tank.Team) - tank.Position, fieldScale);
            Add01(values, (float)tank.HP / Mathf.Max(1, match.GlobalSetting.MaxHP));
            AddBool(values, tank.IsDead);
            AddBool(values, tank.CanFire());
            Add01(values, TankBattleActionMapper.GetFireCooldownRemaining(tank) / Mathf.Max(0.001f, match.GlobalSetting.FireInterval));
            AddDirectionXZ(values, tank.Forward);
            AddDirectionXZ(values, tank.TurretAiming);
            AddRelativeXZ(values, tank.NextDestination - tank.Position, fieldScale);
            AddBool(values, HasMissileThreat(tank));
            Add01(values, Vector3.Distance(tank.Position, Vector3.zero) / fieldScale);

            // Enemies: up to 3 × 11 = 33 dims
            for (int i = 0; i < MaxEnemyObservations; ++i)
            {
                if (i < m_Enemies.Count && m_Enemies[i] != null)
                {
                    AddEnemyObservations(values, tank, m_Enemies[i], fieldScale, speedScale);
                }
                else
                {
                    AddZeros(values, 11);
                }
            }

            // Teammate: 9 dims
            List<Tank> teammates = new List<Tank>(2);
            List<Tank> myTeamTanks = match.GetTanks(tank.Team);
            if (myTeamTanks != null)
            {
                for (int i = 0; i < myTeamTanks.Count; ++i)
                {
                    Tank t = myTeamTanks[i];
                    if (t != null && t != tank)
                    {
                        teammates.Add(t);
                    }
                }
                teammates.Sort((a, b) =>
                {
                    float da = (a.Position - tank.Position).sqrMagnitude;
                    float db = (b.Position - tank.Position).sqrMagnitude;
                    return da.CompareTo(db);
                });
            }

            if (teammates.Count > 0)
            {
                Tank t = teammates[0];
                AddBool(values, true);
                AddBool(values, !t.IsDead);
                AddRelativeXZ(values, t.Position - tank.Position, fieldScale);
                Add01(values, (float)t.HP / Mathf.Max(1, match.GlobalSetting.MaxHP));
                AddDirectionXZ(values, t.Forward);
                AddDirectionXZ(values, t.TurretAiming);
            }
            else
            {
                AddZeros(values, 9);
            }

            // Global: 5 dims
            Add01(values, match.RemainingTime / Mathf.Max(1f, match.GlobalSetting.MatchTime));
            AddClamped(values, tank.Score / scoreScale);
            AddClamped(values, nearestEnemy != null ? nearestEnemy.Score / scoreScale : 0f);
            AddClamped(values, nearestEnemy != null ? (tank.Score - nearestEnemy.Score) / scoreScale : 0f);
            AddSuperStarTiming(values, match);

            // Stars: 24 dims (3 normal stars × 6 + 1 super star × 6)
            AddStarObservations(values, tank, match, fieldScale);

            // Missiles: 24 dims
            AddMissileObservations(values, tank, match, fieldScale, speedScale);

            // LiDAR: 8 dims
            AddLidarObservations(values, tank);

            Pad(values);
        }

        private void FindNearestEnemies(Tank tank, List<Tank> output)
        {
            output.Clear();
            Match match = Match.instance;
            if (match == null)
            {
                return;
            }

            m_Tanks.Clear();
            match.GetOppositeTanks(tank.Team, m_Tanks);

            for (int i = 0; i < m_Tanks.Count; ++i)
            {
                Tank candidate = m_Tanks[i];
                if (candidate == null)
                {
                    continue;
                }

                float sqrDist = (candidate.Position - tank.Position).sqrMagnitude;
                int insertIndex = output.Count;
                for (int j = 0; j < output.Count; ++j)
                {
                    float distJ = (output[j].Position - tank.Position).sqrMagnitude;
                    if (sqrDist < distJ)
                    {
                        insertIndex = j;
                        break;
                    }
                }

                if (insertIndex < MaxEnemyObservations)
                {
                    output.Insert(insertIndex, candidate);
                    if (output.Count > MaxEnemyObservations)
                    {
                        output.RemoveAt(MaxEnemyObservations);
                    }
                }
            }
        }

        private Tank FindNearestTeammate(Tank tank)
        {
            Match match = Match.instance;
            if (match == null)
            {
                return null;
            }

            m_Tanks.Clear();
            List<Tank> tanks = match.GetTanks(tank.Team);
            if (tanks != null)
            {
                m_Tanks.AddRange(tanks);
            }

            Tank nearest = null;
            float nearestSqrDist = float.MaxValue;
            for (int i = 0; i < m_Tanks.Count; ++i)
            {
                Tank candidate = m_Tanks[i];
                if (candidate == null || candidate == tank)
                {
                    continue;
                }

                float sqrDist = (candidate.Position - tank.Position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private void AddEnemyObservations(List<float> values, Tank tank, Tank enemy, float fieldScale, float speedScale)
        {
            AddBool(values, true);
            Add01(values, enemy.GetRebornCD(Time.time) / Mathf.Max(1f, Match.instance.GlobalSetting.RebonCD));
            AddRelativeXZ(values, enemy.Position - tank.Position, fieldScale);
            AddDirectionXZ(values, enemy.Forward);
            AddDirectionXZ(values, enemy.TurretAiming);
            Add01(values, (float)enemy.HP / Mathf.Max(1, Match.instance.GlobalSetting.MaxHP));
            AddBool(values, tank.CanSeeOthers(enemy));
            Add01(values, Vector3.Distance(enemy.Position, Match.instance.GetRebornPos(enemy.Team)) / fieldScale);
        }

        private bool HasMissileThreat(Tank tank)
        {
            Match match = Match.instance;
            if (match == null)
            {
                return false;
            }

            m_Missiles.Clear();
            match.GetOppositeMissilesEx(tank.Team, m_Missiles);
            foreach (var pair in m_Missiles)
            {
                Missile missile = pair.Value;
                if (missile != null &&
                    HasClearLineOfSight(missile, tank) &&
                    WillMissileHitTank(missile, tank))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddSuperStarTiming(List<float> values, Match match)
        {
            float halfTime = match.GlobalSetting.MatchTime * 0.5f;
            float secondsUntilSuperStar = match.RemainingTime - halfTime;
            values.Add(Mathf.Clamp(secondsUntilSuperStar / 10f, -1f, 1f));
        }

        private void AddStarObservations(List<float> values, Tank tank, Match match, float fieldScale)
        {
            Star nearestNormal = null;
            Star secondNormal = null;
            Star thirdNormal = null;
            Star nearestSuper = null;
            float nearestNormalSqr = float.MaxValue;
            float secondNormalSqr = float.MaxValue;
            float thirdNormalSqr = float.MaxValue;
            float nearestSuperSqr = float.MaxValue;
            int normalCount = 0;

            foreach (var pair in match.GetStars())
            {
                Star star = pair.Value;
                if (star == null)
                {
                    continue;
                }

                float sqrDist = (star.Position - tank.Position).sqrMagnitude;
                if (star.IsSuperStar)
                {
                    if (sqrDist < nearestSuperSqr)
                    {
                        nearestSuperSqr = sqrDist;
                        nearestSuper = star;
                    }
                }
                else
                {
                    normalCount++;
                    if (sqrDist < nearestNormalSqr)
                    {
                        thirdNormal = secondNormal;
                        thirdNormalSqr = secondNormalSqr;
                        secondNormal = nearestNormal;
                        secondNormalSqr = nearestNormalSqr;
                        nearestNormal = star;
                        nearestNormalSqr = sqrDist;
                    }
                    else if (sqrDist < secondNormalSqr)
                    {
                        thirdNormal = secondNormal;
                        thirdNormalSqr = secondNormalSqr;
                        secondNormal = star;
                        secondNormalSqr = sqrDist;
                    }
                    else if (sqrDist < thirdNormalSqr)
                    {
                        thirdNormal = star;
                        thirdNormalSqr = sqrDist;
                    }
                }
            }

            AddStar(values, tank, nearestNormal, fieldScale);
            AddStar(values, tank, secondNormal, fieldScale);
            AddStar(values, tank, thirdNormal, fieldScale);

            if (nearestSuper != null)
            {
                AddStar(values, tank, nearestSuper, fieldScale);
            }
            else
            {
                AddBool(values, false);
                AddZeros(values, 5);
            }
        }

        private void AddStar(List<float> values, Tank tank, Star star, float fieldScale)
        {
            AddBool(values, star != null);
            if (star == null)
            {
                AddZeros(values, 5);
                return;
            }

            Vector3 offset = star.Position - tank.Position;
            AddRelativeXZ(values, offset, fieldScale);
            // 路径距离代替直线距离，与 expert 的 NavMesh 路径规划对齐
            float pathDist = GetPathDistance(tank, star.Position);
            Add01(values, pathDist / fieldScale);
            // 路径 waypoint 方向（下一个拐点），帮助 agent 绕开障碍物
            NavMeshPath path = null;
            if (!tank.IsDead)
            {
                path = tank.CaculatePath(star.Position);
            }
            if (path != null && path.corners.Length > 1)
            {
                Vector3 waypointOffset = path.corners[1] - tank.Position;
                AddRelativeXZ(values, waypointOffset, fieldScale);
            }
            else
            {
                // 无障碍或就在旁边，用直线方向代替
                AddRelativeXZ(values, offset, fieldScale);
            }
        }

        private float GetPathDistance(Tank tank, Vector3 targetPos)
        {
            if (tank == null)
            {
                return float.PositiveInfinity;
            }

            // Tank 死亡/禁用期间 NavMeshAgent 不可用，回退到直线距离
            if (tank.IsDead)
            {
                return Vector3.Distance(tank.Position, targetPos);
            }

            NavMeshPath path = tank.CaculatePath(targetPos);
            if (path == null || path.corners.Length == 0)
            {
                return Vector3.Distance(tank.Position, targetPos);
            }

            float distance = 0f;
            Vector3 from = tank.Position;
            for (int i = 0; i < path.corners.Length; ++i)
            {
                distance += Vector3.Distance(from, path.corners[i]);
                from = path.corners[i];
            }
            return distance;
        }

        private void AddMissileObservations(
            List<float> values, Tank tank, Match match, float fieldScale, float speedScale)
        {
            m_Missiles.Clear();
            match.GetOppositeMissilesEx(tank.Team, m_Missiles);
            m_NearestMissiles.Clear();

            foreach (var pair in m_Missiles)
            {
                Missile missile = pair.Value;
                if (missile == null)
                {
                    continue;
                }

                InsertPrioritizedMissile(tank, missile);
            }

            for (int i = 0; i < MaxMissileObservations; ++i)
            {
                if (i < m_NearestMissiles.Count)
                {
                    AddMissile(values, tank, m_NearestMissiles[i], fieldScale, speedScale);
                }
                else
                {
                    AddZeros(values, 8);
                }
            }
        }

        private void InsertPrioritizedMissile(Tank tank, Missile missile)
        {
            int insertIndex = m_NearestMissiles.Count;

            for (int i = 0; i < m_NearestMissiles.Count; ++i)
            {
                if (IsHigherPriorityMissile(tank, missile, m_NearestMissiles[i]))
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < MaxMissileObservations)
            {
                m_NearestMissiles.Insert(insertIndex, missile);
                if (m_NearestMissiles.Count > MaxMissileObservations)
                {
                    m_NearestMissiles.RemoveAt(MaxMissileObservations);
                }
            }
        }

        private bool IsHigherPriorityMissile(Tank tank, Missile candidate, Missile current)
        {
            bool candidateLos = HasClearLineOfSight(candidate, tank);
            bool currentLos = HasClearLineOfSight(current, tank);
            if (candidateLos != currentLos)
            {
                return candidateLos;
            }

            Vector3 candidateToTank = tank.Position - candidate.Position;
            Vector3 currentToTank = tank.Position - current.Position;
            bool candidateClosing = candidate.Velocity.sqrMagnitude > 0.0001f &&
                Vector3.Dot(candidate.Velocity.normalized, candidateToTank.normalized) > 0f;
            bool currentClosing = current.Velocity.sqrMagnitude > 0.0001f &&
                Vector3.Dot(current.Velocity.normalized, currentToTank.normalized) > 0f;
            if (candidateClosing != currentClosing)
            {
                return candidateClosing;
            }

            return candidateToTank.sqrMagnitude < currentToTank.sqrMagnitude;
        }

        private void AddMissile(
            List<float> values, Tank tank, Missile missile, float fieldScale, float speedScale)
        {
            Vector3 offset = missile.Position - tank.Position;
            Vector3 velocity = missile.Velocity;

            AddBool(values, true);
            AddRelativeXZ(values, offset, fieldScale);
            AddRelativeXZ(values, velocity, speedScale);
            Add01(values, offset.magnitude / fieldScale);
            AddBool(values, HasClearLineOfSight(missile, tank));
            AddBool(values, WillMissileHitTank(missile, tank));
        }

        private bool HasClearLineOfSight(Missile missile, Tank tank)
        {
            return !Physics.Linecast(missile.Position, tank.Position, PhysicsUtils.LayerMaskScene);
        }

        private bool WillMissileHitTank(Missile missile, Tank tank)
        {
            Vector3 velocity = missile.Velocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 direction = velocity.normalized;
            float distance = Vector3.Distance(missile.Position, tank.Position);

            RaycastHit[] hits = Physics.SphereCastAll(
                missile.Position, 0.5f, direction, distance, PhysicsUtils.LayerMaskCollsion);
            foreach (RaycastHit hit in hits)
            {
                if (PhysicsUtils.IsFireCollider(hit.collider))
                {
                    FireCollider fc = hit.collider.GetComponent<FireCollider>();
                    if (fc != null && fc.Owner == tank)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void AddLidarObservations(List<float> values, Tank tank)
        {
            Vector3 origin = tank.Position + Vector3.up * 0.5f;
            float maxDist = Mathf.Min(LidarMaxDistance, Mathf.Max(1f, Match.instance.FieldSize));

            for (int i = 0; i < LidarRayCount; ++i)
            {
                float angle = i * Mathf.PI * 2f / LidarRayCount;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
                float distance = maxDist;

                if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist, PhysicsUtils.LayerMaskScene))
                {
                    distance = hit.distance;
                }

                values.Add(Mathf.Clamp01(distance / maxDist));
            }
        }

        private void AddRelativeXZ(List<float> values, Vector3 vector, float scale)
        {
            values.Add(Mathf.Clamp(vector.x / scale, -1f, 1f));
            values.Add(Mathf.Clamp(vector.z / scale, -1f, 1f));
        }

        private void AddDirectionXZ(List<float> values, Vector3 vector)
        {
            Vector3 normalized = vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.zero;
            values.Add(Mathf.Clamp(normalized.x, -1f, 1f));
            values.Add(Mathf.Clamp(normalized.z, -1f, 1f));
        }

        private void Add01(List<float> values, float value)
        {
            values.Add(Mathf.Clamp01(value));
        }

        private void AddClamped(List<float> values, float value)
        {
            values.Add(Mathf.Clamp(value, -1f, 1f));
        }

        private void AddBool(List<float> values, bool value)
        {
            values.Add(value ? 1f : 0f);
        }

        private void AddZeros(List<float> values, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                values.Add(0f);
            }
        }

        private void Pad(List<float> values)
        {
            if (values.Count > TankBattleTrainingSettings.ObservationSize)
            {
                Debug.LogError(
                    $"[BattleObservationEncoder] Observation overflow: " +
                    $"generated {values.Count} values but ObservationSize is " +
                    $"{TankBattleTrainingSettings.ObservationSize}. " +
                    $"Last {values.Count - TankBattleTrainingSettings.ObservationSize} value(s) will be truncated. " +
                    $"Fix ObservationSize or reduce encoded features.");
                values.RemoveRange(TankBattleTrainingSettings.ObservationSize,
                    values.Count - TankBattleTrainingSettings.ObservationSize);
            }

            while (values.Count < TankBattleTrainingSettings.ObservationSize)
            {
                values.Add(0f);
            }
        }
    }
}
