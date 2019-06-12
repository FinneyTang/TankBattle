using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Main;
namespace XY
{
    class MyTank : Tank
    {
        Vector3 nextDes;
        Vector3 reborn;

        public override string GetName()
        {
            return "XY";
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            bool hasStar = false;
            bool seeOthers = false;
            float nearestDist = float.MaxValue;

            Vector3 nearestOppTankPos = Vector3.zero;
            Vector3 nearestStarPos = Vector3.zero;
            

            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
              
                    float dist = (s.transform.position - Position).sqrMagnitude;

                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.transform.position;
                    }
               
            }
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null)
            {

                RaycastHit hitInfo;
                if (Physics.Linecast(FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMaskCollsion))
                {
                    if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                    {
                        seeOthers = true;
                    }
                    else
                    {
                        seeOthers = false;
                    }
                }
                if (seeOthers)
                {
                    TurretTurnTo(oppTank.Position);
                    Vector3 toTarget = oppTank.Position + oppTank.Forward * Velocity.magnitude * Time.deltaTime - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    if (Vector3.Dot(TurretAiming, toTarget) > 0.7f)
                    {
                        Fire();
                    }
                }
                else
                {
                    nearestOppTankPos = oppTank.Position;
                }
            }
            if (oppTank == null && hasStar == false)
            {
               

                Move(new Vector3(0, 0, 0));
            }
            else if (oppTank == null && hasStar)
            {
                Move(nearestStarPos);
            }
            else if (oppTank != null && hasStar == false)
            {
                Move(nearestOppTankPos);
            }
            else if (oppTank != null && hasStar)
            {
                if (HP > 40)
                {
                    float disToStar = Vector3.Distance(transform.position, nearestStarPos);
                    float disOppTankToStar = Vector3.Distance(nearestStarPos, nearestOppTankPos);
                    if (disToStar <= disOppTankToStar)
                    {
                        Move(nearestStarPos);
                    }
                    if (disToStar > disOppTankToStar)
                    {
                        Move(nearestOppTankPos);
                    }
                }
                else if (HP > 25)
                {
                    Move(nearestStarPos);
                }
                else if (HP > 0)
                {
                    float disToStar = Vector3.Distance(transform.position, nearestStarPos);
                    float rangeToEatStar = Random.Range(6, 8);
                    if (disToStar <= rangeToEatStar)
                    {
                        Move(nearestStarPos);
                    }
                    else
                        Move(Match.instance.GetRebornPos(Team));
                }
            }

        }

        protected override void OnReborn()
        {
            base.OnReborn();

        }
    }
}

