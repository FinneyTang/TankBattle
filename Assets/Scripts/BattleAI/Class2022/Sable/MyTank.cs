using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Main;
using System.Linq;

namespace Sable
{
    public enum PositionInfo
    { 
        EnemyPos,
        EnemyLastPos,
        MyPos,
        NearestStarPos,
        SecondNearestStarPos,
        SuperStarPos,
        HomePos,
        EnemyHomePos,
        FireTarget,
        NearestMissilePos,
    }

    public enum MatchInfo
    { 
        MyHp,
        EnemyHp,
        MyScore,
        EnemyScore,
        MatchTime,
        EnemyRebornTime,
    }

    public enum PathInfo
    { 
        HomePath,
        NearestStarPath,
        SecondNearestStarPath,
        SuperStarPath,
        EnemySuperStarPath,
        EscapePath,
        PursuitPath
    }

    public class MyTank : Tank
    {
        public Dictionary<PositionInfo, Vector3> positionInfos = new Dictionary<PositionInfo, Vector3>();
        public Dictionary<MatchInfo, float> matchInfos = new Dictionary<MatchInfo, float>();
        public Dictionary<PathInfo, NavMeshPath> pathInfos = new Dictionary<PathInfo, NavMeshPath>();

        Tank enemyTank;
        Tank myTank;

        Condition canFire;
        Condition canSeeEnemy;
        Condition isEnemyDead;
        Condition isHpMoreThanEnemy;
        Condition isLowHp;
        Condition isSourceMoreThanEnemy;
        Condition isNearbyEnemyHome;
        Condition isSuperStarTimeClose;
        IsEnemyCloserStar isEnemyCloserStar;

        protected override void OnAwake()
        {
            base.OnAwake();
        }

        protected override void OnStart()
        {
            base.OnStart();
            InitKnowledgePool();
            InitConditions();
        }

        private void InitKnowledgePool()
        {
            enemyTank = Match.instance.GetOppositeTank(Team);
            myTank = Match.instance.GetTank(Team);
            positionInfos.Add(PositionInfo.EnemyPos,enemyTank.Position);
            positionInfos.Add(PositionInfo.EnemyLastPos,enemyTank.Position);
            positionInfos.Add(PositionInfo.MyPos,myTank.Position);
            positionInfos.Add(PositionInfo.NearestStarPos,Vector3.zero);
            positionInfos.Add(PositionInfo.SecondNearestStarPos,Vector3.zero);
            positionInfos.Add(PositionInfo.NearestMissilePos, Vector3.zero);
            positionInfos.Add(PositionInfo.SuperStarPos,Vector3.zero);
            positionInfos.Add(PositionInfo.HomePos,Match.instance.GetRebornPos(Team));
            positionInfos.Add(PositionInfo.EnemyHomePos,Match.instance.GetRebornPos(enemyTank.Team));
            positionInfos.Add(PositionInfo.FireTarget,Vector3.zero);

            matchInfos.Add(MatchInfo.MyHp, myTank.HP);
            matchInfos.Add(MatchInfo.EnemyHp, enemyTank.HP);
            matchInfos.Add(MatchInfo.MyScore, myTank.Score);
            matchInfos.Add(MatchInfo.EnemyScore, enemyTank.Score);
            matchInfos.Add(MatchInfo.MatchTime, Match.instance.RemainingTime);
            matchInfos.Add(MatchInfo.EnemyRebornTime, 0) ;

            pathInfos.Add(PathInfo.HomePath, null);
            pathInfos.Add(PathInfo.SuperStarPath, null);
            pathInfos.Add(PathInfo.NearestStarPath, null);
            pathInfos.Add(PathInfo.SecondNearestStarPath, null);
            pathInfos.Add(PathInfo.EscapePath, null);
            pathInfos.Add(PathInfo.PursuitPath, null);

        }

        private void InitConditions()
        {
            canFire = new CanFire();
            canSeeEnemy = new CanSeeENemy();
            isEnemyDead = new IsEnemyDead();
            isHpMoreThanEnemy = new IsHpMoreThanEnemy(enemyTank, 24);
            isLowHp = new IsLowHp(75);
            isSourceMoreThanEnemy = new IsScoreMoreThanEnemy(enemyTank, 100);
            isNearbyEnemyHome = new IsNearByEnemyHome(positionInfos[PositionInfo.EnemyHomePos], 40);
            isSuperStarTimeClose = new IsSuperStarTimeClose(5f);
            isEnemyCloserStar = new IsEnemyCloserStar();
        }

