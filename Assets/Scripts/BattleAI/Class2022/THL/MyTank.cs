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
    class EnemyInHome                                       //�ж϶����Ƿ��ڼһ�Ѫ
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



    class nearStar                                 //�õ����������
    {
       public Vector3 Pos = new Vector3();
    }
        
    class MyTank : Tank
    {
        EnemyInHome EinHome = new EnemyInHome();
        Condition CanSeeEnemy = new HasSeenEnemy();
        nearStar nearStar = new nearStar();
        bool canGetStar = false;
        float deadTime = 0;                              //�Ե��˵�����ʱ����м�ʱ
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
            double DisToStar = Match.instance.FieldSize * 1.4;
            if (!canGetStar)
            {
                Move(Match.instance.GetRebornPos(this.Team));                 //�Գ�����Ϊ��ҪĿ��滮�ж�
            }
            if (canGetStar)
            {
                Move(nearStar.Pos);
            }
            if (OppTank.IsDead)                                             //���Է���������ʼ��ʱ������ʱ��滮�ж�
            {
                deadTime += Time.fixedDeltaTime;
                if (deadTime<=6)
                {
                    if (selfTank.HP > 40||Vector3.Magnitude(selfTank.Position-Match.instance.GetRebornPos(this.Team))< Match.instance.FieldSize)
                    {
                        canGetStar = true;
                        foreach (var st in Match.instance.GetStars())   //����Է���������ʱ����㣬��һֱ������
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
                if (deadTime > 6&&selfTank.HP<93)      //���Է���Ҫ����ʱ�ؼҲ�Ѫ
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
                deadTime = 0;                                           //����������ʱ
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
                        if (s.Value.IsSuperStar || selfTank.HP - OppTank.HP >= -20)                   //��Ϊ�������ǣ����ҷ����Ʋ�����ȥ���ᳬ�����ǣ�
                        {
                            canGetStar = true;
                            nearStar.Pos = s.Value.Position;
                        }
                        if (Vector3.Magnitude(s.Value.Position - selfTank.Position) < DisToStar)      //���ǳ������ǣ������Ѫ��ѡ��ؼһ��ǳ����������
                        {
                            nearStar.Pos = s.Value.Position;
                            if (selfTank.HP >= 50)
                            {
                                canGetStar = true;
                            }
                            else if (selfTank.HP >= 25 && Vector3.Magnitude(s.Value.Position - selfTank.Position) < 5f)  //����ڻؼҵ�·��������÷ǳ��������ǣ���˳�ֳԵ�
                            {
                                canGetStar = true;
                            }
                            else if (selfTank.HP < 50)        //Ѫ������ؼ�
                            {
                                canGetStar = false;
                            }


                        }
                    }
                    else if (s.Value == null)
                    {
                        if (selfTank.HP <=60)      //�������û�����ǣ��Լ���Ѫ������50����ؼ�
                        {
                            canGetStar = false;
                        }
                        else
                        {
                            Move(new Vector3(Random.Range(-Match.instance.FieldSize/2, Match.instance.FieldSize / 2), 0, Random.Range(-Match.instance.FieldSize / 2, Match.instance.FieldSize / 2)));
                        }
                    }
                }
                

                
                
            }
        }
        protected override void OnReborn() //����
        {
            base.OnReborn();
        }

        public override string GetName()
        {
            return "THL";
        }
    }

}


