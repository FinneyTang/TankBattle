using System.Collections.Generic;
using Main;
using Unity.MLAgents.Actuators;
using UnityEngine;

namespace NeuralAI
{
    public struct TankBattleActionResult
    {
        public bool MoveRequested;
        public bool MoveSucceeded;
        public bool FireRequested;
        public bool FireSucceeded;
    }

    public class ExpertActionInferenceState
    {
        public bool HasMissileSnapshot;
        public readonly HashSet<int> KnownFireMissileIds = new HashSet<int>();
    }

    public static class TankBattleActionMapper
    {
        private const float FireAlignmentDot = 0.94f;
        private static readonly List<Tank> Opponents = new List<Tank>(4);
        private static readonly Dictionary<int, Missile> TeamMissiles = new Dictionary<int, Missile>();
        private static readonly Dictionary<Tank, float> s_LastFireTime = new Dictionary<Tank, float>();

        public static float GetFireCooldownRemaining(Tank tank)
        {
            if (tank == null)
            {
                return 0f;
            }
            float fireInterval = Match.instance?.GlobalSetting.FireInterval ?? 1f;
            if (s_LastFireTime.TryGetValue(tank, out float lastTime))
            {
                return Mathf.Max(0f, lastTime + fireInterval - Time.time);
            }
            return 0f;
        }

        public static TankBattleActionResult Apply(
            Tank tank,
            ActionBuffers actions,
            TankBattleTrainingSettings settings,
            bool allowFire)
        {
            var result = new TankBattleActionResult();
            if (tank == null || tank.IsDead)
            {
                return result;
            }

            var continuous = actions.ContinuousActions;
            var discrete = actions.DiscreteActions;

            Vector2 move = continuous.Length >= 2
                ? new Vector2(continuous[0], continuous[1])
                : Vector2.zero;
            Vector2 aim = continuous.Length >= 4
                ? new Vector2(continuous[2], continuous[3])
                : Vector2.zero;

            move = Vector2.ClampMagnitude(move, 1f);
            aim = Vector2.ClampMagnitude(aim, 1f);

            if (move.sqrMagnitude > 0.0001f)
            {
                Vector3 target = tank.Position +
                                 new Vector3(move.x, 0f, move.y) * settings.MoveStepDistance;
                result.MoveRequested = true;
                result.MoveSucceeded = tank.Move(target);
            }
            else
            {
                tank.Move(tank.Position);
            }

            bool fireRequested;
            if (settings.FireMode == FireControlMode.FixedAlgorithm)
            {
                ApplySmTurretAim(tank);
                fireRequested = allowFire && ShouldSmFire(tank);
            }
            else
            {
                Vector3 aimTarget;
                if (aim.sqrMagnitude > 0.0001f)
                {
                    aimTarget = tank.Position +
                                new Vector3(aim.x, 0f, aim.y).normalized * settings.AimDistance;
                    aimTarget.y = tank.FirePos.y;
                }
                else
                {
                    // 不再自动预测瞄准：模型必须自己输出瞄准方向
                    aimTarget = tank.Position + tank.TurretAiming * settings.AimDistance;
                }
                tank.TurretTurnTo(aimTarget);

                fireRequested = allowFire &&
                                discrete.Length > 0 &&
                                discrete[0] == 1;
            }

            result.FireRequested = fireRequested;
            if (fireRequested)
            {
                result.FireSucceeded = tank.Fire();
                if (result.FireSucceeded)
                {
                    s_LastFireTime[tank] = Time.time;
                }
            }

            return result;
        }

        public static void InferExpertAction(
            Tank tank,
            TankBattleTrainingSettings settings,
            ExpertActionInferenceState state,
            float[] continuous,
            int[] discrete)
        {
            for (int i = 0; i < continuous.Length; ++i)
            {
                continuous[i] = 0f;
            }

            for (int i = 0; i < discrete.Length; ++i)
            {
                discrete[i] = 0;
            }

            if (tank == null || tank.IsDead)
            {
                return;
            }

            Vector3 velocity = tank.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                Vector2 move = new Vector2(velocity.x, velocity.z).normalized;
                continuous[0] = move.x;
                continuous[1] = move.y;
            }

