using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using AI.RuleBased;
using Main;
using UnityEngine;

namespace FY
{
    enum EBBKey
    {
        MovingTargetPos,
        IncomingMissile,
        TargetStar,
        IsSuperStarTarget,

        StarDistSqr,
        LastMoveTargetPos,
        TargetLockUntil,

        TurretTF,
        LastAimDir,

        SmoothedEnemyVel,
        LastAimPoint,

        StrafeSide,

        LastEnemyPos,        // 上一次观测到的敌人位置（Vector3）
        LastEnemySeenTime    // 上一次看到敌人的时间戳（float）
    }

    sealed class ConditionHasStarOnMatch : Condition
    {
        public override bool IsTrue(IAgent agent) => Match.instance.GetStars().Count != 0;
    }

    // 动作：强锁定炮塔（始终锁定敌人/敌人未来位置，不因可见性或车身转向漂移）
    sealed class TurnTurretPredict : ActionNode
    {
        private const float TurretTurnSpeed = 1000f; // 更快，尽量接近“锁死”
        private const float MemoryHorizonSeconds = 1.2f; // 丢视野后最多用死算预测这么久

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            var tank = (Tank)agent;
            bool lowHp = tank.HP < 41;
            var enemy = Match.instance.GetOppositeTank(tank.Team);

            if (!workingMemory.TryGetValue((int)EBBKey.TurretTF, out Transform turret) || turret == null)
            {
                turret = tank.transform.childCount > 1 ? tank.transform.GetChild(1) : tank.transform;
                workingMemory.SetValue((int)EBBKey.TurretTF, turret);
            }

            if (enemy == null || enemy.IsDead)
                return ERunningStatus.Executing;

            // 速度平滑（预测更稳）
            Vector3 curVel = enemy.Velocity;
            curVel.y = 0;

            // 预判敌人躲避子弹的移动方向
            var ourMissiles = Match.instance.GetOppositeMissiles(enemy.Team);
            Missile threatening = null;
            float bestThreatDist = float.MaxValue;
            foreach (var kv in ourMissiles)
            {
                var m = kv.Value;
                if (m == null) continue;
                
                Vector3 toEnemy = enemy.Position - m.Position;
                toEnemy.y = 0;
                if (toEnemy.sqrMagnitude > 30f * 30f) continue;
                if (Vector3.Dot(toEnemy, m.Velocity) < 0f) continue;
                if (Physics.Linecast(m.Position, enemy.Position, PhysicsUtils.LayerMaskScene)) continue;
                if (Vector3.Angle(m.Velocity, toEnemy) < 28f)
                {
                    float dist = toEnemy.sqrMagnitude;
                    if (dist < bestThreatDist)
                    {
                        bestThreatDist = dist;
                        threatening = m;
                    }
                }
            }

            if (threatening != null)
            {
                var lateral = Vector3.Cross(threatening.Velocity, Vector3.up);
                lateral.y = 0;
                if (lateral.sqrMagnitude > 0.001f)
                {
                    lateral.Normalize();
                    if (Vector3.Cross(threatening.Velocity, enemy.Position - threatening.Position).y > 0)
                        lateral *= -1f;
                    
                    // 假设敌人会以一定速度往躲避方向移动
                    curVel = lateral * 5.0f; 
                }
            }

            var smoothVel = workingMemory.GetValue<Vector3>((int)EBBKey.SmoothedEnemyVel, curVel);
            const float alpha = 0.35f;
            smoothVel = Vector3.Lerp(smoothVel, curVel, alpha);
            smoothVel.y = 0;
            workingMemory.SetValue((int)EBBKey.SmoothedEnemyVel, smoothVel);

            bool canSee = tank.CanSeeOthers(enemy);
            if (canSee)
            {
                workingMemory.SetValue((int)EBBKey.LastEnemyPos, enemy.Position);
                workingMemory.SetValue((int)EBBKey.LastEnemySeenTime, Time.time);
            }