        private void UpdateKnowledgePool()
        {
            positionInfos[PositionInfo.EnemyLastPos] = positionInfos[PositionInfo.EnemyPos];
            positionInfos[PositionInfo.EnemyPos] = enemyTank.Position;
            positionInfos[PositionInfo.MyPos] = myTank.Position;

            NavMeshPath nearestStarPath = null;
            NavMeshPath secNearestStarPath = null;
            float distance = float.MaxValue;
            float secDis = float.MaxValue;
            Star nearestStar = null;
            Star secNearestStar = null;
            Star superStar = null;
            Dictionary<int,Star> stars = Match.instance.GetStars();
            foreach (var starPair in stars)
            {
                Star star = starPair.Value;
                if (!star.IsSuperStar)
                {
                    NavMeshPath path = CaculatePath(star.Position);
                    float tempDis = Tool.GetPathDistance(path);
                    if (tempDis < distance)
                    {
                        nearestStar = star;
                        distance = tempDis;
                        nearestStarPath = path;
                    }
                    else if (tempDis < secDis && tempDis > distance)
                    {
                        secNearestStar = star;
                        secDis = tempDis;
                        secNearestStarPath = path;
                    }
                }
                else
                {
                    superStar = star;
                    distance = Tool.GetPathDistance(CaculatePath(superStar.Position));
                    break;
                }
            }

            if(nearestStar != null)
                positionInfos[PositionInfo.NearestStarPos] = nearestStar.Position;
            if (secNearestStar != null)
                positionInfos[PositionInfo.SecondNearestStarPos] = secNearestStar.Position;
            pathInfos[PathInfo.NearestStarPath] = nearestStarPath;
            pathInfos[PathInfo.SecondNearestStarPath] = secNearestStarPath;
            if (superStar != null)
            {
                positionInfos[PositionInfo.SuperStarPos] = superStar.Position;
                pathInfos[PathInfo.SuperStarPath] = CaculatePath(superStar.Position);
            }
            else
            {
                pathInfos[PathInfo.SuperStarPath] = null;
            }
            float tempEnemyDis = Vector3.Distance(myTank.Position, enemyTank.Position);
            float time = tempEnemyDis / enemyTank.Velocity.magnitude;

            Vector3 enemyDir = (positionInfos[PositionInfo.EnemyPos] - positionInfos[PositionInfo.EnemyLastPos]).normalized;
            positionInfos[PositionInfo.FireTarget] = enemyDir * time + enemyTank.Position;

            float nearestMissileDis = float.MaxValue;
            Missile nearestMissile = null;
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(Team);
            foreach (var missilePair in missiles)
            {
                Missile missile = missilePair.Value;
                float tempMissileDis = Vector3.Distance(myTank.Position, missile.Position);
                if (tempEnemyDis < nearestMissileDis)
                {
                    nearestMissileDis = tempMissileDis;
                    nearestMissile = missile;
                }
            }
            if (nearestMissile != null)
                positionInfos[PositionInfo.NearestMissilePos] = nearestMissile.Position;
            else
                positionInfos[PositionInfo.NearestMissilePos] = Vector3.zero;

            if (positionInfos[PositionInfo.NearestMissilePos] != Vector3.zero)
            {
                Vector3 escapePos = Vector3.zero;
                float missilesDistance = Vector3.Distance(myTank.Position, positionInfos[PositionInfo.NearestMissilePos]);
                float missilesTime = missilesDistance / Match.instance.GlobalSetting.MissileSpeed;
                Vector3 myPrePos = myTank.Velocity * missilesTime + myTank.Position;
                if (Vector3.Dot(nearestMissile.Velocity.normalized, (myPrePos - nearestMissile.Position).normalized) > 0.99f)
                {
                    escapePos = Vector3.Cross(Vector3.Cross(myTank.Velocity.normalized, (enemyTank.Position - myPrePos).normalized).y > 0.1f ? Vector3.down : Vector3.up, nearestMissile.Velocity).normalized * 10 + myPrePos;
                }
                else if (Vector3.Dot((Position - nearestMissile.Position).normalized, nearestMissile.Velocity.normalized) > 0.99f)
                {
                    escapePos = Vector3.Cross(Vector3.Cross(myTank.Velocity.normalized, (enemyTank.Position - myPrePos).normalized).y > 0.1f ? Vector3.up : Vector3.down, nearestMissile.Velocity).normalized * 10 + myPrePos;
                }
                if (escapePos != Vector3.zero)
                {
                    pathInfos[PathInfo.EscapePath] = CaculatePath(escapePos);
                }
            }
            else
            {
                pathInfos[PathInfo.EscapePath] = null;
            }


            NavMeshPath homePath = null;
            homePath = CaculatePath(positionInfos[PositionInfo.HomePos]);
            pathInfos[PathInfo.HomePath] = homePath;

            NavMeshPath pursuitPath = null;
            pursuitPath = CaculatePath(positionInfos[PositionInfo.EnemyPos]);
            pathInfos[PathInfo.PursuitPath] = pursuitPath;
        }

