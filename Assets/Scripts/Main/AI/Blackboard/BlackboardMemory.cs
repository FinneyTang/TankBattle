using System.Collections.Generic;
using UnityEngine;

namespace AI.Blackboard
{
    public class BlackboardMemory
    {
        class BlackboardItem
        {
            private float m_ExpiredTime;
            private object m_Value;
            public void SetValue(object v, float expiredTime = -1f)
            {
                m_Value = v;
                if(expiredTime >= 0)
                {
                    m_ExpiredTime = Time.time + expiredTime;
                }
                else
                {
                    m_ExpiredTime = -1f;
                }
            }

            public T GetValue<T>(T defaultValue)
            {
                if(m_ExpiredTime >= 0 && Time.time > m_ExpiredTime)
                {
                    return defaultValue;
                }
                return (T)m_Value;
            }
        }
        private Dictionary<int, BlackboardItem> m_Items;

        public BlackboardMemory()
        {
            m_Items = new Dictionary<int, BlackboardItem>();
        }
        public void Clear()
        {
            m_Items.Clear();
        }
        public void SetValue(int key, object v, float expirdTime = -1f)
        {
            BlackboardItem item;
            if (m_Items.ContainsKey(key) == false)
            {
                item = new BlackboardItem();
                m_Items.Add(key, item);
            }
            else
            {
                item = m_Items[key];
            }
            item.SetValue(v, expirdTime);
        }
        public bool HasValue(int key)
        {
            return m_Items.ContainsKey(key);
        }
        public void DelValue(int key)
        {
            m_Items.Remove(key);
        }
        public T GetValue<T>(int key, T defaultValue)
        {
            if (m_Items.ContainsKey(key) == false)
            {
                return defaultValue;
            }
            return m_Items[key].GetValue<T>(defaultValue);
        }
    }
}
