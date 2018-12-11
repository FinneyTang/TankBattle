using AI.Base;
using AI.Blackboard;
using System.Collections.Generic;

namespace AI.SensorSystem
{
    public class SensorManager
    {
        private IAgent m_Agent;
        private List<Sensor> m_Sensors;
        private BlackboardMemory m_SensorMemory;
        public SensorManager(IAgent agent)
        {
            m_Agent = agent;
            m_SensorMemory = new BlackboardMemory();
            m_Sensors = new List<Sensor>();
        }
        public void AddSensor(Sensor s)
        {
            s.Agent = m_Agent;
            m_Sensors.Add(s);
        }
        public BlackboardMemory GetSensorMemory()
        {
            return m_SensorMemory;
        }
        public void ClearSensorMemory()
        {
            m_SensorMemory.Clear();
        }
        public void Update()
        {
            foreach (Sensor s in m_Sensors)
            {
                s.Update(m_SensorMemory);
            }
        }
        public void StimulusReceived(Stimulus stim)
        {
            foreach (Sensor s in m_Sensors)
            {
                if(stim.TargetSensorType != s.GetSensorType())
                {
                    continue;
                }
                s.StimulusReceived(stim, m_SensorMemory);
            }
        }
    }
}