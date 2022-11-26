using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using AI.FiniteStateMachine;
using System.Threading;

namespace WYJ
{
    public class MyTank : Tank
    {
        private Tank enemyTank;
        private Dictionary<int, Star> stars = new Dictionary<int, Star>();
        private Star aimStar;
        private Vector3 movePos;
        private Vector3 aimPos;
        private bool mustGoHome = false;
        private Vector3 superStarPos = new Vector3(0, 0.5f, 0);
        private float superStarTime = Match.instance.GlobalSetting.MatchTime / 2;
        private float gameTimer = -10;
        public float SuperStarCD
        {
            get
            {
                return superStarTime - gameTimer;
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            enemyTank = Match.instance.GetOppositeTank(Team);
        }
        protected override void OnReborn()
        {
            base.OnReborn();

            gameTimer += Match.instance.GlobalSetting.RebonCD;
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();

            Gizmos.DrawWireSphere(aimPos, 3f);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();

            gameTimer += Time.deltaTime;

            StratedgyLayer();

            Move(movePos);

            TurretTurnTo(aimPos);
            if (CanSeeOthers(enemyTank) &&CanFire())
            {
                Fire();
            }
        }
        private void StratedgyLayer()
        {
            if (enemyTank.IsDead)
            {
                float rebornCD = enemyTank.GetRebornCD(Time.time);
                if (rebornCD > 4 || HP >= 75)
                {
                    CalNearestStar();
                    if (aimStar != null)
                    {
                        movePos = aimStar.Position;
                    }
                    else
                    {
                        movePos = Position;
                    }
                }
                else
                {
                    mustGoHome = true;
                }
            }
            else if (!enemyTank.IsDead)
            {
                if (IsHPMoreThanEnemy() && HP >= 75)
                {
                    CalNearestStar();
                }
                else if (IsHPMoreThanEnemy() && HP >= 50)
                {
                    CalSafeStar();
                }
                if (aimStar != null)
                {
                    movePos = aimStar.Position;
                }
                else if (HP >= 75)
                {
                    movePos = enemyTank.NextDestination;
                }
                else
                {
                    movePos = Match.instance.GetRebornPos(Team);
                }
            }
            if (SuperStarCD > 10 && SuperStarCD < 11)
            {
                mustGoHome = true;
            }
            if ((SuperStarCD >= 0 && SuperStarCD <= 10 && !mustGoHome) || IsSuperStarExist())
            {
                movePos = superStarPos;
            }
            if (mustGoHome == true)
            {
                movePos = Match.instance.GetRebornPos(Team);
                if (HP > 70)
                    mustGoHome = false;
            }

            float time_trace = (enemyTank.Position - FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed;
            aimPos = enemyTank.Position + enemyTank.Velocity * (enemyTank.Position + enemyTank.Velocity * Time.deltaTime * time_trace - FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed;
        }

        private bool IsHPMoreThanEnemy()
        {
            return HP >= enemyTank.HP;
        }

        private bool IsSuperStarExist()
        {
            foreach (var item in stars)
            {
                if (item.Value.IsSuperStar)
                {
                    return true;
                }
            }
            return false;
        }

        private void CalSafeStar()
        {
            stars = Match.instance.GetStars();
            Star s = null;

            if (stars.Count > 1)
            {
                float maxDisGap = -999;
                foreach (var item in stars)
                {
                    float toEnemyDis = Vector3.Distance(enemyTank.Position, item.Value.Position);
                    float toSelfDis = Vector3.Distance(FirePos, item.Value.Position);
                    float disGap = toEnemyDis - toSelfDis;

                    if (disGap > maxDisGap)
                    {
                        maxDisGap = disGap;
                        s = item.Value;
                    }
                }
            }
            aimStar = s;
        }
        private void CalNearestStar()
        {
            stars = Match.instance.GetStars();
            Star s = null;

            float dis = 999;
            if(stars.Count > 0)
            {
                foreach (var item in stars)
                {
                    float toSelfDis = Vector3.Distance(Position,item.Value.Position);
                    if(toSelfDis < dis)
                    {
                        dis = toSelfDis;
                        s = item.Value;
                    }
                }
            }
            aimStar = s;
        }
        public override string GetName()
        {
            return "WYJ";
        }
    }
}
