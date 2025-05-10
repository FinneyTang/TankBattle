using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using Main;

namespace DYT
{
    enum EStateType
    {
        FindingStar = 1,
        SeeingEnemy = 2,
        BackToHome = 3,
        FindingEnemy=4,
        PatrolInMiddle=5
    }
    class MyTank : Tank
    {
        private StateMachine FSM;
        protected override void OnStart()
        {
            base.OnStart();
            FSM = new StateMachine(this);
            FSM.AddState(new FindingEnemy());
            FSM.AddState(new FindingStar());
            FSM.AddState(new BackToHome());
            FSM.AddState(new SeeingEnemy());
            FSM.AddState(new PatrolInMiddle());
            FSM.SetDefultState((int)EStateType.FindingStar);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            //攻击
            Tank enemy = Match.instance.GetOppositeTank(Team);
            Vector3 toTarget = Vector3.zero;
            
            if (enemy != null && CanSeeOthers(enemy)) //拿到敌方且能看见敌方
            {
                TurretTurnTo(enemy.Position);
                toTarget = enemy.Position - FirePos;
            }
            else if (CanSeeOthers(enemy) == false)//如果看不见敌人
            {
                /*炮口随机转向
                System.Random random = new System.Random();
                toTarget = new Vector3((float)(random.NextDouble() * 2 - 1), 0, (float)(random.NextDouble() * 2 - 1));
                */
                //炮口转向前方
                TurretTurnTo(Position + Forward);
            }
            toTarget.y = 0;
            toTarget.Normalize();
            if (Vector3.Dot(TurretAiming, toTarget) > 0.98f && CanSeeOthers(enemy)) 
            {
                Fire();
            }

            FSM.Update();
        }
        public override string GetName()
        {
            return "DYT";
        }
    }
    class FindingStar : State
    {
        public FindingStar()
        {
            Statetype = (int)EStateType.FindingStar;
        }
        public override State Execute()
        {

            bool hasSuperStar = false;
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = s.Position;
                    break;
                }
                else
                {
                    float dist = (s.Position - _stateMachine.transform.position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        hasSuperStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position;
                    }
                }
            }
            if (Vector3.Distance(nearestStarPos,agent.Position) < 8f)
            {
                if (Vector3.Distance(agent.Position, Match.instance.GetRebornPos(agent.Team)) < 30f)
                {
                    return _stateMachine.Transition((int)EStateType.BackToHome);
                }
            }
            //在吃掉星星的时候离家很近，回家补血

            if (_stateMachine.agent.HP < 30f && hasSuperStar == false)//如果血小于30且没有超级星星，则回家
                return _stateMachine.Transition((int)EStateType.BackToHome);

            if (_stateMachine.agent.CanSeeOthers(_stateMachine.enemy))  //如果发现敌人且离得最近的星星距离大于某个值
                return _stateMachine.Transition((int)EStateType.SeeingEnemy);

            if (hasStar == true)//有星星就移动
                _stateMachine.agent.Move(nearestStarPos);

            else if (_stateMachine.enemy.IsDead) 
                return _stateMachine.Transition((int)EStateType.PatrolInMiddle); //敌人死了就巡逻

            else
                return _stateMachine.Transition((int)EStateType.FindingEnemy);//敌人没死就回家

