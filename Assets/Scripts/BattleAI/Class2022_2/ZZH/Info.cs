using Main;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZZH
{
    public class Info
    {
        public ETeam myteam;
        public Tank mytank, enemy;
        public Match match;
        public Dictionary<int, Missile> enemyMissiles;
        public Dictionary<int, Star> stars;
        public bool superStarExist, closerToEnemyStar, shouldDodge;
        public Vector3 dodgePosition;
        public Star closestStar, enemyClosestStar, superStar;
        public float directHitDis;
        public int shoot, hit, enemyPreviousHp;
        public bool predictShoot;
        public Info(Tank tank)
        {
            mytank = tank;
            myteam = mytank.Team;
            match = Match.instance;
            enemy = match.GetOppositeTank(myteam);
            directHitDis = match.GlobalSetting.MissileSpeed / 10 * 3; //tank size is 5
            shoot = 0;
            hit = 0;
            predictShoot = true;
        }

        public void updateInfo()
        {
            //decide shooting strategy
            if(enemy.HP < enemyPreviousHp)
            {
                hit += 1;
            }
            if(shoot >= 5 && hit / (float)shoot <= 0.45f)
            {
                predictShoot = !predictShoot;
                shoot = 0;
                hit = 0;
            }
            Debug.Log(predictShoot);
            enemyPreviousHp = enemy.HP;
            //update missile info
            enemyMissiles = match.GetOppositeMissiles(myteam);
            shouldDodge = false;
            foreach(var item in enemyMissiles)
            {
                Missile missile = item.Value;
                if(Vector3.Dot(missile.Position - mytank.Position, enemy.Position - mytank.Position) < 0)
                {
                    continue;
                }
                if (Mathf.Abs(Vector3.Dot(missile.Velocity - mytank.Velocity, missile.Position - mytank.Position)) > 0.95f)
                {
                    shouldDodge = true;
                    Vector3 sup = Vector3.Cross(mytank.Velocity, missile.Velocity);
                    dodgePosition = mytank.Position + (Vector3.Cross(missile.Velocity, sup)).normalized * 20;
                    break;
                }
            }
            if((mytank.Position - enemy.Position).magnitude < directHitDis)
            {
                shouldDodge = false;
            }
            //update star info
            stars = match.GetStars();
            float minDisToMe = float.MaxValue, minDisToEnemy = float.MaxValue;
            closestStar = null;
            enemyClosestStar = null;
            superStarExist = false;
            foreach(var item in stars)
            {
                Star star = item.Value;
                if((star.Position - mytank.Position).sqrMagnitude < minDisToMe)
                {
                    closestStar = star;
                    minDisToMe = (star.Position - mytank.Position).sqrMagnitude;
                }
                if ((star.Position - enemy.Position).sqrMagnitude < minDisToEnemy)
                {
                    enemyClosestStar = star;
                    minDisToEnemy = (star.Position - enemy.Position).sqrMagnitude;
                }
                if (star.IsSuperStar)
                {
                    superStarExist = true;
                    superStar = star;
                }
            }
            closerToEnemyStar = false;
            if(enemyClosestStar != null && (enemyClosestStar.Position - mytank.Position).sqrMagnitude + 1 < (enemyClosestStar.Position - enemy.Position).sqrMagnitude)
            {
                closerToEnemyStar = true;
            }
        }
    }
}

