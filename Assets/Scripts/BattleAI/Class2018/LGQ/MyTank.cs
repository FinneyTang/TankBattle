using Main;
using UnityEngine;
using UnityEngine.AI;

namespace Lee
{
    class MyTank : Tank
    {
        private float m_LastTime = 0;

        protected override void OnUpdate()
        {
            base.OnUpdate();

            //获取敌方坦克
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            

            //是否有星星
            bool HasStar = false;
            if (Match.instance.GetStars() != null)
            HasStar = true;


            //血量足够
            if (this.HP >= 40)
            {
                if (this.HP >= oppTank.HP)
                {
                    if (oppTank.HP != 0 && HasStar == false)
                    {
                        TurretTurnTo(oppTank.Position);FireToTank();Move(oppTank.Position);
                    }
                    else
                    {
                        if (oppTank.HP != 0 && HasStar == true)
                        {
                            TurretTurnTo(oppTank.Position);FireToTank();FindStar();

                        }
                        else { ReadyToFire(); FindStar();  }
                    }
                }
                else
                {
                    if (oppTank.HP == 0)
                    { FindStar(); ReadyToFire(); }
                    else
                    { TurretTurnTo(oppTank.Position); FireToTank();FindStar();}

                }

            }
            //血量不够，HP<40
            else 
            {
                if (this.HP >= oppTank.HP)
                {
                    //血量小于40，血量小于对面，对面有坦克，有星星
                    //找星星，开炮
                    if (oppTank.HP != 0 && HasStar == true)
                    {
                        TurretTurnTo(oppTank.Position); FireToTank();FindStar();
                    }
                    //血量小于40，血量小于对面，对面有坦克没星星/对面没坦克+有无星星
                    else
                    {
                        //血量小于40，对面没坦克
                        if (oppTank.HP == 0)
                        {
                            CheckFindStar();
                            ReadyToFire();
                        }
                        //血量小于40，坦克不为空
                        else
                        {
                            TurretTurnTo(oppTank.Position);
                            FireToTank();
                            FindStar();
                        }
                    }
                }
                else
                {
                    TurretTurnTo(oppTank.Position);
                    FireToTank();
                    FindStar();
                }
            }

        }


        private void ReadyToFire()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Vector3 Base = new Vector3(0,0,0);
            Base = Match.instance.GetRebornPos(oppTank.Team);
            TurretTurnTo(Base);
            //Fire();
        }
        private  void FireToTank()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank.HP!=0)
            {
                if (CanSeeOthers(oppTank))
                {
                    Vector3 toTarget = oppTank.Position - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    if (Vector3.Dot(TurretAiming, toTarget) > 0.99f)
                    {
                        Fire();
                    }
                }
            }
        }

        private void FindStar()
        {
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            //确定星星位置
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = s.Position;
                    break;
                }
                else
                {
                    float dist = (s.Position - Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position;
                    }
                }
            }
            //前进！
            if (hasStar == true)
            {
                Move(nearestStarPos);
            }
            else
            {
                if (Time.time > m_LastTime)
                {
                    if (ApproachNextDestination())
                    {
                        m_LastTime = Time.time + Random.Range(3, 8);
                    }
                }
            }

        }

        private void CheckFindStar()
        {

            Vector3 test = new Vector3(0, 0, 0);
            test = Match.instance.GetRebornPos(Team);

            //敌方泉水位置
            ETeam a;
            if (Team == 0) { a = (ETeam)1; } else a = 0;
            Vector3 test1 = new Vector3(0, 0, 0);
            test1 = Match.instance.GetRebornPos(a);
            Vector3 Base = test1;

            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            //确定星星位置
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = s.Position;
                    break;
                }
                else
                {
                    //星星和自己的距离
                    float dist = (Position - s.Position).sqrMagnitude;
                    //星星和对方基地的距离
                    float StarToBase = (s.Position - Base).sqrMagnitude;
                    //自己和对方基地的距离
                    float check= (Position-Base).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position;
                    }
                    else if (StarToBase < check)
                    {
                        hasStar = false;
                        nearestDist = test.sqrMagnitude;
                        nearestStarPos = test;
                    }
                }
            }

            //前进！
            if (hasStar == true)
            {
                Move(nearestStarPos);
            }
            else
            {
                if (Time.time > m_LastTime)
                {
                    if (ApproachNextDestination())
                    {
                        m_LastTime = Time.time + Random.Range(3, 8);
                    }
                }
            }

        }

        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
        public override string GetName()
        {
            return "Lee";
        }
    }
}
