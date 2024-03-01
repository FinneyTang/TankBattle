using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using UnityEngine.AI;

namespace LYJ
{
    class  MyTank : Tank
    {
        private float m_LastTime = 0;
        bool hasStar = false;
        Tank oppTank;

        protected override void OnUpdate()
        {
            base.OnUpdate();

            oppTank = Match.instance.GetOppositeTank(Team);

            //场上是否有星星
            if (Match.instance.GetStars() != null)
                hasStar = true;

            //根据坦克血量分为两个不同状态
            //1血量充足
            if (this.HP > 50)
            {
                if (this.HP > oppTank.HP)
                {
                    if (oppTank.HP > 0 && !hasStar)
                    {
                        Move(Match.instance.GetRebornPos(Team));
                    }
                    else
                    {
                        if (oppTank.HP > 0 && hasStar)
                        {
                            TurretTurnTo(oppTank.Position);
                            Attack();
                            FindStar();
                        }
                        else
                        {
                            PreparedState();
                            FindStar();
                        }
                    }
                }
                else if (oppTank.HP == 0)
                {
                        FindStar();
                        PreparedState();
                }
                else if(oppTank.HP!=0)
                {
                    TurretTurnTo(oppTank.Position);
                    Attack();
                    FindStar();
                }
            }

            //2低血量状态
            else if (this.HP <= 50)
            {
                if(this.HP >= oppTank.HP)
                {
                    if (oppTank.HP > 0 && hasStar)
                    {
                        FindStar();
                    }
                    else if (oppTank.HP == 0)
                    {
                        CheckStar();
                        PreparedState();
                    }
                    else FindStar();
                }
                else
                {
                    Move(Match.instance.GetRebornPos(Team));
                }
            }
        }

        void PreparedState()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Vector3 m_base = new Vector3(0, 0, 0);
            m_base = Match.instance.GetRebornPos(oppTank.Team);
            TurretTurnTo(m_base);
        }

        void Attack()
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

        private void CheckStar()
        {

            Vector3 test = new Vector3(0, 0, 0);
            test = Match.instance.GetRebornPos(Team);

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
                    float StarToOppBase = (s.Position - Base).sqrMagnitude;
                    //自己和对方基地的距离
                    float StarToBase = (Position - Base).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position;
                    }
                    else if (StarToOppBase < StarToBase)
                    {
                        hasStar = false;
                        nearestDist = test.sqrMagnitude;
                        nearestStarPos = test;
                    }
                }
            }

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

        private bool ApproachNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        public override string GetName()
        {
            return "LYJ";
        }
    }
}
