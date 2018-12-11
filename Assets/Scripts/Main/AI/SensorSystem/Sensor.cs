

using AI.Base;
using AI.Blackboard;

namespace AI.SensorSystem
{
    public enum ESensorType
    {
        Sighting, Hearing, Smelling
    }
    abstract public class Sensor
    {
        public IAgent Agent
        {
            get; set;
        }
        public abstract ESensorType GetSensorType();
        public virtual void Update(BlackboardMemory sensorMemory)
        {
        }
        public virtual void StimulusReceived(Stimulus stim, BlackboardMemory sensorMemory)
        {
        }
    }
}
