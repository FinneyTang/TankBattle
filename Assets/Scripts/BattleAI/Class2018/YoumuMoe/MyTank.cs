
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace YoumuMoe
{
    class MyTank : Tank
    {
        private float m_LastTime = 0;
        protected override void OnUpdate()
        {
            base.OnUpdate();
            
            //#region 找对面坦克
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Fire();
            

            if (HP>=75)
            {
                home = false;
            }

            if (oppTank != null)
            {
                ETeam et = oppTank.Team;
                TurretTurnTo(oppTank.Position + oppTank.Velocity * (oppTank.Position - FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed);
                if (CanSeeOthers(oppTank))
                {
                    Vector3 toTarget = oppTank.Position - FirePos + oppTank.Velocity * (oppTank.Position - FirePos).magnitude / Match.instance.GlobalSetting.MissileSpeed;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    
                }
                else
                {

                    
                    //Move(oppTank.transform.position);
                }



                if (oppTank.HP <= 0)
                {

                    TurretTurnTo(Match.instance.GetRebornPos(et));
                    if (HP <= 50)
                    {
                        home = true;
                        Debug.Log(Match.instance.GetRebornPos(Team));
                    }


                }
                    FindStar(et);

            }
                

            
        }
        bool home = false;
        bool superStar = false;
        private void FindStar(ETeam et)
        {
            superStar=false;
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    superStar = true;
                    hasStar = true;
                    nearestStarPos = s.Position;
                    break;
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
                if (!superStar&&home && nearestDist > (transform.position - Match.instance.GetRebornPos(Team)).sqrMagnitude)
                {
                    Move(Match.instance.GetRebornPos(Team));
                }
                else
                {
                    Move(nearestStarPos);
                }
                
            }
            else
            {
                Move(Match.instance.GetRebornPos(et));
                if (Time.time > m_LastTime)
                {
                    if (ApproachNextDestination())
                    {
                        m_LastTime = Time.time + Random.Range(3, 8);
                    }
                }
            }
        }

        private bool CanSeeOthers(Tank oppTank)
        {
            RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.Find("Turret").forward, 100);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.gameObject.layer == LayerMask.GetMask("Layer_Entity") && hits[i].collider.gameObject != gameObject)
                {
                    return true;
                }
                else if (hits[i].collider.gameObject.layer != LayerMask.GetMask("Layer_Entity"))
                {
                    return false;
                }
            }
            return false;
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
        public override string GetName()
        {
            return "YoumuMoe";
        }
    }
}
