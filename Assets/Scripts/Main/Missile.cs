using UnityEngine;

namespace Main
{
    public class Missile : MonoBehaviour
    {
        private static int IDGen = 0;
        private static int GetNextID()
        {
            return IDGen++;
        }
        public ETeam Team
        {
            get
            {
                return m_Owner.Team;
            }
        }
        public int ID
        {
            get; private set;
        }
        public Vector3 Position
        {
            get
            {
                return transform.position;
            }
        }
        public Vector3 Velocity
        {
            get
            {
                return m_InitVelocity;
            }
        }
        private Tank m_Owner;
        private Vector3 m_InitVelocity;
        internal void Init(Tank owner, Vector3 initPos, Vector3 initVelocity)
        {
            ID = GetNextID();
            m_Owner = owner;
            m_InitVelocity = initVelocity;
            transform.position = initPos;
        }
        void Update()
        {
            Vector3 newPos = Position + m_InitVelocity * Time.deltaTime;
            RaycastHit hitInfo;
            if (Physics.Linecast(Position, newPos, out hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                bool hitOwner = false;
                if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                {
                    //hit player
                    FireCollider fc = hitInfo.collider.GetComponent<FireCollider>();
                    if(fc != null && fc.Owner != null)
                    {
                        if(fc.Owner.Team != Team)
                        {
                            fc.Owner.TakeDamage(m_Owner);
                        }
                        else
                        {
                            hitOwner = true;
                        }
                    }
                    Utils.PlayParticle("CFX3_Hit_SmokePuff", Position);
                }
                else
                {
                    Utils.PlayParticle("CFX3_Hit_SmokePuff_Wall", Position);
                }
                if(hitOwner == false)
                {
                    Match.instance.RemoveMissile(this);
                }
            }
            else
            {
                transform.position = newPos;
            }
        }
    }
}
