using Main;
using UnityEngine;
using UnityEngine.AI;


namespace XJX
{
    class MyTank : Tank
    {
        private bool IsControll;
        private void OnEnable()
        {
            //gameObject.AddComponent<XJXDefense>();//创建开关代码防御脚本，使自己不会被关掉
        }
        private void Start()
        {
            //gameObject.GetComponent<Tank>().enabled = false;//模拟自己被关掉的场景
        }
        private float m_LastTime = 0;
        protected override void OnUpdate()
        {
            base.OnUpdate();
            //ControlOppTank();
            EludeBullet();
            AttackOppTank();
        }
        private void ControlOppTank()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            float ControlTime = 5;
            if (Time.time > ControlTime)
            {
                if (!IsControll)
                {
                    oppTank.gameObject.GetComponent<Tank>().enabled = false;//关闭敌方控制器
                    oppTank.gameObject.AddComponent<XJXAttack1>();//修改敌方控制器(有问题)
                    IsControll = true;
                }
            }
        }
        private void EludeBullet()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            RaycastHit hit;
            if (Physics.Raycast(FirePos, oppTank.FirePos - FirePos, out hit, 100))
            {
                if (hit.transform.gameObject.name == "Sphere")
                {
                    //Debug.Log(hit.transform.gameObject.name + "AAAAAAAAAAAA");

                    Vector3 EludeDir = GetVerticalDir(oppTank.FirePos - FirePos);
                    EludeDir.y = 0;
                    //transform.Translate((EludeDir)*Time.deltaTime,Space.Self);
                    Move(Position + EludeDir * 100);
                    Debug.Log(EludeDir * 100);
                }
                else
                {
                    TankMove();
                    Debug.Log("Move");
                }
            }
        }
        public static Vector3 GetVerticalDir(Vector3 _dir)//求垂直方向
        {
            //（_dir.x,_dir.z）与（？，1）垂直，则_dir.x * ？ + _dir.z * 1 = 0
            if (_dir.z == 0)
            {
                return new Vector3(0, 0, -1);
            }
            else
            {
                return new Vector3(-_dir.z, 0, _dir.x);
            }
        }
        private void OnDrawGizmos()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);
            Gizmos.DrawRay(FirePos, oppTank.FirePos - FirePos);
        }
        #region 老方法
        private void TankMove()
        {
            if (HP <= 75)//血量小于50回城
            {
                Move(Match.instance.GetRebornPos(Team));
            }
            else//血量大于50
            {
                bool hasStar = false;
                float nearestDist = float.MaxValue;
                Vector3 nearestStarPos = Vector3.zero;//初始化信息
                foreach (var pair in Match.instance.GetStars())//拿到场景中的所有星星
                {
                    Star s = pair.Value;//获取星星的属性
                    if (s.IsSuperStar)//如果有超级星，获取超级星的位置，break
                    {
                        hasStar = true;
                        nearestStarPos = s.Position;
                        break;
                    }
                    else//使用排序获得所有星星中的最小值
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
                if (hasStar == true)//如果有星星，移动
                {
                    Move(nearestStarPos);
                }
                else//没有星星就向下一个目标点移动
                {
                    if (Time.time > m_LastTime)
                    {
                        if (ApproachNextDestination())
                        {
                            m_LastTime = Time.time + Random.Range(3, 8);
                        }
                    }
                }
            }
        }
        private void AttackOppTank()
        {
            Tank oppTank = Match.instance.GetOppositeTank(Team);//敌方坦克
            if (oppTank != null)
            {
                if (CanSeeOthers(oppTank))//旋转炮台，开火
                {
                    TurretTurnTo(oppTank.Position);
                    Vector3 toTarget = oppTank.Position - FirePos;
                    toTarget.y = 0;
                    toTarget.Normalize();
                    if (Vector3.Dot(TurretAiming, toTarget) > 0.98f)
                    {
                        Fire();
                    }
                }
                else
                {
                    TurretTurnTo(Position + Forward);//炮台默认前方
                }
            }
        }
        #endregion
        protected override void OnReborn()//复活
        {
            base.OnReborn();
            m_LastTime = 0;
        }
        private bool ApproachNextDestination()//保底移动
        {
            float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
            return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
        }
        public override string GetName()
        {
            return "XJX";
        }
    }
}


