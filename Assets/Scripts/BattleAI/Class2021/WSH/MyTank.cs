using Main;
using UnityEngine;
using UnityEngine.AI;

namespace WSH
{
    class MyTank : Tank
    {
        Vector3 preTarget = Vector3.zero;
        private float m_LastTime = 0;
        Tank oppTank;
        protected override void OnStart()
        {
            base.OnStart();
            oppTank = Match.instance.GetOppositeTank(Team);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            Move();
            Attack();


        }
        void Move()
        {
            if (Match.instance.RemainingTime <= 180 * 0.5f + 5.0f && Match.instance.RemainingTime >= 180 * 0.5f)
            {
                Move(new Vector3(0, 0, 0));
            }
            else
            {
                //if (HP <= 25 && (!oppTank.IsDead))//����25Ѫ�ҶԷ����ʱ
                //{
                //    Move(Match.instance.GetRebornPos(Team));
                //}
                //else
                //{
                    bool hasStar = false;
                    float nearestDist = float.MaxValue;
                    Vector3 nearestStarPos = Vector3.zero;//��ʼ�� ����û������ ����ľ���Ϊ��Զֵ ����������ڣ�0��0����
                    foreach (var pair in Match.instance.GetStars())//foreach����ÿ������
                    {
                        Star s = pair.Value; //unknown
                        if (s.IsSuperStar)//��������ǳ�������
                        {
                            hasStar = true;//�����жϸ�Ϊtrue
                            nearestStarPos = s.Position;//�������λ�ø�Ϊ�������ǣ���ζ�ų����������ȼ�������ͨ����
                            break;
                        }
                        else
                        {
                            float dist = (s.Position - Position).sqrMagnitude;//dist��������ǵ�̹�˵ľ���
                            if (dist < nearestDist)//�����Ǿ��빻Сʱ
                            {
                                hasStar = true;
                                nearestDist = dist;
                                nearestStarPos = s.Position;//����ǰ������̹�˾����ΪnearestDist���ҽ����������λ�ø�Ϊ��ǰ����
                            }
                        }
                    }//�����������ǽ���
                    if (hasStar == true)//������������ʱ���ж�Ѫ���Լ������Ǻͼҵľ�������ƶ�
                    {
                        if((HP<=50 && Vector3.Distance(Position,Match.instance.GetRebornPos(Team))<=nearestDist) ||
                           (HP<=25 && Vector3.Distance(Position, Match.instance.GetRebornPos(Team)) * 0.7f <= nearestDist))
                        {
                            Move(Match.instance.GetRebornPos(Team));
                        }
                        else Move(nearestStarPos);//�ƶ���nearestStar
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
                //}
            }
        }
        void Attack()
        {
            if (oppTank != null)
            {
                float distance = Vector3.Distance(oppTank.Position, Match.instance.GetOppositeTank(oppTank.Team).Position);
                float pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
                preTarget = oppTank.Position + oppTank.Velocity * pTime;//�����ӵ����ٶ��Լ��з�̹���ƶ����ٶȼ����ӵ������
                for (int i = 0; i < 2; i++)
                {
                    distance = Vector3.Distance(Match.instance.GetOppositeTank(oppTank.Team).Position, preTarget);
                    pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
                    preTarget = oppTank.Position + oppTank.Velocity * pTime;
                }
                TurretTurnTo(preTarget);//ת��Ԥ���
                Vector3 direction = (preTarget - Position).normalized;
                if (Vector3.Dot(TurretAiming, direction) > 0.99f && !Physics.Linecast(Position, preTarget, PhysicsUtils.LayerMaskCollsion))
                {
                    Fire();
                }

            }
            if (oppTank.IsDead)
            {
                TurretTurnTo(Match.instance.GetRebornPos(oppTank.Team));
                Vector3 toEnHome = Match.instance.GetRebornPos(oppTank.Team) - FirePos;
                toEnHome.y = 0;
                toEnHome.Normalize();
                if (Vector3.Dot(TurretAiming, toEnHome) > 0.99f && !Physics.Linecast(Position, Match.instance.GetRebornPos(oppTank.Team), PhysicsUtils.LayerMaskCollsion))
                {
                    Fire();
                }
            }
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()
        {
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(0, 0, 0));
        }
        public override string GetName()
        {
            return "WSH";
        }
    }
}
