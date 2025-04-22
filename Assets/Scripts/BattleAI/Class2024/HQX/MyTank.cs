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
    /// ���߲����ƣ�Condition��Ԥ��ָ��
    /// </summary>

    /// <summary>
    /// ������̹��֮���Ƿ�ͨ͸�����������ж�ʹ��
    /// </summary>
    class IsFireLineClear : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            // ��ȡ�ҷ�̹�˺͵з�̹��
            Tank myTank = (Tank)agent;
            Tank enemyTank = Match.instance.GetOppositeTank(myTank.Team);

            // ����ҷ�̹�˺͵з�̹��֮���Ƿ����ͨ��
            return new IsBothCanSeeOpp(myTank.FirePos, myTank.FirePos + myTank.TurretAiming * Vector3.Distance(myTank.FirePos, enemyTank.Position)).IsTrue(agent);

        }
    }

    /// <summary>
    /// ������������������������ж�A�����B����֮���Ƿ����赲
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
    /// ����ҷ�̹��λ���Ƿ��ܿ���ָ��λ��
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
    /// ����ҷ��Ƿ��ܿ����з�̹�ˣ����з�̹��δ����������ҷ�̹��λ���Ƿ��ܿ����з�̹��ǰ���ͺ󷽸�3.5����λλ�ã�����Ԥ������ã�
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
    /// ����ҷ���׼�Ƕ��Ƿ�С��ָ���Ƕ�
    /// </summary>
    class IsMyAimingAngleLessThan : Condition
    {
        private Tank enemyTankC;
        private float angleC;
        
        //�ڵ���������ʱ��ͨ�����乹�캯�����е��ò���ֵ��ʵ�����ⲿ���ݶ��ڴ��Σ����ڲ�������Ȼ���ַ�װ�����ᱻ��Ⱦ��
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
    /// ������ʣ��ʱ���Ƿ�С��ָ��ʱ��
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
    /// ����ҷ�̹�˵�ָ��λ�õ�·�������Ƿ�С��ָ������
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
    /// ����ҷ������Ƿ�ȵ��˸�
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
    /// ���з��Ƿ�����
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
    /// ��������ıȽ��࣬���ڱȽ�A��B�Ĵ�С
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
    /// ���ڼ���յ���warningType�봫����Ƿ�һ��
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
    /// �ж�A��B�ľ����Ƿ�С�ڸ���ֵ
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
    /// �жϳ����Ƿ����
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
    ///��Ϊ&�˶������ƣ�StateMachine�Ľ���
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
        //private float maxSpeed;

        private StateMachine m_FSM;    //״̬������

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
            //״̬����ʼ��
            m_FSM = new StateMachine(this);
            m_FSM.AddState(new FindEnemyState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new FindStarState());
            m_FSM.SetDefaultState((int)EStateType.FindStar);
            //�������������ȡ
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
            //maxSpeed = 10.0f;





        }
        private void DebugLayer()
        {

            if (CanFire())
            {
                Debug.DrawLine(FirePos, FirePos + TurretAiming * Vector3.Distance(FirePos, enemyTank.Position), Color.black);
            }

            //��غ͹��Debug
            if (WarningMissles.Count > 0)
            {
                if (Vector3.Distance(WarningMissles[0].Position, enemyTank.Position) < 3.75f)
                {
                    Debug.LogError("�з����䵼��");
                }
                if (Vector3.Distance(WarningMissles[0].Position, myTank.Position + Vector3.up * 3) < 4f)
                {
                    Debug.LogError("�з���������");
                }
            }

            if (hitWarningFlag != MissileWarningType.Safe && (Vector3.Distance(myTank.transform.position, enemyTank.transform.position) < EvadeMinDistance))//&& !AvoidEvadeConditionPlus()
            {
                Debug.LogWarning("�����������������й��");
            }

        }

        /// <summary>
        /// Condition�ĸ���
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
        /// ��Ϣ��ĸ���
        /// </summary>
        /// <param name="hitWarningFlagC"></param>
        private void KnowledgeLayer(out MissileWarningType hitWarningFlagC)
        {
            DetectFlyingMissile();
            hitWarningFlagC = HitWarning();//hitWarningFlagC����Ϊ���
            averageSpeed = CalculateAverageTankSpeed(lastTime, TankSpeeds,lastPos);
            ConditionBaseUpdate();
        }

        /// <summary>
        /// ���ϵͳ����(����Condition)
        /// </summary>
        private void FireControlSystemAct()
        {
            //���ϵͳ--��׼
            if (!enemyTank.IsDead)
            {
                //if(enemyTank)
                TurretTurnTo(AimAdvanceAmountPosition(enemyTank));
            }
            else
            {
                TurretTurnTo(match.GetRebornPos(enemyTank.Team));
            }

            //���ϵͳ--����
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
        /// �ƶ�ϵͳ����(����Condition)
        /// </summary>
        private void MoveingControlSystemAct()
        {
            if (RebornBase.IsTrue(this))
            {
                DropByStar(15f);
                Move(myRebornPos);
                //Debug.LogWarning("����");
            }
            else if (EvadeRule.IsTrue(this))
            {
                //hitWarningFlag != MissileWarningType.Safe && Vector3.Distance(myTank.transform.position, enemyTank.transform.position) > EvadeMinDistance
                //&& !AvoidEvadeConditionPlus()
                EvadeMissile(WarningMissles[0], hitWarningFlag);
                //Debug.LogWarning("���ڹ��");
            }
            else if (SuperStarRuleA.IsTrue(this))
            {
                Move(new Vector3(0, 0.5f, 0));
                //Debug.LogWarning("����ǿ��ָ��ǰ�����ǵ�");
            }
            else if (SuperStarRuleB.IsTrue(this))
            {
                Move(new Vector3(0, 0.5f, 0));
                //Debug.LogWarning("����ǿ��ָ��ǰ�����ǵ�");
            }
            else if (MoreRecover.IsTrue(this))
            {
                //Vector3.Distance(myTank.Position, myRebornPos) < 2f && myTank.HP < 60f
                Move(myRebornPos);
                Debug.LogWarning("��ػ�Ѫ");
            }

            //��������ʡ�ԣ���������ֵ�жϵ��߼��ǣ�
            //��ȡ����������ֵ����ͬ��25���õ�һ������������Mathf.CeilToInt��������ȡ�����ٽ��бȽ�
            else if (!enemyTank.IsDead && ((IsMyTankPosCanSee(AimAdvanceAmountPosition(enemyTank)) || IsMyCanSeeEneTank()) && ((Mathf.CeilToInt(myTank.HP / 20f)) > Mathf.CeilToInt(enemyTank.HP / 20f)) && Vector3.Distance(enemyTank.Position, match.GetRebornPos(enemyTank.Team)) > 12f))
            {

                Move(enemyTank.Position);
                //Debug.LogWarning("׷������");
            }
            else if ((Mathf.CeilToInt(myTank.HP / 20f)) + 1 < Mathf.CeilToInt(enemyTank.HP / 20f))
            {
                DropByStar(15f);
                Move(myRebornPos);
                //Debug.LogWarning("Ѫ���ȵ����ٵö࣬�ؼҲ�Ѫ");

            }
            //�������ʣ��ʱ������70�����ҵķ����ȵз��ߣ�̹�˽������ƶ����з��������������λ�ã����û�����ǣ����ƶ��ظ���λ��
            else if (new AndCondition(new IsMatchTimeLessThan(70f), new IsMyScoreGreaterThan(enemyTank.Score + 10)).IsTrue(this))
            {
                //match.RemainingTime < 70f && myTank.Score - 10 >= enemyTank.Score

                if (EnemyAreaNearestStar() != null)
                {
                    Move(EnemyAreaNearestStar().Position);
                    //Debug.LogWarning("�ҵг�����");
                }
                else
                {
                    Move(myRebornPos);
                    //Debug.LogWarning("�ؼ�");
                }
            }
            else if (!enemyTank.IsDead && NearestStarAndFar() != null && (myTank.HP > 25) && Mathf.CeilToInt(enemyTank.HP / 25f) >= (Mathf.CeilToInt(myTank.HP / 25f)))
            {
                Move(NearestStarAndFar().Position);
                //Debug.LogWarning("�Ұ�ȫ����");
            }
            else if (NearestStar() != null && (myTank.HP > 25))
            {
                Move(NearestStar().Position);
                //Debug.LogWarning("�ҽ�����");
            }
            else if(myTank.HP>80)
            {
                DropByStar(15f);
                Move(new Vector3(19.18f, 0.50f, 11.59f));
                //Debug.LogWarning("��׼����");
            }
            else
            {
                DropByStar(15f);
                Move(myRebornPos);
                //Debug.LogWarning("�ؼ�");

            }
            //if (Input.GetMouseButton(1))
            //{
            //    HandControl();
            //}

        }

        /// <summary>
        /// ���߲����Ϊ��
        /// </summary>
        private void StratedgyAndBehaviorLayer()
        {
            FireControlSystemAct();
            MoveingControlSystemAct();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            //��Ϣ��
            KnowledgeLayer(out hitWarningFlag);
            //���߲����Ϊ��Update
            StratedgyAndBehaviorLayer();

            //��Ϊ��Update
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
        /// �����̹�˵�ǰλ�õ�Ŀ��λ�õ�ʵ�ʾ��루����NavMesh����ֱ�߾��룩
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
                //TotalDistance += Vector3.Distance(myTank.transform.position, path.corners[0]) + Vector3.Distance(TarPos, path.corners[path.corners.Length - 1]);�Ѿ��ӹ���
            }



            return TotalDistance;
        }

        /// <summary>
        /// ����ӵз�̹�˵�ǰλ�õ�Ŀ��λ�õ�ʵ�ʾ��루����NavMesh����ֱ�߾��룩
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
                //TotalDistance += Vector3.Distance(myTank.transform.position, path.corners[0]) + Vector3.Distance(TarPos, path.corners[path.corners.Length - 1]);�Ѿ��ӹ���
            }



            return TotalDistance;
        }

        /// <summary>
        /// �ҵ����ҷ������ͬʱ�з�������ҷ�Զ������
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
                    //���ҷ�̹�˵�������·����̣��ҵз�·���������ǵľ�����ҷ�Զʱ����Ѹ�������Ϊ������Ǵ���
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
        /// �Ҿ���������������
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
        /// �ҵз����������ҷ�̹�����������
        /// </summary>
        /// <returns></returns>
        private Star EnemyAreaNearestStar()
        {
            List<Star> stars = new List<Star>();
            foreach (var item in match.GetStars().Values)
            {
                //���ҷ�����λ�ó�ǰ���ж��Ƿ���ֱ�ӿ������ǣ���������������������ڵз�������
                if (!IsBothCanSeeOpp(myRebornPos + Vector3.up, item.Position))
                {
                    stars.Add(item);
                }

            }

            if (stars.Count > 0)
            {
                float mindis = 10000f;
                int starId = -1;
                //�����������ǣ�����и��������ǣ����������ID�;��롣
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
        /// �������еĵ��������з���������Missile�б���
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
        /// ΪMissile�б��еĵ����ж��Ƿ��л��з��գ����ظõ����ķ���ö����
        /// </summary>
        private MissileWarningType HitWarning()
        {

            WarningMissles = new List<Missile>();
            if (Missles.Count != 0 && !myTank.IsDead)
            {
                foreach (var missile in Missles)
                {
                    //�жϵ����ص�ǰ�ٶȷ����ƶ�һ�ξ�����ҵ�̹���Ƿ��ܹ���x-zƽ���Ͽ�������(Ԥ���㷨�������ĵ�ǰλ�ü��ϵ����ٶȵĵ�λ��������̹�˺͵�����x-zƽ���ϵľ���)
                    if (!IsBothCanSeeOpp(missile.Position, missile.Position + missile.Velocity.normalized * Vector3.Distance(myTank.Position, new Vector3(missile.Position.x, myTank.Position.y, missile.Position.z))))
                    {
                        continue;
                    }

                    //tmp��һ�����飬���ڴ���ԣ�̹�˵�λ�ó�������̹�˵�ǰ���ƶ�1.5��λ��Ȼ�������ƶ�3��λ��Ϊ���ģ��뾶Ϊ2.5f������Layer_Entity Mask����
                    //��������Ʋ����������ǹʱ��ļ��
                    var tmp = Physics.OverlapSphere(myTank.Position + myTank.Forward * 1.5f + Vector3.up * 3f, 2.5f, LayerMask.GetMask("Layer_Entity"));
                    //var tmp = Physics.OverlapSphere(myTank.Position + Vector3.up * 3f, 3.5f, LayerMask.GetMask("Layer_Entity"));//�ı�����λ�ò���

                    //�ý������������
                    // ������������˵�ֱ�������̹��ǰ���ͺ󷽣��Ը��Ǹ���ļ�ⷶΧ
                    //Vector3 point1 = myTank.Position + Vector3.up * 3f; // �϶˵�
                    //Vector3 point2 = myTank.Position - myTank.Forward * 1.5f + Vector3.up * 3f; // �¶˵�
                    //float capsuleRadius = 2.5f;
                    //var tmp = Physics.OverlapCapsule(point1, point2, capsuleRadius, LayerMask.GetMask("Layer_Entity"));

                    //Gizmos.DrawSphere(myTank.Position + Vector3.up * 3f, 3.5f);

                    //����tmp�е�����Ԫ��,����ǵз���������ѵ�������WarningMissles�б��У�������MissileWarningType.Near
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
                        //Debug.Log("̹���ڽ��ܻ�Ԥ��");
                        return MissileWarningType.Near;
                    }
                    //̹�˵�ǰλ�ñ�����Ԥ������������������⵼����Ԥ���н�·�ߣ��ֱ��ǣ�
                    //�ӵ�����ǰλ��ֱ���򵼵����ٶȷ�����
                    //�ӵ�����ǰλ������ƫ��1.6��λ���򵼵����ٶȷ�����
                    //�ӵ�����ǰλ������ƫ��1.6��λ���򵼵����ٶȷ�����
                    //����κ�һ������������Tank Mask�����壨Ҳ�����ҷ�̹�ˣ�������Щ��������WarningMissles�б�������Locked ����Ԥ��ö������
                    else if (Physics.Raycast(missile.Position, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank) || 
                             Physics.Raycast(missile.Position + missile.transform.right * 1.62f, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank) || 
                             Physics.Raycast(missile.Position - missile.transform.right * 1.62f, missile.Velocity.normalized, Vector3.Distance(myTank.Position + Vector3.up * (missileHeight - 0.5f), missile.Position) + 3f, PhysicsUtils.LayerMaskTank))
                    {
                        WarningMissles.Add(missile);
                        Debug.DrawRay(missile.Position, missile.Velocity, Color.black);
                        //EvadeMissile(missile);
                        //Debug.Log("̹�������ܻ�Ԥ��");
                        return MissileWarningType.Locked;

                    }
                    //��ǰ���ܻ�Ԥ������鵼�����ٶȷ�����̹�˵�Ԥ��λ��֮��ĽǶ��Ƿ�С��30�ȣ����򷵻�Advance����Ԥ��ö������
                    else if (Vector3.Angle(AimAdvanceAmountPosition(myTank) - missile.Position, missile.Velocity) < 30f)
                    {
                        WarningMissles.Add(missile);
                        //Debug.Log("��ǰ���ܻ�Ԥ��");
                        return MissileWarningType.Advance;
                    }
                }
            }
            return MissileWarningType.Safe;//�����ľ����Ҵ�ĵ��������ù�
        }

        /// <summary>
        /// ��ܵ������������ڲ�ͬ��Ԥ�������в�ͬ�Ĵ����߼�
        /// </summary>
        /// <param name="warnmissile"></param>
        /// <param name="warningType"></param>
        private void EvadeMissile(Missile warnmissile, MissileWarningType warningType)
        {
            Vector3 evadeOffset = myTank.transform.forward * 3f;                                //����һ����Ϊ evadeOffset ������������̹��ǰ�������3����������������ڹ��ʱ����̹�˵��ƶ�����
            float angle = Vector3.Angle(warnmissile.Velocity, myTank.Forward);                  //���㵼���ٶ�������̹��ǰ������֮��ĽǶȣ�����û���ϣ�
            Debug.Assert(warningType != MissileWarningType.Safe);                               //ʹ�� Debug.Assert ȷ�� warningType ���� MissileWarningType.Safe
            var dir = Vector3.Cross(warnmissile.Velocity.normalized, myTank.transform.up) * 7f; //��ܷ���ֱ�ڵ����ٶȷ���ʹ�� Vector3.Cross ���㵼���ٶ�������̹���Ϸ������Ĳ�����õ�һ����ֱ�������������������� dir�������䳤������7��

            //Debug.LogError(Vector3.Distance(warnmissile.Position, myTank.Position) + " " + (warnmissile.Velocity.magnitude));

            //����Locked���͵ĵ�������A
            if (warningType == MissileWarningType.Locked)
            {
                //�����ڴ���ĵ������Ҵ���ĵ��������ڶ�ܵĵ�����ϣֵһ��
                if (AvoidingMissile != null && warnmissile.GetHashCode() == AvoidingMissile.GetHashCode())
                {
                    //ͨ��AvoidingDir��������ζ��(+/-)
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
                //���û�����ڴ����ܵĵ�������Ѵ���ĵ�����Ϊ���ڴ���Ķ�ܵĵ���
                AvoidingMissile = warnmissile;
            }



            //����Locked���͵ĵ�������B
            if (warningType == MissileWarningType.Locked)
            {
                //Debug.LogWarning("�������ι��");
                //���û�м�⵽ǽ�棨DetectWall����������Ƿ��ܿ����Է���IsBothCanSeeOpp����ѡ���ܷ���
                if (!DetectWall(dir + evadeOffset) && !DetectWall(-dir + evadeOffset))
                {

                    //���ݵ�������ѡ���ܷ������̹���� dir + evadeOffset �����ϲ��ܿ�������������÷����ܣ�������ܣ����� -dir + evadeOffset ����
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
                        //���ݳ�ͷ�����ƫ��ѡ���ܷ���
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
                //��������ǽ����Ĵ���
                else if (!DetectWall(dir + evadeOffset))
                {
                    //Debug.Log("���ʱ��⵽ǽ��");
                    Move(myTank.transform.position + dir + evadeOffset);
                    Debug.DrawLine(myTank.Position, myTank.transform.position + dir + evadeOffset, Color.red, during);
                    AvoidingDir = true;
                }
                else if (!DetectWall(-dir + evadeOffset))
                {
                    //Debug.Log("���ʱ��⵽ǽ��");
                    Move(myTank.transform.position - dir + evadeOffset);
                    Debug.DrawLine(myTank.Position, myTank.transform.position - dir + evadeOffset, Color.red, during);
                    AvoidingDir = false;
                }
            }
            //Near��Advance���͵ĵ�������ͨ����ͣ����ܵ�
            else if (warningType == MissileWarningType.Near)
            {
                //Debug.LogWarning("��ǰ�е���������ͣ��");
                Move(myTank.Position);

            }
            else if (warningType == MissileWarningType.Advance)
            {
                //Debug.LogWarning("��ǰ����������ͣ���");
                Move(myTank.Position);
            }
            else
            {
                //Debug.LogError("�������봦��");
            }

        }

        /// <summary>
        /// ���ڼ��̹�˵�ǰλ���Ƿ���ǽ��򳡾��е������ϰ�����ָ��������
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private bool DetectWall(Vector3 dir)
        {
            return Physics.Raycast(myTank.transform.position, dir, dir.magnitude, PhysicsUtils.LayerMaskScene);
        }

        /// <summary>
        /// ���������һ���ƶ��еĵз�̹��ʱӦ����׼��λ��
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
            float distance = Vector3.Distance(myTank.Position, enemyTank.Position); //����֮�����
            float missileFlyingTime = distance / match.GlobalSetting.MissileSpeed;  //���㵼���ɵ�Ŀ����ʱ��
            //AdvanceWeight������ǰ����̹��λ��
            //��� AdvanceWeight �ӽ�1����ôԤ��λ�ý������������Ŀ��̹�˵��ƶ����ơ�����ζ�����Ŀ��̹����һ���ٶ�ǰ����Ԥ��λ�ý���Զ��Ŀ��̹�˵ĵ�ǰλ�ã���������δ����λ�á�����Ŀ��̹���ƶ��ٶȽϿ���ߵ�������ʱ��ϳ�ʱ�ر����á�
            //��� AdvanceWeight �ӽ�0����ôԤ��λ�ý����ӽ�Ŀ��̹�˵ĵ�ǰλ�á��������Ŀ��̹�˼������ƶ����ߵ�������ʱ��ǳ���ʱ��Ϊ���á�
            //AdvanceWeight = CalculateAdvanceWeight(averageSpeed,maxSpeed);//��̬����Ȩ��
            AimPos = TargetTank.Position + TargetTank.Forward + TargetTank.Velocity * missileFlyingTime * AdvanceWeight;
            return AimPos;
        }


        /// <summary>
        /// ���ڲ��Դ����A�����B����֮���Ƿ����赲��δ�赲ʱ����T
        /// </summary>
        public bool IsBothCanSeeOpp(Vector3 APos, Vector3 BPos)
        {
            return !Physics.Linecast(APos, BPos, PhysicsUtils.LayerMaskScene);
        }
        /// <summary>
        /// ���ڲ����ҷ�̹�˺ʹ��������֮���Ƿ����赲��δ�赲ʱ����T��Ƕ��ʹ��IsBothCanSeeOpp
        /// </summary>
        private bool IsMyTankPosCanSee(Vector3 pos)
        {
            return IsBothCanSeeOpp(myTank.Position, pos);
        }
        /// <summary>
        /// �ҷ�̹���Ƿ������߽Ӵ����з�̹�˵�ǰ�����ĳ���㣬�Ӷ��ж��Ƿ��п��ܽ�����Ч���
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
        /// ˳·��һ������
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
        /// ����з�̹�˵��ٶȣ��Դ����Ż���׼�㷨
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
                    // ���� lastPos �� lastTime Ϊ��ǰ��λ�ú�ʱ��
                    lastPos = enemyTank.transform.position;
                    lastTime = Time.time;
                    return speed / TankSpeeds.Count; 
                }
            }
            else
                return Vector3.zero;

        }

        /// <summary>
        /// ����Ӱ�����ӵ�Ȩ��
        /// </summary>
        /// <param name="targetVelocity"></param>
        /// <returns></returns>
        private  float CalculateAdvanceWeight(Vector3 targetVelocity , float maxSpeed)
        {

            // �����ٶȵĴ�С
            float speedMagnitude = Math.Abs(targetVelocity.magnitude);
            Debug.LogWarning(speedMagnitude);

            if(maxSpeed< speedMagnitude)
            {
                maxSpeed = speedMagnitude;
            }

            // ���ٶȴ�Сӳ�䵽 (0, 1) ������
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




