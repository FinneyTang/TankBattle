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

            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach(var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                float dist = (s.transform.position - Position).sqrMagnitude;
                if(dist < nearestDist)
                {
                    hasStar = true;
                    nearestDist = dist;
                    nearestStarPos = s.transform.position;
                }
            }
            if (hasStar == true)
            {
                Move(GetID(), nearestStarPos);
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
                    TurretTurnTo(GetID(), oppTank.Position);
                    Vector3 toTarget = oppTank.Position - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    if(Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                    {
                        Fire(GetID());
                    }
                }
                else
                {
                    TurretTurnTo(GetID(), Position + Forward);
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
            return Move(GetID(), new Vector3(Random.Range(-50, 50), 0, Random.Range(-50, 50)));
        }
        public override string GetName()
        {
            return "TJQ";
        }
    }
}
