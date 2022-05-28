using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using UnityEngine.AI;
using System;
using AI.RuleBased;
using AI.Base;

namespace CK
{
    class ConditionCanSeePosition : Condition
    {
        private Vector3 positon;
        public ConditionCanSeePosition(Vector3 p)
        {
            positon = p;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return !Physics.Linecast(t.Position, positon, PhysicsUtils.LayerMaskScene);
        }
    }

    class ConditionAimAtPosition : Condition {
        private Vector3 aimTargetVector;
        public ConditionAimAtPosition(Vector3 t)
        {
            aimTargetVector = t;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return Vector3.Dot(t.TurretAiming, aimTargetVector) > 0.99f;
        }
    }

    class ConditionAtHome : Condition {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return Vector3.Distance(Match.instance.GetRebornPos(t.Team), t.Position) < Match.instance.GlobalSetting.HomeZoneRadius;
        }
    }

    class HPLarger : Condition
    {
        private int m_targetHP;
        public HPLarger(int targetHP)
        {
            m_targetHP = targetHP;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return t.HP >= m_targetHP;
        }
    }

    class ConditionCantDodgeDis : Condition {
        private Vector3 targetPos;
        private float threshold;
        public ConditionCantDodgeDis(float t,Vector3 pos)
        {
            threshold = t;
            targetPos = pos;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return Vector3.Distance(t.Position, targetPos) < threshold;
        }
    }


    public class MyTank : Tank
    {
        Match match;
        Tank enemy;
        Vector3 preEnemyPos;
        Vector3 m_prePos;
        Vector3 moveTarget;
        Vector3 dodgePos;
        List<Action> actionlist = new List<Action>();

        public struct Action : IComparable<Action>
        {
            private Vector3 target;
            private float weight;

            public Action(Vector3 t, float w)
            {
                this.target = t;
                this.weight = w;
            }

            public int CompareTo(Action other)
            {
                if (weight > other.weight)
                {
                    return -1;
                }
                else if (weight < other.weight)
                {
                    return 1;
                }
                return 0;
            }

            public Vector3 GetTarget() { return target; }
        }

        public override string GetName()
        {
            return "CK";
        }
        //fire update
        public void FireTo()
        {
            TurretTurnTo(preEnemyPos);
            //看到敌人且炮塔对准预测位置
            Condition m_Fire = new AndCondition(
                new ConditionCanSeePosition(preEnemyPos),
                new ConditionAimAtPosition((preEnemyPos - Position).normalized)
                );
            if (m_Fire.IsTrue(this))
                Fire();
        }

        //move update更新actions序列，执行当前优先度最大的
        public void MoveTo()
        {
            actionlist.Sort();
            moveTarget = actionlist[0].GetTarget();
            Move(moveTarget);
        }


        int maxMissileKey = 0;
        //获取躲避坐标	
        public void SetDodgePos()
        {
            //如果在家，不用躲
            if (new ConditionAtHome().IsTrue(this))
            {
                return;
            }
            //血量大于敌人，且很近，换血
            if (new HPLarger(enemy.HP).IsTrue(this) && 
                new ConditionCantDodgeDis(8f,enemy.Position).IsTrue(this) && 
                new ConditionCanSeePosition(enemy.Position).IsTrue(this))
            {
                actionlist.Add(new Action(enemy.Position, float.MaxValue));
            }


            var missiles = match.GetOppositeMissiles(Team);
            Missile missile;
            missiles.TryGetValue(maxMissileKey, out missile);
            foreach (var m in missiles)
            {
                if (m.Key > maxMissileKey)
                {
                    maxMissileKey = m.Key;
                    missile = m.Value;
                    dodgePos = Vector3.zero;
                }
                if (new ConditionCantDodgeDis(8f,m.Value.Position).IsTrue(this))
                {
                    continue;
                }
                //能成功躲避
                if (Vector3.Dot((dodgePos - m.Value.Position).normalized, m.Value.Velocity.normalized) < 0.95f
                    && Vector3.Dot((m_prePos - m.Value.Position).normalized, m.Value.Velocity.normalized) < 0.95f
                    && dodgePos != Vector3.zero)
                {
                    break;
                }

                //子弹射到预测位置
                if (Vector3.Dot((m_prePos - m.Value.Position).normalized, m.Value.Velocity.normalized) > 0.96f
                    && !Physics.Linecast(m_prePos, m.Value.Position, PhysicsUtils.LayerMaskScene)
                    && Velocity.magnitude > 5f)
                {
                    dodgePos = Vector3.zero;
                    for (int i = 8; i <= 16; i=i+2)
                    {
                        dodgePos = Vector3.Cross(Vector3.Cross(Velocity.normalized, (m.Value.Position - m_prePos).normalized).y> 0.01f ? Vector3.down : Vector3.up
                            , m.Value.Velocity).normalized * i + m_prePos;
                        if (CaculatePath(dodgePos) != null)
                        {
                            actionlist.Add(new Action(dodgePos, float.MaxValue));
                            break;
                        }
                    }
                    break;
                }
                else if (Vector3.Dot((Position - m.Value.Position).normalized, m.Value.Velocity.normalized) > 0.96f
                    &&  new ConditionCanSeePosition(m.Value.Position).IsTrue(this))
                {
                    dodgePos = Vector3.zero;
                    for (int i = 8; i <= 16; i+=2)
                    {
                        dodgePos = Vector3.Cross(
                            Vector3.Cross(Velocity.normalized, (m.Value.Position - Position).normalized).y > 0.01f ? Vector3.up : Vector3.down
                            , m.Value.Velocity).normalized * i + Position;
                        if (CaculatePath(dodgePos) != null)
                        {
                            actionlist.Add(new Action(dodgePos, float.MaxValue));
                            break;
                        }
                    }
                }
            }

            if (missile == null || Vector3.Dot((Position - missile.Position).normalized, missile.Velocity.normalized) < 0.9f)
            {
                dodgePos = Vector3.zero;
            }
        }

        //get predict pos
        Vector3 SetPrePos(Tank tank)
        {
            if (tank.IsDead)
                return match.GetRebornPos(tank.Team);
            
            float dis = Vector3.Distance(match.GetOppositeTank(tank.Team).Position, tank.Position);
            float time = dis / match.GlobalSetting.MissileSpeed;
            return  tank.Position + tank.Velocity * time;

        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            Gizmos.DrawWireSphere(preEnemyPos, 4f);
            Gizmos.DrawWireSphere(m_prePos, 2f);
            Gizmos.DrawWireCube(actionlist[0].GetTarget(), Vector3.one * 3);
        }

        float CalcU(Vector3 target)
        {
            NavMeshPath path = CaculatePath(target);
            float pathLength = 0.0f;
            for (int i = 0; i < path.corners.Length - 1; ++i)
            {
                pathLength += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            }

            //设置效用函数
            if (Vector3.Distance(target, match.GetRebornPos(Team)) < match.GlobalSetting.HomeZoneRadius + 10f)
            {
                return (100 - HP) * (115 - Mathf.Clamp(pathLength,0,115));
            }
            else
                return HP * (115 - Mathf.Clamp(pathLength, 0, 115));

        }

        void PushAction()
        {
            actionlist.Clear();
            actionlist.Add(new Action(match.GetRebornPos(Team), CalcU(match.GetRebornPos(Team))));
            var stars = match.GetStars();
            if(stars != null)
                foreach (var star in stars)
                {
                    if (star.Value.IsSuperStar)
                        actionlist.Add(new Action(star.Value.Position, float.MaxValue));
                    else
                    {
                        actionlist.Add(new Action(star.Value.Position, CalcU(star.Value.Position)));
                    }
                }
            else
                actionlist.Add(new Action(Vector3.zero, Mathf.Epsilon));
        }

        protected override void OnStart()
        {
            match = Match.instance;
            enemy = match.GetOppositeTank(Team);
  
            base.OnStart();
        }

        protected override void OnUpdate()
        {
            preEnemyPos = SetPrePos(enemy);
            m_prePos = SetPrePos(this);
            PushAction();
            SetDodgePos();


            FireTo();
            MoveTo();
            base.OnUpdate();
        }
    }

}


