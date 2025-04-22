using System.Collections.Generic;
using Main;
using UnityEngine;
using AI.Base;




namespace YZY
{
    class MyTank : Tank
    {
        private float m_LastTime = 0;
        private readonly Timer m_HelpResponseTime = new Timer();
        private Vector3 m_HelpPos;
        private readonly List<Tank> m_CachedOppTanks = new List<Tank>();
      //  private readonly List<Missile> OPP_Missile = new List<Missile>()
      //private float m_NextRandomMoveTime = 0f;
      //private float m_RandomMoveDuration = 0f;
      private Vector3 m_RandomMoveDirection;
      
      Match match;
      int maxMissileKey;
      Missile missile;
      Vector3 eTarget = Vector3.zero;
      Vector3 fTarget = Vector3.zero;
      Vector3 mTarget = Vector3.zero;
      Vector3 escape = Vector3.zero;
      
        protected override void OnUpdate()
        {
            base.OnUpdate();
            
          
            
            if (TeamStrategy == (int)ETeamStrategy.Help && 
                HP > 30 &&
                !m_HelpResponseTime.IsExpired(Time.time))
            {
                Move(m_HelpPos);
            }
            else
            {
                //reset
                m_HelpResponseTime.Reset();
                
                if (HP < 50)
                {
                    SetTeamStrategyParam((int)TeamStrategyBBKey.HelpPos, Position);
                    SendTeamStrategy((int)ETeamStrategy.Help);
                }
                if (ShouldDodge())
                {
                    DodgeMissiles();
                    return; 
                }
                GeneralMovementStrategy();
            }
            var oppTanks = Match.instance.GetOppositeTanks(Team, m_CachedOppTanks);
           
            if(oppTanks != null && oppTanks.Count > 0)
            {
                var oppTank = GetBetterTarget(oppTanks);
                if(oppTank != null)
                {
       
                  Vector3 predictedPosition = PredictTargetPosition(oppTank.Position, oppTank.Velocity, FirePos, 20f);
            
                  // 朝向预测的位置转动炮塔
                  predictedPosition =Vector3.Lerp(predictedPosition,oppTank.Position,0.3f);
                  TurretTurnTo(predictedPosition);
                  Vector3 toTarget = predictedPosition - FirePos;
                  toTarget.y = 0; // 因为是2D环境
                  toTarget.Normalize();
                  
                    if(Vector3.Dot(TurretAiming, toTarget) > 0.89f )
                    {
                        
                        Fire();
                    }
                }
                else
                {
                    TurretTurnTo(Position + Forward);
                }
            }
        }

        // 检查是否有导弹对坦克构成威胁，需要躲避
        private bool ShouldDodge()
        {
            foreach (var missile in FindObjectsOfType<Missile>()) // 假设游戏中所有导弹都是Missile类型
            {
                if (IsMissileThreatening(missile))
                {
                    return true; // 如果至少有一个导弹构成威胁，立即返回true
                }
            }
            return false; // 如果没有导弹构成威胁，返回false
        }

// 实际执行躲避动作
        private void DodgeMissiles()
        {
            foreach (var missile in FindObjectsOfType<Missile>()) // 同样假设游戏中所有导弹都是Missile类型
            {
                if (IsMissileThreatening(missile))
                {
                    Vector3 escapeDirection = CalculateEscapeDirection(missile);
                    EscapeFromMissile(escapeDirection);
                    break; // 假设一次更新只需躲避一个导弹
                }
            }
        }

// 检查导弹是否对坦克构成威胁
        bool IsMissileThreatening(Missile missile)
        {
            Vector3 missileDirection = missile.Velocity.normalized;
            Vector3 toMissile = missile.transform.position - this.transform.position;
            float angle = Vector3.Angle(missileDirection, toMissile);
            if (angle < 30)
            {
                float distance = toMissile.magnitude;
                float timeToImpact = distance / missile.Velocity.magnitude;
                if (timeToImpact < 5)
                {
                    return true;
                }
            }
            return false;
        }

// 计算躲避导弹的方向
        Vector3 CalculateEscapeDirection(Missile missile)
        {
            Vector3 forward = Forward; // 坦克当前前进方向
            Vector3 missileDirection = missile.Velocity.normalized;
            Vector3 toMissile = (missile.transform.position - transform.position).normalized;
            Vector3 escapeDirection;

            // 计算前进方向与导弹方向的夹角
            float angle = Vector3.Angle(forward, toMissile);

            // 如果导弹正从前方来，尝试左右躲避；否则，优先保持当前方向加速躲避
            if (angle < 90f)
            {
                // 基于坦克的右侧方向计算躲避方向，与原方法相同
                escapeDirection = Vector3.Cross(toMissile, Vector3.up).normalized;
                Vector3 alternativeDirection = Vector3.Cross(escapeDirection, Vector3.up).normalized;
                // 选择与导弹相对方向更接近的躲避方向
                if (Vector3.Dot(alternativeDirection, missileDirection) > Vector3.Dot(escapeDirection, missileDirection))
                {
                    escapeDirection = alternativeDirection;
                }
            }
            else
            {
                // 导弹接近时，坦克尝试维持或微调当前前进方向躲避
                escapeDirection = forward + Vector3.Cross(toMissile, Vector3.up) * 0.2f; // 稍微偏离前进方向以躲避
                escapeDirection = escapeDirection.normalized;
            }

            return escapeDirection;
        }

// 执行躲避动作
        private void EscapeFromMissile(Vector3 direction)
        {
            float escapeDistance = 12f; // 默认躲避距离
            float angle = Vector3.Angle(transform.forward, direction);

            // 如果躲避方向与前进方向夹角较小，增加躲避距离以利用坦克的动态优势
            if (angle < 45f)
            {
                escapeDistance *= 1.5f; // 增加躲避距离
            }

            Vector3 newPosition = transform.position + direction * escapeDistance;
            Move(newPosition);
        }
        
        
        
        
        Vector3 PredictTargetPosition(Vector3 targetPos, Vector3 targetVelocity, Vector3 myPosition, float bulletSpeed)
        {
            Vector3 toTarget = targetPos - myPosition;
            float targetMoveAngle = Vector3.Angle(targetVelocity, toTarget) * Mathf.Deg2Rad; // 敌方移动方向与我方与敌方连线方向的夹角
            float targetSpeed = targetVelocity.magnitude;

            // 由于是2D环境，y分量可以忽略
            float a = targetSpeed * targetSpeed - bulletSpeed * bulletSpeed;
            float b = 2 * Vector3.Dot(toTarget, targetVelocity);
            float c = toTarget.sqrMagnitude;

            // 二次方程 Δ = b^2 - 4ac
            float delta = b*b - 4*a*c;

            if(delta < 0)
            {
                // 没有实数解，说明预测不到交点，可以选择不射击或射击当前位置
                return targetPos;
            }

            float t1 = (-b + Mathf.Sqrt(delta)) / (2*a);
            float t2 = (-b - Mathf.Sqrt(delta)) / (2*a);

            float t = Mathf.Max(t1, t2); // 选择一个合适的时间，通常我们选择较大的那个时间
            if (t < 0) t = 0; // 确保时间不是负的

            // 使用预测的时间来计算预测位置
            Vector3 predictedPosition = targetPos + targetVelocity * t;
            return predictedPosition;
        }

