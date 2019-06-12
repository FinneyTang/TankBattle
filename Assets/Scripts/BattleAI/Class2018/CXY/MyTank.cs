using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Main;

namespace CXY
{
    public class MyTank : Tank
    {
        
        Tank m_oppTank;
        Vector3 m_oppTankPos;
        //Vector3 m_targetPos = Vector3.zero;
        //NavMeshAgent m_navMeshAgent;
        float time;
        //Vector3 m_oppTankTargetPos;//敌方坦克目标点
        Vector3 m_nearStarPos;
        bool hasStar = false;
        float nearestDist = float.MaxValue;
        Vector3 nearestStarPos = Vector3.zero;
        //float myDis = 0;//找星星里面的己方坦克离星星的距离
        //float oppDis = 0;//找星星里面的敌方坦克离星星的距离
        public override string GetName()
        {
            return "CXY";
        }
        protected override void OnStart()
        {
            base.OnStart();
            //m_oppTank = Match.instance.GetOppositeTank(Team);//获得敌方坦克
            //m_navMeshAgent = GetComponent<NavMeshAgent>();
        }
        //----------------------------------------------
        protected override void OnUpdate()
        {
            base.OnUpdate();
            m_oppTank = Match.instance.GetOppositeTank(Team);
            m_oppTankPos = m_oppTank.transform.position;
            GetStar();
            Attack();
            //敌方坦克存活，且有星星存在
            if (hasStar && !m_oppTank.IsDead)
            {
                Move(nearestStarPos);
            }
            if (m_oppTank.IsDead)
            {
                time = Time.time;
            }
            if (Match.instance.RemainingTime <= 96 && Match.instance.RemainingTime >= 90 && Vector3.Distance(transform.position, Vector3.zero) >= 5)
            {
                Move(Vector3.zero);
            }
            if (!(Match.instance.RemainingTime <=96 && Match.instance.RemainingTime >= 90 && Vector3.Distance(transform.position, Vector3.zero) >= 5))
            {
                if (hasStar && m_oppTank.IsDead)
                {
                    if (Vector3.Distance(transform.position, Match.instance.GetRebornPos(m_oppTank.Team)) < 30 && Time.time - time > 5)
                    {
                        if (Vector3.Distance(nearestStarPos, transform.position) < 10)
                        {
                            Move(nearestStarPos);
                            Attack();
                        }
                        else
                        {
                            //Vector3 targetPos = (Match.instance.GetRebornPos(m_oppTank.Team) / 2.7f - Match.instance.GetRebornPos(Team) / 2.7f);
                            //Move(targetPos);
                            Move(nearestStarPos);
                            Attack();
                        }
                    }
                    else
                    {
                        Move(nearestStarPos);
                        Attack();
                    }
                }
                if (!hasStar && HP > m_oppTank.HP)
                {
                    Move(m_oppTankPos);
                }
                if (!hasStar && HP < m_oppTank.HP)
                {
                    Move(Vector3.zero);
                }
            }
        }
        /// <summary>
        /// 是否能看见敌方坦克
        /// </summary>
        /// <returns></returns>
        bool CanSee()
        {
            RaycastHit m_hit;
            if (Physics.Linecast(transform.position, m_oppTankPos, out m_hit, PhysicsUtils.LayerMaskCollsion)&& PhysicsUtils.IsFireCollider(m_hit.collider))
                return true;
            else
                return false;
        }
        /// <summary>
        /// 预测敌方坦克的目标
        /// </summary>
        /*void CalculateOppTankTargetPos()
        {
            foreach (var s in Match.instance.GetStars())
            {
                float m_posToOppPos = Vector3.Distance(m_oppTankPos, transform.position);
                Star star = s.Value;
                if (star.IsSuperStar)
                {
                    m_oppTankTargetPos = star.transform.position;
                }
                if (!star.IsSuperStar && Vector3.Distance(m_oppTankPos, transform.position) < m_posToOppPos)//敌方坦克接近己方坦克,认为敌方坦克与己方坦克目标一致
                {
                    m_oppTankTargetPos = m_targetPos;
                }
                //目标坦克远离己方坦克，不进行预测
                if (!star.IsSuperStar && Vector3.Distance(m_oppTankPos, transform.position) >= m_posToOppPos)
                {
                    m_oppTankTargetPos = Vector3.up;
                }
            }
        }*/
        ///// <summary>
        ///// 比较敌方坦克与我方坦克据目标星星路径的距离(前提是双方目标相同)
        ///// </summary>
        ///// <param name="_mp"></param>
        ///// <param name="_op"></param>
        ///// <returns></returns>
        //bool CampareDistance(NavMeshPath _mp, NavMeshPath _op)
        //{
        //    NavMesh.CalculatePath(transform.position, m_targetPos, NavMesh.AllAreas, _mp);
        //NavMesh.CalculatePath(m_oppTankPos, m_targetPos, NavMesh.AllAreas, _op);
        //    float myPosToTargetDis = GetPathDistance(_mp);
        //    float oppPosToTargetDis = GetPathDistance(_op);
        //    if (myPosToTargetDis <= oppPosToTargetDis)
        //    {
        //        return true;
        //    }
        //    else
        //        return false;
        //}
       
