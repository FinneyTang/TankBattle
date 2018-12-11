using AI.SensorSystem;
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
        private float m_NextJingleTime;
        internal void Init(Vector3 pos, bool isSuperStar)
        {
            ID = GetNextID();
            transform.position = pos;
            m_Taken = false;
            m_IsSuperStar = isSuperStar;
        }
        public void Update()
        {
            if(Time.time >= m_NextJingleTime)
            {
                Match.instance.SendStim(
                    Stimulus.CreateStimulus((int)EStimulusType.StarJingle, ESensorType.Hearing, Position, this, IsSuperStar ? 1f : 0.5f));
                m_NextJingleTime = Time.time + 1f;
            }
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
                Match.instance.SendStim(
                    Stimulus.CreateStimulus((int)EStimulusType.StarTaken, ESensorType.Hearing, Position, this));
                Match.instance.RemoveStar(this);
            }
        }
    }
}
