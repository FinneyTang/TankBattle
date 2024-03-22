using System.Collections.Generic;
using Main;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.Base;
using UnityEngine;
using UnityEngine.AI;

namespace MokutkZ
{
    class TankActionSelectorNode : SpecifySelectorNode
    {
        private TankActionType mAppropriateTankActionType;
        private bool mIsInit;
        private Dictionary<TankActionType, ItankNode> mTankActionNodes = new Dictionary<TankActionType, ItankNode>();

        protected override ERunningStatus OnUpdate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (!mIsInit)
            {
                foreach (var child in m_Children)
                {
                    if (child is ItankNode node)
                    {
                        node.Init();
                        mTankActionNodes.Add(node.TankActionType, node);
                    }
                }

                mIsInit = true;
            }

            SpecifyMethod(workingMemory);
         //   Debug.Log(mAppropriateTankActionType);
            return ((Node)mTankActionNodes[mAppropriateTankActionType]).Update(agent, workingMemory);
        }

        protected override void OnReset(IAgent agent, BlackboardMemory workingMemory)
        {

        }

        public override void SpecifyMethod(BlackboardMemory workingMemory)
        {
            mAppropriateTankActionType = GetAppropriateTankActionType(workingMemory);
        }

        TankActionType GetAppropriateTankActionType(BlackboardMemory workingMemory)
        {
            AllData allData = workingMemory.GetValue<AllData>(0);

            var appropriateTankActionType = TankActionType.Pursuit;
            if (allData.BackAndHealingExpectation >= allData.VictoryExpectation)
                appropriateTankActionType = TankActionType.BackToHeal;
            if (allData.CanGetStarExpectation >= allData.BackAndHealingExpectation && appropriateTankActionType == TankActionType.BackToHeal)
                appropriateTankActionType = TankActionType.MoveToEatStar;
            if (allData.DodgeExpectation >= allData.CanGetStarExpectation && appropriateTankActionType == TankActionType.MoveToEatStar)
                appropriateTankActionType = TankActionType.DodgingShell;

            return appropriateTankActionType;
        }
    }

    class Fire : ActionNode, ItankNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;

            return t.CanFire();
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            AllData allData = workingMemory.GetValue<AllData>(0);

            var oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && !oppTank.IsDead && CanSeeTarget(t, allData, 0.3f))
            {
                t.Fire();
                allData.FireTypeCount++;
            }


            //更新射击次数
            allData.ShootNumber += 1;
            //将此次发射的炮弹加入未判定命中的队列
            var missiles = Match.instance.GetOppositeMissiles(allData.EnemyTeam);
            Missile curMissile = null;
            if (missiles != null)
            {
                foreach (var missile in missiles)
                {
                    curMissile = missile.Value;
                }

                if (curMissile)
                    allData.MissileDataList.Add(new MissileData()
                    {
                        ID = allData.ShootNumber - 1,
                        Missile = curMissile,
                        FireType = allData.FireType,
                        HitState = HitState.UnKnow
                    });
            }

            return ERunningStatus.Executing;
        }

        public TankActionType TankActionType { get; set; }

        public void Init()
        {
            TankActionType = TankActionType.Fire;
        }

        // 之前提供的CanSeeOthers方法，做了轻微调整
        bool CanSeeTarget(Tank t, AllData allData, float shellRadius)
        {
            // 计算到指定位置的方向向量
            Vector3 directionToEnemy = (allData.FireTargetPos - t.FirePos).normalized;

            // 计算左右两个射线的起始点
            Vector3 leftRayOrigin = t.FirePos - (Quaternion.Euler(0, 90, 0) * directionToEnemy * shellRadius);
            Vector3 rightRayOrigin = t.FirePos + (Quaternion.Euler(0, 90, 0) * directionToEnemy * shellRadius);

            // 计算左右两个射线的终点
            Vector3 leftRayEnd = allData.FireTargetPos - (Quaternion.Euler(0, 90, 0) * directionToEnemy * shellRadius);
            Vector3 rightRayEnd = allData.FireTargetPos + (Quaternion.Euler(0, 90, 0) * directionToEnemy * shellRadius);
            leftRayEnd += (leftRayEnd - leftRayOrigin).normalized * 90f;
            rightRayEnd += (rightRayEnd - rightRayOrigin).normalized * 90f;

            bool seeLeft;
            bool seeRight;
            if (allData.FireType == FireType.RegularFire)
            {
                // 发射左侧射线并检查是否命中
                seeLeft =
                    Physics.Linecast(leftRayOrigin, leftRayEnd, out RaycastHit hitInfoLeft,
                        PhysicsUtils.LayerMaskCollsion) &&
                    CheckFireCollider(hitInfoLeft.collider, allData.EnemyTeam);
                // 发射右侧射线并检查是否命中
                seeRight =
                    Physics.Linecast(rightRayOrigin, rightRayEnd, out RaycastHit hitInfoRight,
                        PhysicsUtils.LayerMaskCollsion) &&
                    CheckFireCollider(hitInfoRight.collider, allData.EnemyTeam);
            }
            else
            {
                // 发射左侧射线并检查是否命中
                seeLeft = (
                    Physics.Linecast(leftRayOrigin, leftRayEnd,
                        out RaycastHit hitInfoLeft1,
                        PhysicsUtils.LayerMaskCollsion) &&
                    Vector3.Distance(t.FirePos, hitInfoLeft1.point) >
                    Vector3.Distance(t.FirePos, Match.instance.GetOppositeTank(t.Team).Position)) || (Physics.Linecast(
                        leftRayOrigin, leftRayEnd, out RaycastHit hitInfoLeft2,
                        PhysicsUtils.LayerMaskCollsion) &&
                    CheckFireCollider(hitInfoLeft2.collider, allData.EnemyTeam));
                Debug.DrawLine(leftRayOrigin, leftRayEnd, Color.red);

                // 发射右侧射线并检查是否命中
                seeRight = (
                    Physics.Linecast(rightRayOrigin, rightRayEnd,
                        out RaycastHit hitInfoRight1,
                        PhysicsUtils.LayerMaskScene) &&
                    Vector3.Distance(t.FirePos, hitInfoRight1.point) >
                    Vector3.Distance(t.FirePos, Match.instance.GetOppositeTank(t.Team).Position)) || (Physics.Linecast(
                        rightRayOrigin, rightRayEnd, out RaycastHit hitInfoRight2,
                        PhysicsUtils.LayerMaskCollsion) &&
                    CheckFireCollider(hitInfoRight2.collider, allData.EnemyTeam));
                Debug.DrawLine(rightRayOrigin, rightRayEnd, Color.blue);

            }

            // 如果两边都看到目标位置，则返回true
            return seeLeft && seeRight;
        }

        bool CheckFireCollider(Collider collider, ETeam enemyTeam)
        {
            // 检查碰撞体是否为可以射击的对象
            if (PhysicsUtils.IsFireCollider(collider))
            {
                // 获取FireCollider组件
                FireCollider fc = collider.GetComponent<FireCollider>();
                // 如果有FireCollider组件，并且拥有者是敌方队伍，则可以射击
                if (fc != null && fc.Owner != null && fc.Owner.Team == enemyTeam)
                {
                    return true;
                }
            }

            return false;
        }

    }

    class TurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            AllData allData = workingMemory.GetValue<AllData>(0);

            if (oppTank && oppTank.IsDead == false)
            {
                t.TurretTurnTo(allData.FireTargetPos);
            }
            else
            {
                t.TurretTurnTo(t.Position + t.Forward);
            }


            return ERunningStatus.Executing;
        }

    }

    class Dodge : ActionNode, ItankNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            AllData allData = workingMemory.GetValue<AllData>(0);

            //Debug.Log(allData.DodgeType);

            if (allData.DodgeType == DodgeType.TangentialDodge)
            {
                t.Move(allData.DodgePos); // 切向躲避
            }

            if (allData.DodgeType == DodgeType.EmergencyStop)
            {
                t.Move(t.Position); // 急停
            }

            return ERunningStatus.Executing;
        }

        public TankActionType TankActionType { get; set; }

        public void Init()
        {
            TankActionType = TankActionType.DodgingShell;
        }
    }

    class MoveToGetStar : ActionNode, ItankNode
    {
        private Vector3 mTargetPos;

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            AllData allData = workingMemory.GetValue<AllData>(0);

            mTargetPos = allData.StarTargetPos;
            //Debug.Log("MoveToGetStar");
            t.Move(mTargetPos);
            return ERunningStatus.Executing;
        }

        public TankActionType TankActionType { get; set; }

        public void Init()
        {
            TankActionType = TankActionType.MoveToEatStar;
        }
    }

    class Pursuit : ActionNode, ItankNode
    {
        private Vector3 mTargetPos;

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;

            var enemy = Match.instance.GetOppositeTank(t.Team);
            if (enemy != null && enemy.IsDead == false)
            {
                mTargetPos = enemy.Position;
                t.Move(mTargetPos);
            }

            return ERunningStatus.Executing;
        }

        public TankActionType TankActionType { get; set; }

        public void Init()
        {
            TankActionType = TankActionType.Pursuit;
        }
    }

    class BackToHomeAndHealing : ActionNode, ItankNode
    {
        private Vector3 mTargetPos;

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;

            mTargetPos = Match.instance.GetRebornPos(t.Team);

            //Debug.Log("BackToHomeAndHealing");
            t.Move(mTargetPos);

            return ERunningStatus.Executing;
        }


        public TankActionType TankActionType { get; set; }

        public void Init()
        {
            TankActionType = TankActionType.BackToHeal;
        }
    }

    class ExpectationCalculation : ActionNode
    {
        private float timer = 0.02f;
        private float timeStep = 0.02f;

        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (timer < 0)
            {
                timer = timeStep;
                return true;
            }

            timer -= Time.deltaTime;
            return false;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            AllData allData = workingMemory.GetValue<AllData>(0);

       //     Debug.Log("---------");

            //我方命中率计算
            HitExpectationCalculation(allData);

            //开火方式决策
            FireTypeDecision(t, allData);

            // 战胜期望计算
            VictoryExpectationCalculation(t, allData);

            //吃星期望计算
            CanGetStarExpectationCalculation(t, allData);

            //回城加血期望计算
            BackAndHealingExpectationCalculation(t, allData);

            //闪避期望计算
            DodgeExpectationCalculation(t, allData);
            
//            Debug.Log("---------");


            return ERunningStatus.Executing;
        }

        #region 我方命中率计算函数

        void HitExpectationCalculation(AllData allData)
        {
            var missileDataList = allData.MissileDataList; //获取列表

            if (missileDataList.Count < 0) return;
            foreach (var missileData in missileDataList)
            {
                var missile = missileData.Missile;
                if (!missile) continue;
                List<Tank> oppTanks = Match.instance.GetOppositeTanks(missile.Team);
                Vector3 newPos = missile.Position + missile.Velocity * (Time.deltaTime * 2);
                bool missileHitEnemy = false;
                bool missileHitSomething = false;
                foreach (var oppTank in oppTanks)
                {
                    if (oppTank != null && oppTank.IsDead == false)
                    {
                        if (oppTank.IsInFireCollider(newPos))
                        {
                            missileHitEnemy = true;
                            missileHitSomething = true;

                            missileData.SetIsHit(HitState.Hit);
                        }
                    }
                }

                if (missileHitEnemy == false)
                {
                    if (Physics.Linecast(missile.Position, newPos, out var hitInfo, PhysicsUtils.LayerMaskCollsion))
                    {
                        if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                        {
                            //hit player
                            FireCollider fc = hitInfo.collider.GetComponent<FireCollider>();
                            if (fc != null && fc.Owner != null)
                            {
                                if (fc.Owner.Team != missile.Team)
                                {
                                    missileHitEnemy = true;
                                    missileHitSomething = true;

                                    missileData.SetIsHit(HitState.Hit);
                                }
                            }
                            else //未命中敌人，但打到什么东西了
                            {
                                missileHitSomething = true;

                            }
                        }

                    }
                }

                if (missileHitSomething && !missileHitEnemy)
                {
                    missileData.SetIsHit(HitState.NotHit);
                }
            }

        }

        #endregion

        #region 开火方式决策

        void FireTypeDecision(Tank t, AllData allData)
        {
            
            if( Vector3.Distance(t.Position, Match.instance.GetOppositeTank(t.Team).Position) < allData.BattleDis)
            {
                allData.FireType = FireType.RegularFire;
            }
            
            //judgeCount 远的话>2,近的话>1,还没刷新CD的话==1
            var judgeCount = Vector3.Distance(t.Position, Match.instance.GetOppositeTank(t.Team).Position) > 30 ? 2 : 1;

            if (!t.CanFire()) //距离上次开火还没刷新CD的时候如果命中了什么东西就要直接判断当前这个炮弹
            {
                if (allData.FireTypeCount == judgeCount && allData.ShootNumber == allData.FireTypeCountIndexHead)
                {
                    if (allData.MissileDataList[allData.FireTypeCountIndexHead].HitState == HitState.NotHit)
                    {
                        allData.FireType = ChangeFireType(allData.FireType);
                        allData.FireTypeCountIndexHead = allData.ShootNumber + 1;
                        allData.FireTypeCount = 0;
                    }
                }

            }

            //已经开了两炮或以上
            if (allData.FireTypeCount > judgeCount && allData.ShootNumber >= allData.FireTypeCountIndexHead)
            {
                //如果除了最后一发外都命中了，那就继续这种开火策略，否则切换开火策略
                for (int i = allData.FireTypeCountIndexHead; i < allData.MissileDataList.Count - 1; i++)
                {
                    if (allData.MissileDataList[allData.FireTypeCountIndexHead].HitState == HitState.NotHit)
                    {
                        allData.FireType = ChangeFireType(allData.FireType);
                        allData.FireTypeCountIndexHead = allData.ShootNumber + 1;
                        allData.FireTypeCount = 0;
                        break;
                    }
                }
            }

            allData.FireTargetPos = allData.FireType == FireType.RegularFire
                ? Match.instance.GetOppositeTank(t.Team).Position
                : AdvanceFireTargetPos(t, Match.instance.GetOppositeTank(t.Team));
            
        }

        private Vector3 AdvanceFireTargetPos(Tank selfTank, Tank oppTank)
        {
            Vector3 oppTankPos = oppTank.Position;
            Vector3 selfTankPos = selfTank.Position;
            var missileFlyTime =
                Vector3.Distance(oppTankPos, selfTankPos) / Match.instance.GlobalSetting.MissileSpeed; //这个其实不太准确
            Vector3 oppTankNewPos = oppTankPos + oppTank.Velocity * (missileFlyTime * 0.7f);

            return oppTankNewPos;
        }

        FireType ChangeFireType(FireType curFireType)
        {
            if (curFireType == FireType.RegularFire)
            {
                return FireType.AdvanceFire;
            }
            else
            {
                return FireType.RegularFire;
            }
        }

        #endregion

        #region 战胜期望计算函数

        void VictoryExpectationCalculation(Tank t, AllData allData)
        {
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (Match.instance.GetOppositeTank(t.Team).HP < t.HP)
            {
                if (oppTank != null && oppTank.IsDead == false)
                {
                    var victoryExpectation = (t.HP - oppTank.HP) / 41f;
                    allData.VictoryExpectation = Mathf.Clamp(victoryExpectation, 0f, 0.97f);
                }
                else
                {
                    allData.VictoryExpectation = 0f;
                }
            }

            //Debug.Log("VictoryExpectation" + allData.VictoryExpectation);


        }

        #endregion

        #region 吃星期望计算函数

        void CanGetStarExpectationCalculation(Tank t, AllData allData)
        {
            if (Match.instance.RemainingTime < 95f && Match.instance.RemainingTime > 90f) 
            {
                allData.CanGetStarExpectation = 0.99f;
                var targetPos = new Vector3(0, 3, 0);
                allData.StarTargetPos = targetPos;
                return;
            }

            var stars = Match.instance.GetStars();
            if (stars != null && stars.Count > 0) 
            {
                Vector3 nearStar = Vector3.zero;
                foreach (var star in stars)
                {
                    nearStar = star.Value.Position;
                    break;
                }

                var pathLength = GetPathLength(t.CaculatePath(nearStar));
                foreach (var star in stars)
                {
                    var newPathLength = GetPathLength(t.CaculatePath(star.Value.Position));
                    if (newPathLength < pathLength)
                    {
                        pathLength = newPathLength;
                        nearStar = star.Value.Position;
                    }
                }

                allData.CanGetStarExpectation = Mathf.Clamp((90 - pathLength) / 80f, 0f, 0.98f);

                allData.StarTargetPos = nearStar;
            }
            else
            {
                allData.CanGetStarExpectation = 0;
            }

            
            //Debug.Log("CanGetStarExpectation" + allData.CanGetStarExpectation);

        }

        public float GetPathLength(NavMeshPath path)
        {
            float length = 0.0f;

            // 检查路径是否有效
            if (path.status != NavMeshPathStatus.PathInvalid)
            {
                // 遍历所有路径的拐点来计算路径长度
                for (int i = 1; i < path.corners.Length; ++i)
                {
                    length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                }
            }

            return length;
        }

        #endregion

        #region 回城加血期望计算函数

        void BackAndHealingExpectationCalculation(Tank t, AllData allData)
        {
            float backAndHealingExpectation = 0f;
            if (t.HP < 60)
            {
                var pathLength = GetPathLength(t.CaculatePath(Match.instance.GetRebornPos(t.Team)));
                backAndHealingExpectation = ((90 - pathLength) / 90f + ((100 - t.HP) / 110f) * 10f) / 11f;
            }
            
            allData.BackAndHealingExpectation = Mathf.Clamp(backAndHealingExpectation, 0f, 0.99f);
//            Debug.Log("backAndHealingExpectation" + backAndHealingExpectation);

        }

        #endregion

        #region 闪避期望计算函数

        //TODO 对各种分类判定还不够精确,对各类受击情况和对应手段判断极其不准确，要好好调参数
        void DodgeExpectationCalculation(Tank t, AllData allData)
        {
            var enemyMissiles = Match.instance.GetOppositeMissiles(t.Team);
            if (enemyMissiles is null or { Count: 0 })
            {
                allData.DodgeExpectation = 0f;
//                Debug.Log("DodgeExpectation" + allData.DodgeExpectation);
                return;
            }

            float nearDis = float.MaxValue;
            Missile nearMissile = null;
            foreach (var enemyMissile in enemyMissiles)
            {
                var dis = Vector3.Distance(t.Position, enemyMissile.Value.Position);
                float dot = Vector3.Dot((t.Position - enemyMissile.Value.Position).normalized,
                    enemyMissile.Value.Velocity.normalized);
                if (dis < allData.BattleDis || dot < -0.9) continue; //进入不可躲避范围或大致已经越过本坦克，不计入
                // Calculate the dot product


                if (dis < nearDis && !IsMissilePassed(t, enemyMissile.Value))
                {
                    nearMissile = enemyMissile.Value;
                    nearDis = dis;
                }
            }

            if (nearMissile)
            {
                if (nearMissile != allData.CurDodgeMissile)//新炮弹来了
                {
                    allData.CurDodgeMissile = nearMissile;
                    float dot = Vector3.Dot((t.Position - nearMissile.Position).normalized,
                        nearMissile.Velocity.normalized);
                    // 使用反余弦函数得到夹角的弧度值
                    float angleRadians = Mathf.Acos(dot);

                    // 将弧度转换为度
                    float angleDegrees = angleRadians * Mathf.Rad2Deg;

                    float wight = QuadraticBezierCurve(new Vector2(0f, 1f), new Vector2(0.95f, 0.8f), //TODO 调曲线权重
                        new Vector2(1f, 0.2f),
                        (80f - (t.Position - nearMissile.Position).magnitude) / 80f).y;

                    float judgement = angleDegrees * wight;

                    //Debug.Log("wight:" + wight);
                    //Debug.Log("judgement:" + judgement);
                    if (judgement < 9.5f)
                    {
                        //Debug.Log("对方瞄准射击");
                        DodgeOnAim(t, nearMissile, allData);

                    }
                    else if (judgement < 40)
                    { 
                        //Debug.Log("对方预测射击");
                        DodgeOnPre(t, nearMissile, allData);
                    }
                    else
                    {
                        //Debug.Log("你这射的歪到哪去了");
                    }
                }
                else
                {

                    if (IsMissilePassed(t, nearMissile)) 
                    {
                        Debug.Log("躲过去了");
                        allData.CurDodgeMissile = null;
                        allData.DodgeExpectation = 0;
                    }
                    
                    
                }
            }

//            Debug.Log("DodgeExpectation" + allData.DodgeExpectation);
        }

        Vector2 QuadraticBezierCurve(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;

            Vector2 p = uu * p0; // 第一项
            p += 2 * u * t * p1; // 第二项
            p += tt * p2; // 第三项

            return p;
        }


        public enum ShellPosition
        {
            Front,
            FrontRight,
            Right,
            BackRight,
            Back,
            BackLeft,
            Left,
            FrontLeft
        }

        public ShellPosition GetShellPosition(Vector3 tankPosition, Vector3 shellPosition, Vector3 tankForward, Vector3 tankRight)
        {
            // 将坦克和炮弹的位置投影到x-z平面
            Vector3 tankPositionXZ = new Vector3(tankPosition.x, 0, tankPosition.z);
            Vector3 shellPositionXZ = new Vector3(shellPosition.x, 0, shellPosition.z);

            // 计算坦克前方和右方向的二维向量（忽略y轴）
            Vector3 tankForwardXZ = new Vector3(tankForward.x, 0, tankForward.z).normalized;
            Vector3 tankRightXZ = new Vector3(tankRight.x, 0, tankRight.z).normalized;

            // 计算从坦克到炮弹的二维向量
            Vector3 directionToShellXZ = (shellPositionXZ - tankPositionXZ).normalized;

            // 计算与坦克前方和右方向的夹角
            float angleForward = Vector3.Angle(tankForwardXZ, directionToShellXZ);
            float angleRight = Vector3.Angle(tankRightXZ, directionToShellXZ);

            // 使用向量叉乘来确定炮弹是在坦克的左边还是右边
            Vector3 crossProduct = Vector3.Cross(tankForwardXZ, directionToShellXZ);
            bool isLeft = crossProduct.y < 0;

            // 根据角度和叉乘结果确定炮弹的具体方位
            if (angleForward <= 45)
            {
                return ShellPosition.Front;
            }
            else if (angleForward > 45 && angleForward <= 135)
            {
                if (angleRight <= 90)
                {
                    return isLeft ? ShellPosition.FrontLeft : ShellPosition.FrontRight;
                }
                else
                {
                    return isLeft ? ShellPosition.BackLeft : ShellPosition.BackRight;
                }
            }
            else
            {
                return ShellPosition.Back;
            }
        }
        
        void DodgeOnAim(Tank t, Missile missile, AllData allData)
        {
            ShellPosition missilePosition =
                GetShellPosition(t.Position, missile.Position, t.Forward, t.transform.right);

            
            Vector3 forward = new Vector3(missile.Velocity.x, 1.2f, missile.Velocity.z); // 炮弹方向
            float weightTowardsA = 1f;
          
            Vector3 dir1 = Vector3.Lerp((Quaternion.Euler(0, 90, 0) * forward).normalized, t.Forward.normalized,
                1 - weightTowardsA).normalized;
            Vector3 dir2 = Vector3.Lerp((Quaternion.Euler(0, -90, 0) * forward).normalized, t.Forward.normalized,
                1 - weightTowardsA).normalized;
            Ray ray1 = new Ray(t.Position, dir1);
            Ray ray2 = new Ray(t.Position, dir2);

            RaycastHit hit1;
            RaycastHit hit2;
            bool isHit1;
            bool isHit2;
            // 执行射线检测
            isHit1 = Physics.Raycast(ray1, out hit1, Match.instance.FieldSize,
                LayerMask.GetMask("Layer_StaticObject"));

            isHit2 = Physics.Raycast(ray2, out hit2, Match.instance.FieldSize,
                LayerMask.GetMask("Layer_StaticObject"));


            Vector3 pos1 =  FindClosestPointOnNavMesh(t.Position, hit1.point, 100, 0.1f);
            Vector3 pos2 = FindClosestPointOnNavMesh(t.Position, hit2.point, 100, 0.1f);
            
            float angleToTarget1 = Vector3.Angle(t.Forward, (pos1 - t.Position));
            float angleToTarget2 = Vector3.Angle(t.Forward, (pos2 - t.Position));

            // 比较两个角度
            if (angleToTarget1 < angleToTarget2 && Vector3.Distance(t.Position, pos1) > 10)
            {
                allData.DodgePos = pos1;
            }
            else if (angleToTarget2 < angleToTarget1 && Vector3.Distance(t.Position, pos2) > 10)
            {
                allData.DodgePos = pos1;
            }
            else 
            {
                if(Vector3.Distance(t.Position, pos1) > Vector3.Distance(t.Position, pos2))
                    allData.DodgePos = pos1;
                else
                    allData.DodgePos = pos2;
            }

            //CrateSphere(allData.DodgePos, 1f, Color.green);


            allData.DodgeExpectation = 1;
            allData.DodgeType = DodgeType.TangentialDodge;
        }
        

        void DodgeOnPre(Tank t, Missile missile, AllData allData)
        {
            Vector3 forward = missile.Velocity.normalized; // 炮弹方向

            Vector3 toOther1 = (t.Position - missile.Position).normalized; // 从角色指向另一个点的向量
            Vector3 toOther2 = (t.Position + t.Velocity.normalized - missile.Position).normalized; // 从角色指向另一个点的向量

            if (Vector3.Dot(forward, toOther1) - Vector3.Dot(forward, toOther2) < 0)
            {
                Debug.Log("预测朝着车头去的，得急停");
                allData.DodgeType = DodgeType.EmergencyStop;
                allData.DodgeExpectation = 1;
            }
            else
            {
                Debug.Log("预测朝着车尾去的，只管冲");
            }
        }

        Vector3 FindClosestPointOnNavMesh(Vector3 fromPosition, Vector3 toPosition, float maxDistance, float stepSize)
        {
            // 计算方向向量，忽略Y轴差异
            Vector3 direction = new Vector3(toPosition.x, fromPosition.y, toPosition.z) - fromPosition;
            direction.y = 0; // 确保方向是水平的
            direction.Normalize();

            Vector3 lastValidPoint = fromPosition;
            bool pointFound = false;

            for (float dist = 0; dist < maxDistance; dist += stepSize)
            {
                // 在方向上前进一步，忽略Y轴
                Vector3 currentPoint = fromPosition + direction * dist;
                currentPoint.y = fromPosition.y; // 保持Y轴不变

                // 检查这个点是否在NavMesh上
                if (NavMesh.SamplePosition(currentPoint, out NavMeshHit hit, stepSize, NavMesh.AllAreas))
                {
                    // 如果在NavMesh上，更新最近的有效点
                    lastValidPoint = hit.position;
                    pointFound = true;
                }
                else
                {
                    // 如果不在NavMesh上，跳出循环
                    break;
                }
            }

            if (pointFound)
            {
                return lastValidPoint;
            }
            else
            {
                // 如果没有找到任何点，则返回起始点
                return fromPosition;
            }
        }
        
        void CrateSphere(Vector3 pointToMark, float radius, Color color)
        {
            // 创建球体
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            // 设置球体的位置和大小
            sphere.transform.position = pointToMark;
            sphere.transform.localScale = new Vector3(radius, radius, radius); // 设置球体的半径

            // 为球体添加材质或颜色（可选）
            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            sphereRenderer.material.color = color;
        }
        
        bool IsMissilePassed(Tank tank, Missile missile)
        {
            Vector3 missilePosition = missile.Position;
            Vector3 missileVelocity = missile.Velocity;
            Vector3 tankPosition = tank.Position;

            bool isMiss = Vector3.Distance(missilePosition, tankPosition) > 3f &&
                          Vector3.Distance(missilePosition + missileVelocity * Time.deltaTime, tankPosition) >
                          Vector3.Distance(missilePosition, tankPosition);

            return isMiss;
        }

        #endregion


    }

    enum FireType
    {
        /// <summary>
        /// 普通开火
        /// </summary>
        RegularFire,

        /// <summary>
        /// 预测开火
        /// </summary>
        AdvanceFire,
    }

    enum DodgeType
    {
        /// <summary>
        /// 急停
        /// </summary>
        EmergencyStop,

        /// <summary>
        /// 切向躲避
        /// </summary>
        TangentialDodge,
    }

    /// <summary>
    /// 所有可能用到的信息，本来是枚举，但是枚举的话每个数据类型都要在存取的时候强制装拆箱，所以就改成了存一个类型明确的类，而且由于成员太多，结构体的话复制成本太大，不如用类只存一个索引
    /// </summary>
    class AllData
    {
        /// <summary>
        /// 战胜期望（战斗的代价收益）
        /// </summary>
        public float VictoryExpectation;

        /// <summary>
        /// 可以获取星星的代价收益
        /// </summary>
        public float CanGetStarExpectation;

        /// <summary>
        /// 回城回血的代价收益
        /// </summary>
        public float BackAndHealingExpectation;

        /// <summary>
        /// 闪避期望
        /// </summary>
        public float DodgeExpectation;

        /// <summary>
        /// 我方当前选择的闪避策略
        /// </summary>
        public DodgeType DodgeType;

        /// <summary>
        /// 躲避目标点
        /// </summary>
        public Vector3 DodgePos;

        /// <summary>
        /// 吃星移动目标点
        /// </summary>
        public Vector3 StarTargetPos;

        /// <summary>
        /// 攻击类型
        /// </summary>
        public FireType FireType;

        /// <summary>
        /// 当前开火类型连续开火了几次
        /// </summary>
        public int FireTypeCount;

        /// <summary>
        /// 本次开火策略连续开火计数的首索引
        /// </summary>
        public int FireTypeCountIndexHead;

        /// <summary>
        /// 射击目标点
        /// </summary>
        public Vector3 FireTargetPos;

        /// <summary>
        /// 已经获得超级星星
        /// </summary>
        public bool ObtainedSuperStar;

        /// <summary>
        /// 射击次数，用于计算命中率
        /// </summary>
        public int ShootNumber;

        /// <summary>
        /// 尚未判定是否命中的我方炮弹的列表，用于计算命中率
        /// </summary>
        public List<MissileData> MissileDataList;


        /// <summary>
        /// 敌方最新发射的炮弹
        /// </summary>
        public Missile CurDodgeMissile;

        /// <summary>
        /// 敌方阵营
        /// </summary>
        public ETeam EnemyTeam;

        /// <summary>
        /// 决斗距离
        /// </summary>
        public float BattleDis = 4f;

        public AllData(float victoryExpectation, float canGetStarExpectation, float backAndHealingExpectation,
            float dodgeExpectation, DodgeType dodgeType, Vector3 starTargetPos, FireType fireType,
            Vector3 fireTargetPos,
            bool obtainedSuperStar, int shootNumber, List<MissileData> missileDataList,
            Missile newEnemyMissile, ETeam enemyTeam
        )
        {
            VictoryExpectation = victoryExpectation;
            CanGetStarExpectation = canGetStarExpectation;
            BackAndHealingExpectation = backAndHealingExpectation;
            DodgeExpectation = dodgeExpectation;
            DodgeType = dodgeType;
            StarTargetPos = starTargetPos;
            FireType = fireType;
            FireTargetPos = fireTargetPos;
            ObtainedSuperStar = obtainedSuperStar;
            ShootNumber = shootNumber;
            MissileDataList = missileDataList;
            CurDodgeMissile = newEnemyMissile;
            EnemyTeam = enemyTeam;
        }

    }

    enum HitState
    {
        UnKnow,
        Hit,
        NotHit
    }

    struct MissileData
    {
        public int ID;
        public Missile Missile;
        public FireType FireType;
        public HitState HitState;

        public void SetIsHit(HitState hitState)
        {
            HitState = hitState;
        }
    }

    class MyTank : Tank
    {
        private BlackboardMemory m_WorkingMemory;
        private Node m_BTNode;

        protected override void OnStart()
        {
            base.OnStart();

            m_WorkingMemory = new BlackboardMemory();


            //TODO 可以搞数据驱动
            m_WorkingMemory.SetValue(0,
                new AllData(0f, 0f, 0f, 0f, DodgeType.TangentialDodge, Vector3.zero,
                    FireType.AdvanceFire, Vector3.zero, false, 0, new List<MissileData>(),
                    null, ETeam.A));



            m_BTNode = new ParallelNode(1).AddChild(
                new ExpectationCalculation(),
                new TurnTurret(),
                new Fire(),
                new TankActionSelectorNode().AddChild(
                    new Dodge(),
                    new MoveToGetStar(),
                    new BackToHomeAndHealing(),
                    new Pursuit()
                ));
        }

        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }

        public override string GetName()
        {
            return "MokutkZTank";
        }
    }

}
