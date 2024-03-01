using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Main;
using UnityEngine.AI;

namespace HZH
{
    abstract class Condition
    {
        public abstract bool IsTrue(Tank name);
    }

    class TrueCondition : Condition
    {
        public override bool IsTrue(Tank name)
        {
            return true;
        }
    }

    class FalseCondition : Condition
    {
        public override bool IsTrue(Tank name)
        {
            return false;
        }
    }

    class AndCondition : Condition
    {
        private Condition m_left;
        private Condition m_right;

        public AndCondition(Condition left, Condition right)
        {
            m_left = left;
            m_right = right;
        }

        public override bool IsTrue(Tank name)
        {
            return m_left.IsTrue(name) && m_right.IsTrue(name);
        }

    }

    class OrCondition : Condition
    {
        private Condition m_left;
        private Condition m_right;

        public OrCondition(Condition left, Condition right)
        {
            m_left = left;
            m_right = right;
        }

        public override bool IsTrue(Tank name)
        {
            return m_left.IsTrue(name) || m_right.IsTrue(name);
        }

    }

    class XorCondition : Condition
    {
        private Condition m_left;
        private Condition m_right;

        public XorCondition(Condition left, Condition right)
        {
            m_left = left;
            m_right = right;
        }

        public override bool IsTrue(Tank name)
        {
            return m_left.IsTrue(name) ^ m_right.IsTrue(name);
        }
    }

    class NotCondition : Condition 
    {
        private Condition m_left;
        public NotCondition(Condition left)
        {
            m_left = left;
        }

        public override bool IsTrue(Tank name)
        {
            return !m_left.IsTrue(name);
        }
    }
    ////////////////////////////////////////////////////////////////////
    class HPBelow : Condition
    {
        private int m_targetHP;
        public HPBelow(int targetHP)
        {
            m_targetHP = targetHP;
        }

        public override bool IsTrue(Tank name)
        {
            return name.HP <= m_targetHP;
        }
    }

    class HasStar : Condition
    {
        public override bool IsTrue(Tank name)
        {
            return Match.instance.GetStars().Count > 0;
        }
    }

