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
            if (Time.time > m_LastTime)
            {
                if (ApproachNextDestination())
                {
                    m_LastTime = Time.time + Random.Range(3, 8);
                }
            }
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if(oppTank != null)
            {
                bool seeOthers = false;
                RaycastHit hitInfo;
                if (Physics.Linecast(FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMask_Collsion))
                {
                    if(PhysicsUtils.IsFireCollider(hitInfo.collider))
                    {
                        seeOthers = true;
                    }
                }
                if(seeOthers)
                {
                    TurretTurnTo(GetID(), oppTank.Position);
                    Vector3 aimDirection = GetTurretAiming();
                    Vector3 toTarget = oppTank.Position - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    if(Vector3.Dot(aimDirection, toTarget) > 0.98f)
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
        protected override void OnBorn()
        {
            base.OnBorn();
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()
        {
            return Move(GetID(), new Vector3(Random.Range(-50, 50), 0, Random.Range(-50, 50)));
        }
        public override int GetID()
        {
            return 1;
        }
    }
}
