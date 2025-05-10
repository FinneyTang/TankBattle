using Main;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace LYF
{
    public class AvoidState : TankState
    {
        private Dictionary<int, Missile> m_cachedMissiles = new Dictionary<int, Missile>();

        private float timer = 0f;

        public AvoidState(MyTank tank) : base(tank)
        {
        }

        public override void OnUpdate()
        {
            if (tank.HP <= tank.stateParams.minHpBeforeRetreat)
            {
                tank.ChangeState(new RetreatState(tank));
                return;
            }

            if (timer > 2f || tank.EnemyTank.IsDead)
            {
                tank.ChangeState(new CollectStarsState(tank));
                return;
            }

            if (!tank.CanSeeEnemy())
                timer += Time.deltaTime;
            else
                timer = 0;

            Match.instance.GetOppositeMissilesEx(tank.Team, m_cachedMissiles);
            foreach (var missile in m_cachedMissiles.Values)
            {
                float dist = (missile.Position - tank.Position).sqrMagnitude;
                if (dist < tank.stateParams.maxDistToAvoidMissiles * tank.stateParams.maxDistToAvoidMissiles)
                {
                    // 超远离导弹和敌方坦克的方向躲避
                    Vector3 evadeDir = CalculateOptimalEvadeDirection(missile);

                    Vector3 evadePos = tank.Position + evadeDir * tank.stateParams.evadeDistance;

                    if (NavMesh.SamplePosition(evadePos, out var hit, tank.stateParams.evadeDistance, NavMesh.AllAreas))
                    {
                        tank.Move(hit.position);
                        break;
                    }
                }
            }
            // tank.ChangeState(new CollectStarsState(tank));
        }

        private Vector3 CalculateEvadeDirection(Missile missile)
        {
            // 计算导弹前进方向
            Vector3 missileForward = missile.Velocity.normalized;

            // 计算从导弹指向坦克的向量
            Vector3 missileToTank = (tank.Position - missile.Position).normalized;

            // 计算坦克相对于导弹前进方向的左右位置
            // 使用叉积判断左右：导弹前进方向 × 导弹到坦克方向
            Vector3 cross = Vector3.Cross(missileForward, missileToTank);
            bool isOnRightSide = cross.y > 0; // y分量>0表示在右侧

            // 计算垂直于导弹前进方向的躲避方向
            Vector3 perpendicular = Vector3.Cross(missileForward, Vector3.up).normalized;

            // 坦克在导弹右侧就继续向右躲避，左侧则向左
            return isOnRightSide ? -perpendicular : perpendicular;
        }

        private Vector3 CalculateOptimalEvadeDirection(Missile missile)
        {
            // 基础躲避方向（远离导弹）
            Vector3 missileEvadeDir = CalculateEvadeDirection(missile);

            // 获取最近的敌方坦克
            Tank nearestEnemy = tank.EnemyTank;
            if (nearestEnemy.IsDead) return missileEvadeDir;

            // 计算远离敌方坦克的方向
            Vector3 enemyAwayDir = (tank.Position - nearestEnemy.Position).normalized;

            // 结合两个方向（加权平均）
            float enemyWeight = 0.3f; // 可调整权重 (0-1)
            Vector3 optimalDir = (missileEvadeDir * (1 - enemyWeight) + enemyAwayDir * enemyWeight).normalized;

            return optimalDir;
        }
    }
}
