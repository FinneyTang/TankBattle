using AI.Base;
using AI.RuleBased;
using Main;
using UnityEngine;

namespace ZSC
{

    class HPBelow : Condition
    {
        private int m_TargetHP;
        public HPBelow(int targetHP)
        {
            m_TargetHP = targetHP;
        }
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            return t.HP <= m_TargetHP;
        }
    }

    class HasSuperStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            
            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value.IsSuperStar)
                {
                    return true;
                }
            }
            return false;
        }
    }

    class HasStar : Condition
    {
        public override bool IsTrue(IAgent owner)
        {
            if (Match.instance.GetStars() != null)
            {
                return true;
            }
            return false;
        }
    }

    class HasSeenEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null)
            {
                return false;
            }
            return t.CanSeeOthers(oppTank);
        }
    }

    class FireBlock : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            RaycastHit hitInfo;
            if (Physics.Linecast(t.FirePos, oppTank.Position, out hitInfo, PhysicsUtils.LayerMaskScene))
            {
                return true;
            }
            return false;
        }
    }


    class MyTank : Tank
    {
        public float preDis=2;

        //private float m_MovInterval = 0;
        private float m_LastTime = 0;
        private Star star;
        private int prestarID;
        private bool movFin = true;
        private bool backhome = false;
        private Vector3 rebornpos;


        //4 rules
        private Condition m_GetSuperStar;
        private Condition m_GetStar;
        private Condition m_Fire;
        private Condition m_BackToHome;


        protected override void OnStart()
        {
            base.OnStart();
            m_GetSuperStar = new HasSuperStar();
            m_GetStar = new HasStar();
            m_Fire = new AndCondition(new HasSeenEnemy(), new NotCondition(new FireBlock()));
            m_BackToHome = new AndCondition(new HPBelow(30),new NotCondition(new HasSuperStar()));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Tank myTank = (Tank)this;
            rebornpos = Match.instance.GetRebornPos(myTank.Team);
            if (Match.instance.RemainingTime > 90 && Match.instance.RemainingTime < 95)
            {
                Move(Vector3.zero);
            }
            else if (!backhome)
            {
                if (m_GetSuperStar.IsTrue(this))
                {
                    Move(Vector3.zero);
                }
                else if (m_GetStar.IsTrue(this))
                {
                    if (movFin == true)
                    {
                        star = SearchNearStar(this);
                    }
                    if (star != null)
                    {
                        Move(star.Position);
                        movFin = false;
                    }
                }
                if (m_BackToHome.IsTrue(this))
                {
                    backhome = true;
                }
                if (Time.time > m_LastTime)
                {

                    m_LastTime = Time.time + 5;
                    Move(Vector3.zero + new Vector3(1, 0, 1) * Random.Range(-8, 8));
                }
                if (star == null)
                {
                    movFin = true;
                }
                if (movFin == false && (Vector3.Distance(star.Position, myTank.Position) < 1f || prestarID != star.ID))
                {
                    movFin = true;
                    prestarID = star.ID;
                }
            }
            else
            {
                if (m_GetSuperStar.IsTrue(this))
                {
                    Move(Vector3.zero);
                }
                if (Vector3.Distance(myTank.Position, oppTank.Position) < 65 && Vector3.Distance(myTank.Position, rebornpos) < 1f)
                {
                    backhome = false;
                }
                if (star != null)
                {
                    if (movFin && Vector3.Distance(Match.instance.GetRebornPos(Team), myTank.Position) < Vector3.Distance(star.Position, myTank.Position))
                    {
                        Move(Match.instance.GetRebornPos(Team));
                        if (HP > 48)
                        {
                            backhome = false;
                        }
                    }
                }
                else
                {
                    Move(Match.instance.GetRebornPos(Team));
                    if (HP > 48)
                    {
                        backhome = false;
                    }
                }
            }

            ///fire

            if (oppTank != null && Vector3.Dot(myTank.Forward, oppTank.Forward) > 0.3)
            {
                TurretTurnTo(oppTank.Position + oppTank.Forward * Vector3.Magnitude(oppTank.Velocity) * 0.3f);
            }
            else
            {
                TurretTurnTo(oppTank.Position + oppTank.Forward * Vector3.Magnitude(oppTank.Velocity) * 0.6f);
            }
            if (m_Fire.IsTrue(this))
            {
                Fire();
            }

        }
        protected override void OnReborn()
        {
            base.OnReborn();
            movFin = true;
            backhome = false;
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }

        private Star SearchNearStar(IAgent agent)
        {
            Star s = null;
            float dis = 0;
            Tank owner = (Tank)agent;
            foreach (var pair in Match.instance.GetStars())
            {
                if(dis<Vector3.SqrMagnitude(owner.Position - pair.Value.Position)&& Vector3.SqrMagnitude(rebornpos-pair.Value.Position)<9000)
                {
                    dis = Vector3.SqrMagnitude(owner.Position - pair.Value.Position);
                    s = pair.Value;
                }
            }
            return s;
        }
  
        public override string GetName()
        {
            return "ZSC";
        }
    }
}
