using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
//��*�˹����ϣ��Ҹо��ҵ�aiӦ���ǽ�����˵�

namespace HYT
{
    public class MyTank : Tank
    {
        enum State
        {
            Nothing,//ʲô��û��
            LifeAttack,//��������
            LookStar,//׷����
            LookEnemy,//׷����
            Rest//��Ϣ

        }
        Tank tankMine;//�Լ���̹��
        Tank tankEnemy;//���˵�̹��
        State state;//��ǰģʽ
        bool isE;//�ڶ��
        Vector3 Born;//������
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
            Born = this.Position;//����ʱΪ������
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Debug.Log(isStar());
            Debug.Log(state);
            TurretTurnTo(tankEnemy.Position);//ʱ����׼����
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

        public void nearStar()//�������������ȥ
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

        public void lifeAttack()//��ս
        {
            float lasttime = Time.time;//�����ϴο���ʱ��
            Fire();//����
            //�����ж�
            if (!CanFire() && !isE)//�����ܿ���ʱ
            {
                Elude();
            }
            else
            {
                isE = false;
                lasttime = Time.time;
            }
            //�л��׶��ж�
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
            //�����ж�
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
        public void Elude()//�����϶�Ķ��
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
            Debug.Log("���ڶ��");
            isE = true;
        }
        public void rest()//��Ϣ
        {
            Move(Born);//�ؼ���Ϣ
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

