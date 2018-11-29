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
            public float MatchTime = 180;
            public float FireInterval = 1f;
            public float MissileSpeed = 40f;
            public int MaxHP = 100;
            public int DamagePerHit = 25;
        }
        public MatchSetting GlobalSetting = new MatchSetting();

        private List<Tank> m_Tanks;
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
            AddTank(ETeam.A);
            if(TeamSettings.Count > 1)
            {
                AddTank(ETeam.B);
            }
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
        }
        void Update()
        {
            for(int i = 0; i < m_Tanks.Count; ++i)
            {
                if(m_Tanks[i].IsDead)
                {
                    m_Tanks[i].Born();
                }
            }
        }
        void OnGUI()
        {
            if(m_Tanks.Count > 0)
            {
                GUIStyle AInfoStyle = new GUIStyle();
                AInfoStyle.normal.textColor = Color.red;
                AInfoStyle.fontSize = 25;
                AInfoStyle.fontStyle = FontStyle.Bold;
                AInfoStyle.alignment = TextAnchor.UpperLeft;

                string ainfo = string.Format("{0}\nHP: {1}", m_Tanks[0].GetName(), m_Tanks[0].HP);
                GUI.Label(new Rect(10, 10, Screen.width * 0.5f - 10, 100), ainfo, AInfoStyle);
            }

            if (m_Tanks.Count > 1)
            {
                GUIStyle BInfoStyle = new GUIStyle();
                BInfoStyle.normal.textColor = Color.cyan;
                BInfoStyle.fontSize = 25;
                BInfoStyle.fontStyle = FontStyle.Bold;
                BInfoStyle.alignment = TextAnchor.UpperRight;

                string binfo = string.Format("{0}\nHP: {1}", m_Tanks[0].GetName(), m_Tanks[1].HP);
                GUI.Label(new Rect(Screen.width * 0.5f, 10, Screen.width * 0.5f - 10, 100), binfo, BInfoStyle);
            }
        }
    }
}
