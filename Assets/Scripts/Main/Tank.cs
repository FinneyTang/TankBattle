using System;
using AI.Base;
using AI.Blackboard;
using AI.SensorSystem;
using UnityEngine;
using UnityEngine.AI;

namespace Main
{
    public enum ETeamStrategy
    {
        None, Help, FocusFire, StaySafe, UserDefine
    }

    public enum TeamStrategyBBKey
    {
        HelpPos, AttackTarget
    }
    
    public abstract class Tank : MonoBehaviour, IAgent
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
        private float m_HPRecoveryFraction;
        private GameObject m_HPRecoveryEffectGO;
        private BlackboardMemory m_TeamStratedgyBB = new BlackboardMemory();
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
        public int Score
        {
            get
            {
                return m_Score;
            }
        }

        public int TeamStrategy
        {
            get;
            internal set;
        }
        public void TurretTurnTo(Vector3 targetPos)
        {
            m_TurretTargetPos = targetPos;
        }
        public bool CanSeeOthers(Vector3 pos)
        {
            bool seeOthers = false;
            if (Physics.Linecast(FirePos, pos, out var hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                {
                    FireCollider fc = hitInfo.collider.GetComponent<FireCollider>();
                    if (fc != null && fc.Owner != null)
                    {
                        if (fc.Owner.Team != Team)
                        {
                            seeOthers = true;
                        }
                    }
                }
            }
            return seeOthers;
        }
        public bool CanSeeOthers(Tank t)
        {
            if(t.IsDead)
            {
                return false;
            }
            return CanSeeOthers(t.Position);
        }
        public Vector3 TurretAiming
        {
            get
            {
                return m_TurretTF.forward;
            }
        }
        public bool Move(NavMeshPath path)
        {
            m_NavAgent.path = path;
            return true;
        }
        public bool Move(Vector3 targetPos)
        {
            NavMeshPath path = CaculatePath(targetPos);
            if (path != null)
            {
                Move(path);
                return true;
            }
            return false;
        }
        public bool Fire()
        {
            if (CanFire() == false)
            {
                return false;
            }
            m_NextFireTime = Time.time + Match.instance.GlobalSetting.FireInterval;
            Match.instance.AddMissile(this, FirePos, TurretAiming);
            return true;
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
        
        //----------------------------------------------
        //team stratedgy
        public void SendTeamStratedgy(int teamStrategy)
        {
            var teammates = Match.instance.GetTanks(Team);
            if (teammates.Count <= 1)
            {
                return; //no teammate
            }
            foreach (var t in teammates)
            {
                if (t == this)
                {
                    continue; //skip self
                }
                t.HandleSendTeamStratedgy(this, teamStrategy);
            }
        }
        public void SetTeamStratedgyParam(int bbKey, object value)
        {
            m_TeamStratedgyBB.SetValue(bbKey, value);
        }
        public T GetTeamStratedgyParam<T>(int bbKey)
        {
            return m_TeamStratedgyBB.GetValue<T>(bbKey);
        }
        private void HandleSendTeamStratedgy(Tank sender, int teamStrategy)
        {
            TeamStrategy = teamStrategy;
            OnHandleSendTeamStratedgy(sender, teamStrategy);
        }
        //----------------------------------------------
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

        private void Awake()
        {
            m_NavAgent = GetComponent<NavMeshAgent>();
            m_TurretTF = Find(transform, "Turret");
            m_FirePosTF = Find(transform, "FirePos");
            Transform hptf = Find(transform, "HPRecoveryEffect");
            m_HPRecoveryEffectGO = hptf.gameObject;
            m_HPRecoveryEffectGO.SetActive(false);
            Transform tf = Find(transform, "FireCollider");
            m_FireCollider = tf.GetComponent<FireCollider>();
            m_FireCollider.Owner = this;
            OnAwake();
        }

        private void Start()
        {
            OnStart();
            ReBorn();
        }
        // Update is called once per frame
        private void Update()
        {
            OnUpdate();
            UpdateTurretRotation();
        }
        internal void StimulusReceived(Stimulus stim)
        {
            OnStimulusReceived(stim);
        }
        internal void TakeDamage(Tank damager)
        {
            HP -= Match.instance.GlobalSetting.DamagePerHit;
            if(HP <= 0)
            {
                damager.AddScore(Match.instance.GlobalSetting.ScoreForKill);
                Dead();
            }
        }
        internal void HPRecovery(float v)
        {
            int maxHP = Match.instance.GlobalSetting.MaxHP;
            bool inHomeZone = v > Mathf.Epsilon && HP < maxHP && IsDead == false;
            if(inHomeZone)
            {
                m_HPRecoveryFraction += v;
                int addHP = (int)m_HPRecoveryFraction;
                if (addHP > 0)
                {
                    HP += addHP;
                    if (HP > maxHP)
                    {
                        HP = maxHP;
                    }
                    m_HPRecoveryFraction -= addHP;
                }
            }
            else
            {
                m_HPRecoveryFraction = 0;
            }
            if(m_HPRecoveryEffectGO.activeSelf != inHomeZone)
            {
                m_HPRecoveryEffectGO.SetActive(inHomeZone);
            }
        }
        internal void TakeStar(bool isSuperStar)
        {
            AddScore(isSuperStar ? 
                Match.instance.GlobalSetting.ScoreForSuperStar :
                Match.instance.GlobalSetting.ScoreForStar);
            var resName = string.Empty;
            switch (Team)
            {
                case ETeam.A:
                    resName = "CFX2_PickupSmileyA";
                    break;
                case ETeam.B:
                    resName = "CFX2_PickupSmileyB";
                    break;
                case ETeam.C:
                    resName = "CFX2_PickupSmileyC";
                    break;
                case ETeam.D:
                    resName = "CFX2_PickupSmileyD";
                    break;
            }
            if (string.IsNullOrEmpty(resName))
            {
                return;
            }
            Utils.PlayParticle(resName, Position);
        }
        internal string GetTankInfo(bool usingTeamScore = false)
        {
            string info;
            if (usingTeamScore)
            {
                info = Match.instance.IsMathEnd() == false ? $"{GetName()}\nHP: {HP}"
                    : $"{GetName()}";
            }
            else
            {
                info = Match.instance.IsMathEnd() == false ? $"{GetName()}\nHP: {HP}\nScore: {m_Score}"
                    : $"{GetName()}\nScore: {m_Score}";
            }
            if (IsDead && Match.instance.IsMathEnd() == false)
            {
                float rebornCD = GetRebornCD(Time.time);
                info += $"\nReborning: {rebornCD:f3}";
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
            transform.position = Match.instance.GetRebornPos(Team);
            transform.forward = (Vector3.zero - transform.position).normalized;
            m_TurretTF.forward = transform.forward;
            m_TurretTargetPos = Vector3.zero;//m_TurretTF.position + m_TurretTF.forward;
            gameObject.SetActive(true);
            Utils.PlayParticle("CFX3_MagicAura_B_Runic", Position);

            OnReborn();
        }
        internal bool IsInFireCollider(Vector3 pos)
        {
            return m_FireCollider.Col.bounds.Contains(pos);
        }
        private void OnDrawGizmos()
        {
            if (m_NavAgent != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(NextDestination, 0.5f);
                Gizmos.DrawLine(Position, NextDestination);

                Gizmos.color = Color.blue;
                Vector3 aimTarget = m_TurretTargetPos;
                aimTarget.y = FirePos.y;
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
            HP = 0;
            Utils.PlayParticle("CFX_Explosion_B_Smoke", Position);
            gameObject.SetActive(false);
            if(m_RebornTimer == null)
            {
                m_RebornTimer = new Timer();
            }
            m_RebornTimer.SetExpiredTime(Time.time + Match.instance.GlobalSetting.RebonCD);
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
        protected virtual void OnOnDrawGizmos()
        {

        }
        protected virtual void OnReborn()
        {

        }
        protected virtual void OnStimulusReceived(Stimulus stim)
        {

        }
        protected virtual void OnHandleSendTeamStratedgy(Tank sender, int teamStrategy)
        {

        }
    }
}