            // 即便看不见，也用“最后看到的位置 + 速度死算”来保持炮管锁定（不回正、不乱漂）
            Vector3 basePos = workingMemory.GetValue<Vector3>((int)EBBKey.LastEnemyPos, enemy.Position);
            float lastSeenTime = workingMemory.GetValue<float>((int)EBBKey.LastEnemySeenTime, Time.time);
            float unseenDt = Mathf.Clamp(Time.time - lastSeenTime, 0f, MemoryHorizonSeconds);

            // 近似预测一个“虚拟敌人位置”用于瞄准
            Vector3 virtualEnemyPos = basePos + smoothVel * unseenDt;

            // 可见则用真实位置/速度做拦截预判；不可见则用虚拟位置并弱化提前量
            Vector3 aimPoint;
            if (canSee)
            {
                float distSqr = (enemy.Position - tank.Position).sqrMagnitude;
                if (distSqr > 10f * 10f && distSqr < 24f * 24f)
                    aimPoint = GetAimPointRefined(tank, enemy.Position, smoothVel);
                else
                    aimPoint = GetAimPointSM(tank, enemy.Position, enemy.Velocity);
                aimPoint = ApplyDodgeBias(tank, enemy, aimPoint);
            }
            else
            {
                // 不可见：不要做过远的提前量，直接锁虚拟位置，保证炮管不受车身影响
                aimPoint = virtualEnemyPos;
            }

            workingMemory.SetValue((int)EBBKey.LastAimPoint, aimPoint);

            // 用 FirePos 计算方向，贴合弹道
            Vector3 dir = aimPoint - tank.FirePos;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f)
                dir = tank.Forward;
            dir.Normalize();

            workingMemory.SetValue((int)EBBKey.LastAimDir, dir);

            // 直接设置 forward；Lerp 只是为了避免极端抖动
            turret.forward = Vector3.Lerp(turret.forward, dir, Time.deltaTime * TurretTurnSpeed);

