using Main;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;


namespace KD
{
    abstract class Condition
    {
        public abstract bool IsTrue(Tank tank);
    }

    class WinCondition : Condition
    {

        public override bool IsTrue(Tank tank)
        {
            int EnemyScore = (int)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.EnemyScore];
            if (tank.Score - EnemyScore > Match.instance.RemainingTime)
                return true;
            else
                return false;
        }
    }
    class StarExist : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            Dictionary<int, Star> stars = (Dictionary<int, Star>)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.Stars];
            if (stars.Count > 0)
                return true;
            else
                return false;
        }
    }
    class CanSeePos : Condition
    {

        public override bool IsTrue(Tank tank)
        {
            Vector3 Target = (Vector3)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.TurrentAimedPos];

            return tank.GetComponent<MyTank>().CanSeeOthers(Target);

        }
    }
    class CanSeeEnemy : Condition
    {

        public override bool IsTrue(Tank tank)
        {
            Vector3 Target = (Vector3)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.EnemyPos];

            return tank.GetComponent<MyTank>().CanSeeOthers(Target);

        }
    }
    class CanFire : Condition
    {

        public override bool IsTrue(Tank tank)
        {

            return tank.CanFire();

        }
    }

    class SetHpCompare : Condition
    {
        public delegate bool Cpr(int Hp, int EnemyHp);
        Cpr myCpr;
        int SetHp;
        public SetHpCompare(Cpr myCpr, int SetHp)
        {
            this.SetHp = SetHp;
            this.myCpr = myCpr;

        }
        public override bool IsTrue(Tank tank)
        {

            return myCpr(tank.HP, SetHp);
        }


    }
    class HpCompare : Condition
    {
        public delegate bool Cpr(int Hp, int EnemyHp);
        Cpr myCpr;

        public HpCompare(Cpr myCpr)
        {
            this.myCpr = myCpr;

        }
        public override bool IsTrue(Tank tank)
        {

            return myCpr(tank.HP, (int)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.EnemyHp]);
        }


    }
    class SuperStarExist : Condition
    {


        public override bool IsTrue(Tank tank)
        {
            Star SuperStar = (Star)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.SuperStar];
            if (SuperStar != null)
                return true;
            else
                return false;
        }
    }



    class DistanceToStar : Condition
    {
        float distance;
        public DistanceToStar(float distance)
        {

            this.distance = distance;


        }
        public override bool IsTrue(Tank tank)
        {
            float distanceToStar = (float)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.DistanceToNearestStar];

            if (distanceToStar < distance)
                return true;
            else
                return false;
        }
    }
    class TimeBetween : Condition
    {
        float timeBegin = 0;
        float timeOver = 0;

        public TimeBetween(float timeBegin, float timeOver = 0)
        {
            this.timeBegin = timeBegin;
            this.timeOver = timeOver;

        }
        public override bool IsTrue(Tank tank)
        {
            float remainingTime = Match.instance.RemainingTime;
            if (timeBegin > remainingTime && timeOver < remainingTime)
                return true;
            else
                return false;

        }
    }

    class HasMissle : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            Missile missile = (Missile)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.NearestMissile];
            if (missile != null)
                return true;
            else
                return false;

        }
    }

    class EnemyTankDead : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            int EnemyHp = (int)tank.GetComponent<MyTank>().c_KnowledgePool[MyTank.Cooked_KnowledgePool.EnemyHp];
            if (EnemyHp == 0)
                return true;
            else
                return false;

        }
    }

    class MyTank : Tank
    {
        Tank selfTank;
        Tank enemyTank;
        private Vector3 SetPosition;
        public enum Row_KnowledgePool
        {
            EnemyMessage,
            SelfMessage
        };

        public enum Cooked_KnowledgePool
        {
            Time,

            Stars,
            SuperStar,

            EnemyHp,
            EnemyPos,
            EnemyScore,
            EnemyRebornPos,

            NearestMissile,
            TurrentAimedPos,
            
            DistanceToNearestStar,

            WigglePos,
            SuperStarPos,

            ToNearestStarPath,
            ToHomePath
        };
        Condition canSeePos;
        Condition timeBetween;
        Condition hpCompare;
        Condition canFire;
        Condition superStarExist;
        Condition starExist;
        Condition hasMissile;
        Condition backToHomeHp;
        Condition LimitedHp;
        Condition canSeeEnemy;
        Condition enemyTankDead;
        Condition distanceToStar;
        Condition winCondition;

        public Dictionary<Row_KnowledgePool, object> r_KnowledgePool;
        public Dictionary<Cooked_KnowledgePool, object> c_KnowledgePool;


        bool InitRow_KnowledgePool()
        {

            r_KnowledgePool = new Dictionary<Row_KnowledgePool, object>();

            r_KnowledgePool.Add(Row_KnowledgePool.EnemyMessage, Match.instance.GetOppositeTank(Team));
            r_KnowledgePool.Add(Row_KnowledgePool.SelfMessage, Match.instance.GetTank(Team));

            return true;

        }

        bool InitCooked_KnowledgePool()
        {
            c_KnowledgePool  = new Dictionary<Cooked_KnowledgePool, object>();

            c_KnowledgePool.Add(Cooked_KnowledgePool.Time, Match.instance.RemainingTime);
            c_KnowledgePool.Add(Cooked_KnowledgePool.Stars, Match.instance.GetStars());
            c_KnowledgePool.Add(Cooked_KnowledgePool.SuperStar, null);
            c_KnowledgePool.Add(Cooked_KnowledgePool.EnemyHp, 0);
            c_KnowledgePool.Add(Cooked_KnowledgePool.TurrentAimedPos, new Vector3(0, 0, 0));
            c_KnowledgePool.Add(Cooked_KnowledgePool.EnemyRebornPos, Match.instance.GetRebornPos(Match.instance.GetOppositeTank(Team).Team));
            c_KnowledgePool.Add(Cooked_KnowledgePool.WigglePos, new Vector3(0, 0, 0));
            c_KnowledgePool.Add(Cooked_KnowledgePool.ToHomePath, null);
            c_KnowledgePool.Add(Cooked_KnowledgePool.ToNearestStarPath, null);
            c_KnowledgePool.Add(Cooked_KnowledgePool.SuperStarPos, new Vector3(0, 0, 0.01f));
            c_KnowledgePool.Add(Cooked_KnowledgePool.NearestMissile, null);
            c_KnowledgePool.Add(Cooked_KnowledgePool.EnemyPos, new Vector3(0, 0, 0));
            c_KnowledgePool.Add(Cooked_KnowledgePool.DistanceToNearestStar, 0);
            c_KnowledgePool.Add(Cooked_KnowledgePool.EnemyScore, 0);

            return true;
        }
        bool InitCondition()
        {
            canSeePos = new CanSeePos();
            timeBetween = new TimeBetween(100, 90);
            hpCompare = new HpCompare(Larger);
            canFire = new CanFire();
            superStarExist = new SuperStarExist();
            canSeeEnemy = new CanSeeEnemy();
            starExist = new StarExist();
            hasMissile = new HasMissle();
            backToHomeHp = new SetHpCompare(Smaller, 26);
            enemyTankDead = new EnemyTankDead();
            LimitedHp = new SetHpCompare(Smaller, 99);
            distanceToStar = new DistanceToStar(25f);
            winCondition = new WinCondition();
            return true;
        }

        bool Update_and_ProcessKnowledgePool()
        {

            r_KnowledgePool[Row_KnowledgePool.SelfMessage] = Match.instance.GetTank(Team);
            r_KnowledgePool[Row_KnowledgePool.EnemyMessage] = Match.instance.GetOppositeTank(Team);
            c_KnowledgePool[Cooked_KnowledgePool.Stars] = Match.instance.GetStars();
            c_KnowledgePool[Cooked_KnowledgePool.Time] = Match.instance.RemainingTime;

            enemyTank = (Tank)r_KnowledgePool[Row_KnowledgePool.EnemyMessage];
            selfTank = (Tank)r_KnowledgePool[Row_KnowledgePool.SelfMessage];
           
            Vector3 escapePlace = Vector3.zero;


            Dictionary<int, Star> stars = (Dictionary<int, Star>)c_KnowledgePool[Cooked_KnowledgePool.Stars];

            NavMeshPath pathHome = CaculatePath(Match.instance.GetRebornPos(selfTank.Team));
            NavMeshPath pathStar = null;
            Star superStar = null;
            float distanceToStar = 1e6f;

            GetNearestStar(ref superStar, ref distanceToStar, ref pathStar);




            float time_trace = ((enemyTank.Position - selfTank.FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed);

            SetPosition = enemyTank.Position + enemyTank.Velocity * (((enemyTank.Position + enemyTank.Velocity * Time.deltaTime * time_trace - selfTank.FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed));
            TurretTurnTo(SetPosition);

            c_KnowledgePool[Cooked_KnowledgePool.WigglePos] = SetPosition;
            



            Missile nearest_Missile = GetNearestMissile();
            if (nearest_Missile != null)
                Debug.Log(nearest_Missile.Team);

            escapePlace = CalcEscape(nearest_Missile, selfTank);
            c_KnowledgePool[Cooked_KnowledgePool.Stars] = stars;
            c_KnowledgePool[Cooked_KnowledgePool.SuperStar] = superStar;
            c_KnowledgePool[Cooked_KnowledgePool.EnemyHp] = enemyTank.HP;
            c_KnowledgePool[Cooked_KnowledgePool.NearestMissile] = nearest_Missile;
            c_KnowledgePool[Cooked_KnowledgePool.EnemyPos] = enemyTank.Position;
            c_KnowledgePool[Cooked_KnowledgePool.DistanceToNearestStar] = distanceToStar;
            c_KnowledgePool[Cooked_KnowledgePool.EnemyScore] = enemyTank.Score;
            c_KnowledgePool[Cooked_KnowledgePool.WigglePos] = escapePlace;
            c_KnowledgePool[Cooked_KnowledgePool.ToHomePath] = pathHome;
            c_KnowledgePool[Cooked_KnowledgePool.ToNearestStarPath] = pathStar;
            c_KnowledgePool[Cooked_KnowledgePool.SuperStarPos] = new Vector3(0, 0, 0.01f);

            return true;
        }

        bool Do_sth()
        {
            Vector3 escapePlace = (Vector3)c_KnowledgePool[Cooked_KnowledgePool.WigglePos];
            NavMeshPath pathStar = (NavMeshPath)c_KnowledgePool[Cooked_KnowledgePool.ToNearestStarPath];
            NavMeshPath pathHome = (NavMeshPath)c_KnowledgePool[Cooked_KnowledgePool.ToHomePath];
            Vector3 superStarPlace = (Vector3)c_KnowledgePool[Cooked_KnowledgePool.SuperStarPos];
     
            if (canSeePos.IsTrue(selfTank) && canFire.IsTrue(selfTank) && !enemyTankDead.IsTrue(selfTank))
            {
                Fire();
                Debug.Log("fire");

            }
            if (winCondition.IsTrue(selfTank))
            {
                Move(pathHome);
            }
            if (timeBetween.IsTrue(selfTank))
            {
                Move(superStarPlace);
            }
            if (superStarExist.IsTrue(selfTank))
            {
                Move(superStarPlace);
            }
            if (!enemyTankDead.IsTrue(selfTank))
                if (canSeeEnemy.IsTrue(selfTank) && hasMissile.IsTrue(selfTank))
                {
                    Move(escapePlace);
                }

            if (hpCompare.IsTrue(selfTank) && starExist.IsTrue(selfTank))
            {
                Move(pathStar);
            }
            
            if (enemyTankDead.IsTrue(selfTank) && (LimitedHp.IsTrue(selfTank)))
            {
                if (distanceToStar.IsTrue(selfTank))
                    Move(pathStar);
                else
                    Move(pathHome);
            }
            else if (starExist.IsTrue(selfTank))
                Move(pathStar);

            if (backToHomeHp.IsTrue(selfTank))
                Move(pathHome);
            
            return true;
        }

        bool Larger(int a, int b)
        {
            return a > b;
        }

        bool Smaller(int a, int b)
        {
            return a <= b;
        }

        public new bool Move(Vector3 targetPos)
        {
            if (targetPos == Vector3.zero)
            {
                Debug.Log("1");
                return false;
            }
            NavMeshPath path = CaculatePath(targetPos);
            if (path != null)
            {
                Move(path);
                return true;
            }
            return false;
        }

        public new bool CanSeeOthers(Vector3 pos)
        {
            return !Physics.Linecast(Position, pos, PhysicsUtils.LayerMaskScene);
        }

        public bool CanSeeOthers(Vector3 pos, Vector3 target)
        {
            return !Physics.Linecast(pos, target, PhysicsUtils.LayerMaskScene);
        }

        float CalcDistance(NavMeshPath path)
        {
            float temp_Distance = 0;
            for (int i = 0; i < path.corners.Length - 1; i++)
                temp_Distance += (path.corners[i + 1] - path.corners[i]).magnitude;
            return temp_Distance;
        }

        Vector3 CalcEscape(Missile nearest_Missile, Tank enemyTank)
        {
            if (nearest_Missile == null)
                return Vector3.zero;
            Vector3 moveDir = new Vector3(-nearest_Missile.Velocity.z, 0.0f, nearest_Missile.Velocity.x);
            //if (Vector3.Dot(moveDir, selfTank.Velocity) < 0 )
            //    moveDir = -moveDir;

            for (int len = 5; len < 10; len++)
            {
                NavMeshPath path = null;
                path = CaculatePath(transform.position + len * moveDir.normalized);
                if (path != null && !CanSeeOthers(transform.position + len * moveDir.normalized, nearest_Missile.Position) && CanSeeOthers(transform.position + len * moveDir.normalized, transform.position))
                    return transform.position + len * moveDir.normalized;
                path = CaculatePath(transform.position - len * moveDir.normalized);
                if (path != null && !CanSeeOthers(transform.position - len * moveDir.normalized, nearest_Missile.Position) && CanSeeOthers(transform.position - len * moveDir.normalized, transform.position))
                    return transform.position + len * moveDir.normalized;
            }
            for (int len = 5; len < 10; len++)
            {
                NavMeshPath path = null;
                path = CaculatePath(transform.position + len * moveDir.normalized);
                if (path != null && !CanSeeOthers(transform.position + len * moveDir.normalized, enemyTank.Position) && CanSeeOthers(transform.position - len * moveDir.normalized, transform.position))
                    return transform.position + len * moveDir.normalized;
                path = CaculatePath(transform.position - len * moveDir.normalized);
                if (path != null && !CanSeeOthers(transform.position - len * moveDir.normalized, enemyTank.Position) && CanSeeOthers(transform.position - len * moveDir.normalized, transform.position))
                    return transform.position + len * moveDir.normalized;
            }
            for (int len = 5; len < 8; len++)
            {
                NavMeshPath path = null;
                path = CaculatePath(transform.position + len * moveDir.normalized);
                if (path != null && Vector3.Dot(moveDir, selfTank.Velocity) > 0)
                    return transform.position + len * moveDir.normalized;
                path = CaculatePath(transform.position - len * moveDir.normalized);
                if (path != null && Vector3.Dot(moveDir, selfTank.Velocity) > 0)
                    return transform.position + len * moveDir.normalized;
            }

            return Vector3.zero;


        }

        Missile GetNearestMissile()
        {
            float missileDistance = 1e6f;
            Missile nearest_Missile = null;
            foreach (var pair in Match.instance.GetOppositeMissiles(enemyTank.Team))
            {

                Missile s = pair.Value;

                float temp_MissileDistance = Vector3.Distance(s.Position, selfTank.Position);
                if (temp_MissileDistance < missileDistance)
                {
                    missileDistance = temp_MissileDistance;
                    nearest_Missile = s;
                }
            }

            return nearest_Missile;


        }

        void GetNearestStar(ref Star superStar, ref float distanceToStar, ref NavMeshPath pathStar)
        {

            foreach (var pair in Match.instance.GetStars())
            {
                NavMeshPath temp_PathStar = null;
                Star s = pair.Value;
                temp_PathStar = CaculatePath(s.Position);
                if (s.IsSuperStar)
                    superStar = s;

                float temp_Distance = 0;
                temp_Distance = CalcDistance(temp_PathStar);
                if (distanceToStar > temp_Distance && temp_Distance != 0)
                {
                    distanceToStar = temp_Distance;
                    pathStar = temp_PathStar;
                    if (s.IsSuperStar)
                        break;
                }

            }




        }


        protected override void OnAwake()
        {
            base.OnAwake();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (HP > 0)
            {
                Update_and_ProcessKnowledgePool();
                Do_sth();
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            InitRow_KnowledgePool();
            InitCooked_KnowledgePool();
            InitCondition();
        }

        protected override void OnReborn()
        {
            base.OnReborn();
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(SetPosition, 3f);


        }

        public override string GetName()
        {
            return "kd";
        }
    }
}