        private void GeneralMovementStrategy()
        {
            var oppTanks = Match.instance.GetOppositeTanks(Team, m_CachedOppTanks);
            var oppTank = GetBetterTarget(oppTanks);


            // Decision to move based on a timer to avoid constant recalculations.
            if (Time.time > m_LastTime)
            {
                if (ApproachNextDestination())
                {
                    m_LastTime = Time.time + Random.Range(1.5f, 3f);
                }
            }

            // Enhanced logic for low HP scenarios and star collection.
            if (HP <= 40)
            {
                Move(Match.instance.GetRebornPos(Team));
          
            }
            else
            {
           
                if (!SeekAndMoveToStar(false))
                {
                
                    if (Time.time > m_LastTime)
                    {
                        if (ApproachNextDestination())
                        {
                            m_LastTime = Time.time + Random.Range(2, 5);
                        }
                    }
                }
            }
        }


        private bool SeekAndMoveToStar(bool criticalCondition)
        {
            // 首先获取所有敌方导弹实例，判断是否存在对坦克构成威胁的导弹
            Dictionary<int, Missile> enemyMissiles = new Dictionary<int, Missile>();
            Match.instance.GetOppositeMissilesEx(Team, enemyMissiles); // 假设这里的Team是当前坦克的队伍枚举

            foreach (var missile in enemyMissiles.Values)
            {
                // 利用CanSeeOthers和其他逻辑判断导弹是否构成威胁
                if (IsMissileThreatening(missile) && CanSeeOthers(missile.Position))
                {
                    Vector3 escapeDirection = CalculateEscapeDirection(missile);
                    EscapeFromMissile(escapeDirection); // 执行躲避逻辑
                    return false; // 在这个更新周期内，坦克优先躲避导弹，不执行寻星逻辑
                }
            }

            // 如果没有导弹威胁，继续执行寻找并移向星星的逻辑
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
                else if (!criticalCondition)
                {
            
                    float dist = (s.Position - Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position;
                    }
                }
            }

            if (hasStar)
            {
                Move(nearestStarPos); 
                return true;
            }

            return false; 
        }

        private Tank GetBetterTarget(List<Tank> tanks)
        {
            float minDist = float.MaxValue;
            Tank targetTank = null;
            foreach (var t in tanks)
            {
                if (t.IsDead)
                {
                    continue;
                }
                var dist = (Position - t.Position).sqrMagnitude;
                if (dist < minDist)
                {
                    targetTank = t;
                    minDist = dist;
                }
            }

            return targetTank;
        }

        protected override void OnHandleSendTeamStrategy(Tank sender, int teamStrategy)
        {
            if (teamStrategy == (int)ETeamStrategy.Help)
            {

                m_HelpResponseTime.SetExpiredTime(Time.time + 1f);
                m_HelpPos = sender.GetTeamStratedgyParam<Vector3>((int)TeamStrategyBBKey.HelpPos);
            }
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
        public override string GetName()
        {
            return "YZY";
        }
    }
}
