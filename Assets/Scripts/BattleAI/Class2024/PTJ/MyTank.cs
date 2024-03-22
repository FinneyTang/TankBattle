using System.Collections.Generic;
using Main;
using UnityEngine;

namespace PTJ
{
    /*
    不太会写代码，所以基本搬运了老师的脚本）--> 真的很懒
    逻辑上以吃星星为主，打人为辅
    看见敌人（应该）会跑，会选择距离敌人更远的⭐去吃
    但是真打起架来好像谁也打不过）））
    */
    class MyTank : Tank
    {
        private readonly List<Tank> m_CachedOppTanks = new List<Tank>();
        protected override void OnUpdate()
        {
            base.OnUpdate();
            
            var oppTanks = Match.instance.GetOppositeTanks(Team, m_CachedOppTanks);
            
            MoveStrategy(oppTanks);

            Fire(oppTanks);

        }

        private void MoveStrategy(List<Tank> oppTanks)
        {
            bool hasStar = false;
            bool hasEnemy = false;
            float nearestDist = float.MaxValue;
            float halfSize = Match.instance.FieldSize * 0.5f;
            Vector3 nearestStarPos = Vector3.zero;
            Vector3 nearestEnemyPos = Vector3.zero;

            //var oppTanks = Match.instance.GetOppositeTanks(Team, m_CachedOppTanks);
            var oppTank = GetNearTarget(oppTanks);

            if (oppTanks != null && oppTanks.Count > 0) 
            {
                hasEnemy = true;
                if (oppTank != null)
                {
                    nearestEnemyPos = oppTank.Position;
                }
            }

            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = s.Position;
                    break;
                }
                else
                {
                    float dist = (s.Position - Position).sqrMagnitude;
                    if ((nearestEnemyPos - this.Position).sqrMagnitude < (nearestStarPos - this.Position).sqrMagnitude)
                        continue;

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
                Move(nearestStarPos);
            }
            else if((hasStar == false && HP <= 50) || (HP <= 25 && hasEnemy == true))
            {
                Move(Match.instance.GetRebornPos(Team));
            }
            else if (CanSeeOthers(oppTank))
            {
                Vector3 dir = (oppTank.Position - Position).normalized;
                //远离敌方坦克
                Vector3 randomDes = new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
                Vector3 moveDir = new Vector3(dir.x * randomDes.x, dir.y * randomDes.y, dir.z * randomDes.z);
                Move(moveDir);
            }
            else
            {
                Move(this.Position);
            }


        }

        private Tank GetNearTarget(List<Tank> tanks)
        {
            float minDist = float.MaxValue;
            Tank targetTank = null;
            foreach (var t in tanks)
            {
                if (t.IsDead)
                {
                    continue;
                }

                if (!CanSeeOthers(t))
                {
                    continue;
                }

                var dist = (Position - t.Position).sqrMagnitude;
                if (dist < minDist)
                {
                    targetTank = t;
                    minDist = dist;
                }
            }
            return targetTank;
        }

        private void Fire(List<Tank> oppTanks)
        {
            if (oppTanks != null && oppTanks.Count > 0)
            {
                var oppTank = GetNearTarget(oppTanks);
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
            }
        }


        public override string GetName()
        {
            return "PTJ";
        }
    }


}
