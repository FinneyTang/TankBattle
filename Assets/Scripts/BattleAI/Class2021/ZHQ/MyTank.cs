using AI.FiniteStateMachine;
using UnityEngine;
using Main;


namespace ZHQ
{
    //define 4 States of the Tank
    enum EStateType
    {
        FindStar,
        BackToHome,
        Dodge
    };
        
    class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)EStateType.FindStar;
        }
        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            
            if (oppTank != null && !oppTank.IsDead && t.CanSeeOthers(oppTank.Position))
            {
                return m_StateMachine.Transition((int) EStateType.Dodge);
            }
            
            //Find Nearest State
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
            //if there exists superstar
            if (hasSuperStar)
            {
                t.Move(SuperStar.Position);
                return this;
            }
            
            //if low hp and can't see oppTank
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
                        return m_StateMachine.Transition((int) EStateType.BackToHome);
                    }
                }
                else
                {
                    return m_StateMachine.Transition((int) EStateType.BackToHome);
                }
            }

            if(hasStar) t.Move(nearestStar.Position);
            return this;
        }
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
            if (t.HP >= 100)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }
            t.Move(Match.instance.GetRebornPos(t.Team));
            return this;
        }
    }
   
   
    class DodgeState : State
    {
        public DodgeState()
        {
            StateType = (int)EStateType.Dodge;
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            
            //Handle Transitioning
            if (oppTank == null || oppTank.IsDead || !t.CanSeeOthers(oppTank))
            {
                return m_StateMachine.Transition((int) EStateType.FindStar);
            }
            
            //Handle Behavior
            var missiles = Match.instance.GetOppositeMissiles(t.Team);
           
            //Find Nearest missile
            Missile nearestMissile = null;
            float minDis = Mathf.Infinity;
            foreach (var missile in missiles)
            {
                float Dis = Vector3.Distance(missile.Value.Position, t.Position);
                if (minDis >= Dis)
                {
                    minDis = Dis;
                    nearestMissile = missile.Value;
                }
            }

            Vector3 nextPos = Vector3.zero;
            //Calculate Next Position of the Tank
            if (nearestMissile != null)
            {
                Vector3 dir1 = Quaternion.AngleAxis(90, Vector3.up) * nearestMissile.Velocity.normalized;
                Vector3 dir2 = Quaternion.AngleAxis(-90, Vector3.up) * nearestMissile.Velocity.normalized;

                if (Vector3.Dot(t.Velocity.normalized, dir1) >= 0.0f)
                {
                    nextPos = t.Position + dir1 * 10.0f;
                }
                else if (Vector3.Dot(t.Velocity.normalized, dir2) >= 0.0f)
                {
                    nextPos = t.Position + dir2 * 10.0f;
                }
                
                t.Move(nextPos);
                return this;
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
            m_FSM.AddState(new FindStarState());
            m_FSM.AddState(new BackToHomeState());
            m_FSM.AddState(new DodgeState());
            m_FSM.SetDefaultState((int)EStateType.FindStar);
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
            base.OnUpdate();
            
            //handle fire
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && !oppTank.IsDead)
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
            m_FSM.Update();
        }
        
        public override string GetName()
        {
            return "HiddenpiggyAI";
        }
    }
}