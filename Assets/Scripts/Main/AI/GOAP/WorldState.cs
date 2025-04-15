using System.Collections.Generic;
using System.Text;

namespace AI.GOAP
{
    public class WorldState
    {
        private readonly Dictionary<string, object> m_State = new Dictionary<string, object>();
        public void SetState(string key, object value)
        {
            m_State[key] = value;
        }

        public object GetState(string key)
        {
            if (m_State.TryGetValue(key, out var state))
            {
                return state;
            }
            return null;
        }

        public T GetState<T>(string key, T defaultValue = default(T))
        {
            if (m_State.TryGetValue(key, out var state))
            {
                return (T)state;
            }
            return defaultValue;
        }

        public void Clear()
        {
            m_State.Clear();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is WorldState other))
            {
                return false;
            }
            if (m_State.Count != other.m_State.Count)
            {
                return false;
            }
            foreach (var pair in m_State)
            {
                if (!other.m_State.TryGetValue(pair.Key, out var otherValue) || !pair.Value.Equals(otherValue))
                {
                    return false;
                }
            }
            return true;
        }
        
        public override int GetHashCode()
        {
            int hash = 0;
            foreach (var pair in m_State)
            {
                hash ^= pair.Key.GetHashCode();
                hash ^= pair.Value.GetHashCode();
            }
            return hash;
        }

        public WorldState Clone()
        {
            WorldState clone = new WorldState();
            foreach (var pair in m_State)
            {
                clone.SetState(pair.Key, pair.Value);
            }  
            return clone;
        }
        
        public bool IsSatisfied(WorldState other)
        {
            if (other == null)
            {
                return false;
            }
            foreach (var pair in other.m_State)
            {
                var currentValue = GetState(pair.Key);
                if (currentValue == null || !currentValue.Equals(pair.Value))
                {
                    return false;
                }
            }
            return true;
        }
        
        public WorldState Merge(WorldState other)
        {
            if (other == null)
            {
                return this;
            }
            foreach (var pair in other.m_State)
            {
                SetState(pair.Key, pair.Value);
            }
            return this;
        }

        private StringBuilder m_SB;
        public override string ToString()
        {
            if (m_SB == null)
            {
                m_SB = new StringBuilder();
            }
            m_SB.Length = 0;
            foreach (var pair in m_State)
            {
                m_SB.Append(pair.Key);
                m_SB.Append(":");
                m_SB.Append(pair.Value);
                m_SB.Append(",");
            }
            return m_SB.ToString();
        }
    }
}