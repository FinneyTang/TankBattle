using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Main
{
    public class Tank : MonoBehaviour
    {
        protected NavMeshAgent m_Agent;
        public ETeam Team
        {
            get; internal set;
        }
        void Awake()
        {
            m_Agent = this.GetComponent<NavMeshAgent>();
            OnAwake();
        }
        void Start()
        {
            OnStart();
        }
        // Update is called once per frame
        void Update()
        {
            OnUpdate();
        }
        private void OnDrawGizmos()
        {
            if (m_Agent != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(m_Agent.destination, 0.5f);
                Gizmos.DrawLine(m_Agent.destination, gameObject.transform.position);
            }
            OnOnDrawGizmos();
        }
        protected virtual void OnAwake()
        {

        }
        protected virtual void OnStart()
        {

        }
        protected virtual void OnUpdate()
        {

        }
        protected virtual void OnOnDestroy()
        {

        }
        protected virtual void OnOnDrawGizmos()
        {

        }
    }
}