            Vector3 aim = tank.TurretAiming;
            Vector2 aim2D = new Vector2(aim.x, aim.z);
            if (aim2D.sqrMagnitude > 0.0001f)
            {
                aim2D.Normalize();
                continuous[2] = aim2D.x;
                continuous[3] = aim2D.y;
            }

            if (DetectNewTeamFire(tank, state))
            {
                discrete[0] = 1;
                s_LastFireTime[tank] = Time.time;
            }
        }

        private static bool DetectNewTeamFire(Tank tank, ExpertActionInferenceState state)
        {
            if (Match.instance == null || state == null)
            {
                return false;
            }

            bool fired = false;
            TeamMissiles.Clear();
            Match.instance.GetOppositeMissilesEx(GetMissileSnapshotExcludedTeam(tank.Team), TeamMissiles);
            foreach (var pair in TeamMissiles)
            {
                Missile missile = pair.Value;
                if (missile == null || missile.Team != tank.Team)
                {
                    continue;
                }

                if (state.KnownFireMissileIds.Add(missile.ID) && state.HasMissileSnapshot)
                {
                    fired = true;
                }
            }

            state.HasMissileSnapshot = true;
            return fired;
        }

        private static ETeam GetMissileSnapshotExcludedTeam(ETeam team)
        {
            for (int i = 0; i < (int)ETeam.NB; ++i)
            {
                ETeam candidate = (ETeam)i;
                if (candidate != team)
                {
                    return candidate;
                }
            }

            return team;
        }

