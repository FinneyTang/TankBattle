using AI.Base;
using AI.RuleBased;
using FSM;
using HQX;
using JetBrains.Annotations;
using Main;
using System;
using System.Collections.Generic;
using System.Reflection;
using TSH;
using UnityEngine;
using UnityEngine.AI;

namespace LYM
{
    class IsEnemyDeak : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank ltank = (Tank)agent;
            Tank eTank = Match.instance.GetOppositeTank(ltank.Team);
            if(eTank.IsDead)
                return true;
            return false;
        }
    }
    class CanSeeEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank ltank = (Tank)agent;
            Tank eTank = Match.instance.GetOppositeTank(ltank.Team);
            if (ltank.CanSeeOthers(eTank))
                return true;
            return false;
        }
    }
    class NeedDodgeBullet : Condition
    {
        Calculate _calculate;
        public NeedDodgeBullet(Calculate calculate)
        {
            _calculate = calculate;
        }
        public override bool IsTrue(IAgent agent)
        {
            if (_calculate.CheckNeedDodge())
            {
                return true;
            }
            return false;
        }
    }
    class IsWillGoHome :  Condition
    {
        // 判断是否要回家<40
        public override bool IsTrue(IAgent agent)
        {
            Tank ltank = (Tank)agent;

            int t_hp = ltank.HP;
            if (t_hp <= 40)
            {
                return true;
            }
            return false;
        }
    }
    class isHomeGoOut : Condition 
    {
        //判断是否要出去>80
        public override bool IsTrue(IAgent agent)
        {
            Tank ltank = (Tank)agent;
            int t_hp = ltank.HP;
            if (t_hp >= 80)
            {
                return true;
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
    class HasSuperStarTime : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            if (Match.instance.RemainingTime >= 100)
                return false;
            if (Match.instance.RemainingTime <= 88)
                return false;
            else return true;
        }
    }
    class HasNearGoodPosition : Condition
    {
        Calculate _calculate;
        public HasNearGoodPosition(Calculate calculate)
        {
           _calculate = calculate;
        }
        public override bool IsTrue(IAgent agent)
        {
            if(_calculate.goodPosition.goodPosition1 ==null)
                return false;
            if(_calculate.goodPosition.goodPosition2 == null)
                return false;
            return true;
        }
    }
    class GoodHomeStar : Condition
    {
        Calculate _calculate;
        public GoodHomeStar(Calculate calculate)
        {
            _calculate = calculate;
        }
        public override bool IsTrue(IAgent agent)
        {
            Vector3 starPosition = Match.instance.GetStarByID(_calculate.getNearStar).Position;
            if (
                (Vector3.Distance(starPosition, _calculate.tankMyL.ltank.Position) <=15)
                && _calculate.tankMyL.ltank.CanSeeOthers(starPosition)
                )
                return true;
            return false;
        }
    }
    class HasNearStar : Condition
    {
        Calculate _calculate;
        public HasNearStar(Calculate calculate)
        {
            _calculate = calculate;
        }
        public override bool IsTrue(IAgent agent)
        {
            ETeam eTeam = _calculate.tankMyL.etank.Team;
            Vector3 starPosition = Match.instance.GetStarByID(_calculate.getNearStar).Position;
            if(Vector3.Distance(starPosition,_calculate.goodPosition.goodPosition1) <23)
                return true;
            if (Vector3.Distance(starPosition , _calculate.goodPosition.goodPosition2) < 23)
                return true;
            if (Vector3.Distance(starPosition, _calculate.goodPosition.NearHome) < 40)
                return true;
            if (Vector3.Distance(starPosition, _calculate.tankMyL.ltank.transform.position) < 30)
                return true;
            if (Vector3.Distance(starPosition, Match.instance.GetRebornPos(eTeam)) < 15)
                return false;

            return false;
        }
    }
   /* class CanGetNearStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            if (!agent.TryGetCalculate(out Calculate calculate))
                return false;

            Debug.Log(calculate.getNearStar);
            if (calculate.getNearStar < 0)
                return false;
            
            return true;
        }
    }*/
    public class Calculate//获得好位置
    {
        public float dodgeDistance = 8.1f;
        public Vector3 dodgeDirection; // 新增全局方向存储
        public GoodPosition goodPosition;
        public StarInMap starInMap;
        public L_Tank tankMyL;
        public Vector3 centre;
        public float R1 = 12f;
        public float R2;
        //获得最近星星
        public int getNearStar = -1;
        //超级星星
        public bool isSuperStarGenerate;
        public void StartCalculate()
        {
            centre = new Vector3(0, 0, 0);
            starInMap = new StarInMap();
            goodPosition = new GoodPosition();
            tankMyL = new L_Tank();
        }
        public void InitCalculate(ETeam team)
        {
            goodPosition.InitGoodPosition(team);
            tankMyL.InitTank(team);
            isSuperStarGenerate = false;
        }
        public void UpdateCalculate()
        {
            NearStar();
        }
        public Vector3 GetGoodPlace()
        {
            float disGoodone = Vector3.Distance(tankMyL.ltank.transform.position, goodPosition.goodPosition1);
            float disGoodtwo = Vector3.Distance(tankMyL.ltank.transform.position, goodPosition.goodPosition2);
            if (disGoodone < disGoodtwo)
                return goodPosition.goodPosition1;
            if (disGoodone >= disGoodtwo)
                return goodPosition.goodPosition2;

            return  tankMyL.ltank.transform.position;
        }
        public bool NearStar()
        {
            int countStar = Match.instance.GetStars().Count;
            if (countStar > 0)
            {
                Vector3 tankPosition = tankMyL.ltank.Position;
                float mindis = 999;
                getNearStar = -1;
                foreach (var pair in Match.instance.GetStars())
                {
                    if (mindis > (tankMyL.ltank.Position - pair.Value.Position).magnitude)
                    {
                        mindis = (tankMyL.ltank.Position - pair.Value.Position).magnitude;
                        getNearStar = pair.Key;
                    }
                }
            }
            return countStar > 0;
        }
        public bool CheckNeedDodge()
        {
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(tankMyL.ltank.Team);
            foreach (var pair in missiles)
            {
                Missile m = pair.Value;
                Vector3 toTank = tankMyL.ltank.Position - m.Position;
                if (toTank.sqrMagnitude > 144) continue; 
                Vector3 cross = Vector3.Cross(m.Velocity, toTank);
                dodgeDirection = Vector3.Cross(m.Velocity, Vector3.up).normalized;
                if (cross.y > 0) dodgeDirection *= -1;
                return true;
            }
            return false;
        }
    }
    public class GoodPosition //双点巡逻基础
    {
        public ETeam myTeam;
        public Vector3 NearHome;
        public Vector3 goodPosition1;
        public Vector3 goodPosition2;
        public void InitGoodPosition(ETeam team)
        {
            GetInformation(team);
            GetGoodPosition();
        }
        public void GetInformation(ETeam team)
        {
            myTeam = team;
        }
        public void GetGoodPosition()
        {
            if (myTeam == ETeam.A)
            {
                goodPosition1 = new Vector3(21, 0, 19);
                goodPosition2 = new Vector3(-15, 0, -34);
                NearHome = new Vector3(21, 0, -20);
            }
            if (myTeam == ETeam.B)
            {
                goodPosition1 = new Vector3(9.9f, 0, 39.4f);
                goodPosition2 = new Vector3(-30, 0, -20);
                NearHome = new Vector3(-30, 0, 30);
            }
        }
    }
    public class StarInMap//星星基础改
    {
        public bool isSuperStar = false;
        public int isSuperStarID = -1;
    }
    public class L_Tank
    {
        public Tank ltank;
        public Tank etank;
        public void InitTank(ETeam team)
        {
            ltank  = Match.instance.GetTank(team);
            etank = Match.instance.GetOppositeTank(team);
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
    class MyTank : Tank
    {
        //private float m_LastTime = 0;
        private bool m_health = true;
        private bool fullhealth = false;
        private Calculate m_calculate;
        //3 rules
        private Condition m_getSuperStar;
        private Condition m_dodgeBullet;//躲避子弹s
        private Condition m_goHome;
        private Condition m_goOut;
        private Condition m_getNearStar;
        private Condition m_hasGoodPosition;
        private Condition m_goHomeStar;
        public Condition test;

        protected override void OnStart()
        {

            base.OnStart();

            m_calculate = new Calculate();
            m_calculate.StartCalculate();
            m_calculate.InitCalculate(Team);
            //初始化设置
            m_getSuperStar = new HasSuperStarTime();
            m_goHome = new IsWillGoHome();
            m_goOut = new AndCondition(
                new NotCondition(m_goHome),
                new isHomeGoOut()
                );
            test = new IsWillGoHome();
            m_hasGoodPosition = new HasNearGoodPosition(m_calculate);
            m_dodgeBullet = new AndCondition(new CanSeeEnemy(),new NeedDodgeBullet(m_calculate));
            m_getNearStar = new OrCondition(new HasNearStar(m_calculate), new IsEnemyDeak());
            m_goHomeStar = new GoodHomeStar(m_calculate);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            m_calculate.UpdateCalculate();
            TankFireCalculate();
            //没xue直接回家
            if (m_getSuperStar.IsTrue(this))
            {//直接写强逻辑
                if(!fullhealth)
                    Move(Match.instance.GetRebornPos(Team));
                if(m_calculate.tankMyL.ltank.HP >= 95)
                    fullhealth  = true;
                if (fullhealth)
                {
                    if (m_dodgeBullet.IsTrue(this))
                    {
                        PerformDodge();
                    } else
                    if(Match.instance.RemainingTime<=98&& Match.instance.RemainingTime >= 88)
                    { 
                        Move(Vector3.zero);
                    }
                }
            }
            else if (m_goHome.IsTrue(this))
            {
                if (m_goHomeStar.IsTrue(this))
                {
                    Move(Match.instance.GetStarByID(m_calculate.getNearStar).Position);
                }
                else
                {
                    Move(Match.instance.GetRebornPos(Team));
                    m_health = false;
                }
            }
            else if (!m_health)//不健康，残血回家后的行为
            {
                if (m_goOut.IsTrue(this))
                    m_health = true;
            }
            else if (m_health)
            {
                if (m_dodgeBullet.IsTrue(this))
                {
                    PerformDodge();
                }
                else if (m_getNearStar.IsTrue(this))
                {
                    Move(Match.instance.GetStarByID(m_calculate.getNearStar).Position);
                }
                else if (m_hasGoodPosition.IsTrue(this))
                {
                    Move(m_calculate.GetGoodPlace());
                }
            }

        }
        public void PerformDodge()
        {
            m_calculate.tankMyL.ltank.Move(m_calculate.tankMyL.ltank.Position +m_calculate.dodgeDirection * m_calculate.dodgeDistance);
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            //m_LastTime = 0;
        }
       private void TankFireCalculate()
{
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && !oppTank.IsDead)
            {
                Transform turret = transform.GetChild(1);
                Vector3 firePos = FirePos; // 炮口位置（Y=3）
                Vector3 enemyPos = new Vector3(oppTank.Position.x, firePos.y, oppTank.Position.z);
                Vector3 enemyVel = oppTank.Velocity;
                Vector2 vel = new Vector2(enemyVel.x, enemyVel.z);
                Vector2 deltaPos = new Vector2(enemyPos.x - firePos.x, enemyPos.z - firePos.z);
                float a = vel.sqrMagnitude - 1600f; // 40^2=1600
                float b = 2 * Vector2.Dot(deltaPos, vel);
                float c = deltaPos.sqrMagnitude;
                if (Mathf.Abs(a) > 0.001f)
                {
                    float discriminant = b * b - 4 * a * c;
                    if (discriminant >= 0)
                    {
                        float sqrtDelta = Mathf.Sqrt(discriminant);
                        float predictedTime = (-b - sqrtDelta) / (2 * a);
                        if (predictedTime > 0)
                        {
                            Vector3 predictedPos = new Vector3(enemyPos.x + vel.x * predictedTime, firePos.y, enemyPos.z + vel.y * predictedTime);
                            Vector3 targetDir = (predictedPos - firePos).normalized;
                            targetDir.y = 0;
                            turret.forward = Vector3.Lerp(turret.forward,targetDir,Time.deltaTime * 180);
                            float distanceThreshold = 14 * 14;
                            if ((transform.position - enemyPos).sqrMagnitude < distanceThreshold)
                                Fire();
                            else
                            {
                                RaycastHit hit;
                                float castDistance = Vector3.Distance(firePos, predictedPos) - 2f;
                                if (Physics.SphereCast(firePos,0.23f,targetDir,out hit,castDistance))
                                {
                                    if (hit.collider.GetComponent<FireCollider>() != null && Vector3.Angle(turret.forward, targetDir) < 2f)
                                        Fire();
                                }
                                else if (Vector3.Angle(turret.forward, targetDir) < 2.1f)
                                    Fire();
                            }
                            return;
                        }
                    }
                }

                // 预测失败时瞄准当前位置（保持Y轴同高）
                Vector3 fallbackDir = (enemyPos - firePos).normalized;
                fallbackDir.y = 0;
                turret.forward = Vector3.Lerp(turret.forward, fallbackDir, Time.deltaTime * 180);
                if (Vector3.Angle(turret.forward, fallbackDir) < 2f)
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
}
        public override string GetName()
        {
            return "LYM";
        }
    }
}
