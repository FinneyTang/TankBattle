

using AI.Base;
using AI.Blackboard;

namespace AI.SensorSystem
{
    public class Sensor
    {
        public IAgent Agent
        {
            get; set;
        }
        public virtual void Update(BlackboardMemory sensorMemory)
        {

        }
    }
}
