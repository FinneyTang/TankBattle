using System.Collections;
using System.Collections.Generic;
using Main;
using UnityEngine;

namespace LCTank
{
    public class LCTank : Tank
    {
        public override string GetName()
        {
            return "LCTank";
        }

        private float m_LastTime = 0;
        protected override void OnUpdate()
        {
            base.OnUpdate();

           
                bool hasStar = false;
                float nearestDist = float.MaxValue;
                Vector3 nearestStarPos = Vector3.zero;
                Tank oppTank = Match.instance.GetOppositeTank(Team);
            foreach (var pair in Match.instance.GetStars())
                {
                    Star s = pair.Value;
                    if (s.IsSuperStar)
                    {
                        hasStar = true;
                        nearestStarPos = s.Position;
                    }
                    else
                    {
                        float dist = (s.Position - Position).sqrMagnitude;
                        if (dist < nearestDist)
                        {
                            hasStar = true;
                            nearestDist = dist;
                            nearestStarPos = s.Position;
                        }
                    }
                }
                if (hasStar == true)
                {
                    Vector3 target = Match.instance.GetRebornPos(Team);
                    float distance = (target - Position).magnitude;
                    RaycastHit hitInfo;
                    if (oppTank == null)
                    {
                        if (Physics.Linecast(FirePos, Match .instance .GetRebornPos( oppTank .Team ), out hitInfo, PhysicsUtils.LayerMaskCollsion))
                        {
                            if (hitInfo.transform == null && HP >= 50)
                            {
                                TurretTurnTo(Match.instance.GetRebornPos(oppTank.Team));
                                if (Vector3.Dot(TurretAiming, (Match.instance.GetRebornPos(oppTank.Team) - FirePos)) >
                                    0.98f)
                                {
                                    Fire();
                                }
                            }
                        }
                    }
                    else
                    {
                        if (HP >= 50)
                        {
                            Move(nearestStarPos);
                        }
                        else
                        {
                            if (nearestDist < 28)
                            {
                                Move(nearestStarPos);
                            }
                            else
                            {
                                Move(target);
                            }
                        }
                    }
                }
                else
                {
                    if (Time.time > m_LastTime)
                    {
                        if (ApproachNextDestination())
                        {
                            m_LastTime = Time.time + Random.Range(3, 8);
                        }
                    }
                }
            
            
            if (oppTank != null)
            {
                bool seeOthers = false;
                RaycastHit hitInfo;
                if (Physics.Linecast(FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMaskCollsion))
                {
                    if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                    {
                        seeOthers = true;
                    }
                }
                if (seeOthers)
                {
                    TurretTurnTo(oppTank.Position);
                    Vector3 toTarget = oppTank.Position - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    
                    //Vector3 tar = GetVerticalDir(toTarget);
                    if (Vector3.Distance(oppTank.Position, Position) > 50)
                    {
                        Move(Position + GetVerticalDir(toTarget) * 5);
                    }
                    if (Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                    {
                        Fire();
                    }
                }
                else
                {
                    TurretTurnTo(Position+Forward);
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
        public  Vector3 GetVerticalDir(Vector3 _dir)
        {        //（_dir.x,_dir.z）与（？，1）垂直，则_dir.x * ？ + _dir.z * 1 = 0
            if (_dir.z == 0)
            {
                return new Vector3(0, 0, -1);
            }
            else
            {
                return new Vector3(-_dir.z / _dir.x, 0, 1).normalized;
            }

        } 
           
    }
}
