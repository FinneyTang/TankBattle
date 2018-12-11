using AI.Blackboard;
using AI.SensorSystem;
using Main;
using UnityEngine;

namespace SensorAI
{
    class SightingSensor : Sensor
    {
        private float m_SightDistance = 10f;
        private float m_SightAngle = 45f;
        public override ESensorType GetSensorType()
        {
            return ESensorType.Sighting;
        }
        public SightingSensor(float dist, float angle)
        {
            m_SightDistance = dist;
            m_SightAngle = angle;
        }
        public override void Update(BlackboardMemory sensorMemory)
        {
            Tank t = (Tank)Agent;
            //check if can see other tank
            int targetOppKey = (int)EBBKey.TargetOppTank;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false && TrySightingTest(oppTank.Position))
            {
                sensorMemory.SetValue(targetOppKey, oppTank);
            }
            else
            {
                sensorMemory.SetValue(targetOppKey, null);
            }
            //check if star still exists
            int cachedStarID = sensorMemory.GetValue<int>((int)EBBKey.CaredStar, -1);
            if (cachedStarID >= 0) //has star
            {
                //move to star position it heard
                Vector3 starPos = sensorMemory.GetValue<Vector3>((int)EBBKey.TargetStarPos, default(Vector3));
                if(TrySightingTest(starPos))
                {
                    Star cachedStar = Match.instance.GetStarByID(cachedStarID);
                    if(cachedStar == null)
                    {
                        sensorMemory.DelValue((int)EBBKey.CaredStar);
                        sensorMemory.DelValue((int)EBBKey.TargetStarPos);
                    }
                }
            }
        }
        private bool TrySightingTest(Vector3 pos)
        {
            Tank t = (Tank)Agent;
            Vector3 toTarget = pos - t.FirePos;
            //sighting dist
            if (toTarget.sqrMagnitude > m_SightDistance * m_SightDistance)
            {
                return false;
            }
            //sighting angle
            if (Vector3.Angle(t.TurretAiming, toTarget) > m_SightAngle)
            {
                return false;
            }
            //line of sight
            if (t.CanSeeOthers(pos) == false)
            {
                return false;
            }
            return true;
        }
    }
    class HearingSensor : Sensor
    {
        private float m_HearingRadius = 10f;
        public override ESensorType GetSensorType()
        {
            return ESensorType.Hearing;
        }
        public HearingSensor(float radius)
        {
            m_HearingRadius = radius;
        }
        public override void StimulusReceived(Stimulus stim, BlackboardMemory sensorMemory)
        {
            Tank t = (Tank)Agent;
            Vector3 toTarget = stim.EmitterPos - t.Position;
            if(toTarget.sqrMagnitude > m_HearingRadius * m_HearingRadius)
            {
                return;
            }
            //star jingle
            if(stim.StimulusType == (int)EStimulusType.StarJingle)
            {
                bool needUpdateCachedStar = false;
                int cachedStarKey = (int)EBBKey.CaredStar;
                Star star = (Star)stim.TargetObject;
                int cachedStarID = sensorMemory.GetValue<int>(cachedStarKey, -1);
                if(cachedStarID < 0)
                {
                    needUpdateCachedStar = true;
                }
                else
                {
                    Star cachedStar = Match.instance.GetStarByID(cachedStarID);
                    if (cachedStar == null) //no star found, use current
                    {
                        needUpdateCachedStar = true;
                    }
                    else
                    {
                        //check if need update cached star
                        if((cachedStar.Position - t.Position).sqrMagnitude > toTarget.sqrMagnitude)
                        {
                            needUpdateCachedStar = true;
                        }
                    }
                }
                if(needUpdateCachedStar)
                {
                    //update target to nearer one
                    sensorMemory.SetValue(cachedStarKey, star.ID);
                    sensorMemory.SetValue((int)EBBKey.TargetStarPos, star.Position);
                }
            }
            else if(stim.StimulusType == (int)EStimulusType.StarTaken)
            {
                //if hears star taken, remove it from memory
                int cachedStarKey = (int)EBBKey.CaredStar;
                Star star = (Star)stim.TargetObject;
                int cachedStarID = sensorMemory.GetValue<int>(cachedStarKey, -1);
                if(star.ID == cachedStarID)
                {
                    sensorMemory.DelValue(cachedStarKey);
                    sensorMemory.DelValue((int)EBBKey.TargetStarPos);
                }
            }
        }
    }
    enum EBBKey
    {
        CaredStar, TargetStarPos, TargetOppTank
    }
    public class MyTank : Tank
    {
        const float SightDist = 40f;
        const float SightAngle = 60f;
        const float HearingRadius = 50f;

        private float m_LastTime;
        private SensorManager m_SensorManager;
        protected override void OnStart()
        {
            base.OnStart();
            m_SensorManager = new SensorManager(this);
            m_SensorManager.AddSensor(new SightingSensor(SightDist, SightAngle));
            m_SensorManager.AddSensor(new HearingSensor(HearingRadius));
        }
        protected override void OnUpdate()
        {
            m_SensorManager.Update();
            Tank oppTank = m_SensorManager.GetSensorMemory().GetValue<Tank>((int)EBBKey.TargetOppTank);
            if (oppTank != null)
            {
                TurretTurnTo(oppTank.Position);
                Vector3 toTarget = oppTank.Position - FirePos;
                toTarget.y = 0;
                toTarget.Normalize();
                if (Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            int targetStarID = m_SensorManager.GetSensorMemory().GetValue<int>((int)EBBKey.CaredStar, -1);
            if (targetStarID >= 0) //has star
            {
                //move to star position it heard before
                Vector3 starPos = m_SensorManager.GetSensorMemory().GetValue<Vector3>((int)EBBKey.TargetStarPos);
                Move(starPos);
            }
            else
            {
                if (Time.time > m_LastTime && ApproachNextDestination())
                {
                    m_LastTime = Time.time + Random.Range(3, 8);
                }
            }
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
            m_SensorManager.ClearSensorMemory();
        }
        private bool ApproachNextDestination()
        {
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
        protected override void OnStimulusReceived(Stimulus stim)
        {
            m_SensorManager.StimulusReceived(stim);
        }
        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            Matrix4x4 defaultMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Match.instance.GetTeamColor(Team);
            //draw hearing sensor
            Vector3 beginPoint = Vector3.zero;
            Vector3 firstPoint = Vector3.zero;
            for (float theta = 0; theta < 2 * Mathf.PI; theta += 2 * Mathf.PI / 32f)
            {
                float x = HearingRadius * Mathf.Cos(theta);
                float z = HearingRadius * Mathf.Sin(theta);
                Vector3 endPoint = new Vector3(x, 0.5f, z);
                if (theta == 0)
                {
                    firstPoint = endPoint;
                }
                else
                {
                    Gizmos.DrawLine(beginPoint, endPoint);
                }
                beginPoint = endPoint;
            }
            Gizmos.DrawLine(firstPoint, beginPoint);
            Gizmos.matrix = defaultMatrix;
            //draw sighting sensor
            RaycastHit hitInfo;
            Vector3 v1 = Quaternion.AngleAxis(SightAngle, Vector3.up) * TurretAiming;
            Vector3 v1EndPos = FirePos + v1 * SightDist;
            if (Physics.Linecast(FirePos, v1EndPos, out hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                v1EndPos = hitInfo.point;
            }
            Vector3 v2 = Quaternion.AngleAxis(-SightAngle, Vector3.up) * TurretAiming;
            Vector3 v2EndPos = FirePos + v2 * SightDist;
            if (Physics.Linecast(FirePos, v2EndPos, out hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                v2EndPos = hitInfo.point;
            }
            Gizmos.DrawLine(FirePos, v1EndPos);
            Gizmos.DrawLine(FirePos, v2EndPos);
        }
        public override string GetName()
        {
            return "SensorTank";
        }
    }
}
