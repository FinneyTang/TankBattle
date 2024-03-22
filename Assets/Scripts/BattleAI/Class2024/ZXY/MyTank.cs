using System;
using System.Collections.Generic;
using AI.Base;
using AI.BehaviourTree;
using AI.Blackboard;
using AI.RuleBased;
using Main;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;


namespace ZXY
{
    #region OwnClasses

    public static class StaticVar
    {
        public static float HitCandy = 0.9f;

        public static void HitAndAddHitCandy()
        {
            HitCandy = Mathf.Clamp01(HitCandy);
        }

        public static void UnHitAndDeleteHitCandy()
        {
            HitCandy -= 0.05f;
            HitCandy = Mathf.Clamp01(HitCandy);
        }

        public static bool CanSeePosition(Vector3 pos1, Vector3 pos2)
        {
            return !Physics.Linecast(pos1, pos2, PhysicsUtils.LayerMaskScene);
        }
        
        public static bool CheckWall(Vector3 origin, Vector3 dir)
        {
            return Physics.Raycast(origin, dir, dir.magnitude, PhysicsUtils.LayerMaskScene);
        }
    }

    #endregion

    #region Condition

    class CanSeeEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (!oppTank) return false;

            return t.CanSeeOthers(oppTank);
        }
    }

    class InMustHitDistance : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);

            Vector3 toTarget = (oppTank.Position - t.Position); // 从你到目标的向量

            if (Vector3.Distance(oppTank.Position, t.Position) < 15)
            {
                return true;
            }

            return false;
        }
    }

    #endregion

    #region Action

    #region FireAction

    class PreFire : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            return t.CanFire() && !Match.instance.GetOppositeTank(t.Team).IsDead;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;

            t.PreFire();

            return ERunningStatus.Executing;
        }
    }

    class Fire : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            return t.CanFire() && !Match.instance.GetOppositeTank(t.Team).IsDead;
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemroy)
        {
            MyTank t = (MyTank)agent;

            t.Fire();

            if (!new InMustHitDistance().IsTrue(agent))
            {
                t.TrackMissile(t.Team);
            }

            return ERunningStatus.Executing;
        }
    }

    class PreTurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;

            Tank target = Match.instance.GetOppositeTank(t.Team);
            //
            // //40 = missile speed
            // float time = Vector3.Distance(t.FirePos, target.Position) / 40;
            //
            // var nav = target.GetComponent<NavMeshAgent>();
            //
            // //10 = tank speed
            // float speed = nav == null ? 10 : nav.speed;

            if (target != null && target.IsDead == false)
            {
                t.TurretTurnTo(t.CalculateInterceptPoint());
                //t.TurretTurnTo(target.Position + target.Forward * (time * speed * StaticVar.hitCandy));
            }
            else
            {
                t.TurretTurnTo(Match.instance.GetRebornPos(t.enemyTeam));
            }

            return ERunningStatus.Executing;
        }
    }

    class TurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                //t.TurretTurnTo(oppTank.Position);
                t.TurretTurnTo(oppTank.Position);
            }
            else
            {
                t.TurretTurnTo(Match.instance.GetRebornPos(t.enemyTeam));
            }

            return ERunningStatus.Executing;
        }
    }

    #endregion

    #region MoveAction

    public class FindStar : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;
            Tank oppo = Match.instance.GetOppositeTank(t.Team);

            //意味着特殊行动
            if (t.state != BTState.BasicMove)
                return false;

            var stars = Match.instance.GetStars();

            if (stars.Count < 1 && t.HP > 80)
            {
                //血限健康 在中间架狙
                workingMemory.SetValue((int)BTKey.MovingTargetPos, Vector3.zero);
                return false;
            }
            else if (stars.Count < 1)
            {
                t.state = BTState.StayHome;
                workingMemory.SetValue((int)BTKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                return false;
            }

            SortedDictionary<float, Star> starDis = new SortedDictionary<float, Star>();

            foreach (var star in stars)
            {
                Star s = star.Value;

                if (s.IsSuperStar)
                {
                    workingMemory.SetValue((int)BTKey.MovingTargetPos, s.Position);
                    t.state = BTState.MoveToSuperStar;
                    //不进行任何计算了，为了超级星星！
                    return false;
                }

                //分数
                float distance = Vector3.Distance(s.Position, t.Position);

                if (t.HP < 80 && oppo.IsDead)
                {
                    if (t.areaDic[t.GetArea(s.Position)] != Safety.Danger)
                    {
                        starDis.Add(distance, s);
                    }
                }
                else
                {
                    starDis.Add(distance, s);
                }
            }

            SortedDictionary<float, Star> starScore = new SortedDictionary<float, Star>();

            foreach (var star in starDis)
            {
                //float enemyDisScore = 1 - Vector3.Distance(star.Value.Position, oppo.Position) / 140.0f; //距离分

                float areaScore = t.ScoreArea(star.Value.Position); //区域分

                float distanceScore = (1 - star.Key / 140.0f) * 0.5f; //距离分, 越近越大

                //distanceScore = enemyDisScore > distanceScore ? enemyDisScore - distanceScore : distanceScore;

                float finalScore = areaScore * Mathf.Clamp01(distanceScore);

                bool sameArea = false;

                foreach (var s in starScore)
                {
                    if (Math.Abs(t.ScoreArea(s.Value.Position) - t.ScoreArea(s.Value.Position)) < 0.1f)
                    {
                        sameArea = true;
                    }
                }

                if (!sameArea) starScore.Add(finalScore, star.Value);
            }

            foreach (var star in starScore)
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, star.Value.Position);
                return true;
            }

            return false;
        }
    }

    public class BackToHome : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;
            Tank oppo = (Tank)Match.instance.GetOppositeTank(t.Team);

            //超级星星准备状态
            if (t.state == BTState.PrepareSuperStar)
            {
                //为了超级星星回血
                if (t.HP > 80)
                {
                    t.state = BTState.MoveToSuperStar;
                    return true;
                }
                else
                {
                    //在家 = 无敌，别动了
                    workingMemory.SetValue((int)BTKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                    return false;
                }
            }

            //超级星星要来了
            if (Match.instance.RemainingTime > 90 && Match.instance.RemainingTime < 105 &&
                t.state != BTState.PrepareSuperStar && t.state != BTState.MoveToSuperStar)
            {
                t.state = BTState.PrepareSuperStar;
                workingMemory.SetValue((int)BTKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));

                return true;
            }

            float tHP = t.HP / 20;
            float oppoHP = oppo.HP / 20;
            
            //平常的状态
            //我方比对方少血且血量低于50, 跑！
            if (tHP < oppoHP && tHP < 4)
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                t.state = BTState.StayHome;

                return true;
            }

            //小于30血且对方没死
            if (t.HP < 30 && !oppo.IsDead)
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                t.state = BTState.StayHome;
                return true;
            }

            var stars = Match.instance.GetStars();

            if (oppo.IsDead && stars.Count > 0)
            {
                float distanceToHome = Vector3.Distance(t.Position, Match.instance.GetRebornPos(t.Team));

                foreach (var star in stars)
                {
                    t.areaDic.TryGetValue(t.GetArea(star.Value.Position), out Safety safety);

                    if (Vector3.Distance(star.Value.Position, t.Position) < distanceToHome && safety != Safety.Danger)
                    {
                        t.state = BTState.BasicMove;
                        return false;
                    }

                    if (safety == Safety.Danger && t.HP < 80 &&
                        Vector3.Distance(star.Value.Position, t.Position) > distanceToHome)
                    {
                        t.state = BTState.StayHome;
                        workingMemory.SetValue((int)BTKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                        return true;
                    }
                }
            }

            //没星星没血，回家过年
            if (t.HP < 85 && Match.instance.GetStars().Count < 1)
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
                t.state = BTState.StayHome;
                return true;
            }

            if (t.HP > 60 && t.state == BTState.StayHome)
            {
                t.state = BTState.BasicMove;
                return false;
            }

            return t.state is BTState.StayHome or BTState.PrepareSuperStar;
        }
    }

    public class MoveForSuperStar : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;

            if (t.state != BTState.MoveToSuperStar)
            {
                return false;
            }

            bool hasSuperStar = false;

            if (Match.instance.RemainingTime < 89.5f)
            {
                foreach (var star in Match.instance.GetStars())
                {
                    if (star.Value.IsSuperStar)
                    {
                        hasSuperStar = true;
                    }
                }

                if (!hasSuperStar)
                {
                    t.state = BTState.BasicMove;
                    return false;
                }
            }
            
            if (Match.instance.RemainingTime <= 98)
            {
                //冲刺！
                workingMemory.SetValue((int)BTKey.MovingTargetPos, Vector3.zero);
                return true;
            }

            return false;
        }
    }

    public class AvoidMissile : ActionNode
    {
        private List<Missile> _missiles = new List<Missile>();
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;
            
            Tank opp = Match.instance.GetOppositeTank(t.Team);

            if (new InMustHitDistance().IsTrue(agent))
            {
                return false;
            }

            if (Vector3.Distance(t.Position, Match.instance.GetRebornPos(t.Team)) < 20f)
            {
                return false;
            }
            
            if (t.state == BTState.MoveToSuperStar)
            {
                return false;
            }
            
            var missiles = Match.instance.GetOppositeMissiles(t.Team);

            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            var navMeshAgent = t.GetComponent<NavMeshAgent>();
            
            foreach (var m in missiles)
            {
                var missile = m.Value;
                if (WillBulletHit(missile.Position, missile.Velocity, t.Position, navMeshAgent, t, workingMemory, missile))
                {
                    if (!_missiles.Contains(missile))
                    {
                        _missiles.Add(missile);
                    }
                    return true;
                }
                
            }

            for (int i = 0; i < _missiles.Count; i++)
            {
                if (_missiles[i] == null)
                {
                    _missiles.Remove(_missiles[i]);
                }
            }

            if (_missiles.Count > 0)
            {
                return true;
            }
            
            return false;
        }

        // 检查子弹是否会命中移动的目标
        private bool WillBulletHit(Vector3 bulletPosition, Vector3 bulletDirection, Vector3 targetPosition, NavMeshAgent nav, MyTank t, BlackboardMemory workingMemory, Missile missile = null) // by chat gpt
        {
            Vector3 targetVelocity = nav.velocity;
            NavMeshPath targetPath = nav.path;
    
            float bulletSpeed = 40f;
            // 根据子弹的速度和方向计算子弹的速度向量
            Vector3 bulletVelocity = bulletDirection.normalized * bulletSpeed;
            
            Debug.DrawRay(bulletPosition, bulletVelocity, Color.red);
    
            float distanceToTarget = Vector3.Distance(bulletPosition, targetPosition);
            float predictionTime = distanceToTarget / bulletSpeed;

            // 基于目标的当前速度和路径预测目标的未来位置
            Vector3 predictedTargetPosition = PredictTargetPosition(targetPosition, targetVelocity, targetPath, predictionTime);

            // 检查子弹和预测的目标位置之间是否有障碍物
            if (Physics.Linecast(bulletPosition, predictedTargetPosition, PhysicsUtils.LayerMaskScene))
            {
                // 如果有障碍物，子弹将不会命中目标
                return false;
            }

            // 预测未来位置
            bool willHit = CheckIfBulletWillHitCharacter(predictedTargetPosition, bulletPosition, bulletVelocity, predictionTime);
            
            if (willHit)
            {
                Dodge(bulletPosition, bulletDirection, predictedTargetPosition, t, workingMemory); // 执行躲避动作
                //Avoid(missile, t, workingMemory);
                return true; // 子弹会命中目标
            }

            return false; // 子弹不会命中目标
        }
        
        // 预测目标在NavMeshPath上的未来位置
        private Vector3 PredictTargetPosition(Vector3 currentTargetPosition, Vector3 targetVelocity, NavMeshPath path, float predictionTime) // by chat gpt
        {
            // 目标每秒移动的距离
            float speed = targetVelocity.magnitude;
            // 已经预测的距离
            float distancePredicted = 0.0f;
            // 当前位置
            Vector3 currentPosition = currentTargetPosition;

            // 如果路径无效或只有一个点，直接根据速度和时间预测位置
            if (path == null || path.corners.Length < 2)
            {
                return currentTargetPosition + (targetVelocity.normalized * (speed * predictionTime));
            }

            // 遍历路径点
            for (int i = 1; i < path.corners.Length; i++)
            {
                Vector3 start = path.corners[i - 1];
                Vector3 end = path.corners[i];
                float distanceBetweenCorners = Vector3.Distance(start, end);

                // 如果当前段的预测距离加上段距离小于等于总预测距离
                if (distancePredicted + distanceBetweenCorners <= speed * predictionTime)
                {
                    distancePredicted += distanceBetweenCorners;
                    currentPosition = end;
                }
                else
                {
                    // 找到在当前段上的预测点
                    float remainingDistance = speed * predictionTime - distancePredicted;
                    Vector3 direction = (end - start).normalized;
                    currentPosition = start + direction * remainingDistance;
                    break;
                }
            }

            return currentPosition;
        }
        
        private bool CheckIfBulletWillHitCharacter(Vector3 predictedTargetPosition, Vector3 bulletPosition, Vector3 bulletVelocity, float predictionTime)
        {
            // 检查未来位置是否足够接近以考虑为命中（考虑矩形体积）
            // 创建矩形的四个角
            Vector3 futureBulletPosition = bulletPosition + bulletVelocity * predictionTime;
            
            // 考虑角色体积的碰撞检测
            Vector3 size = new Vector3(4.8f, 4, 4.8f); // 角色的体积大小
            float halfWidth = size.x / 2;
            float halfLength = size.z / 2;
            
            Vector3[] corners =
            {
                predictedTargetPosition + new Vector3(halfWidth, 0, halfLength),
                predictedTargetPosition + new Vector3(-halfWidth, 0, halfLength),
                predictedTargetPosition + new Vector3(halfWidth, 0, -halfLength),
                predictedTargetPosition + new Vector3(-halfWidth, 0, -halfLength)
            };

            // 检查子弹路径与任一角落的距离
            foreach (Vector3 corner in corners)
            {
                if (Vector3.Distance(futureBulletPosition, corner) < 2f) 
                {
                    return true;
                }
            }
            
            // 检查子弹路径与矩形中心的距离
            if (Vector3.Distance(futureBulletPosition, predictedTargetPosition) < 6f)
            {
                return true;
            }

            if (_missiles.Count > 0)
            {
                return true;
            }
            
            return false; // 假设这里是检测逻辑的结果
        }
        
        private void Dodge(Vector3 bulletPosition, Vector3 bulletVelocity, Vector3 predictedTargetPosition, MyTank t, BlackboardMemory workingMemory)
        {
            
            float angle = 95;
            float moveDis = 7f;

            if (t.HP < 45)
            {
                angle = 35;
                moveDis = 9f;
            }

            if (t.state == BTState.PrepareSuperStar)
            {
                angle = 15;
            }
            
            Quaternion rotationRight45 = Quaternion.AngleAxis(angle, Vector3.up);
            
            Vector3 dodgeDirectionRight = rotationRight45 * t.Forward.normalized;
            
            // 旋转炮塔瞄准方向-45度来得到左前方的躲避方向
            Quaternion rotationLeft45 = Quaternion.AngleAxis(-angle, Vector3.up);
            Vector3 dodgeDirectionLeft = rotationLeft45 * t.Forward.normalized;


            
            Vector3 dodgePositionRight = t.Position + dodgeDirectionRight * moveDis; 
            Vector3 dodgePositionLeft = t.Position + dodgeDirectionLeft * moveDis;

            // 检测左右两边是否存在墙
            bool wallOnRight = Physics.Raycast(t.Position, dodgeDirectionRight, moveDis, PhysicsUtils.LayerMaskScene);
            bool wallOnLeft = Physics.Raycast(t.Position, dodgeDirectionLeft, moveDis, PhysicsUtils.LayerMaskScene);
            
            NavMeshHit hitRight, hitLeft;

            bool canDodgeRight = NavMesh.SamplePosition(dodgePositionRight, out hitRight, moveDis, NavMesh.AllAreas);
            bool canDodgeLeft = NavMesh.SamplePosition(dodgePositionLeft, out hitLeft, moveDis, NavMesh.AllAreas);

            // 根据子弹的当前位置和方向评估哪个躲避方向更安全
            float distanceAfterDodgeRight = Vector3.Distance(hitRight.position, bulletPosition + bulletVelocity);
            float distanceAfterDodgeLeft = Vector3.Distance(hitLeft.position, bulletPosition + bulletVelocity);

            if (_missiles.Count > 0 && t.GetComponent<NavMeshAgent>().velocity.magnitude > 3f)
            {
                return;
            }
            
            bool hasHit =  Physics.SphereCast(bulletPosition, 0.2f, bulletVelocity.normalized, out RaycastHit hit);
            
            if (canDodgeRight && (!canDodgeLeft || distanceAfterDodgeRight > distanceAfterDodgeLeft) && !wallOnRight)
            {
                // 如果只能向右前方躲避或右前方更安全
                workingMemory.SetValue((int)BTKey.MovingTargetPos, hitRight.position);
            }
            else if (canDodgeLeft && !wallOnLeft)
            {
                // 如果只能向左前方躲避或左前方更安全
                workingMemory.SetValue((int)BTKey.MovingTargetPos, hitLeft.position);
            }
            else if(hasHit && hit.transform.GetComponentInParent<MyTank>() != t)
            {
                // 如果左右前方都无法躲避，保持当前位置
                workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position);
            }
            else
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position - t.Forward * 3f);
            }
        }
        
        private void Avoid(Missile missile, MyTank t, BlackboardMemory workingMemory)
        {
            Vector3 tankForward = t.Forward * 3f;
            Vector3 direction = Vector3.Cross(missile.Velocity.normalized, t.transform.up) * 7f;
            
            if (!StaticVar.CheckWall(t.Position, direction + tankForward) &&
                !StaticVar.CheckWall(t.Position, -direction + tankForward))
            {
                if (StaticVar.CanSeePosition(t.Position + direction + tankForward, missile.Position))
                {
                    workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position + direction + tankForward);
                }
                else if(StaticVar.CanSeePosition(t.Position - direction + tankForward, missile.Position))
                {
                    workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position + direction + tankForward);
                }
                else
                {
                    float side = Vector3.Dot(direction, t.Forward);
                    if (side >= 0)
                    {
                        workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position + direction + tankForward);
                    }
                    else
                    {
                        workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position - direction + tankForward);
                    }
                }
            }
            else if(!StaticVar.CheckWall(t.Position, direction + tankForward))
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position + direction + tankForward);
            }
            else if(!StaticVar.CheckWall(t.Position, -direction + tankForward))
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position - direction + tankForward);
            }
            else
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, t.Position);
            }
        }
        
    }

    public class Chase : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            MyTank t = (MyTank)agent;
            Tank opp = (Tank)Match.instance.GetOppositeTank(t.Team);

            float tHP = t.HP / 20;
            float oppHP = opp.HP / 20;
            
            if (tHP < oppHP || opp.IsDead)
            {
                return false;
            }

            if (tHP > oppHP && oppHP < 4 && Vector3.Distance(opp.Position, Match.instance.GetRebornPos(opp.Team)) > 35f &&
                Vector3.Distance(t.Position, opp.Position) < 14)
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, opp.Position);
                return true;
            }

            if (t.HP >= opp.HP + 30 && Vector3.Distance(opp.Position, Match.instance.GetRebornPos(opp.Team)) > 20f) //比你多两发
            {
                workingMemory.SetValue((int)BTKey.MovingTargetPos, opp.Position);
                return true;
            }

            return false;
        }
    }

    class MoveTo : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return workingMemory.HasValue((int)BTKey.MovingTargetPos);
        }

        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            t.Move(workingMemory.GetValue<Vector3>((int)BTKey.MovingTargetPos));

            return ERunningStatus.Finished;
        }
    }

    #endregion

    #endregion

    public enum BTKey
    {
        MovingTargetPos = 0,
    }

    public enum BTState
    {
        StayHome = 1,
        Chase = 2,
        PrepareSuperStar = 3,
        MoveToSuperStar = 4,
        BasicMove = 5
    }

    public enum Area
    {
        Area1,
        Area2,
        Area3,
        Area4,
        Center
    }

    public enum Safety
    {
        Safe, //优先级最高 但低于superStar
        Normal, //优先级低于Safe
        LowerNormal,
        Danger //敌方不死不去
    }
    //50 50 ,0 50, 0 0, 50 0 Area 1 
    //50 -50, 0 -50, 0 0, 50 0 Area 2 respon
    //-50 50, 0 50, 0 0, -50 0 Area 3 respon
    //-50 -50, 0 -50, 0 0, 50 0 Area 4 
    

    public class MyTank : Tank
    {
        /* 行为分解
         * 1.移动
         * 2.炮台旋转
         * 3.开火(maybe)
         */

        //public Dictionary<Tank, Information> informations = new Dictionary<Tank, Information>();
        [HideInInspector] public ETeam enemyTeam;

        private BlackboardMemory _workingMemory;
        private Node _btNode;

        private int _enemyHp = 100;
        private Vector3 oppLastPosition;
        private Vector3 oppDirection;
        private Vector3 oppVelocity;

        private List<Missile> enemyMissileList = new List<Missile>();

        private List<Missile> myTankMissleList = new List<Missile>();
        private List<Missile> myTankMisslesLateTrack = new List<Missile>();

        public readonly Dictionary<Area, Safety> areaDic = new Dictionary<Area, Safety>();
        public BTState state = BTState.BasicMove;

        protected override void OnStart()
        {
            base.OnStart();

            SetEnemyTeam();
            DefineArea();

            oppLastPosition = Match.instance.GetOppositeTank(Team).Position;

            _workingMemory = new BlackboardMemory();
            _btNode = new ParallelNode(2).AddChild(
                new SelectorNode().AddChild( //旋转
                    new PreTurnTurret().SetPrecondition(new NotCondition(new InMustHitDistance())),
                    new TurnTurret().SetPrecondition(new InMustHitDistance())
                ),
                new SelectorNode().AddChild( //开火
                    new Fire().SetPrecondition(new CanSeeEnemy())),
                new PreFire().SetPrecondition(new NotCondition(new InMustHitDistance()) //敌人不在必中距离的时候
                ),
                new SelectorNode().AddChild( //Move Strategy
                    new Chase(),
                    new AvoidMissile(),
                    new MoveForSuperStar(),
                    new BackToHome(),
                    new FindStar()
                ),
                new MoveTo()
            );
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            MissileTracker();
            EnemyLastPosition();

            //TestCode();
            RunBTree();
        }

        private void LateUpdate()
        {
            MyTankMissilesHitTracker();
        }

        private void EnemyLastPosition()
        {
            var position = Match.instance.GetOppositeTank(Team).Position;


            // oppVelocity = Vector3.Distance(oppLastPosition, position) /
            //               Time.deltaTime;
            //
            // oppDirection = position - oppLastPosition;
            //
            // oppLastPosition = position;

            oppVelocity = Match.instance.GetOppositeTank(Team).GetComponent<NavMeshAgent>().velocity;
        }

        /// <summary>
        /// 预判开火计数
        /// </summary>
        private int preHitCount = 0;

        private int preMisCount = 0;

        private void MyTankMissilesHitTracker()
        {
            for (int i = 0; i < myTankMisslesLateTrack.Count; i++)
            {
                if (myTankMisslesLateTrack[i] == null)
                {
                    myTankMisslesLateTrack.Remove(myTankMisslesLateTrack[i]);

                    //如果受伤了，那么认为该次预判命中
                    if (_enemyHp > Match.instance.GetOppositeTank(Team).HP)
                    {
                        preHitCount++;
                    }
                    else
                    {
                        preMisCount++;
                    }
                }
            }

            if (preHitCount > 8)
            {
                preHitCount = 0;
                StaticVar.HitAndAddHitCandy();
            }

            if (preMisCount > 4)
            {
                preHitCount = 0;
                StaticVar.UnHitAndDeleteHitCandy();
            }
        }

        private void MissileTracker()
        {
            _enemyHp = Match.instance.GetOppositeTank(Team).HP;

            OwnMyTankTracker();
        }

        /// <summary>
        /// 在所有函数调用前记录所有的己方当前的子弹
        /// </summary>
        private void OwnMyTankTracker()
        {
            myTankMissleList = new List<Missile>();

            Dictionary<int, Missile> myMissiles = Match.instance.GetOppositeMissiles(enemyTeam);

            foreach (var item in myMissiles)
            {
                if (!myTankMissleList.Contains(item.Value))
                {
                    myTankMissleList.Add(item.Value);
                }
            }
        }

        /// <summary>
        /// 在预瞄函数调用后会调用函数，比较Match与本地存储的子弹，是否有多，是否有少
        /// 多的加入lateTrackList跟踪
        /// </summary>
        private void LateOwnTankTracker()
        {
            Dictionary<int, Missile> myMissiles = Match.instance.GetOppositeMissiles(enemyTeam);

            foreach (var item in myMissiles)
            {
                if (myTankMissleList.Contains(item.Value))
                {
                    myTankMissleList.Remove(item.Value);
                }
                else
                {
                    //将需要追踪的目标塞入
                    myTankMisslesLateTrack.Add(item.Value);
                }
            }
        }

        /// <summary>
        /// 开始追踪子弹数据
        /// </summary>
        /// <param name="team">需要追踪的子弹的队伍</param>
        public void TrackMissile(ETeam team)
        {
            if (team == Team)
            {
                LateOwnTankTracker();
            }
            else
            {
                Debug.Log("Enemy Missile");
            }
        }

        private void TestCode()
        {
            //TODO:记得删
            HP = 100;
        }

        private void RunBTree()
        {
            BehaviourTreeRunner.Exec(_btNode, this, _workingMemory);
        }

        private void SetEnemyTeam()
        {
            if (Team == ETeam.A)
            {
                enemyTeam = ETeam.B;
            }
            else if (Team == ETeam.B)
            {
                enemyTeam = ETeam.A;
            }
            else
            {
                Debug.LogWarning("队伍名称不是A或者B, 重新设置");
            }
        }

        //预瞄
        public void PreFire()
        {
            //var target = Match.instance.GetOppositeTank(Team);
            //只有看不见的情况下会进入这个函数
            //move direction = target.forward
            //aim Point
            // float time = Vector3.Distance(FirePos, target.Position) / 40;
            //
            // var nav = target.GetComponent<NavMeshAgent>();
            //
            // //10 = tank speed
            // float speed = nav == null ? 10 : nav.speed;

            //rayCast aim point
            //Vector3 aimPoint = target.Position + target.Forward * (time * speed);

            Vector3 aimPoint = CalculateInterceptPoint();

            Vector3 direction = (aimPoint - FirePos);

            float distance = Vector3.Distance(aimPoint, FirePos);
            //hit point

            // bool isHit = Physics.Raycast(new Ray(FirePos, direction), out RaycastHit hit,
            //     Vector3.Distance(FirePos, aimPoint));

            direction.y = FirePos.y;

            bool isHit = Physics.SphereCast(new Ray(FirePos, TurretAiming), 0.35f, out RaycastHit hit, distance,
                PhysicsUtils.LayerMaskScene);

            Vector3 targetDirection = aimPoint - Position;

            float angle = Vector3.Angle(TurretAiming, targetDirection);

            if (!isHit && angle < 15)
            {
                Fire();
            }
        }

        public Vector3 CalculateInterceptPoint() //by chat gpt 
        {
            Tank target = Match.instance.GetOppositeTank(Team);

            Vector3 targetVelocity = oppVelocity;

            if (targetVelocity.magnitude < 6)
            {
                return target.Position;
            }

            Vector3 direction = target.Position - FirePos;

            direction.y = FirePos.y;

            float cosp = Mathf.Cos(Vector3.Angle(-direction, targetVelocity) * (Mathf.PI / 180));
            
            float a = targetVelocity.sqrMagnitude - (40 * 40);
            
            float b = 2.0f * cosp * direction.magnitude * oppVelocity.magnitude;
            
            float c = direction.sqrMagnitude;

            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
            {
                // 没有解决方案，直接瞄准目标当前位置
                return target.Position;
            }
            else
            {
                // 计算需要的时间来击中目标
                float t1 = (-b + Mathf.Sqrt(discriminant)) / (2 * a);
                float t2 = (-b - Mathf.Sqrt(discriminant)) / (2 * a);

                float t = Mathf.Max(t1, t2);

                // 如果计算出的时间为负，表示目标不可击中，直接瞄准当前位置
                if (t < 0)
                {
                    return target.Position;
                }

                // 预瞄点：目标当前位置加上目标运动方向上的预测位置
                //hitCandy 对预瞄坐标系数的影响，最大为1
                Vector3 interceptPoint = target.Position + targetVelocity * (t * StaticVar.HitCandy);

                //TODO: 做预瞄的时候如果扫到了星星，返回星星
                if (Physics.Raycast(new Ray(FirePos, TurretAiming), out RaycastHit hit))
                {
                    if (hit.transform.GetComponent<Star>())
                    {
                        return hit.transform.position;
                    }
                }

                return interceptPoint;
            }
        }

        private void DefineArea()
        {
            Vector3 safePosition = Match.instance.GetRebornPos(Team);
            Area area = GetArea(safePosition);

            areaDic.Add(area, Safety.Safe);

            Vector3 dangerPosition = Match.instance.GetRebornPos(enemyTeam);
            area = GetArea(dangerPosition);


            areaDic.Add(area, Safety.Danger);

            areaDic.Add(Area.Area1, Safety.Normal);
            areaDic.Add(Area.Area4, Safety.LowerNormal);
            areaDic.Add(Area.Center, Safety.Normal);
        }

        //50 50 ,0 50, 0 0, 50 0 Area 1 
        //50 -50, 0 -50, 0 0, 50 0 Area 2 respon
        //-50 50, 0 50, 0 0, -50 0 Area 3 respon
        //-50 -50, 0 -50, 0 0, 50 0 Area 4 
        public Area GetArea(Vector3 position)
        {
            // 忽略Y轴，只考虑X和Z轴
            if (position.x is >= -10 and <= 10 && position.z is >= -30 and <= 30)
            {
                return Area.Center;
            }
            else if (position.x > 0 && position.z > 0)
            {
                // Area 1
                return Area.Area1;
            }
            else if (position.x > 0 && position.z < 0)
            {
                // Area 2
                return Area.Area2;
            }
            else if (position.x < 0 && position.z > 0)
            {
                // Area 3
                return Area.Area3;
            }
            else
            {
                // Area 4
                return Area.Area4;
            }
        }

        public float ScoreArea(Vector3 position)
        {
            Area area = GetArea(position);

            if (area == Area.Center) return 0.4f;

            Safety safety = areaDic[area];

            switch (safety)
            {
                case Safety.Safe:
                    return 1;
                case Safety.Normal:
                    return 0.23f;
                case Safety.LowerNormal:
                    return 0.27f;
                case Safety.Danger:
                    return 0.05f;

                default:
                    Debug.LogWarning("不应该进来的啊，怎么回事呢");
                    return 0;
            }
        }

        public override string GetName()
        {
            return "ZXY TANK";
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
        }
    }
}