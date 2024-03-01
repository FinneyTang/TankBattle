using Main;
using UnityEngine;
using UnityEngine.AI;

namespace WSH
{
    class MyTank : Tank
    {
        Vector3 preTarget = Vector3.zero;
        private float m_LastTime = 0;
        Tank oppTank;
        protected override void OnStart()
        {
            base.OnStart();
            oppTank = Match.instance.GetOppositeTank(Team);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            Move();
            Attack();


        }
        void Move()
        {
            if (Match.instance.RemainingTime <= 180 * 0.5f + 5.0f && Match.instance.RemainingTime >= 180 * 0.5f)
            {
                Move(new Vector3(0, 0, 0));
            }
            else
            {
                //if (HP <= 25 && (!oppTank.IsDead))//少于25血且对方存活时
                //{
                //    Move(Match.instance.GetRebornPos(Team));
                //}
                //else
                //{
                    bool hasStar = false;
                    float nearestDist = float.MaxValue;
                    Vector3 nearestStarPos = Vector3.zero;//初始化 场上没有星星 最近的距离为最远值 最近的星星在（0，0）点
                    foreach (var pair in Match.instance.GetStars())//foreach场上每颗星星
                    {
                        Star s = pair.Value; //unknown
                        if (s.IsSuperStar)//如果星星是超级星星
                        {
                            hasStar = true;//星星判断改为true
                            nearestStarPos = s.Position;//最近星星位置改为超级星星，意味着超级星星优先级高于普通星星
                            break;
                        }
                        else
                        {
                            float dist = (s.Position - Position).sqrMagnitude;//dist保存该星星到坦克的距离
                            if (dist < nearestDist)//当星星距离够小时
                            {
                                hasStar = true;
                                nearestDist = dist;
                                nearestStarPos = s.Position;//将当前星星与坦克距离改为nearestDist并且酱最近的星星位置改为当前星星
                            }
                        }
                    }//遍历场上星星结束
                    if (hasStar == true)//当场上有星星时，判断血量以及到星星和家的距离进行移动
                    {
                        if((HP<=50 && Vector3.Distance(Position,Match.instance.GetRebornPos(Team))<=nearestDist) ||
                           (HP<=25 && Vector3.Distance(Position, Match.instance.GetRebornPos(Team)) * 0.7f <= nearestDist))
                        {
                            Move(Match.instance.GetRebornPos(Team));
                        }
                        else Move(nearestStarPos);//移动至nearestStar
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
                //}
            }
        }
        void Attack()
        {
            if (oppTank != null)
            {
                float distance = Vector3.Distance(oppTank.Position, Match.instance.GetOppositeTank(oppTank.Team).Position);
                float pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
                preTarget = oppTank.Position + oppTank.Velocity * pTime;//按照子弹的速度以及敌方坦克移动的速度计算子弹的落点
                for (int i = 0; i < 2; i++)
                {
                    distance = Vector3.Distance(Match.instance.GetOppositeTank(oppTank.Team).Position, preTarget);
                    pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
                    preTarget = oppTank.Position + oppTank.Velocity * pTime;
                }
                TurretTurnTo(preTarget);//转向预瞄点
                Vector3 direction = (preTarget - Position).normalized;
                if (Vector3.Dot(TurretAiming, direction) > 0.99f && !Physics.Linecast(Position, preTarget, PhysicsUtils.LayerMaskCollsion))
                {
                    Fire();
                }

            }
            if (oppTank.IsDead)
            {
                TurretTurnTo(Match.instance.GetRebornPos(oppTank.Team));
                Vector3 toEnHome = Match.instance.GetRebornPos(oppTank.Team) - FirePos;
                toEnHome.y = 0;
                toEnHome.Normalize();
                if (Vector3.Dot(TurretAiming, toEnHome) > 0.99f && !Physics.Linecast(Position, Match.instance.GetRebornPos(oppTank.Team), PhysicsUtils.LayerMaskCollsion))
                {
                    Fire();
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
            float halfSize = Match.instance.FieldSize * 0.5f;
            return Move(new Vector3(0, 0, 0));
        }
        public override string GetName()
        {
            return "WSH";
        }
    }
}