    class HasSuperStar : Condition
    {
        public override bool IsTrue(Tank name)
        {
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                    return true;
            }
            return false;
        }
    }

    class MovetoSuperStar : Condition
    {
        private float m_gettime;
        public MovetoSuperStar(float gettime)
        {
            m_gettime = gettime;
        }
        
        public override bool IsTrue(Tank owner)
        {
            if (Match.instance.GlobalSetting.MatchTime / 2 + m_gettime > Match.instance.RemainingTime)
                if(Match.instance.GlobalSetting.MatchTime / 2 < Match.instance.RemainingTime)
                    return true;            
            return false;
        }
    }

    class HasSeeEnemy : Condition
    {
        public override bool IsTrue(Tank name)
        {
            Tank Enemy = Match.instance.GetOppositeTank(name.Team);
            if (Enemy == null)
                return false;
            bool canSee = false;
            RaycastHit hitInfo;
            if (Physics.Linecast(name.FirePos, Enemy.Position, out hitInfo, PhysicsUtils.LayerMaskCollsion))
            {
                if (PhysicsUtils.IsFireCollider(hitInfo.collider))
                {
                    canSee = true;
                }
            }
            return canSee;
        }
    }

    class HasEnemyReturn : Condition
    {
        public override bool IsTrue(Tank name)
        {
            Tank Enemy = Match.instance.GetOppositeTank(name.Team);
            Vector3 destination = Enemy.Position;
            destination.y = 0;
            return (destination - Match.instance.GetRebornPos(Enemy.Team)).sqrMagnitude < 0.01f;
        }
    }

    class HasEnemyDead : Condition
    {
        public override bool IsTrue(Tank name)
        {
            Tank Enemy = Match.instance.GetOppositeTank(name.Team);
            return Enemy.IsDead && Enemy.GetRebornCD(Time.time)< 2*Match.instance.GlobalSetting.RebonCD/3;
        }
    }

    

    class NeedReturn : Condition
    {
        public override bool IsTrue(Tank name)
        {
            MyTank Mytank = (MyTank)name;
            float starDistance = (Mytank.GetNearestStar() - Mytank.Position).sqrMagnitude;
            float homeDistance = (Mytank.Position - Match.instance.GetRebornPos(Mytank.Team)).sqrMagnitude;
            Tank Enemy = Match.instance.GetOppositeTank(name.Team);
                if(Enemy.HP > Mytank.HP)
                    if (homeDistance < starDistance / 3)
                        return true;
                if (Mytank.HP <= 30) 
                    return true;
                return false;
        }
    }

    class CanChase : Condition
    {
        public override bool IsTrue(Tank name)
        {
            MyTank Mytank = (MyTank)name;
            
            Tank Enemy = Match.instance.GetOppositeTank(name.Team);
            if (Enemy.HP < Mytank.HP)
                if(Enemy.HP < 50)
                    return true;
            if(Enemy.IsDead)
                return false;
            return false;
        }
    }


    class HasStarNearReborn : Condition
    {
        private float m_Radius;
        public HasStarNearReborn(float Radius)
        {
            m_Radius = Radius;
        }
        public override bool IsTrue(Tank name)
        {
            MyTank Mytank = (MyTank)name;
            float DistanceStarToReborn;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                DistanceStarToReborn = (s.Position - Match.instance.GetRebornPos(Mytank.Team)).sqrMagnitude;
                if (DistanceStarToReborn < m_Radius)
                    return true;
            }
            return false;
        }
    }

    class HasDetectMissle : Condition
    {
        private float m_DetectRadius;
        public HasDetectMissle(float DetectRadius)
        {
            m_DetectRadius = DetectRadius * DetectRadius;
        }

        public override bool IsTrue(Tank name)
        {
            MyTank Mytank = (MyTank)name;
            foreach (var missile in Match.instance.GetOppositeMissiles(Mytank.Team))
            {
                float missileDistance = (missile.Value.Position - Mytank.Position).sqrMagnitude;
                if (missileDistance < m_DetectRadius)
                    return true;
            }
            return false;
        }
    }

    public class MyTank : Tank 
    {
        private Condition m_getStar;
        private Condition m_getSuperStar;
        private Condition m_EnemyDead;
        private Condition m_OnBattle;
        private Condition m_Return;
        private Condition m_Chase;
        private Condition m_DodgeMissile;
        private float m_LastTime = 0;

        private float gotime = 4f;//直接前往超级星时间
        private float RebornRadius = 20f;//重生点附近星星探测
        private float MissileDetectRadius = 20f;//导弹探测
        private float lastTime=0;
        private Tank Enemy;
        private Vector3 EnemyLastPosition;


        protected override void OnStart()
        {
            base.OnStart();
            m_getSuperStar = new OrCondition(new MovetoSuperStar(gotime), new HasSuperStar());
            m_EnemyDead = new HasEnemyDead();
            m_OnBattle = new OrCondition(new HasSeeEnemy(), new HasDetectMissle(MissileDetectRadius));
            m_Return = new AndCondition(new OrCondition(new NeedReturn(), m_EnemyDead), new NotCondition(m_getSuperStar));
            //m_Return = new AndCondition(new AndCondition(new HasStarNearReborn(RebornRadius), new NeedReturn()),new NotCondition(m_getSuperStar));
            m_getStar = new AndCondition(new NotCondition(m_getSuperStar), new NotCondition(m_Return));
            m_Chase = new AndCondition(new CanChase(), new NotCondition(new HasEnemyReturn()));
            m_DodgeMissile = new HasDetectMissle(MissileDetectRadius);


            Enemy = Match.instance.GetOppositeTank(Team);
            EnemyLastPosition = Match.instance.GetOppositeTank(Team).Position;
            lastTime = Time.time;

        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Tank Enemy = Match.instance.GetOppositeTank(Team);
            float EnemySpeed = MathTool.GetRightSpeed(EnemyLastPosition, Enemy.Position, lastTime, Time.time);
            Vector3 rightPos = MathTool.GetRightPos(EnemySpeed, Match.instance.GlobalSetting.MissileSpeed, EnemyLastPosition, transform, Enemy.transform);


            if (m_Chase.IsTrue(this))
            {
                Move(Enemy.Position);
            }

            if (m_Return.IsTrue(this)&&HP!=100)
            {
                Move(Match.instance.GetRebornPos(Team));
            }

            if (m_DodgeMissile.IsTrue(this))
            {
                Debug.LogError("DodgeMissle");
                Move(GetDodgeMissilePos(GetNearestMissile().Velocity));
            }

            if (m_OnBattle.IsTrue(this))
            {
                TurretTurnTo(rightPos);
                Fire();
                Debug.LogWarning((Position - Enemy.transform.position).sqrMagnitude);
            }
            else
            {
                if (Enemy.IsDead)
                    TurretTurnTo(Match.instance.GetRebornPos(Enemy.Team));
                else
                    TurretTurnTo(rightPos);
            }

            if (m_getStar.IsTrue(this))
            {
                Move(GetNearestStar());
            }
            else
            {
                if (Time.time > m_LastTime)
                {
                    if (ApproachNextDestination())
                    {
                        m_LastTime = Time.time + Random.Range(3, 8);
                    }
                }
            }

            if (m_getSuperStar.IsTrue(this))
            {
                Move(Vector3.zero);
            }

            EnemyLastPosition =Match.instance.GetOppositeTank(Team).Position;;
            lastTime = Time.time;
        }


        public Vector3 GetNearestStar()
        {
            float NearestStarDistance = float.MaxValue;
            Vector3 NearestStarPos = Vector3.zero;
            
            foreach (var star in Match.instance.GetStars())
            {
                Star s = star.Value;
                float distance = (s.Position - Position).sqrMagnitude;
                float EnemytoStar = (s.Position - Enemy.Position).sqrMagnitude;
                if (distance < NearestStarDistance)
                {
                    NearestStarDistance = distance;
                    NearestStarPos = s.Position;
                }
            }
            return NearestStarPos;
        }

        private Missile GetNearestMissile()
        {
            int nearestID = int.MaxValue;
            foreach (var missile in Match.instance.GetOppositeMissiles(Team))
            {
                if (missile.Key < nearestID)
                {
                    nearestID = missile.Key;
                }
            }
            return Match.instance.GetOppositeMissiles(Team)[nearestID];
        }

        private Vector3 GetDodgeMissilePos(Vector3 missileVelocity)
        {
            Vector3 normal = Vector3.Cross(Vector3.up, missileVelocity).normalized;
            RaycastHit hit_1, hit_2;
            float hit1Distance = 0, hit2Distance = 0;
            if (Physics.Linecast(Position, Position + normal * 1000, out hit_1, PhysicsUtils.LayerMaskScene))
            {
                hit1Distance = (hit_1.point - Position).sqrMagnitude;
            }
            if (Physics.Linecast(Position, Position - normal * 1000, out hit_2, PhysicsUtils.LayerMaskScene))
            {
                hit2Distance = (hit_2.point - Position).sqrMagnitude;
            }

            if (hit1Distance > hit2Distance)
            {
                return Position + normal * (hit_1.point - Position).magnitude * .8f;
            }
            return Position + normal * (hit_2.point - Position).magnitude * .8f;
        }

        private bool ApproachNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            m_LastTime = 0;
        }

        public override string GetName()
        {
            return "HZH";
        }

    }
    //////////////////////////////////////////

    public class MathTool : MonoBehaviour
    {
        /// <summary>
        /// 计算目标 当前帧的移动速度
        /// </summary>
        /// <param name="lastPos"></param>
        /// <param name="currentPos"></param>
        /// <param name="lastTime"></param>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        public static float GetRightSpeed(Vector3 lastPos, Vector3 currentPos, float lastTime, float currentTime)
        {
            if ((currentTime - lastTime) < 0)
                return -1;
            else if ((currentTime - lastTime) == 0)
                return 0;
            else
                return Vector3.Distance(currentPos, lastPos) / (currentTime - lastTime);
        }

        /// <summary>
        /// 计算目标 正确的移动方向
        /// </summary>
        /// <param name="lastPos"></param>
        /// <param name="currentPos"></param>
        /// <returns></returns>
        public static Vector3 GetDirection(Vector3 lastPos, Vector3 currentPos)
        {
            return (currentPos - lastPos).normalized;
        }

        /// <summary>
        /// 计算出 detla
        /// </summary>
        /// <param name="b"></param>
        /// <param name="a"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static float GetDetla(float b, float a, float c)
        {
            return b * b - 4 * a * c;
        }

        /// <summary>
        /// 计算目标 正确的碰撞时间
        /// </summary>
        /// <param name="b"></param>
        /// <param name="detla"></param>
        /// <param name="a"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static float GetRightTime(float b, float detla, float a, float c)
        {
            if (detla < 0) //判断 detla < 0 则方程无解
            {
                return -1;
            }
            else if (a == 0)   //at^2 + bt +c = 0  当 a == 0 时 。 
            {
                return -(b / c);
            }
            else if (detla == 0)   //detla == 0 时。
            {
                return -(b / 2 * a);
            }
            else
            {
                float time1 = (-b + Mathf.Sqrt(detla)) / (2 * a);
                float time2 = (-b - Mathf.Sqrt(detla)) / (2 * a);
                return time1 > time2 ? time1 : time2;
            }

        }

        /// <summary>
        /// 计算目标 正确的目标移动位置
        /// </summary>
        /// <param name="targetSpeed"></param>
        /// <param name="selfSpeed"></param>
        /// <param name="lastPos"></param>
        /// <param name="self"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static Vector3 GetRightPos(float targetSpeed, float selfSpeed, Vector3 lastPos, Transform self, Transform target)
        {
            if (targetSpeed == -1)
                return Vector3.zero;
            Vector3 targetDir = GetDirection(lastPos, target.position);                    //获取 目标 移动方向
            Vector3 AB = target.position - self.position;
            float angle = Vector3.Angle(AB, targetDir);         //获取角度
            float L1 = Mathf.Sin(angle * Mathf.Deg2Rad) * AB.magnitude;         //计算L1
            float L2 = Mathf.Cos(angle * Mathf.Deg2Rad) * AB.magnitude;         //计算L2
            float a = targetSpeed * targetSpeed - selfSpeed * selfSpeed;      //计算 a
            float b = 2 * targetSpeed * L2;                                   //计算 b
            float c = L1 * L1 + L2 * L2;                                      //计算 c
            if (Vector3.Dot(AB, targetDir) < 0)                   //判断 同向 还是 反向
            {
                b *= -1;                                                      //如果 反向 则b应该为相反数
            }
            float detla = GetDetla(b, a, c);                      //计算 detla
            float rightTime = GetRightTime(b, detla, a, c);                //计算 正确的时间
            if (rightTime == -1)
                return Vector3.zero;
            Vector3 rightPos = targetDir * rightTime * targetSpeed + target.position;   //计算 正确的 目标点
            return rightPos;
        }
    }






}
