using System;
using System.Collections.Generic;
using UnityEngine;

namespace Main
{
    public enum ETeam
    {
        A, B, NB
    }
    public class Match : MonoBehaviour
    {
        public static Match instance = null;

        [Serializable]
        public class TeamSetting
        {
            public GameObject Reborn;
            public string TankScript;
            public string TankName;
        }
        public List<TeamSetting> TeamSettings;

        [Serializable]
        public class MatchSetting
        {
            public int MatchTime = 180;
            public float FireInterval = 1f;
            public float MissileSpeed = 40f;
            public int MaxHP = 100;
            public float RebonCD = 10f;
            public int DamagePerHit = 25;
            public int ScoreForStar = 5;
            public int ScoreForKill = 10;
        }
        public MatchSetting GlobalSetting = new MatchSetting();

        private List<Tank> m_Tanks;
        private List<Dictionary<int, Missile>> m_Missiles;

        private float m_RemainingTime = 0;
        void Awake()
        {
            Application.targetFrameRate = 60;
            Match.instance = this;
        }
        void Start()
        {
            if(TeamSettings.Count < 1)
            {
                Debug.LogError("must have 1 team settings");
                return;
            }
            m_Tanks = new List<Tank>();
            m_Missiles = new List<Dictionary<int, Missile>>();
            AddTank(ETeam.A);
            if(TeamSettings.Count > 1)
            {
                AddTank(ETeam.B);
            }
            m_RemainingTime = GlobalSetting.MatchTime;
        }
        public Tank GetOppositeTank(ETeam myTeam)
        {
            ETeam oppTeam = myTeam == ETeam.A ? ETeam.B : ETeam.A;
            if(m_Tanks.Count < (int)oppTeam)
            {
                return null;
            }
            return m_Tanks[(int)oppTeam];
        }
        private void AddTank(ETeam team)
        {
            TeamSetting setting = TeamSettings[(int)team];
            Type scriptType = Type.GetType(setting.TankScript);
            if(scriptType == null || scriptType.IsSubclassOf(Type.GetType("Main.Tank")) == false)
            {
                Debug.LogError("no tank script found");
                return;
            }
            GameObject tank = (GameObject)Instantiate(Resources.Load("Tank"));
            tank.transform.position = setting.Reborn ? setting.Reborn.transform.position : Vector3.zero;
            MeshRenderer[] mesh = tank.GetComponentsInChildren<MeshRenderer>();
            foreach (var m in mesh)
            {
                m.material.color = (team == ETeam.A ? Color.red : Color.cyan);
            }
            Tank t = (Tank)tank.AddComponent(scriptType);
            t.Team = team;
            m_Tanks.Add(t);
            m_Missiles.Add(new Dictionary<int, Missile>());
        }
        internal void AddMissile(Tank owner, Vector3 pos, Vector3 dir)
        {
            GameObject missileGO = (GameObject)Instantiate(Resources.Load("Missile"));
            Missile missile = missileGO.GetComponent<Missile>();
            missile.Init(owner, pos, dir.normalized * GlobalSetting.MissileSpeed);
            m_Missiles[(int)missile.Team].Add(missile.ID, missile);
        }
        internal void RemoveMissile(Missile m)
        {
            m_Missiles[(int)m.Team].Remove(m.ID);
            Destroy(m.gameObject);
        }
        void Update()
        {
            for(int i = 0; i < m_Tanks.Count; ++i)
            {
                Tank t = m_Tanks[i];
                if(t.IsDead && t.CanReborn(Time.time))
                {
                    t.ReBorn();
                }
            }
            m_RemainingTime -= Time.deltaTime;
        }

        private GUIStyle m_TeamAInfoStyle;
        private GUIStyle m_TeamBInfoStyle;
        private GUIStyle m_MatchInfoStyle;
        void OnGUI()
        {
            if(m_Tanks.Count > 0)
            {
                if (m_TeamAInfoStyle == null)
                {
                    m_TeamAInfoStyle = new GUIStyle();
                    m_TeamAInfoStyle.normal.textColor = Color.red;
                    m_TeamAInfoStyle.fontSize = 25;
                    m_TeamAInfoStyle.fontStyle = FontStyle.Bold;
                    m_TeamAInfoStyle.alignment = TextAnchor.UpperLeft;
                }
                GUI.Label(new Rect(10, 10, Screen.width * 0.5f - 10, 100), m_Tanks[0].GetTankInfo(), m_TeamAInfoStyle);
            }
            if (m_Tanks.Count > 1)
            {
                if (m_TeamBInfoStyle == null)
                {
                    m_TeamBInfoStyle = new GUIStyle();
                    m_TeamBInfoStyle.normal.textColor = Color.cyan;
                    m_TeamBInfoStyle.fontSize = 25;
                    m_TeamBInfoStyle.fontStyle = FontStyle.Bold;
                    m_TeamBInfoStyle.alignment = TextAnchor.UpperRight;
                }
                GUI.Label(new Rect(Screen.width * 0.5f, 10, Screen.width * 0.5f - 10, 100), m_Tanks[1].GetTankInfo(), m_TeamBInfoStyle);
            }
            if (m_MatchInfoStyle == null)
            {
                m_MatchInfoStyle = new GUIStyle();
                m_MatchInfoStyle.normal.textColor = Color.black;
                m_MatchInfoStyle.fontSize = 25;
                m_MatchInfoStyle.fontStyle = FontStyle.Bold;
                m_MatchInfoStyle.alignment = TextAnchor.UpperCenter;
            }
            int secounds = (int)m_RemainingTime % 60;
            int minutes = (int)m_RemainingTime / 60;
            string timeStr = string.Format("{0:00}:{1:00}", minutes, secounds);
            GUI.Label(new Rect(0, 10, Screen.width, 100), timeStr, m_MatchInfoStyle);
        }
    }
}
