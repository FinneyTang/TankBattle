using System.Collections.Generic;
using Main;
using UnityEngine;

namespace MLS
{
    /// <summary>
    /// 条件信息,用于AI状态的切换
    /// </summary>
    public class Conditions
    {
        public const float MaxDis = 70.71f;
        public Tank Self;
        public Tank Enemy
        {
            get
            {
                if (_enemy == null)
                {
                    _enemy = Match.instance.GetOppositeTank(Self.Team);  
                }
                return _enemy;
            }   
        }
        private Tank _enemy;
        
        public Dictionary<int, WeightedStarInfo> starInfos;
        private List<int> _starDelete;
        public Vector3 currentDest;
        public Vector3 EnemyRebornPosition => Match.instance.GetRebornPos(Enemy.Team);
        public Vector3 RebornPosition => Match.instance.GetRebornPos(Self.Team);
    

        public Conditions(Tank tank)
        {
            Self = tank;
            _starDelete = new List<int>();
            starInfos = new Dictionary<int, WeightedStarInfo>();
        }

        /// <summary>
        /// 更新条件信息
        /// </summary>
        public void UpdateConditions()
        {
            //更新场上的星星的信息
            var stars = Match.instance.GetStars();
            var keys = starInfos.Keys;
            //删除
            _starDelete.Clear();
            foreach (var key in keys)
            {
                Star t;
                bool isExist = stars.TryGetValue(key, out t);
                if (!isExist)
                {
                    _starDelete.Add(key);
                }
                else
                {
                    //更新权重
                    starInfos[key].UpdateInfo();
                }
            }
            for (int i = 0; i < _starDelete.Count; i++)
            {
                starInfos.Remove(_starDelete[i]);
            }
            //增加
            var keysInMatch = stars.Keys;
            foreach (var key in keysInMatch)
            {
                WeightedStarInfo t;
                bool isExist = starInfos.TryGetValue(key, out t);
                if (!isExist)
                {
                    Star temp = stars[key];
                    starInfos.Add( key , new WeightedStarInfo(temp,this) );
                }
            }
        }

        public Star GetBestStar()
        {
            if (starInfos.Count == 0)
                return null;
            var keys = starInfos.Keys;
            float highest = float.MinValue;
            int target = 0;
            foreach (var intKey in keys)
            {
                var value = starInfos[intKey].GetTotalPriority();
                if (value > highest)
                {
                    target = intKey;
                    highest = value;
                }
            }
            return starInfos[target].star;
        }
    }
}