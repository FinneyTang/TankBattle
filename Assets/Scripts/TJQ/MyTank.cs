using Main;
using UnityEngine;
using UnityEngine.AI;

namespace TJQ
{
    class MyTank : Tank
    {
        private float m_LastTime = 0;
        protected override void OnUpdate()
        {
            base.OnUpdate();

            if(HP <= 50)
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
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if(oppTank != null)
            {
                bool seeOthers = false;
                RaycastHit hitInfo;
                if (Physics.Linecast(FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMaskCollsion))
                {
                    if(PhysicsUtils.IsFireCollider(hitInfo.collider))
                    {
                        seeOthers = true;
                    }
                }
                if(seeOthers)
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
