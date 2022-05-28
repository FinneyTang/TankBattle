using Main;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
//Mathc Time 180
namespace LGB
{

    abstract class Condition
    {
        public abstract bool IsTrue(Tank tank);

    }
    class WinCondition : Condition
    {
        
        public override bool IsTrue(Tank tank)
        {
            int EnemyScore = (int)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.EnemyScore];
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
            Dictionary<int, Star> stars = (Dictionary<int, Star>)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.Stars];
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
            Vector3 Target= (Vector3)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.Target];

            return tank.GetComponent<MyTank>().CanSeeOthers(Target);

        }
    }
    class CanSeeEnemy : Condition
    {

        public override bool IsTrue(Tank tank)
        {
            Vector3 Target = (Vector3)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.EnemyPos];

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
        public SetHpCompare(Cpr myCpr,int SetHp)
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
            
            return myCpr(tank.HP ,(int)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.EnemyHp]);
        }

       
    }
    class SuperStarExist : Condition
    {
      

        public override bool IsTrue(Tank tank)
        {
            Star SuperStar = (Star)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.SuperStar];
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
            float distanceToStar= (float)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.DistanceToStar];
            
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
       
       public TimeBetween(float timeBegin, float timeOver=0)
        {
            this.timeBegin = timeBegin;
            this.timeOver = timeOver;

        }
        public override bool IsTrue(Tank tank)
        {
            float remainingTime = Match.instance.RemainingTime;
            if (timeBegin > remainingTime&&timeOver<remainingTime)
                return true;
            else
                return false;
           
        }
    }

    class HasMissle : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            Missile missile = (Missile)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.NearestMissile];
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
            int EnemyHp = (int)tank.GetComponent<MyTank>().m_HandleKnowledgePool[MyTank.HandleKnowledgePool.EnemyHp];
            if (EnemyHp == 0)
                return true;
            else
                return false;

        }
    }
    public class MyTank : Tank
    {
      




        private float m_FireTime;
        private Vector3 SetPosition;
        Tank selfTank;
        Tank enemyTank;
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
      public  enum KnowledgePool
        {
            StarMessage,
            EnemyMessage,
            Time,
            CanFire,
            OppsiteRebornPos,
            SelfMessage

        };
     public enum HandleKnowledgePool
        {
           SuperStar,
           EnemyHp,
           Target,
           Stars,
            NearestMissile,
            EnemyPos,
            DistanceToStar,
            EnemyScore,
          
        };
        public enum Destination
        {
            EscapePlace,
            SuperStarPlace,
            StarPath,
            HomePath

        };
        public Dictionary<KnowledgePool, object> m_KnowledgePool;
        public Dictionary<HandleKnowledgePool, object> m_HandleKnowledgePool;
        public Dictionary<Destination, object> m_DestinationPool;

        bool Larger(int a, int b)
        {
            return a > b;

        }
        bool Smaller(int a, int b)
        {
            return a <= b;

        }
        bool InitKnowledgePool()
        {
             
            m_KnowledgePool = new Dictionary<KnowledgePool, object>();
            m_KnowledgePool.Add(KnowledgePool.StarMessage, Match.instance.GetStars());
            m_KnowledgePool.Add(KnowledgePool.EnemyMessage, Match.instance.GetOppositeTank(Team));
            m_KnowledgePool.Add(KnowledgePool.Time, Match.instance.RemainingTime);
            m_KnowledgePool.Add(KnowledgePool.SelfMessage, Match.instance.GetTank(Team));
            m_KnowledgePool.Add(KnowledgePool.OppsiteRebornPos, Match.instance.GetRebornPos(Match.instance.GetOppositeTank(Team).Team));
            return true;

        }
        public new bool Move(Vector3 targetPos)
        {
            if (targetPos == Vector3.zero) {
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
        bool InitHandleKnowledgePool()
        {
            canSeePos = new CanSeePos();
            m_HandleKnowledgePool = new Dictionary<HandleKnowledgePool, object>();
            m_HandleKnowledgePool.Add(HandleKnowledgePool.Stars, Match.instance.GetStars());
            m_HandleKnowledgePool.Add(HandleKnowledgePool.SuperStar, null);
            m_HandleKnowledgePool.Add(HandleKnowledgePool.EnemyHp, 0);
            m_HandleKnowledgePool.Add(HandleKnowledgePool.Target, new Vector3(0,0,0));
            m_HandleKnowledgePool.Add(HandleKnowledgePool.NearestMissile, null);
            m_HandleKnowledgePool.Add(HandleKnowledgePool.EnemyPos, new Vector3(0, 0, 0));
            m_HandleKnowledgePool.Add(HandleKnowledgePool.DistanceToStar, 0);
            m_HandleKnowledgePool.Add(HandleKnowledgePool.EnemyScore, 0);
            return true;
        }
        bool InitDestinationPool()
        {
            m_DestinationPool = new Dictionary<Destination, object>();
            m_DestinationPool.Add(Destination.EscapePlace, new Vector3(0, 0, 0));
            m_DestinationPool.Add(Destination.HomePath, null);
            m_DestinationPool.Add(Destination.StarPath,null);
            m_DestinationPool.Add(Destination.SuperStarPlace, new Vector3(0, 0, 0.01f));
            return true;
        }
        bool InitCondition()
        {
             canSeePos=new CanSeePos();
             timeBetween = new TimeBetween(100,90);
             hpCompare=new HpCompare(Larger);
             canFire=new CanFire();
             superStarExist=new SuperStarExist();
            canSeeEnemy = new CanSeeEnemy();
            starExist=new StarExist();
             hasMissile = new HasMissle();
            backToHomeHp = new SetHpCompare(Smaller, 26);
            enemyTankDead = new EnemyTankDead();
            LimitedHp = new SetHpCompare(Smaller, 99);
            distanceToStar = new DistanceToStar(25f);
            winCondition = new WinCondition();
            return true;




        }
        bool UpdateKnowledgePool()
        {

            m_KnowledgePool[KnowledgePool.StarMessage] = Match.instance.GetStars();
            m_KnowledgePool[KnowledgePool.EnemyMessage] = Match.instance.GetOppositeTank(Team);
            m_KnowledgePool[KnowledgePool.Time] = Match.instance.RemainingTime;
            m_KnowledgePool[KnowledgePool.SelfMessage] = Match.instance.GetTank(Team);
            return true;
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
        Vector3 CalcEscape(Missile nearest_Missile,Tank enemyTank)
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
                if (path != null && !CanSeeOthers(transform.position + len * moveDir.normalized, nearest_Missile.Position)&& CanSeeOthers(transform.position + len * moveDir.normalized, transform.position))
                    return transform.position + len * moveDir.normalized;
                path = CaculatePath(transform.position - len * moveDir.normalized);
                if (path != null && !CanSeeOthers(transform.position -len * moveDir.normalized, nearest_Missile.Position) && CanSeeOthers(transform.position - len * moveDir.normalized, transform.position))
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
                if (path != null&&Vector3.Dot(moveDir,selfTank.Velocity)>0)
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
        bool HandleKnowledge()
        {
             enemyTank = (Tank)m_KnowledgePool[KnowledgePool.EnemyMessage];
             selfTank = (Tank)m_KnowledgePool[KnowledgePool.SelfMessage];
            Vector3 escapePlace=Vector3.zero;
            
           
            Dictionary<int, Star> stars = (Dictionary<int, Star>)m_KnowledgePool[KnowledgePool.StarMessage];
            
            float remainingtime = (float)m_KnowledgePool[KnowledgePool.Time];
            NavMeshPath pathHome = CaculatePath(Match.instance.GetRebornPos(selfTank.Team));
            NavMeshPath pathStar = null;
            Star superStar=null;
            float distanceToEnemy = 0;
            float distanceToStar = 1e6f;

            GetNearestStar(ref superStar, ref distanceToStar, ref pathStar);
           



            float score_GetStarPerSecond = 5.0f / (distanceToStar / selfTank.Velocity.magnitude);
            float score_GetEnemyPerSecond = 15.0f / (distanceToEnemy / selfTank.Velocity.magnitude + 6.0f);
            float time_trace = ((enemyTank.Position - selfTank.FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed);
           
            SetPosition = enemyTank.Position + enemyTank.Velocity * (((enemyTank.Position + enemyTank.Velocity * Time.deltaTime * time_trace - selfTank.FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed));
            TurretTurnTo(SetPosition);
            
            m_HandleKnowledgePool[HandleKnowledgePool.Target] = SetPosition;
            Debug.Log(CanSeeOthers(SetPosition));



            Missile nearest_Missile = GetNearestMissile();
            if (nearest_Missile != null)
                Debug.Log(nearest_Missile.Team);
         
            escapePlace = CalcEscape(nearest_Missile, selfTank);
            m_HandleKnowledgePool[HandleKnowledgePool.Stars] = stars;
            m_HandleKnowledgePool[HandleKnowledgePool.SuperStar] = superStar;
            m_HandleKnowledgePool[HandleKnowledgePool.EnemyHp] = enemyTank.HP;
            m_HandleKnowledgePool[HandleKnowledgePool.NearestMissile] = nearest_Missile;
            m_HandleKnowledgePool[HandleKnowledgePool.EnemyPos] = enemyTank.Position;
            m_HandleKnowledgePool[HandleKnowledgePool.DistanceToStar] = distanceToStar;
            m_HandleKnowledgePool[HandleKnowledgePool.EnemyScore]=enemyTank.Score;
            m_DestinationPool[Destination.EscapePlace] = escapePlace;
            m_DestinationPool[Destination.HomePath] = pathHome;
            m_DestinationPool[Destination.StarPath] = pathStar;
            m_DestinationPool[Destination.SuperStarPlace] = new Vector3(0,0,0.01f);
             

            OnAction();
                    return true;
        }

        bool OnAction()
        {
            Vector3 escapePlace = (Vector3)m_DestinationPool[Destination.EscapePlace];
            NavMeshPath pathStar = (NavMeshPath)m_DestinationPool[Destination.StarPath];
            NavMeshPath pathHome = (NavMeshPath)m_DestinationPool[Destination.HomePath];
            Vector3 superStarPlace = (Vector3)m_DestinationPool[Destination.SuperStarPlace];
           
            if (canSeePos.IsTrue(selfTank) && canFire.IsTrue(selfTank)&& !enemyTankDead.IsTrue(selfTank))
            {
                Fire();
                
            }
            if (winCondition.IsTrue(selfTank))
            {
                Move(pathHome);
                return true;
            }
            if (timeBetween.IsTrue(selfTank))
            {
                Move(superStarPlace);
                return true;
            }
            if (superStarExist.IsTrue(selfTank)) {
                Move(superStarPlace);
                   return true;
            }
            if (!enemyTankDead.IsTrue(selfTank))
                if (hasMissile.IsTrue(selfTank))
                {
                    Move(escapePlace);
                    return true;
                }
                else if (canSeeEnemy.IsTrue(selfTank) && hasMissile.IsTrue(selfTank))
                {


                    

                    Move(escapePlace);
                    return true;
                }

            if (hpCompare.IsTrue(selfTank) && starExist.IsTrue(selfTank))
                Move(pathStar);

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
        protected override void OnAwake() { base.OnAwake(); }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (HP > 0)
            {
                
                UpdateKnowledgePool(); HandleKnowledge();
            }
        }
        protected override void OnStart() { base.OnStart(); InitKnowledgePool();InitHandleKnowledgePool();InitCondition();InitDestinationPool(); }

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
            return "LGB";
        }
    }
}
