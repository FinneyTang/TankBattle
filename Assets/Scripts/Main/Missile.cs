using UnityEngine;

namespace Main
{
    public class Missile : MonoBehaviour
    {
        public ETeam Team
        {
            get; private set;
        }
        private Vector3 m_InitVelocity;
        internal void Init(ETeam team, Vector3 initPos, Vector3 initVelocity)
        {
            Team = team;
            m_InitVelocity = initVelocity;
            transform.position = initPos;
        }
        void Update()
        {
            Vector3 newPos = transform.position + m_InitVelocity * Time.deltaTime;
            RaycastHit hitInfo;
            if (Physics.Linecast(transform.position, newPos, out hitInfo, PhysicsUtils.LayerMask_Collsion))
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
                            fc.Owner.TakeDamage();
                        }
                        else
                        {
                            hitOwner = true;
                        }
                    }
                }
                if(hitOwner == false)
                {
                    GameObject explosion = (GameObject)Instantiate(Resources.Load("CFX3_Hit_SmokePuff"));
                    explosion.transform.position = transform.position;
                    Destroy(this.gameObject);
                }
            }
            else
            {
                transform.position = newPos;
            }
        }
    }
}
