using UnityEngine;

namespace Main
{
    public class Star : MonoBehaviour
    {
        private static int IDGen = 0;
        private static int GetNextID()
        {
            return IDGen++;
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

        private bool m_Taken = false;
        internal void Init(Vector3 pos)
        {
            ID = GetNextID();
            transform.position = pos;
            m_Taken = false;
        }
        void OnTriggerEnter(Collider other)
        {
            if(PhysicsUtils.IsFireCollider(other) == false)
            {
                return;
            }
            if(m_Taken == true)
            {
                return;
            }
            m_Taken = true;
            FireCollider fc = other.GetComponent<FireCollider>();
            if (fc != null && fc.Owner != null)
            {
                fc.Owner.TakeStar();
                Match.instance.RemoveStar(this);
            }
        }
    }
}
