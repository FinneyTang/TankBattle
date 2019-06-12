using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using UnityEngine.AI;

namespace LTY
{

    abstract class Condition
    {
        public abstract bool IsTrue(Tank owner);
    }

    class TrueCondition : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            return true;
        }
    }

    class FalseCondition : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            return false;
        }
    }

    class AndCondition : Condition
    {
        private Condition m_lhs;
        private Condition m_rhs;
        public AndCondition(Condition lhs, Condition rhs)
        {
            m_lhs = lhs;
            m_rhs = rhs;
        }
    
        public override bool IsTrue(Tank owner)
        {
            return m_lhs.IsTrue(owner) && m_rhs.IsTrue(owner);
        }
    }

    class OrCondition : Condition
    {
        private Condition m_lhs;
        private Condition m_rhs;
        public OrCondition(Condition lhs, Condition rhs)
        {
            m_lhs = lhs;
            m_rhs = rhs;
        }
        public override bool IsTrue(Tank owner)
        {
            return m_lhs.IsTrue(owner) || m_rhs.IsTrue(owner);
        }
    }

    class XorCondition : Condition
    {
        private Condition m_lhs;
        private Condition m_rhs;
        public XorCondition(Condition lhs, Condition rhs)
        {
            m_lhs = lhs;
            m_rhs = rhs;
        }
        public override bool IsTrue(Tank owner)
        {
            return m_lhs.IsTrue(owner) ^ m_rhs.IsTrue(owner);
        }
    }

    class NotCondition : Condition
    {
        private Condition m_lhs;
        public NotCondition(Condition lhs)
        {
            m_lhs = lhs;
        }
        public override bool IsTrue(Tank owner)
        {
            return !m_lhs.IsTrue(owner);
        }
    }

    class HPBelow : Condition
    {
        private int m_targetHP;
        public HPBelow(int targetHP)
        {
            m_targetHP = targetHP;
        }
        public override bool IsTrue(Tank owner)
        {
            return owner.HP <= m_targetHP;
        }
    }

    class HasSuperStar : Condition
    {
        
        //int m_score = 0;
        //int m_oppScore = 0;
        bool m_isSuperEaten = false;
        public override bool IsTrue(Tank owner)
        {
            if (!m_isSuperEaten && Match.instance.GlobalSetting.MatchTime / 2 + 6f > Match.instance.RemainingTime)
            {
                if(Match.instance.GlobalSetting.MatchTime / 2 > Match.instance.RemainingTime)
                {
                    foreach (var pair in Match.instance.GetStars())
                    {
                        Star s = pair.Value;
                        if(s.IsSuperStar==true)
                            return true;
                    }
                    m_isSuperEaten = true;
                }
                else
                {
                    return true;
                }
            }
            return false;
        }
        
    }

    class HasStar : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            return Match.instance.GetStars().Count > 0;
        }
    }

    class HasSeeEnemy : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            Tank oppTank = Match.instance.GetOppositeTank(owner.Team);
            if (oppTank == null) return false;
            bool seeOthers = false;
            RaycastHit hitInfo;
            if (Physics.Linecast(owner.FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                {
                    seeOthers = true;
                }
            }
            return seeOthers;
        }
    }

    class HasEnemyBackHome : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            Tank oppTank = Match.instance.GetOppositeTank(owner.Team);
            Vector3 destination = oppTank.NextDestination;
            destination.y = 0;
            return (destination - Match.instance.GetRebornPos(oppTank.Team)).sqrMagnitude < 0.01f;
        }
    }

    class HasEnemyDead : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            Tank oppTank = Match.instance.GetOppositeTank(owner.Team);
            return oppTank.IsDead && oppTank.GetRebornCD(Time.time) < Match.instance.GlobalSetting.RebonCD / 3;
        }
    }

    class HasOppMissileNear : Condition
    {
        private float m_detectDistance;
        public HasOppMissileNear(float detectRadius)
        {
            m_detectDistance = detectRadius * detectRadius;
        }
        public override bool IsTrue(Tank owner)
        {
            float distanceBetweenTanks = (owner.Position - Match.instance.GetOppositeTank(owner.Team).Position).sqrMagnitude;
            foreach (var missile in Match.instance.GetOppositeMissiles(owner.Team))
            {
                float missileDistance = (missile.Value.Position - owner.Position).sqrMagnitude;
                if (missileDistance < m_detectDistance)
                {
                    if (distanceBetweenTanks > 35f)
                        return true;
                }
            }
            return false;
        }
    }

    class HasBackHome : Condition
    {
        public override bool IsTrue(Tank owner)
        {
            RuleBasedTank ltyTank = (RuleBasedTank)owner;
            float starDistance = (ltyTank.GetNearestStar() - ltyTank.Position).sqrMagnitude;
            float homeDistance = (ltyTank.Position - Match.instance.GetRebornPos(ltyTank.Team)).sqrMagnitude;
            Debug.Log("Star Distance: " + starDistance);
            Debug.Log("Home Distance: " + homeDistance);
            if(homeDistance<starDistance/3)
            {
                return true;
            }
            return false;
        }
    }



    public class RuleBasedTank : Tank
    {
        private Condition m_getStar;
        private Condition m_getSuperStar;
        //private Condition m_fire;
        //private Condition m_backToHome;
        private Condition m_oppTankDead;
        private Condition m_oppMissileNear;

        private float m_LastTime = 0;
        private float m_tankMoveSpeed = 10f;
        private Vector3 m_lastOppTankPosition;
        private Vector3 minOppDeadPos = new Vector3(10, 0, 5);
        private Vector3 maxOppDeadPos = new Vector3(13, 0, 30);

        public float missileDetectRadius = 28f;

        protected override void OnStart()
        {
            base.OnStart();
            //m_backToHome = new AndCondition(new HPBelow(50), new AndCondition(new NotCondition(new HasSuperStar()), new HasBackHome()));
            //m_fire = new HasSeeEnemy();
            m_getSuperStar = new HasSuperStar();
            m_oppTankDead = new HasEnemyDead();
            m_oppMissileNear = new AndCondition(new HasSeeEnemy(), new HasOppMissileNear(missileDetectRadius));
            m_getStar = new AndCondition(new NotCondition(m_oppMissileNear)
                , new NotCondition(m_getSuperStar));
            m_lastOppTankPosition = Match.instance.GetOppositeTank(Team).Position;

            //Reborn B
            if (Match.instance.GetRebornPos(Match.instance.GetOppositeTank(Team).Team).x < 0)
            {
                minOppDeadPos = -minOppDeadPos;
                maxOppDeadPos = -maxOppDeadPos;
            }
        }


        protected override void OnUpdate()
        {
            base.OnUpdate();
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            //对敌方坦克运动方向预判
            Vector3 toCurrOffset = oppTank.Position - m_lastOppTankPosition;
            toCurrOffset.y = 0;
            Vector3 toNextDesOffset = oppTank.NextDestination - oppTank.Position;
            toNextDesOffset.y = 0;
            float length = Vector3.Dot(toCurrOffset.normalized, toNextDesOffset.normalized) *
                (oppTank.Position - m_lastOppTankPosition).magnitude / (Time.deltaTime * m_tankMoveSpeed) *
                Vector3.Distance(oppTank.Position, Position) / Match.instance.GlobalSetting.MissileSpeed * m_tankMoveSpeed;
            //炮管对准的目标
            Vector3 toTarget;
            if (m_oppTankDead.IsTrue(this))
            {
                toTarget = Match.instance.GetRebornPos(oppTank.Team);
            }
            else
            {
                toTarget = oppTank.Position + toNextDesOffset.normalized * length;
            }
            if (oppTank != null)
            {
                TurretTurnTo(toTarget);
            }
            //开火
            if (CanSeeEnemy(toTarget)&&HasTurretRotateToTarget(this, toTarget - FirePos))
            {
                Fire();
            }

            //垂直敌方导弹方向移动
            if (m_oppMissileNear.IsTrue(this))
            {
                Move(GetAvoidMissilePos(GetNearestMissile().Velocity));
                Debug.Log("Avoid Missile");
            }
            //敌方坦克死亡，走到固定点阻击
            //if (m_oppTankDead.IsTrue(this))
            //{
            //    Move(maxOppDeadPos);
            //}
            //回家补血
            //if (m_backToHome.IsTrue(this))
            //{
            //    Move(Match.instance.GetRebornPos(Team));
            //    //Debug.Log("Back  Home:" + Time.frameCount);
            //}
            //吃超级星星
            if (m_getSuperStar.IsTrue(this))
            {
                Move(Vector3.zero);
                //Debug.Log("Super Star:" + Time.frameCount);
            }
            //吃星星
            if (m_getStar.IsTrue(this))
            {
                Move(GetNearestStar()); 
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
            m_lastOppTankPosition = oppTank.Position;
        }

        public override string GetName()
        {
            return "LTY";
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
            m_lastOppTankPosition = Match.instance.GetOppositeTank(Team).Position;
        }
        private bool ApproachNextDestination()
        {
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }

        public Vector3 GetNearestStar()
        {
            float nearestDistance = float.MaxValue;
            Vector3 nearestPosition = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                float dis = (s.Position - Position).sqrMagnitude;
                if (dis < nearestDistance)
                {
                    nearestDistance = dis;
                    nearestPosition = s.Position;
                }
            }
            return nearestPosition;
        }

        private bool HasTurretRotateToTarget(Tank owner, Vector3 toTarget)
        {
            toTarget.y = 0;
            toTarget.Normalize();
            if (Vector3.Dot(owner.TurretAiming, toTarget) > 0.98f)
            {
                return true;
            }
            return false;
        }

        private Missile GetNearestMissile()
        {
            int nearestID = int.MaxValue;
            foreach (var missile in Match.instance.GetOppositeMissiles(Team))
            {
                if(missile.Key<nearestID)
                {
                    nearestID = missile.Key;
                }
            }
            return Match.instance.GetOppositeMissiles(Team)[nearestID];
        }

        private Vector3 GetAvoidMissilePos(Vector3 missileVelocity)
        {
            Vector3 normal = Vector3.Cross(Vector3.up, missileVelocity).normalized;
            RaycastHit hit_1, hit_2;
            float hit1Distance = 0, hit2Distance = 0;
            if(Physics.Linecast(Position, Position + normal * 1000, out hit_1, PhysicsUtils.LayerMaskScene))
            {
                hit1Distance = (hit_1.point - Position).sqrMagnitude;
            }
            if( Physics.Linecast(Position, Position - normal * 1000, out hit_2, PhysicsUtils.LayerMaskScene))
            {
                hit2Distance = (hit_2.point - Position).sqrMagnitude;
            }
            
            if (hit1Distance > hit2Distance)
            {
                return Position + normal * (hit_1.point - Position).magnitude * .8f;
            }
            return Position + normal * (hit_2.point - Position).magnitude * .8f;
        }

        private bool CanSeeEnemy(Vector3 endPos)
        {
            bool seeOthers = true;
            RaycastHit hitInfo;
            if (Physics.Linecast(FirePos, endPos, out hitInfo, PhysicsUtils.LayerMaskScene))
            {
                seeOthers = false;
            }
            return seeOthers;
        }
    }
}
