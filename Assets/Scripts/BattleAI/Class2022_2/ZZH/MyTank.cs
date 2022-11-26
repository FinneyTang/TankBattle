using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;

namespace ZZH
{
    public class MyTank : Tank
    {
        private StateMachine stateMachine;
        private Vector3 targetPosition;

        protected override void OnStart()
        {
            base.OnStart();
            stateMachine = new StateMachine(this);
            stateMachine.AddState(new Normal());
            stateMachine.AddState(new SuperStar());
            stateMachine.AddState(new LowHp());
            stateMachine.SetDefaultState((int)EStateType.normal);
        }
        protected override void OnUpdate()
        {
            //Debug.Log(Velocity.magnitude);
            base.OnUpdate();
            stateMachine.Update();
            //dogde
            if (stateMachine.info.shouldDodge)
            {
                Move(CaculatePath(stateMachine.info.dodgePosition));
            }
            //choose target to attack
            if (stateMachine.info.enemy != null && stateMachine.info.enemy.IsDead == false)
            {
                //Vector3 targetPosition;
                if ((stateMachine.info.enemy.Position - Position).magnitude > stateMachine.info.directHitDis)
                {
                    targetPosition = stateMachine.info.enemy.Position + stateMachine.info.enemy.Velocity * ((stateMachine.info.enemy.Position - Position).magnitude / stateMachine.info.match.GlobalSetting.MissileSpeed);
                }
                else
                {
                    targetPosition = stateMachine.info.enemy.Position;
                }
                TurretTurnTo(targetPosition);
                if (Mathf.Abs(Vector3.Dot(TurretAiming.normalized, (Position - targetPosition).normalized)) > 0.9f)
                {
                    if (CanFire())
                    {
                        stateMachine.info.shoot += 1;
                    }
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + Forward);
            }
        }
        public override string GetName()
        {
            return "ZZH";
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            Gizmos.color = Color.black;
            //Debug.Log(targetPosition);
            Gizmos.DrawCube(targetPosition, Vector3.up * 5);
        }
    }
}
