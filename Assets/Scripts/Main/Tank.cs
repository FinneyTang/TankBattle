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
        private FireCollider m_FireCollider;
        private float m_NextFireTime;
        public ETeam Team
        {
            get; internal set;
        }
        public int HP
        {
            get; internal set;
        }
        public bool IsDead
        {
            get
            {
                return gameObject.activeSelf == false;
            }
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
        public void Fire(int ownerID)
        {
            if (CheckOwner(ownerID) == false)
            {
                return;
            }
            if (CanFire() == false)
            {
                return;
            }
            m_NextFireTime = Time.time + Match.instance.GlobalSetting.FireInterval;
            GameObject missileGO = (GameObject)Instantiate(Resources.Load("Missile"));
            Missile missile = missileGO.GetComponent<Missile>();
            missile.Init(Team, FirePos, GetTurretAiming() * Match.instance.GlobalSetting.MissileSpeed);
        }
        public bool CanFire()
        {
            return Time.time > m_NextFireTime;
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
            Transform tf = Find(transform, "FireCollider");
            m_FireCollider = tf.GetComponent<FireCollider>();
            m_FireCollider.Owner = this;
            m_TurretTargetPos = m_TurretTF.position + m_TurretTF.forward;
            OnAwake();
        }
        void Start()
        {
            OnStart();
            Born();
        }
        // Update is called once per frame
        void Update()
        {
            OnUpdate();
            UpdateTurretRotation();
        }
        internal void TakeDamage()
        {
            HP -= Match.instance.GlobalSetting.DamagePerHit;
            if(HP == 0)
            {
                Dead();
            }
        }
        internal void Born()
        {
            HP = Match.instance.GlobalSetting.MaxHP;
            transform.position = Match.instance.TeamSettings[(int)Team].Reborn.transform.position;
            gameObject.SetActive(true);
            GameObject boneEffect = (GameObject)Instantiate(Resources.Load("CFX3_MagicAura_B_Runic"));
            boneEffect.transform.position = transform.position;
            OnBorn();
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
        private void Dead()
        {
            GameObject explosion = (GameObject)Instantiate(Resources.Load("CFX_Explosion_B_Smoke+Text"));
            explosion.transform.position = transform.position;
            gameObject.SetActive(false);
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
        protected virtual void OnBorn()
        {

        }
    }
}

