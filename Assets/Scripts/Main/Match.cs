using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
            public float StarAddInterval = 5;
            public float MaxStarCount = 2;
        }
        public MatchSetting GlobalSetting = new MatchSetting();

        public Camera WinningCamera;
        public GameObject WinnerShow;

        private List<Tank> m_Tanks;
        private List<Dictionary<int, Missile>> m_Missiles;
        private Dictionary<int, Star> m_Stars;
        private Timer m_TimerToAddStar;
        private bool m_MatchEnd = false;
        private Tank m_Winner;
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
            m_Stars = new Dictionary<int, Star>();
            AddTank(ETeam.A);
            if(TeamSettings.Count > 1)
            {
                AddTank(ETeam.B);
            }
            m_RemainingTime = GlobalSetting.MatchTime;
            m_TimerToAddStar = new Timer();
            if(WinningCamera != null)
            {
                WinningCamera.gameObject.SetActive(false);
            }
            m_MatchEnd = false;
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
        public Dictionary<int, Star> GetStars()
        {
            return m_Stars;
        }
        public Dictionary<int, Missile> GetOppositeMissiles(ETeam myTeam)
        {
            ETeam oppTeam = myTeam == ETeam.A ? ETeam.B : ETeam.A;
            if (m_Missiles.Count < (int)oppTeam)
            {
                return null;
            }
            return m_Missiles[(int)oppTeam];
        }
        public bool IsMathEnd()
        {
            return m_MatchEnd;
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
        private void AddStar()
        {
            bool hasValidPos = false;
            Vector3 targetPos = Vector3.zero;
            NavMeshHit hit;
            targetPos = new Vector3(UnityEngine.Random.Range(-40, 40), 0, UnityEngine.Random.Range(-40, 40));
            targetPos.y = 3f;
            if(NavMesh.SamplePosition(targetPos, out hit, 10f, 1 << NavMesh.GetAreaFromName("Walkable")))
            {
                targetPos = hit.position;
                hasValidPos = true;
            }
            if(hasValidPos)
            {
                GameObject starGO = (GameObject)Instantiate(Resources.Load("Star"));
                Star s = starGO.GetComponent<Star>();
                s.Init(targetPos);
                m_Stars.Add(s.ID, s);
            }
        }
        internal void RemoveStar(Star s)
        {
            m_Stars.Remove(s.ID);
            Destroy(s.gameObject);
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
        private Tank GetWinner()
        {
            if(m_Tanks.Count < 2)
            {
                return m_Tanks[0];
            }
            Tank tA = m_Tanks[0];
            Tank tB = m_Tanks[1];
            if(tA.Score == tB.Score)
            {
                return null;
            }
            return tA.Score > tB.Score ? tA : tB;
        }
        void Update()
        {
            if(m_MatchEnd)
            {
                return;
            }
            for(int i = 0; i < m_Tanks.Count; ++i)
            {
                Tank t = m_Tanks[i];
                if(t.IsDead && t.CanReborn(Time.time))
                {
                    t.ReBorn();
                }
            }
            if(m_TimerToAddStar.IsExpired(Time.time) && m_Stars.Count < GlobalSetting.MaxStarCount)
            {
                AddStar();
                m_TimerToAddStar.SetExpiredTime(Time.time + GlobalSetting.StarAddInterval);
            }
            m_RemainingTime -= Time.deltaTime;
            if(m_RemainingTime < 0)
            {
                m_MatchEnd = true;
                m_Winner = GetWinner();
                if(m_Winner != null)
                {
                    if (WinningCamera != null)
                    {
                        WinningCamera.gameObject.SetActive(true);
                    }
                    if (WinnerShow != null)
                    {
                        MeshRenderer[] mesh = WinnerShow.GetComponentsInChildren<MeshRenderer>();
                        foreach (var m in mesh)
                        {
                            m.material.color = (m_Winner.Team == ETeam.A ? Color.red : Color.cyan);
                        }
                    }
                }
                for (int i = 0; i < m_Tanks.Count; ++i)
                {
                    Tank t = m_Tanks[i];
                    t.gameObject.SetActive(false);
                }
            }
        }

        private GUIStyle m_TeamAInfoStyle;
        private GUIStyle m_TeamBInfoStyle;
        private GUIStyle m_MatchInfoStyle;
        private GUIStyle m_WinningStyle;
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
            if(m_MatchEnd == false)
            {
                int secounds = (int)m_RemainingTime % 60;
                int minutes = (int)m_RemainingTime / 60;
                string timeStr = string.Format("{0:00}:{1:00}", minutes, secounds);
                GUI.Label(new Rect(0, 10, Screen.width, 100), timeStr, m_MatchInfoStyle);
            }
            else
            {
                if (m_WinningStyle == null)
                {
                    m_WinningStyle = new GUIStyle();
                    m_WinningStyle.normal.textColor = Color.black;
                    m_WinningStyle.fontSize = 50;
                    m_WinningStyle.fontStyle = FontStyle.Bold;
                    m_WinningStyle.alignment = TextAnchor.MiddleCenter;
                }
                if(m_Winner == null)
                {
                    GUI.Label(new Rect(0, 10, Screen.width, 200), "Draw", m_WinningStyle);
                }
                else
                {
                    m_WinningStyle.normal.textColor = m_Winner.Team == ETeam.A ? Color.red : Color.cyan;
                    string winnerInfo = string.Format("Winner, {0}", m_Winner.GetName());
                    GUI.Label(new Rect(0, 10, Screen.width, 200), winnerInfo, m_WinningStyle);
                }
            }
        }
    }
}
