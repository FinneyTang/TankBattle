using Main;
using UnityEngine;
using AI.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LXK
{
    class Threat : UtilityBase
    {
        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppositeTank = Match.instance.GetOppositeTank(t.Team);
            if (oppositeTank == null || oppositeTank.IsDead)
            {
                return 0.0f;
            }
            if (t.CanSeeOthers(oppositeTank))
            {
                return 1.0f;
            }
            return 0.0f;
        }
    }

    class Safety : UtilityBase
    {
        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppositeTank = Match.instance.GetOppositeTank(t.Team);
            if (oppositeTank.IsDead&&t.HP!=Match.instance.GlobalSetting.MaxHP)
                return 0.0f;
            return ((float)t.HP) / Match.instance.GlobalSetting.MaxHP;
        }
    }

    class Arrogancy : UtilityBase
    {
        protected override float OnCalcU(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppositeTank = Match.instance.GetOppositeTank(t.Team);
            if ((t.Score - oppositeTank.Score) >= Match.instance.GlobalSetting.ScoreForSuperStar && Match.instance.RemainingTime < (Match.instance.GlobalSetting.StarAddInterval*4))
            {
                return 1.0f;
            }
            foreach (var pair in Match.instance.GetStars())
            {
                if (Vector3.Distance(pair.Value.Position, t.Position) < 25.0f)
                {
                    return 0.0f;
                }
                if(Vector3.Distance(pair.Value.Position, t.Position) < 35.0f && pair.Value.IsSuperStar)
                {
                    return 0.0f;
                }
            }
            if (oppositeTank.IsDead && t.HP < Match.instance.GlobalSetting.MaxHP)
            {
                return 0.7f;
            }
            return 0.5f;
        }
    }

    public class MyTank : Tank
    {
        enum EAction
        {
            None = -1,Fire = 0, GetStar, Heal
        }
        private float m_LastTime = 0.0f;
        private UtilityBase fire;
        private UtilityBase getStar;
        private UtilityBase heal;
        private Selector selector;
        private Vector3 nearestStarPos;
        protected override void OnStart()
        {
            base.OnStart();

            fire = new Threat();
            getStar = new OneMinusScore(new Arrogancy());
            heal = new OneMinusScore(new Safety());
            selector = new MaxSelector();
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            
            Tank oppositeTank = Match.instance.GetOppositeTank(Team);
            if (oppositeTank == null || oppositeTank.IsDead)
            {
                TurretTurnTo(Match.instance.GetRebornPos(oppositeTank.Team));
            }
            else
            {
                TurretTurnTo(oppositeTank.Position + Vector3.Normalize(oppositeTank.Velocity)*4.0f);
            }
            EAction action = (EAction)UtilitySelector.Select(this, selector, fire, getStar,heal);
            Debug.Log(action);
            switch (action)
            {
                case EAction.Fire:
                    Fire();
                    Move(Position+ Vector3.Normalize(nearestStarPos - Position)*2.0f);
                    break;
                case EAction.Heal:
                    Move(Match.instance.GetRebornPos(Team));
                    break;
                case EAction.GetStar:
                    bool hasStar = false;
                    float nearestDist = float.MaxValue;
                    nearestStarPos = Vector3.zero;
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
                        //Debug.Log(Vector3.Distance(nearestStarPos,Position));
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
            float halfSize = Match.instance.FieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0.0f;
        }

#if UNITY_EDITOR
        private GUIStyle m_ScoreStyle;
#endif
        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
#if UNITY_EDITOR
            if (m_ScoreStyle == null)
            {
                m_ScoreStyle = new GUIStyle();
                m_ScoreStyle.normal.textColor = Color.yellow;
                m_ScoreStyle.fontSize = 16;
                m_ScoreStyle.fontStyle = FontStyle.Bold;
            }
            string score = string.Format("{0}: {1}\n{2}: {3}\n{4}: {5}\n ",
                EAction.Fire, fire.GetBaseScore().ToString("f2"),
                EAction.GetStar, getStar.GetBaseScore().ToString("f2"),
                EAction.Heal, heal.GetBaseScore().ToString("f2"));
            Handles.Label(Position + Forward * 5, score, m_ScoreStyle);
#endif
        }
        public override string GetName()
        {
            return "LXK";
        }
    }
}
