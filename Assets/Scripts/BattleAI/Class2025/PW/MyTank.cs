using AI.Base;
using AI.RuleBased;
using Main;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace PW
{
    /*🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀*/
    public class MyTank : Tank
    {
        // AI内部状态和参数
        private Vector3 lastEnemyVelocity;
        private float timeSinceLastPersonalShotDecision;

        // 躲避相关的参数
        private const float DODGE_CHECK_RADIUS = 12f;
        private const float DODGE_DISTANCE = 7.5f;
        private Vector3 preferredDodgeDirection = Vector3.right;
        private float lastDodgeDirectionChangeTime = 0f;
        private const float DODGE_DIRECTION_CHANGE_INTERVAL = 3.5f;

        // AI决策的入口Condition实例
        private Condition _shouldAttackEnemy;
        private Condition _shouldGoHome;
        private Condition _shouldGoToStar;
        private Condition _shouldDodgeMissileSayoVer;
        private Condition _shouldGoToSuperStar;

        public int targetStarIndex = -1;
        public Vector3 currentGoodPosition = Vector3.zero;

        public float ASSUMED_TANK_RADIUS = 5.5f;

        protected override void OnStart()
        {
            base.OnStart(); // 调用基类的OnStart
            lastEnemyVelocity = Vector3.zero;

            // 初始化
            _shouldAttackEnemy = new ShouldAttackEnemy();
            _shouldGoHome = new ShouldGoHome();
            _shouldGoToStar = new ShouldGoToStar(this);
            _shouldDodgeMissileSayoVer = new WillHitPredictedMissileSayoVer(ASSUMED_TANK_RADIUS);
            _shouldGoToSuperStar = new CheckSuperStarStatus(SuperStarStatus.AboutToGenerate);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            timeSinceLastPersonalShotDecision += Time.deltaTime;

            ChooseGoodPositionSmartly();

            FindBestStarToGet();

            Tank oppTank = Match.instance.GetOppositeTank(this.Team);

            TurretControl(oppTank);

            if (_shouldDodgeMissileSayoVer.IsTrue(this))
            {
                ExecuteDodgeManeuver();
                Debug.Log("sayo：躲导弹");
            }

            else if (_shouldGoToSuperStar.IsTrue(this))
            {
                this.Move(Vector3.zero);
                Debug.Log("sayo：抢超级星星");
            }

            else if (_shouldAttackEnemy.IsTrue(this) && oppTank != null && !oppTank.IsDead)
            {
                this.Move(oppTank.Position);
                Debug.Log("sayo：打敌人");
            }

            else if (_shouldGoHome.IsTrue(this))
            {

                this.Move(Match.instance.GetRebornPos(this.Team));
                Debug.Log("sayo：回家");
            }

            else if (_shouldGoToStar.IsTrue(this) && this.targetStarIndex != -1 && Match.instance.GetStars().ContainsKey(this.targetStarIndex))
            {
                Star targetStarInstance = Match.instance.GetStarByID(this.targetStarIndex);
                if (targetStarInstance != null) // 再次确认星星还在
                {
                    this.Move(targetStarInstance.Position);
                    Debug.Log("sayo：捡普通星星");
                }
            }

            else
            {
                this.Move(Vector3.zero);
                Debug.Log("sayo：没事做了");
            }


            if (oppTank != null)
            {
                lastEnemyVelocity = oppTank.Velocity;
            }
            else
            {
                lastEnemyVelocity = Vector3.zero; // 如果敌人没了，速度当然也归零
            }
        }

        private void FindBestStarToGet()
        {
            this.targetStarIndex = -1;
            float highestScore = float.MinValue; // 用来评估哪个星星最“值得”去拿

            var allStarsOnMap = Match.instance.GetStars();
            if (allStarsOnMap.Count == 0) return; // 场上没星星，直接返回

            Tank opponentTank = Match.instance.GetOppositeTank(this.Team);

            foreach (var starEntry in allStarsOnMap)
            {
                if (starEntry.Value.IsSuperStar) continue; // 超级星星由专门的逻辑处理

                Star currentStar = starEntry.Value;
                float myDistanceToStar = (this.Position - currentStar.Position).magnitude;
                float opponentDistanceToStar = float.MaxValue; // 先假设敌人离无限远或者不存在

                if (opponentTank != null && !opponentTank.IsDead)
                {
                    opponentDistanceToStar = (opponentTank.Position - currentStar.Position).magnitude;
                }


                float distanceAdvantageScore = opponentDistanceToStar - myDistanceToStar;


                float myReachCostScore = -myDistanceToStar * 0.3f;

                float enemyInterferencePenalty = 0f;
                if (opponentDistanceToStar < myDistanceToStar + ASSUMED_TANK_RADIUS * 3f)
                {
                    enemyInterferencePenalty = -5f;
                }


                float totalScoreForThisStar = distanceAdvantageScore + myReachCostScore + enemyInterferencePenalty;


                if ((distanceAdvantageScore > ASSUMED_TANK_RADIUS && totalScoreForThisStar > highestScore) || (HP > opponentTank.HP))
                {
                    highestScore = totalScoreForThisStar;
                    this.targetStarIndex = starEntry.Key;
                }
            }
        }

        private void ChooseGoodPositionSmartly()
        {
            if (new CheckSuperStarStatus(SuperStarStatus.AboutToGenerate).IsTrue(this))
            {
                Vector3 superStarAnticipationPoint;
                if (Match.instance.GetRebornPos(this.Team).x > 0)
                {
                    superStarAnticipationPoint = new Vector3(10f, 0, Random.Range(-5f, 5f));
                }
                else // 我家在地图左边
                {
                    superStarAnticipationPoint = new Vector3(-10f, 0, Random.Range(-5f, 5f));
                }
                this.currentGoodPosition = superStarAnticipationPoint;
                return;
            }

            if (!(new CheckSuperStarStatus(SuperStarStatus.AboutToGenerate).IsTrue(this)))
            {
                this.currentGoodPosition = Vector3.zero;
            }
        }

        private void TurretControl(Tank opponentTank)
        {
            if (opponentTank != null && !opponentTank.IsDead)
            {
                Vector2 oppPositionVec2 = new Vector2(opponentTank.Position.x, opponentTank.Position.z);
                Vector2 oppVelocityVec2 = new Vector2(opponentTank.Velocity.x, opponentTank.Velocity.z);
                Vector2 myFirePositionVec2 = new Vector2(this.FirePos.x, this.FirePos.z);
                Vector2 deltaPositionVec2 = oppPositionVec2 - myFirePositionVec2;

                float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
                float a_coeff = oppVelocityVec2.sqrMagnitude - (missileSpeed * missileSpeed);
                float b_coeff = 2 * Vector2.Dot(deltaPositionVec2, oppVelocityVec2);
                float c_coeff = deltaPositionVec2.sqrMagnitude;

                float discriminant = (b_coeff * b_coeff) - (4 * a_coeff * c_coeff);

                Vector3 targetDirectionVec3;

                if (discriminant >= 0 && Mathf.Abs(a_coeff) > 0.001f)
                {
                    float predictedTime = (-b_coeff - Mathf.Sqrt(discriminant)) / (2 * a_coeff);
                    if (predictedTime < 0)
                    {
                        predictedTime = (-b_coeff + Mathf.Sqrt(discriminant)) / (2 * a_coeff);
                    }
                    if (predictedTime < 0)
                    {
                        targetDirectionVec3 = (opponentTank.Position - this.FirePos).normalized;
                    }
                    else
                    {
                        Vector2 predictedTargetVec2 = deltaPositionVec2 + oppVelocityVec2 * predictedTime;
                        targetDirectionVec3 = new Vector3(predictedTargetVec2.x, 0, predictedTargetVec2.y).normalized; // 得到3D射击方向                                                                                              // Debug.Log($"[{GetName()}] Prora's Prediction: Time={predictedTime:F2}s, TargetDir={targetDirectionVec3}");
                    }
                }
                else
                {
                    targetDirectionVec3 = (opponentTank.Position - this.FirePos).normalized;
                }

                this.TurretTurnTo(this.Position + targetDirectionVec3);

                if ((this.Position - opponentTank.Position).magnitude < 15f)
                {
                    if (this.CanFire()) this.Fire();
                }

                else if (Physics.SphereCast(this.FirePos, 0.24f, targetDirectionVec3, out RaycastHit hit,
                                          (opponentTank.Position - this.FirePos).magnitude - 1f,
                                          PhysicsUtils.LayerMaskTank))
                {
                    FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                    if (fireCollider != null && fireCollider.Owner == opponentTank)
                    {
                        if (Vector3.Angle(this.TurretAiming, targetDirectionVec3) < 2f && this.CanFire())
                            this.Fire();
                    }
                }
                else
                {
                    if (Vector3.Angle(this.TurretAiming, targetDirectionVec3) < 2f && this.CanFire())
                        this.Fire();
                }
            }
            else
            {
                this.TurretTurnTo(this.Position + this.Forward);
            }
        }



        private void ExecuteDodgeManeuver()
        {
            Vector3 bestDodgeDirection = Vector3.zero;
            Missile mostImminentThreat = FindMostThreateningMissileDetails();

            if (mostImminentThreat != null)
            {

                Vector3 missileIncomingVelocity = mostImminentThreat.Velocity;

                Vector3 dodgeDirOption1 = new Vector3(missileIncomingVelocity.z, 0, -missileIncomingVelocity.x).normalized;
                Vector3 dodgeDirOption2 = -dodgeDirOption1;

                if (Time.time - lastDodgeDirectionChangeTime > DODGE_DIRECTION_CHANGE_INTERVAL)
                {
                    preferredDodgeDirection = Random.value > 0.5f ? this.transform.right : -this.transform.right;
                    lastDodgeDirectionChangeTime = Time.time;
                }

                bestDodgeDirection = Vector3.Dot(dodgeDirOption1, preferredDodgeDirection) > Vector3.Dot(dodgeDirOption2, preferredDodgeDirection) ? dodgeDirOption1 : dodgeDirOption2;

            }
            else
            {
                if (Time.time - lastDodgeDirectionChangeTime > DODGE_DIRECTION_CHANGE_INTERVAL)
                {
                    preferredDodgeDirection = Random.value > 0.5f ? this.transform.right : -this.transform.right; // 随机向左或向右
                    lastDodgeDirectionChangeTime = Time.time;
                }
                bestDodgeDirection = preferredDodgeDirection;
            }

            if (bestDodgeDirection != Vector3.zero)
            {
                Vector3 dodgeTargetPosition = this.Position + bestDodgeDirection * DODGE_DISTANCE;
                this.Move(dodgeTargetPosition);
            }
            else
            {
                this.Move(this.Position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)));
            }
        }


        private Missile FindMostThreateningMissileDetails()
        {
            Missile mostThreatening = null;
            float closestTimeToImpact = float.MaxValue; // 记录预计的最近碰撞时间

            var incomingMissiles = Match.instance.GetOppositeMissiles(this.Team);
            foreach (var missileEntry in incomingMissiles)
            {
                Missile currentMissile = missileEntry.Value;
                Vector3 missilePos = currentMissile.Position;
                Vector3 missileVelNorm = currentMissile.Velocity.normalized;
                float missileSpeed = currentMissile.Velocity.magnitude;

                if (missileSpeed < 0.1f) continue;

                Vector3 vecTankToMissile = this.Position - missilePos;
                float dotProd = Vector3.Dot(vecTankToMissile, missileVelNorm);
                if (dotProd <= 0) continue; // 导弹背向我飞

                float timeToClosest = dotProd / missileSpeed;

                // MySlyTank_BasedOnYourTank.DODGE_CHECK_RADIUS
                if (timeToClosest < 0.05f || timeToClosest > (DODGE_CHECK_RADIUS / missileSpeed) + 0.8f) continue;

                Vector3 closestPtOnTraj = missilePos + missileVelNorm * timeToClosest * missileSpeed;
                float distSqToClosestPt = (this.Position - closestPtOnTraj).sqrMagnitude;
                float collisionRadius = ASSUMED_TANK_RADIUS + 0.25f; // 0.25f是假设的导弹半径
                float collisionThreshSq = collisionRadius * collisionRadius;

                if (distSqToClosestPt < collisionThreshSq) // 如果预测会撞上
                {
                    if (timeToClosest < closestTimeToImpact) // 并且这个导弹比之前记录的更“迫在眉睫”
                    {
                        closestTimeToImpact = timeToClosest;
                        mostThreatening = currentMissile;
                    }
                }
            }
            return mostThreatening; // 返回那个最快要撞上来的导弹，或者null（如果没有威胁）
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            targetStarIndex = -1; // 重生后目标星星清空
            lastEnemyVelocity = Vector3.zero; // 敌人速度信息也清空
            Debug.Log("复！活！");
        }

        public override string GetName()
        {
            return "PW";
        }
    }
    /*🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀*/
    public enum SuperStarStatus
    {
        None,
        Preparing,
        AboutToGenerate,
        Generated
    }


    class CheckSuperStarStatus : Condition
    {
        private SuperStarStatus _expectedStatus; // 期望星星是哪个状态
        public CheckSuperStarStatus(SuperStarStatus expected) { _expectedStatus = expected; }

        public override bool IsTrue(IAgent agent)
        {
            float time = Match.instance.RemainingTime; // 拿到游戏剩余时间
            SuperStarStatus currentStatus = SuperStarStatus.None; // 先假设啥也没有

            // 优先级最高：直接看场上有没有超级星星实例
            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value.IsSuperStar)
                {
                    currentStatus = SuperStarStatus.Generated;
                    return currentStatus == _expectedStatus; // 找到了就直接判断返回
                }
            }

            // 如果场上没有，才根据时间来判断
            if (time <= 0)
            {
                currentStatus = SuperStarStatus.None;
            }

            else if (time < 90)
            {
                currentStatus = SuperStarStatus.Generated;
            }
            else if (time <= 100) // 100秒到90秒之间（包含100和90）
            {
                currentStatus = SuperStarStatus.AboutToGenerate;
            }
            else if (time < 110)  // 110秒到100秒之间（不含110，含100）
            {
                currentStatus = SuperStarStatus.Preparing;
            }

            return currentStatus == _expectedStatus; // 最后比较一下是不是期望的状态
        }
    }

    class ShouldAttackEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);

            if (oppTank == null || oppTank.IsDead) return false;

            // 现在是不是Gank敌人的好时机？
            bool iCanResistMore = (myTank.HP / 20f) > (oppTank.HP / 20f); // 我比他能抗 (20是每次攻击伤害)
            bool enemyIsFarFromHome = (oppTank.Position - Match.instance.GetRebornPos(oppTank.Team)).magnitude > 20f; // 敌人离家远
            bool enemyIsNearMe = (myTank.Position - oppTank.Position).magnitude < 15f; // 敌人离我近

            bool isGoodTimeToGank = iCanResistMore && enemyIsFarFromHome && enemyIsNearMe;

            if (new CheckSuperStarStatus(SuperStarStatus.Preparing).IsTrue(agent) && isGoodTimeToGank)
            {
                return true;
            }

            bool notSuperStarCriticalPrep = new NotCondition(new CheckSuperStarStatus(SuperStarStatus.Preparing)).IsTrue(agent) &&
                                          new NotCondition(new CheckSuperStarStatus(SuperStarStatus.AboutToGenerate)).IsTrue(agent);
            if (notSuperStarCriticalPrep && isGoodTimeToGank)
            {
                return true;
            }

            return false; // 其他情况，先怂一波
        }
    }


    class ShouldGoHome : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            float myHPRatio = (float)myTank.HP / Match.instance.GlobalSetting.MaxHP;

            bool canOnlyResistOneHit = myHPRatio <= 0.20f;

            bool enemyIsNear = false;
            bool iAmWeakerThanEnemy = false;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if (oppTank != null && !oppTank.IsDead)
            {
                enemyIsNear = (myTank.Position - oppTank.Position).magnitude < 15f;

                float myHitsCanTake = myTank.HP / Match.instance.GlobalSetting.DamagePerHit;
                float oppHitsCanTake = oppTank.HP / Match.instance.GlobalSetting.DamagePerHit;
                iAmWeakerThanEnemy = myHitsCanTake <= oppHitsCanTake;
            }

            if (oppTank.IsDead && (Match.instance.GetStars().Count == 0 || myTank.HP < 80))
            {
                return true;
            }


            if (canOnlyResistOneHit)
            {
                return true;
            }
            // 敌人骑脸，而且还打不过，溜了溜了
            if (enemyIsNear && iAmWeakerThanEnemy)
            {
                return true;
            }

            //为了准备超级星星，回家补满状态
            bool superstarIsPreparing = new CheckSuperStarStatus(SuperStarStatus.Preparing).IsTrue(agent);
            bool notFullHP = myTank.HP < Match.instance.GlobalSetting.MaxHP;


            bool enemyAliveAndKicking = (oppTank != null && !oppTank.IsDead);
            if (superstarIsPreparing && notFullHP && enemyAliveAndKicking)
            {

                return true;
            }

            return false; // 其他情况，还能继续浪
        }
    }


    class ShouldGoToStar : Condition
    {
        private MyTank _slyTankInstance;
        public ShouldGoToStar(MyTank tankInstance) { _slyTankInstance = tankInstance; }

        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            if (Match.instance.GetStars().Count == 0) return false;

            if (_slyTankInstance.targetStarIndex == -1) return false;

            Star targetStar = Match.instance.GetStarByID(_slyTankInstance.targetStarIndex);
            if (targetStar == null || targetStar.IsSuperStar) return false;

            bool notSuperStarCriticalTime = new NotCondition(new CheckSuperStarStatus(SuperStarStatus.Preparing)).IsTrue(agent) &&
                                            new NotCondition(new CheckSuperStarStatus(SuperStarStatus.AboutToGenerate)).IsTrue(agent);


            bool iHaveAdvantageForThisStar = true;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if (oppTank != null && !oppTank.IsDead)
            {
                float myDistToStar = (myTank.Position - targetStar.Position).magnitude;
                float oppDistToStar = (oppTank.Position - targetStar.Position).magnitude;
                iHaveAdvantageForThisStar = myDistToStar < (oppDistToStar - _slyTankInstance.ASSUMED_TANK_RADIUS * 2f);
            }

            if (notSuperStarCriticalTime && iHaveAdvantageForThisStar)
            {

                return true;
            }

            return false;
        }
    }


    class WillHitPredictedMissileSayoVer : Condition
    {
        private float _tankRadius;
        public WillHitPredictedMissileSayoVer(float tankRadius) { _tankRadius = tankRadius; }

        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Dictionary<int, Missile> incomingMissiles = Match.instance.GetOppositeMissiles(myTank.Team);

            foreach (var pair in incomingMissiles)
            {
                Missile missile = pair.Value;
                Vector3 missilePos = missile.Position;
                Vector3 missileVelNorm = missile.Velocity.normalized;
                float missileSpeed = missile.Velocity.magnitude;


                Vector3 vecTankToMissileStart = myTank.Position - missilePos;

                float dotProduct = Vector3.Dot(vecTankToMissileStart, missileVelNorm);
                if (dotProduct <= 0) continue;

                float timeToClosestApproach = dotProduct / missileSpeed; // 到达轨迹最近点的时间

                if (timeToClosestApproach < 0.05f || timeToClosestApproach > (12f / missileSpeed) + 0.8f)
                {
                    continue;
                }

                Vector3 closestPointOnTrajectory = missilePos + missileVelNorm * timeToClosestApproach * missileSpeed;

                float distanceSqToClosestPoint = (myTank.Position - closestPointOnTrajectory).sqrMagnitude;

                float collisionRadiusSum = _tankRadius + 0.25f;
                float collisionThresholdSq = collisionRadiusSum * collisionRadiusSum;

                if (distanceSqToClosestPoint < collisionThresholdSq)
                {

                    return true;
                }
            }
            return false; // 可以继续横着走！
        }
    }
}
/*🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀🦀*/
