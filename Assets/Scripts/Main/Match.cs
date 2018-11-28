using System;
using System.Collections.Generic;
using UnityEngine;

namespace Main
{
    public enum ETeam
    {
        A, B, NB
    }
    class Match : MonoBehaviour
    {
        [Serializable]
        public class TeamSetting : System.Object
        {
            public GameObject Reborn;
            public string TankScript;
        }
        public List<TeamSetting> TeamSettings;
        void Start()
        {
            if(TeamSettings.Count < 1)
            {
                Debug.LogError("must have 1 team settings");
                return;
            }
            AddTank(ETeam.A);
            if(TeamSettings.Count > 2)
            {
                AddTank(ETeam.B);
            }
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
        }
    }
}
