using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Main
{
    abstract public class Tank : MonoBehaviour
    {
        private Transform m_TurretTF;
        private Transform m_FirePosTF;
        private Vector3 m_TurretTargetPos;
        private NavMeshAgent m_NavAgent;
        public ETeam Team
        {
            get; internal set;
        }
        public void TurretTurnTo(int ownerID, Vector3 targetPos)
        {
            if(CheckOwner(ownerID) == false)
            {
                return;
            }
            m_TurretTargetPos = targetPos;
        }
        public Vector3 GetTurretAiming()
        {
            return m_TurretTF.forward;
        }
        public void Move(int ownerID, NavMeshPath path)
        {
            if (CheckOwner(ownerID) == false)
            {
                return;
            }
            m_NavAgent.path = path;
        }
        public bool Move(int ownerID, Vector3 targetPos)
        {
            if (CheckOwner(ownerID) == false)
            {
                return false;
            }
            NavMeshPath path = CaculatePath(targetPos);
            if (path != null)
            {
                Move(GetID(), path);
                return true;
            }
            return false;
        }
        public NavMeshPath CaculatePath(Vector3 targetPos)
        {
            NavMeshPath path = new NavMeshPath();
            if(m_NavAgent.CalculatePath(targetPos, path))
            {
                return path;
            }
            return null;
        }
        public Vector3 NextDestination
        {
            get
            {
                return m_NavAgent.destination;
            }
        }
        public Vector3 Velocity
        {
            get
            {
                return m_NavAgent.velocity;
            }
        }
        public Vector3 Position
        {
            get
            {
                return transform.position;
            }
        }
        public Vector3 FirePos
        {
            get
            {
                return m_FirePosTF.position;
            }
        }
        public Vector3 Forward
        {
            get
            {
                return transform.forward;
            }
        }
        public abstract int GetID();
        void Awake()
        {
            m_NavAgent = GetComponent<NavMeshAgent>();
            m_TurretTF = Find(transform, "Turret");
            m_FirePosTF = Find(transform, "FirePos");
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
            if (m_NavAgent != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(NextDestination, 0.5f);
                Gizmos.DrawLine(Position, NextDestination);

                Gizmos.color = Color.blue;
                Vector3 aimTarget = m_TurretTargetPos;
                aimTarget.y = m_FirePosTF.position.y;
                Gizmos.DrawLine(m_FirePosTF.position, aimTarget);
            }
            OnOnDrawGizmos();
        }
        private bool CheckOwner(int tankID)
        {
            return GetID() == Match.instance.TeamSettings[(int)Team].TankID;
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
        private Transform Find(Transform root, string name)
        {
            for(int i = 0; i < root.childCount; ++i)
            {
                Transform t = root.GetChild(i);
                if(t.name == name)
                {
                    return t;
                }
                t = Find(t, name);
                if(t != null)
                {
                    return t;
                }
            }
            return null;
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