        float CaculateAttack()
        {
            if (!m_oppTank.IsDead)
            {
                float dis = Vector3.Distance(m_oppTankPos, transform.position);
                if (dis <= 10)
                    return 0.1f;
                else
                {
                    return dis / 5;
                }
            }
            else
                return 0;
        }
        void Attack()
        {
            if (!m_oppTank.IsDead)
            {
                TurretTurnTo(m_oppTankPos + m_oppTank.Forward.normalized * CaculateAttack());
                if (CanSee())
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Match.instance.GetRebornPos(m_oppTank.Team));
                Fire();
            }
        }
        
        void GetStar()
            {
                hasStar = false;
                nearestDist = float.MaxValue;
                nearestStarPos = Vector3.zero;
                foreach (var pair in Match.instance.GetStars())
                {
                    Star s = pair.Value;
                    if (s.IsSuperStar)
                    {
                        hasStar = true;
                        nearestStarPos = s.Position;
                    return;
                    }
                    else
                    {
                        float dist = Vector3.Distance(s.transform.position, transform.position);
                        if (dist < nearestDist)
                        {
                            hasStar = true;
                            nearestDist = dist;
                            nearestStarPos = s.Position;
                        }
                    }
                }
                //NavMeshPath p1 = null;
                //NavMeshPath p2 = null;
                //int i = 0;
                //foreach (var pair in Match.instance.GetStars())
                //{
                //    if (i == 0 && s != null)
                //    {
                //        m_hasStar = true;
                //        s1 = s;
                //    }
                //    //若有两个星星将其按远近排序s1为近的
                //    if (i == 1 && s != null)
                //    {
                //        //m_navMeshAgent.CalculatePath()
                //        m_hasStar = true;
                //        m_navMeshAgent.CalculatePath(s1.transform.position, p1);
                //        m_navMeshAgent.CalculatePath(s.transform.position, p2);
                //        if (GetPathDistance(p1) > GetPathDistance(p2))
                //        {
                //            s1 = s;
                //        }
                //    }
                //    //若有3个星星则将第三个星星与前两个比较取最近的两个
                //    if (i == 2 && s != null)
                //    {
                //        m_hasStar = true;
                //        m_navMeshAgent.CalculatePath(s1.transform.position, p1);
                //        m_navMeshAgent.CalculatePath(s.transform.position, p2);
                //        if (GetPathDistance(p1) > GetPathDistance(p2))
                //        {
                //            s1 = s;
                //        }
                //    }
                //    i++;
                //}
            }
        ///// <summary>
        ///// 返回选择的星星路径
        ///// </summary>
        ///// <returns></returns>
        //NavMeshPath SlectStar()
        //{
        //    NavMeshPath p1=null;
        //    NavMeshPath p2 = null;
        //    GetNearestStar(transform.position);
         //NavMesh.CalculatePath();
         //NavMesh.CalculatePath(m_oppTankPos, s.transform.position, NavMesh.AllAreas, p1);//己方p
            //    NavMesh.CalculatePath(m_oppTankPos, s1.transform.position, NavMesh.AllAreas, p2);//敌方p
            //    if (CampareDistance(p1, p2))
            //    {
            //        return p1;
            //    }
            //    else
            //    {
            //        NavMesh.CalculatePath(m_oppTankPos, s2.transform.position, NavMesh.AllAreas, p1);
            //        return p1;
            //    }
            //}
            ///// <summary>
            ///// 获取两点间的寻路路径距离
            ///// </summary>
            ///// <param name="_path"></param>
            ///// <returns></returns>
            //    float GetPathDistance(NavMeshPath _path)
            //    {
            //        float dis=0;
            //        for (int i = 0; i < _path.corners.Length - 1; i++)
            //        {
            //            dis+= Vector3.Distance(_path.corners[i], _path.corners[i+1]);
            //        }
            //        Debug.Log(dis);
            //        return dis;
            //    }

            //}
            //    if(HP <= 50)
            //            {
            //                Move(Match.instance.GetRebornPos(Team));
            //}
        }
}
