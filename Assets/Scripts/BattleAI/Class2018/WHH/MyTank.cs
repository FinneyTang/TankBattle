using Main;
using AI.ScriptBased;
using UnityEngine;

namespace WHH
{
    class Condition
    {
        public virtual bool IsTrue(Tank owner)
        {
            return false;
        }
    }

    class TrueCondition : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            return true;
        }
    }

    class FalseCondition : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            return false;
        }
    }

    class AndCondition : Condition
    {
        private Condition m_LHS;
        private Condition m_RHS;
        public AndCondition(Condition lhs, Condition rhs)
        {
            m_LHS = lhs;
            m_RHS = rhs;
        }

        public override bool IsTrue(Tank owner)
        {
            return m_LHS.IsTrue(owner) && m_RHS.IsTrue(owner);
        }
    }

    class OrCondition : Condition
    {
        private Condition m_LHS;
        private Condition m_RHS;
        public OrCondition(Condition lhs, Condition rhs)
        {
            m_LHS = lhs;
            m_RHS = rhs;
        }

        public override bool IsTrue(Tank owner)
        {
            return m_LHS.IsTrue(owner) || m_RHS.IsTrue(owner);
        }
    }

    class XorCondition : Condition
    {
        private Condition m_LHS;
        private Condition m_RHS;
        public XorCondition(Condition lhs, Condition rhs)
        {
            m_LHS = lhs;
            m_RHS = rhs;
        }

        public override bool IsTrue(Tank owner)
        {
            return m_LHS.IsTrue(owner) ^ m_RHS.IsTrue(owner);
        }
    }

    class NotCondition : Condition
    {
        private Condition m_LHS;
        public NotCondition(Condition lhs)
        {
            m_LHS = lhs;
        }

        public override bool IsTrue(Tank owner)
        {
            return !m_LHS.IsTrue(owner);
        }
    }

    class HPBelow : Condition
    {
        private int m_TargetHP;
        public HPBelow(int targetHP)
        {
            m_TargetHP = targetHP;
        }
        public override bool IsTrue(Tank owner)
        {
            return owner.HP <= m_TargetHP;
        }
    }

    class HasSuperStar : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value.IsSuperStar)
                {
                    return true;
                }
            }
            return false;
        }
    }

    class HasSeenEnemy : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            Tank oppTank = Match.instance.GetOppositeTank(owner.Team);
            if (oppTank == null)
            {
                return false;
            }
            bool seeOthers = false;
            RaycastHit hitInfo;
            if (Physics.Linecast(owner.FirePos, oppTank.Position, out hitInfo))
            {
                if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                {
                    seeOthers = true;
                }
            }
            return seeOthers;
        }
    }

    public class MyTank : Tank
    {

        public override string GetName()
        {
            return "WHH";
        }

        private float m_LastTime = 0;
        private Condition m_BackToHome;
        private Condition m_Fire;
        private Condition m_GetSuperStar;

        protected override void OnStart()
        {
            base.OnStart();
            m_GetSuperStar = new HasSuperStar();
            m_BackToHome = new AndCondition(new HPBelow(50), new AndCondition(new NotCondition(new HasSuperStar()), new NotCondition(new HasSeenEnemy())));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            bool hasStar = false;
            bool seeOthers = false;

            Vector3 nearestOppTankPos = Vector3.zero;
            Vector3 nearestStarPos = Vector3.zero;
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Fire();
            if (oppTank != null)
            {

                RaycastHit hitInfo;
                if (Physics.Linecast(FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMaskCollsion))
                {
                    if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                    {
                        seeOthers = true;
                    }
                    else
                    {
                        seeOthers = false;
                    }
                }
                if (seeOthers)
                {
                    TurretTurnTo(oppTank.Position);
                    Vector3 toTarget = oppTank.Position + oppTank.Forward * Velocity.magnitude * Time.deltaTime - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    if (Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                    {
                        Fire();
                    }
                }
                else
                {
                    nearestOppTankPos = oppTank.Position;
                }
            }
            if (oppTank == null && hasStar == false)
            {
                Move(new Vector3(0, 0, 0));
            }
            else if (oppTank == null && hasStar)
            {
                Move(nearestStarPos);
            }
            else if (oppTank != null && hasStar == false)
            {
                Move(nearestOppTankPos);
            }
            else if (oppTank != null && hasStar)
            {
                if (HP > 50)
                {
                    float disToStar = Vector3.Distance(transform.position, nearestStarPos);
                    float disOppTankToStar = Vector3.Distance(nearestStarPos, nearestOppTankPos);
                    if (disToStar <= disOppTankToStar)
                    {
                        Move(nearestStarPos);
                    }
                    if (disToStar > disOppTankToStar)
                    {
                        Move(nearestOppTankPos);
                    }
                }
                else if (HP > 25)
                {
                    Move(nearestStarPos);
                }
                else if (HP > 0)
                {
                    float disToStar = Vector3.Distance(transform.position, nearestStarPos);
                    float rangeToEatStar = Random.Range(5, 8);
                    if (disToStar <= rangeToEatStar)
                    {
                        Move(nearestStarPos);
                    }
                    else
                        Move(Match.instance.GetRebornPos(Team));
                }
            }
            if (m_GetSuperStar.IsTrue(this))
            {
                Move(Vector3.zero);
            }
            else if (m_BackToHome.IsTrue(this))
            {
                Move(Match.instance.GetRebornPos(Team));
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
    }
}  
