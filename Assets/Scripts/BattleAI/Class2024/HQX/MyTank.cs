using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using AI.FiniteStateMachine;
using Main;
using AI.Base;
using UnityEngine.AI;
using AI.RuleBased;
using AI.BehaviourTree;

namespace HQX
{
    /// <summary>
    /// 决策层的设计，Condition的预先指定
    /// </summary>

    /// <summary>
    /// 检查敌我坦克之间是否通透，用作开火判断使用
    /// </summary>
    class IsFireLineClear : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            // 获取我方坦克和敌方坦克
            Tank myTank = (Tank)agent;
            Tank enemyTank = Match.instance.GetOppositeTank(myTank.Team);

            // 检查我方坦克和敌方坦克之间是否存在通视
            return new IsBothCanSeeOpp(myTank.FirePos, myTank.FirePos + myTank.TurretAiming * Vector3.Distance(myTank.FirePos, enemyTank.Position)).IsTrue(agent);

        }
    }

    /// <summary>
    /// 单独抽象出来的条件，用于判断A物体和B物体之间是否有阻挡
    /// </summary>
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

    /// <summary>
    /// 检查我方坦克位置是否能看到指定位置
    /// </summary>
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

    /// <summary>
    /// 检查我方是否能看到敌方坦克，若敌方坦克未死亡，检查我方坦克位置是否能看到敌方坦克前方和后方各3.5个单位位置（用于预判射击用）
    /// </summary>
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

    /// <summary>
    /// 检查我方瞄准角度是否小于指定角度
    /// </summary>
    class IsMyAimingAngleLessThan : Condition
    {
        private Tank enemyTankC;
        private float angleC;
        
        //在调用这个类的时候，通过对其构造函数进行调用并赋值，实现了外部数据对内传参，而内部数据仍然保持封装（不会被污染）
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

    /// <summary>
    /// 检查比赛剩余时间是否小于指定时间
    /// </summary>
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

    /// <summary>
    /// 检查我方坦克到指定位置的路径距离是否小于指定距离
    /// </summary>
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

    /// <summary>
    /// 检测我方分数是否比敌人高
    /// </summary>
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

    /// <summary>
    /// 检测敌方是否死亡
    /// </summary>
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

    /// <summary>
    /// 抽象出来的比较类，用于比较A和B的大小
    /// </summary>
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

    /// <summary>
    /// 用于检测收到的warningType与传入的是否一致
    /// </summary>
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

    /// <summary>
    /// 判断A和B的距离是否小于给定值
    /// </summary>
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

    /// <summary>
    /// 判断超星是否存在
    /// </summary>
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



    /// <summary>
    ///行为&运动层的设计，StateMachine的建立
    /// </summary>
    enum EStateType
    {
        FindEnemy, FindStar, BackToHome
    }
    class FindEnemyState : State
    {
        public FindEnemyState()
        {
            StateType = (int)EStateType.FindEnemy;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            if (t.HP <= 30)
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null || oppTank.IsDead)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(oppTank.Position);
            return this;
        }
    }
    class BackToHomeState : State
    {
        public BackToHomeState()
        {
            StateType = (int)EStateType.BackToHome;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            if (t.HP >= 70)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(Match.instance.GetRebornPos(t.Team));
            return this;
        }
    }
    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)EStateType.FindStar;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false && t.HP > oppTank.HP)
            {
                return m_StateMachine.Transition((int)EStateType.FindEnemy);
            }
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStar = s;
                    }
                }
            }
            //if low hp and no super star
            if (t.HP <= 30 && (hasStar == false || nearestStar.IsSuperStar == false))
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            if (hasStar == true)
            {
                t.Move(nearestStar.Position);
            }
            return this;
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

        //enemy tank parameters
        Queue<Vector3> TankSpeeds = new Queue<Vector3>();
        private float lastTime;
        private Vector3 lastPos;
        private Vector3 averageSpeed;
        private float maxSpeed;

        private StateMachine m_FSM;    //状态机声明

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

        //Condition GetStarDropByRule;

        //General-Condition
        Condition MatchTimeLessThan;
        Condition MoreRecover;
        //------------------------




        private void OnDrawGizmos()
        {
            Gizmos.DrawSphere(AimAdvanceAmountPosition(enemyTank), 0.5f);
            Gizmos.DrawSphere(AimAdvanceAmountPosition(myTank), 0.5f);
            //Gizmos.DrawWireCube(myTank.Position + myTank.Forward * 6, new Vector3(4, 8, 4));
            Gizmos.DrawWireSphere(myTank.Position + myTank.Forward * 1.5f + Vector3.up * 3f, 2.5f);
        }

        protected override void OnStart()
        {
            base.OnStart();
            //状态机初始化
            m_FSM = new StateMachine(this);
            m_FSM.AddState(new FindEnemyState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new FindStarState());
            m_FSM.SetDefaultState((int)EStateType.FindStar);
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
            lastPos = enemyTank.Position;
            lastTime = Time.time;
            maxSpeed = 10.0f;





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

        /// <summary>
        /// Condition的更新
        /// </summary>
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

            //GetStarDropByRule = new IsMyTankPathDistanceLessThan(Star,5f);
        }

        /// <summary>
        /// 信息层的更新
        /// </summary>
        /// <param name="hitWarningFlagC"></param>
        private void KnowledgeLayer(out MissileWarningType hitWarningFlagC)
        {
            DetectFlyingMissile();
            hitWarningFlagC = HitWarning();//hitWarningFlagC将作为输出
            averageSpeed = CalculateAverageTankSpeed(lastTime, TankSpeeds,lastPos);
            ConditionBaseUpdate();
        }

        /// <summary>
        /// 火控系统策略(基于Condition)
        /// </summary>
        private void FireControlSystemAct()
        {
            //火控系统--瞄准
            if (!enemyTank.IsDead)
            {
                //if(enemyTank)
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

        /// <summary>
        /// 移动系统策略(基于Condition)
        /// </summary>
        private void MoveingControlSystemAct()
        {
            if (RebornBase.IsTrue(this))
            {
                DropByStar(15f);
                Move(myRebornPos);
                //Debug.LogWarning("保命");
            }
            else if (EvadeRule.IsTrue(this))
            {
                //hitWarningFlag != MissileWarningType.Safe && Vector3.Distance(myTank.transform.position, enemyTank.transform.position) > EvadeMinDistance
                //&& !AvoidEvadeConditionPlus()
                EvadeMissile(WarningMissles[0], hitWarningFlag);
                //Debug.LogWarning("正在规避");
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

            //其他条件省略，这里生命值判断的逻辑是：
            //获取到敌我生命值，并同除25来得到一个比例，并用Mathf.CeilToInt函数向上取整，再进行比较
            else if (!enemyTank.IsDead && ((IsMyTankPosCanSee(AimAdvanceAmountPosition(enemyTank)) || IsMyCanSeeEneTank()) && ((Mathf.CeilToInt(myTank.HP / 20f)) > Mathf.CeilToInt(enemyTank.HP / 20f)) && Vector3.Distance(enemyTank.Position, match.GetRebornPos(enemyTank.Team)) > 12f))
            {

                Move(enemyTank.Position);
                //Debug.LogWarning("追击敌人");
            }
            else if ((Mathf.CeilToInt(myTank.HP / 20f)) + 1 < Mathf.CeilToInt(enemyTank.HP / 20f))
            {
                DropByStar(15f);
                Move(myRebornPos);
                //Debug.LogWarning("血量比敌人少得多，回家补血");

            }
            //如果比赛剩余时间少于70秒且我的分数比敌方高，坦克将尝试移动到敌方区域最近的星星位置；如果没有星星，则移动回复活位置
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
            else if(myTank.HP>80)
            {
                DropByStar(15f);
                Move(new Vector3(19.18f, 0.50f, 11.59f));
                //Debug.LogWarning("到准备点");
            }
            else
            {
                DropByStar(15f);
                Move(myRebornPos);
                //Debug.LogWarning("回家");

            }
            //if (Input.GetMouseButton(1))
            //{
            //    HandControl();
            //}

        }

        /// <summary>
        /// 决策层和行为层
        /// </summary>
        private void StratedgyAndBehaviorLayer()
        {
            FireControlSystemAct();
            MoveingControlSystemAct();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            //信息层
            KnowledgeLayer(out hitWarningFlag);
            //决策层和行为层Update
            StratedgyAndBehaviorLayer();

            //行为层Update
            //Tank oppTank = Match.instance.GetOppositeTank(Team);
            //if (oppTank != null && oppTank.IsDead == false)
            //{
            //    TurretTurnTo(oppTank.Position);
            //    if (CanSeeOthers(oppTank))
            //    {
            //        Fire();
            //    }
            //}
            //else
            //{
            //    TurretTurnTo(Position + Forward);
            //}
            ////state update
            //m_FSM.Update();
            //DebugLayer();

        }





        //-------------------------------ExtraLogic----------------------------------------


        private bool IsFireLineClear()
        {
            Debug.DrawLine(FirePos, FirePos + TurretAiming * Vector3.Distance(FirePos, enemyTank.Position), Color.black);
            return IsBothCanSeeOpp(FirePos, FirePos + TurretAiming * Vector3.Distance(FirePos, enemyTank.Position));

        }

        /// <summary>
        /// 计算从坦克当前位置到目标位置的实际距离（基于NavMesh，非直线距离）
        /// </summary>
        /// <param name="TarPos"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 计算从敌方坦克当前位置到目标位置的实际距离（基于NavMesh，非直线距离）
        /// </summary>
        /// <param name="TarPos"></param>
        /// <returns></returns>
        private float EnemyPathDistance(Vector3 TarPos)
        {
            //NavMeshPath path = new NavMeshPath();
            //EnemyAgent.CalculatePath(TarPos, path);
            var oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank == null || oppTank.IsDead)
            {
                return 0f;
            }

            var path = oppTank.CaculatePath(TarPos);
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

        /// <summary>
        /// 找到离我方最近，同时敌方距离比我方远的星星
        /// </summary>
        /// <returns></returns>
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
                    //当我方坦克到该星星路径最短，且敌方路径到该星星的距离比我方远时，会把该星星作为最近星星传回
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

        /// <summary>
        /// 找距离玩家最近的星星
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 找敌方场地中离我方坦克最近的星星
        /// </summary>
        /// <returns></returns>
        private Star EnemyAreaNearestStar()
        {
            List<Star> stars = new List<Star>();
            foreach (var item in match.GetStars().Values)
            {
                //从我方复活位置朝前，判断是否能直接看到星星，如果看不到，则大概率属于敌方场地中
                if (!IsBothCanSeeOpp(myRebornPos + Vector3.up, item.Position))
                {
                    stars.Add(item);
                }

            }

            if (stars.Count > 0)
            {
                float mindis = 10000f;
                int starId = -1;
                //遍历所有星星，如果有更近的星星，则更新星星ID和距离。
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

        /// <summary>
        /// 检测飞行中的导弹，将敌方导弹存入Missile列表中
        /// </summary>
        private void DetectFlyingMissile()
        {

            Missles = new List<Missile>();
            foreach (var item in match.GetOppositeMissiles(Team).Values)
            {
                Missles.Add(item);
            }

        }

        /// <summary>
        /// 为Missile列表中的导弹判断是否有击中风险，返回该导弹的风险枚举型
        /// </summary>
        private MissileWarningType HitWarning()
        {

            WarningMissles = new List<Missile>();
            if (Missles.Count != 0 && !myTank.IsDead)
            {
                foreach (var missile in Missles)
                {
                    //判断导弹沿当前速度方向移动一段距离后，我的坦克是否能够在x-z平面上看到导弹(预测算法：导弹的当前位置加上导弹速度的单位向量乘以坦克和导弹在x-z平面上的距离)
                    if (!IsBothCanSeeOpp(missile.Position, missile.Position + missile.Velocity.normalized * Vector3.Distance(myTank.Position, new Vector3(missile.Position.x, myTank.Position.y, missile.Position.z))))
                    {
                        continue;
                    }

                    //tmp是一个数组，用于存放以（坦克的位置出发，向坦克的前方移动1.5单位，然后向上移动3单位）为中心，半径为2.5f，属于Layer_Entity Mask物体
                    //这样子设计侧重于正面对枪时候的检测
                    var tmp = Physics.OverlapSphere(myTank.Position + myTank.Forward * 1.5f + Vector3.up * 3f, 2.5f, LayerMask.GetMask("Layer_Entity"));
                    //var tmp = Physics.OverlapSphere(myTank.Position + Vector3.up * 3f, 3.5f, LayerMask.GetMask("Layer_Entity"));//改变球体位置测试

                    //用胶囊体替代测试
                    // 胶囊体的两个端点分别设置在坦克前方和后方，以覆盖更大的检测范围
                    //Vector3 point1 = myTank.Position + Vector3.up * 3f; // 上端点
                    //Vector3 point2 = myTank.Position - myTank.Forward * 1.5f + Vector3.up * 3f; // 下端点
                    //float capsuleRadius = 2.5f;
                    //var tmp = Physics.OverlapCapsule(point1, point2, capsuleRadius, LayerMask.GetMask("Layer_Entity"));

                    //Gizmos.DrawSphere(myTank.Position + Vector3.up * 3f, 3.5f);

                    //遍历tmp中的所有元素,如果是敌方导弹，则把导弹放入WarningMissles列表中，并返回MissileWarningType.Near
                    if (tmp.Length > 0 && tmp[0].GetComponentInParent<Missile>().Team != myTank.Team)
                    {
                        if (WarningMissles.Count == 0)
                        {
                            WarningMissles.Add(missile);
                        }
                        else
                        {
                            WarningMissles[0] = missile;
                        }
                        //Debug.Log("坦克邻近受击预警");
                        return MissileWarningType.Near;
                    }
                    //坦克当前位置被锁定预警，用三条射线来检测导弹的预测行进路线，分别是：
                    //从导弹当前位置直接向导弹的速度方向发射
                    //从导弹当前位置向右偏移1.6单位后向导弹的速度方向发射
                    //从导弹当前位置向左偏移1.6单位后向导弹的速度方向发射
                    //如何任何一条射线碰到了Tank Mask的物体（也就是我方坦克），将这些导弹存入WarningMissles列表，并返回Locked 导弹预警枚举类型
                    else if (Physics.Raycast(missile.Position, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank) || 
                             Physics.Raycast(missile.Position + missile.transform.right * 1.62f, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank) || 
                             Physics.Raycast(missile.Position - missile.transform.right * 1.62f, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank))
                    {
                        WarningMissles.Add(missile);
                        Debug.DrawRay(missile.Position, missile.Velocity, Color.black);
                        //EvadeMissile(missile);
                        //Debug.Log("坦克中心受击预警");
                        return MissileWarningType.Locked;

                    }
                    //提前量受击预警，检查导弹的速度方向与坦克的预期位置之间的角度是否小于30度，是则返回Advance导弹预警枚举类型
                    else if (Vector3.Angle(AimAdvanceAmountPosition(myTank) - missile.Position, missile.Velocity) < 30f)
                    {
                        WarningMissles.Add(missile);
                        //Debug.Log("提前量受击预警");
                        return MissileWarningType.Advance;
                    }
                }
            }
            return MissileWarningType.Safe;//其他的就是乱打的导弹，不用管
        }

        /// <summary>
        /// 规避导弹函数，对于不同的预警类型有不同的处理逻辑
        /// </summary>
        /// <param name="warnmissile"></param>
        /// <param name="warningType"></param>
        private void EvadeMissile(Missile warnmissile, MissileWarningType warningType)
        {
            Vector3 evadeOffset = myTank.transform.forward * 3f;                                //计算一个名为 evadeOffset 的向量，它是坦克前进方向的3倍。这个向量用于在规避时增加坦克的移动距离
            float angle = Vector3.Angle(warnmissile.Velocity, myTank.Forward);                  //计算导弹速度向量和坦克前进方向之间的角度（这里没用上）
            Debug.Assert(warningType != MissileWarningType.Safe);                               //使用 Debug.Assert 确保 warningType 不是 MissileWarningType.Safe
            var dir = Vector3.Cross(warnmissile.Velocity.normalized, myTank.transform.up) * 7f; //规避方向垂直于导弹速度方向，使用 Vector3.Cross 计算导弹速度向量和坦克上方向量的叉积，得到一个垂直于这两个向量的新向量 dir，并将其长度扩大7倍

            //Debug.LogError(Vector3.Distance(warnmissile.Position, myTank.Position) + " " + (warnmissile.Velocity.magnitude));

            //处理Locked类型的导弹策略A
            if (warningType == MissileWarningType.Locked)
            {
                //有正在处理的导弹，且传入的导弹和正在躲避的导弹哈希值一致
                if (AvoidingMissile != null && warnmissile.GetHashCode() == AvoidingMissile.GetHashCode())
                {
                    //通过AvoidingDir来决策如何躲避(+/-)
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
                //如果没有正在处理躲避的导弹，则把传入的导弹作为正在处理的躲避的导弹
                AvoidingMissile = warnmissile;
            }



            //处理Locked类型的导弹策略B
            if (warningType == MissileWarningType.Locked)
            {
                //Debug.LogWarning("采用蛇形规避");
                //如果没有检测到墙面（DetectWall），则根据是否能看到对方（IsBothCanSeeOpp）来选择规避方向。
                if (!DetectWall(dir + evadeOffset) && !DetectWall(-dir + evadeOffset))
                {

                    //根据地形掩体选择规避方向，如果坦克在 dir + evadeOffset 方向上不能看到导弹，则向该方向规避；如果不能，则检查 -dir + evadeOffset 方向
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
                //单方向有墙情况的处理
                else if (!DetectWall(dir + evadeOffset))
                {
                    //Debug.Log("规避时检测到墙面");
                    Move(myTank.transform.position + dir + evadeOffset);
                    Debug.DrawLine(myTank.Position, myTank.transform.position + dir + evadeOffset, Color.red, during);
                    AvoidingDir = true;
                }
                else if (!DetectWall(-dir + evadeOffset))
                {
                    //Debug.Log("规避时检测到墙面");
                    Move(myTank.transform.position - dir + evadeOffset);
                    Debug.DrawLine(myTank.Position, myTank.transform.position - dir + evadeOffset, Color.red, during);
                    AvoidingDir = false;
                }
            }
            //Near和Advance类型的导弹都是通过急停来规避的
            else if (warningType == MissileWarningType.Near)
            {
                //Debug.LogWarning("车前有导弹，紧急停车");
                Move(myTank.Position);

            }
            else if (warningType == MissileWarningType.Advance)
            {
                //Debug.LogWarning("提前量导弹，急停躲避");
                Move(myTank.Position);
            }
            else
            {
                //Debug.LogError("若报错，须处理");
            }

        }

        /// <summary>
        /// 用于检测坦克当前位置是否有墙面或场景中的其他障碍物在指定方向上
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private bool DetectWall(Vector3 dir)
        {
            return Physics.Raycast(myTank.transform.position, dir, dir.magnitude, PhysicsUtils.LayerMaskScene);
        }

        /// <summary>
        /// 计算在射击一个移动中的敌方坦克时应该瞄准的位置
        /// </summary>
        /// <param name="TargetTank"></param>
        /// <param name="AdvanceWeight"></param>
        /// <returns></returns>
        public Vector3 AimAdvanceAmountPosition(Tank TargetTank, float AdvanceWeight = 0.85f)
        {
            //Test
            //if (TargetTank.name == enemyTank.name)
            //{
            //    AdvanceWeight = 0.7f;
            //}

            Vector3 AimPos;
            float distance = Vector3.Distance(myTank.Position, enemyTank.Position); //敌我之间距离
            float missileFlyingTime = distance / match.GlobalSetting.MissileSpeed;  //计算导弹飞到目标点的时间
            //AdvanceWeight调和提前量和坦克位置
            //如果 AdvanceWeight 接近1，那么预测位置将更多地依赖于目标坦克的移动趋势。这意味着如果目标坦克以一定速度前进，预测位置将更远离目标坦克的当前位置，更靠近它未来的位置。这在目标坦克移动速度较快或者导弹飞行时间较长时特别有用。
            //如果 AdvanceWeight 接近0，那么预测位置将更接近目标坦克的当前位置。这可能在目标坦克几乎不移动或者导弹飞行时间非常短时更为适用。
            //AdvanceWeight = CalculateAdvanceWeight(averageSpeed,maxSpeed);//动态更新权重
            AimPos = TargetTank.Position + TargetTank.Forward + TargetTank.Velocity * missileFlyingTime * AdvanceWeight;
            return AimPos;
        }


        /// <summary>
        /// 用于测试传入的A物体和B物体之间是否有阻挡，未阻挡时返回T
        /// </summary>
        public bool IsBothCanSeeOpp(Vector3 APos, Vector3 BPos)
        {
            return !Physics.Linecast(APos, BPos, PhysicsUtils.LayerMaskScene);
        }
        /// <summary>
        /// 用于测试我方坦克和传入的物体之间是否有阻挡，未阻挡时返回T，嵌套使用IsBothCanSeeOpp
        /// </summary>
        private bool IsMyTankPosCanSee(Vector3 pos)
        {
            return IsBothCanSeeOpp(myTank.Position, pos);
        }
        /// <summary>
        /// 我方坦克是否有视线接触到敌方坦克的前方或后方某个点，从而判断是否有可能进行有效射击
        /// </summary>
        /// <returns></returns>
        private bool IsMyCanSeeEneTank()
        {
            if (enemyTank.IsDead)
            {
                return false;
            }
            return (IsMyTankPosCanSee(enemyTank.Position + enemyTank.Forward * 3.5f) || IsMyTankPosCanSee(enemyTank.Position - enemyTank.Forward * 3.5f));
        }

        /// <summary>
        /// 顺路拿一下星星
        /// </summary>
        /// <param name="range"></param>
        private void DropByStar(float range)
        {
            if (NearestStarAndFar() != null && Vector3.Distance(NearestStar().Position,myTank.Position) < range)
            {
                Move(NearestStar().Position);
            }
        }

        /// <summary>
        /// 计算敌方坦克的速度，以此来优化瞄准算法
        /// </summary>
        /// <param name="lastTime"></param>
        /// <param name="TankSpeeds"></param>
        /// <param name="lastPos"></param>
        /// <returns></returns>
        private Vector3 CalculateAverageTankSpeed(float lastTime, Queue<Vector3> TankSpeeds, Vector3 lastPos)
        {
            int maxSteps = 3;

            if (enemyTank != null && enemyTank.IsDead == false)
            {
                Vector3 v = (enemyTank.transform.position - lastPos) / (Time.time - lastTime);
                //Vector3 v = (enemyTank.transform.position - lastPos) / Time.deltaTime;
                if (TankSpeeds.Count >= maxSteps)
                {
                    TankSpeeds.Dequeue();
                }
                TankSpeeds.Enqueue(v);

                Vector3 speed = Vector3.zero;
                foreach (var s in TankSpeeds)
                {
                    speed += s;
                }
                if (speed == Vector3.zero)
                    return Vector3.zero;
                else
                {
                    // 更新 lastPos 和 lastTime 为当前的位置和时间
                    lastPos = enemyTank.transform.position;
                    lastTime = Time.time;
                    return speed / TankSpeeds.Count; 
                }
            }
            else
                return Vector3.zero;

        }

        /// <summary>
        /// 计算影响因子的权重
        /// </summary>
        /// <param name="targetVelocity"></param>
        /// <returns></returns>
        private  float CalculateAdvanceWeight(Vector3 targetVelocity , float maxSpeed)
        {

            // 计算速度的大小
            float speedMagnitude = Math.Abs(targetVelocity.magnitude);
            Debug.LogWarning(speedMagnitude);

            if(maxSpeed< speedMagnitude)
            {
                maxSpeed = speedMagnitude;
            }

            // 将速度大小映射到 (0, 1) 区间内
            //float advanceWeight = Math.Min(speedMagnitude / maxSpeed, 1.0f);
            float advanceWeight = Mathf.Lerp(0f, 1f, speedMagnitude / maxSpeed);

            //Debug.LogWarning(advanceWeight);

            //return advanceWeight;
            return speedMagnitude / maxSpeed;
        }

        private bool AvoidEvadeConditionPlus()
        {
            return (enemyTank.HP < 26f && myTank.HP >= 26f && Vector3.Distance(myTank.Position, enemyTank.Position) < 25f && (IsMyCanSeeEneTank() || IsMyTankPosCanSee(AimAdvanceAmountPosition(enemyTank))));
        }

        public enum MissileWarningType
        {
            Safe, Near, Advance, Locked
        }

        public override string GetName()
        {
            return "HQX";
        }
    }

}




