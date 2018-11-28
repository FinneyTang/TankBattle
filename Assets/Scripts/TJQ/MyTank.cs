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
                    m_LastTime = Time.time + 5;
                }
            }
        }
        private bool ApproachNextDestination()
        {
            NavMeshPath path = new NavMeshPath();
            if (NavAgent.CalculatePath(new Vector3(Random.Range(-50, 50), 0, Random.Range(-50, 50)), path))
            {
                NavAgent.path = path;
                TurretTurnTo(NavAgent.destination);
                return true;
            }
            return false;
        }
    }
}
