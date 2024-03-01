                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     using System.Collections;
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace YCG
{
    class ArtificialIdiotA : Tank
    {
       
        private float m_LastTime = 0;
        protected override void OnUpdate()
        {
            bool HaveSuperStar = false;
            Vector3 SuperPos=Vector3.zero;
            base.OnUpdate();
            FindStar(2);
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    HaveSuperStar = true;
                    SuperPos = s.Position;
                }
            }  
            if (HaveSuperStar)
            {
                Tank oppTank = Match.instance.GetOppositeTank(Team);
                RaycastHit hitInfo;
                if (Physics.Linecast(FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMaskCollsion))
                {
                    if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                    {
                        Fight(oppTank);
                    }
                }
                Move(SuperPos);
            }
            else
            {
                Tank oppTank = Match.instance.GetOppositeTank(Team);
                if (oppTank != null)
                {
                    bool seeOthers = false;
                    TurretTurnTo(oppTank.Position);
                    Condition backhome = new HpBelow(HP, oppTank.HP, 30);
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
                        if (Vector3.Distance(Position, oppTank.Position) < 16.0f)
                        {
                            FindStar(1);
                            Fight(oppTank);
                        }
                        else
                        {
                            Miss();
                            Fight(oppTank);
                        }  
                        
                    }
                    else
                    {
                        if (backhome.Istrue() && !seeOthers)
                        {
                            Move(Match.instance.GetRebornPos(Team));
                        }
                        else
                        {
                            FindStar(2);
                        }
                    }
                }
            } 
        }
        public void Fight(Tank Opp)
        {
                Vector3 toTarget = Opp.Position - FirePos; 
                toTarget.y = 0;
                toTarget.Normalize();
                if (Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                {
                    Fire();
                }
        }
        public void Miss()
        {
            float nearestMissile = float.MaxValue;
            Vector3 nearestMissilePos = Vector3.zero;
            Vector3 a=Vector3.zero;
            bool can = false;
            foreach (var pair in Match.instance.GetOppositeMissiles(Team))
            {
                Missile p = pair.Value;
                if (Vector3.Distance(p.Position, Position) < 5.0f)
                {
                    float dist = (p.Position - Position).sqrMagnitude;
                    if (dist < nearestMissile)
                    {
                        can = true;
                        nearestMissile = dist;
                        nearestMissilePos = p.Position;
                        a = p.Velocity;
                    }
                }
                
            }

            //if (Mathf.Abs(Vector3.Dot(Position, a)) < 0.6f)
            //{
            //    Move(Position + 5.0f * (Position - nearestMissilePos).normalized);
            //}
            //else
            if (can)
            {
                Vector3 next = Quaternion.AngleAxis(90, Position) * a;
                Move(Position + next.normalized * 7.0f);
            }
               
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.25f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }

        public override string GetName()
        {
            return "YCG";
        }
        public class Condition
        {
            public virtual bool Istrue()
            {
                return true;
            }
        }
        public class HpBelow : Condition
        {
            private int m_self;
            private int m_opp;
            private int m_minHP;
            public HpBelow(int self, int opp, int minHP)
            {
                m_self = self;
                m_opp = opp;
                m_minHP = minHP;
            }
            public override bool Istrue()
            {
                return (m_self < m_opp&& m_self< m_minHP);
            }
        }
        public class EatStar_Fighting : Condition
        {
            private Tank m_self;
            private Vector3 m_star;
            public EatStar_Fighting(Tank self, Vector3 star)
            {
                m_self = self;
                m_star = star;
            }
            public override bool Istrue()
            {
                return (Mathf.Abs(Vector3.Dot(m_self.Position, m_star))< 0.7f);
            }
        }
        public void FindStar(int Type)
        {
            Condition eat=new Condition();
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
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
            switch (Type)
            {
                case 1:
                    eat = new EatStar_Fighting(this, nearestStarPos);
                    break;
                case 2:
                    eat = new Condition();
                    break;
            }
            if (hasStar == true&& eat.Istrue())
            {
                Move(nearestStarPos);
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
        }
    }
}
 
