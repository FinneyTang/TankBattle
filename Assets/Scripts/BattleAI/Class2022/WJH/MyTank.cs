using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using System;

namespace WJH
{ 
 class MyTank : Tank
 {
    Match Match_Now;
    Tank Enemy;
    Vector3 NowPos, EnemyPos_Now, EnemyPos_Last, MyPos;
        bool isEscape =false;
        bool GoBackHome = false;
    public override string GetName()
    {
        return "WJH";
    }
    protected override void OnStart()
    {
        base.OnStart();
        Match_Now = Match.instance;
        Enemy = Match_Now.GetOppositeTank(Team);
            EnemyPos_Now = Enemy.Position;
            MyPos = this.Position;

        //enemyTank = match.GetOppositeTank(Team);
        }
    protected override void OnUpdate()
    {
        base.OnUpdate();
            EnemyPos_Now = Enemy.Position;
            //Debug.LogError(isEscape);

            Fire(EnemyPos_Now);
            //判断星的位置 ，哪个玩家要移动的路线更久。如果对方更近。就向敌方方向移动、反则立即前往
            Escape();

            if (Match_Now.GetStars()!=null)
            {
                foreach (var item in Match_Now.GetStars())
                {

                    if (item.Value.IsSuperStar)
                    {
                        Move(CaculatePath(item.Value.Position));
                    }
                    else
                    {
                        if (Vector3.Distance(this.Position, item.Value.Position) <= Vector3.Distance(EnemyPos_Now, item.Value.Position) && isEscape == false && GoBackHome == false)
                        {
                            //Debug.Log(" MAX_NearStar");
                            Move(CaculatePath(item.Value.Position));
                        }
                        else
                        {
                            if (GoBackHome == false)
                            {
                                NearStar();
                                //Debug.Log(" NearStar");
                                Move(CaculatePath(NowPos));
                            }
                            else
                                NowPos = Match.instance.GetRebornPos(Team);
                        }

                    }


                }
            }
            else if (isEscape == false && GoBackHome==false)
            {
               // Debug.Log("MoveToEnemy");
                Move(CaculatePath(Enemy.Position));
            }

            if (isEscape==true)
            {
                Move(CaculatePath(NowPos));
            }
            
            ToHome();

            EnemyPos_Last = Enemy.Position;
        }



        private void Escape( )
        {
            foreach (var item in Match.instance.GetOppositeMissiles(this.Team))
            {
               

                if ( item.Value!=null)
                {
                    if (Vector3.Distance(item.Value.Position, Position) < 5 /*&& Vector3.Dot(item.Value.Velocity.normalized, this.Forward.normalized) >= 0 && Vector3.Dot(item.Value.Velocity.normalized, this.Forward.normalized) <= 1 && CanSeeOthers(item.Value.Position)*/)
                    {
                        //Debug.LogError(item.Value.Position);
                        isEscape = true;
                        
                        NowPos = new Vector3(item.Value.Velocity.z*3, Position.y, item.Value.Velocity.x*3).normalized  ;
                        //Debug.Log(NowPos);
                    }
                   
                    
                }
                else
                {
                    //Debug.Log("item.Value==null");
                    isEscape = false;
                }

            }

        }

        Vector3 AimfirePos(Vector3 firePos)
        {
            if (Enemy.IsDead)
            {
                return Match_Now.GetRebornPos(Enemy.Team);
            }
            else
            {
                float disten = Vector3.Distance(EnemyPos_Now, Position);
                 float MoveTime = disten / Match.instance.GlobalSetting.MissileSpeed;
                //float EnemyMoveDisten = (MoveTime * Enemy.Forward).magnitude ;

                Vector3 MovePosition = MoveTime * Velocity +Position;

                float Disten = Vector3.Distance(EnemyPos_Now, MovePosition);

                float fireTime = Disten / Match.instance.GlobalSetting.MissileSpeed;
                firePos = fireTime*Enemy.Velocity+ Enemy.Position;
                //firePos =
            }

            return firePos;
        }


        private void Fire(Vector3 firePos)
        {
            Vector3 direction = (EnemyPos_Now - EnemyPos_Last).normalized;
            firePos += direction * 2;
            TurretTurnTo(AimfirePos(firePos));
            if (CanSeeOthers(firePos) &&  this.IsDead==false )
            {
                Fire();
            }
        }


        private void NearStar ()
        {
            float distenMin = float.MaxValue;
            Vector3 StarNear = NowPos;
            foreach (var item in Match_Now.GetStars())
                {

                float disten = (item.Value.transform.position - Position).sqrMagnitude;

                if (disten< distenMin)
                {
                    distenMin = disten;
                    StarNear = item.Value.transform.position;
                }
   
                }


            NowPos = StarNear;
        }



        private void ToHome()
        {
            if (HP <= 50)
            {
                GoBackHome = true;
                //Debug.LogError("HOME");
                if (isEscape == false)
                {
                    NowPos = Match.instance.GetRebornPos(Team);
                }

            }
            else
                GoBackHome = false;
        }

        protected override void OnReborn()
    {
        base.OnReborn();
            isEscape = false;
       
    }


    //// Start is called before the first frame update
    //void Start()
    //{

    //}

    //// Update is called once per frame
    //void Update()
    //{

    //}
 }
}
