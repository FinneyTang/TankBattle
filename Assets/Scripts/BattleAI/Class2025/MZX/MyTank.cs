using Main;
using AI.FiniteStateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace MZX
{   
    public enum EStateType
    {
        FindEnemy, 
        FindStar, 
        BackToHome,
        AvoidMissiles,
    }

    public class Blackboard
    {
        public static Blackboard Instance = new Blackboard();
        public Tank _tank;
        public Tank _oppTank;
        public Missile NearestMissile;
        public bool HaveSuperStar;

        public void Init(Tank t,Tank ot)
        {
            _tank = t;
            _oppTank = ot;
            HaveSuperStar = false;
        }

        #region �ӵ�

        //��ȡ����ӵ�
        private Missile GetNearestMissilePosition()
        {
            var missiles = Match.instance.GetOppositeMissiles(_tank.Team);
            if (missiles == null || missiles.Count == 0)
            {
                return null;
            }

            Missile nearestMissile = null;
            float minDist = float.MaxValue;

            foreach (var missile in missiles.Values)
            {   
                float dist = Vector3.Distance(_tank.Position, missile.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestMissile = missile;
                }
            }

            return nearestMissile;
        }

        //�Ƿ���Σ��
        public bool IsDanger()
        {
            if (NearestMissile == null)
            {
                return false;
            }

            if (Physics.SphereCast(NearestMissile.Position, 0.1f, NearestMissile.Velocity, out RaycastHit hit, 40))
            {
                FireCollider fc = hit.transform.GetComponent<FireCollider>();
                if (fc != null)
                {
                    if (fc.Owner == _tank)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        //ˢ������ӵ�
        public void UpdateNearestMissilePosition() => NearestMissile = GetNearestMissilePosition();

        #endregion

        #region ����

        public bool WillUpdateSuperStar()
        {   
            float dist = (Vector3.zero - _tank.Position).sqrMagnitude;
            if (dist <= 190f)
            {   
                return Match.instance.RemainingTime >= 90f && Match.instance.RemainingTime <= 92f;
            }
            else if(dist <= 790f)
            {
                return Match.instance.RemainingTime >= 90f && Match.instance.RemainingTime <= 94f;
            }

            return Match.instance.RemainingTime >= 90f && Match.instance.RemainingTime <= 100f;
        }

        public Star GetStar()
        {
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var star in Match.instance.GetStars().Values)
            {
                if (star.IsSuperStar)
                {   
                    HaveSuperStar = true;
                    nearestStar = star;
                    break;
                }
                else
                {
                    HaveSuperStar = false;
                    float dist = (star.Position - _tank.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestStar = star;
                    }
                }
            }

            return nearestStar;
        }

        public Star GetNearestStar(float distance)
        {
            Star nearestStar = null;
            foreach (var star in Match.instance.GetStars().Values)
            {
                if (star.IsSuperStar)
                {
                    HaveSuperStar = true;
                    nearestStar = star;
                    break;
                }
                else
                {
                    HaveSuperStar = false;
                    float dist = (star.Position - _tank.Position).sqrMagnitude;
                    if (dist < distance)
                    {
                        nearestStar = star;
                        return nearestStar;
                    }
                }
            }

            return nearestStar;
        }

        #endregion
    }

    public class FindEnemyState : State
    {
        public FindEnemyState()
        {
            StateType = (int)EStateType.FindEnemy;
        }

        public override void Enter()
        {
            //Debug.Log("Ѱ�ҵ���");
            base.Enter();
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);

            if (Blackboard.Instance.WillUpdateSuperStar())
            {
                t.Move(Vector2.zero);
                return this;
            }

            Star superStar = Blackboard.Instance.GetStar();
            if (Blackboard.Instance.HaveSuperStar)
            {
                t.Move(superStar.Position);
                return this;
            }

            if (Blackboard.Instance.IsDanger())
            {
                return m_StateMachine.Transition((int)EStateType.AvoidMissiles);
            }

            if (oppTank == null || oppTank.IsDead)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }

            if (t.HP < 50)
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }

            t.Move(oppTank.Position);

            return this;
        }
    }

    public class FindStarState : State
    {
        public FindStarState()
        {
            StateType = (int)EStateType.FindStar;
        }

        public override void Enter()
        {
            //Debug.Log("Ѱ������");
            base.Enter();
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);

            if (Blackboard.Instance.WillUpdateSuperStar())
            {
                t.Move(Vector2.zero);
                return this;
            }

            Star superStar = Blackboard.Instance.GetStar();
            if (Blackboard.Instance.HaveSuperStar)
            {
                t.Move(superStar.Position);
                return this;
            }

            if (Blackboard.Instance.IsDanger())
            {
                return m_StateMachine.Transition((int)EStateType.AvoidMissiles);
            }

            if (t.HP < 50 ||((t.Position - Match.instance.GetRebornPos(t.Team)).sqrMagnitude <= 885f) && t.HP < 50)
            {
                return m_StateMachine.Transition((int)EStateType.BackToHome);
            }

            Star star = Blackboard.Instance.GetStar();          

            if (star != null)
            {
                t.Move(star.Position);
            }
            else
            {   
                if (oppTank == null || oppTank.IsDead)
                {
                    return m_StateMachine.Transition((int)EStateType.BackToHome);
                }

                if (Mathf.Abs(t.HP - oppTank.HP) <= 20)
                {
                    return m_StateMachine.Transition((int)EStateType.FindEnemy);
                }
                else
                {
                    return m_StateMachine.Transition((int)EStateType.BackToHome);
                }
            }

            return this;
        }
    }

    public class BackToHomeState : State
    {   
        public BackToHomeState()
        {
            StateType = (int)EStateType.BackToHome;
        }

        public override void Enter()
        {
            //Debug.Log("�ؼ�");
            base.Enter();
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);

            if (Blackboard.Instance.WillUpdateSuperStar() && ((t.Position - Match.instance.GetRebornPos(t.Team)).sqrMagnitude > 685f))
            {
                t.Move(Vector2.zero);
                return this;
            }

            Star superStar = Blackboard.Instance.GetStar();
            if (Blackboard.Instance.HaveSuperStar)
            {
                t.Move(superStar.Position);
                return this;
            }

            if (Blackboard.Instance.IsDanger())
            {
                return m_StateMachine.Transition((int)EStateType.AvoidMissiles);
            }

            if (t.HP >= 85)
            {
                return m_StateMachine.Transition((int)EStateType.FindStar);
            }

            Star star = Blackboard.Instance.GetNearestStar(450f);
            if (star != null)
            {
                t.Move(star.Position);
            }
            else
            {
                t.Move(Match.instance.GetRebornPos(t.Team));
            }

            return this;
        }
    }

    public class AvoidMissilesState : State
    {
        private float timer;
        private float safeTime = 0.05f;

        public AvoidMissilesState()
        {
            StateType = (int)EStateType.AvoidMissiles;
        }

        public override void Enter()
        {
            base.Enter();
            //Debug.Log("����ӵ�");
            timer = Time.time;
        }

        public override State Execute()
        {
            Tank t = (Tank)Agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);

            if (Blackboard.Instance.WillUpdateSuperStar())
            {
                t.Move(Vector2.zero);
                return this;
            }

            Star superStar = Blackboard.Instance.GetStar();
            if (Blackboard.Instance.HaveSuperStar)
            {
                t.Move(superStar.Position);
                return this;
            }

            Vector3? direcion = CalculateAvoidDirection(t);

            if (direcion != null)
            {
                t.Move((Vector3)direcion);
            }
            else
            {
                Star star = Blackboard.Instance.GetStar();
                if (star != null)
                {
                    t.Move(star.Position);
                }
            }

            if (!Blackboard.Instance.IsDanger())
            {
                if (Time.time - timer >= safeTime)
                {   
                    if (t.HP >= 50)
                    {
                        return m_StateMachine.Transition((int)EStateType.FindStar);
                    }
                    else
                    {
                        return m_StateMachine.Transition((int)EStateType.BackToHome);
                    }
                }
            }
            else
            {
                timer = Time.time;
            }

            return this;
        }

        private Vector3? CalculateAvoidDirection(Tank tank)
        {   
            if (Blackboard.Instance.NearestMissile == null)
            {
                return null;
            }

            Vector3 side = Vector3.Cross(Blackboard.Instance.NearestMissile.Velocity, tank.Position - Blackboard.Instance.NearestMissile.Position);
            Vector3 cross = Vector3.Cross(Blackboard.Instance.NearestMissile.Velocity, Vector3.up).normalized;
            if (side.y > 0)
            {
                cross *= -1;
            }

            return tank.Position + cross * 5f;
        }
    }

    public class MyTank : Tank
    {
        private StateMachine _FSM;
        private bool _currentOppTank;

        protected override void OnStart()
        {
            base.OnStart();
            Blackboard.Instance.Init(this, Match.instance.GetOppositeTank(Team));
            _FSM = new StateMachine(this);
            _FSM.AddState(new FindEnemyState());
            _FSM.AddState(new BackToHomeState());
            _FSM.AddState(new FindStarState());
            _FSM.AddState(new AvoidMissilesState());

            _FSM.SetDefaultState((int)EStateType.FindStar);

            _currentOppTank = false;
        }

        protected override void OnReborn()
        {
            _FSM.Transition((int)EStateType.FindStar);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && !oppTank.IsDead)
            {
                Vector3 aimDirection = Vector3.zero;
                if ((Position - oppTank.Position).sqrMagnitude <= 600f)
                {
                    aimDirection = oppTank.Position;
                    _currentOppTank = true;
                }
                else
                {
                    aimDirection = Aiming(oppTank);
                    _currentOppTank = false;
                }

                TurretTurnTo(aimDirection);

                if (_currentOppTank)
                {
                    Fire();
                }
                else
                {
                    if (CanSeeOthers(oppTank) && Vector3.Angle(TurretAiming, aimDirection - Position) < 3f)
                    {
                        Fire();
                    }
                }
            }

            Blackboard.Instance.UpdateNearestMissilePosition();
            _FSM.Update();
        }

        private Vector3 Aiming(Tank oppTank)
        {
            Vector3 toEnemy = Position - oppTank.Position;
            float distance = toEnemy.magnitude;
            float missileSpeed = distance / 40f;

            float horizontalFactor = Mathf.Clamp(missileSpeed * 0.8f, 0.5f, 2f); // ����Ԥ��ϵ��
            float verticalFactor = Mathf.Clamp(missileSpeed * 1.2f, 1f, 3f); // ����Ԥ��ϵ��

            Vector3 oppTankVelocity = oppTank.Velocity.normalized;
            Vector3 basePrediction = oppTank.Position + oppTank.Velocity * missileSpeed * verticalFactor;

            Vector3 side = Vector3.Cross(oppTankVelocity, toEnemy.normalized);
            Vector3 sideOffset = (side.y > 0 ? Vector3.Cross(Vector3.up, oppTankVelocity) : -Vector3.Cross(Vector3.up, oppTankVelocity)) * horizontalFactor;

            Vector3 finalPrediction = basePrediction + sideOffset;
            return finalPrediction;
        }

        public override string GetName()
        {
            return "MZX";
        }
    }
}