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
        public bool IsSuperStar
        {
            get
            {
                return m_IsSuperStar;
            }
        }
        private bool m_Taken = false;
        private bool m_IsSuperStar = false;
        internal void Init(Vector3 pos, bool isSuperStar)
        {
            ID = GetNextID();
            transform.position = pos;
            m_Taken = false;
            m_IsSuperStar = isSuperStar;
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
                fc.Owner.TakeStar(m_IsSuperStar);
                Match.instance.RemoveStar(this);
            }
        }
    }
}
