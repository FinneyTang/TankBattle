using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.SensorSystem;
using Main;
using UnityEngine;
using Random = UnityEngine.Random;

namespace XiaoWang
{
    
    public class WangTank : Tank
    {
        private Star starTarget;
        private Tank tankTarget;

        private Vector3 homePoint;

        private float randomCD = 2f;
        private float randomTimer = 0f;
        private float randomRadius = 5f;
        
        private bool onStayHome;
        private Vector3 targetPos;
        
        public override string GetName()
        {
            return "小王";
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            homePoint = Position;
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            tankTarget = null;
        }

        protected override void OnStart()
        {
            base.OnStart();
            
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            randomTimer += Time.deltaTime;
            GetNeareastStar();

            if (tankTarget != null && !tankTarget.IsDead)
            {
                TurretTurnTo(tankTarget.Position);
                
                Vector3 toTarget = tankTarget.Position - FirePos;
                toTarget.y = 0;
                toTarget.Normalize();
                // Debug.LogError(Vector3.Dot(TurretAiming, tankTarget.Position) );
                if(Vector3.Dot(TurretAiming, toTarget) > 0.98f && CanSeeOthers(tankTarget))
                {
                    Fire();
                }
            }
            else
            {
                SearchEnemyTarget();
                TurretTurnTo(Position + Forward);
            }

            if (onStayHome&&HP<85)
            {
                return;
            }
            else
            {
                onStayHome = false; 
            }

            if (HP < 25)
            {
                Move(homePoint);
                return;
            }
            
            if (HP < 30)
            {
                if (starTarget != null &&
                    Vector3.Distance(homePoint, Position) > Vector3.Distance(starTarget.Position, Position) && CanSeeOthers(starTarget.Position))
                {
                    Move(starTarget.Position);
                    return;
                }
                if (Vector3.Distance(homePoint, Position) < 10f)
                {
                    onStayHome = true;
                }
                Move(homePoint);
                return;
            }

            MoveToStar();
            if (starTarget!=null&&starTarget.IsSuperStar)
            {
                return;
            }

            if (tankTarget != null && tankTarget.IsDead)
            {
                tankTarget = null;
            }
            if (tankTarget != null && !tankTarget.IsDead)
            {
                targetPos = tankTarget.Position;
                if (HP<=40)
                {
                    Move(homePoint);
                    return;
                }
                
                if (randomTimer > randomCD && Vector3.Distance(tankTarget.Position,Position)<18f)
                {
                    float randomX = Random.Range(-randomRadius, randomRadius);
                    float randomZ = Random.Range(-randomRadius, randomRadius);
                    targetPos += new Vector3(randomX,0,randomZ);
                } 

                

                if(tankTarget.HP<HP)
                    Move(targetPos);
            }
            else
            {
                MoveToStar();
                if (starTarget == null && HP<=80)
                {
                    Move(homePoint);
                }

                if (starTarget != null && HP < 35)
                {
                    Move(homePoint);
                }
            }

        }



        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            
            Gizmos.DrawWireSphere(Position,10f);
            Gizmos.color= Color.green;
            if (tankTarget != null)
            {
                Gizmos.color = Color.red;
            }
        }

        protected override void OnStimulusReceived(Stimulus stim)
        {
            base.OnStimulusReceived(stim);
        }

        protected override void OnHandleSendTeamStrategy(Tank sender, int teamStrategy)
        {
            base.OnHandleSendTeamStrategy(sender, teamStrategy);
        }



        #region 逻辑

        #region 星星
        private void MoveToStar()
        {
            if(starTarget==null)
                return;
            if(HP<45 && Vector3.Distance(homePoint,Position)< Vector3.Distance(starTarget.Position,Position))
                return;
            Move(starTarget.Position);
        }

        private void GetNeareastStar()
        {
            var starts = Match.instance.GetStars();

            int superStar = 0;
            starTarget = null;
            Func<KeyValuePair<int,Star>, bool> checkSuperStar = s =>
            {
                if (s.Value.IsSuperStar)
                {
                    superStar = s.Key;
                    return true;
                }

                return false;
            };
            bool hasSuperStar = starts.Any(checkSuperStar);
            if (hasSuperStar)
            {
                starTarget = starts[superStar];
            }
            else
            {
                if(starts!=null && starts.Count>0)
                { 
                    var closest = starts.OrderBy(pair => Vector3.Distance(pair.Value.Position, Position))?.First();
                    starTarget = closest?.Value;
                }
            }
        }
        #endregion


        #region 开火

        private void SearchEnemyTarget()
        {
            var eteam = Match.instance.GetOppositeTanks(Team);
            List<Tank> seeTeam=new();
            bool canSeeOther= eteam.Any(tank =>
            {
                bool result = CanSeeOthers(tank) && !tank.IsDead;
                if (result)
                {
                    seeTeam.Add(tank);
                }
                return result;
            });

            if (!canSeeOther)
            {
                return;
            }
                
            tankTarget = seeTeam.OrderBy(pair => Vector3.Distance(pair.Position, Position)).First();
        }

        #endregion

        #endregion
    }
}
