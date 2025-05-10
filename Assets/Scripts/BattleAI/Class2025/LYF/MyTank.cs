using UnityEngine;
using Main;
using System.Collections.Generic;

namespace LYF
{
    public class MyTank : Tank
    {
        public class StateParams
        {
            public readonly float maxDistToAvoidMissiles = 35f;
            public readonly float maxDistToCollectStars = 33f;
            public readonly float evadeDistance = 6f; // 躲避距离(米)
            public readonly float minHpBeforeRetreat = 40f;
        }

        private TankState m_currentState;
        public Tank EnemyTank { get; private set; }
        public Vector3 RebornPos { get; private set; }

        private bool hasSuperStar = true;

        private Dictionary<int, Missile> m_cachedMissiles = new Dictionary<int, Missile>();

        public readonly StateParams stateParams = new StateParams();

        protected override void OnStart()
        {
            base.OnStart();
            EnemyTank = Match.instance.GetOppositeTank(Team);
            RebornPos = Match.instance.GetRebornPos(Team);
            // 游戏开始后先寻找星星
            ChangeState(new CollectStarsState(this));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (ExistsSuperStar())
            {
                ChangeState(new CollectStarsState(this));
            }
            else
            {
                if (Match.instance.RemainingTime <= 110 && Match.instance.RemainingTime >= 100 && HP <= 80)
                    ChangeState(new RetreatState(this));
                else
                    AvoidMissiles();
            }

            m_currentState?.OnUpdate();
            // 更新炮管朝向
            UpdateTurretAiming();
            // 检查开火条件
            CheckFireCondition();
        }

        public void ChangeState(TankState newState)
        {
            m_currentState?.OnExit();
            m_currentState = newState;
            m_currentState.OnEnter();
        }

        private void AvoidMissiles()
        {
            if (m_currentState is AvoidState) return;

            Match.instance.GetOppositeMissilesEx(Team, m_cachedMissiles);
            foreach (var missile in m_cachedMissiles.Values)
            {
                float dist = (missile.Position - Position).sqrMagnitude;
                if (dist < stateParams.maxDistToAvoidMissiles * stateParams.maxDistToAvoidMissiles)
                {
                    ChangeState(new AvoidState(this));
                }
            }
        }

        private bool ExistsSuperStar()
        {
            if (!hasSuperStar || Match.instance.RemainingTime > 90) return false;
            foreach (var star in Match.instance.GetStars().Values)
            {
                if (star.IsSuperStar)
                {
                    return true;
                }
            }
            hasSuperStar = false;
            return false;
        }

        private void UpdateTurretAiming()
        {
            Vector3 pos = PredictTargetPosition(EnemyTank);
            TurretTurnTo(pos);
        }

        private Vector3 PredictTargetPosition(Tank target)
        {
            Vector3 targetVelocity = target.Velocity;
            float distance = (target.Position - FirePos).magnitude;
            float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
            float timeToImpact = distance / missileSpeed;
            return target.Position + targetVelocity * timeToImpact;
        }

        private void CheckFireCondition()
        {
            if (!CanFire()) return;

            if (CanSeeOthers(EnemyTank))
                Fire();
        }

        public bool CanSeeEnemy() => EnemyTank != null && CanSeeOthers(EnemyTank);

        protected override void OnReborn()
        {
            base.OnReborn();
            ChangeState(new CollectStarsState(this));
        }

        public override string GetName()
        {
            return "LYF";
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Position, stateParams.maxDistToCollectStars);

            // 绘制当前状态文本
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 20;
            UnityEditor.Handles.Label(Position + Vector3.up * 2, m_currentState.GetType().Name, style);
        }
    }
}
