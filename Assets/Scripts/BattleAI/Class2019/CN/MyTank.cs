using Main;
using UnityEngine;
using UnityEngine.AI;

namespace CN
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
            bool hasStar = false;
            if (Match.instance.GetStars() != null)
                hasStar = true;


            //血量充足
            if (this.HP >= 26)
            {
                if (this.HP >= oppTank.HP)
                {
                    if (oppTank.HP != 0 && hasStar == false)
                    {
                        Move(Match.instance.GetRebornPos(Team));
                    }
                    else
                    {
                        if (oppTank.HP != 0 && hasStar == true)
                        {
                            TurretTurnTo(oppTank.Position);
                            FireToTank();
                            FindStar();

                        }
                        else
                        {
                            ReadyToFire();
                            FindStar();
                        }
                    }
                }
                else
                {
                    if (oppTank.HP == 0)
                    {
                        FindStar();
                        ReadyToFire();
                    }
                    else
                    {
                        TurretTurnTo(oppTank.Position);
                        FireToTank();
                        FindStar();
                    }

                }

            }
            //血量不够，HP<26
            else
            {
                if (this.HP >= oppTank.HP)
                {
                    if (oppTank.HP != 0 && hasStar == true)
                    {
                       
                        FindStar();
                    }
                    
                    else
                    {
                        
                        if (oppTank.HP == 0)
                        {
                            CheckFindStar();
                            ReadyToFire();
                        }
                       
                        else
                        {
                            
                            FindStar();
                        }
                    }
                }
                else
                {

                    
                    Move(Match.instance.GetRebornPos(Team));
                    
                    
                }
            }

        }


        private void ReadyToFire()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Vector3 Base = new Vector3(0, 0, 0);
            Base = Match.instance.GetRebornPos(oppTank.Team);
            TurretTurnTo(Base);
            
        }
        private void FireToTank()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank.HP != 0)
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
            //
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

            //敌方基地位置
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
                    float check = (Position - Base).sqrMagnitude;
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

            //
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
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
        public override string GetName()
        {
            return "CN";
        }
    }
}

