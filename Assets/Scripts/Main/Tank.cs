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
        private int m_ID;
        private Timer m_RebornTimer;
        private int m_Score;
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
        public Vector3 TurretAiming
        {
            get
            {
                return m_TurretTF.forward;
            }
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
            Match.instance.AddMissile(this, FirePos, TurretAiming);
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
        public abstract string GetName();
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
            ReBorn();
        }
        // Update is called once per frame
        void Update()
        {
            OnUpdate();
            UpdateTurretRotation();
        }
        internal void TakeDamage(Tank damager)
        {
            HP -= Match.instance.GlobalSetting.DamagePerHit;
            if(HP == 0)
            {
                damager.AddScore(Match.instance.GlobalSetting.ScoreForKill);
                Dead();
            }
        }
        internal string GetTankInfo()
        {
            string info = string.Format("{0}\nHP: {1}\nScore: {2}", GetName(), HP, m_Score);
            if (IsDead)
            {
                float rebornCD = GetRebornCD(Time.time);
                info += string.Format("\nWaiting For Reborn: {0}", rebornCD.ToString("f3"));
            }
            return info;
        }
        internal float GetRebornCD(float gameTime)
        {
            if (m_RebornTimer == null)
            {
                return 0;
            }
            return m_RebornTimer.GetRemaingTime(gameTime);
        }
        internal bool CanReborn(float gameTime)
        {
            if(m_RebornTimer == null)
            {
                return false;
            }
            return m_RebornTimer.IsExpired(gameTime);
        }
        internal void ReBorn()
        {
            HP = Match.instance.GlobalSetting.MaxHP;
            transform.position = Match.instance.TeamSettings[(int)Team].Reborn.transform.position;
            gameObject.SetActive(true);
            Utils.PlayParticle("CFX3_MagicAura_B_Runic", Position);
            OnBorn();
        }
        protected int GetID()
        {
            return GetName().GetHashCode();
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
        private void AddScore(int score)
        {
            m_Score += score;
        }
        private void Dead()
        {
            Utils.PlayParticle("CFX_Explosion_B_Smoke", Position);
            gameObject.SetActive(false);
            if(m_RebornTimer == null)
            {
                m_RebornTimer = new Timer();
            }
            m_RebornTimer.SetExpiredTime(Time.time + Match.instance.GlobalSetting.RebonCD);
        }
        private bool CheckOwner(int tankID)
        {
            return GetID() == Match.instance.TeamSettings[(int)Team].TankName.GetHashCode();
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

