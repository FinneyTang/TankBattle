using Main;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using AI.Base;
using AI.RuleBased;


namespace HYQ
{

    class IsFireLineClear : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank enemyTank = Match.instance.GetOppositeTank(myTank.Team);

            return new IsBothCanSeeOpp(myTank.FirePos, myTank.FirePos + myTank.TurretAiming * Vector3.Distance(myTank.FirePos, enemyTank.Position)).IsTrue(agent);

        }
    }

    class IsBothCanSeeOpp : Condition
    {
        private Vector3 APos;
        private Vector3 BPos;
        public IsBothCanSeeOpp(Vector3 A, Vector3 B)
        {
            APos = A;
            BPos = B;
        }
        public override bool IsTrue(IAgent agent)
        {

            return !Physics.Linecast(APos, BPos, PhysicsUtils.LayerMaskScene);
        }
    }

    class IsMyTankPosCanSee : Condition
    {
        private Vector3 BPos;
        public IsMyTankPosCanSee(Vector3 Pos)
        {
            BPos = Pos;
        }
        public override bool IsTrue(IAgent agent)
        {
            return new IsBothCanSeeOpp(((Tank)agent).Position, BPos).IsTrue(agent);
        }
    }
    class IsMyCanSeeEneTank : Condition
    {
        private Tank enemyTankC;
        public IsMyCanSeeEneTank(Tank enemyTank)
        {
            enemyTankC = enemyTank;
        }
        public override bool IsTrue(IAgent agent)
        {
            if (enemyTankC.IsDead)
            {
                return false;
            }
            return new OrCondition(new IsMyTankPosCanSee(enemyTankC.Position + enemyTankC.Forward * 3.5f), new IsMyTankPosCanSee(enemyTankC.Position - enemyTankC.Forward * 3.5f)).IsTrue(agent);

        }
    }

    class IsMyAimingAngleLessThan : Condition
    {
        private Tank enemyTankC;
        private float angleC;
        public IsMyAimingAngleLessThan(Tank enemyTank, float angle = 90f)
        {
            enemyTankC = enemyTank;
            angleC = angle;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            return Vector3.Angle(myTank.TurretAiming, ((MyTank)agent).AimAdvanceAmountPosition(enemyTankC) - myTank.transform.position) < angleC;
        }

    }

    class IsMatchTimeLessThan : Condition
    {
        float remainTimeC;
        public IsMatchTimeLessThan(float remainTime = 70f)
        {
            remainTimeC = remainTime;
        }
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.RemainingTime < remainTimeC;
        }

    }

    class IsMyTankPathDistanceLessThan : Condition
    {
        float distance;
        Vector3 targetPos;
        public IsMyTankPathDistanceLessThan(Vector3 Pos, float dis)
        {
            distance = dis;
            targetPos = Pos;
        }
        public override bool IsTrue(IAgent agent)
        {

            return ((MyTank)agent).PathDistance(targetPos) < distance;
        }
    }

    class IsMyScoreGreaterThan : Condition
    {
        float scoreC;
        public IsMyScoreGreaterThan(float score)
        {
            scoreC = score;
        }
        public override bool IsTrue(IAgent agent)
        {
            return ((Tank)agent).Score > scoreC;
        }
    }

    class IsTankDead : Condition
    {
        Tank tank;
        public IsTankDead(Tank tankC)
        {
            tank = tankC;
        }
        public override bool IsTrue(IAgent agent)
        {
            return tank.IsDead;
        }
    }

    class IsNumALessNumB : Condition
    {
        private int numA;
        private int numB;


        public IsNumALessNumB(int A, int B)
        {
            numA = A;
            numB = B;
        }
        public override bool IsTrue(IAgent agent)
        {
            return numA < numB;
        }
    }

    class IsWarningFlagNotEqual : Condition
    {
        MyTank.MissileWarningType warningType;

        public IsWarningFlagNotEqual(MyTank.MissileWarningType TargetwarningType)
        {
            warningType = TargetwarningType;
        }
        public override bool IsTrue(IAgent agent)
        {
            return warningType != ((MyTank)agent).hitWarningFlag;

        }

    }

    class IsLineDistanceLess : Condition
    {
        Vector3 PA;
        Vector3 PB;
        float distance;
        public IsLineDistanceLess(Vector3 A, Vector3 B, float dis)
        {
            PA = A;
            PB = B;
            distance = dis;
        }

        public override bool IsTrue(IAgent agent)
        {
            return Vector3.Distance(PA, PB) < distance;
        }
    }


    class IsSuperStarExist : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            foreach (var item in Match.instance.GetStars().Values)
            {
                if (item.IsSuperStar)
                {
                    return true;
                }
            }
            return false;
        }
    }
    public class MyTank : Tank
    {

        Match match;
        public Tank myTank;

        private Tank _enemyTank;
        public Tank enemyTank
        {
            get
            {
                if (_enemyTank == null)
                {
                    _enemyTank = Match.instance.GetOppositeTank(Team);
                }

                return _enemyTank;
            }
        }
        Vector3 myRebornPos;
        float EvadeMinDistance;
        float during = 0.5f;



        public Missile AvoidingMissile;
        public bool AvoidingDir;

        List<Missile> Missles;
        List<Missile> WarningMissles;


        NavMeshAgent navMeshAgent;

        private NavMeshAgent _enemyAgent;
        NavMeshAgent EnemyAgent
        {
            get
            {
                if (_enemyAgent == null)
                {
                    if (this.enemyTank != null)
                    {
                        _enemyAgent = this.enemyTank.GetComponent<NavMeshAgent>();
                    }
                }

                return _enemyAgent;
            }
        }

        float missileHeight = 3.5f;
        public MissileWarningType hitWarningFlag;
        //-----ConditionBase-----

        //Fire-Condition
        Condition FireConditionWhenEnemyAlive;
        Condition FireConditionWhenEnemyDead;
        //Moving-Condition
        Condition RebornBase;
        Condition EvadeRule;

        Condition SuperStarRuleA;
        Condition SuperStarRuleB;


        //General-Condition
        Condition MatchTimeLessThan;
        Condition MoreRecover;
        //------------------------




        private void OnDrawGizmos()
        {
            //Gizmos.DrawSphere(AimAdvanceAmountPosition(enemyTank), 0.5f);
            //Gizmos.DrawSphere(AimAdvanceAmountPosition(myTank), 0.5f);
            ////Gizmos.DrawWireCube(myTank.Position + myTank.Forward * 6, new Vector3(4, 8, 4));
            //Gizmos.DrawWireSphere(myTank.Position + myTank.Forward * 1.5f + Vector3.up * 3f, 2.5f);


        }
        protected override void OnStart()
        {
            base.OnStart();
            //比赛基础对象获取
            match = Match.instance;
            myTank = match.GetTank(Team);
            //enemyTank = match.GetOppositeTank(Team);
            navMeshAgent = GetComponent<NavMeshAgent>();
            //EnemyAgent = enemyTank.GetComponent<NavMeshAgent>();
            //-----------------
            EvadeMinDistance = 20f;
            myRebornPos = (match.GetRebornPos(Team) + (Vector3.zero - match.GetRebornPos(Team)).normalized * 6) + Vector3.up * 0.5f;
            AvoidingMissile = null;
            AvoidingDir = false;





        }
        private void DebugLayer()
        {

            if (CanFire())
            {
                Debug.DrawLine(FirePos, FirePos + TurretAiming * Vector3.Distance(FirePos, enemyTank.Position), Color.black);
            }

            //火控和规避Debug
            if (WarningMissles.Count > 0)
            {
                if (Vector3.Distance(WarningMissles[0].Position, enemyTank.Position) < 3.75f)
                {
                    Debug.LogError("敌方发射导弹");
                }
                if (Vector3.Distance(WarningMissles[0].Position, myTank.Position + Vector3.up * 3) < 4f)
                {
                    Debug.LogError("敌方导弹近身");
                }
            }

            if (hitWarningFlag != MissileWarningType.Safe && (Vector3.Distance(myTank.transform.position, enemyTank.transform.position) < EvadeMinDistance))//&& !AvoidEvadeConditionPlus()
            {
                Debug.LogWarning("满足特殊条件不进行规避");
            }

        }
        private void ConditionBaseUpdate()
        {
            //Fire-Condition
            FireConditionWhenEnemyAlive = new AndCondition(new OrCondition(new IsMyTankPosCanSee(AimAdvanceAmountPosition(enemyTank)), new IsMyCanSeeEneTank(enemyTank)), new AndCondition(new IsMyAimingAngleLessThan(enemyTank, 36f), new IsFireLineClear()));

            FireConditionWhenEnemyDead = new IsMyTankPosCanSee(match.GetRebornPos(enemyTank.Team));


            //Moving-Condition
            RebornBase = new AndCondition(new AndCondition(new AndCondition(new IsMatchTimeLessThan(70f), new IsMyScoreGreaterThan(enemyTank.Score + 15)), new NotCondition(new IsTankDead(enemyTank))), new IsMyTankPathDistanceLessThan(enemyTank.Position, 60f));

            EvadeRule = new AndCondition(new IsWarningFlagNotEqual(MissileWarningType.Safe), new NotCondition(new IsLineDistanceLess(myTank.Position, enemyTank.Position, EvadeMinDistance)));
            MoreRecover = new AndCondition(new IsMyTankPathDistanceLessThan(myRebornPos, 2f), new IsNumALessNumB(myTank.HP, 60));

            SuperStarRuleA = new AndCondition(new IsMatchTimeLessThan(match.GlobalSetting.MatchTime / 2), new IsSuperStarExist());
            SuperStarRuleB = new AndCondition(new NotCondition(new IsMatchTimeLessThan(match.GlobalSetting.MatchTime / 2)), new NotCondition(new IsMyTankPathDistanceLessThan(new Vector3(0, 0.5f, 0), (match.RemainingTime - match.GlobalSetting.MatchTime / 2) * navMeshAgent.speed - 15f)));
        }

        private void KnowledgeLayer(out MissileWarningType hitWarningFlagC)
        {
            DetectFlyingMissile();
            hitWarningFlagC = HitWarning();

            ConditionBaseUpdate();
        }

        private void FireControlSystemAct()
        {
            //火控系统--瞄准
            if (!enemyTank.IsDead)
            {
                TurretTurnTo(AimAdvanceAmountPosition(enemyTank));
            }
            else
            {
                TurretTurnTo(match.GetRebornPos(enemyTank.Team));
            }

            //火控系统--开火
            if (!enemyTank.IsDead && FireConditionWhenEnemyAlive.IsTrue(myTank))
            {
                Fire();
            }
            else if (enemyTank.IsDead && FireConditionWhenEnemyDead.IsTrue(myTank))
            {
                Fire();
            }

        }

        private void MoveingControlSystemAct()
        {
            if (RebornBase.IsTrue(this))
            {
                Move(myRebornPos);
                //Debug.LogWarning("保命");
            }
            else if (SuperStarRuleA.IsTrue(this))
            {
                Move(new Vector3(0, 0.5f, 0));
                //Debug.LogWarning("正在强制指引前往超星点");
            }
            else if (SuperStarRuleB.IsTrue(this))
            {
                Move(new Vector3(0, 0.5f, 0));
                //Debug.LogWarning("正在强制指引前往超星点");
            }
            else if (MoreRecover.IsTrue(this))
            {
                //Vector3.Distance(myTank.Position, myRebornPos) < 2f && myTank.HP < 60f
                Move(myRebornPos);
                Debug.LogWarning("多回会血");
            }
            else if (EvadeRule.IsTrue(this))
            {
                //hitWarningFlag != MissileWarningType.Safe && Vector3.Distance(myTank.transform.position, enemyTank.transform.position) > EvadeMinDistance
                //&& !AvoidEvadeConditionPlus()
                EvadeMissile(WarningMissles[0], hitWarningFlag);
                //Debug.LogWarning("正在规避");
            }
            else if (!enemyTank.IsDead && ((IsMyTankPosCanSee(AimAdvanceAmountPosition(enemyTank)) || IsMyCanSeeEneTank()) && ((Mathf.CeilToInt(myTank.HP / 25f)) > Mathf.CeilToInt(enemyTank.HP / 25f)) && Vector3.Distance(enemyTank.Position, match.GetRebornPos(enemyTank.Team)) > 12f))
            {

                Move(enemyTank.Position);
                //Debug.LogWarning("追击敌人");
            }
            else if ((Mathf.CeilToInt(myTank.HP / 25f)) + 1 < Mathf.CeilToInt(enemyTank.HP / 25f))
            {
                Move(myRebornPos);
                //Debug.LogWarning("血量比敌人少得多，回家补血");
            }
            else if (new AndCondition(new IsMatchTimeLessThan(70f), new IsMyScoreGreaterThan(enemyTank.Score + 10)).IsTrue(this))
            {
                //match.RemainingTime < 70f && myTank.Score - 10 >= enemyTank.Score

                if (EnemyAreaNearestStar() != null)
                {
                    Move(EnemyAreaNearestStar().Position);
                    //Debug.LogWarning("找敌场星星");
                }
                else
                {
                    Move(myRebornPos);
                    //Debug.LogWarning("回家");
                }
            }
            else if (!enemyTank.IsDead && NearestStarAndFar() != null && (myTank.HP > 25) && Mathf.CeilToInt(enemyTank.HP / 25f) >= (Mathf.CeilToInt(myTank.HP / 25f)))
            {
                Move(NearestStarAndFar().Position);
                //Debug.LogWarning("找安全星星");
            }
            else if (NearestStar() != null && (myTank.HP > 25))
            {
                Move(NearestStar().Position);
                //Debug.LogWarning("找近星星");
            }
            else
            {
                Move(myRebornPos);
                //Debug.LogWarning("回家");
            }
            //if (Input.GetMouseButton(1))
            //{
            //    HandControl();
            //}

        }
        private void StratedgyAndBehaviorLayer()
        {
            FireControlSystemAct();
            MoveingControlSystemAct();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            KnowledgeLayer(out hitWarningFlag);
            StratedgyAndBehaviorLayer();
            //DebugLayer();

        }





        //-------------------------------ExtraLogic----------------------------------------


        private bool IsFireLineClear()
        {
            Debug.DrawLine(FirePos, FirePos + TurretAiming * Vector3.Distance(FirePos, enemyTank.Position), Color.black);
            return IsBothCanSeeOpp(FirePos, FirePos + TurretAiming * Vector3.Distance(FirePos, enemyTank.Position));

        }

        public override string GetName()
        {
            return "HYQ_Tank";
        }

        public float PathDistance(Vector3 TarPos)
        {
            NavMeshPath path = new NavMeshPath();
            navMeshAgent.CalculatePath(TarPos, path);
            float TotalDistance = 0;

            if (path.corners.Length == 0)
            {
                TotalDistance = Vector3.Distance(myTank.transform.position, TarPos);
            }
            else if (path.corners.Length == 1)
            {
                TotalDistance = Vector3.Distance(myTank.transform.position, path.corners[0]) + Vector3.Distance(TarPos, path.corners[0]);
            }

            else if (path.corners.Length > 1)
            {
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    //Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.red, 3);
                    TotalDistance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                //TotalDistance += Vector3.Distance(myTank.transform.position, path.corners[0]) + Vector3.Distance(TarPos, path.corners[path.corners.Length - 1]);已经加过了
            }



            return TotalDistance;
        }

        private float EnemyPathDistance(Vector3 TarPos)
        {
            NavMeshPath path = new NavMeshPath();
            EnemyAgent.CalculatePath(TarPos, path);
            float TotalDistance = 0;

            if (path.corners.Length == 0)
            {
                TotalDistance = Vector3.Distance(enemyTank.transform.position, TarPos);
            }
            else if (path.corners.Length == 1)
            {
                TotalDistance = Vector3.Distance(enemyTank.transform.position, path.corners[0]) + Vector3.Distance(TarPos, path.corners[0]);
            }

            else if (path.corners.Length > 1)
            {
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    //Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.red, 3);
                    TotalDistance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                //TotalDistance += Vector3.Distance(myTank.transform.position, path.corners[0]) + Vector3.Distance(TarPos, path.corners[path.corners.Length - 1]);已经加过了
            }



            return TotalDistance;
        }

        private Star NearestStarAndFar()
        {
            List<Star> stars = new List<Star>();
            foreach (var item in match.GetStars().Values)
            {
                stars.Add(item);
            }

            if (stars.Count > 0)
            {
                float mindis = 10000f;
                int starId = -1;
                foreach (var item in stars)
                {
                    if (PathDistance(item.Position) < mindis && PathDistance(item.Position) < EnemyPathDistance(item.Position))
                    {
                        mindis = PathDistance(item.Position);
                        starId = item.ID;
                    }

                }

                return match.GetStarByID(starId);
            }
            else
            {
                return null;
            }

        }
        private Star NearestStar()
        {
            List<Star> stars = new List<Star>();
            foreach (var item in match.GetStars().Values)
            {
                stars.Add(item);
            }

            if (stars.Count > 0)
            {
                float mindis = 10000f;
                int starId = -1;
                foreach (var item in stars)
                {
                    if (PathDistance(item.Position) < mindis)
                    {
                        mindis = PathDistance(item.Position);
                        starId = item.ID;
                    }

                }

                return match.GetStarByID(starId);
            }
            else
            {
                return null;
            }

        }

        private Star EnemyAreaNearestStar()
        {
            List<Star> stars = new List<Star>();
            foreach (var item in match.GetStars().Values)
            {
                if (!IsBothCanSeeOpp(myRebornPos + Vector3.up, item.Position))
                {
                    stars.Add(item);
                }

            }

            if (stars.Count > 0)
            {
                float mindis = 10000f;
                int starId = -1;
                foreach (var item in stars)
                {
                    if (PathDistance(item.Position) < mindis)
                    {
                        mindis = PathDistance(item.Position);
                        starId = item.ID;
                    }

                }

                return match.GetStarByID(starId);
            }
            else
            {
                return null;
            }
        }

        private void DetectFlyingMissile()
        {

            Missles = new List<Missile>();
            foreach (var item in match.GetOppositeMissiles(Team).Values)
            {
                Missles.Add(item);
            }

        }

        private MissileWarningType HitWarning()
        {

            WarningMissles = new List<Missile>();
            if (Missles.Count != 0 && !myTank.IsDead)
            {
                foreach (var missile in Missles)
                {

                    if (!IsBothCanSeeOpp(missile.Position, missile.Position + missile.Velocity.normalized * Vector3.Distance(myTank.Position, new Vector3(missile.Position.x, myTank.Position.y, missile.Position.z))))
                    {
                        continue;
                    }



                    //if (Physics.SphereCast(myTank.Position + Vector3.up, 3f, myTank.Forward, out tmp1, 3f, LayerMask.NameToLayer("Layer_Entity")) || Physics.SphereCast(myTank.Position + Vector3.up, 3f, -myTank.Forward, out tmp1, 3f, LayerMask.NameToLayer("Layer_Entity")))
                    var tmp = Physics.OverlapSphere(myTank.Position + myTank.Forward * 1.5f + Vector3.up * 3f, 2.5f, LayerMask.GetMask("Layer_Entity"));
                    //var tmp = Physics.OverlapBox(myTank.Position + myTank.Forward * 0.6f + Vector3.up * 1.5f, new Vector3(2.5f, 2.5f, 3.8f), myTank.transform.rotation, LayerMask.GetMask("Layer_Entity"));
                    if (tmp.Length > 0 && tmp[0].GetComponentInParent<Missile>().Team != myTank.Team)
                    {

                        //foreach (var item in tmp)
                        //{
                        //    Debug.LogWarning("对象位置：" + item.GetComponentInParent<Transform>().position);
                        //}

                        if (WarningMissles.Count == 0)
                        {
                            WarningMissles.Add(missile);
                        }
                        else
                        {
                            WarningMissles[0] = missile;
                        }
                        Debug.Log("坦克邻近受击预警");
                        return MissileWarningType.Near;
                    }
                    //坦克当前位置被锁定预警
                    else if (Physics.Raycast(missile.Position, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank) || Physics.Raycast(missile.Position + missile.transform.right * 1.6f, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank) || Physics.Raycast(missile.Position - missile.transform.right * 1.6f, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank))
                    {
                        WarningMissles.Add(missile);
                        Debug.DrawRay(missile.Position, missile.Velocity, Color.black);
                        //EvadeMissile(missile);
                        Debug.Log("坦克中心受击预警");
                        return MissileWarningType.Locked;

                    }
                    //提前量受击预警
                    else if (Vector3.Angle(AimAdvanceAmountPosition(myTank) - missile.Position, missile.Velocity) < 30f)
                    {

                        WarningMissles.Add(missile);
                        Debug.Log("提前量受击预警");
                        return MissileWarningType.Advance;

                    }



                }


            }
            return MissileWarningType.Safe;
        }

        private void EvadeMissile(Missile warnmissile, MissileWarningType warningType)
        {
            Vector3 evadeOffset = myTank.transform.forward * 3f;
            float angle = Vector3.Angle(warnmissile.Velocity, myTank.Forward);
            Debug.Assert(warningType != MissileWarningType.Safe);
            var dir = Vector3.Cross(warnmissile.Velocity.normalized, myTank.transform.up) * 7f;

            //Debug.LogError(Vector3.Distance(warnmissile.Position, myTank.Position) + " " + (warnmissile.Velocity.magnitude));

            if (warningType == MissileWarningType.Locked)
            {
                if (AvoidingMissile != null && warnmissile.GetHashCode() == AvoidingMissile.GetHashCode())
                {
                    if (AvoidingDir)
                    {
                        Move(myTank.transform.position + dir + evadeOffset);
                    }
                    else
                    {
                        Move(myTank.transform.position - dir + evadeOffset);
                    }
                    return;
                }
                AvoidingMissile = warnmissile;
            }




            if (warningType == MissileWarningType.Locked)
            {
                Debug.LogWarning("采用蛇形规避");
                if (!DetectWall(dir + evadeOffset) && !DetectWall(-dir + evadeOffset))
                {

                    //根据地形掩体选择规避方向
                    if (!IsBothCanSeeOpp(myTank.Position + dir + evadeOffset, warnmissile.Position))
                    {
                        Move(myTank.transform.position + dir + evadeOffset);
                        Debug.DrawLine(myTank.Position, myTank.transform.position + dir + evadeOffset, Color.red, during);
                        AvoidingDir = true;
                    }
                    else if (!IsBothCanSeeOpp(myTank.Position - dir + evadeOffset, warnmissile.Position))
                    {
                        Move(myTank.transform.position - dir + evadeOffset);
                        Debug.DrawLine(myTank.Position, myTank.transform.position - dir + evadeOffset, Color.red, during);
                        AvoidingDir = false;
                    }
                    else
                    {
                        //根据车头朝向的偏好选择规避方向
                        float chooseSide = Vector3.Dot(dir, myTank.Forward);
                        if (chooseSide >= 0)
                        {
                            Move(myTank.transform.position + dir + evadeOffset);
                            Debug.DrawLine(myTank.Position, myTank.transform.position + dir + evadeOffset, Color.red, during);
                            AvoidingDir = true;
                        }
                        else
                        {
                            Move(myTank.transform.position - dir + evadeOffset);
                            Debug.DrawLine(myTank.Position, myTank.transform.position - dir + evadeOffset, Color.red, during);
                            AvoidingDir = false;
                        }
                    }



                }
                else if (!DetectWall(dir + evadeOffset))
                {
                    Debug.Log("规避时检测到墙面");
                    Move(myTank.transform.position + dir + evadeOffset);
                    Debug.DrawLine(myTank.Position, myTank.transform.position + dir + evadeOffset, Color.red, during);
                    AvoidingDir = true;
                }
                else if (!DetectWall(-dir + evadeOffset))
                {
                    Debug.Log("规避时检测到墙面");
                    Move(myTank.transform.position - dir + evadeOffset);
                    Debug.DrawLine(myTank.Position, myTank.transform.position - dir + evadeOffset, Color.red, during);
                    AvoidingDir = false;
                }
            }
            else if (warningType == MissileWarningType.Near)
            {
                Debug.LogWarning("车前有导弹，紧急停车");
                Move(myTank.Position);

            }
            else if (warningType == MissileWarningType.Advance)
            {
                Debug.LogWarning("提前量导弹，急停躲避");
                Move(myTank.Position);
            }
            else
            {
                Debug.LogError("若报错，须处理");
            }

        }

        private bool DetectWall(Vector3 dir)
        {
            return Physics.Raycast(myTank.transform.position, dir, dir.magnitude, PhysicsUtils.LayerMaskScene);
        }
        private void HandControl()
        {
            Vector3 TarPos = myRebornPos;
            if (Input.GetMouseButton(1))
            {
                RaycastHit raycasthit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Physics.Raycast(ray, out raycasthit, PhysicsUtils.LayerMaskScene);
                //Debug.Log(raycasthit.collider.name);
                TarPos = raycasthit.point;

            }
            Move(TarPos);
        }

        public Vector3 AimAdvanceAmountPosition(Tank TargetTank, float AdvanceWeight = 0.85f)
        {
            //Test
            //if (TargetTank.name == enemyTank.name)
            //{
            //    AdvanceWeight = 0.7f;
            //}
            


            Vector3 AimPos;
            float distance = Vector3.Distance(myTank.Position, enemyTank.Position);
            float missileFlyingTime = distance / match.GlobalSetting.MissileSpeed;
            AimPos = TargetTank.Position + TargetTank.Forward + TargetTank.Velocity * missileFlyingTime * AdvanceWeight;//AdvanceWeight调和提前量和坦克位置
            return AimPos;
        }



        public bool IsBothCanSeeOpp(Vector3 APos, Vector3 BPos)
        {
            return !Physics.Linecast(APos, BPos, PhysicsUtils.LayerMaskScene);
        }
        private bool IsMyTankPosCanSee(Vector3 pos)
        {
            return IsBothCanSeeOpp(myTank.Position, pos);
        }
        private bool IsMyCanSeeEneTank()
        {
            if (enemyTank.IsDead)
            {
                return false;
            }
            return (IsMyTankPosCanSee(enemyTank.Position + enemyTank.Forward * 3.5f) || IsMyTankPosCanSee(enemyTank.Position - enemyTank.Forward * 3.5f));
        }

        private bool AvoidEvadeConditionPlus()
        {
            return (enemyTank.HP < 26f && myTank.HP >= 26f && Vector3.Distance(myTank.Position, enemyTank.Position) < 25f && (IsMyCanSeeEneTank() || IsMyTankPosCanSee(AimAdvanceAmountPosition(enemyTank))));
        }

        public enum MissileWarningType
        {
            Safe, Near, Advance, Locked
        }
    }









}