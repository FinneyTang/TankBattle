using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Main
{
    public class Tank : MonoBehaviour
    {
        private Transform m_TurretTF;
        private Transform m_FirePosTF;
        private Vector3 m_TurretTargetPos;
        public NavMeshAgent NavAgent
        {
            get; private set;
        }
        public ETeam Team
        {
            get; internal set;
        }
        public void TurretTurnTo(Vector3 targetPos)
        {
            m_TurretTargetPos = targetPos;
        }
        public Vector3 GetTurretAiming()
        {
            return m_TurretTF.forward;
        }
        void Awake()
        {
            NavAgent = GetComponent<NavMeshAgent>();
            m_TurretTF = transform.Find("Turret");
            m_FirePosTF = transform.Find("FirePos");
            m_TurretTargetPos = m_TurretTF.position + m_TurretTF.forward;
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
            UpdateTurretRotation();
        }
        private void OnDrawGizmos()
        {
            if (NavAgent != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(NavAgent.destination, 0.5f);
                Gizmos.DrawLine(NavAgent.destination, gameObject.transform.position);
            }
            OnOnDrawGizmos();
        }
        private void UpdateTurretRotation()
        {
            Vector3 toTarget = m_TurretTargetPos - transform.position;
            toTarget.y = 0;
            if(toTarget.sqrMagnitude > 0.0001f)
            {
                m_TurretTF.forward = Vector3.RotateTowards(
                    m_TurretTF.forward, toTarget.normalized, Time.deltaTime * Mathf.Deg2Rad * 180f, 1);
            }
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

