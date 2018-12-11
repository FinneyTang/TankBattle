using UnityEngine;

namespace AI.SensorSystem
{
    public class Stimulus
    {
        //Stimulus emission postion
        public Vector3 EmitterPos
        {
            get; private set;
        }
        //Target sensor which is intresested in this stimulus
        public ESensorType TargetSensorType
        {
            get; private set;
        }
        //what kind of stimulus is, defined by user
        public int StimulusType
        {
            get; private set;
        }
        //intensity of this stimulus
        public float Intensity
        {
            get; private set;
        }
        public object TargetObject
        {
            get; private set;
        }
        public static Stimulus CreateStimulus(int stimType, ESensorType sensorType, Vector3 pos, object obj, float intensity = 0.5f)
        {
            return new Stimulus()
            {
                StimulusType = stimType,
                EmitterPos = pos,
                TargetSensorType = sensorType,
                TargetObject = obj,
                Intensity = Mathf.Clamp01(intensity)
            };
        }
    }
}
