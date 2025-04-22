using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using Main;
using UnityEngine;

namespace ZSJ
{
    class MyTank : Tank
    {
        private Information info;
        private Strategy strategy;
        private Act act;
        protected override void OnStart()
        {
            info = new Information();
            strategy = new Strategy();
            act = new Act();
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            strategy.Decide(this);
        }
        public override string GetName()
        {
            return "ZSJTank";
        }
    }
    class Information
    {
        private Dictionary<int, Star> starDic = Match.instance.GetStars();
        public float CaculateDis(NavMeshPath path)
        {
            float sum = 0;
            for(int i=0;i<path.corners.Length-1;i++)
            {
                sum += (path.corners[i] - path.corners[i + 1]).magnitude;
            }
            return sum;
        }
        public List<StarInfo> GetStarInfos(Tank myTank,Tank enemyTank)
        {
            List<StarInfo> starInfos = new List<StarInfo>();
            foreach(var child in starDic)
            {
                Star star = child.Value;
                StarInfo starInfo = new StarInfo();
                starInfo.ID = star.ID;
                starInfo.my_Dis = CaculateDis(myTank.CaculatePath(star.Position));
                if(enemyTank!=null)
                {
                    if(!enemyTank.IsDead)
                    starInfo.enemy_Dis = CaculateDis(enemyTank.CaculatePath(star.Position));
                    else
                    {
                        starInfo.enemy_Dis = 99999;
                    }
                }
                starInfos.Add(starInfo);
            }
            //Debug.Log(starInfos.Count);
            return starInfos;
        }
        public Vector3 GetNearEnemyStar(Tank myTank)
        {
            Tank enemyTank=Match.instance.GetOppositeTank(myTank.Team);
            List<StarInfo> starInfos = new List<StarInfo>();
            starInfos = GetStarInfos(myTank, enemyTank);
            int nearEnemyStarNum = -1;
            for (int i = 0; i < starInfos.Count; i++)
            {
                if (nearEnemyStarNum == -1) nearEnemyStarNum = i;
                else
                {
                    if (starInfos[i].enemy_Dis < starInfos[nearEnemyStarNum].enemy_Dis)
                        nearEnemyStarNum = i;
                }
            }
            if (nearEnemyStarNum != -1)
            {
                return enemyTank.CaculatePath(starDic[starInfos[nearEnemyStarNum].ID].Position).corners[1];
            }
            else return enemyTank.Position;
        }
    }
    class Strategy
    {
        //private bool moveReady=true;
        private Information info=new Information();
        private Act act=new Act();
        private Dictionary<int, Star> starDic = Match.instance.GetStars();
        private List<StarInfo> starInfos = new List<StarInfo>();
        public void Decide(MyTank tank)
        {
            //Debug.Log(moveReady); 
            MoveStrategy(tank);
            BarrelStrategy(tank);
            FireStrategy(tank);
        }
        private void MoveStrategy(MyTank tank)
        {
            //------����ڼҾͻ���Ѫ
            if ((tank.Position - Match.instance.GetRebornPos(tank.Team)).magnitude <= 10)
            {
                if (tank.HP < 100)
                {
                    return;
                }
            }
            //------
            //------������һ��Ŀ������
            starInfos=info.GetStarInfos(tank,Match.instance.GetOppositeTank(tank.Team));
            int nearMyStarNum=-1;
            int nearEnemyStarNum=-1;
            int goalStarID=-1;
            for (int i = 0; i < starInfos.Count; i++)
            {
                if (nearEnemyStarNum == -1) nearEnemyStarNum = i;
                else
                {
                    if (starInfos[i].enemy_Dis < starInfos[nearEnemyStarNum].enemy_Dis)
                        nearEnemyStarNum = i;
                }
            }

            if(nearEnemyStarNum!=-1)
            {
                if(starInfos[nearEnemyStarNum].enemy_Dis>starInfos[nearEnemyStarNum].my_Dis)//���������������ǣ����Ǹ���
                {
                    goalStarID = starInfos[nearEnemyStarNum].ID;
                }
            }
            
            for (int i = 0; i < starInfos.Count; i++) 
            {   
                if(starInfos[i].enemy_Dis < starInfos[i].my_Dis)
                    if (i == nearEnemyStarNum) continue;
                if (nearMyStarNum == -1) nearMyStarNum = i;
                else
                {
                    if (starInfos[i].my_Dis < starInfos[nearMyStarNum].my_Dis)
                        nearMyStarNum = i;
                }
            }
            if(nearMyStarNum!=-1)
            goalStarID = starInfos[nearMyStarNum].ID;
            //------
            //------ǰ��Ŀ������
            if(Danger(tank))//����  �����ڵ����Լ��ľ��� �����ڵ�����ʱ�� �ж��Ƿ����� �����Ƿ�ر�
            {
                
            }
            else if(CheckSpecial(tank))
            {
                act.MoveToCenter(tank);
            }
            else if(tank.HP<=20)
            {
                act.MoveToReborn(tank);
            }
            else if(goalStarID!=-1)
            {
                act.MoveToStar(tank, goalStarID);
            }
            else if(tank.HP>70)
            {
                act.MoveToCenter(tank);
            }
            else if(tank.HP<=70)
            {
                act.MoveToReborn(tank);
            }
            //------
        }
        private void BarrelStrategy(MyTank tank)
        {
            Tank enemyTank;
            Vector3 shootGoal=Vector3.zero;
            Vector3 cPosition = Vector3.zero;
            enemyTank = Match.instance.GetOppositeTank(tank.Team);
            if (enemyTank!=null)
            {
                if((enemyTank.Position-tank.Position).magnitude<20)
                {
                    shootGoal = enemyTank.Position;
                }
                else if ((cPosition=info.GetNearEnemyStar(tank))!= enemyTank.Position)
                {
                    //Debug.Log("on");
                    shootGoal = enemyTank.Position + (cPosition - enemyTank.Position).normalized * (enemyTank.Position - tank.Position).magnitude / 40 * enemyTank.Velocity.magnitude;
                }
                else shootGoal = enemyTank.Position + enemyTank.Velocity * (enemyTank.Position - tank.Position).magnitude / 40;
                tank.TurretTurnTo(shootGoal);
            }
            if (tank.CanFire()&&CheckFire(tank,shootGoal)) tank.Fire();
        }
        bool CheckFire(Tank tank,Vector3 shootGoal)
        {
            Vector3 V1, V2, V3;
            V1 = new Vector3(tank.Position.x,0, tank.Position.z);
            V2 = new Vector3(tank.FirePos.x, 0, tank.FirePos.z);
            V3 = new Vector3(shootGoal.x, 0, shootGoal.z);
            return ((V1 - V2).normalized - (V1 - V3).normalized).magnitude<0.1f;
        }
        private void FireStrategy(MyTank tank)
        {
            //if(tank.CanFire()) tank.Fire();
        }
        bool goal=false;
        private bool CheckSpecial(Tank tank)
        {
            //Debug.Log(goal+" "+ Match.instance.RemainingTime);
            float dis = info.CaculateDis(tank.CaculatePath(new Vector3(0, 0, 0)));
            float v = 10;
            if(Match.instance.RemainingTime<89)
            {
                goal = false;
                return goal;
            }
            if(v!=0)
            {
                if(Match.instance.RemainingTime-(dis/v)<91)
                {
                    goal = true;
                }
            }
            return goal;
        }
        private bool Danger(Tank tank)
        {
            Tank enemyTank;
            enemyTank = Match.instance.GetOppositeTank(tank.Team);
            if (enemyTank != null && !enemyTank.IsDead)
                if ((enemyTank.Position - tank.Position).magnitude < 20)
                    return false;
            Dictionary<int, Missile> missileDic = Match.instance.GetOppositeMissiles(tank.Team);
            foreach(var child in missileDic)
            {
                Missile missile = child.Value;
                //if (!tank.CanSeeOthers(missile.Position)) continue;
                float X = (missile.Velocity - tank.Velocity.normalized * 10f).magnitude;
                float BA = (missile.Position - tank.Position).magnitude;
                float R = 6f;
                if (4 * Vector3.Dot((missile.Velocity - tank.Velocity.normalized * 10f), (missile.Position - tank.Position))
                    * Vector3.Dot((missile.Velocity - tank.Velocity.normalized * 10f), (missile.Position - tank.Position))
                    - 4 * X * X * (BA * BA - R * R) >= 0)
                {
                    float x = missile.Velocity.x;
                    float z = missile.Velocity.z;
                    float t = (missile.Position - tank.Position).magnitude / 40f;
                    Vector3 offset;
                    if (Vector3.Dot(missile.Velocity, (missile.Position - tank.Position)) > 0)
                        return false;
                    if(Vector3.Cross(missile.Velocity,(missile.Position-tank.Position)).y>0)
                    {
                        offset = new Vector3(-z, 0, x);
                    }
                    else
                    {
                        offset = new Vector3(z, 0, -x);
                    }
                    //if (t < 0.1) return false;
                    tank.Move(tank.Position + offset.normalized*R);
                    return true;
                }
            }
            return false;
        }
    }
    class Act
    {
        private Dictionary<int, Star> starDic = Match.instance.GetStars();
        public bool MoveToStar(Tank tank,int starID)
        {
            return tank.Move(tank.CaculatePath(starDic[starID].Position));
        }
        public bool MoveToCenter(Tank tank)
        {
            return tank.Move(tank.CaculatePath(new Vector3(0, 0, 0)));
        }
        public bool MoveToReborn(Tank tank)
        {
            return tank.Move(tank.CaculatePath(Match.instance.GetRebornPos(tank.Team)));
        }
    }
    class StarInfo
    {
        public int ID;
        public float my_Dis;
        public float enemy_Dis;
    }
}
