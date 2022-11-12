using Main;
using UnityEngine;
using AI.RuleBased;
using AI.Base;

namespace THL
{
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
    class EnemyInHome                                       //判断对面是否在家回血
    {
        
        public bool IsTrue(Tank tank)
        {
            Tank Enemy = tank;
            Vector3 EHome = Match.instance.GetRebornPos(Enemy.Team);
            float DisToHome = Vector3.Magnitude(Enemy.Position - EHome);
            if (DisToHome <= 3f)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }



    class nearStar                                 //得到最近的星星
    {
       public Vector3 Pos = new Vector3();
    }
        
    class MyTank : Tank
    {
        EnemyInHome EinHome = new EnemyInHome();
        Condition CanSeeEnemy = new HasSeenEnemy();
        nearStar nearStar = new nearStar();
        bool canGetStar = false;
        float deadTime = 0;                              //对敌人的死亡时间进行计时
        //Missile escapeWay = new Missile();
        protected override void OnStart()
        {

        }
        protected override void OnUpdate()
        {
            
            Tank OppTank = Match.instance.GetOppositeTank(Team);
            Tank selfTank = this;
            float AftOppTankHP = OppTank.HP;
            base.OnUpdate();
            double DisToStar = PhysicsUtils.MaxFieldSize * 1.4;
            if (!canGetStar)
            {
                Move(Match.instance.GetRebornPos(this.Team));                 //以吃星星为主要目标规划行动
            }
            if (canGetStar)
            {
                Move(nearStar.Pos);
            }
            if (OppTank.IsDead)                                             //若对方死亡，则开始计时，根据时间规划行动
            {
                deadTime += Time.fixedDeltaTime;
                if (deadTime<=6)
                {
                    if (selfTank.HP > 40||Vector3.Magnitude(selfTank.Position-Match.instance.GetRebornPos(this.Team))< PhysicsUtils.MaxFieldSize)
                    {
                        canGetStar = true;
                        foreach (var st in Match.instance.GetStars())   //如果对方死亡，且时间充足，则一直吃星星
                        {
                            if (st.Value != null)
                            {
                                nearStar.Pos = st.Value.Position;
                            }
                        }
                    }
                    else
                    {
                        Move(Match.instance.GetRebornPos(this.Team));                     
                    }
                }
                if (deadTime > 6&&selfTank.HP<93)      //当对方快要复活时回家补血
                {
                    canGetStar = false;
                }
                else
                {
                    canGetStar = true;
                }
                
            }
            else
            {
                deadTime = 0;                                           //重置死亡计时
                if (EinHome.IsTrue(OppTank) == false)
                {
                    if (CanSeeEnemy.IsTrue(OppTank))
                    {
                        if (OppTank.Velocity.magnitude < 4f)
                        {
                            TurretTurnTo(OppTank.Position);
                        }
                        else
                        {
                            TurretTurnTo(OppTank.Position + OppTank.Velocity / 4);
                        }
                        Fire();
                    }
                    else
                    {                  
                        TurretTurnTo(OppTank.Position + OppTank.Velocity / 4);
                    }

                }
                else if (EinHome.IsTrue(OppTank))
                {
                    if (CanSeeEnemy.IsTrue(OppTank))
                    {

                        Move(Match.instance.GetRebornPos(this.Team));
                    }
                    else
                    {
                        TurretTurnTo(OppTank.Position + OppTank.Velocity / 4);
                        if (selfTank.HP > 50)
                        {
                            canGetStar = true;
                        }
                        else
                        {
                                canGetStar = false;
                        }
                    }
                }
                foreach (var s in Match.instance.GetStars())
                {
                    if (s.Value != null)
                    {
                        if (s.Value.IsSuperStar || selfTank.HP - OppTank.HP >= -20)                   //若为超级星星，且我方劣势不大，则去争夺超级星星；
                        {
                            canGetStar = true;
                            nearStar.Pos = s.Value.Position;
                        }
                        if (Vector3.Magnitude(s.Value.Position - selfTank.Position) < DisToStar)      //不是超级星星，则根据血量选择回家还是吃最近的星星
                        {
                            nearStar.Pos = s.Value.Position;
                            if (selfTank.HP >= 50)
                            {
                                canGetStar = true;
                            }
                            else if (selfTank.HP >= 25 && Vector3.Magnitude(s.Value.Position - selfTank.Position) < 5f)  //如果在回家的路上遇到离得非常近的星星，则顺手吃掉
                            {
                                canGetStar = true;
                            }
                            else if (selfTank.HP < 50)        //血量低则回家
                            {
                                canGetStar = false;
                            }


                        }
                    }
                    else if (s.Value == null)
                    {
                        if (selfTank.HP <=60)      //如果场上没有星星，自己的血量低于50，则回家
                        {
                            canGetStar = false;
                        }
                        else
                        {
                            Move(new Vector3(Random.Range(-PhysicsUtils.MaxFieldSize/2, PhysicsUtils.MaxFieldSize / 2), 0, Random.Range(-PhysicsUtils.MaxFieldSize / 2, PhysicsUtils.MaxFieldSize / 2)));
                        }
                    }
                }
                

                
                
            }
        }
        protected override void OnReborn() //重生
        {
            base.OnReborn();
        }

        public override string GetName()
        {
            return "THL";
        }
    }

}


