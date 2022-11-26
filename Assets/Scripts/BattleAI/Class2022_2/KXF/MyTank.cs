using System.Collections;
using System.Collections.Generic;
using Main;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace KXF
{
    public class MyTank : Tank
    {
        private Dictionary<EntityKnowledgePoolEnum, object> _entityKnowledge;


        private IsStarExist _isStarExist;
        private IsEnemyDead _isEnemyDead;
        private IsHpGreater _isHpGreater;
        private IsSuperStarExistAfterXSec _isSuperStarExistAfter10Sec;
        private IsSuperStarExistAfterXSec _isSuperStarExistAfter5Sec;
        private IsSuperStarExistAfterXSec _isSuperStarExistAfterN1Sec;

        private float noVoidDistance = 3f;
        // Start is called before the first frame update
        public override string GetName()
        {
            return "KXF ";
        }

        protected override void OnStart()
        {
            base.OnStart();
            //初始化规则
            InitAIConditions();
            InitEntityKnowledgePool();



        }


        protected override void OnUpdate()
        {
            base.OnUpdate();
            //TODO: update knowledge pool
            UpdateEntityKnowledgePool();
            LogEntityKnowledgePool();
            
            
            //TODO: take action
            //选择决策
            DoAction();
        }
        private void DoAction()
        {
            TurretTurnTo((Vector3)_entityKnowledge[EntityKnowledgePoolEnum.NextMissileEnemyPos]);
            
            if (CanFire()) Fire();
            if (_isSuperStarExistAfterN1Sec.IsTrue(this))
            {

                Move(Match.instance.GetStarByID((int) _entityKnowledge[EntityKnowledgePoolEnum.ToNearestStarID]).Position);

            }else
            if (_isSuperStarExistAfter5Sec.IsTrue(this))
            {
                Move(Vector3.zero);
            }else if (_isSuperStarExistAfter10Sec.IsTrue(this))
            {
                if (!_isHpGreater.IsTrue(this) &&  _isEnemyDead.IsTrue(this))
                {
                    Move(Match.instance.GetRebornPos(this.Team));
                }
                else
                {
                    Move(Match.instance.GetStarByID((int) _entityKnowledge[EntityKnowledgePoolEnum.ToNearestStarID]).Position);
                }
            }
            else
            {
                if (this.HP <= 25)
                {
                    Move(Match.instance.GetRebornPos(this.Team));
                }else
                if (this.HP < 50 && _isEnemyDead.IsTrue(this))
                {
                    Move(Match.instance.GetRebornPos(this.Team));

                }
                else
                {
                    var star = Match.instance.GetStarByID(
                        (int) _entityKnowledge[EntityKnowledgePoolEnum.ToNearestStarID]);
                    if (star != null)
                    {
                        Move(star.Position);
                    }
                }
            }
            
    

            //找星星
            //看到敌人 -> 预判位置攻击敌人
            //事件： 10秒后 超级星星
            //如果HP < Enemy.HP && !Enemy.isDead 回家，
            //如果 HP > Enemy ， 向敌人移动
            //Else 找星星

            //事件： 5秒后 超级星星
            // -> 去超级星星

            // 超级星星被吃， 如果score 比对面低， 如果score比对面高

        }

        private void InitAIConditions()
        {
            _isStarExist = new IsStarExist();
            _isEnemyDead = new IsEnemyDead();
            _isHpGreater = new IsHpGreater(Match.instance.GlobalSetting.DamagePerHit);
            _isSuperStarExistAfter10Sec = new IsSuperStarExistAfterXSec(10);
            _isSuperStarExistAfter5Sec = new IsSuperStarExistAfterXSec(5);
            _isSuperStarExistAfterN1Sec = new IsSuperStarExistAfterXSec(-1);


        }


        private void InitEntityKnowledgePool()
        {
            _entityKnowledge = new Dictionary<EntityKnowledgePoolEnum, object>();
            _entityKnowledge.Add(EntityKnowledgePoolEnum.ToEnemyDistance,0f);
            _entityKnowledge.Add(EntityKnowledgePoolEnum.ToNearestStarID, 0);
            _entityKnowledge.Add(EntityKnowledgePoolEnum.ToNearestEnemyStarID, null);
            _entityKnowledge.Add(EntityKnowledgePoolEnum.ToEnemyMissileTime,0f);
            _entityKnowledge.Add(EntityKnowledgePoolEnum.NextMissileEnemyPos, Vector3.zero);
            _entityKnowledge.Add(EntityKnowledgePoolEnum.NextMissileSelfPos, Vector3.zero);
            _entityKnowledge.Add(EntityKnowledgePoolEnum.SafePos, null);
            
        }

        private void UpdateEntityKnowledgePool()
        {
            Tank enemyTank = Match.instance.GetOppositeTank(this.Team);
            
            
            
            
            
            
            _entityKnowledge[EntityKnowledgePoolEnum.ToEnemyDistance] =
                distanceBetween(this.Position, enemyTank.Position);
            _entityKnowledge[EntityKnowledgePoolEnum.ToNearestStarID] = getNearestStarID(this);
            _entityKnowledge[EntityKnowledgePoolEnum.ToNearestEnemyStarID] = getNearestStarID(this);
            _entityKnowledge[EntityKnowledgePoolEnum.ToEnemyMissileTime] =
                (float) _entityKnowledge[EntityKnowledgePoolEnum.ToEnemyDistance] /
                Match.instance.GlobalSetting.MissileSpeed;
            _entityKnowledge[EntityKnowledgePoolEnum.NextMissileEnemyPos] = PosAfterXSec(
                Match.instance.GetOppositeTank(this.Team),
                (float) _entityKnowledge[EntityKnowledgePoolEnum.ToEnemyMissileTime]);
            _entityKnowledge[EntityKnowledgePoolEnum.NextMissileSelfPos] = PosAfterXSec(
                this,
                (float) _entityKnowledge[EntityKnowledgePoolEnum.ToEnemyMissileTime]);


        }


        private void LogEntityKnowledgePool()
        {
            /*Debug.Log("ToEnemyDistance : " + (float)_entityKnowledge[EntityKnowledgePoolEnum.ToEnemyDistance]);
            Debug.Log("ToNearestStarID : " + (int)_entityKnowledge[EntityKnowledgePoolEnum.ToNearestStarID]);
            Debug.Log("ToNearestEnemyStarID : " + (int)_entityKnowledge[EntityKnowledgePoolEnum.ToNearestEnemyStarID]);
            Debug.Log("ToEnemyMissileTime : " + (float)_entityKnowledge[EntityKnowledgePoolEnum.ToEnemyMissileTime]);*/
        }

        // private Missile getNewEnemyMissile()
        // {
        //     
        // }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            Gizmos.color = Color.green;
            //下一次发射导弹的位置
            Gizmos.DrawWireSphere((Vector3)_entityKnowledge[EntityKnowledgePoolEnum.NextMissileEnemyPos], 3f);
            //敌方预判我的位置
            Gizmos.DrawWireSphere((Vector3)_entityKnowledge[EntityKnowledgePoolEnum.NextMissileSelfPos], 3f);

        }


        private enum EntityKnowledgePoolEnum
        {
            ToEnemyDistance,
            //获得离自己最近的星星ID
            ToNearestStarID,
            //获得离敌人最近的星星ID
            ToNearestEnemyStarID,
            ToEnemyMissileTime,
            NextMissileEnemyPos,
            NextMissileSelfPos,
            SafePos
        }

        float distanceBetween(Vector3 pos1, Vector3 pos2)
        {
            return Vector3.Distance(pos1,pos2);
        }

        int getNearestStarID(Tank t)
        {
            Dictionary<int, Star> stars = Match.instance.GetStars();
            if (stars.Count <= 0) return -1;
            float minDis = float.MaxValue;
            int minStarID = -1;
            foreach (var keyValuePair in stars)
            {
                NavMeshPath pathToStar = t.CaculatePath(keyValuePair.Value.Position);
                if (pathToStar != null)
                {
                    float distance = CalcDistance(pathToStar);
                    if (minDis > distance)
                    {
                        minDis = distance;
                        minStarID = keyValuePair.Key;
                    }
                }
            }
            return minStarID;
        }
        //from LGB_Winner 好方法，我偷了
        float CalcDistance(NavMeshPath path)
        {
            float temp_Distance = 0;
            for (int i = 0; i < path.corners.Length - 1; i++)
                temp_Distance += (path.corners[i + 1] - path.corners[i]).magnitude;
            return temp_Distance;
        }

        Vector3 PosAfterXSec(Tank t, float time)
        {
            if (time < 0)
            {
                Debug.Log("time < 0 in PosAfterXSec");
                return Vector3.zero;
            }
            Vector3 currentLocation = t.Position;
            Vector3 distance = time * t.Velocity;

            return currentLocation + distance;
        }
        
        
    }

    
}