            return ERunningStatus.Executing;
        }

        // 用位置+速度做拦截点：闭式解 + 2 次迭代修正
        private static Vector3 GetAimPointRefined(Tank tank, Vector3 enemyPos3, Vector3 enemyVelSmoothed)
        {
            Vector3 p0 = enemyPos3;

            Vector3 v3 = enemyVelSmoothed;
            v3.y = 0;
            if (v3.sqrMagnitude < 0.6f * 0.6f)
                return p0;

            Vector3 firePos = tank.FirePos;
            float missileSpeed = Mathf.Max(Match.instance.GlobalSetting.MissileSpeed, 0.01f);

            Vector2 enemyPos = new Vector2(p0.x, p0.z);
            Vector2 enemyVel = new Vector2(v3.x, v3.z);
            Vector2 shooterPos = new Vector2(firePos.x, firePos.z);
            Vector2 delta = enemyPos - shooterPos;

            float a = Vector2.Dot(enemyVel, enemyVel) - missileSpeed * missileSpeed;
            float b = 2f * Vector2.Dot(delta, enemyVel);
            float c = delta.sqrMagnitude;

            float dist = Mathf.Sqrt(c);
            float tMax = Mathf.Clamp(dist / missileSpeed + 0.2f, 0.08f, 1.5f);

            float t;
            const float eps = 1e-4f;
            if (Mathf.Abs(a) < eps)
            {
                t = dist / missileSpeed;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f)
                {
                    t = 0f;
                }
                else
                {
                    float sqrt = Mathf.Sqrt(disc);
                    float t0 = (-b - sqrt) / (2f * a);
                    float t1 = (-b + sqrt) / (2f * a);
                    t = (t0 > 0f) ? t0 : ((t1 > 0f) ? t1 : 0f);
                }
            }

            t = Mathf.Clamp(t, 0f, tMax);

            for (int i = 0; i < 2; i++)
            {
                Vector3 p = p0 + v3 * t;
                Vector3 dp = p - firePos;
                dp.y = 0;
                float newT = dp.magnitude / missileSpeed;
                newT = Mathf.Clamp(newT, 0f, tMax);
                t = (t * 0.4f) + (newT * 0.6f);
            }

            Vector3 aim = p0 + v3 * t;
            aim.y = enemyPos3.y;

            if ((aim - p0).sqrMagnitude > 12f * 12f)
                return p0;

            return aim;
        }

        private static Vector3 GetAimPointSM(Tank tank, Vector3 enemyPos3, Vector3 enemyVel3)
        {
            Vector3 firePos = tank.FirePos;
            Vector2 enemyPos = new Vector2(enemyPos3.x, enemyPos3.z);
            Vector2 enemyVel = new Vector2(enemyVel3.x, enemyVel3.z);
            Vector2 shooterPos = new Vector2(firePos.x, firePos.z);
            Vector2 delta = enemyPos - shooterPos;

            float missileSpeed = Mathf.Max(Match.instance.GlobalSetting.MissileSpeed, 0.01f);
            float a = Vector2.Dot(enemyVel, enemyVel) - missileSpeed * missileSpeed;
            float b = 2f * Vector2.Dot(delta, enemyVel);
            float c = delta.sqrMagnitude;

            float t = 0f;
            float eps = 1e-4f;
            if (Mathf.Abs(a) < eps)
            {
                t = Mathf.Sqrt(c) / missileSpeed;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float sqrt = Mathf.Sqrt(disc);
                    float t0 = (-b - sqrt) / (2f * a);
                    float t1 = (-b + sqrt) / (2f * a);
                    t = (t0 > 0f) ? t0 : ((t1 > 0f) ? t1 : 0f);
                }
            }

            if (t <= 0f)
                return enemyPos3;

            Vector2 intercept = enemyPos + enemyVel * t;
            return new Vector3(intercept.x, enemyPos3.y, intercept.y);
        }

        private static Vector3 ApplyDodgeBias(Tank tank, Tank enemy, Vector3 aimPoint)
        {
            Vector3 toEnemy = enemy.Position - tank.Position;
            toEnemy.y = 0;
            if (toEnemy.sqrMagnitude < 0.001f)
                return aimPoint;

            Vector3 lateral = Vector3.Cross(Vector3.up, toEnemy).normalized;
            if (lateral.sqrMagnitude < 0.001f)
                return aimPoint;

            Vector3 ev = enemy.Velocity;
            ev.y = 0;
            float side = Mathf.Sign(Vector3.Dot(ev, lateral));
            if (side == 0)
                side = 1f;

            float missileSpeed = Mathf.Max(Match.instance.GlobalSetting.MissileSpeed, 0.01f);
            float travelTime = Vector3.Distance(tank.FirePos, aimPoint) / missileSpeed;
            float offset = Mathf.Clamp(ev.magnitude * travelTime * 0.22f, 0.6f, 2.4f);

            return aimPoint + lateral * side * offset;
        }
    }

    // 动作：只在“对准+无墙挡”时开火，减少浪费，提高对拼胜率
    sealed class FireWhenAligned : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            var tank = (Tank)agent;
            var enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null || enemy.IsDead)
                return false;

            if (!tank.CanFire())
                return false;

            if (Vector3.SqrMagnitude(enemy.Position - Match.instance.GetRebornPos(enemy.Team)) < 200.0f)
                return false;

            bool canSee = tank.CanSeeOthers(enemy);

            // 使用“预判命中点”做开火判定，避免只拿当前位置导致远距离偏差
            Vector3 aimPoint = workingMemory.GetValue<Vector3>((int)EBBKey.LastAimPoint, enemy.Position);
            Vector3 toAim = aimPoint - tank.FirePos;
            toAim.y = 0;
            float dist = toAim.magnitude;
            if (dist < 0.01f)
                return false;

            Vector3 aimDir = toAim / dist;

            // 角度阈值：近距离放宽，远距离收紧
            float maxAngle = (dist < 12f) ? 22f : 8f;

            float lastSeenTime = workingMemory.GetValue<float>((int)EBBKey.LastEnemySeenTime, -999f);
            bool recentlySeen = Time.time - lastSeenTime <= 1.2f;

            // 近身对拼直接开（命中率高、也利于压制）
            if (dist < 12f)
                return true;

            if (!canSee)
            {
                if (!recentlySeen || dist > 22f)
                    return false;

                Vector3 smoothedVel = workingMemory.GetValue<Vector3>((int)EBBKey.SmoothedEnemyVel, Vector3.zero);
                smoothedVel.y = 0;
                Vector3 toTank = tank.Position - aimPoint;
                toTank.y = 0;
                if (Vector3.Dot(smoothedVel, toTank) <= 0.05f)
                    return false;
            }

            // 对准检查：用 turret aiming 和 “指向预判点的方向”对齐
            if (Vector3.Angle(tank.TurretAiming, aimDir) > maxAngle)
                return false;

            // 遮挡检测：FirePos -> 预判点（若被墙挡就不打）
            if (Physics.SphereCast(tank.FirePos, 0.23f, aimDir, out RaycastHit hit, dist, PhysicsUtils.LayerMaskScene))
            {
                var fc = hit.transform.GetComponent<FireCollider>();
                if (fc == null || fc.Owner != enemy)
                    return false;
            }

            return true;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            ((Tank)agent).Fire();
            return ERunningStatus.Executing;
        }
    }

    // 动作：加权决策（根据权重选一个“最该做的移动行为”，并用锁定/滞回避免原地抽搐）
    sealed class WeightedMovementDecision : ActionNode
    {
        private const float NearStarDistSqr = 8f * 8f;
        private const float FarStarDistSqr = 20f * 20f;
        private const float LockSeconds = 0.25f;

        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory) => true;

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            var tank = (Tank)agent;

            if (workingMemory.TryGetValue((int)EBBKey.TargetLockUntil, out float lockUntil) && Time.time < lockUntil)
            {
                if (workingMemory.TryGetValue((int)EBBKey.LastMoveTargetPos, out Vector3 last))
                {
                    workingMemory.SetValue((int)EBBKey.MovingTargetPos, last);
                    return ERunningStatus.Finished;
                }
            }

            Star bestStar = null;
            bool isSuper = false;
            float bestScore = float.MinValue;
            float bestDistSqr = float.MaxValue;

            var stars = Match.instance.GetStars();
            foreach (var kv in stars)
            {
                var s = kv.Value;
                if (s == null)
                    continue;

                if (s.IsSuperStar)
                {
                    bestStar = s;
                    isSuper = true;
                    bestDistSqr = (s.Position - tank.Position).sqrMagnitude;
                    break;
                }

                float d = (s.Position - tank.Position).sqrMagnitude;
                int nearby = 0;
                foreach (var other in stars)
                {
                    var os = other.Value;
                    if (os == null)
                        continue;
                    if ((os.Position - s.Position).sqrMagnitude <= 18f * 18f)
                        nearby++;
                }

                float score = nearby * 1200f - d;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDistSqr = d;
                    bestStar = s;
                }
            }

            if (bestStar != null)
            {
                workingMemory.SetValue((int)EBBKey.TargetStar, bestStar);
                workingMemory.SetValue((int)EBBKey.IsSuperStarTarget, isSuper);
                workingMemory.SetValue((int)EBBKey.StarDistSqr, bestDistSqr);
            }
            else
            {
                workingMemory.DelValue((int)EBBKey.TargetStar);
                workingMemory.SetValue((int)EBBKey.IsSuperStarTarget, false);
                workingMemory.SetValue((int)EBBKey.StarDistSqr, float.MaxValue);
            }

            float starDistSqr = workingMemory.GetValue<float>((int)EBBKey.StarDistSqr, float.MaxValue);
            bool hasStar = bestStar != null;

            var enemy = Match.instance.GetOppositeTank(tank.Team);
            float enemyStarDistSqr = float.MaxValue;
            bool enemyCloserToStar = false;
            if (enemy != null && !enemy.IsDead && bestStar != null)
            {
                enemyStarDistSqr = (enemy.Position - bestStar.Position).sqrMagnitude;
                enemyCloserToStar = enemyStarDistSqr + 0.1f < bestDistSqr;
            }

            bool lowHp = tank.HP < 41;
            float wHeal = WeightHeal(tank);
            float wEvade = WeightEvade(tank, starDistSqr);
            bool hasIncoming = HasThreateningMissile(tank, out _);
            float wSuperPre = WeightSuperPre();
            float wAtk = WeightAttack(tank);
            float wStar = WeightStar(tank, hasStar, starDistSqr);
            float wAvoid = WeightAvoid(tank, starDistSqr);
            float wCombat = WeightCombatStrafe(tank);
            bool isSuperStarTarget = workingMemory.GetValue<bool>((int)EBBKey.IsSuperStarTarget, false);
            int starCount = stars.Count;

            if (enemy != null && !enemy.IsDead)
            {
                float dmg = Match.instance.GlobalSetting.DamagePerHit;
                if (tank.HP + dmg < enemy.HP)
                {
                    wAtk *= 0.35f;
                    wCombat *= 0.5f;
                    wAvoid *= 1.35f;
                    wEvade *= 1.2f;
                }
            }

            if (hasIncoming)
                wEvade = 999f;

            if (hasStar)
            {
                float countBoost = Mathf.Lerp(1.0f, 1.8f, Mathf.Clamp01((starCount - 1) / 6f));
                wStar *= 1.4f * countBoost;
                wAtk *= 0.6f;
                wCombat *= 0.7f;
            }

            if (isSuperStarTarget)
            {
                wStar *= 1.6f;
                wEvade *= 0.55f;
                wAtk *= 0.7f;
                wCombat *= 0.8f;
            }

            if (enemyCloserToStar)
            {
                wAtk *= 1.35f;
                wCombat *= 1.25f;
                wStar *= 0.8f;
                wEvade *= 0.9f;
            }
            else
            {
                wAtk *= 0.85f;
                wCombat *= 0.9f;
                wStar *= 1.25f;
                wEvade *= 1.15f;
            }
            float wPatrol = hasStar ? 0.2f : 0.05f;

            Vector3 target;
            bool selectedEvade = false;

            float bestW = wPatrol;
            target = PickPatrol(tank);

            if (wHeal > bestW)
            {
                bestW = wHeal;
                target = Match.instance.GetRebornPos(tank.Team);
            }

            if (wEvade > bestW)
            {
                bestW = wEvade;
                target = PickEvadeTarget(tank, lowHp);
                selectedEvade = true;
            }

            if (wCombat > bestW)
            {
                bestW = wCombat;
                target = PickCombatStrafePos(tank, workingMemory);
            }

            if (wSuperPre > bestW)
            {
                bestW = wSuperPre;
                target = Vector3.zero;
            }

            if (wAtk > bestW)
            {
                bestW = wAtk;
                target = PickAttackTarget(tank);
            }

            if (wStar > bestW)
            {
                bestW = wStar;
                target = bestStar.Position;
            }

            if (wAvoid > bestW)
            {
                bestW = wAvoid;
                target = PickAvoidTarget(tank);
            }

            if (workingMemory.TryGetValue((int)EBBKey.LastMoveTargetPos, out Vector3 lastTarget))
            {
                float diffSqr = (target - lastTarget).sqrMagnitude;
                if (diffSqr < 1.5f * 1.5f)
                    target = lastTarget;
            }

            float lockSeconds = LockSeconds;
            if (selectedEvade)
                lockSeconds = lowHp ? 0.1f : 0.14f;

            workingMemory.SetValue((int)EBBKey.MovingTargetPos, target);
            workingMemory.SetValue((int)EBBKey.LastMoveTargetPos, target);
            workingMemory.SetValue((int)EBBKey.TargetLockUntil, Time.time + lockSeconds);

            return ERunningStatus.Finished;
        }

        private static float WeightCombatStrafe(Tank tank)
        {
            // HP 低于强制回血时不进入对拼绕圈
            if (tank.HP <= 40)
                return 0f;

            var enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null || enemy.IsDead)
                return 0f;

            if (!tank.CanSeeOthers(enemy))
                return 0f;

            float distSqr = (enemy.Position - tank.Position).sqrMagnitude;
            // 中近距离（约 7~22）适合贴身压制
            if (distSqr < 7f * 7f || distSqr > 22f * 22f)
                return 0f;

            return 4.2f;
        }

        private static Vector3 PickCombatStrafePos(Tank tank, BlackboardMemory workingMemory)
        {
            var enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null)
                return tank.Position;

            // 选择稳定的侧向（+1/-1），避免每帧翻转造成抽搐
            int side = workingMemory.GetValue<int>((int)EBBKey.StrafeSide, 0);
            if (side == 0)
            {
                side = (Random.value > 0.5f) ? 1 : -1;
                workingMemory.SetValue((int)EBBKey.StrafeSide, side);
            }

            Vector3 toEnemy = enemy.Position - tank.Position;
            toEnemy.y = 0;
            if (toEnemy.sqrMagnitude < 0.001f)
                toEnemy = tank.Forward;
            toEnemy.Normalize();

            // 侧向方向（绕圈）
            Vector3 lateral = Vector3.Cross(Vector3.up, toEnemy) * side;
            lateral.y = 0;
            if (lateral.sqrMagnitude < 0.001f)
                lateral = Vector3.Cross(Vector3.up, tank.Forward) * side;
            lateral.Normalize();

            // 维持更近的压制半径
            float desired = 11f;
            float curDist = Vector3.Distance(tank.Position, enemy.Position);

            // 轻微径向纠偏：太近就退一点，太远就靠一点
            Vector3 radial = -toEnemy;
            float radialW = Mathf.Clamp((desired - curDist) * 0.12f, -0.6f, 0.6f);

            Vector3 dir = (lateral + radial * radialW);
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f)
                dir = lateral;
            dir.Normalize();

            return tank.Position + dir * 6.0f;
        }

        private static float WeightHeal(Tank tank)
        {
            if (tank.HP <= 40)
                return 10f;
            if (tank.HP < 70)
                return 2.0f;
            return 0f;
        }

        private static float WeightEvade(Tank tank, float starDistSqr)
        {
            float starFactor;
            if (starDistSqr >= FarStarDistSqr) starFactor = 1.0f;
            else if (starDistSqr <= NearStarDistSqr) starFactor = 0.4f;
            else starFactor = Mathf.Lerp(0.4f, 1.0f, (starDistSqr - NearStarDistSqr) / (FarStarDistSqr - NearStarDistSqr));

            float hpFactor = tank.HP < 41 ? 2.4f : (tank.HP >= 70 ? 0.8f : 1.0f);

            var missiles = Match.instance.GetOppositeMissiles(tank.Team);
            foreach (var kv in missiles)
            {
                if (kv.Value != null && IsThreateningForWeight(kv.Value, tank))
                    return 7f * starFactor * hpFactor;
            }
            return 0f;
        }

        private static bool IsThreateningForWeight(Missile missile, Tank tank)
        {
            Vector3 toTank = tank.Position - missile.Position;
            toTank.y = 0;
            if (toTank.sqrMagnitude > 45f * 45f)
                return false;
            Vector3 vel = missile.Velocity;
            vel.y = 0;
            float speed = vel.magnitude;
            if (speed < 0.01f)
                return false;
            Vector3 dir = vel / speed;
            float forwardDot = Vector3.Dot(toTank, dir);
            if (forwardDot < 0.1f)
                return false;

            float closestSqr = toTank.sqrMagnitude - forwardDot * forwardDot;
            float hitRadius = 6.5f;
            if (closestSqr > hitRadius * hitRadius)
                return false;

            float maxCheckDist = Mathf.Min(forwardDot + 8f, 90f);
            if (Physics.Raycast(missile.Position, dir, maxCheckDist, PhysicsUtils.LayerMaskScene))
                return false;

            float timeToClosest = forwardDot / speed;
            if (timeToClosest > 4.5f)
                return false;

            return Vector3.Angle(vel, toTank) < 65f;
        }

        private static bool HasThreateningMissile(Tank tank, out Missile missile)
        {
            missile = null;
            float bestScore = float.MaxValue;
            var missiles = Match.instance.GetOppositeMissiles(tank.Team);
            foreach (var kv in missiles)
            {
                var m = kv.Value;
                if (m == null || !IsThreateningForWeight(m, tank))
                    continue;

                float dist = Vector3.SqrMagnitude(m.Position - tank.Position);
                if (dist < bestScore)
                {
                    bestScore = dist;
                    missile = m;
                }
            }

            return missile != null;
        }

        private static float WeightSuperPre()
        {
            float spawnTime = Match.instance.GlobalSetting.MatchTime * 0.5f;
            float remainingTime = Match.instance.RemainingTime;
            float diff = remainingTime - spawnTime;
            if (remainingTime <= 90f && remainingTime > 70f)
                return 8.0f;
            if (diff > 0f && diff < 12.0f)
                return 6.5f;
            return 0f;
        }

        private static float WeightAttack(Tank tank)
        {
            var enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null || enemy.IsDead)
                return 0f;

            float dmg = Match.instance.GlobalSetting.DamagePerHit;
            bool enemyWeak = enemy.HP <= 2 * dmg;
            bool haveAdvantage = tank.HP >= enemy.HP + 2 * dmg;

            if (tank.HP < 45)
                return 0f;

            Vector3 enemyHome = Match.instance.GetRebornPos(enemy.Team);
            bool enemyNearHome = Vector3.SqrMagnitude(enemy.Position - enemyHome) < 200.0f;

            float distSqr = (enemy.Position - tank.Position).sqrMagnitude;
            if (distSqr > 30f * 30f)
                return 0f;

            float hpBoost = tank.HP >= 70 ? 1.25f : 1.0f;

            if (enemyWeak && enemyNearHome)
                return 3.6f * hpBoost;
            if (enemyWeak)
                return 3.0f * hpBoost;
            if (haveAdvantage)
                return 2.2f * hpBoost;
            return 0f;
        }

        private static float WeightStar(Tank tank, bool hasStar, float starDistSqr)
        {
            if (!hasStar)
                return 0f;

            float hpBoost = tank.HP >= 70 ? 1.2f : 1.0f;

            if (starDistSqr <= NearStarDistSqr)
                return 5.0f * hpBoost;
            if (starDistSqr >= FarStarDistSqr)
                return 2.2f * hpBoost;

            float t = (starDistSqr - NearStarDistSqr) / (FarStarDistSqr - NearStarDistSqr);
            return Mathf.Lerp(5.0f, 2.2f, t) * hpBoost;
        }

        private static float WeightAvoid(Tank tank, float starDistSqr)
        {
            var enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null || enemy.IsDead)
                return 0f;

            float distSqr = (enemy.Position - tank.Position).sqrMagnitude;
            if (distSqr < 10f * 10f)
                return 4.0f;

            if (tank.HP < 50 && distSqr < 16f * 16f)
                return 3.0f;

            if (starDistSqr <= NearStarDistSqr)
                return 1.0f;

            return 0f;
        }

        private static Vector3 PickEvadeTarget(Tank tank, bool lowHp)
        {
            if (!HasThreateningMissile(tank, out Missile best))
                return tank.Position + tank.Forward * 2f;

            var lateral = Vector3.Cross(best.Velocity, Vector3.up);
            lateral.y = 0;
            if (lateral.sqrMagnitude < 0.001f)
                return tank.Position + tank.Forward * 2f;

            lateral.Normalize();
            if (Vector3.Cross(best.Velocity, tank.Position - best.Position).y > 0)
                lateral *= -1f;

            Vector3 missileDir = best.Velocity;
            missileDir.y = 0;
            if (missileDir.sqrMagnitude > 0.001f)
                missileDir.Normalize();

            float step = lowHp ? 6.8f : 5.5f;
            Vector3 forward = tank.Forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 back = -missileDir;
            return tank.Position + lateral * step + forward * 0.8f + back * 1.2f;
        }

        private static Vector3 PickAttackTarget(Tank tank)
        {
            var enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null)
                return tank.Position;

            Vector3 predictedPos = enemy.Position;
            Vector3 enemyVel = enemy.Velocity;
            enemyVel.y = 0;
            if (enemyVel.sqrMagnitude > 0.5f * 0.5f)
                predictedPos += enemyVel * 0.6f;

            Vector3 toEnemy = predictedPos - tank.Position;
            toEnemy.y = 0;
            if (toEnemy.sqrMagnitude > 0.001f)
            {
                float dist = toEnemy.magnitude;
                if (dist < 12f)
                    return tank.Position + toEnemy.normalized * 3.5f;
            }
            if (Physics.Linecast(tank.Position, predictedPos, PhysicsUtils.LayerMaskScene))
            {
                int side = Random.value > 0.5f ? 1 : -1;
                Vector3 lateral = Vector3.Cross(Vector3.up, toEnemy).normalized * side;
                if (lateral.sqrMagnitude < 0.001f)
                    lateral = Vector3.Cross(Vector3.up, tank.Forward).normalized * side;
                return tank.Position + lateral * 5f + toEnemy.normalized * 3f;
            }
            return predictedPos;
        }

        private static Vector3 PickAvoidTarget(Tank tank)
        {
            var enemy = Match.instance.GetOppositeTank(tank.Team);
            if (enemy == null)
                return tank.Position;

            Vector3 away = tank.Position - enemy.Position;
            away.y = 0;
            if (away.sqrMagnitude < 0.001f)
                away = -tank.Forward;
            away.Normalize();

            Vector3 home = Match.instance.GetRebornPos(tank.Team);
            Vector3 toHome = home - tank.Position;
            toHome.y = 0;
            if (toHome.sqrMagnitude > 0.001f)
                toHome.Normalize();

            Vector3 dir = (away * 0.7f + toHome * 0.3f);
            if (dir.sqrMagnitude < 0.001f)
                dir = away;
            dir.Normalize();

            float step = (tank.HP < 50) ? 12f : 9f;
            return tank.Position + dir * step;
        }

        private static Vector3 PickPatrol(Tank tank)
        {
            float half = Match.instance.FieldSize * 0.5f;
            Vector3 home = Match.instance.GetRebornPos(tank.Team);

            Vector3 best = Vector3.zero;
            float bestDist = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(Random.Range(-half, half), 0, Random.Range(-half, half));
                float d = (p - home).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }
    }

    sealed class MoveToBlackboardPos : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
            => workingMemory.HasValue((int)EBBKey.MovingTargetPos);

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            var tank = (Tank)agent;
            tank.Move(workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos));
            return ERunningStatus.Finished;
        }
    }

    public class MyTank : Tank
    {
        private BlackboardMemory m_Memory;
        private Node m_Root;

        public override string GetName() => "FY";

        protected override void OnStart()
        {
            base.OnStart();
            m_Memory = new BlackboardMemory();

            if (transform.childCount > 1)
                m_Memory.SetValue((int)EBBKey.TurretTF, transform.GetChild(1));

            m_Root = new ParallelNode(1).AddChild(
                new TurnTurretPredict(),
                new FireWhenAligned(),
                new SequenceNode().AddChild(
                    new WeightedMovementDecision(),
                    new MoveToBlackboardPos()
                ));
        }

        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_Root, this, m_Memory);
        }
    }
}