        private void UpdateAction()
        {
            TurretTurnTo(positionInfos[PositionInfo.FireTarget]);
            if (!myTank.IsDead && (canSeeEnemy.IsTrue(myTank) || CanSeeTarget(positionInfos[PositionInfo.FireTarget])) && canFire.IsTrue(myTank))
            {
                Fire();
            }


            NavMeshPath movePath = null;


            if (pathInfos[PathInfo.NearestStarPath] != null && !myTank.IsDead)
            {
                if (!isEnemyDead.IsTrue(myTank) && !isEnemyCloserStar.IsTrue(myTank, enemyTank, positionInfos[PositionInfo.NearestStarPos]))
                {
                    if (pathInfos[PathInfo.SecondNearestStarPath] != null)
                    {
                        movePath = pathInfos[PathInfo.SecondNearestStarPath];
                    }
                }
                else
                {
                    movePath = pathInfos[PathInfo.NearestStarPath];
                }

            }


            if (!myTank.IsDead && isLowHp.IsTrue(myTank) && !isEnemyDead.IsTrue(myTank))
            {
                movePath = pathInfos[PathInfo.HomePath];
            }

            if (!myTank.IsDead && isSourceMoreThanEnemy.IsTrue(myTank))
            {
                movePath = pathInfos[PathInfo.HomePath];
            }

            if (isHpMoreThanEnemy.IsTrue(myTank) && !isEnemyDead.IsTrue(myTank))
            {
                movePath = pathInfos[PathInfo.PursuitPath];
            }



            if (isSuperStarTimeClose.IsTrue(myTank))
            {
                movePath = CaculatePath(Vector3.zero);
            }

            if (pathInfos[PathInfo.SuperStarPath] != null)
            {
                movePath = pathInfos[PathInfo.SuperStarPath];
            }

            if (pathInfos[PathInfo.EscapePath] != null)
            {
                movePath = pathInfos[PathInfo.EscapePath];
            }



            if (movePath == null)
            {
                Move(pathInfos[PathInfo.PursuitPath]);
            }
            else
            {
                Move(movePath);
            }
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            UpdateKnowledgePool();
            UpdateAction();
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            if(pathInfos[PathInfo.EscapePath] != null)
            Gizmos.DrawWireSphere(pathInfos[PathInfo.EscapePath].corners.Last(), 3f);
        }

        protected override void OnReborn()
        {
            base.OnReborn();
        }

        public override string GetName()
        {
            return "Sable";
        }

        private bool CanSeeTarget(Vector3 target)
        {
            return Physics.Raycast(positionInfos[PositionInfo.MyPos], (target - positionInfos[PositionInfo.MyPos]), 100f);
        }

    }

    public abstract class Condition
    {
        public abstract bool IsTrue(Tank tank);
    }

    public class CanFire : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            return tank.CanFire();
        }
    }

    public class CanSeeENemy : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            return tank.CanSeeOthers(Match.instance.GetOppositeTank(tank.Team).Position);
        }
    }

    public class IsEnemyDead : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            return Match.instance.GetOppositeTank(tank.Team).IsDead;
        }
    }

    public class IsHpMoreThanEnemy : Condition
    {
        Tank enemyTank;
        int moreHp;

        public IsHpMoreThanEnemy(Tank _enemyTank, int _moreHp)
        {
            enemyTank = _enemyTank;
            moreHp = _moreHp;
        }


        public override bool IsTrue(Tank tank)
        {
            return tank.HP >= enemyTank.HP + moreHp;
        }
    }

    public class IsLowHp : Condition
    {
        int lowHp;
        public IsLowHp(int _lowHp)
        {
            lowHp = _lowHp;
        }
        public override bool IsTrue(Tank tank)
        {
            return tank.HP <= lowHp;
        }
    }

    public class IsScoreMoreThanEnemy : Condition
    {
        Tank enemyTank;
        int moreScore;

        public IsScoreMoreThanEnemy(Tank _enemyTank, int _moreScore)
        {
            enemyTank = _enemyTank;
            moreScore = _moreScore;
        }
        public override bool IsTrue(Tank tank)
        {
            return tank.Score > (enemyTank.Score + moreScore);
        }
    }

    public class IsNearByEnemyHome : Condition
    {
        Vector3 enemyHomePos;
        float distance;
        public IsNearByEnemyHome(Vector3 _enemyHomePos, float _distance)
        {
            enemyHomePos = _enemyHomePos;
            distance = _distance;
        }

        public override bool IsTrue(Tank tank)
        {
            return Vector3.Distance(tank.Position, enemyHomePos) < distance;
        }
    }


    public class IsSuperStarTimeClose : Condition
    {
        float closeTime;
        public IsSuperStarTimeClose(float _closeTime)
        {
            closeTime = _closeTime;
        }

        public override bool IsTrue(Tank tank)
        {
            return (Match.instance.RemainingTime < 90 + closeTime) && (Match.instance.RemainingTime > 90);
        }
    }

    public class IsEnemyCloserStar : Condition
    {

        public bool IsTrue(Tank tank, Tank enemyTank, Vector3 starPos)
        {
            NavMeshPath toStar = tank.CaculatePath(starPos);
            NavMeshPath enemyToStar = enemyTank.CaculatePath(starPos);
            if (toStar == null)
            {
                return true;
            }
            else if (enemyToStar == null && toStar != null)
            {
                return false;
            }
            else
            {
                return Tool.GetPathDistance(toStar) > Tool.GetPathDistance(enemyToStar);
            }

        }

        public override bool IsTrue(Tank tank)
        {
            throw new System.NotImplementedException();
        }
    }


    static class Tool
    {
        public static float GetPathDistance(NavMeshPath path)
        {
            float distance = 0f;
            for (int i = 0; i < path.corners.Length - 1; ++i)
            {
                distance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            }
            return distance;
        }

    }

}
