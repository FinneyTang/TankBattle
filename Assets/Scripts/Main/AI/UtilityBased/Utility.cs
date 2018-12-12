using AI.Base;
using System.Collections.Generic;
using UnityEngine;

namespace AI.UtilityBased
{
    public class Utility
    {
        public virtual float CalcU(IAgent agent)
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
        public override float CalcU(IAgent agent)
        {
            float v = 0;
            foreach (Utility u in m_Us)
            {
                v += u.CalcU(agent);
            }
            return v;
        }
    }
    public class MultipleComposite : CompositeUtility
    {
        public override float CalcU(IAgent agent)
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
        public override float CalcU(IAgent agent)
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