using System.Collections;
using Main;
using UnityEngine;

namespace MLS
{
    /// <summary>
    /// 射击的策略
    /// 射击本质上只有一个需要调整的地方
    /// 即射击的提前预判量
    /// Shoot控制器会根据对方坦克的各种行为来实时调整射击的策略
    /// </summary>
    public class ShootController
    {
        private Conditions _conditions;
        //一个0-1的值，会检测对方的坦克躲避子弹的概率
        private float _canEnemyAvoidBullet;
        private int _shootTime = 0;

        #region Properties

        

        #endregion

        public ShootController(Conditions conditions)
        {
            _conditions = conditions;
        }

        public void OnUpdate()
        {
            bool shootInAdvance = false;
            var willingPos = SetTurret(ref shootInAdvance);
            if (
                _conditions.Self.CanSeeOthers(_conditions.Enemy) || shootInAdvance
                && !_conditions.Enemy.IsDead
                )
            {
                //check炮塔角度
                var fireDir = Vector3.Normalize( _conditions.Self.FirePos - _conditions.Self.Position);
                float cos = Vector3.Dot(fireDir, Vector3.Normalize(willingPos - _conditions.Self.Position));
                if (cos >0.5)
                {
                    bool success = _conditions.Self.Fire();
                    if (success)
                    {
                        _shootTime++;
                    }
                }
            }
        }

        private Vector3 SetTurret(ref bool shootInAdvance)
        {
            //子弹速度
            float missileSpeed = Match.instance.GlobalSetting.MissileSpeed;
            //获取当前敌方坦克
            var enemyTank = _conditions.Enemy;
            Vector3 curEnemyPos = enemyTank.Position;
            Vector3 curEnemyVelocity = enemyTank.Velocity;
            float tankDis = Vector3.Distance(curEnemyPos, _conditions.Self.Position);
            float missileReachedTime = tankDis / missileSpeed;
            Vector3 enemyWillingPos = curEnemyVelocity * missileReachedTime + enemyTank.Position;
            //将枪口朝向敌人的预期位置
            _conditions.Self.TurretTurnTo(enemyWillingPos);
            //做检测，预判是否有提前射击的机会
            RaycastHit hitInfo;
            Vector3 correctWillingPos = enemyWillingPos + curEnemyVelocity * (-1f * missileReachedTime * 0.6f);
            if (Physics.Linecast(_conditions.Self.Position, correctWillingPos, out hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                shootInAdvance = false;
            }
            else
            {
                shootInAdvance = true;
            }
            return enemyWillingPos;
        }

        private IEnumerator MonitorEnemyReactionToAttack()
        {
            float timeCounter = 0.0f;
            float MaxTime = 1.0f;
            float waitTime = 0.05f;
            while (timeCounter < MaxTime)
            {
                timeCounter += waitTime;
                yield return new WaitForSeconds(waitTime);
            }
            yield return null;
        }
    }
    

}