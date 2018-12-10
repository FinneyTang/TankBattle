using AI.Base;
using AI.RuleBased;
using Main;
using UnityEngine;

namespace RuleBasedAI
{
    class HPBelow : Condition
    {
        private int m_TargetHP;
        public HPBelow(int targetHP)
        {
            m_TargetHP = targetHP;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return t.HP <= m_TargetHP;
        }
    }
    class HasSuperStar : Condition
    {
        public override bool IsTrue(IAgent agent)
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
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null)
            {
                return false;
            }
            return t.CanSeeOthers(oppTank);
        }
    }
    class MyTank : Tank
    {
        private float m_LastTime = 0;

        //3 rules
        private Condition m_BackToHome;
        private Condition m_Fire;
        private Condition m_GetSuperStar;

        protected override void OnStart()
        {
            base.OnStart();
            m_Fire = new AndCondition(
                new HasSeenEnemy(),
                new NotCondition(new HPBelow(50)));
            m_GetSuperStar = new HasSuperStar();
            m_BackToHome = new AndCondition(
                new HPBelow(50),
                new NotCondition(new HasSuperStar()));
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null)
            {
                TurretTurnTo(oppTank.Position);
            }
            if (m_Fire.IsTrue(this))
            {
                Fire();
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
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
        public override string GetName()
        {
            return "RuleBasedAITank";
        }
    }
}
