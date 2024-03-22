using System.Collections;
using AI.FiniteStateMachine;
using UnityEngine;
using Main;
using UnityEditor;

namespace HYY
{
    public enum States
    {
        EatStar,GoHome,Fighting
    }

    class Fighting : State
    {
        public Fighting()
        {
            StateType = (int)States.Fighting; 
        }

        public override void Enter()
        {
            Debug.Log("战斗！");
        }

        public override State Execute()
        {
            Tank myTank = (Tank)Agent;
            Tank enemyTank = Match.instance.GetOppositeTank(myTank.Team);
            Vector3 disVector3 = myTank.Position - enemyTank.Position;
            Vector2 disVector2 = new Vector2(disVector3.x, disVector3.z);
            //Debug.Log(disVector2);
            float distance = disVector3.magnitude;
            if (!enemyTank.IsDead)
            {
                if (distance < 25)
                {
                    Vector2 temp = Vector2.Perpendicular(disVector2);
                    Vector3 myForward = new Vector3(temp.x,0,temp.y).normalized;
                    Vector3 nextPos = myTank.Position + myForward*10;
                    //Debug.Log(nextPos);
                    myTank.Move(nextPos);
                }
                else
                {
                    myTank.Move(enemyTank.Position);
                }
                
                return this;
            }
            else
            {
                return m_StateMachine.Transition((int)States.EatStar);
            }
            
        }
    }


    class GoHome : State
    {
        private int recoverHp;
        public GoHome(int recoverHp)
        {
            
            StateType = (int)States.GoHome;
            this.recoverHp = recoverHp;
        }

        public override void Enter()
        {
            Debug.Log("回家中");
        }

        public override State Execute()
        {
            Tank myTank = (Tank)Agent;
            if (myTank.HP > recoverHp)
            {
                return m_StateMachine.Transition((int)States.EatStar);
            }

            myTank.Move(Match.instance.GetRebornPos(myTank.Team));
            return this;
        }
    }
    class EatStar : State
    {
        public EatStar()
        {
            StateType = (int)States.EatStar;
        }

        public override void Enter()
        {
            Debug.Log("吃星星ing");
        }

        public override State Execute()
        {
            Tank myTank = (Tank)Agent;
            Tank enemyTank = Match.instance.GetOppositeTank(myTank.Team);
            Star closestStar = MathUtil.GetClosestStar(myTank);
            if (myTank.HP < 50 && enemyTank.IsDead)
            {
                return m_StateMachine.Transition((int)States.GoHome);
            }
            if (CanGetStar(myTank,enemyTank))
            {
                myTank.Move(closestStar.Position);
            }
            else
            {
                if (myTank.HP >= 50)
                {
                    if (!enemyTank.IsDead)
                    {
                        return m_StateMachine.Transition((int)States.Fighting);
                    }
                    else
                    {
                        return m_StateMachine.Transition((int)States.GoHome);
                    }
                    
                }
                
            }

            return this;

        }

        private Star CanGetStar(Tank myTank, Tank enemyTank)
        {
            Star closestStar = MathUtil.GetClosestStar(myTank);
            
            if (closestStar)
            {
                if (enemyTank.IsDead)
                {
                    return closestStar;
                }
                else
                {
                    float disToMy = (myTank.Position - closestStar.Position).sqrMagnitude;
                    float disToEnemy = (enemyTank.Position - closestStar.Position).sqrMagnitude;
                    if (disToMy < disToEnemy)
                    {
                        return closestStar;
                    }
                    else
                    {
                        return null;
                    }
                }
                
            }
            return null;
        }
    }
    public class MyTank : Tank
    {
        private StateMachine _machine;
        

        [SerializeField] int recoverHp = 50;
        // Start is called before the first frame update
        public override string GetName()
        {
            return "HYY";
        }

        protected override void OnStart()
        {
            base.OnStart();
            _machine = new StateMachine(this);
            _machine.AddState(new EatStar());
            _machine.AddState(new GoHome(recoverHp));
            _machine.AddState(new Fighting());
            _machine.SetDefaultState((int)States.EatStar);
        }

        protected override void OnUpdate()
        {
            Tank enemyTank = Match.instance.GetOppositeTank(Team);
            if (enemyTank && enemyTank.IsDead == false)
            {
                Aiming(enemyTank);
                if (CanFire())
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
            
            _machine.Update();
        }

        private void Aiming(Tank enemyTank)
        {
            float distance = Vector3.Distance(Position, enemyTank.Position);
            float timeOnFly = distance / Match.instance.GlobalSetting.MissileSpeed;
            Vector3 aimPos = enemyTank.Position + enemyTank.Forward + enemyTank.Velocity * timeOnFly * 0.9f;
            TurretTurnTo(aimPos);
        }

        private void ShortRangeAiming(Tank enemyTank)
        {
            Vector3 aimPos = enemyTank.Position;
            TurretTurnTo(aimPos);
        }
        protected override void OnOnDrawGizmos()
        {
            // Gizmos.color = Color.green;
            // Gizmos.DrawSphere(Position,25);
            base.OnOnDrawGizmos();
        }
    } 
}