            return _stateMachine.Transition((int)EStateType.FindingStar);
        }
    }

    class SeeingEnemy : State
    {
        public SeeingEnemy()
        {
            Statetype = (int)EStateType.SeeingEnemy;
        }
        public override State Execute()
        {

            if (_stateMachine.enemy.IsDead || _stateMachine.agent.CanSeeOthers(_stateMachine.enemy) == false) //敌人死了或者看不见敌人了
                return _stateMachine.Transition((int)EStateType.FindingStar);
            if (_stateMachine.agent.HP < 30) //血小于30
                return _stateMachine.Transition((int)EStateType.BackToHome);

            GameObject nearestWall = null;//找离得最近的墙
            float nearestDis = float.MaxValue;
            Vector3 direction = Vector3.zero;
            foreach (Collider c in _stateMachine.Walls)
            {
                float distance = Vector3.Distance(_stateMachine.agent.transform.position, c.gameObject.transform.position);
                if (distance < nearestDis)
                {
                    nearestWall = c.gameObject;
                    nearestDis = distance;
                }
            }
            if (nearestWall != null)
            {
                direction = (nearestWall.transform.position - _stateMachine.agent.transform.position).normalized;
            }

            if (UnityEngine.Random.Range(0, 100) < 1) // 10%的概率改变方向//蛇形走位
            {
                _stateMachine.isMovingLeft = !_stateMachine.isMovingLeft;
            }

            if (_stateMachine.isMovingLeft)
            {
                _stateMachine.agent.Move(direction + Vector3.right * -100f);
            }
            else
            {
                _stateMachine.agent.Move(direction + Vector3.right * 100f);
            }
            return _stateMachine.Transition((int)EStateType.SeeingEnemy);
        }
    }
    class BackToHome : State
    {
        public BackToHome()
        {
            Statetype = (int)EStateType.BackToHome;
        }
        public override State Execute()
        {
            _stateMachine.agent.Move(Match.instance.GetRebornPos(agent.Team));
            if (_stateMachine.agent.HP > 70)
                return _stateMachine.Transition((int)EStateType.FindingStar);
            else
                return _stateMachine.Transition((int)EStateType.BackToHome);
        }
    }
    class FindingEnemy : State
    {
        public FindingEnemy()
        {
            Statetype = (int)EStateType.FindingEnemy;
        }
        public override State Execute()
        {
            _stateMachine.agent.Move(_stateMachine.enemy.Position);
            if (_stateMachine.enemy.IsDead) //敌人死了
                return _stateMachine.Transition((int)EStateType.FindingStar);

            if (_stateMachine.agent.CanSeeOthers(_stateMachine.enemy))
                return _stateMachine.Transition((int)EStateType.SeeingEnemy);

            return _stateMachine.Transition((int)EStateType.FindingEnemy);
        }
    }
    class PatrolInMiddle : State
    {
        public PatrolInMiddle()
        {
            Statetype = (int)EStateType.PatrolInMiddle;
        }
        public override State Execute()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            _stateMachine.agent.Move(new Vector3(UnityEngine.Random.Range(-halfSize, halfSize), 0, UnityEngine.Random.Range(-halfSize, halfSize)));
            if(Match.instance.GetStars().Count>0)//场上存在星星
                return _stateMachine.Transition((int)EStateType.FindingStar);

            if (_stateMachine.agent.CanSeeOthers(_stateMachine.enemy))//看见敌人
                return _stateMachine.Transition((int)EStateType.SeeingEnemy);

            if (_stateMachine.agent.HP < 30)
                return _stateMachine.Transition((int)EStateType.BackToHome);

            return _stateMachine.Transition((int)EStateType.PatrolInMiddle);
        }
    }
    class State
    {
        public int Statetype { get; protected set; }
        public Tank agent { get; set; }
        protected StateMachine _stateMachine;

        public void SetStateMachine(StateMachine s)
        {
            _stateMachine = s;
        }
        public virtual void Enter()
        {

        }
        public virtual State Execute()
        {
            return null;
        }
        public virtual void Exit()
        {

        }
    }
    class StateMachine
    {
        public Tank agent;
        public Dictionary<int, State> _states = new Dictionary<int, State>();
        public State currentState;

        public Transform transform;
        public Tank enemy;
        public bool isMovingLeft = false;
        public Collider[] Walls;
        public StateMachine(Tank a)
        {
            agent = a;
            transform = agent.GetComponent<Transform>();
            enemy = Match.instance.GetOppositeTank(agent.Team);
            Walls = Physics.OverlapSphere(transform.position, 10f, PhysicsUtils.LayerMaskScene);

        }
        public void AddState(State s)
        {
            s.agent = agent;
            s.SetStateMachine(this);
            _states[s.Statetype] = s;
        }
        public State Transition(int type)
        {
            State s;
            _states.TryGetValue(type, out s);
            return s;
        }
        public void SetDefultState(int t)
        {
            if (_states.TryGetValue(t, out currentState))
            {
                currentState.Enter();
            }
        }
        public void Update()
        {
            if (currentState == null)
                return;
            State nextState = currentState.Execute();
            if (nextState != currentState)
            {
                currentState.Exit();
                currentState = nextState;
                if (currentState != null)
                    currentState.Enter();
            }
        }
    }
}
