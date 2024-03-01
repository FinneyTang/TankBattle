using System.Collections.Generic;
using Main;
using UnityEngine;

namespace TJQ
{
    class MyTank : Tank
    {
        private float m_LastTime = 0;
        private readonly List<Tank> m_CachedOppTanks = new List<Tank>();
        protected override void OnUpdate()
        {
            base.OnUpdate();

            if(HP <= 30)
            {
                Move(Match.instance.GetRebornPos(Team));
            }
            else
            {
                bool hasStar = false;
                float nearestDist = float.MaxValue;
                Vector3 nearestStarPos = Vector3.zero;
                foreach (var pair in Match.instance.GetStars())
                {
                    Star s = pair.Value;
                    if(s.IsSuperStar)
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
            var oppTanks = Match.instance.GetOppositeTanks(Team, m_CachedOppTanks);
            if(oppTanks != null && oppTanks.Count > 0)
            {
                var oppTank = GetBetterTarget(oppTanks);
                if(oppTank != null)
                {
                    TurretTurnTo(oppTank.Position);
                    Vector3 toTarget = oppTank.Position - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    if(Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                    {
                        Fire();
                    }
                }
                else
                {
                    TurretTurnTo(Position + Forward);
                }
            }
        }
        private Tank GetBetterTarget(List<Tank> tanks)
        {
            float minDist = float.MaxValue;
            Tank targetTank = null;
            foreach (var t in tanks)
            {
                if (t.IsDead)
                {
                    continue;
                }

                if (!CanSeeOthers(t))
                {
                    continue;
                }

                var dist = (Position - t.Position).sqrMagnitude;
                if (dist < minDist)
                {
                    targetTank = t;
                    minDist = dist;
                }
            }
            return targetTank;
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
            return "TJQ";
        }
    }
}
