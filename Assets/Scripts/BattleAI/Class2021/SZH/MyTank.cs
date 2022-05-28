using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;


namespace SZH
{
    class InformationPool
    {
        public ETeam Team { get; set; }
        public float MatchTime => Match.instance.GlobalSetting.MatchTime;
        public float RemainingTime => Match.instance.RemainingTime;
        public float DistanceWithEnemy => Vector3.Distance(self.Position, enemy.Position);
        public float DistanceWithStar { get; private set; }
        public float DistanceWithSuperStar { get; private set; }

        public Vector3 ForecastPoint//预测点
        {
            get
            {
                if (enemy.IsDead)
                {
                    return Match.instance.GetRebornPos(Team);
                }
                float time = DistanceWithEnemy / Match.instance.GlobalSetting.MissileSpeed;

                Vector3 result = enemy.Position + enemy.Velocity * time;

                for (int i = 0; i < 5; i++)
                {
                    float distance = Vector3.Distance(self.Position, result);
                    time = distance / Match.instance.GlobalSetting.MissileSpeed;
                    result = enemy.Position + enemy.Velocity * time;
                }

                return result;
            }
        }

        public float unit => 10;
        public bool findEnemy;
        public Tank enemy;
        public Tank self;

        public Star nearestOrdinaryStar;
        public Star superStar;

        public InformationPool(ETeam team)
        {
            Team = team;
            enemy = Match.instance.GetOppositeTank(Team);
        }

        public void RefreshStarInfo()
        {
            nearestOrdinaryStar = null;
            superStar = null;
            DistanceWithStar = float.MaxValue;
            

            foreach (var i in Match.instance.GetStars())
            {
                if (i.Value == null) return;

                Star star = i.Value;

                float distance = Vector3.Distance(self.Position, star.Position);

                if (distance < DistanceWithStar)
                {
                    DistanceWithStar = distance;
                    nearestOrdinaryStar = star;
                }

                if (star.IsSuperStar)
                {
                    superStar = star;
                    DistanceWithSuperStar= Vector3.Distance(self.Position, star.Position);
                }
            }
        }
    }




    class MyTank : Tank
    {
        InformationPool information;//收集的信息

        protected override void OnStart()
        {
            base.OnStart();
            information = new InformationPool(Team);
            information.self = this;
            information.enemy = Match.instance.GetOppositeTank(Team);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            information.RefreshStarInfo();

            Attack(information.enemy);


            if (information.enemy.IsDead && !CanSeeOthers(information.enemy.Position))
            {
                SearchStar(Vector3.zero);
                return;
            }

            if(information.RemainingTime> information.MatchTime/2 +5 && information.RemainingTime < information.MatchTime / 2 + 20)
            {
                
                if (HP < information.enemy.HP)
                {
                    Move(Match.instance.GetRebornPos(Team));
                    return;
                }
                else
                {
                    SearchStar(Vector3.zero);
                }

            }
            if (information.RemainingTime >= information.MatchTime / 2 && information.RemainingTime < information.MatchTime / 2 + 5)
            {
                Move(Vector3.zero);
                return;
            }


            if (HP == Match.instance.GlobalSetting.MaxHP ||HP>information.enemy.HP+25)
            {
                if (information.superStar != null)
                {
                    Move(information.superStar.Position);
                }
                else if (information.DistanceWithEnemy < 5 * information.unit && !CanSeeOthers(information.enemy))
                {
                    Move(information.enemy.Position);
                }
                else
                {
                    if (information.nearestOrdinaryStar != null)
                    {
                        Move(information.nearestOrdinaryStar.Position);
                    }
                    else
                    {
                        Move(Vector3.zero);
                    }
                }
            }

            else if (50 < HP && HP < Match.instance.GlobalSetting.MaxHP)
            {
                if (information.superStar != null)
                {
                    Move(information.superStar.Position);
                }
                else if (information.nearestOrdinaryStar != null)
                {
                    Move(information.nearestOrdinaryStar.Position);
                }
                else
                { 
                    Move(Match.instance.GetRebornPos(Team));
                }
            }
            else
            {
                if (!CanSeeOthers(information.enemy) && information.DistanceWithEnemy > 5 * information.unit)
                {
                    if (information.superStar != null && (information.DistanceWithSuperStar < 4 * information.unit && CanSeeOthers(information.superStar.Position) || information.DistanceWithSuperStar < 3 * information.unit))
                    {
                        Move(information.superStar.Position);
                    }
                    else if (information.nearestOrdinaryStar != null && information.DistanceWithStar < 3 * information.unit && CanSeeOthers(information.nearestOrdinaryStar.Position))
                    {
                        Move(information.nearestOrdinaryStar.Position);
                    }
                    else
                    {
                        Move(Match.instance.GetRebornPos(Team));
                    }
                }
                else
                {
                    Move(Match.instance.GetRebornPos(Team));
                }
            }
    
        }

        protected override void OnReborn()
        {
            base.OnReborn();
        }

        public override string GetName()
        {
            return "SZH";
        }

        void Attack(Tank tank)
        {
            if (CanSeeOthers(tank))
            {
                TurretTurnTo(tank.Position);
                Vector3 toTarget = tank.Position - FirePos;
                toTarget.y = 0;
                toTarget.Normalize();
                if (Vector3.Dot(TurretAiming, information.ForecastPoint) > 0.99f)
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(information.enemy.Position);
            }
        }

        void SearchStar(Vector3 defaultPos)
        {
            if (information.superStar != null)
            {
                Move(information.superStar.Position);
            }
            else if (information.nearestOrdinaryStar != null)
            {
                Move(information.nearestOrdinaryStar.Position);
            }
            else
            {
                Move(defaultPos);
            }
        }
    }
}
