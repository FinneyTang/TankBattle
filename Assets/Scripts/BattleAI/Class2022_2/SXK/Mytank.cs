using AI.FiniteStateMachine;
using Main;
using UnityEngine;

namespace SXK

{
    enum EStateType//�ĳ���������
    {
         FindStar, BackToHome
    }
   
    class BackToHomeState : State
    {
        
        public BackToHomeState()
        {
            StateType = (int)EStateType.BackToHome;
            
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
        
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);//һֱ����
            if (oppTank != null && oppTank.IsDead == false)
            {
                t.TurretTurnTo(oppTank.Position);
            }
            else
            {
                t.TurretTurnTo(t.Position + t.Forward);
            }
            t.Fire();
            //bool hasStar = false;
            float nearestDist = float.MaxValue;
            //Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {

                    //hasStar = true;
                    //nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        //hasStar = true;
                        nearestDist = dist;
                        //nearestStar = s;
                        Debug.Log(nearestDist);
                    }
                }
            }
            if (nearestDist<=500)//�ػ����н���������ȥ��
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
           
                if (t.HP >75)//�ܿ����¾ͳ���
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(Match.instance.GetRebornPos(t.Team));
            return this;
        }
    }
    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)EStateType.FindStar;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);//һֱ����
            if (oppTank != null && oppTank.IsDead == false)
            {
                t.TurretTurnTo(oppTank.Position);
            }
            else
            {
                t.TurretTurnTo(t.Position + t.Forward);
            }
            t.Fire();
            
            
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {

                    hasStar = true;
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStar = s;
                        //Debug.Log(nearestDist);
                    }
                }
            }
            
            if (t.HP <= 25 && (hasStar == false || nearestStar.IsSuperStar == false)&& nearestDist>500)//Ѫ������һ���Ҹ���û���ǲŻؼ�
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
                
            }
            if (System.Math.Abs(Match.instance.RemainingTime-100)<1 && t.HP <= 75)//�������ǳ����Ȼؼ�׼����״̬
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
            if (hasStar == true)
            {
                t.Move(nearestStar.Position);
            }
            return this;
        }
    }
    public class MyTank : Tank
    {
        private StateMachine m_FSM;
        protected override void OnStart()
        {
            base.OnStart();
            m_FSM = new StateMachine(this);
           
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new FindStarState());
            m_FSM.SetDefaultState((int)EStateType.FindStar);
        }
        protected override void OnUpdate()
        {
            
           
           
            m_FSM.Update();
        }
        public override string GetName()
        {
            return "SXKTank";
        }
    }
}