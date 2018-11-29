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
            public int TankID;
        }
        public List<TeamSetting> TeamSettings;

        [Serializable]
        public class MatchSetting
        {
            public float MatchTime = 180;
            public int NumOfStartToWin = 10;
            public float FireInterval = 1f;
            public float MissileSpeed = 40f;
            public int MaxHP = 100;
            public int DamagePerHit = 10;
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
    }
}
