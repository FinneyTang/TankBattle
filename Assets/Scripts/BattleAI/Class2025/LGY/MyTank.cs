using AI.Base;
using AI.RuleBased;
using Main;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LGY_WALLY
{
    class SuperStarPreparation : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            float time = Match.instance.RemainingTime;
            return time < 110 && time > 100;
        }
    }

    class SuperStarAboutToGenerate : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value.IsSuperStar)
                {
                    return true;
                }
            }
            float time = Match.instance.RemainingTime;
            return time <= 100 && time > 90;
        }
    }

    class superStarGenerateFewSecondsLeft : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            float time = Match.instance.RemainingTime;
            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value.IsSuperStar)
                {
                    return true;
                }
            }
            if (time < 89)
            {
                return false;
            }
            if (time < 95 && myTank.Position.magnitude > 17)
            {
                return true;
            }
            return time < 93.5f;
        }
    }

    class SuperStarOtherCondition : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return new AndCondition(
                new NotCondition(new SuperStarPreparation()),
                new NotCondition(new SuperStarAboutToGenerate())
                ).IsTrue(agent);
        }
    }

    class EnemyAlive : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null)
            {
                return !oppTank.IsDead;
            }
            else
            {
                return false;
            }
        }
    }

    class ResistMoreHit : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if (oppTank != null && !oppTank.IsDead)
            {
                return myTank.HP / 20 > oppTank.HP / 20;
            }
            else
            {
                return true;
            }
        }
    }

    class EnemyFarFromHome : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            Vector3 enemyPosition = oppTank.Position;
            Vector3 enemyHomePosition = Match.instance.GetRebornPos(oppTank.Team);
            return (enemyPosition - enemyHomePosition).magnitude > 20;
        }
    }

    class FullHP : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            return myTank.HP == 100;
        }
    }

    class hasStars : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            int count = Match.instance.GetStars().Count;
            if (count > 0)
            {
                MyTank myTank = (MyTank)agent;
                float distance = 1000;
                int index = -1;
                foreach (var pair in Match.instance.GetStars())
                {
                    if (distance > (myTank.Position - pair.Value.Position).magnitude)
                    {
                        distance = (myTank.Position - pair.Value.Position).magnitude;
                        index = pair.Key;
                    }
                }
                myTank.index = index;
            }
            return count > 0;
        }
    }

    class starNearToMe : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if (oppTank == null || oppTank.IsDead) return true; // �з�������ʱĬ����Ҹ���

            Vector3 myPosition = myTank.Position;
            float minPlayerPathLength = float.MaxValue;
            int closestStarIndex = -1;

            // �����������ǣ�������ҵ����·��
            foreach (var pair in Match.instance.GetStars())
            {
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(myPosition, pair.Value.Position, NavMesh.AllAreas, path))
                {
                    float pathLength = CalculatePathLength(path);
                    if (pathLength < minPlayerPathLength)
                    {
                        minPlayerPathLength = pathLength;
                        closestStarIndex = pair.Key;
                    }
                }
            }

            if (closestStarIndex == -1) return false; // û�пɴ������

            // ������˵������ǵ�·������
            Vector3 enemyPosition = oppTank.Position;
            NavMeshPath enemyPath = new NavMeshPath();
            float enemyPathLength = float.MaxValue;
            if (NavMesh.CalculatePath(enemyPosition,
                Match.instance.GetStars()[closestStarIndex].Position,
                NavMesh.AllAreas, enemyPath))
            {
                enemyPathLength = CalculatePathLength(enemyPath);
            }

            // �Ƚ�·������
            if (minPlayerPathLength < enemyPathLength)
            {
                myTank.index = closestStarIndex;
                return true;
            }
            return false;
        }

        // ����·���ܳ���
        private float CalculatePathLength(NavMeshPath path)
        {
            if (path.corners.Length < 2) return 0;
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            return length;
        }
    }

    class AnotherStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.GetStars().Count > 1;
        }
    }

    class canResistThreeAttack : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            return myTank.HP / 20 > 3;
        }
    }

    class OnMissileTrajectory : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            myTank.directMissiles.Clear();
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(myTank.Team);
            foreach (var pair in missiles)
            {
                Missile missile = pair.Value;
                if (Physics.SphereCast(missile.Position, 0.1f, missile.Velocity, out RaycastHit hit, 40))
                {
                    FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                    if (fireCollider != null)
                    {
                        if (fireCollider.Owner == myTank)
                        {
                            myTank.directMissiles.Add(pair.Key, missile);
                        }
                    }
                }
            }
            if (myTank.directMissiles.Count > 0)
            {
                return true;
            }
            return false;
        }
    }

    class willHitPredictionMissile : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(myTank.Team);
            foreach (var pair in missiles)
            {
                Missile missile = pair.Value;
                Collider[] colliders = Physics.OverlapSphere(missile.Position, 5f);
                foreach (var collider in colliders)
                {
                    if (collider != null)
                    {
                        if (collider.gameObject == myTank.gameObject)
                        {
                            myTank.Move(myTank.Position);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    class enemyNearToMe : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if (oppTank != null)
            {
                if ((myTank.Position - oppTank.Position).magnitude < 15)
                {
                    return true;
                }
            }
            return false;
        }
    }

    class onlyResistOneHit : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            return (myTank.HP / 20) <= 1;
        }
    }

    class NearToGoodPosition : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            return (myTank.Position - myTank.goodPosition).magnitude < 2;
        }
    }

    class ConditionOne : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank enemyTank = Match.instance.GetOppositeTank(myTank.Team);
            if (enemyTank == null || enemyTank.IsDead) return false;

            // ʣ��ʱ�� <70��
            bool timeCondition = Match.instance.RemainingTime < 50;
            // ����������ȵз�15��
            bool scoreCondition = (myTank.Score - enemyTank.Score) >= 15;
            // ·������з�λ�� <60�ף�ʹ��ֱ�߾���������·�����룩
            bool distanceCondition = (myTank.Position - enemyTank.Position).magnitude < 60;

            return timeCondition && scoreCondition && distanceCondition;
        }
    }

    class ConditionTwo : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Vector3 rebornPos = Match.instance.GetRebornPos(myTank.Team);
            // Ѫ�� ����20 ��ֱ�߾��������� <2��
            return myTank.HP < 40 && (myTank.Position - rebornPos).magnitude < 1;
        }
    }

    class ThereIsASnake : Condition
    {
        private const float DIR_CHANGE_THRESHOLD = 45f; // ����仯��ֵ���ȣ�
        private const float CHECK_INTERVAL = 0.5f;      // ��������룩
        private static Dictionary<Tank, SnakeState> enemyStates = new Dictionary<Tank, SnakeState>();

        class SnakeState
        {
            public Vector3 lastPosition;
            public Vector3 lastDirection;
            public float lastCheckTime;
            public int directionChangeCount;
        }

        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            Tank enemyTank = Match.instance.GetOppositeTank(myTank.Team);
            if (enemyTank == null || enemyTank.IsDead) return false;

            // ��ʼ��״̬��¼
            if (!enemyStates.ContainsKey(enemyTank))
            {
                enemyStates[enemyTank] = new SnakeState()
                {
                    lastPosition = enemyTank.Position,
                    lastDirection = enemyTank.Forward,
                    directionChangeCount = 0
                };
                return false;
            }

            SnakeState state = enemyStates[enemyTank];

            // ʱ�������
            if (Time.time - state.lastCheckTime < CHECK_INTERVAL) return false;
            state.lastCheckTime = Time.time;

            // ���㷽��仯
            Vector3 currentDirection = enemyTank.Forward;
            float angleChange = Vector3.Angle(state.lastDirection, currentDirection);

            // �����Ч����仯��HYQ������������Ƶ��Ƕ�ת��
            if (angleChange > DIR_CHANGE_THRESHOLD)
            {
                state.directionChangeCount++;
                // 2���ڼ�⵽3�����ϴ�Ƕ�ת�����ж�Ϊ����
                if (state.directionChangeCount >= 3)
                {
                    state.directionChangeCount = 0; // ���ü�����
                    return true;
                }
            }
            else
            {
                state.directionChangeCount = Mathf.Max(0, state.directionChangeCount - 1);
            }

            // ����״̬
            state.lastDirection = currentDirection;
            state.lastPosition = enemyTank.Position;
            return false;
        }

    }
    class EnemyRespawnTimeGreaterThanFive : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            if (myTank.enemyDeathTime < 0) return false; // �з�δ����

            float elapsed = Time.time - myTank.enemyDeathTime;
            float remaining = MyTank.ENEMY_RESPAWN_DURATION - elapsed;
            return remaining > 5f; // ʣ�ิ��ʱ��>5��
        }
    }

    class EnemyRespawnTimeLessOrEqualFive : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            if (myTank.enemyDeathTime < 0) return false;

            float elapsed = Time.time - myTank.enemyDeathTime;
            float remaining = MyTank.ENEMY_RESPAWN_DURATION - elapsed;
            return remaining <= 5f && remaining > 0; // ʣ��ʱ��<=5����δ����
        }
    }
    
    
    class MyTank : Tank
    {
        private Condition goToSuperStar;
        private Condition goToEnemy;
        private Condition goBackHome;
        private Condition goToStar;
        private Condition goToGoodPosition;
        private Condition avoidMissile;

        private Condition goToEnemy1;
        private Condition goToEnemy2;
        private Condition goBackHome1;
        private Condition goBackHome2;
        private Condition goBackHome3;
        private Condition goToStar1;
        private Condition goToStar2;
        private Condition goToStar3;
        Condition goBackHome4;
        Condition goBackHome5;
        private Condition goToGoodPosition1;
        private Condition goToGoodPosition2;
        private Condition goToGoodPosition3;

        private Condition enemyLessHitAndFarFromHome;
        private Condition goToStarNodeContion;
        private Condition thereIsASnake;

        // ���õз�����ʱ���������
        private Condition enemyRespawnTimeGreaterThanFive;
        private Condition enemyRespawnTimeLessOrEqualFive;

        public int index;    //Ҫȥ�����ǵ�����
        public Vector3 goodPosition;    //��λ��
        public Dictionary<int, Missile> directMissiles;    //ֱ�򵼵�

        // ��Ӽ���������
        public int NormalStarCount { get; private set; }
        public int SuperStarCount { get; private set; }
        public int KillCount { get; private set; }

        private int lastScore; // ���ڸ�����һ�εķ���
                               // ���������Ʊ�־λ
        private bool hasOutput = false;
        // ��ӻ��м�������
        private int enemyMissileHits;  // �з����������ҷ�����
        private int myMissileHits;     // �ҷ��������ез�����
        private int lastHP;            // ��һ֡��HPֵ
        private int lastEnemyHP;       // �з���һ֡��HPֵ
        private Vector3 myRebornPos;
        public const float ENEMY_RESPAWN_DURATION = 10f; // �з�����ʱ��
        public float enemyDeathTime = -1f; // �з�����ʱ���¼

        protected override void OnStart()
        {
            base.OnStart();
            lastScore = Score; // ��ʼ����һ�η���

            // ��ʼ������
            enemyMissileHits = 0;
            myMissileHits = 0;
            lastHP = Match.instance.GlobalSetting.MaxHP;
            lastEnemyHP = Match.instance.GlobalSetting.MaxHP;

            index = -1;
            enemyDeathTime = -1f; // ��ʼ���з�����ʱ���¼

            enemyLessHitAndFarFromHome = new AndCondition(new ResistMoreHit(),
                new AndCondition(new EnemyFarFromHome(), new enemyNearToMe()));
            goToStarNodeContion = new AndCondition(
                new SuperStarOtherCondition(),
                new OrCondition(
                    new AndCondition(
                        new EnemyAlive(),
                        new NotCondition(enemyLessHitAndFarFromHome)),
                    new NotCondition(new EnemyAlive())));

            goToEnemy1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new EnemyAlive(),
                    enemyLessHitAndFarFromHome));
            goToEnemy2 = new AndCondition(
                new SuperStarOtherCondition(),
                new AndCondition(
                    new EnemyAlive(),
                    enemyLessHitAndFarFromHome));
            goBackHome1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new NotCondition(new FullHP())));
            goBackHome2 = new AndCondition(
                new SuperStarOtherCondition(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new AndCondition(
                        new OrCondition(
                            new AndCondition(
                                new hasStars(),
                                new NotCondition(new starNearToMe())),
                            new NotCondition(new hasStars())),
                        new NotCondition(new canResistThreeAttack()))));
            goBackHome3 = new OrCondition(new onlyResistOneHit(),
                new AndCondition(new enemyNearToMe(),
                new NotCondition(new ResistMoreHit())));
            goBackHome4 = new ConditionOne();
            goBackHome5 = new ConditionTwo();
            goToStar1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new AndCondition(
                        new FullHP(),
                        new hasStars())));
            goToStar2 = new AndCondition(
                goToStarNodeContion,
                new AndCondition(
                    new hasStars(),
                    new starNearToMe()));
            goToStar3 = new AndCondition(
                goToStarNodeContion,
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new hasStars(),
                            new NotCondition(new starNearToMe())),
                        new NotCondition(new hasStars())),
                    new AndCondition(
                        new canResistThreeAttack(),
                        new AnotherStar())));
            goToGoodPosition1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new AndCondition(
                        new FullHP(),
                        new NotCondition(new hasStars()))));
            goToGoodPosition2 = new AndCondition(
                goToStarNodeContion,
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new hasStars(),
                            new NotCondition(new starNearToMe())),
                        new NotCondition(new hasStars())),
                    new AndCondition(
                        new canResistThreeAttack(),
                        new NotCondition(new AnotherStar()))));
            goToGoodPosition3 = new AndCondition(new SuperStarAboutToGenerate(),
                new NotCondition(new NearToGoodPosition()));
            // ���õз�����ʱ���������
            enemyRespawnTimeGreaterThanFive = new EnemyRespawnTimeGreaterThanFive();
            enemyRespawnTimeLessOrEqualFive = new EnemyRespawnTimeLessOrEqualFive();

            goToSuperStar = new superStarGenerateFewSecondsLeft();
            goToEnemy = new OrCondition(goToEnemy1, goToEnemy2);
            // �޸�goBackHome����������з�����ʱ���ж�
            goBackHome = new OrCondition(
                goBackHome1,
                new OrCondition(
                    goBackHome2,
                    new OrCondition(
                        goBackHome3,
                        new OrCondition(
                            goBackHome4,
                            new OrCondition(
                                goBackHome5,
                                enemyRespawnTimeLessOrEqualFive // �з�����ʱ��<=5��ʱ�ؼ�
                            )
                        )
                    )
                )
            );
            // �޸�goToStar����������з�����ʱ��>5��ʱ���ȼ���
            goToStar = new OrCondition(
                goToStar1,
                new OrCondition(
                    goToStar2,
                    new OrCondition(
                        goToStar3,
                        enemyRespawnTimeGreaterThanFive // �з�����ʱ��>5��ʱ���ȼ���
                    )
                )
            );
            goToGoodPosition = new OrCondition(goToGoodPosition1, new OrCondition(
                goToGoodPosition2, goToGoodPosition3));
            avoidMissile = new AndCondition(
                new NotCondition(
                    new AndCondition(
                        new SuperStarPreparation(),
                        new NotCondition(new FullHP()))),
                new AndCondition(
                    new NotCondition(
                        new AndCondition(
                            new SuperStarAboutToGenerate(),
                            new NotCondition(new NearToGoodPosition())))
                    , new OrCondition(
                        new OnMissileTrajectory(),
                        new willHitPredictionMissile())));
            thereIsASnake = new ThereIsASnake();

            directMissiles = new Dictionary<int, Missile>();
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            UpdateMyRebornPos();

            Tank oppTank = Match.instance.GetOppositeTank(Team);

            // ���з������¼�
            if (oppTank != null)
            {
                // ���1���з��ո�������HP��>0��Ϊ0��
                if (oppTank.HP <= 0 && enemyDeathTime < 0)
                {
                    enemyDeathTime = Time.time; // ��¼����ʱ��
                }
                // ���2���з��Ѹ��HP�ָ���
                else if (oppTank.HP > 0)
                {
                    enemyDeathTime = -1; // ���ü�ʱ��
                }
            }

            //�ڹ���׼����
            //�߱����������򿪻�
            if (thereIsASnake.IsTrue(this))
            {
                Debug.Log("�����ζ��");
                TurnTurretSnake();
            }
            else
            {
                Debug.Log("��ͨ��׼");
                TurnTurret();
            }

            chooseGoodPosition();
            if (goToSuperStar.IsTrue(this))
            {
                Debug.Log("ȥ����������");
                Move(Vector3.zero);
            }
            else if (avoidMissile.IsTrue(this))
            {
                if (new OnMissileTrajectory().IsTrue(this))
                {
                    avoidDirectMissile();
                }
                else if (new willHitPredictionMissile().IsTrue(this))
                {
                    Move(this.Position);
                }
            }
            else
            {
                // �з�������ʣ�ิ��ʱ��>5��ʱ���ȼ���
                if (new EnemyRespawnTimeGreaterThanFive().IsTrue(this))
                {
                    Debug.Log("�з�����ʱ��>5�룬���ȼ���");
                    if (goToStar.IsTrue(this))
                    {
                        Move(Match.instance.GetStarByID(index).Position);
                    }
                    else
                    {
                        Move(Vector3.zero); // ���û�����ǿɼ�ȥ��������
                    }
                }
                // �з�������ʣ�ิ��ʱ��<=5��ʱ���Ȼؼ�
                else if (new EnemyRespawnTimeLessOrEqualFive().IsTrue(this))
                {
                    Debug.Log("�з�����������Ȼؼ�");
                    Move(myRebornPos);
                }
                // ��������µ���Ϊ�߼�
                else if (goToEnemy.IsTrue(this))
                {
                    Debug.Log("ȥ������");
                    Move(oppTank.Position);
                }
                else if (goBackHome.IsTrue(this))
                {
                    Debug.Log("�ؼ�");
                    Move(myRebornPos);
                }
                else if (goToStar.IsTrue(this))
                {
                    Debug.Log("ȥ������");
                    Move(Match.instance.GetStarByID(index).Position);
                }
                else if (goToGoodPosition.IsTrue(this))
                {
                    Debug.Log("ȥ��λ��");
                    Move(goodPosition);
                }
                else
                {
                    Debug.Log("������");
                }
            }

            // ���㵱ǰ��������һ�εĲ�ֵ
            int currentScore = Score;
            int delta = currentScore - lastScore;

            if (delta > 0)
            {
                var settings = Match.instance.GlobalSetting;
                // ���ݵ÷�ֵ�ж���Դ
                if (delta == settings.ScoreForStar)
                {
                    NormalStarCount++;
                }
                else if (delta == settings.ScoreForSuperStar)
                {
                    SuperStarCount++;
                }
                else if (delta == settings.ScoreForKill)
                {
                    KillCount++;
                }
            }

            lastScore = currentScore; // ������һ�η���

            // ���ʣ��ʱ�䣨����ʱ��180��ʱ��179���Ӧʣ��1�룩
            if (Match.instance.RemainingTime <= 1f && !hasOutput)
            {
                var settings = Match.instance.GlobalSetting;

                // ������÷����͵��ܷ�
                int normalScore = NormalStarCount * settings.ScoreForStar;
                int superScore = SuperStarCount * settings.ScoreForSuperStar;
                int killScore = KillCount * settings.ScoreForKill;

                // ���ͳ�ƽ��
                Debug.Log($"�÷�ͳ�ƣ�" +
                          $"��ͨ���� {normalScore} �֣� " +
                          $"�������� {superScore} �֣�" +
                          $"��ɱ {killScore} ��");

                hasOutput = true; // ȷ��ֻ���һ��
            }
            UpdateHitCounters();
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            index = -1;
            enemyDeathTime = -1f;
        }

        public override string GetName()
        {
            return "LGY_WALLY_Tank";
        }

        // ���»��м�����
        private void UpdateHitCounters()
        {
            // �����ҷ������д���
            if (HP < lastHP)
            {
                int damage = lastHP - HP;
                enemyMissileHits += damage / Match.instance.GlobalSetting.DamagePerHit;
            }
            lastHP = HP;

            // ����з������д���
            Tank enemyTank = Match.instance.GetOppositeTank(Team);
            if (enemyTank != null && !enemyTank.IsDead)
            {
                if (enemyTank.HP < lastEnemyHP)
                {
                    int damage = lastEnemyHP - enemyTank.HP;
                    myMissileHits += damage / Match.instance.GlobalSetting.DamagePerHit;
                }
                lastEnemyHP = enemyTank.HP;
            }
            else
            {
                lastEnemyHP = Match.instance.GlobalSetting.MaxHP;
            }
            // �ڱ������1����ʾͳ������
            if (Match.instance.RemainingTime <= 1f && Match.instance.RemainingTime > 0f)
            {
                // ��ʾ����Ļ���·�
                Debug.Log(
                    $"��ͣ�㷨������: {enemyMissileHits}");
                Debug.Log(
                     $"���ζ�ܱ�����: {myMissileHits}");
            }
        }


        private void TurnTurret()
        {
            Tank oppTank = Match.instance.GetOppositeTank(this.Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                Transform turret = this.transform.GetChild(1);
                Vector2 oppPosition = new Vector2(oppTank.Position.x, oppTank.Position.z);
                Vector2 oppVelocity = new Vector2(oppTank.Velocity.x, oppTank.Velocity.z);
                Vector2 myFirePosition = new Vector2(this.FirePos.x, this.FirePos.z);
                Vector2 deltaPosition = oppPosition - myFirePosition;
                float a = Mathf.Pow(oppVelocity.x, 2) + Mathf.Pow(oppVelocity.y, 2) - 1600;
                float b = 2 * (deltaPosition.x * oppVelocity.x + deltaPosition.y * oppVelocity.y);
                float c = Mathf.Pow(deltaPosition.x, 2) + Mathf.Pow(deltaPosition.y, 2);
                float delta = b * b - 4 * a * c;
                float predictedTime = (-b - Mathf.Sqrt(delta)) / (2 * a);
                Vector2 predictedPosition = deltaPosition + oppVelocity * predictedTime;
                Vector3 targetDirection = new Vector3(predictedPosition.x, 0, predictedPosition.y);
                turret.forward = Vector3.Lerp(turret.forward, targetDirection, Time.deltaTime * 180);
                if ((this.Position - oppTank.Position).magnitude < 15)
                {
                    this.Fire();
                }
                else if (Physics.SphereCast(this.FirePos, 0.24f, targetDirection, out RaycastHit hit,
                              (targetDirection - this.FirePos).magnitude - 2))
                {
                    FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                    if (fireCollider != null)
                    {
                        if (Vector3.Angle(this.TurretAiming, targetDirection) < 2)
                            this.Fire();
                    }
                }
                else
                {
                    if (Vector3.Angle(this.TurretAiming, targetDirection) < 2)
                        this.Fire();
                }
            }
            else
            {
                this.TurretTurnTo(this.Position + this.Forward);
            }
        }

        private void TurnTurretSnake()
        {
            Tank enemyTank = Match.instance.GetOppositeTank(Team);
            if (enemyTank == null || enemyTank.IsDead)
            {
                TurretTurnTo(Position + Forward);
                return;
            }

            // ʹ������Ԥ�⸲�Ƕ������λ��
            Vector3 basePrediction = PredictPosition(enemyTank, 1.0f); // ����Ԥ��
            Vector3 leftPrediction = PredictPosition(enemyTank, 1.2f) + enemyTank.transform.right * 3f;  // �����չ
            Vector3 rightPrediction = PredictPosition(enemyTank, 1.2f) - enemyTank.transform.right * 3f; // �Ҳ���չ

            // ��̬ѡ������Ŀ��
            Vector3 targetPos = ChooseBestTarget(basePrediction, leftPrediction, rightPrediction);
            TurretTurnTo(targetPos);

            // ������׼�Ƕ�ʱ����
            if (Vector3.Angle(TurretAiming, targetPos - FirePos) < 5f)
            {
                Fire();
            }
        }

        // Ŀ��Ԥ�⣨����ʱ��ϵ����Ӧ���٣�
        private Vector3 PredictPosition(Tank target, float timeScale)
        {
            float distance = Vector3.Distance(FirePos, target.Position);
            float missileTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            return target.Position + target.Velocity * (missileTime * timeScale);
        }

        // ѡ����������е�Ŀ��
        private Vector3 ChooseBestTarget(params Vector3[] positions)
        {
            Tank enemyTank = Match.instance.GetOppositeTank(Team);
            Vector3 enemyVelocity = enemyTank.Velocity;

            // ����ѡ�����ٶȷ���һ�µ�Ԥ���
            foreach (Vector3 pos in positions)
            {
                Vector3 predictedDir = (pos - enemyTank.Position).normalized;
                if (Vector3.Dot(predictedDir, enemyVelocity.normalized) > 0.7f)
                {
                    return pos;
                }
            }
            return positions[0]; // Ĭ�Ϸ��ػ���Ԥ��
        }



        private void chooseGoodPosition()
        {
            if (new SuperStarAboutToGenerate().IsTrue(this) &&
                        new NotCondition(new superStarGenerateFewSecondsLeft()).IsTrue(this))
            {
                Tank oppTank = Match.instance.GetOppositeTank(this.Team);
                if (Match.instance.GetRebornPos(this.Team).x > 0)
                {
                    if (oppTank != null && !oppTank.IsDead)
                    {
                        if (oppTank.Position.x > 10 || (oppTank.Position.z >= 30 && oppTank.Position.z <= 50 &&
                            oppTank.Position.x >= -5 && oppTank.Position.x <= 10))
                        {
                            goodPosition = new Vector3(-7, 0, -33);
                        }
                        else
                        {
                            goodPosition = new Vector3(13, 0, -5);
                        }
                    }
                }
                else
                {
                    if (oppTank != null && !oppTank.IsDead)
                    {
                        if (oppTank.Position.x < -10 || (oppTank.Position.z <= 130 && oppTank.Position.z >= -50 &&
                            oppTank.Position.x <= 5 && oppTank.Position.x >= -10))
                        {
                            goodPosition = new Vector3(7, 0, 33);
                        }
                        else
                        {
                            goodPosition = new Vector3(-13, 0, 5);
                        }
                    }
                }
            }
            else
            {
                goodPosition = new Vector3(0, 0, 0);
            }
        }

        private void avoidDirectMissile()
        {
            foreach (var pair in directMissiles)
            {
                Missile missile = pair.Value;
                Vector3 onWhichSideInfo = Vector3.Cross(missile.Velocity, this.Position - missile.Position);
                //��ֱ�ڵ����ٶȶ��
                Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                if (onWhichSideInfo.y > 0)
                    cross *= -1;
                this.Move(this.Position + cross * 4.5f);
            }
        }

        private void UpdateMyRebornPos()
        {
            Vector3 basePos = Match.instance.GetRebornPos(Team);
            myRebornPos = basePos + (Vector3.zero - basePos).normalized * 6 + Vector3.up * 0.5f;
        }
    }
}