using AI.SensorSystem;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace Main
{
    public enum ETeam
    {
        A, B, C, D, NB
    }
    public enum EStimulusType
    {
        StarJingle, StarTaken
    }
    public class Match : MonoBehaviour
    {
        public static Match instance = null;

        [Serializable]
        public class TeamSetting
        {
            public ETeam Team;
            public string TankScript;
        }
        public List<TeamSetting> TeamSettings;

        public enum EGameMode
        {
            FreeForAll, TeamBattle,
        }
        
        [Serializable]
        public class MatchSettingData
        {
            //public EGameMode GameMode = EGameMode.FreeForAll;
            public int MatchTime = 180;
            public float FireInterval = 1f;
            public float MissileSpeed = 40f;
            public int MaxHP = 100;
            public float RebonCD = 10f;
            public int DamagePerHit = 25;
            public int ScoreForStar = 5;
            public int ScoreForKill = 10;
            public int ScoreForSuperStar = 30;
            public float StarAddInterval = 5;
            public int MaxStarCount = 3;
            public int HPRecoverySpeed = 5;
            public float HomeZoneRadius = 10;
        }
        [Serializable]
        public class RebornAreaSetting
        {
            public ETeam Team;
            public GameObject RebornArea;
        }
        [Serializable]
        public class FieldSettingData
        {
            public float FieldSize = 100f;
            public List<RebornAreaSetting> RebornAreas;
        }
        
        public MatchSettingData GlobalSetting = new MatchSettingData();
        public FieldSettingData FieldSetting = new FieldSettingData();

        public float FieldSize => FieldSetting.FieldSize;

        public Camera WinningCamera;
        public GameObject WinnerShow;

        private Dictionary<ETeam, List<Tank>> m_Tanks;
        private Dictionary<ETeam, GameObject> m_RebornAreas;
        private Dictionary<ETeam, Dictionary<int, Missile>> m_Missiles;
        private Dictionary<int, Star> m_Stars;
        private Timer m_TimerToAddStar;
        private bool m_MatchEnd = false;
        private ETeam m_WinnerTeam;
        private float m_RemainingTime = 0;
        private bool m_SuperStarAdded = false;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Match.instance = this;
        }

        private void Start()
        {
            //init reborn area
            m_RebornAreas = new Dictionary<ETeam, GameObject>();
            foreach (var setting in FieldSetting.RebornAreas)
            {
                if (setting.RebornArea == null)
                {
                    continue;
                }
                m_RebornAreas[setting.Team] = setting.RebornArea;
                setting.RebornArea.GetComponentInChildren<HomeZone>().Team = setting.Team;
            }
            
            if(TeamSettings.Count < 1)
            {
                Debug.LogError("must have 1 team settings");
                return;
            }
            m_Tanks = new Dictionary<ETeam, List<Tank>>();
            m_Missiles = new Dictionary<ETeam, Dictionary<int, Missile>>();
            m_Stars = new Dictionary<int, Star>();
            foreach (var tankSetting in TeamSettings)
            {
                AddTank(tankSetting.Team, tankSetting.TankScript);
            }
            m_RemainingTime = GlobalSetting.MatchTime;
            m_TimerToAddStar = new Timer();
            if(WinningCamera != null)
            {
                WinningCamera.gameObject.SetActive(false);
            }
            m_MatchEnd = false;
            m_SuperStarAdded = false;
        }
        public Tank GetOppositeTank(ETeam myTeam)
        {
            ETeam oppTeam = myTeam == ETeam.A ? ETeam.B : ETeam.A;
            if (!m_Tanks.TryGetValue(oppTeam, out var tanks) || tanks.Count == 0)
            {
                return null;
            }
            return tanks[0];
        }
        public List<Tank> GetOppositeTanks(ETeam myTeam, List<Tank> outputs = null)
        {
            outputs ??= new List<Tank>();
            outputs.Clear();
            foreach (var pair in m_Tanks)
            {
                if (pair.Key != myTeam)
                {
                    outputs.AddRange(pair.Value);
                }
            }
            return outputs;
        }
        public Tank GetTank(ETeam t)
        {
            if (!m_Tanks.TryGetValue(t, out var tanks) || tanks.Count == 0)
            {
                return null;
            }
            return tanks[0];
        }
        public List<Tank> GetTanks(ETeam t)
        {
            if (m_Tanks.TryGetValue(t, out var tanks))
            {
                return tanks;
            }
            return null;
        }
        public Dictionary<int, Star> GetStars()
        {
            return m_Stars;
        }
        public Star GetStarByID(int id)
        {
            m_Stars.TryGetValue(id, out var s);
            return s;
        }
        public Dictionary<int, Missile> GetOppositeMissiles(ETeam myTeam)
        {
            ETeam oppTeam = myTeam == ETeam.A ? ETeam.B : ETeam.A;
            if (!m_Missiles.TryGetValue(oppTeam, out var missiles))
            {
                return null;
            }
            return missiles;
        }
        public Dictionary<int, Missile> GetOppositeMissilesEx(ETeam myTeam, Dictionary<int, Missile> outputs = null)
        {
            outputs ??= new Dictionary<int, Missile>();
            outputs.Clear();
            foreach (var pair in m_Missiles)
            {
                if (pair.Key == myTeam)
                {
                    continue;
                }
                foreach (var missilePair in pair.Value)
                {
                    outputs.Add(missilePair.Key, missilePair.Value);
                }
            }
            return outputs;
        }
        public bool IsMathEnd()
        {
            return m_MatchEnd;
        }
        public float RemainingTime
        {
            get
            {
                return m_RemainingTime;
            }
        }
        public Vector3 GetRebornPos(ETeam t)
        {
            if((int)t >= TeamSettings.Count)
            {
                return Vector3.zero;
            }
            if (!m_RebornAreas.TryGetValue(t, out var rebornGO))
            {
                return Vector3.zero;
            }
            if(rebornGO == null)
            {
                return Vector3.zero;
            }
            return rebornGO.transform.position;
        }
        private void AddTank(ETeam team, string scriptName)
        {
            Type scriptType = Type.GetType(scriptName);
            if(scriptType == null || scriptType.IsSubclassOf(Type.GetType("Main.Tank")) == false)
            {
                Debug.LogError("no tank script found");
                return;
            }
            GameObject tank = (GameObject)Instantiate(Resources.Load("Tank"), GetRebornPos(team), Quaternion.identity);
            MeshRenderer[] mesh = tank.GetComponentsInChildren<MeshRenderer>();
            foreach (var m in mesh)
            {
                m.material.color = Utils.GetTeamColor(team);
            }
            Tank t = (Tank)tank.AddComponent(scriptType);
            t.Init(team);
            //add to tank list
            if (!m_Tanks.TryGetValue(team, out var tanks))
            {
                tanks = new List<Tank>();
                m_Tanks.Add(team, tanks);
            }
            tanks.Add(t);
            //init team missiles list
            if(!m_Missiles.TryGetValue(team, out var missiles) || missiles == null)
            {
                missiles = new Dictionary<int, Missile>();
                m_Missiles[team] = missiles;
            }
        }
        private void AddStar(bool isSuperStar)
        {
            bool hasValidPos = false;
            Vector3 targetPos = Vector3.zero;
            if (isSuperStar == false)
            {
                var l = FieldSize * 0.5f - 10;
                targetPos = new Vector3(UnityEngine.Random.Range(-l, l), 0, UnityEngine.Random.Range(-l, l));
            }
            targetPos.y = 3f;
            if(NavMesh.SamplePosition(targetPos, out var hit, 10f, 1 << NavMesh.GetAreaFromName("Walkable")))
            {
                targetPos = hit.position;
                hasValidPos = true;
            }
            if(hasValidPos)
            {
                GameObject starGO = (GameObject)Instantiate(Resources.Load(isSuperStar ? "SuperStar" : "Star"));
                Star s = starGO.GetComponent<Star>();
                s.Init(targetPos, isSuperStar);
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
            if (!m_Missiles.TryGetValue(missile.Team, out var missiles))
            {
                missiles = new Dictionary<int, Missile>();
                m_Missiles.Add(missile.Team, missiles);
            }
            missiles.Add(missile.ID, missile);
        }
        internal void RemoveMissile(Missile m)
        {
            if (!m_Missiles.TryGetValue(m.Team, out var missiles))
            {
                return;
            }
            missiles.Remove(m.ID);
            Destroy(m.gameObject);
        }
        internal void SendStim(Stimulus s)
        {
            if (IsMathEnd())
            {
                return;
            }
            foreach (var pair in m_Tanks)
            {
                foreach (var t in pair.Value)
                {
                    if (t.IsDead == false)
                    {
                        t.StimulusReceived(s);
                    }
                }
            }
        }

        public ETeam WinnerTeam => m_WinnerTeam;

        private int m_MaxScore = -1;
        private void UpdateWinner()
        {
            if(m_Tanks.Count < 2)
            {
                m_WinnerTeam = ETeam.A;
                return;
            }
            ETeam winner = m_WinnerTeam;
            foreach (var pair in m_Tanks)
            {
                int totalScore = 0;
                foreach (var t in pair.Value)
                {
                    totalScore += t.Score;
                }
                if (totalScore > m_MaxScore)
                {
                    m_MaxScore = totalScore;
                    winner = pair.Key;
                }
            }
            m_WinnerTeam = winner;
        }
        void Update()
        {
            if(m_MatchEnd)
            {
                return;
            }
            foreach (var pair in m_Tanks)
            {
                foreach (var t in pair.Value)
                {
                    if(t.IsDead && t.CanReborn(Time.time))
                    {
                        t.ReBorn();
                    }
                }
            }
            if(m_TimerToAddStar.IsExpired(Time.time) && m_Stars.Count < GlobalSetting.MaxStarCount)
            {
                AddStar(false);
                m_TimerToAddStar.SetExpiredTime(Time.time + GlobalSetting.StarAddInterval);
            }
            if(m_SuperStarAdded == false && m_RemainingTime < Match.instance.GlobalSetting.MatchTime * 0.5f)
            {
                m_SuperStarAdded = true;
                AddStar(true);
            }
            m_RemainingTime -= Time.deltaTime;
            //caculate winner each frame
            UpdateWinner();
            //check if match end
            if(m_RemainingTime < 0)
            {
                m_MatchEnd = true;
                if (WinningCamera != null)
                {
                    WinningCamera.gameObject.SetActive(true);
                }
                var winnerTanks = GetTanks(m_WinnerTeam);
                for (int i = 0; i < winnerTanks.Count; ++i)
                {
                    if (WinnerShow != null)
                    {
                        var winnerTank = GameObject.Instantiate(WinnerShow, WinnerShow.transform.parent);
                        MeshRenderer[] mesh = winnerTank.GetComponentsInChildren<MeshRenderer>();
                        foreach (var m in mesh)
                        {
                            m.material.color = Utils.GetTeamColor(m_WinnerTeam);
                        }
                        var localPos = winnerTank.transform.localPosition;
                        localPos.x = (winnerTanks.Count == 1) ? 0 : (i == 0) ? -6 : 6;
                        winnerTank.transform.localPosition = localPos;
                    }
                }
                WinnerShow.SetActive(false);
                foreach (var pair in m_Tanks)
                {
                    foreach (var t in pair.Value)
                    {
                        t.gameObject.SetActive(false);
                    }
                }
            }
        }
        
        public string GetWinningIndicater(ETeam team)
        {
            return WinnerTeam == team ? "★" : "";
        }

        private readonly GUIStyle[] m_TeamInfoStyle = new GUIStyle[(int)ETeam.NB];
        private GUIStyle m_TeamBInfoStyle;
        private GUIStyle m_MatchInfoStyle;
        private GUIStyle m_WinningStyle;
        private StringBuilder m_SB = new StringBuilder();

        private void UpdateTeamInfo(ETeam team)
        {
            if(m_Tanks.TryGetValue(team, out var tanks))
            {
                var teamInfoStyle = m_TeamInfoStyle[(int)team];
                if (teamInfoStyle == null)
                {
                    teamInfoStyle = new GUIStyle();
                    m_TeamInfoStyle[(int)team] = teamInfoStyle;
                    teamInfoStyle.normal.textColor = Utils.GetTeamColor(team);
                    teamInfoStyle.fontSize = 20;
                    teamInfoStyle.fontStyle = FontStyle.Bold;
                    switch (team)
                    {
                        case ETeam.A:
                            teamInfoStyle.alignment = TextAnchor.UpperLeft;
                            break;
                        case ETeam.B:
                            teamInfoStyle.alignment = TextAnchor.UpperRight;
                            break;
                        case ETeam.C:
                            teamInfoStyle.alignment = TextAnchor.LowerLeft;
                            break;
                        case ETeam.D:
                            teamInfoStyle.alignment = TextAnchor.LowerRight;
                            break;
                    }
                }

                int tankCount = tanks.Count;
                Rect rect = new Rect();
                switch (team)
                {
                    case ETeam.A:
                        rect = new Rect(10, 10, Screen.width * 0.5f - 10, 100 * tankCount);
                        break;
                    case ETeam.B:
                        rect = new Rect(Screen.width * 0.5f, 10, Screen.width * 0.5f - 10, 100 * tankCount);
                        break;
                    case ETeam.C:
                        rect = new Rect(10, Screen.height - 100 * tankCount - 10, Screen.width * 0.5f - 10, 100 * tankCount);
                        break;
                    case ETeam.D:
                        rect = new Rect(Screen.width * 0.5f, Screen.height - 100 * tankCount - 10, Screen.width * 0.5f - 10, 100 * tankCount);
                        break;
                }
                if (tankCount == 1)
                {
                    GUI.Label(rect, tanks[0].GetTankInfo(), teamInfoStyle);
                }
                else
                {
                    m_SB.Clear();
                    int teamScore = 0;
                    for (int i = 0; i < tankCount; ++i)
                    {
                        teamScore += tanks[i].Score;
                        m_SB.Append(tanks[i].GetTankInfo(true));
                        m_SB.Append("\n\n");
                    }
                    m_SB.Append($"Score: {teamScore} {GetWinningIndicater(team)}");
                    GUI.Label(rect, m_SB.ToString(), teamInfoStyle);
                }
            }
        }
        private void OnGUI()
        {
            for (int i = 0; i < (int)ETeam.NB; ++i)
            {
                UpdateTeamInfo((ETeam)i);
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
                string timeStr = $"{minutes:00}:{secounds:00}";
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
                m_WinningStyle.normal.textColor = Utils.GetTeamColor(m_WinnerTeam);
                string winnerInfo = string.Empty;
                if (m_Tanks.TryGetValue(m_WinnerTeam, out var tanks))
                {
                    string winnerName = string.Empty;
                    for(int i = 0; i < tanks.Count; ++i)
                    {
                        winnerName += tanks[i].GetName();
                        if (i != tanks.Count - 1)
                        {
                            winnerName += " & ";
                        }
                    }
                    winnerInfo = $"Winner, {winnerName}";
                }
                GUI.Label(new Rect(0, 10, Screen.width, 200), winnerInfo, m_WinningStyle);
            }
        }
    }
}
