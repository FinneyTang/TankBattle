using AI.Base;
using AI.RuleBased;
using Main;
using UnityEngine;

namespace ZQY
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
    class HPMax : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return t.HP == Match.instance.GlobalSetting.MaxHP;
        }
    }
    class HasStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            foreach (var pair in Match.instance.GetStars())
            {
                if (!pair.Value.IsSuperStar)
                {
                    return true;
                }
            }
            return false;
        }
    }
    class TimeApproachingHalf : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            if (Match.instance.RemainingTime < (Match.instance.GlobalSetting.MatchTime / 2 + 2) && Match.instance.RemainingTime>(Match.instance.GlobalSetting.MatchTime / 2 - 2))
            {
                return true;
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

        private Condition m_BackToHome;
        private Condition m_Fire;
        private Condition m_GetSuperStar;
        private Condition m_GetStar;
        private Condition m_HPMax;

        private bool CanOut;
        protected override void OnStart()
        {
            base.OnStart();
            CanOut = true;
            m_Fire = new HasSeenEnemy();
            m_GetSuperStar = new TimeApproachingHalf();
            m_BackToHome = new AndCondition(
                new HPBelow(60),
                new NotCondition(new TimeApproachingHalf()));
            m_GetStar = new AndCondition(
                new HasStar(),
                new NotCondition(new TimeApproachingHalf()));
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            m_HPMax = new HPMax();
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                TurretTurnTo(oppTank.Position + 1.5f * oppTank.Forward);
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            if (m_Fire.IsTrue(this) && Vector3.Dot(TurretAiming, (oppTank.Position - Position).normalized) > 0.99f)
            {
                Fire();
            }
            if (CanOut)
            {
                if (m_GetStar.IsTrue(this))
                {
 
                    Move(getNearestStar().Position);
                }
                else
                {
                    if (!m_HPMax.IsTrue(this))
                    {
                        Move(Match.instance.GetRebornPos(Team));
                    }
                    else
                        Move(Vector3.zero);
                }
            }

            if (m_GetSuperStar.IsTrue(this))
            {
                Move(Vector3.zero);
            }
            if (m_BackToHome.IsTrue(this))
            {
                CanOut = false;
                if (m_GetStar.IsTrue(this) && (getNearestStar().Position - Position).sqrMagnitude < (Match.instance.GetRebornPos(Team) - Position).sqrMagnitude)
                {
                    Move(getNearestStar().Position);
                }
                else
                {
                    Move(Match.instance.GetRebornPos(Team));
                }
               
            }
            else if(m_HPMax.IsTrue(this))
            {
                CanOut = true;
            }

        }
        public Star getNearestStar()
        {
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - Match.instance.GetTank(Team).Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestStar = s;
                    }
                }
            }
            return nearestStar;
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            CanOut = true;
        }
        public override string GetName()
        {
            return "ZQY";
        }
    }
}
