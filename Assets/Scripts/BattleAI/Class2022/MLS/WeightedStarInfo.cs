using System.Collections.Generic;
using Main;
using UnityEngine;

namespace MLS
{
    /// <summary>
    /// 会根据星星的各个属性，来判断吃这颗星星的优先度，因为游戏规则下获得分数很多来自吃星
    /// （超级星星的优先级更高）
    /// 0.（易获取程度）距自己的距离
    /// 1-0.（敌人先获取可能性）离敌人的距离与离自己距离的比值
    /// 1-1.如果敌人死亡，抢先吃掉敌人家门口的星星，然后再回家回血
    /// 2.附近是否有连续星星（如果吃到一颗，其他几颗星星也是可以轻易获得的）
    /// </summary>
    
    public class WeightedStarInfo
    {
        private List<float> _infos;
        public readonly Star star;
        private Conditions _conditions;


        
        public WeightedStarInfo(Star input,Conditions conditions)
        {
            _conditions = conditions;
            _infos = new List<float>()
            {
                0,
            };
            star = input;
            UpdateInfo();
        }

        public void UpdateInfo()
        {
            Vector3 tankPos = _conditions.Self.Position;
            Vector3 starPos = star.Position;
            //这里需要计算路径距离，而不是直线距离
            float val = 1 - Vector3.Distance(tankPos, starPos) / Conditions.MaxDis;
            _infos[0] = val;
        }
        
        public float GetTotalPriority()
        {
            //TODO:如果敌人死亡，优先将敌人家门口的星星吃掉
            //暂时返回距离
            return _infos[0];
        }
        
        
    }
}