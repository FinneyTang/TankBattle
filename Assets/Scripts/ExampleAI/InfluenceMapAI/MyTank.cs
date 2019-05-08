using AI.InfluenceMap;
using Main;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace InfluenceMap
{
    class MyTank : Tank
    {
        private float m_LastTime = 0;

        private float m_LastUpdateInfluenceMapTime = 0;
        private InfluenceMap2D m_InfluenceMap;
        protected override void OnStart()
        {
            base.OnStart();
            if(Team == ETeam.A)
            {
                m_InfluenceMap = new InfluenceMap2D(10, 10, 10, 10, new Vector3(-50, 0, -50));
            }
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if(m_InfluenceMap != null && Time.time > m_LastUpdateInfluenceMapTime)
            {
                //update influence map
                m_InfluenceMap.Clear();
                m_InfluenceMap.AddInfluenceSource(Position);
                Tank oppTank = Match.instance.GetOppositeTank(Team);
                if (oppTank != null)
                {
                    m_InfluenceMap.AddInfluenceSource(oppTank.Position);
                }
                m_LastUpdateInfluenceMapTime = Time.time + 0.5f;
            }
            //random move
            if (Time.time > m_LastTime)
            {
                if (ApproachNextDestination())
                {
                    m_LastTime = Time.time + Random.Range(3, 8);
                }
            }
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()
        {
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
#if UNITY_EDITOR
        private List<GUIStyle> m_InfStyles;
#endif
        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
#if UNITY_EDITOR
            if (m_InfStyles == null)
            {
                m_InfStyles = new List<GUIStyle>();

                GUIStyle h;
                
                h = new GUIStyle();
                h.fontSize = 16;
                h.fontStyle = FontStyle.Bold;
                h.normal.textColor = Color.green;
                m_InfStyles.Add(h);

                h = new GUIStyle();
                h.fontSize = 16;
                h.fontStyle = FontStyle.Bold;
                h.normal.textColor = Color.yellow;
                m_InfStyles.Add(h);

                h = new GUIStyle();
                h.fontSize = 16;
                h.fontStyle = FontStyle.Bold;
                h.normal.textColor = Color.red;
                m_InfStyles.Add(h);
            }
            if(m_InfluenceMap != null)
            {
                m_InfluenceMap.IteratorGrid(Vector3.zero, 10, 10, (float value, int centerX, int centerY, int curX, int curY) =>
                {
                    Vector3 gridPos = Vector3.zero;
                    if (m_InfluenceMap.GridCoordToPos(curX, curY, ref gridPos) == false)
                    {
                        return;
                    }
                    Handles.Label(gridPos + Vector3.up * 1, ((int)value).ToString(), m_InfStyles[Mathf.Clamp((int)(value / 34f), 0, m_InfStyles.Count - 1)]);
                });
            }
#endif
        }
        public override string GetName()
        {
            return "InfluenceMapTank";
        }
    }
}