        private static bool IsFireAligned(Tank tank, Vector3 target)
        {
            Vector3 toTarget = target - tank.FirePos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 turretAiming = tank.TurretAiming;
            turretAiming.y = 0f;
            if (turretAiming.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            return Vector3.Dot(turretAiming.normalized, toTarget.normalized) >= FireAlignmentDot;
        }

        private static bool CanFireAtPredictedTarget(Tank tank)
        {
            if (Match.instance == null)
            {
                return false;
            }

            Opponents.Clear();
            Match.instance.GetOppositeTanks(tank.Team, Opponents);
            for (int i = 0; i < Opponents.Count; ++i)
            {
                Tank enemy = Opponents[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if (IsNearRebornPoint(enemy))
                {
                    continue;
                }

                Vector3 predictedTarget = GetPredictedAimTarget(tank, enemy);
                if (IsFireAligned(tank, predictedTarget) && HasClearStaticFireLine(tank, predictedTarget))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNearRebornPoint(Tank tank)
        {
            return Match.instance != null &&
                   Vector3.SqrMagnitude(tank.Position - Match.instance.GetRebornPos(tank.Team)) < 200f;
        }

        private static bool HasClearStaticFireLine(Tank tank, Vector3 target)
        {
            target.y = tank.FirePos.y;
            return !Physics.Linecast(tank.FirePos, target, PhysicsUtils.LayerMaskScene);
        }

        /// <summary>
        /// 沿当前炮塔方向做 SphereCast，确认前方会先命中敌人（FireCollider）而不是墙。
        /// 参考 ZYH_ICE_Winner / HZR_Winner 的预测后碰撞检测思路。
        /// </summary>
        public static bool WillHitEnemyAlongAim(Tank tank, float maxDistance)
        {
            Vector3 origin = tank.FirePos;
            Vector3 direction = tank.TurretAiming;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            if (Physics.SphereCast(origin, 0.24f, direction.normalized, out RaycastHit hit,
                maxDistance, PhysicsUtils.LayerMaskCollsion))
            {
                return PhysicsUtils.IsFireCollider(hit.collider);
            }

            return false;
        }

        public static Vector3 GetPredictedAimTarget(Tank tank, Tank enemy)
        {
            Vector3 shooterPosition = tank.FirePos;
            Vector3 enemyPosition = enemy.Position;
            enemyPosition.y = shooterPosition.y;

            if (Match.instance == null)
            {
                return enemyPosition;
            }

            Vector3 enemyVelocity = enemy.Velocity;
            enemyVelocity.y = 0f;
            float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
            if (missileSpeed <= 0.001f || enemyVelocity.sqrMagnitude < 0.0001f)
            {
                return enemyPosition;
            }

            Vector3 toEnemy = enemyPosition - shooterPosition;
            float a = Vector3.Dot(enemyVelocity, enemyVelocity) - missileSpeed * missileSpeed;
            float b = 2f * Vector3.Dot(toEnemy, enemyVelocity);
            float c = Vector3.Dot(toEnemy, toEnemy);
            float interceptTime = 0f;

            if (Mathf.Abs(a) < 0.0001f)
            {
                if (Mathf.Abs(b) > 0.0001f)
                {
                    interceptTime = -c / b;
                }
            }
            else
            {
                float discriminant = b * b - 4f * a * c;
                if (discriminant >= 0f)
                {
                    float sqrt = Mathf.Sqrt(discriminant);
                    float t1 = (-b - sqrt) / (2f * a);
                    float t2 = (-b + sqrt) / (2f * a);
                    interceptTime = SelectPositiveInterceptTime(t1, t2);
                }
            }

            if (interceptTime > 0f && !float.IsNaN(interceptTime) && !float.IsInfinity(interceptTime))
            {
                enemyPosition += enemyVelocity * interceptTime;
            }

            enemyPosition.y = shooterPosition.y;
            return enemyPosition;
        }

        private static float SelectPositiveInterceptTime(float t1, float t2)
        {
            bool t1Valid = t1 > 0f;
            bool t2Valid = t2 > 0f;
            if (t1Valid && t2Valid)
            {
                return Mathf.Min(t1, t2);
            }

            if (t1Valid)
            {
                return t1;
            }

            return t2Valid ? t2 : 0f;
        }

        private static Tank FindNearestLiveEnemy(Tank tank)
        {
            if (Match.instance == null)
            {
                return null;
            }

            Opponents.Clear();
            Match.instance.GetOppositeTanks(tank.Team, Opponents);
            Tank nearest = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < Opponents.Count; ++i)
            {
                Tank candidate = Opponents[i];
                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                float sqrDistance = (candidate.Position - tank.Position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static void ApplySmTurretAim(Tank tank)
        {
            Tank enemy = FindNearestLiveEnemy(tank);
            if (enemy != null && !enemy.IsDead)
            {
                Vector3 aimPoint = CalculateSmAimPoint(tank, enemy);
                tank.TurretTurnTo(aimPoint);
            }
            else
            {
                Vector3 fallbackTarget = tank.Position + tank.Forward * 100f;
                fallbackTarget.y = tank.FirePos.y;
                tank.TurretTurnTo(fallbackTarget);
            }
        }

        private static Vector3 CalculateSmAimPoint(Tank tank, Tank enemy)
        {
            Vector3 predictedTarget = GetPredictedAimTarget(tank, enemy);
            Vector3 targetDirection = predictedTarget - tank.FirePos;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude < 0.0001f)
            {
                targetDirection = enemy.Position - tank.FirePos;
                targetDirection.y = 0f;
            }

            if (targetDirection.sqrMagnitude < 0.0001f)
            {
                targetDirection = tank.TurretAiming;
                targetDirection.y = 0f;
            }

            Vector3 aimPoint = tank.FirePos + targetDirection.normalized * 100f;
            aimPoint.y = tank.FirePos.y;
            return aimPoint;
        }

        private static bool ShouldSmFire(Tank tank)
        {
            if (tank == null || Match.instance == null || !tank.CanFire())
            {
                return false;
            }

            return CanFireAtPredictedTarget(tank);
        }
    }
}
