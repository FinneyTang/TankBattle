using Main;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LYF
{
    public class AttackState : TankState
    {
        private float maxDistToStar = 30f;
        private float minDistToEnemy = 30f;
        private bool isAvoidingMissile = false;

        private Dictionary<int, Missile> m_cachedMissiles = new Dictionary<int, Missile>();

        public AttackState(MyTank tank) : base(tank) { }

        public override void OnUpdate()
        {
            // 如果附近有星星，则切换到收集星星状态
            if (HasNearbyStar())
            {
                tank.ChangeState(new CollectStarsState(tank));
                return;
            }

            // 如果敌人死亡
            if (tank.EnemyTank.IsDead)
            {
                // 场上存在星星则切换到收集星星状态
                if (Match.instance.GetStars().Count > 0)
                    tank.ChangeState(new CollectStarsState(tank));
                // 如果生命值小于等于80则回重生点回血
                else if (tank.HP <= 80)
                    tank.ChangeState(new RetreatState(tank));
                // 否则占据场地中央位置
                else
                    tank.Move(Match.instance.gameObject.transform.position);

                return;
            }

            AvoidMissiles();

            if (isAvoidingMissile) return;

            // 移动到攻击位置
            if (!tank.CanSeeEnemy())
            {
                tank.Move(tank.EnemyTank.Position);
            }
            else if ((tank.EnemyTank.Position - tank.Position).sqrMagnitude < minDistToEnemy * minDistToEnemy)
            {
                Vector3 directionToEnemy = (tank.EnemyTank.Position - tank.Position).normalized;
                Vector3 targetPos = tank.Position - directionToEnemy * minDistToEnemy;
                tank.Move(targetPos);
            }
        }

        private void AvoidMissiles()
        {
            Match.instance.GetOppositeMissilesEx(tank.Team, m_cachedMissiles);
            foreach (var missile in m_cachedMissiles.Values)
            {
                float dist = (missile.Position - tank.Position).sqrMagnitude;
                if (dist < 900f) // 10~30m范围内有导弹
                {
                    // 计算两个可能的躲避方向（左和右）
                    Vector3 missileDirection = missile.Velocity.normalized;
                    Vector3 leftEvadeDirection = Vector3.Cross(missileDirection, Vector3.up).normalized;
                    Vector3 rightEvadeDirection = -leftEvadeDirection;

                    // 选择更靠近坦克前进方向的躲避方向
                    Vector3 selectedEvadeDirection =
                        Vector3.Dot(leftEvadeDirection, tank.Forward) > Vector3.Dot(rightEvadeDirection, tank.Forward)
                        ? leftEvadeDirection
                        : rightEvadeDirection;

                    Vector3 evadePos = tank.Position + selectedEvadeDirection * 5f;

                    if (NavMesh.SamplePosition(evadePos, out var hit, 5f, NavMesh.AllAreas))
                    {
                        tank.Move(hit.position);
                        break;
                    }
                }
            }
            isAvoidingMissile = false;
        }

        private bool HasNearbyStar()
        {
            foreach (var star in Match.instance.GetStars().Values)
            {
                if ((star.Position - tank.Position).sqrMagnitude < maxDistToStar * maxDistToStar)
                {
                    return true;
                }
            }
            return false;
        }
    }
}