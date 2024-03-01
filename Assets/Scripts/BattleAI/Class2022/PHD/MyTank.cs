using AI.FiniteStateMachine;
using Main;
using UnityEngine;

namespace PHD
{
     enum EStateType
    {
        FindEnemy, FindStar, BackToHome,GoCenter
    }
    class GoCenterState:State
    {
          public GoCenterState(){
           StateType=(int)EStateType.GoCenter;
       }
       public override State Execute(){
           bool hasStar = false;
           bool hasSuperStar=false;
           Star nearestStar = null;
           Star SuperStar = null;
           float nearestDist = float.MaxValue;
           Tank t=(Tank)Agent;
           Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    hasSuperStar = true;
                    SuperStar = s;
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
                    }
                }
            }
            if (hasStar||hasSuperStar){
                Debug.Log("star");
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            if ((!hasStar||!hasSuperStar)&&t.HP<=25){
                Debug.Log("home");
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }
             if ((!hasStar&&!hasSuperStar)&&t.CanSeeOthers(oppTank)){
                Debug.Log("enemy");
                return m_StateMachine.Transition((int)EStateType.FindEnemy);
            }
             
            t.Move(new Vector3(0,0,0));
            Debug.Log("center");
            return this;
       }
    }
    class FindEnemyState:State
    {
       public FindEnemyState(){
           StateType=(int)EStateType.FindEnemy;
       }
       public override State Execute()
       {
           Tank t=(Tank)Agent;
           if(t.HP<=26)
           {
                Debug.Log("home");
                return m_StateMachine.Transition((int)EStateType.BackToHome);
           }
           Tank oppTank=Match.instance.GetOppositeTank(t.Team);
           
           if (oppTank == null || oppTank.IsDead||t.CanSeeOthers(oppTank)==false){
                Debug.Log("star");
                return m_StateMachine.Transition((int)EStateType.FindStar);
           }
           if (oppTank.Position== Match.instance.GetRebornPos(oppTank.Team))
            {
                Debug.Log("center");
                return m_StateMachine.Transition((int)EStateType.GoCenter);
            }
            t.Move(oppTank.Position);
            Debug.Log("enemy");
            return this;
       }
    }

    class BackToHomeState : State{
        public BackToHomeState()
        {
            StateType = (int)EStateType.BackToHome;
        }
           public override State Execute()
        {
            Tank t = (Tank)Agent;
            if (t.HP >= 50)
            {
                Debug.Log("star");
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(Match.instance.GetRebornPos(t.Team));
            Debug.Log("home");
            return this;
        }
    }

    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)EStateType.FindStar;
        }
        public override State Execute() { 
        Tank t = (Tank)Agent;
        Tank oppTank = Match.instance.GetOppositeTank(t.Team);
          bool hasStar = false;
            bool hasSuperStar = false;
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            Star SuperStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    hasSuperStar = true;
                    SuperStar = s;
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
                    }
                }
            }
             if (hasSuperStar)
            {
                t.Move(SuperStar.Position);
                Debug.Log("star");
                return this;
            }
            if (t.HP <= 40 && !t.CanSeeOthers(oppTank))
            {
                if (hasStar)
                {
                    float DisToStar = Vector3.Distance(nearestStar.Position, t.Position);
                    Vector3 HomePos = Match.instance.GetRebornPos(t.Team);
                    float DisToHome = Vector3.Distance(nearestStar.Position, HomePos);

                    if (DisToStar <= 0.5 * DisToHome)
                    {
                        t.Move(nearestStar.Position);
                        return this;
                    }
                    else
                    {
                        Debug.Log("home");
                        return m_StateMachine.Transition((int) EStateType.BackToHome);
                    }
                }
                else
                {
                    Debug.Log("home");
                    return m_StateMachine.Transition((int) EStateType.BackToHome);
                }
            }

            if (hasStar) { t.Move(nearestStar.Position); }
            else
            {
                if (t.HP > 50)
                {
                    Debug.Log("center");
                    return m_StateMachine.Transition((int)EStateType.GoCenter);
                }
                else
                {
                    Debug.Log("home");
                    return m_StateMachine.Transition((int)EStateType.BackToHome);
                }
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
            m_FSM.AddState(new FindEnemyState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new FindStarState());
            m_FSM.AddState(new GoCenterState());
            m_FSM.SetDefaultState((int)EStateType.GoCenter);
        }

         Vector3 TargetPrediction(Tank tank)
        {
            if (tank.IsDead)
            {
                return Match.instance.GetRebornPos(tank.Team);
            }
            float distance = Vector3.Distance(tank.Position, Match.instance.GetOppositeTank(tank.Team).Position);
            float pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
            Vector3 pPos = tank.Position + tank.Velocity * pTime;
            for (int i = 0; i < 2; i++)
            {
                distance = Vector3.Distance(Match.instance.GetOppositeTank(tank.Team).Position, pPos);
                pTime = distance / Match.instance.GlobalSetting.MissileSpeed;
                pPos = tank.Position + tank.Velocity * pTime;
            }
            return pPos;
        }
        
        protected override void OnUpdate()
        {
            //fire check
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                TurretTurnTo(TargetPrediction(oppTank));


                if (CanSeeOthers(oppTank))
                {

                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            //state update
            m_FSM.Update();
        }
        public override string GetName()
        {
            return "PHD";
        }
    }
}