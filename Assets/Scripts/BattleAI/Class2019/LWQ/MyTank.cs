using AI.Base;
using Main;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LWQ
{
    public class Condition
    {
        public virtual bool IsTrue(Tank tank)
        {
            return false;
        }
    }

    class TrueCondition : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            return true;
        }
    }

    class FalseCondition : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            return false;
        }
    }

    class AndCondition:Condition {
        private Condition m_Left;
        private Condition m_Right;
        public AndCondition(Condition left, Condition right)
        {
            m_Left = left;
            m_Right = right;
        }

        public override bool IsTrue(Tank tank)
        {
            return m_Left.IsTrue(tank) && m_Right.IsTrue(tank);
        }
    }

    class OrCondition : Condition
    {
        private Condition m_Left;
        private Condition m_Right;
        public OrCondition(Condition left,Condition right)
        {
            m_Left = left;
            m_Right = right;

        }
        public override bool IsTrue(Tank tank)
        {
            return m_Left.IsTrue(tank) || m_Right.IsTrue(tank);
        }
    }

    class XorCondition : Condition
    {
        private Condition m_Left;
        private Condition m_Right;
        public XorCondition(Condition left,Condition right)
        {
            m_Left = left;
            m_Right = right;
        }

        public override bool IsTrue(Tank tank)
        {
            return m_Left.IsTrue(tank)^m_Right.IsTrue(tank);
        }
    }

    class NotCondition : Condition
    {
        private Condition m_lhs;
        public NotCondition(Condition lhs)
        {
            m_lhs = lhs;
        }
        public override bool IsTrue(Tank tank)
        {
            return !m_lhs.IsTrue(tank);
        }
    }

    class HPBelow : Condition
    {
        float m_targetHP;
        public HPBelow(float targetHP)
        {
            m_targetHP = targetHP;
        }
        public override bool IsTrue(Tank tank)
        {
            return tank.HP<=m_targetHP;
        }
    }

    class HasSuperStar : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            foreach(var star in Match.instance.GetStars())
            {
                if (star.Value.IsSuperStar)
                {
                    return true;
                }
            }
            return false;
        }
    }

    class IsStarNearest : Condition
    {
        private float starDis;
        private float oppTankDis;
        public IsStarNearest(float star,float tank)
        {
            starDis = star;
            oppTankDis = tank;
        }

        public override bool IsTrue(Tank tank)
        {
            return (oppTankDis - starDis > 6);
        }
    }

    class HasSeenEnemy : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            Tank oppTank = Match.instance.GetOppositeTank(tank.Team);
            if (oppTank == null)
            {
                return false;
            }
            else
            {
                return tank.CanSeeOthers(oppTank);
            }
        }
    }

    class HasOppDead : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            Tank oppTank = Match.instance.GetOppositeTank(tank.Team);
            if (oppTank.IsDead)
            {
                return true;
            }
            else return false;
        }
    }

    class HasStar : Condition
    {
        public override bool IsTrue(Tank tank)
        {
            return Match.instance.GetStars().Count > 0;
        }
    }


    public class MyTank : Tank
    {
        private Condition m_getSuperStar;
        private Condition m_fire;
        private Condition m_backToHome;
        private Condition m_oppTankDead;
        private Condition m_getStar;
        private Condition m_getNearest;
        private bool m_getSatrOnWay;
           
        private float m_LastTime = 0;

        public override string GetName()
        {
            return "LWQ";
        }

        protected override void OnStart()
        {
            base.OnStart();
            m_getSuperStar = new HasSuperStar();
            m_fire =new HasSeenEnemy();
            m_backToHome = new AndCondition(
                new NotCondition(new HasSuperStar()),
                new AndCondition(
                    new NotCondition(new HasOppDead()),
                    new HPBelow(50)
                    ));
            m_oppTankDead = new HasOppDead();
            m_getStar =new AndCondition(new HasStar(),new NotCondition(new HasSuperStar()));
            m_getSatrOnWay = false;

    }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Tank oppTank = Match.instance.GetOppositeTank(this.Team);

            //炮管移动
            if (m_oppTankDead.IsTrue(this))
            {
                TurretTurnTo(Match.instance.GetRebornPos(oppTank.Team));            
            }
            else
            {
                TurretTurnTo(oppTank.Position);
            }

            //开火
            if (m_fire.IsTrue(this))
            {
                this.Fire();
            }

            if (m_getSuperStar.IsTrue(this))
            {
                Move(Vector3.zero);
            }
           else  if (m_backToHome.IsTrue(this))
            {
                Vector3 starPos = GetNearestStar();
                float disToStar = Vector3.Distance(this.Position, starPos);
                float disToOppTank = Vector3.Distance(this.Position, oppTank.Position);
                m_getNearest = new IsStarNearest(disToStar, disToOppTank);
                if (m_getNearest.IsTrue(this) && !m_getSatrOnWay)
                {
                    Move(starPos);
                    m_getSatrOnWay = true;
                }
                else {
                    Move(Match.instance.GetRebornPos(this.Team));
                    m_getSatrOnWay = false;
                }
            }
            else if (m_getStar.IsTrue(this))
            {               
                Move(GetNearestStar());
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

        public Vector3 GetNearestStar()
        {
            Vector3 nearestStarPos = Vector3.zero;
            float nearestDist = float.MaxValue;
                foreach (var pair in Match.instance.GetStars())
                {
                    Star s = pair.Value;
                    if (!s.IsSuperStar)
                    {
                        float dist = (s.Position - this.Position).sqrMagnitude;
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestStarPos = s.Position;
                        }
                    }
                }
            return nearestStarPos;
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

    }
}
