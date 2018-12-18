using AI.Base;
using System.Collections.Generic;
using UnityEngine;

namespace AI.UtilityBased
{
    public class Utility
    {
        private float m_LastScore = 0;
        public float CalcU(IAgent agent)
        {
            m_LastScore = OnCalcU(agent);
            return m_LastScore;
        }
        public float GetLastScore()
        {
            return m_LastScore;
        }
        protected virtual float OnCalcU(IAgent agent)
        {
            return 0;
        }
    }
    public class CompositeUtility : Utility
    {
        protected List<Utility> m_Us = new List<Utility>();
        public virtual CompositeUtility AddUtility(Utility u)
        {
            m_Us.Add(u);
            return this;
        }
    }
    public class AdditiveComposite : CompositeUtility
    {
        protected override float OnCalcU(IAgent agent)
        {
            if(m_Us.Count == 0)
            {
                return 0;
            }
            float v = 0;
            foreach (Utility u in m_Us)
            {
                v += u.CalcU(agent);
            }
            return v / m_Us.Count;
        }
    }
    public class MultipleComposite : CompositeUtility
    {
        protected override float OnCalcU(IAgent agent)
        {
            float v = 1;
            foreach (Utility u in m_Us)
            {
                v *= u.CalcU(agent);
            }
            return v;
        }
    }
    public class WeightedAddtiveComposite : CompositeUtility
    {
        private List<float> m_Weights = new List<float>();
        private bool m_WeightsDirty = false;
        public override CompositeUtility AddUtility(Utility u)
        {
            return AddUtility(u, 1f);
        }
        public CompositeUtility AddUtility(Utility u, float w)
        {
            m_Us.Add(u);
            m_Weights.Add(w);
            m_WeightsDirty = true;
            return this;
        }
        protected override float OnCalcU(IAgent agent)
        {
            if(m_WeightsDirty)
            {
                float totalW = 0;
                foreach(float w in m_Weights)
                {
                    totalW += w;
                }
                if(Mathf.Abs(totalW) >= Mathf.Epsilon)
                {
                    for (int i = 0; i < m_Weights.Count; ++i)
                    {
                        m_Weights[i] /= totalW;
                    }
                }
                m_WeightsDirty = false;
            }
            float v = 0;
            for (int i = 0; i < m_Us.Count; ++i)
            {
                v += (m_Us[i].CalcU(agent) * m_Weights[i]);
            }
            return v;
        }
    }
}