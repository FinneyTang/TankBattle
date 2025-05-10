using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using AI.Base;
using AI.RuleBased;

namespace ZSJ2025
{
    class FullHP : Condition
    {
        Tank targetTank;
        public FullHP(Tank tank)
        {
            targetTank = tank;
        }
        public override bool IsTrue(IAgent agent)
        {
            return targetTank.HP >= 80;
        }
    }
    class MyHPHasAdvantage : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank MyTank = (Tank)agent;
            Tank EnemyTank = Match.instance.GetOppositeTank(MyTank.Team);
            return (MyTank.HP / 20) > (EnemyTank.HP / 20);
        }
    }

    class VeryCloseToEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank MyTank = (Tank)agent;
            Tank EnemyTank = Match.instance.GetOppositeTank(MyTank.Team);
            float Distance = Vector3.Distance(MyTank.Position, EnemyTank.Position);
            return Distance < 10 && MyTank.CanSeeOthers(EnemyTank);
        }
    }

    class EnemyFarFromHome : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank MyTank = (Tank)agent;
            Tank EnemyTank = Match.instance.GetOppositeTank(MyTank.Team);
            Vector3 enemyPosition = EnemyTank.Position;
            Vector3 enemyHomePosition = Match.instance.GetRebornPos(EnemyTank.Team);
            return (enemyPosition - enemyHomePosition).magnitude > 25;
        }
    }

    class OneMoreHitCanKill : Condition
    {
        Tank targetTank;
        public OneMoreHitCanKill(Tank tank)
        {
            targetTank = tank;
        }
        public override bool IsTrue(IAgent agent)
        {
            return targetTank.HP <= 20;
        }
    }

    class EnemyIsAlive : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank MyTank = (Tank)agent;
            Tank EnemyTank = Match.instance.GetOppositeTank(MyTank.Team);
            return !EnemyTank.IsDead;
        }
    }
    class HasStars : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.GetStars().Count != 0 && Match.instance.GetStars()!=null;
        }
    }
    class HasStarsNearHome : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank MyTank = (Tank)agent;
            Vector3 MyHomePos = Match.instance.GetRebornPos(MyTank.Team);
            float SafeZoneRadius = Vector3.Distance(MyHomePos, Vector3.zero);
            Dictionary<int, Star> stars = Match.instance.GetStars();
            if (stars == null || stars.Count == 0) return false;

            foreach(var star in stars)
            {
                if (Vector3.Distance(star.Value.Position, MyHomePos) < SafeZoneRadius) return true;
            }
            return false;
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

    class HasStarInSafeZone : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank tank = (Tank)agent;
            Vector3 myHomePos = Match.instance.GetRebornPos(tank.Team);
            float safeZoneRadius = Vector3.Distance(myHomePos, Vector3.zero);

            var stars = Match.instance.GetStars();
            if (stars == null || stars.Count == 0)
                return false;

            foreach (var star in stars.Values)
            {
                if (star == null) continue;

                float distToHome = Vector3.Distance(star.Position, myHomePos);
                if (distToHome <= safeZoneRadius)
                {
                    return true; // �ҵ�����һ���͹���
                }
            }

            return false;
        }
    }


    class MyTank : Tank
    {
        private void OnDrawGizmos()
        {
            if (m_MyTank == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m_MyTank.Position, 6f);
        }

        private Condition m_Chase;
        private Condition m_GetStarWhileEnemyIsAlive;
        private Condition m_GetStarWhileEnemyIsDead;
        private Condition m_GoHome;
        private Condition m_EnemyIsAlive;
        private Condition m_HasSuperStar;
        private Vector3 m_EnemyRebornPos;
        private Vector3 m_MyRebornPos;
        private Dictionary<int, Star> m_Stars;
        private int index = -1;
        Tank m_EnemyTank;
        Tank m_MyTank;

        protected override void OnStart()
        {
            base.OnStart();
            m_EnemyTank = Match.instance.GetOppositeTank(Team);
            
            if (Team == ETeam.A)
            {
                m_EnemyRebornPos = Match.instance.GetRebornPos(ETeam.B);
                m_MyRebornPos = Match.instance.GetRebornPos(ETeam.A);
                m_MyTank = Match.instance.GetTank(ETeam.A);
            }
            else
            {
                m_EnemyRebornPos = Match.instance.GetRebornPos(ETeam.A);
                m_MyRebornPos = Match.instance.GetRebornPos(ETeam.B);
                m_MyTank = Match.instance.GetTank(ETeam.B);
            }

            m_EnemyIsAlive = new EnemyIsAlive();

            Condition EnemyHasMoreHP = new NotCondition(new MyHPHasAdvantage());
            Condition EnemyAliveAndOneMoreHitCanKill = new AndCondition(m_EnemyIsAlive, new OneMoreHitCanKill(m_EnemyTank));
            Condition EnemyAliveButMeInAdv = new AndCondition(m_EnemyIsAlive, new MyHPHasAdvantage());

            m_Chase = new OrCondition(EnemyAliveAndOneMoreHitCanKill, new AndCondition(EnemyAliveButMeInAdv, new EnemyFarFromHome()));
            m_GetStarWhileEnemyIsAlive = new AndCondition(m_EnemyIsAlive, new HasStars());
            m_GetStarWhileEnemyIsDead = new AndCondition(new NotCondition(m_EnemyIsAlive), new HasStarInSafeZone());
            m_GoHome = new OrCondition(
                new AndCondition(EnemyHasMoreHP, new NotCondition(new FullHP(m_MyTank))),
                new AndCondition(new NotCondition(m_EnemyIsAlive), new NotCondition(new FullHP(m_MyTank)))
            );

            m_HasSuperStar = new HasSuperStar();
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            TurnTurret();
            
            if(m_HasSuperStar.IsTrue(this))
            {
                if (m_MyTank.HP > 40) Move(Vector3.zero);
            }
            else
            {
                if (m_Chase.IsTrue(this))
                {
                    //Debug.Log("׷��");
                    Move(m_EnemyTank.Position);
                    if (HasStarByMySide()) Move(Match.instance.GetStarByID(index).Position);
                }
                else if (m_GoHome.IsTrue(this))
                {
                    //Debug.Log("�ؼ�");
                    Move(m_MyRebornPos);
                    if (HasStarByMySide()) Move(Match.instance.GetStarByID(index).Position);
                }
                else if (m_GetStarWhileEnemyIsDead.IsTrue(this))
                {
                    if (m_MyTank.HP > 60)
                    {
                        //Debug.Log("ȫͼ��");
                        index = NearestStarIndex();
                        if (index != -1) Move(Match.instance.GetStarByID(index).Position);
                    }
                    else
                    {
                        //Debug.Log("�԰�ͼ");
                        index = NearestStarIndexInSafeZone();
                        if (index != -1) Move(Match.instance.GetStarByID(index).Position);
                    }
                }
                else if (m_GetStarWhileEnemyIsAlive.IsTrue(this))
                {
                    //Debug.Log("����1");
                    index = NearestStarIndex();
                    if (index != -1) Move(Match.instance.GetStarByID(index).Position);
                }
                else if(m_MyTank.HP < 77)
                {
                    Move(m_MyRebornPos);
                }
                else
                {
                    Move(Vector3.zero);
                }
            }
        }

        public bool HasStarByMySide()
        {
            m_Stars = Match.instance.GetStars();
            index = -1;  // �����Ƿ�Ϊ�գ������� index

            if (m_Stars == null || m_Stars.Count == 0)
                return false;

            float minDis = 6f;

            foreach (var star in m_Stars)
            {
                if (star.Value == null) continue;

                float curDis = Vector3.Distance(m_MyTank.Position, star.Value.Position);
                if (curDis < minDis)
                {
                    //minDis = curDis;
                    index = star.Key;
                    return true;
                }
            }

            return false;
        }

        public int NearestStarIndex()
        {
            m_Stars = Match.instance.GetStars();
            if (m_Stars == null || m_Stars.Count == 0)
                return -1;

            float minDis = float.MaxValue;
            int nearestStarID = -1;

            foreach (var star in m_Stars)
            {
                if (star.Value == null) continue;

                float curDis = Vector3.Distance(m_MyTank.Position, star.Value.Position);
                if (curDis < minDis)
                {
                    minDis = curDis;
                    nearestStarID = star.Key;
                }
            }

            return nearestStarID;
        }

        public int NearestStarIndexInSafeZone()
        {
            Vector3 myHomePos = Match.instance.GetRebornPos(m_MyTank.Team);
            float safeZoneRadius = Vector3.Distance(myHomePos, Vector3.zero);

            var stars = Match.instance.GetStars();
            if (stars == null || stars.Count == 0)
                return -1;

            float minDist = float.MaxValue;
            int nearestID = -1;

            foreach (var pair in stars)
            {
                var star = pair.Value;
                if (star == null) continue;

                float distToHome = Vector3.Distance(star.Position, myHomePos);
                if (distToHome <= safeZoneRadius)
                {
                    float distToMe = Vector3.Distance(star.Position, m_MyTank.Position);
                    if (distToMe < minDist)
                    {
                        minDist = distToMe;
                        nearestID = pair.Key;
                    }
                }
            }

            return nearestID;
        }

        private void TurnTurret()
        {
            if (m_EnemyTank != null && m_EnemyTank.IsDead == false)
            {
                Transform turret = this.transform.GetChild(1);
                Vector2 oppPosition = new Vector2(m_EnemyTank.Position.x, m_EnemyTank.Position.z);
                Vector2 oppVelocity = new Vector2(m_EnemyTank.Velocity.x, m_EnemyTank.Velocity.z);
                Vector2 myFirePosition = new Vector2(this.FirePos.x, this.FirePos.z);
                Vector2 deltaPosition = oppPosition - myFirePosition;
                float a = Mathf.Pow(oppVelocity.x, 2) + Mathf.Pow(oppVelocity.y, 2) - 1600;
                float b = 2 * (deltaPosition.x * oppVelocity.x + deltaPosition.y * oppVelocity.y);
                float c = Mathf.Pow(deltaPosition.x, 2) + Mathf.Pow(deltaPosition.y, 2);
                float delta = b * b - 4 * a * c;
                float predictedTime = (-b - Mathf.Sqrt(delta)) / (2 * a);
                Vector2 predictedPosition = deltaPosition + oppVelocity * predictedTime;
                Vector3 targetDirection = new Vector3(predictedPosition.x, 0, predictedPosition.y);
                turret.forward = Vector3.Lerp(turret.forward, targetDirection, Time.deltaTime * 180);
                if ((this.Position - m_EnemyTank.Position).magnitude < 15)
                {
                    this.Fire();
                }
                else if (Physics.SphereCast(this.FirePos, 0.24f, targetDirection, out RaycastHit hit,
                              (targetDirection - this.FirePos).magnitude - 2))
                {
                    FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                    if (fireCollider != null)
                    {
                        if (Vector3.Angle(this.TurretAiming, targetDirection) < 2)
                            this.Fire();
                    }
                }
                else
                {
                    if (Vector3.Angle(this.TurretAiming, targetDirection) < 2)
                        this.Fire();
                }
            }
            else
            {
                this.TurretTurnTo(m_EnemyRebornPos);
            }
        }


        public override string GetName()
        {
            return "ZSJ2025";
        }
    }
}
