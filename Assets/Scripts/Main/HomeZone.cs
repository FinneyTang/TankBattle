using UnityEngine;

namespace Main
{
    internal class HomeZone : MonoBehaviour
    {
        public ETeam Team { set; get; } = ETeam.A;

        private float m_SqrRadius;

        private void Start()
        {
            m_SqrRadius = Match.instance.GlobalSetting.HomeZoneRadius * Match.instance.GlobalSetting.HomeZoneRadius;
        }

        private void Update()
        {
            var tanks = Match.instance.GetTanks(Team);
            if(tanks == null)
            {
                return;
            }
            foreach (var t in tanks)
            {
                Vector3 homeZonePos = Match.instance.GetRebornPos(Team);
                if((homeZonePos - t.Position).sqrMagnitude < m_SqrRadius)
                {
                    t.HPRecovery(Time.deltaTime * Match.instance.GlobalSetting.HPRecoverySpeed);
                }
                else
                {
                    t.HPRecovery(0);
                }
            }
        }
    }
}
