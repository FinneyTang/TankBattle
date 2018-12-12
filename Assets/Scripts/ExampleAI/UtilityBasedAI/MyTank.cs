using AI.Base;
using AI.UtilityBased;
using Main;
using UnityEngine;

namespace UtilityBasedAI
{
    class EnemyThreatenScore : Utility
    {
        public override float CalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null || oppTank.IsDead)
            {
                return 0;
            }
            if(t.CanSeeOthers(oppTank) == false)
            {
                return 0;
            }
            return Mathf.Lerp(1f, 0f, Vector3.Distance(oppTank.Position, t.Position) / 50f);
        }
    }
    class StarScore : Utility
    {
        public override float CalcU(IAgent agent)
        {
            int maxStarCount = Match.instance.GlobalSetting.MaxStarCount;
            return Mathf.Clamp01((float)Match.instance.GetStars().Count / maxStarCount);
        }
    }
    class EagerToWinScore : Utility
    {
        public override float CalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.Score > t.Score)
            {
                return 1f;
            }
            return 0.5f;
        }
    }
    class HPScore : Utility
    {
        public override float CalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            if(t.HP > 50)
            {
                return 0f;
            }
            return Mathf.Lerp(1f, 0f, t.HP / Match.instance.GlobalSetting.MaxHP);
        }
    }

    class MyTank : Tank
    {
        enum EAction
        {
            None = -1, Fire = 0, GetStar, BackToHome
        }
        private float m_LastTime = 0;
        private Utility m_Fire;
        private Utility m_BackToHome;
        private Utility m_GetStar;
        private Selector m_MaxSelector;
        protected override void OnStart()
        {
            base.OnStart();
            m_MaxSelector = new MaxSelector();
            m_Fire = new EnemyThreatenScore();
            m_GetStar = new StarScore();
            m_BackToHome = new MultipleComposite().
                            AddUtility(new HPScore()).
                            AddUtility(new EagerToWinScore());
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();

            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                TurretTurnTo(oppTank.Position);
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            EAction action = (EAction)UtilitySelector.Select(this, m_MaxSelector, m_Fire, m_GetStar, m_BackToHome);
            switch(action)
            {
                case EAction.Fire:
                    Fire();
                    break;
                case EAction.BackToHome:
                    Move(Match.instance.GetRebornPos(Team));
                    break;
                case EAction.GetStar:
                    bool hasStar = false;
                    float nearestDist = float.MaxValue;
                    Vector3 nearestStarPos = Vector3.zero;
                    foreach (var pair in Match.instance.GetStars())
                    {
                        Star s = pair.Value;
                        if (s.IsSuperStar)
                        {
                            hasStar = true;
                            nearestStarPos = s.Position;
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
                    break;
                default:
                    if (Time.time > m_LastTime && ApproachNextDestination())
                    {
                        m_LastTime = Time.time + Random.Range(3, 8);
                    }
                    break;
            }
        }
        private bool ApproachNextDestination()
        {
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        public override string GetName()
        {
            return "UtilityBasedAITank";
        }
    }
}
