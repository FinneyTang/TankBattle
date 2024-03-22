using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
//真*人工智障，我感觉我的ai应该是今年最菜的

namespace HYT
{
    public class MyTank : Tank
    {
        enum State
        {
            Nothing,//什么都没有
            LifeAttack,//攻击敌人
            LookStar,//追星星
            LookEnemy,//追敌人
            Rest//休息

        }
        Tank tankMine;//自己的坦克
        Tank tankEnemy;//敌人的坦克
        State state;//当前模式
        bool isE;//在躲避
        Vector3 Born;//出生点
        int roll;

        protected override void OnAwake()
        {
            base.OnAwake();
        }
        protected override void OnStart()
        {
            base.OnStart();
            state = State.LookStar;
            isE = false;
            roll = 1;
            tankMine = Match.instance.GetTank(Team);
            tankEnemy = Match.instance.GetOppositeTank(Team);
            Born = this.Position;//复活时为重生点
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Debug.Log(isStar());
            Debug.Log(state);
            TurretTurnTo(tankEnemy.Position);//时刻瞄准敌人
            Doing();
        }

        protected override void OnReborn()
        {
            base.OnReborn();
        }
        public override string GetName()
        {
            return "HYT";
        }

        public void Doing()
        {
            switch (state)
            {
                case State.Nothing:
                    break;
                case State.LookStar:
                    nearStar();
                    break;
                case State.LifeAttack:
                    lifeAttack();
                    break;
                case State.Rest:
                    rest();
                    break;
                case State.LookEnemy:
                    lookenemy();
                    break;
            }
        }
        public bool isHPOK()
        {
            return ((tankMine.HP > tankEnemy.HP) || (tankMine.HP == tankEnemy.HP));
        }
        public bool isStar()
        {
            return (Match.instance.GetStars() != null);
        }
        public bool isEnemy()
        {
            return (tankEnemy != null);
        }

        public void nearStar()//向最近的星星走去
        {
            var star = Match.instance.GetStars();
            float minpath = 100000f;
            Star min = null;
            foreach (KeyValuePair<int, Star> s in star)
            {
                float now = Vector3.Distance(this.transform.position, s.Value.transform.position);
                if (now < minpath)
                {
                    minpath = now;
                    min = s.Value;
                }
            }
            if (min != null)
            {
                Move(min.Position);
            }
            if (CanSeeOthers(tankEnemy)) state = State.LifeAttack;
            if (this.HP != 100 && tankEnemy.HP == 100) { state = State.Rest; }
            if (!isStar())
            {
                if (tankEnemy.IsDead || this.HP != 100) { state = State.Rest; }
                else { state = State.LookEnemy; }
            }
        }

        public void lifeAttack()//死战
        {
            float lasttime = Time.time;//储存上次开炮时间
            Fire();//开火
            //开火判断
            if (!CanFire() && !isE)//当不能开火时
            {
                Elude();
            }
            else
            {
                isE = false;
                lasttime = Time.time;
            }
            //切换阶段判断
            if (!CanSeeOthers(tankEnemy))
            {
                var star = Match.instance.GetStars();
                float minpath = 100000f;
                Star min = null;
                foreach (KeyValuePair<int, Star> s in star)
                {
                    float now = Vector3.Distance(this.transform.position, s.Value.transform.position);
                    if (now < minpath)
                    {
                        minpath = now;
                        min = s.Value;
                    }
                }
                if (!tankEnemy.IsDead && isStar())
                {
                    if (Vector3.Distance(this.Position, min.Position) < Vector3.Distance(this.Position, tankEnemy.Position))
                    {
                        state = State.LookStar;
                    }
                    else
                    {
                        state = State.LookEnemy;
                    }
                }


            }
            //死亡判断
            if (tankEnemy.IsDead)
            {
                state = State.LookStar;
                isE = false;
            }
            else if (this.IsDead)
            {
                state = State.LookStar;
                isE = false;
            }
        }
        public void Elude()//开火间隙的躲避
        {
            if (roll == 0)
            {
                Move(Born);
            }
            else
            {
                if (!isStar())
                {
                    roll = 0;
                }
                else
                {
                    var star = Match.instance.GetStars();
                    float minpath = 100000f;
                    Star min = null;
                    foreach (KeyValuePair<int, Star> s in star)
                    {
                        float now = Vector3.Distance(this.transform.position, s.Value.transform.position);
                        if (now < minpath)
                        {
                            minpath = now;
                            min = s.Value;
                        }
                    }
                    if (min != null)
                    {
                        Move(min.Position);
                    }
                }
            }
            Debug.Log("正在躲避");
            isE = true;
        }
        public void rest()//休息
        {
            Move(Born);//回家休息
            if (this.HP == 100)
            {
                state = State.LookStar;
            }
            if (CanSeeOthers(tankEnemy) && this.HP > 70)
            {
                state = State.LifeAttack;
            }
        }
        public void lookenemy()
        {
            Move(tankEnemy.Position);
            if (CanSeeOthers(tankEnemy)) { state = State.LifeAttack; }
        }
    }

}

